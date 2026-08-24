import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { getEmailMessages, type EmailMessageSummary } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

export default async function EmailMessagesPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string>>;
}) {
  await requirePlatformAdmin();

  const sp = await searchParams;
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  const sourceId    = sp.sourceId ?? undefined;
  const fromAddress = sp.fromAddress ?? undefined;
  const subject     = sp.subject ?? undefined;
  const pageOffset  = parseInt(sp.pageOffset ?? '0', 10) || 0;
  const pageSize    = 50;

  let messages: EmailMessageSummary[] = [];
  let totalCount = 0;

  try {
    const result = await getEmailMessages(token, {
      sourceId,
      fromAddress,
      subject,
      pageSize,
      pageOffset,
    });
    messages    = result.messages ?? [];
    totalCount  = result.totalCount ?? 0;
  } catch {
    // non-fatal — show empty state
  }

  const importColor: Record<string, string> = {
    Imported:  'bg-green-100 text-green-800',
    Duplicate: 'bg-gray-100 text-gray-500',
    Updated:   'bg-blue-100 text-blue-800',
    Failed:    'bg-red-100 text-red-800',
    Pending:   'bg-yellow-100 text-yellow-800',
  };

  const importanceIcon: Record<string, string> = {
    High: '🔴',
    Low: '⬇️',
    Normal: '',
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">Ingested Messages</h2>
          <p className="text-sm text-gray-500 mt-0.5">
            {totalCount > 0 ? `${totalCount.toLocaleString()} messages` : 'No messages yet'}
          </p>
        </div>
        <a
          href="/xenia/email/sources"
          className="text-xs text-gray-400 hover:text-gray-600"
        >
          ← Email Sources
        </a>
      </div>

      {/* Filters */}
      <form method="GET" className="flex flex-wrap items-end gap-2">
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">From</label>
          <input
            name="fromAddress"
            defaultValue={fromAddress}
            placeholder="sender@example.com"
            className="rounded border border-gray-300 px-2 py-1 text-sm text-gray-800 w-44"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">Subject</label>
          <input
            name="subject"
            defaultValue={subject}
            placeholder="contains…"
            className="rounded border border-gray-300 px-2 py-1 text-sm text-gray-800 w-44"
          />
        </div>
        <button
          type="submit"
          className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
        >
          Filter
        </button>
        {(fromAddress || subject) && (
          <a
            href="/xenia/email/messages"
            className="text-xs text-gray-400 hover:text-gray-600 self-center"
          >
            Clear
          </a>
        )}
      </form>

      {/* Table */}
      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        {messages.length === 0 ? (
          <div className="px-4 py-12 text-center text-sm text-gray-400">
            No messages found. Trigger a sync from an email source to ingest messages.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100 text-xs">
              <thead>
                <tr className="bg-gray-50">
                  <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Subject</th>
                  <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">From</th>
                  <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Received</th>
                  <th className="px-4 py-2 text-left font-medium text-gray-500 uppercase">Status</th>
                  <th className="px-4 py-2 text-center font-medium text-gray-500 uppercase">Att.</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {messages.map((msg) => (
                  <tr key={msg.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2 max-w-xs">
                      <a
                        href={`/xenia/email/messages/${msg.id}`}
                        className="text-indigo-700 hover:text-indigo-900 font-medium"
                      >
                        {importanceIcon[msg.importance] && (
                          <span className="mr-1">{importanceIcon[msg.importance]}</span>
                        )}
                        {msg.subject ?? <span className="text-gray-400 italic">(no subject)</span>}
                      </a>
                      {msg.bodyPreview && (
                        <p className="text-gray-400 truncate mt-0.5 max-w-[280px]">{msg.bodyPreview}</p>
                      )}
                    </td>
                    <td className="px-4 py-2 text-gray-600">
                      <div>{msg.fromName ?? msg.fromAddress}</div>
                      {msg.fromName && <div className="text-gray-400">{msg.fromAddress}</div>}
                    </td>
                    <td className="px-4 py-2 text-gray-500">
                      {msg.receivedAt ? new Date(msg.receivedAt).toLocaleString() : '—'}
                    </td>
                    <td className="px-4 py-2">
                      <span
                        className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${importColor[msg.importStatus] ?? 'bg-gray-100 text-gray-500'}`}
                      >
                        {msg.importStatus}
                      </span>
                    </td>
                    <td className="px-4 py-2 text-center text-gray-500">
                      {msg.hasAttachments ? (
                        <span title={`${msg.attachmentCount} attachment(s)`}>📎 {msg.attachmentCount}</span>
                      ) : (
                        '—'
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Pagination */}
      {totalCount > pageSize && (
        <div className="flex items-center justify-between text-xs text-gray-500">
          <span>
            Showing {pageOffset + 1}–{Math.min(pageOffset + pageSize, totalCount)} of {totalCount.toLocaleString()}
          </span>
          <div className="flex gap-2">
            {pageOffset > 0 && (
              <a
                href={`?pageOffset=${Math.max(0, pageOffset - pageSize)}${fromAddress ? `&fromAddress=${fromAddress}` : ''}${subject ? `&subject=${subject}` : ''}`}
                className="rounded border border-gray-300 px-2 py-1 hover:bg-gray-50"
              >
                Previous
              </a>
            )}
            {pageOffset + pageSize < totalCount && (
              <a
                href={`?pageOffset=${pageOffset + pageSize}${fromAddress ? `&fromAddress=${fromAddress}` : ''}${subject ? `&subject=${subject}` : ''}`}
                className="rounded border border-gray-300 px-2 py-1 hover:bg-gray-50"
              >
                Next
              </a>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
