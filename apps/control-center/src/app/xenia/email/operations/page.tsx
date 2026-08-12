import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import {
  getEmailOperationsSummary,
  getAllSourceHealth,
  getAllProviderHealth,
  type OperationsSummary,
  type SourceHealthSnapshot,
  type ProviderHealthSnapshot,
} from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function EmailOperationsDashboardPage() {
  await requirePlatformAdmin();

  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let summary: OperationsSummary | null = null;
  let sources: SourceHealthSnapshot[]   = [];
  let providers: ProviderHealthSnapshot[] = [];
  let serviceError = false;

  try {
    [summary, { items: sources }, { items: providers }] = await Promise.all([
      getEmailOperationsSummary(token),
      getAllSourceHealth(token),
      getAllProviderHealth(token),
    ]);
  } catch {
    serviceError = true;
  }

  const healthBadge = (h: string) => {
    switch (h) {
      case 'Healthy':     return 'bg-green-100 text-green-800';
      case 'Degraded':    return 'bg-yellow-100 text-yellow-800';
      case 'Unavailable': return 'bg-red-100 text-red-800';
      default:            return 'bg-gray-100 text-gray-500';
    }
  };

  const severityBadge = (count: number, type: 'critical' | 'warning') => {
    if (count === 0) return 'bg-gray-100 text-gray-500';
    return type === 'critical' ? 'bg-red-100 text-red-800' : 'bg-yellow-100 text-yellow-800';
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Operations Dashboard</h2>
          <p className="text-sm text-gray-500 mt-1">
            Email ingestion health, alerts, and run metrics.
          </p>
        </div>
        <div className="flex gap-2">
          <a
            href="/xenia/email/runs"
            className="rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 transition-colors"
          >
            View Runs
          </a>
          <a
            href="/xenia/email/alerts"
            className="rounded-md border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 transition-colors"
          >
            View Alerts
          </a>
        </div>
      </div>

      {serviceError ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service. Ensure it is running on port 5035.
        </div>
      ) : (
        <>
          {/* Run metrics */}
          {summary && (
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
              <StatCard label="Total Runs" value={summary.totalRuns} />
              <StatCard label="Successful" value={summary.successfulRuns} accent="green" />
              <StatCard label="Failed" value={summary.failedRuns} accent="red" />
              <StatCard label="Messages Imported" value={summary.totalMessagesImported} />
            </div>
          )}

          {/* Alert summary */}
          {summary && (
            <div className="rounded-lg border border-gray-200 bg-white p-5">
              <div className="flex items-center justify-between mb-3">
                <h3 className="text-sm font-semibold text-gray-900">Active Alerts</h3>
                <a href="/xenia/email/alerts" className="text-xs text-indigo-600 hover:text-indigo-700">
                  Manage →
                </a>
              </div>
              <div className="flex gap-4">
                <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ${severityBadge(summary.criticalAlerts, 'critical')}`}>
                  {summary.criticalAlerts} Critical
                </span>
                <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ${severityBadge(summary.warningAlerts, 'warning')}`}>
                  {summary.warningAlerts} Warning
                </span>
                <span className="inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold bg-gray-100 text-gray-600">
                  {summary.openAlerts} Open Total
                </span>
              </div>
            </div>
          )}

          {/* Source health table */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
              <h3 className="text-sm font-semibold text-gray-900">Source Health</h3>
              <span className="text-xs text-gray-400">{sources.length} sources</span>
            </div>
            {sources.length === 0 ? (
              <div className="px-4 py-8 text-center text-sm text-gray-400">No sources configured.</div>
            ) : (
              <table className="min-w-full divide-y divide-gray-100">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Source</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Health</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Failures</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Last Sync</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Lock Renewals Failed</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 bg-white">
                  {sources.map(s => (
                    <tr key={s.sourceId} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-2.5">
                        <div className="text-sm font-medium text-gray-900">{s.displayName}</div>
                        <div className="text-xs text-gray-400">{s.emailAddress}</div>
                      </td>
                      <td className="px-4 py-2.5">
                        <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${healthBadge(s.healthStatus)}`}>
                          {s.healthStatus}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-sm text-gray-700">
                        {s.consecutiveFailureCount > 0 ? (
                          <span className="font-medium text-red-600">{s.consecutiveFailureCount}</span>
                        ) : (
                          <span className="text-gray-400">0</span>
                        )}
                      </td>
                      <td className="px-4 py-2.5 text-xs text-gray-500">
                        {s.lastSuccessfulSyncAt
                          ? new Date(s.lastSuccessfulSyncAt).toLocaleString()
                          : '—'}
                      </td>
                      <td className="px-4 py-2.5 text-xs text-gray-500">
                        {s.renewalFailureCount != null && s.renewalFailureCount > 0 ? (
                          <span className="text-yellow-600 font-medium">{s.renewalFailureCount}</span>
                        ) : (
                          <span className="text-gray-400">0</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          {/* Provider health summary */}
          {providers.length > 0 && (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100">
                <h3 className="text-sm font-semibold text-gray-900">Provider Health</h3>
              </div>
              <div className="divide-y divide-gray-100">
                {providers.map(p => (
                  <div key={p.providerType} className="px-4 py-3 flex items-center justify-between">
                    <div>
                      <span className="text-sm font-medium text-gray-900">{p.displayName}</span>
                      <span className="ml-2 text-xs text-gray-400">{p.totalSources} sources</span>
                    </div>
                    <div className="flex gap-3 text-xs">
                      <span className="text-green-600">{p.healthySources} healthy</span>
                      {p.degradedSources > 0 && (
                        <span className="text-yellow-600">{p.degradedSources} degraded</span>
                      )}
                      {p.unavailableSources > 0 && (
                        <span className="text-red-600">{p.unavailableSources} unavailable</span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}

function StatCard({ label, value, accent }: { label: string; value: number; accent?: 'green' | 'red' }) {
  const valueClass = accent === 'green'
    ? 'text-green-700'
    : accent === 'red' && value > 0
      ? 'text-red-600'
      : 'text-gray-900';

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <p className="text-xs font-medium text-gray-500 uppercase tracking-wider">{label}</p>
      <p className={`text-2xl font-bold mt-1 ${valueClass}`}>{value.toLocaleString()}</p>
    </div>
  );
}
