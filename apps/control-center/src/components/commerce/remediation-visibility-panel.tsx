'use client';

import { useMemo } from 'react';
import type { CommerceBridgeDiagnostics, RemediationItem, RemediationSummary } from '@/types/control-center';

interface Props {
  diagnostics: CommerceBridgeDiagnostics | null;
}

function severityConfig(severity: RemediationItem['severity']): { icon: string; row: string; badge: string } {
  if (severity === 'warning') {
    return {
      icon:  'ri-error-warning-line text-amber-500',
      row:   'border-amber-200 bg-amber-50/50',
      badge: 'bg-amber-100 text-amber-700',
    };
  }
  return {
    icon:  'ri-information-line text-blue-500',
    row:   'border-slate-200 bg-slate-50/50',
    badge: 'bg-blue-50 text-blue-600',
  };
}

function categoryLabel(cat: RemediationItem['category']): string {
  switch (cat) {
    case 'stale-snapshot':    return 'Stale Snapshot';
    case 'failed-publish':    return 'Failed Publish';
    case 'pending-publish':   return 'Pending Publish';
    case 'missing-profile':   return 'Missing Profile';
    case 'access-mismatch':   return 'Access Mismatch';
    case 'bridge-disabled':   return 'Bridge Disabled';
    default:                  return 'Notice';
  }
}

function buildItems(diag: CommerceBridgeDiagnostics | null): RemediationSummary {
  const items: RemediationItem[] = [];
  const now = new Date().toISOString();

  if (!diag) {
    return { items, warningCount: 0, infoCount: 0, lastCheckedAtUtc: now };
  }

  if (!diag.enabled) {
    items.push({
      id:       'bridge-disabled',
      category: 'bridge-disabled',
      severity: 'info',
      title:    'Commerce bridge is disabled',
      detail:   'Commerce → Tenant Billing entitlement publishing is currently disabled. Set LegalSynq:TenantBillingBridge:Enabled=true to enable.',
      detectedAtUtc: now,
    });
  }

  if (diag.outboxFailedCount > 0) {
    items.push({
      id:       'failed-publish',
      category: 'failed-publish',
      severity: 'warning',
      title:    `${diag.outboxFailedCount} failed publish attempt${diag.outboxFailedCount !== 1 ? 's' : ''}`,
      detail:   `${diag.outboxFailedCount} outbox row${diag.outboxFailedCount !== 1 ? 's are' : ' is'} in a Failed state. These rows will be retried automatically up to MaxAttempts. Check Commerce logs if retries continue failing.`,
      detectedAtUtc: now,
    });
  }

  if (diag.outboxPendingCount > 5) {
    items.push({
      id:       'pending-publish',
      category: 'pending-publish',
      severity: 'info',
      title:    `${diag.outboxPendingCount} publish${diag.outboxPendingCount !== 1 ? 'es' : ''} pending`,
      detail:   `${diag.outboxPendingCount} outbox rows are pending processing. Normal during high activity. Investigate if this count remains elevated after several minutes.`,
      detectedAtUtc: now,
    });
  }

  if (!diag.internalTokenConfigured) {
    items.push({
      id:       'missing-profile',
      category: 'missing-profile',
      severity: 'warning',
      title:    'Billing internal token not configured',
      detail:   'BILLING_INTERNAL_TOKEN is not set in the Commerce service configuration. Entitlement publish calls to Tenant Billing will fail with 401.',
      detectedAtUtc: now,
    });
  }

  if (diag.circuitBreakerEnabled && diag.circuitBreakerState &&
      diag.circuitBreakerState.toLowerCase() !== 'closed') {
    items.push({
      id:       'access-mismatch',
      category: 'access-mismatch',
      severity: 'warning',
      title:    `Circuit breaker is ${diag.circuitBreakerState}`,
      detail:   `The Commerce → Billing circuit breaker state is "${diag.circuitBreakerState}" (expected "Closed"). Publish calls may be blocked. Check Billing service health.`,
      detectedAtUtc: now,
    });
  }

  if (items.length === 0) {
    items.push({
      id:       'all-clear',
      category: 'bridge-disabled',
      severity: 'info',
      title:    'No remediation items detected',
      detail:   'Bridge diagnostics show no outstanding issues. Publish pipeline appears healthy.',
      detectedAtUtc: now,
    });
  }

  const warningCount = items.filter(i => i.severity === 'warning').length;
  const infoCount    = items.filter(i => i.severity === 'info').length;

  return { items, warningCount, infoCount, lastCheckedAtUtc: now };
}

function fmt(iso: string): string {
  if (!iso) return '—';
  try { return new Date(iso).toLocaleString(); } catch { return iso; }
}

export function RemediationVisibilityPanel({ diagnostics }: Props) {
  const summary = useMemo(() => buildItems(diagnostics), [diagnostics]);

  const hasWarnings = summary.warningCount > 0;

  return (
    <section className="bg-white border border-slate-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 bg-slate-50 border-b border-slate-200 flex items-center gap-2">
        <i className={`${hasWarnings ? 'ri-error-warning-line text-amber-500' : 'ri-shield-check-line text-emerald-500'}`} />
        <h2 className="text-sm font-semibold text-slate-700">Remediation Visibility</h2>
        <span className="ml-auto text-xs text-slate-400">
          {summary.warningCount > 0
            ? `${summary.warningCount} warning${summary.warningCount !== 1 ? 's' : ''}`
            : 'No warnings'}
        </span>
      </div>

      <div className="p-5 space-y-2">
        <p className="text-xs text-slate-500 mb-3">
          Read-only visibility of potential operational issues derived from bridge diagnostics.
          No automatic remediation is performed.
        </p>

        {summary.items.map(item => {
          const cfg = severityConfig(item.severity);
          return (
            <div
              key={item.id}
              className={`flex items-start gap-3 rounded-md border px-4 py-3 ${cfg.row}`}
            >
              <i className={`${cfg.icon} text-base mt-0.5 shrink-0`} />
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap mb-0.5">
                  <span className="text-xs font-medium text-slate-800">{item.title}</span>
                  <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded ${cfg.badge}`}>
                    {categoryLabel(item.category)}
                  </span>
                </div>
                <p className="text-xs text-slate-600">{item.detail}</p>
              </div>
            </div>
          );
        })}

        <div className="mt-2 text-xs text-slate-400 pt-2 border-t border-slate-100">
          Derived from bridge diagnostics checked at: {fmt(diagnostics?.lastCheckedAtUtc ?? summary.lastCheckedAtUtc)}
        </div>
      </div>
    </section>
  );
}
