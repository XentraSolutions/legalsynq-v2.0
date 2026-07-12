import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { listEmailAlerts, type OperationalAlert } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

interface SearchParams {
  page?: string;
  status?: string;
  severity?: string;
  alertType?: string;
}

export default async function EmailAlertsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  await requirePlatformAdmin();

  const jar    = await cookies();
  const token  = jar.get(SESSION_COOKIE_NAME)?.value ?? '';
  const params = await searchParams;

  const page = Math.max(1, parseInt(params.page ?? '1', 10));

  let alerts: OperationalAlert[] = [];
  let totalCount = 0;
  let serviceError = false;

  try {
    const result = await listEmailAlerts(token, {
      page,
      pageSize: 50,
      status: params.status,
      severity: params.severity,
      alertType: params.alertType,
    });
    alerts     = result.alerts;
    totalCount = result.totalCount;
  } catch {
    serviceError = true;
  }

  const severityBadge = (severity: string) => {
    switch (severity) {
      case 'Critical': return 'bg-red-100 text-red-800';
      case 'Warning':  return 'bg-yellow-100 text-yellow-800';
      case 'Info':     return 'bg-blue-100 text-blue-800';
      default:         return 'bg-gray-100 text-gray-500';
    }
  };

  const statusBadge = (status: string) => {
    switch (status) {
      case 'Open':         return 'bg-red-50 text-red-700';
      case 'Acknowledged': return 'bg-yellow-50 text-yellow-700';
      case 'Resolved':     return 'bg-green-50 text-green-700';
      case 'Suppressed':   return 'bg-gray-50 text-gray-500';
      default:             return 'bg-gray-50 text-gray-500';
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Operational Alerts</h2>
          <p className="text-sm text-gray-500 mt-1">
            Active, acknowledged, and resolved operational events.
          </p>
        </div>
        <a href="/xenia/email/operations" className="text-xs text-indigo-600 hover:text-indigo-700">
          ← Operations
        </a>
      </div>

      {serviceError ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service.
        </div>
      ) : (
        <>
          {/* Filters */}
          <form className="flex flex-wrap gap-3">
            <select
              name="status"
              defaultValue={params.status ?? ''}
              className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700"
            >
              <option value="">All statuses</option>
              <option value="Open">Open</option>
              <option value="Acknowledged">Acknowledged</option>
              <option value="Resolved">Resolved</option>
              <option value="Suppressed">Suppressed</option>
            </select>
            <select
              name="severity"
              defaultValue={params.severity ?? ''}
              className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700"
            >
              <option value="">All severities</option>
              <option value="Critical">Critical</option>
              <option value="Warning">Warning</option>
              <option value="Info">Info</option>
            </select>
            <button
              type="submit"
              className="rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 transition-colors"
            >
              Filter
            </button>
          </form>

          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100">
              <span className="text-sm font-semibold text-gray-900">
                {totalCount} alert{totalCount !== 1 ? 's' : ''}
              </span>
            </div>

            {alerts.length === 0 ? (
              <div className="px-4 py-10 text-center text-sm text-gray-400">
                {params.status === 'Open' ? 'No open alerts. System is healthy.' : 'No alerts found.'}
              </div>
            ) : (
              <table className="min-w-full divide-y divide-gray-100">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Severity</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Alert</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Source</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Occurrences</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">First Seen</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Last Seen</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 bg-white">
                  {alerts.map(a => (
                    <tr key={a.id} className={`hover:bg-gray-50 transition-colors ${a.isSuppressedNow ? 'opacity-60' : ''}`}>
                      <td className="px-4 py-2.5">
                        <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-semibold ${severityBadge(a.severity)}`}>
                          {a.severity}
                        </span>
                      </td>
                      <td className="px-4 py-2.5">
                        <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${statusBadge(a.status)}`}>
                          {a.status}
                        </span>
                      </td>
                      <td className="px-4 py-2.5">
                        <div className="text-sm font-medium text-gray-900">{a.title}</div>
                        <div className="text-xs text-gray-400">{a.alertType}</div>
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="text-xs text-gray-500">
                          {a.sourceDisplayName ?? a.emailSourceId ?? '—'}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-sm text-gray-700">{a.occurrenceCount}</td>
                      <td className="px-4 py-2.5 text-xs text-gray-500">
                        {new Date(a.firstObservedAt).toLocaleString()}
                      </td>
                      <td className="px-4 py-2.5 text-xs text-gray-500">
                        {new Date(a.lastObservedAt).toLocaleString()}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </div>
  );
}
