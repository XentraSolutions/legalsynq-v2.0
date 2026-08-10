'use client';

import { useState, useEffect, use, type ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { PageHeader } from '@/components/lien/page-header';
import type { EmailMessageDetail } from '@/lib/xenia-email-api';
import { DateDisplay } from '@/components/ui/date-display';

export const dynamic = 'force-dynamic';

const IMPORT_COLORS: Record<string, string> = {
  Imported:  'bg-green-100 text-green-800',
  Duplicate: 'bg-gray-100 text-gray-500',
  Updated:   'bg-blue-100 text-blue-800',
  Failed:    'bg-red-100 text-red-800',
  Pending:   'bg-yellow-100 text-yellow-800',
};

const DISPATCH_COLORS: Record<string, string> = {
  Dispatched: 'bg-green-100 text-green-800',
  Pending:    'bg-yellow-100 text-yellow-800',
  Failed:     'bg-red-100 text-red-800',
  Skipped:    'bg-gray-100 text-gray-500',
};

function formatBytes(b: number) {
  if (b < 1024) return `${b} B`;
  if (b < 1024 * 1024) return `${(b / 1024).toFixed(1)} KB`;
  return `${(b / (1024 * 1024)).toFixed(1)} MB`;
}

function Row({ label, value, mono = false }: { label: string; value?: ReactNode; mono?: boolean }) {
  if (!value) return null;
  return (
    <div className="flex items-baseline gap-4 px-4 py-2.5 border-b border-gray-100 last:border-0">
      <dt className="w-28 shrink-0 text-xs font-medium text-gray-500">{label}</dt>
      <dd className={`text-sm text-gray-900 break-all ${mono ? 'font-mono' : ''}`}>{value}</dd>
    </div>
  );
}

export default function EmailMessageDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const router  = useRouter();

  const [message, setMessage] = useState<EmailMessageDetail | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch(`/api/xenia/email/messages/${id}`)
      .then((r) => {
        if (!r.ok) { router.replace('/lien/settings/email-inbox'); return null; }
        return r.json();
      })
      .then((d) => { if (d) setMessage(d); })
      .catch(() => router.replace('/lien/settings/email-inbox'))
      .finally(() => setLoading(false));
  }, [id, router]);

  if (loading) {
    return <div className="flex items-center justify-center py-20 text-sm text-gray-400">Loading…</div>;
  }
  if (!message) return null;

  const toRecipients = message.recipients?.filter((r) => r.recipientType === 'To') ?? [];
  const ccRecipients = message.recipients?.filter((r) => r.recipientType === 'Cc') ?? [];
  const nonInlineAttachments = message.attachments?.filter((a) => !a.isInline) ?? [];

  return (
    <div className="space-y-5">
      <PageHeader
        title={message.subject ?? '(no subject)'}
        subtitle={`From ${message.fromName ? `${message.fromName} <${message.fromAddress}>` : message.fromAddress ?? '—'}`}
        breadcrumbs={[
          { label: 'Email Inbox', href: '/lien/settings/email-inbox' },
          { label: message.subject ?? '(no subject)' },
        ]}
        badge={
          <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${IMPORT_COLORS[message.importStatus] ?? 'bg-gray-100 text-gray-500'}`}>
            {message.importStatus}
          </span>
        }
      />

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">

          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <p className="text-sm font-semibold text-gray-700">Envelope</p>
            </div>
            <dl>
              <Row label="From"    value={message.fromName ? `${message.fromName} <${message.fromAddress}>` : message.fromAddress} />
              {message.senderAddress && message.senderAddress !== message.fromAddress && (
                <Row label="Sender" value={message.senderAddress} />
              )}
              {toRecipients.length > 0 && (
                <Row label="To" value={toRecipients.map((r) => r.displayName ? `${r.displayName} <${r.emailAddress}>` : r.emailAddress).join(', ')} />
              )}
              {ccRecipients.length > 0 && (
                <Row label="Cc" value={ccRecipients.map((r) => r.emailAddress).join(', ')} />
              )}
              {message.replyToAddresses && <Row label="Reply-To" value={message.replyToAddresses} />}
              {message.sentAt     && <Row label="Sent"     value={<DateDisplay value={message.sentAt} format="datetime" />} />}
              {message.receivedAt && <Row label="Received" value={<DateDisplay value={message.receivedAt} format="datetime" />} />}
              <Row label="Importance" value={message.importance} />
            </dl>
          </div>

          {message.bodyPreview && (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                <p className="text-sm font-semibold text-gray-700">Body Preview</p>
                <span className="text-xs text-gray-400 bg-gray-100 rounded px-1.5 py-0.5">{message.bodyType}</span>
              </div>
              <div className="px-4 py-4">
                <pre className="whitespace-pre-wrap text-sm text-gray-800 font-sans leading-relaxed">
                  {message.bodyPreview}
                </pre>
              </div>
              <div className="px-4 py-2 border-t border-amber-100 bg-amber-50">
                <p className="text-xs text-amber-700">
                  <i className="ri-shield-line mr-1" />
                  HTML bodies are not rendered here — plain-text preview only for security.
                </p>
              </div>
            </div>
          )}

          {nonInlineAttachments.length > 0 && (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                <p className="text-sm font-semibold text-gray-700">
                  Attachments <span className="ml-1 text-gray-400 font-normal">({nonInlineAttachments.length})</span>
                </p>
              </div>
              <ul className="divide-y divide-gray-100">
                {nonInlineAttachments.map((a) => (
                  <li key={a.id} className="flex items-center justify-between px-4 py-3 gap-4">
                    <div className="flex items-center gap-3 min-w-0">
                      <i className="ri-file-line text-gray-400 text-lg shrink-0" />
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-gray-900 truncate">{a.fileName}</p>
                        <p className="text-xs text-gray-400">
                          {a.mimeType ?? 'unknown type'}
                          {a.sizeBytes != null && ` · ${formatBytes(a.sizeBytes)}`}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2 shrink-0">
                      {a.documentReferenceId && (
                        <span className="text-xs text-indigo-600 font-mono bg-indigo-50 rounded px-1.5 py-0.5">
                          doc linked
                        </span>
                      )}
                      <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${DISPATCH_COLORS[a.dispatchStatus] ?? 'bg-gray-100 text-gray-500'}`}>
                        {a.dispatchStatus}
                      </span>
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>

        <div className="space-y-4">
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <p className="text-sm font-semibold text-gray-700">Metadata</p>
            </div>
            <div className="px-4 py-3 space-y-3">
              {[
                { label: 'Import Status', value: message.importStatus },
                { label: 'Body Type',     value: message.bodyType },
                { label: 'Attachments',   value: message.attachmentCount > 0 ? `${message.attachmentCount} file${message.attachmentCount !== 1 ? 's' : ''}` : 'None' },
                { label: 'Read',          value: message.isRead == null ? 'Unknown' : message.isRead ? 'Yes' : 'No' },
              ].map(({ label, value }) => (
                <div key={label}>
                  <p className="text-xs text-gray-500">{label}</p>
                  <p className="text-xs font-medium text-gray-800 mt-0.5">{value}</p>
                </div>
              ))}
              {message.importedAt && (
                <div>
                  <p className="text-xs text-gray-500">Imported at</p>
                  <p className="text-xs font-medium text-gray-800 mt-0.5">
                    <DateDisplay value={message.importedAt} format="datetime" />
                  </p>
                </div>
              )}
            </div>
          </div>

          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <p className="text-sm font-semibold text-gray-700">IDs</p>
            </div>
            <div className="px-4 py-3 space-y-3">
              {[
                { label: 'Message ID',   value: message.id },
                { label: 'Source ID',    value: message.emailSourceId },
                { label: 'Internet MID', value: message.internetMessageId },
                { label: 'Thread ID',    value: message.threadId },
                { label: 'Conv. ID',     value: message.conversationId },
              ].filter((r) => r.value).map(({ label, value }) => (
                <div key={label}>
                  <p className="text-xs text-gray-500">{label}</p>
                  <p className="text-xs font-mono break-all text-gray-600 mt-0.5">{value}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
