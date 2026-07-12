import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import {
  getEmailSource,
  getValidationHistory,
  type EmailSource,
  type ValidationHistoryEntry,
} from '@/lib/xenia-email-api';
import { notFound } from 'next/navigation';

export const dynamic = 'force-dynamic';

export default async function EmailSourceDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requirePlatformAdmin();

  const { id } = await params;
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let source: EmailSource | null = null;
  let history: ValidationHistoryEntry[] = [];

  try {
    source = await getEmailSource(token, id);
  } catch {
    notFound();
  }

  if (!source) notFound();

  try {
    const h = await getValidationHistory(token, id, 10);
    history = h.history;
  } catch {
    // Non-fatal — display source without history
  }

  const statusColor: Record<string, string> = {
    Active: 'bg-green-100 text-green-800',
    Disabled: 'bg-gray-100 text-gray-600',
    Error: 'bg-red-100 text-red-800',
    Pending: 'bg-yellow-100 text-yellow-800',
    Validating: 'bg-blue-100 text-blue-800',
  };

  const healthColor: Record<string, string> = {
    Healthy: 'bg-green-100 text-green-800',
    Degraded: 'bg-yellow-100 text-yellow-800',
    Unavailable: 'bg-red-100 text-red-800',
    Unknown: 'bg-gray-100 text-gray-500',
  };

  const validationColor: Record<string, string> = {
    Valid: 'bg-green-100 text-green-800',
    Invalid: 'bg-red-100 text-red-800',
    Pending: 'bg-blue-100 text-blue-800',
    NotValidated: 'bg-gray-100 text-gray-500',
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-2">
            <a
              href="/xenia/email/sources"
              className="text-xs text-gray-400 hover:text-gray-600"
            >
              ← Sources
            </a>
          </div>
          <h2 className="text-xl font-semibold text-gray-900 mt-1">{source.displayName}</h2>
          <p className="text-sm text-gray-500 mt-0.5">{source.emailAddress}</p>
        </div>
        <div className="flex items-center gap-2">
          <span
            className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${statusColor[source.status] ?? 'bg-gray-100 text-gray-600'}`}
          >
            {source.status}
          </span>
          <a
            href={`/xenia/email/sources/${id}/edit`}
            className="inline-flex items-center gap-1 rounded-md border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
          >
            Edit
          </a>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Main info */}
        <div className="lg:col-span-2 space-y-4">
          {/* Configuration */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Configuration</h3>
            </div>
            <dl className="divide-y divide-gray-100 text-sm">
              <DetailRow label="Provider" value={source.providerType} />
              <DetailRow label="Auth Type" value={source.authType} />
              {source.incomingHost && (
                <DetailRow
                  label="Host"
                  value={`${source.incomingHost}${source.incomingPort ? `:${source.incomingPort}` : ''}`}
                />
              )}
              <DetailRow label="TLS" value={source.useTls ? 'Required' : 'Optional'} />
              {source.mailboxFolder && <DetailRow label="Folder" value={source.mailboxFolder} />}
              {source.username && <DetailRow label="Username" value={source.username} />}
              <DetailRow
                label="Secret Reference"
                value={source.hasSecretReference ? '✓ Configured' : 'Not configured'}
                muted={!source.hasSecretReference}
              />
              <DetailRow
                label="OAuth Connection"
                value={source.hasOAuthConnection ? '✓ Configured' : 'Not configured'}
                muted={!source.hasOAuthConnection}
              />
              {source.description && <DetailRow label="Description" value={source.description} />}
            </dl>
          </div>

          {/* Validation History */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Validation History</h3>
            </div>
            {history.length === 0 ? (
              <div className="px-4 py-8 text-center text-sm text-gray-400">
                No validation attempts recorded.
              </div>
            ) : (
              <table className="min-w-full divide-y divide-gray-100 text-xs">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Started</th>
                    <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Result</th>
                    <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Duration</th>
                    <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Error</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {history.map((h) => (
                    <tr key={h.id} className="hover:bg-gray-50">
                      <td className="px-4 py-2 text-gray-700">
                        {new Date(h.startedAt).toLocaleString()}
                      </td>
                      <td className="px-4 py-2">
                        <span
                          className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                            h.result === 'Success'
                              ? 'bg-green-100 text-green-800'
                              : 'bg-red-100 text-red-800'
                          }`}
                        >
                          {h.result}
                        </span>
                      </td>
                      <td className="px-4 py-2 text-gray-600">
                        {h.durationMs != null ? `${h.durationMs}ms` : '—'}
                      </td>
                      <td className="px-4 py-2 text-gray-500">
                        {h.errorCode ?? '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>

        {/* Sidebar */}
        <div className="space-y-4">
          {/* Status */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Status</h3>
            </div>
            <div className="px-4 py-3 space-y-3">
              <StatusItem
                label="Operational"
                badge={statusColor[source.status]}
                value={source.status}
              />
              <StatusItem
                label="Health"
                badge={healthColor[source.healthStatus]}
                value={source.healthStatus}
              />
              <StatusItem
                label="Validation"
                badge={validationColor[source.validationStatus]}
                value={source.validationStatus}
              />
              <div className="pt-2 border-t border-gray-100">
                <p className="text-xs text-gray-500">Enabled</p>
                <p className="text-sm font-medium text-gray-900 mt-0.5">
                  {source.enabled ? 'Yes' : 'No'}
                </p>
              </div>
            </div>
          </div>

          {/* Last activity */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Last Activity</h3>
            </div>
            <div className="px-4 py-3 space-y-2">
              <TimestampItem label="Last Validated" value={source.lastValidatedAt} />
              <TimestampItem label="Last Success" value={source.lastSuccessfulValidationAt} />
              <TimestampItem label="Last Connection" value={source.lastConnectionAt} />
              {source.lastValidationLatencyMs != null && (
                <div>
                  <p className="text-xs text-gray-500">Latency</p>
                  <p className="text-sm font-medium text-gray-900">{source.lastValidationLatencyMs}ms</p>
                </div>
              )}
              {source.lastErrorCode && (
                <div className="pt-2 border-t border-gray-100">
                  <p className="text-xs text-red-500">Last Error</p>
                  <p className="text-xs font-mono text-red-700 mt-0.5">{source.lastErrorCode}</p>
                  {source.lastErrorSummary && (
                    <p className="text-xs text-red-600 mt-0.5">{source.lastErrorSummary}</p>
                  )}
                </div>
              )}
            </div>
          </div>

          {/* Record info */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Record</h3>
            </div>
            <div className="px-4 py-3 space-y-2 text-xs">
              <p className="font-mono text-gray-500 break-all">{source.id}</p>
              <p className="text-gray-400">Version {source.rowVersion}</p>
              <p className="text-gray-400">Created {new Date(source.createdAtUtc).toLocaleDateString()}</p>
              <p className="text-gray-400">Updated {new Date(source.updatedAtUtc).toLocaleDateString()}</p>
            </div>
          </div>
        </div>
      </div>

      <div className="rounded-lg border border-amber-200 bg-amber-50 p-3">
        <p className="text-xs text-amber-800 font-medium">Security note</p>
        <p className="text-xs text-amber-700 mt-0.5">
          Credentials are never stored or displayed here. Only configuration metadata is shown.
          The secret reference and OAuth connection references are opaque identifiers resolved at runtime.
        </p>
      </div>
    </div>
  );
}

function DetailRow({
  label,
  value,
  muted,
}: {
  label: string;
  value: string;
  muted?: boolean;
}) {
  return (
    <div className="flex items-baseline justify-between px-4 py-2.5 gap-4">
      <dt className="text-xs font-medium text-gray-500 shrink-0">{label}</dt>
      <dd className={`text-sm text-right ${muted ? 'text-gray-400' : 'text-gray-900'}`}>{value}</dd>
    </div>
  );
}

function StatusItem({
  label,
  badge,
  value,
}: {
  label: string;
  badge: string;
  value: string;
}) {
  return (
    <div className="flex items-center justify-between">
      <p className="text-xs text-gray-500">{label}</p>
      <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${badge}`}>
        {value}
      </span>
    </div>
  );
}

function TimestampItem({ label, value }: { label: string; value?: string }) {
  return (
    <div>
      <p className="text-xs text-gray-500">{label}</p>
      <p className="text-xs font-medium text-gray-700 mt-0.5">
        {value ? new Date(value).toLocaleString() : '—'}
      </p>
    </div>
  );
}
