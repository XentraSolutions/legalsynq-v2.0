import Link                      from 'next/link';
import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { CCRoutes }               from '@/lib/control-center-routes';
import { notifWebApi }            from '@/lib/notif-web-api';

interface DeliveryEvent {
  id:             string;
  channel:        string;
  recipientId?:   string | null;
  recipientEmail?:string | null;
  status:         string;
  templateKey?:   string | null;
  providerRef?:   string | null;
  failureReason?: string | null;
  occurredAt:     string;
}

const STATUS_COLORS: Record<string, string> = {
  delivered: 'bg-green-50 text-green-700 border-green-200',
  sent:      'bg-green-50 text-green-700 border-green-200',
  failed:    'bg-red-50 text-red-700 border-red-200',
  bounced:   'bg-orange-50 text-orange-700 border-orange-200',
  suppressed:'bg-gray-50 text-gray-600 border-gray-200',
  queued:    'bg-yellow-50 text-yellow-700 border-yellow-200',
};

export default async function NotifLogPage() {
  await requireCCPlatformAdmin();

  let events:     DeliveryEvent[] = [];
  let fetchError: string | null   = null;

  try {
    events = await notifWebApi.get<DeliveryEvent[] | { items: DeliveryEvent[] }>('/delivery/log?pageSize=50')
      .then(r => notifWebApi.unwrap(r as DeliveryEvent[] | { items: DeliveryEvent[] }))
      .catch(() => []);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load delivery log.';
  }

  return (
    <div className="space-y-4">
      <div>
        <div className="flex items-center gap-2 text-sm text-gray-500 mb-1">
          <Link href={CCRoutes.notifications} className="hover:text-indigo-600">Notifications</Link>
          <span>/</span>
          <span className="text-gray-700 font-medium">Delivery Log</span>
        </div>
        <h1 className="text-xl font-semibold text-gray-900">Delivery Log</h1>
        <p className="text-sm text-gray-500 mt-0.5">Recent notification dispatch events — last 50 entries.</p>
      </div>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">{fetchError}</div>
      )}

      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        {events.length === 0 && !fetchError ? (
          <p className="px-4 py-10 text-center text-sm text-gray-400">No delivery events found.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Recipient</th>
                  <th className="px-4 py-2.5 text-left font-medium">Template</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Provider Ref</th>
                  <th className="px-4 py-2.5 text-left font-medium">Failure</th>
                  <th className="px-4 py-2.5 text-left font-medium">Time</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {events.map(e => (
                  <tr key={e.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2.5">
                      <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">
                        {e.channel}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-700">
                      {e.recipientEmail ?? e.recipientId ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-600">
                      {e.templateKey ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${STATUS_COLORS[e.status] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
                        {e.status}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500">
                      {e.providerRef ? e.providerRef.slice(0, 16) + '…' : <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 text-xs text-red-600 max-w-[180px] truncate">
                      {e.failureReason ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(e.occurredAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
