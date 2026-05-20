/**
 * GET /api/monitoring/summary
 *
 * Returns a sanitized, public-safe MonitoringSummary: overall system health,
 * per-service integration statuses, and active alerts.
 *
 * This route is intentionally public (used by the /status page and listed in
 * middleware PUBLIC_PATHS). It exposes only the subset of fields that the
 * public status UI renders — internal diagnostic fields (latencyMs, category,
 * alert IDs, resolvedAtUtc) are stripped before sending to the browser.
 *
 * All monitoring logic lives in @/lib/monitoring-source — this route is a
 * thin HTTP adapter and sanitizer. To switch from the local probe engine to the
 * Monitoring Service, set MONITORING_SOURCE=service and implement the
 * 'service' branch in monitoring-source.ts (MON-INT-01-001).
 */
import { NextResponse } from 'next/server';
import { getMonitoringSummary } from '@/lib/monitoring-source';

// ── Public-safe response shapes ────────────────────────────────────────────────
// Only expose the fields the public /status page actually renders.
// Strip: integrations.latencyMs, integrations.category, alerts.id, alerts.resolvedAtUtc

interface PublicIntegrationStatus {
  name:             string;
  status:           string;
  lastCheckedAtUtc: string;
}

interface PublicAlert {
  message:      string;
  severity:     string;
  createdAtUtc: string;
  entityName:   string;
}

interface PublicMonitoringSummaryResponse {
  system: {
    status:           string;
    lastCheckedAtUtc: string;
  };
  integrations: PublicIntegrationStatus[];
  alerts:       PublicAlert[];
}

export async function GET() {
  try {
    const summary = await getMonitoringSummary();

    const safe: PublicMonitoringSummaryResponse = {
      system: {
        status:           summary.system.status,
        lastCheckedAtUtc: summary.system.lastCheckedAtUtc,
      },
      integrations: summary.integrations.map(i => ({
        name:             i.name,
        status:           i.status,
        lastCheckedAtUtc: i.lastCheckedAtUtc,
      })),
      alerts: summary.alerts.map(a => ({
        message:      a.message,
        severity:     a.severity,
        createdAtUtc: a.createdAtUtc,
        entityName:   a.entityName ?? '',
      })),
    };

    return NextResponse.json(safe, {
      headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Monitoring source unavailable';
    console.error('[/api/monitoring/summary]', message);
    return NextResponse.json(
      { error: 'monitoring_unavailable', message },
      {
        status: 503,
        headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
      },
    );
  }
}
