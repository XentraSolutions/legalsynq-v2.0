import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import {
  getEmailSource,
  getEmailSyncState,
  getIngestionHistory,
  type EmailSource,
  type EmailSyncState,
  type IngestionRun,
} from '@/lib/xenia-email-api';
import { notFound } from 'next/navigation';

export const dynamic = 'force-dynamic';

export default async function EmailSourceSyncPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requirePlatformAdmin();

  const { id } = await params;
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let source: EmailSource | null = null;
  let syncState: EmailSyncState | null = null;
  let runs: IngestionRun[] = [];

  try {
    source = await getEmailSource(token, id);
  } catch {
    notFound();
  }
  if (!source) notFound();

  try {
    syncState = await getEmailSyncState(token, id);
  } catch {
    // no sync state yet — source never synced
  }

  try {
    const h = await getIngestionHistory(token, id, 20);
    runs = h.runs ?? [];
  } catch {
    // non-fatal
  }

  const statusColor: Record<string, string> = {
    Completed: 'bg-green-100 text-green-800',
    CompletedWithErrors: 'bg-yellow-100 text-yellow-800',
    Running: 'bg-blue-100 text-blue-800',
    Queued: 'bg-indigo-100 text-indigo-800',
    Failed: 'bg-red-100 text-red-800',
    Cancelled: 'bg-gray-100 text-gray-600',
    Interrupted: 'bg-orange-100 text-orange-800',
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-2">
            <a
              href={`/xenia/email/sources/${id}`}
              className="text-xs text-gray-400 hover:text-gray-600"
            >
              ← {source.displayName}
            </a>
          </div>
          <h2 className="text-xl font-semibold text-gray-900 mt-1">Email Sync</h2>
          <p className="text-sm text-gray-500 mt-0.5">{source.emailAddress}</p>
        </div>
        <form method="POST" action={`/api/xenia/email/sources/${id}/sync`}>
          <button
            type="submit"
            className="inline-flex items-center gap-1.5 rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
          >
            <i className="ri-refresh-line text-sm" />
            Sync Now
          </button>
        </form>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Sync State */}
        <div className="lg:col-span-1">
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Sync State</h3>
            </div>
            {syncState ? (
              <div className="px-4 py-3 space-y-3 text-sm">
                <SyncStateRow label="Provider" value={syncState.providerType} />
                <SyncStateRow label="Cursor" value={syncState.cursorType} />
                {syncState.safeCursorSummary && (
                  <SyncStateRow label="Position" value={syncState.safeCursorSummary} />
                )}
                <SyncStateRow
                  label="Initial sync"
                  value={syncState.initialSyncCompleted ? 'Completed' : 'Pending'}
                />
                {syncState.lastSuccessfulSyncAt && (
                  <SyncStateRow
                    label="Last success"
                    value={new Date(syncState.lastSuccessfulSyncAt).toLocaleString()}
                  />
                )}
                {syncState.consecutiveFailureCount > 0 && (
                  <SyncStateRow
                    label="Failures"
                    value={String(syncState.consecutiveFailureCount)}
                    muted={false}
                    warn
                  />
                )}
                {syncState.nextEligibleSyncAt && (
                  <SyncStateRow
                    label="Next eligible"
                    value={new Date(syncState.nextEligibleSyncAt).toLocaleString()}
                  />
                )}
                {syncState.lastErrorCode && (
                  <div className="pt-2 border-t border-gray-100">
                    <p className="text-xs text-red-500 font-medium">Last Error</p>
                    <p className="text-xs font-mono text-red-700 mt-0.5">{syncState.lastErrorCode}</p>
                    {syncState.safeLastErrorSummary && (
                      <p className="text-xs text-red-600 mt-0.5">{syncState.safeLastErrorSummary}</p>
                    )}
                  </div>
                )}
              </div>
            ) : (
              <div className="px-4 py-8 text-center text-sm text-gray-400">
                No sync state yet. Trigger a sync to initialize.
              </div>
            )}
          </div>
        </div>

        {/* Ingestion History */}
        <div className="lg:col-span-2">
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Recent Runs</h3>
            </div>
            {runs.length === 0 ? (
              <div className="px-4 py-8 text-center text-sm text-gray-400">
                No ingestion runs yet.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-100 text-xs">
                  <thead>
                    <tr className="bg-gray-50">
                      <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Started</th>
                      <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Status</th>
                      <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Trigger</th>
                      <th className="px-4 py-2 text-right font-medium text-gray-500 uppercase">Imported</th>
                      <th className="px-4 py-2 text-right font-medium text-gray-500 uppercase">Duped</th>
                      <th className="px-4 py-2 text-right font-medium text-gray-500 uppercase">Failed</th>
                      <th className="px-4 py-2 text-right font-medium text-gray-500 uppercase">Pages</th>
                      <th className="px-4 py-2 text-right font-medium text-gray-500 uppercase">Duration</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {runs.map((run) => (
                      <tr key={run.id} className="hover:bg-gray-50">
                        <td className="px-4 py-2 text-gray-700">
                          {new Date(run.startedAt).toLocaleString()}
                        </td>
                        <td className="px-4 py-2">
                          <span
                            className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${statusColor[run.status] ?? 'bg-gray-100 text-gray-600'}`}
                          >
                            {run.status}
                          </span>
                        </td>
                        <td className="px-4 py-2 text-gray-500">{run.triggerType}</td>
                        <td className="px-4 py-2 text-right text-gray-700">{run.messagesImported}</td>
                        <td className="px-4 py-2 text-right text-gray-500">{run.messagesDuplicated}</td>
                        <td className={`px-4 py-2 text-right ${run.messagesFailed > 0 ? 'text-red-600 font-medium' : 'text-gray-500'}`}>
                          {run.messagesFailed}
                        </td>
                        <td className="px-4 py-2 text-right text-gray-500">{run.pagesProcessed}</td>
                        <td className="px-4 py-2 text-right text-gray-500">
                          {run.durationMs != null ? `${run.durationMs}ms` : '—'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function SyncStateRow({
  label,
  value,
  muted = true,
  warn = false,
}: {
  label: string;
  value: string;
  muted?: boolean;
  warn?: boolean;
}) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <span className="text-xs font-medium text-gray-500 shrink-0">{label}</span>
      <span className={`text-sm text-right ${warn ? 'text-orange-600 font-medium' : muted ? 'text-gray-500' : 'text-gray-900'}`}>
        {value}
      </span>
    </div>
  );
}
