import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import {
  listEmailRuns,
  type IngestionRunSummary,
} from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

interface SearchParams {
  page?: string;
  status?: string;
  trigger?: string;
  hasErrors?: string;
  sourceId?: string;
}

export default async function EmailRunsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  await requirePlatformAdmin();

  const jar    = await cookies();
  const token  = jar.get(SESSION_COOKIE_NAME)?.value ?? '';
  const params = await searchParams;

  const page     = Math.max(1, parseInt(params.page ?? '1', 10));
  const pageSize = 50;

  let runs: IngestionRunSummary[] = [];
  let totalCount = 0;
  let serviceError = false;

  try {
    const result = await listEmailRuns(token, {
      page,
      pageSize,
      status: params.status,
      trigger: params.trigger,
      hasErrors: params.hasErrors === 'true' ? true : undefined,
      sourceId: params.sourceId,
    });
    runs       = result.runs;
    totalCount = result.totalCount;
  } catch {
    serviceError = true;
  }

  const statusBadge = (status: string) => {
    switch (status) {
      case 'Completed':  return 'bg-green-100 text-green-800';
      case 'Failed':     return 'bg-red-100 text-red-800';
      case 'Running':    return 'bg-blue-100 text-blue-800';
      case 'Queued':     return 'bg-gray-100 text-gray-600';
      case 'Cancelled':  return 'bg-yellow-100 text-yellow-800';
      default:           return 'bg-gray-100 text-gray-500';
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Ingestion Runs</h2>
          <p className="text-sm text-gray-500 mt-1">History of all email ingestion runs with retry and cancel controls.</p>
        </div>
        <a
          href="/xenia/email/operations"
          className="text-xs text-indigo-600 hover:text-indigo-700"
        >
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
              <option value="Queued">Queued</option>
              <option value="Running">Running</option>
              <option value="Completed">Completed</option>
              <option value="Failed">Failed</option>
              <option value="Cancelled">Cancelled</option>
            </select>
            <select
              name="trigger"
              defaultValue={params.trigger ?? ''}
              className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700"
            >
              <option value="">All triggers</option>
              <option value="Manual">Manual</option>
              <option value="Scheduled">Scheduled</option>
              <option value="EventDriven">Event Driven</option>
            </select>
            <select
              name="hasErrors"
              defaultValue={params.hasErrors ?? ''}
              className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700"
            >
              <option value="">Any errors</option>
              <option value="true">Has errors</option>
              <option value="false">No errors</option>
            </select>
            <button
              type="submit"
              className="rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 transition-colors"
            >
              Filter
            </button>
          </form>

          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
              <span className="text-sm font-semibold text-gray-900">
                {totalCount} run{totalCount !== 1 ? 's' : ''}
              </span>
              {totalPages > 1 && (
                <span className="text-xs text-gray-400">
                  Page {page} of {totalPages}
                </span>
              )}
            </div>

            {runs.length === 0 ? (
              <div className="px-4 py-10 text-center text-sm text-gray-400">
                No runs found matching current filters.
              </div>
            ) : (
              <table className="min-w-full divide-y divide-gray-100">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Source</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Trigger</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Started</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Duration</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Imported</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Errors</th>
                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Retry</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 bg-white">
                  {runs.map(r => (
                    <tr key={r.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-2.5">
                        <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${statusBadge(r.status)}`}>
                          {r.status}
                        </span>
                      </td>
                      <td className="px-4 py-2.5">
                        <div className="text-sm text-gray-900">{r.sourceDisplayName ?? r.emailSourceId.slice(0, 8)}</div>
                        <div className="text-xs text-gray-400">{r.providerType}</div>
                      </td>
                      <td className="px-4 py-2.5 text-xs text-gray-500">{r.triggerType}</td>
                      <td className="px-4 py-2.5 text-xs text-gray-500">
                        {new Date(r.startedAt).toLocaleString()}
                      </td>
                      <td className="px-4 py-2.5 text-xs text-gray-500">
                        {r.durationMs != null ? `${(r.durationMs / 1000).toFixed(1)}s` : '—'}
                      </td>
                      <td className="px-4 py-2.5 text-sm text-gray-700">{r.messagesImported}</td>
                      <td className="px-4 py-2.5">
                        {r.errorCount > 0
                          ? <span className="text-sm font-medium text-red-600">{r.errorCount}</span>
                          : <span className="text-xs text-gray-400">0</span>}
                      </td>
                      <td className="px-4 py-2.5">
                        {r.retryOfRunId && (
                          <span className="rounded-full bg-orange-100 text-orange-700 px-2 py-0.5 text-xs font-medium">
                            Retry
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="px-4 py-3 border-t border-gray-100 flex gap-2 text-xs">
                {page > 1 && (
                  <a
                    href={`?page=${page - 1}${params.status ? `&status=${params.status}` : ''}`}
                    className="text-indigo-600 hover:underline"
                  >
                    ← Prev
                  </a>
                )}
                <span className="text-gray-400">{page} / {totalPages}</span>
                {page < totalPages && (
                  <a
                    href={`?page=${page + 1}${params.status ? `&status=${params.status}` : ''}`}
                    className="text-indigo-600 hover:underline"
                  >
                    Next →
                  </a>
                )}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
