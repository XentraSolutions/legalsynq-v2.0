import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { getTenantContext }               from '@/lib/auth';
import { CCShell }                        from '@/components/shell/cc-shell';
import { NoTenantContext }                from '@/components/notifications/no-tenant-context';
import { ChannelBadge }                   from '@/components/notifications/channel-badge';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type { NotifContactPolicy }       from '@/lib/notifications-api';

export default async function ContactPoliciesPage() {
  const session   = await requirePlatformAdmin();
  const tenantCtx = getTenantContext();

  if (!tenantCtx) {
    return (
      <CCShell userEmail={session.email}>
        <div className="space-y-4">
          <div>
            <div className="flex items-center gap-2 mb-1">
              <a href="/notifications/contacts/suppressions" className="text-sm text-indigo-600 hover:text-indigo-800">Suppressions</a>
              <span className="text-gray-300">|</span>
              <a href="/notifications/contacts/health" className="text-sm text-indigo-600 hover:text-indigo-800">Health</a>
              <span className="text-gray-300">|</span>
              <span className="text-sm font-semibold text-gray-700">Policies</span>
            </div>
            <h1 className="text-xl font-semibold text-gray-900">Contact Policies</h1>
          </div>
          <NoTenantContext />
        </div>
      </CCShell>
    );
  }

  let policies:   NotifContactPolicy[] = [];
  let fetchError: string | null        = null;

  try {
    const res = await notifClient.get<NotifContactPolicy[] | { items: NotifContactPolicy[] }>(
      '/contacts/policies', 60, [NOTIF_CACHE_TAGS.contacts],
    );
    policies = Array.isArray(res) ? res : (res as { items: NotifContactPolicy[] }).items ?? [];
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load contact policies.';
  }

  const statusCfg: Record<string, string> = {
    active:   'bg-green-50 text-green-700 border-green-200',
    inactive: 'bg-gray-50  text-gray-600  border-gray-200',
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        <div>
          <div className="flex items-center gap-3 mb-2 text-sm">
            <a href="/notifications/contacts/suppressions" className="text-indigo-600 hover:text-indigo-800">Suppressions</a>
            <span className="text-gray-300">|</span>
            <a href="/notifications/contacts/health" className="text-indigo-600 hover:text-indigo-800">Health</a>
            <span className="text-gray-300">|</span>
            <span className="font-semibold text-gray-700">Policies</span>
          </div>
          <h1 className="text-xl font-semibold text-gray-900">Contact Policies</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Contact enforcement policies for <strong>{tenantCtx.tenantName}</strong>.
          </p>
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{fetchError}</div>
        )}

        {!fetchError && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Policy Type</th>
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Config</th>
                  <th className="px-4 py-2.5 text-left font-medium">Created</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {policies.map(p => (
                  <tr key={p.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-700">{p.policyType}</td>
                    <td className="px-4 py-2.5">
                      {p.channel
                        ? <ChannelBadge channel={p.channel} />
                        : <span className="text-xs text-gray-400 italic">All channels</span>}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${statusCfg[p.status] ?? statusCfg['inactive']}`}>
                        {p.status}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-600 max-w-[200px] truncate font-mono">
                      {JSON.stringify(p.config)}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(p.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                    </td>
                  </tr>
                ))}
                {policies.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-4 py-10 text-center text-sm text-gray-400">
                      No contact policies configured for this tenant.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}

      </div>
    </CCShell>
  );
}
