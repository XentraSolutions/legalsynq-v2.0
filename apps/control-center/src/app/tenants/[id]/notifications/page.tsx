import Link                            from 'next/link';
import { requirePlatformAdmin }        from '@/lib/auth-guards';
import { controlCenterServerApi }      from '@/lib/control-center-api';
import { CCShell }                     from '@/components/shell/cc-shell';
import { Routes }                      from '@/lib/routes';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type { NotifListResponse, NotifStats } from '@/lib/notifications-api';
import { NotificationStatusBadge }     from '@/components/notifications/status-badge';
import { ChannelBadge }                from '@/components/notifications/channel-badge';
import type { TenantStatus as TStatus, TenantType as TType } from '@/types/control-center';

interface Props {
  params:       { id: string };
  searchParams: {
    status?:  string;
    channel?: string;
    page?:    string;
    actorId?: string;
    category?: string;
  };
}

const PAGE_SIZE      = 20;
const ACTIVITY_SIZE  = 15;

const STATUS_OPTIONS  = ['', 'accepted', 'processing', 'sent', 'failed', 'blocked'];
const CHANNEL_OPTIONS = ['', 'email', 'sms', 'push', 'in-app'];

const CATEGORY_TABS = [
  { label: 'All',           value: '' },
  { label: 'Access',        value: 'Security' },
  { label: 'Admin Actions', value: 'Administrative' },
];

function parseRecipient(j: string): string {
  try { const r = JSON.parse(j); return r.email ?? r.phone ?? r.address ?? '—'; }
  catch { return '—'; }
}

function fmtUtc(iso: string): string {
  try {
    const d = new Date(iso);
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getUTCFullYear()}-${p(d.getUTCMonth()+1)}-${p(d.getUTCDate())} ${p(d.getUTCHours())}:${p(d.getUTCMinutes())}:${p(d.getUTCSeconds())}`;
  } catch { return iso; }
}

export default async function TenantNotificationsPage({ params, searchParams }: Props) {
  const session = await requirePlatformAdmin();
  const { id }  = params;

  const status   = searchParams.status   ?? '';
  const channel  = searchParams.channel  ?? '';
  const actorId  = searchParams.actorId  ?? '';
  const category = searchParams.category ?? '';
  const page     = Math.max(1, parseInt(searchParams.page ?? '1', 10));

  // ── Fetch tenant metadata ──────────────────────────────────────────────────
  let tenant:     Awaited<ReturnType<typeof controlCenterServerApi.tenants.getById>> = null;
  let tenantErr:  string | null = null;
  try { tenant = await controlCenterServerApi.tenants.getById(id); }
  catch (e) { tenantErr = e instanceof Error ? e.message : 'Failed to load tenant.'; }

  const tenantName = tenant?.displayName ?? id;

  // ── Fetch notification stats for this tenant ───────────────────────────────
  let stats:     NotifStats | null = null;
  let statsErr:  string | null = null;
  try {
    stats = await notifClient
      .get<{ data: NotifStats }>(`/notifications/stats?tenantId=${encodeURIComponent(id)}`, 30, [NOTIF_CACHE_TAGS.notifications])
      .then(r => r.data)
      .catch(() => null);
  } catch (e) {
    statsErr = e instanceof Error ? e.message : 'Notification stats unavailable.';
  }

  // ── Fetch notification logs for this tenant ────────────────────────────────
  const notifQs = new URLSearchParams();
  notifQs.set('tenantId', id);
  notifQs.set('limit',    String(PAGE_SIZE));
  notifQs.set('offset',   String((page - 1) * PAGE_SIZE));
  if (status)  notifQs.set('status',  status);
  if (channel) notifQs.set('channel', channel);

  let notifData:  NotifListResponse | null = null;
  let notifErr:   string | null = null;
  try {
    notifData = await notifClient.get<NotifListResponse>(
      `/notifications?${notifQs.toString()}`,
      0,
      [NOTIF_CACHE_TAGS.notifications],
    );
  } catch (e) {
    notifErr = e instanceof Error ? e.message : 'Failed to load notification logs.';
  }

  const notifItems  = notifData?.data      ?? [];
  const notifTotal  = notifData?.meta?.total ?? 0;
  const notifPages  = Math.max(1, Math.ceil(notifTotal / PAGE_SIZE));

  // ── Fetch user activity for this tenant ───────────────────────────────────
  let activityItems: Awaited<ReturnType<typeof controlCenterServerApi.auditCanonical.list>>['items'] = [];
  let activityTotal  = 0;
  let activityErr:   string | null = null;
  try {
    const r = await controlCenterServerApi.auditCanonical.list({
      tenantId:  id,
      page:      1,
      pageSize:  ACTIVITY_SIZE,
      category:  category || undefined,
      actorId:   actorId  || undefined,
    });
    activityItems = r.items;
    activityTotal = r.totalCount;
  } catch (e) {
    activityErr = e instanceof Error ? e.message : 'Failed to load user activity.';
  }

  // ── URL builders ──────────────────────────────────────────────────────────
  const base = Routes.tenantNotifications(id);

  function filterHref(overrides: Record<string, string>) {
    const q = new URLSearchParams({ page: '1', status, channel, actorId, category, ...overrides });
    if (!q.get('status'))   q.delete('status');
    if (!q.get('channel'))  q.delete('channel');
    if (!q.get('actorId'))  q.delete('actorId');
    if (!q.get('category')) q.delete('category');
    if (q.get('page') === '1') q.delete('page');
    const s = q.toString();
    return `${base}${s ? `?${s}` : ''}`;
  }

  function pageHref(p: number) {
    return filterHref({ page: String(p) });
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">

        {/* Breadcrumb */}
        <nav className="flex items-center gap-1.5 text-sm text-gray-500">
          <Link href={Routes.tenants} className="hover:text-gray-900 transition-colors">Tenants</Link>
          <span className="text-gray-300">›</span>
          <Link href={Routes.tenantDetail(id)} className="hover:text-gray-900 transition-colors">{tenantName}</Link>
          <span className="text-gray-300">›</span>
          <span className="text-gray-900 font-medium">Notifications</span>
        </nav>

        {tenantErr && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">{tenantErr}</div>
        )}

        {/* Page header */}
        {tenant && (
          <div className="flex items-start justify-between gap-4">
            <div className="space-y-1">
              <div className="flex items-center gap-3">
                <h1 className="text-xl font-semibold text-gray-900">{tenantName}</h1>
                <TenantStatusBadge status={tenant.status} />
              </div>
              <p className="text-sm text-gray-500">
                <span className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-600">{tenant.code}</span>
                <span className="ml-2">{formatType(tenant.type)}</span>
              </p>
            </div>
          </div>
        )}

        {/* Sub-navigation tabs */}
        <div className="flex items-center gap-0 border-b border-gray-200">
          <SubNavLink href={Routes.tenantDetail(id)}         label="Overview"      active={false} />
          <SubNavLink href={Routes.tenantUsers_(id)}         label="Users"         active={false} />
          <SubNavLink href={Routes.tenantNotifications(id)}  label="Notifications" active />
        </div>

        {/* ── Section 1: Stats ──────────────────────────────────────────────── */}
        <div>
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">
            Notification Activity — {tenantName}
          </p>

          {statsErr && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 mb-3">
              Stats unavailable: {statsErr}
            </div>
          )}

          {stats ? (
            <div className="space-y-3">
              {/* All-time stats */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                {[
                  { label: 'Total (all time)', value: (stats.total ?? 0).toLocaleString(), color: 'text-indigo-700' },
                  { label: 'Sent',             value: (stats.byStatus?.sent    ?? 0).toLocaleString(), color: 'text-green-700' },
                  { label: 'Failed',           value: (stats.byStatus?.failed  ?? 0).toLocaleString(), color: 'text-red-700' },
                  { label: 'Blocked',          value: (stats.byStatus?.blocked ?? 0).toLocaleString(), color: 'text-amber-700' },
                ].map(c => (
                  <div key={c.label} className="rounded-lg border border-gray-200 bg-white px-4 py-3">
                    <p className="text-xs text-gray-500 mb-1">{c.label}</p>
                    <p className={`text-2xl font-bold ${c.color}`}>{c.value}</p>
                  </div>
                ))}
              </div>
              {/* Last 24h */}
              {stats.last24h && (
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                  {[
                    { label: 'Last 24h — total',   value: stats.last24h.total.toLocaleString(),   color: 'text-gray-800' },
                    { label: 'Last 24h — sent',    value: stats.last24h.sent.toLocaleString(),    color: 'text-green-700' },
                    { label: 'Last 24h — failed',  value: stats.last24h.failed.toLocaleString(),  color: 'text-red-700' },
                    { label: 'Last 24h — blocked', value: stats.last24h.blocked.toLocaleString(), color: 'text-amber-700' },
                  ].map(c => (
                    <div key={c.label} className="rounded-lg border border-gray-200 bg-white px-4 py-3">
                      <p className="text-xs text-gray-500 mb-1">{c.label}</p>
                      <p className={`text-2xl font-bold ${c.color}`}>{c.value}</p>
                    </div>
                  ))}
                </div>
              )}
            </div>
          ) : !statsErr ? (
            <div className="rounded-lg border border-gray-200 bg-white px-6 py-6 text-center">
              <p className="text-sm text-gray-400">No notification statistics available for this tenant.</p>
            </div>
          ) : null}
        </div>

        {/* ── Section 2: Notification Logs ─────────────────────────────────── */}
        <div>
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">Delivery Log</p>

          {/* Filter bar */}
          <div className="flex flex-wrap gap-4 items-start bg-white border border-gray-200 rounded-lg px-4 py-3 mb-3">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-xs font-medium text-gray-600 whitespace-nowrap">Status</span>
              <div className="flex gap-1 flex-wrap">
                {STATUS_OPTIONS.map(s => (
                  <Link key={s || '__all'} href={filterHref({ status: s, page: '1' })}
                    className={`px-2.5 py-1 rounded-md text-xs font-medium border transition-colors ${
                      status === s
                        ? 'bg-indigo-600 text-white border-indigo-600'
                        : 'bg-white text-gray-600 border-gray-300 hover:border-gray-400'
                    }`}
                  >
                    {s || 'All'}
                  </Link>
                ))}
              </div>
            </div>
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-xs font-medium text-gray-600 whitespace-nowrap">Channel</span>
              <div className="flex gap-1 flex-wrap">
                {CHANNEL_OPTIONS.map(c => (
                  <Link key={c || '__all'} href={filterHref({ channel: c, page: '1' })}
                    className={`px-2.5 py-1 rounded-md text-xs font-medium border transition-colors ${
                      channel === c
                        ? 'bg-indigo-600 text-white border-indigo-600'
                        : 'bg-white text-gray-600 border-gray-300 hover:border-gray-400'
                    }`}
                  >
                    {c || 'All'}
                  </Link>
                ))}
              </div>
            </div>
          </div>

          {notifErr && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 mb-3">{notifErr}</div>
          )}

          {!notifErr && (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              {notifTotal > 0 && (
                <div className="px-4 py-2.5 border-b border-gray-100 bg-gray-50">
                  <p className="text-xs text-gray-500">
                    {notifTotal.toLocaleString()} notification{notifTotal !== 1 ? 's' : ''}
                    {(status || channel) && <span className="ml-1 text-indigo-600">(filtered)</span>}
                  </p>
                </div>
              )}
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">ID</th>
                      <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                      <th className="px-4 py-2.5 text-left font-medium">Recipient</th>
                      <th className="px-4 py-2.5 text-left font-medium">Subject / Template</th>
                      <th className="px-4 py-2.5 text-left font-medium">Status</th>
                      <th className="px-4 py-2.5 text-left font-medium">Provider</th>
                      <th className="px-4 py-2.5 text-left font-medium whitespace-nowrap">Created (UTC)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {notifItems.map(n => {
                      const recipient = parseRecipient(n.recipientJson);
                      const subject   = n.renderedSubject ?? n.templateKey ?? null;
                      return (
                        <tr key={n.id} className="hover:bg-gray-50">
                          <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                            <a href={`/notifications/log/${n.id}`} className="hover:text-indigo-600 hover:underline">
                              {n.id.slice(0, 8)}…
                            </a>
                          </td>
                          <td className="px-4 py-2.5"><ChannelBadge channel={n.channel} /></td>
                          <td className="px-4 py-2.5 font-mono text-[11px] text-gray-700 max-w-[160px] truncate" title={recipient}>
                            {recipient}
                          </td>
                          <td className="px-4 py-2.5 text-xs text-gray-700 max-w-[200px]">
                            {subject
                              ? <span className="truncate block" title={subject}>{subject}</span>
                              : <span className="text-gray-400 italic">—</span>
                            }
                          </td>
                          <td className="px-4 py-2.5"><NotificationStatusBadge status={n.status} /></td>
                          <td className="px-4 py-2.5 text-xs text-gray-600 whitespace-nowrap">
                            {n.providerUsed ?? <span className="text-gray-400 italic">—</span>}
                          </td>
                          <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                            {new Date(n.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                          </td>
                        </tr>
                      );
                    })}
                    {notifItems.length === 0 && (
                      <tr>
                        <td colSpan={7} className="px-4 py-10 text-center text-sm text-gray-400">
                          No notifications found for this tenant.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Pagination */}
          {notifPages > 1 && (
            <div className="flex items-center justify-between text-sm mt-3">
              <span className="text-xs text-gray-500">Page {page} of {notifPages} · {notifTotal.toLocaleString()} total</span>
              <div className="flex gap-2">
                {page > 1 && (
                  <Link href={pageHref(page - 1)}
                    className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 hover:bg-gray-50 text-xs font-medium">
                    ← Previous
                  </Link>
                )}
                {page < notifPages && (
                  <Link href={pageHref(page + 1)}
                    className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 hover:bg-gray-50 text-xs font-medium">
                    Next →
                  </Link>
                )}
              </div>
            </div>
          )}
        </div>

        {/* ── Section 3: User Activity Logs ────────────────────────────────── */}
        <div>
          <div className="flex items-center justify-between mb-3">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">User Activity</p>
            <a
              href={`/synqaudit/user-activity?tenantId=${encodeURIComponent(id)}`}
              className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
            >
              Full investigation →
            </a>
          </div>

          {/* Category filter */}
          <div className="flex gap-1 bg-gray-100 p-1 rounded-lg w-fit mb-3">
            {CATEGORY_TABS.map(tab => (
              <Link
                key={tab.value}
                href={filterHref({ category: tab.value, page: '1' })}
                className={[
                  'inline-flex items-center px-3 py-1.5 rounded-md text-xs font-medium transition-colors whitespace-nowrap',
                  category === tab.value
                    ? 'bg-white text-gray-900 shadow-sm'
                    : 'text-gray-600 hover:text-gray-900',
                ].join(' ')}
              >
                {tab.label}
              </Link>
            ))}
          </div>

          {activityErr && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">{activityErr}</div>
          )}

          {!activityErr && (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              {activityTotal > 0 && (
                <div className="px-4 py-2.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                  <p className="text-xs text-gray-500">
                    {activityTotal.toLocaleString()} event{activityTotal !== 1 ? 's' : ''}
                    {category && <span className="ml-1 text-indigo-600">(filtered: {category})</span>}
                  </p>
                  <p className="text-xs text-gray-400">Showing latest {Math.min(ACTIVITY_SIZE, activityTotal)}</p>
                </div>
              )}
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium whitespace-nowrap">Time (UTC)</th>
                      <th className="px-4 py-2.5 text-left font-medium whitespace-nowrap">Event</th>
                      <th className="px-4 py-2.5 text-left font-medium whitespace-nowrap">Category</th>
                      <th className="px-4 py-2.5 text-left font-medium whitespace-nowrap">Severity</th>
                      <th className="px-4 py-2.5 text-left font-medium whitespace-nowrap">Actor</th>
                      <th className="px-4 py-2.5 text-left font-medium whitespace-nowrap">Description</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {activityItems.map(e => (
                      <tr key={e.id} className="hover:bg-gray-50">
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                          {fmtUtc(e.occurredAtUtc)}
                        </td>
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-700 max-w-[180px]">
                          <span className="block truncate" title={e.eventType}>{e.eventType}</span>
                        </td>
                        <td className="px-4 py-2.5"><CategoryBadge value={e.category} /></td>
                        <td className="px-4 py-2.5"><SeverityBadge value={e.severity} /></td>
                        <td className="px-4 py-2.5 whitespace-nowrap">
                          <span className="block text-xs font-medium text-gray-700">
                            {e.actorLabel ?? (e.actorId ? '—' : <span className="text-gray-400 italic text-[11px]">system</span>)}
                          </span>
                          {e.actorId && (
                            <a
                              href={`/synqaudit/user-activity?actorId=${encodeURIComponent(e.actorId)}`}
                              className="block font-mono text-[10px] text-indigo-500 hover:underline"
                            >
                              {e.actorId.slice(0, 16)}…
                            </a>
                          )}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-gray-500 max-w-xs truncate" title={e.description}>
                          {e.description}
                        </td>
                      </tr>
                    ))}
                    {activityItems.length === 0 && (
                      <tr>
                        <td colSpan={6} className="px-4 py-10 text-center text-sm text-gray-400">
                          No activity events recorded for this tenant.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>

      </div>
    </CCShell>
  );
}

// ── Local helpers ─────────────────────────────────────────────────────────────

function SubNavLink({ href, label, active }: { href: string; label: string; active: boolean }) {
  return (
    <Link
      href={href}
      className={[
        'px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors',
        active
          ? 'border-indigo-600 text-indigo-700'
          : 'border-transparent text-gray-600 hover:text-gray-900 hover:border-gray-300',
      ].join(' ')}
    >
      {label}
    </Link>
  );
}

function TenantStatusBadge({ status }: { status: TStatus }) {
  const styles: Record<TStatus, string> = {
    Active:    'bg-green-50 text-green-700 border-green-200',
    Inactive:  'bg-gray-100 text-gray-500 border-gray-200',
    Suspended: 'bg-red-50 text-red-700 border-red-200',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status]}`}>
      {status}
    </span>
  );
}

function formatType(type: TType): string {
  const labels: Record<TType, string> = {
    LawFirm:    'Law Firm',
    Provider:   'Provider',
    Corporate:  'Corporate',
    Government: 'Government',
    Other:      'Other',
  };
  return labels[type] ?? type;
}

function SeverityBadge({ value }: { value: string }) {
  const MAP: Record<string, string> = {
    info:     'bg-blue-50  text-blue-700  border border-blue-200',
    warn:     'bg-amber-50 text-amber-700 border border-amber-300',
    error:    'bg-red-50   text-red-700   border border-red-200',
    critical: 'bg-red-100  text-red-800   border border-red-400 font-bold',
  };
  const cls = MAP[value.toLowerCase()] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide ${cls}`}>
      {value}
    </span>
  );
}

function CategoryBadge({ value }: { value: string }) {
  const MAP: Record<string, string> = {
    security:       'bg-red-50    text-red-700   border border-red-200',
    administrative: 'bg-gray-100  text-gray-600  border border-gray-200',
    business:       'bg-green-50  text-green-700 border border-green-200',
    compliance:     'bg-purple-50 text-purple-700 border border-purple-200',
  };
  const cls = MAP[value.toLowerCase()] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium capitalize ${cls}`}>
      {value}
    </span>
  );
}
