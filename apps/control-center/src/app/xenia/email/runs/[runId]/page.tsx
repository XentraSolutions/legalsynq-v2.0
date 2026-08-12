import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getEmailRun, type IngestionRunDetail } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function RunDetailPage({
  params,
}: {
  params: Promise<{ runId: string }>;
}) {
  await requirePlatformAdmin();

  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';
  const { runId } = await params;

  let run: IngestionRunDetail | null = null;
  let serviceError = false;

  try {
    run = await getEmailRun(token, runId);
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

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Run Detail</h2>
          <p className="text-sm text-gray-500 mt-1 font-mono text-xs">{runId}</p>
        </div>
        <a href="/xenia/email/runs" className="text-xs text-indigo-600 hover:text-indigo-700">
          ← All Runs
        </a>
      </div>

      {serviceError ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Unable to reach the Xenia service.
        </div>
      ) : !run ? (
        <div className="rounded-lg border border-gray-200 bg-white p-8 text-center text-sm text-gray-400">
          Run not found.
        </div>
      ) : (
        <div className="space-y-5">
          {/* Header */}
          <div className="rounded-lg border border-gray-200 bg-white p-5">
            <div className="flex items-start justify-between mb-4">
              <div>
                <div className="flex items-center gap-3">
                  <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusBadge(run.status)}`}>
                    {run.status}
                  </span>
                  <span className="text-xs text-gray-400">{run.triggerType}</span>
                  {run.retryOfRunId && (
                    <span className="rounded-full bg-orange-100 text-orange-700 px-2 py-0.5 text-xs font-medium">
                      Retry of {run.retryOfRunId.slice(0, 8)}
                    </span>
                  )}
                </div>
                <p className="text-sm text-gray-600 mt-2">
                  {run.sourceDisplayName ?? run.emailSourceId} · {run.providerType}
                </p>
              </div>
              {run.status === 'Failed' && (
                <a
                  href={`/xenia/email/runs?action=retry&runId=${run.id}`}
                  className="rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 transition-colors"
                >
                  Retry Run
                </a>
              )}
            </div>

            <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
              <RunMetric label="Started" value={new Date(run.startedAt).toLocaleString()} />
              <RunMetric
                label="Completed"
                value={run.completedAt ? new Date(run.completedAt).toLocaleString() : '—'}
              />
              <RunMetric
                label="Duration"
                value={run.durationMs != null ? `${(run.durationMs / 1000).toFixed(1)}s` : '—'}
              />
              <RunMetric label="Pages Processed" value={String(run.pagesProcessed)} />
            </dl>
          </div>

          {/* Message counts */}
          <div className="rounded-lg border border-gray-200 bg-white p-5">
            <h3 className="text-sm font-semibold text-gray-900 mb-3">Message Counts</h3>
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
              <CountCard label="Imported"   value={run.messagesImported}  accent="green" />
              <CountCard label="Duplicate"  value={run.messagesDuplicate} />
              <CountCard label="Skipped"    value={run.messagesSkipped} />
              <CountCard label="Errors"     value={run.errorCount}         accent={run.errorCount > 0 ? 'red' : undefined} />
            </div>
          </div>

          {/* Cursor info */}
          {(run.cursorBeforeSafeSummary || run.cursorAfterSafeSummary) && (
            <div className="rounded-lg border border-gray-200 bg-white p-5">
              <h3 className="text-sm font-semibold text-gray-900 mb-3">Cursor State</h3>
              <dl className="space-y-2">
                {run.cursorBeforeSafeSummary && (
                  <div>
                    <dt className="text-xs text-gray-500">Before</dt>
                    <dd className="text-xs font-mono text-gray-700 mt-0.5">{run.cursorBeforeSafeSummary}</dd>
                  </div>
                )}
                {run.cursorAfterSafeSummary && (
                  <div>
                    <dt className="text-xs text-gray-500">After</dt>
                    <dd className="text-xs font-mono text-gray-700 mt-0.5">{run.cursorAfterSafeSummary}</dd>
                  </div>
                )}
              </dl>
            </div>
          )}

          {/* Error */}
          {(run.errorCode || run.safeErrorSummary) && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-5">
              <h3 className="text-sm font-semibold text-red-800 mb-2">Error Details</h3>
              {run.errorCode && (
                <p className="text-xs font-mono text-red-700 mb-1">{run.errorCode}</p>
              )}
              {run.safeErrorSummary && (
                <p className="text-sm text-red-700">{run.safeErrorSummary}</p>
              )}
            </div>
          )}

          {/* Metadata */}
          <div className="rounded-lg border border-gray-200 bg-white p-5">
            <h3 className="text-sm font-semibold text-gray-900 mb-3">Metadata</h3>
            <dl className="grid grid-cols-2 gap-3 text-sm">
              <RunMetric label="Run ID"         value={run.id} />
              <RunMetric label="Source ID"      value={run.emailSourceId} />
              <RunMetric label="Retry Count"    value={String(run.retryCount)} />
              {run.correlationId && (
                <RunMetric label="Correlation ID" value={run.correlationId} />
              )}
            </dl>
          </div>
        </div>
      )}
    </div>
  );
}

function RunMetric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-gray-500">{label}</dt>
      <dd className="text-sm font-medium text-gray-900 mt-0.5 break-all">{value}</dd>
    </div>
  );
}

function CountCard({
  label,
  value,
  accent,
}: {
  label: string;
  value: number;
  accent?: 'green' | 'red';
}) {
  const cls =
    accent === 'green' ? 'text-green-700' :
    accent === 'red' && value > 0 ? 'text-red-600' :
    'text-gray-900';

  return (
    <div className="rounded-md bg-gray-50 p-3">
      <dt className="text-xs text-gray-500">{label}</dt>
      <dd className={`text-xl font-bold mt-1 ${cls}`}>{value.toLocaleString()}</dd>
    </div>
  );
}
