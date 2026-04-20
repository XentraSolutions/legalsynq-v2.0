/**
 * monitoring-source.ts — Central abstraction layer for all monitoring data access.
 *
 * Architecture rule: Control Center = read-only consumer.
 * All monitoring data access in the Control Center must go through this module,
 * never directly through system-health-store or probe logic.
 *
 * Source switch (env):
 *   MONITORING_SOURCE=local   → use built-in local probe engine (default, current behavior)
 *   MONITORING_SOURCE=service → delegate to Monitoring Service REST API (MON-INT-01-001)
 *
 * Migration target: MON-INT-01-001 — Monitoring Read API Integration.
 * When the Monitoring Service is integrated, only the 'service' branch below
 * needs to be implemented. The UI, route, and types require zero changes.
 */

import { listServices, type ServiceDef } from '@/lib/system-health-store';
import type {
  MonitoringSummary,
  MonitoringStatus,
  SystemHealthSummary,
  IntegrationStatus,
  SystemAlert,
  AlertSeverity,
} from '@/types/control-center';

// ── Source toggle ─────────────────────────────────────────────────────────────

const MONITORING_SOURCE = process.env.MONITORING_SOURCE ?? 'local';

// ── Local engine types (internal to this module) ──────────────────────────────

interface ProbeResult {
  name:             string;
  status:           MonitoringStatus;
  latencyMs?:       number;
  lastCheckedAtUtc: string;
  category:         string;
  detail?:          string;
}

// ── Monitoring Service types (internal to this module) ───────────────────────
// Matches MonitoredEntityResponse from Monitoring.Api/Contracts/MonitoredEntityResponse.cs

interface MonitoringServiceEntity {
  id:             string;
  name:           string;
  entityType:     string;
  monitoringType: string;
  target:         string;
  isEnabled:      boolean;
  scope:          string;
  impactLevel:    string;
  createdAtUtc:   string;
  updatedAtUtc:   string;
}

// ── Local engine implementation ───────────────────────────────────────────────
// TEMPORARY — local monitoring engine (to be replaced by Monitoring Service)
// MON-INT-01 migration target

/**
 * Execute a single HTTP health probe against a registered service URL.
 * TEMPORARY — this responsibility will move to the Monitoring Service.
 */
async function probeService(svc: ServiceDef): Promise<ProbeResult> {
  const start      = Date.now();
  const controller = new AbortController();
  const timer      = setTimeout(() => controller.abort(), 4000);

  try {
    const res = await fetch(svc.url, {
      signal: controller.signal,
      cache:  'no-store',
    });
    const latencyMs = Date.now() - start;

    let detail: string | undefined;
    try {
      const text = await res.text();
      try {
        const body = JSON.parse(text);
        detail = body.status ?? body.service ?? undefined;
      } catch {
        if (text) detail = text.trim();
      }
    } catch { /* ignore body-read errors */ }

    const status: MonitoringStatus = !res.ok
      ? 'Degraded'
      : latencyMs > 2000
        ? 'Degraded'
        : 'Healthy';

    return { name: svc.name, status, latencyMs, lastCheckedAtUtc: new Date().toISOString(), category: svc.category, detail };
  } catch {
    return {
      name:             svc.name,
      status:           'Down',
      latencyMs:        Date.now() - start,
      lastCheckedAtUtc: new Date().toISOString(),
      category:         svc.category,
      detail:           'Unreachable',
    };
  } finally {
    clearTimeout(timer);
  }
}

/**
 * Run probes against all registered services and aggregate results.
 * TEMPORARY — aggregation will be owned by the Monitoring Service.
 */
async function localGetMonitoringSummary(): Promise<MonitoringSummary> {
  const services = await listServices();
  const results  = await Promise.all(services.map(probeService));

  const downCount     = results.filter(r => r.status === 'Down').length;
  const degradedCount = results.filter(r => r.status === 'Degraded').length;

  const overallStatus: MonitoringStatus =
    downCount     > 0 ? 'Down' :
    degradedCount > 0 ? 'Degraded' :
                        'Healthy';

  const system: SystemHealthSummary = {
    status:           overallStatus,
    lastCheckedAtUtc: new Date().toISOString(),
  };

  const integrations: IntegrationStatus[] = results.map(r => ({
    name:             r.name,
    status:           r.status,
    latencyMs:        r.latencyMs,
    lastCheckedAtUtc: r.lastCheckedAtUtc,
    category:         r.category,
  }));

  const alerts: SystemAlert[] = results
    .filter(r => r.status !== 'Healthy')
    .map(r => ({
      id:           `alert-${r.name.toLowerCase().replace(/\s+/g, '-')}`,
      message:      `${r.name} is ${r.status.toLowerCase()}${r.detail ? `: ${r.detail}` : ''}`,
      severity:     (r.status === 'Down' ? 'Critical' : 'Warning') as AlertSeverity,
      createdAtUtc: r.lastCheckedAtUtc,
    }));

  return { system, integrations, alerts };
}

// ── Service engine implementation ─────────────────────────────────────────────
// MON-INT-01-001 — Monitoring Read API Integration
//
// Calls the Monitoring Service entity registry via the YARP gateway.
// Gateway URL: {GATEWAY_URL}/monitoring/monitoring/entities
//   → YARP strips "/monitoring" prefix
//   → Monitoring Service receives: GET /monitoring/entities
//
// Architecture note: The Monitoring Service (port 5015) exposes entity CRUD
// and a background scheduler that writes check results and alerts to the DB.
// As of MON-INT-01-001, there is no REST endpoint for current-status or alert
// reads — those are stored in EntityCurrentStatus and MonitoringAlerts tables.
// This implementation derives a best-effort summary from entity registry data:
//   - isEnabled=true  → status: 'Healthy' (entity is configured and enabled)
//   - isEnabled=false → status: 'Down'    (entity intentionally disabled)
// Actual health data (Healthy/Degraded/Down from HTTP checks) requires a future
// /monitoring/status read endpoint. Track as MON-INT-01-002.

function buildSummaryFromEntities(entities: MonitoringServiceEntity[]): MonitoringSummary {
  const now = new Date().toISOString();

  const integrations: IntegrationStatus[] = entities.map(e => ({
    name:             e.name,
    status:           (e.isEnabled ? 'Healthy' : 'Down') as MonitoringStatus,
    latencyMs:        undefined,
    lastCheckedAtUtc: e.updatedAtUtc ?? now,
    category:         e.scope || e.entityType,
  }));

  const downCount     = integrations.filter(i => i.status === 'Down').length;
  const overallStatus: MonitoringStatus = downCount > 0 ? 'Down' : 'Healthy';

  const system: SystemHealthSummary = {
    status:           overallStatus,
    lastCheckedAtUtc: now,
  };

  const alerts: SystemAlert[] = integrations
    .filter(i => i.status === 'Down')
    .map(i => ({
      id:           `alert-${i.name.toLowerCase().replace(/\s+/g, '-')}`,
      message:      `${i.name} is disabled`,
      severity:     'Warning' as AlertSeverity,
      createdAtUtc: i.lastCheckedAtUtc,
    }));

  return { system, integrations, alerts };
}

async function serviceGetMonitoringSummary(): Promise<MonitoringSummary> {
  const gatewayBase = process.env.GATEWAY_URL ?? 'http://localhost:5010';
  // Double "monitoring" is intentional: gateway strips the /monitoring prefix,
  // so the outer /monitoring routes to the monitoring-cluster, then the inner
  // /monitoring/entities is the actual service path after prefix removal.
  const url = `${gatewayBase}/monitoring/monitoring/entities`;

  const res = await fetch(url, {
    cache:   'no-store',
    headers: { 'Accept': 'application/json' },
  });

  if (!res.ok) {
    throw new Error(
      `[monitoring-source] Monitoring Service returned HTTP ${res.status} from ${url}. ` +
      `Verify the Monitoring Service is running on port 5015 and ConnectionStrings__MonitoringDb is set.`,
    );
  }

  const entities: MonitoringServiceEntity[] = await res.json();
  return buildSummaryFromEntities(entities);
}

// ── Public API ────────────────────────────────────────────────────────────────

/**
 * Return a complete monitoring summary: overall system status, per-service
 * integration statuses, and active alerts.
 *
 * In 'local' mode: probes registered services directly (current behavior).
 * In 'service' mode: delegates to Monitoring Service REST API (MON-INT-01-001).
 */
export async function getMonitoringSummary(): Promise<MonitoringSummary> {
  if (MONITORING_SOURCE === 'service') {
    return serviceGetMonitoringSummary();
  }

  // Default: local engine (existing behavior, no regression).
  return localGetMonitoringSummary();
}

/**
 * Return only the top-level system health status.
 * Derived from the full summary — convenience helper for dashboard widgets.
 */
export async function getMonitoringStatus(): Promise<SystemHealthSummary> {
  const summary = await getMonitoringSummary();
  return summary.system;
}

/**
 * Return only the active alert list.
 * Derived from the full summary — convenience helper for alert-focused consumers.
 */
export async function getMonitoringAlerts(): Promise<SystemAlert[]> {
  const summary = await getMonitoringSummary();
  return summary.alerts;
}
