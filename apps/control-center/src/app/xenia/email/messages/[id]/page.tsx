import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getEmailMessage, type EmailMessageDetail } from '@/lib/xenia-email-api';
import { notFound } from 'next/navigation';

export const dynamic = 'force-dynamic';

export default async function EmailMessageDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requirePlatformAdmin();

  const { id } = await params;
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  let message: EmailMessageDetail | null = null;
  try {
    message = await getEmailMessage(token, id);
  } catch {
    notFound();
  }
  if (!message) notFound();

  const importColor: Record<string, string> = {
    Imported:  'bg-green-100 text-green-800',
    Duplicate: 'bg-gray-100 text-gray-500',
    Updated:   'bg-blue-100 text-blue-800',
    Failed:    'bg-red-100 text-red-800',
    Pending:   'bg-yellow-100 text-yellow-800',
  };

  const dispatchColor: Record<string, string> = {
    Dispatched: 'bg-green-100 text-green-800',
    Pending:    'bg-yellow-100 text-yellow-800',
    Failed:     'bg-red-100 text-red-800',
    Skipped:    'bg-gray-100 text-gray-500',
  };

  const toRecipients  = message.recipients.filter((r) => r.recipientType === 'To');
  const ccRecipients  = message.recipients.filter((r) => r.recipientType === 'Cc');

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <a
            href="/xenia/email/messages"
            className="text-xs text-gray-400 hover:text-gray-600"
          >
            ← Messages
          </a>
          <h2 className="text-xl font-semibold text-gray-900 mt-1">
            {message.subject ?? <span className="text-gray-400 italic">(no subject)</span>}
          </h2>
          <p className="text-xs text-gray-400 mt-0.5 font-mono break-all">{message.id}</p>
        </div>
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${importColor[message.importStatus] ?? 'bg-gray-100 text-gray-500'}`}
        >
          {message.importStatus}
        </span>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Message detail */}
        <div className="lg:col-span-2 space-y-4">
          {/* Envelope */}
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Envelope</h3>
            </div>
            <dl className="divide-y divide-gray-100 text-sm">
              <DetailRow label="From" value={`${message.fromName ?? ''} <${message.fromAddress ?? '—'}>`} />
              {message.senderAddress && message.senderAddress !== message.fromAddress && (
                <DetailRow label="Sender" value={message.senderAddress} />
              )}
              {toRecipients.length > 0 && (
                <DetailRow label="To" value={toRecipients.map((r) => r.emailAddress).join(', ')} />
              )}
              {ccRecipients.length > 0 && (
                <DetailRow label="Cc" value={ccRecipients.map((r) => r.emailAddress).join(', ')} />
              )}
              {message.sentAt && (
                <DetailRow label="Sent" value={new Date(message.sentAt).toLocaleString()} />
              )}
              {message.receivedAt && (
                <DetailRow label="Received" value={new Date(message.receivedAt).toLocaleString()} />
              )}
              <DetailRow label="Importance" value={message.importance} />
              <DetailRow label="Body Type" value={message.bodyType} />
            </dl>
          </div>

          {/* Preview */}
          {message.bodyPreview && (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                <h3 className="text-sm font-semibold text-gray-700">Body Preview</h3>
              </div>
              <div className="px-4 py-3 text-sm text-gray-700 whitespace-pre-wrap">
                {message.bodyPreview}
              </div>
            </div>
          )}

          {/* Attachments */}
          {message.attachments.length > 0 && (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                <h3 className="text-sm font-semibold text-gray-700">
                  Attachments ({message.attachments.length})
                </h3>
              </div>
              <table className="min-w-full divide-y divide-gray-100 text-xs">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">File</th>
                    <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Type</th>
                    <th className="px-4 py-2 text-right font-medium text-gray-500 uppercase">Size</th>
                    <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Dispatch</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {message.attachments.map((att) => (
                    <tr key={att.id} className="hover:bg-gray-50">
                      <td className="px-4 py-2 text-gray-800 font-medium">
                        {att.fileName}
                        {att.isInline && (
                          <span className="ml-1 text-gray-400 font-normal">(inline)</span>
                        )}
                      </td>
                      <td className="px-4 py-2 text-gray-500">{att.mimeType ?? '—'}</td>
                      <td className="px-4 py-2 text-right text-gray-500">
                        {att.sizeBytes != null ? formatBytes(att.sizeBytes) : '—'}
                      </td>
                      <td className="px-4 py-2">
                        <span
                          className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${dispatchColor[att.dispatchStatus] ?? 'bg-gray-100 text-gray-500'}`}
                        >
                          {att.dispatchStatus}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Sidebar */}
        <div className="space-y-4">
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Ingestion</h3>
            </div>
            <div className="px-4 py-3 space-y-2 text-sm">
              {message.importedAt && (
                <MetaRow label="Imported" value={new Date(message.importedAt).toLocaleString()} />
              )}
              {message.internetMessageId && (
                <MetaRow label="Message-ID" value={message.internetMessageId} mono />
              )}
              {message.threadId && (
                <MetaRow label="Thread ID" value={message.threadId} mono />
              )}
              <MetaRow label="Source ID" value={message.emailSourceId} mono />
            </div>
          </div>

          <div className="rounded-lg border border-amber-200 bg-amber-50 p-3">
            <p className="text-xs font-medium text-amber-800">Security note</p>
            <p className="text-xs text-amber-700 mt-0.5">
              HTML bodies are not rendered here. Body preview shows plain-text only.
              Full HTML access requires explicit authorization.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between px-4 py-2.5 gap-4">
      <dt className="text-xs font-medium text-gray-500 shrink-0">{label}</dt>
      <dd className="text-sm text-right text-gray-900 break-all">{value}</dd>
    </div>
  );
}

function MetaRow({
  label,
  value,
  mono = false,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div>
      <p className="text-xs text-gray-500">{label}</p>
      <p className={`text-xs mt-0.5 break-all text-gray-700 ${mono ? 'font-mono' : ''}`}>{value}</p>
    </div>
  );
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
