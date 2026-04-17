import { SystemHealthCard } from '@/components/monitoring/system-health-card';
import { IntegrationStatusTable } from '@/components/monitoring/integration-status-table';
import { AlertsPanel } from '@/components/monitoring/alerts-panel';
import type { MonitoringSummary } from '@/types/control-center';

export const dynamic = 'force-dynamic';

async function fetchMonitoringSummary(): Promise<MonitoringSummary> {
  const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const res = await fetch(`${base}/api/monitoring/summary`, {
    cache: 'no-store',
  });
  if (!res.ok) throw new Error(`Health probe failed: ${res.status}`);
  return res.json();
}

export default async function SystemStatusPage() {
  let data:       MonitoringSummary | null = null;
  let fetchError: string | null           = null;

  try {
    data = await fetchMonitoringSummary();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load monitoring data.';
  }

  const hasAlerts       = (data?.alerts.length ?? 0) > 0;
  const criticalCount   = data?.alerts.filter(a => a.severity === 'Critical').length ?? 0;

  const infraServices   = data?.integrations.filter(i => i.category === 'infrastructure') ?? [];
  const productServices = data?.integrations.filter(i => i.category === 'product') ?? [];

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-4xl mx-auto px-6 py-8">

        <div className="mb-6 flex items-start justify-between gap-4">
          <div>
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-md bg-indigo-50 border border-indigo-200 mb-2">
              <span className="text-xs font-semibold text-indigo-700 tracking-wide uppercase">
                LegalSynq
              </span>
            </div>
            <h1 className="text-xl font-semibold text-gray-900">System Health</h1>
            <p className="text-sm text-gray-500 mt-1">
              Real-time view of platform service status and active alerts.
            </p>
          </div>
          {criticalCount > 0 && (
            <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-semibold bg-red-100 text-red-700 border border-red-300 shrink-0">
              {criticalCount} Critical Alert{criticalCount > 1 ? 's' : ''}
            </span>
          )}
        </div>

        {fetchError ? (
          <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
            <p className="text-sm text-red-700 font-medium">Failed to load monitoring data</p>
            <p className="text-xs text-red-600 mt-1">{fetchError}</p>
          </div>
        ) : data ? (
          <div className="space-y-5">

            <SystemHealthCard summary={data.system} />

            <IntegrationStatusTable
              integrations={infraServices}
              title="Platform Services"
              subtitle="Core infrastructure components"
            />

            <IntegrationStatusTable
              integrations={productServices}
              title="Products"
              subtitle="Tenant-facing product services"
            />

            {hasAlerts ? (
              <AlertsPanel alerts={data.alerts} />
            ) : (
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
  );
}
