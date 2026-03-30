import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { SystemHealthCard } from '@/components/monitoring/system-health-card';
import { IntegrationStatusTable } from '@/components/monitoring/integration-status-table';
import { AlertsPanel } from '@/components/monitoring/alerts-panel';
import type { MonitoringSummary } from '@/types/control-center';

/**
 * /monitoring — System Health Dashboard.
 *
 * Access: PlatformAdmin only.
 * Data: served from mock stub in controlCenterServerApi.monitoring.getSummary().
 * TODO: When GET /platform/monitoring/summary is live, the stub auto-wires —
 *       no page change needed, only the API method in control-center-api.ts.
 *
 * Layout (top → bottom):
 *  - Overall system health banner (SystemHealthCard)
 *  - Integration health table  (IntegrationStatusTable)
 *  - Active alerts list        (AlertsPanel)
 */
export default async function MonitoringPage() {
  const session = await requirePlatformAdmin();

  let data:       MonitoringSummary | null = null;
  let fetchError: string | null           = null;

  try {
    data = await controlCenterServerApi.monitoring.getSummary();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load monitoring data.';
  }

  const hasAlerts       = (data?.alerts.length ?? 0) > 0;
  const criticalCount   = data?.alerts.filter(a => a.severity === 'Critical').length ?? 0;

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-4xl mx-auto px-6 py-8">

          {/* Page header */}
          <div className="mb-6 flex items-start justify-between gap-4">
            <div>
              <div className="flex items-center gap-3">
                <h1 className="text-xl font-semibold text-gray-900">System Health</h1>
                <span className="inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-700">
                  IN PROGRESS
                </span>
              </div>
              <p className="text-sm text-gray-500 mt-1">
                Real-time view of platform service status and active alerts.
              </p>
            </div>
            {criticalCount > 0 && (
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-semibold bg-red-100 text-red-700 border border-red-300 shrink-0">
                <span aria-hidden>🔴</span>
                {criticalCount} Critical Alert{criticalCount > 1 ? 's' : ''}
              </span>
            )}
          </div>

          {/* Error state */}
          {fetchError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
              <p className="text-sm text-red-700 font-medium">Failed to load monitoring data</p>
              <p className="text-xs text-red-600 mt-1">{fetchError}</p>
            </div>
          ) : data ? (
            <div className="space-y-5">

              {/* Overall health banner */}
              <SystemHealthCard summary={data.system} />

              {/* Integrations table */}
              <IntegrationStatusTable integrations={data.integrations} />

              {/* Alerts — always shown; panel handles empty state */}
              {hasAlerts && (
                <AlertsPanel alerts={data.alerts} />
              )}

              {!hasAlerts && (
                <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
                  <p className="text-sm text-gray-500 text-center py-4">
                    No active alerts.
                  </p>
                </div>
              )}
            </div>
          ) : null}

        </div>
      </div>
    </CCShell>
  );
}
