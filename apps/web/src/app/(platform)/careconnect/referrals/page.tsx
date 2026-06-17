import Link from 'next/link';
import { Suspense } from 'react';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { checkCareConnectReceiverAccess } from '@/lib/careconnect-access';
import { ReferralAccessBlocked } from '@/components/careconnect/referral-access-blocked';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { tenantServerApi } from '@/lib/tenant-api';
import { ReferralListTable } from '@/components/careconnect/referral-list-table';
import { ReferralQueueToolbar } from '@/components/careconnect/referral-queue-toolbar';
import { isValidIsoDate, formatDisplayDate } from '@/lib/daterange';
import { formatTimestamp } from '@/lib/format-date';
import type { AdminReferralItem, AdminReferralPage, NetworkReferralItem } from '@/types/careconnect';

export const dynamic = 'force-dynamic';


interface ReferralsPageProps {
  searchParams: Promise<{
    status?:      string;
    urgency?:     string;
    providerId?:  string;
    createdFrom?: string;
    createdTo?:   string;
    page?:        string;
    search?:      string;
  }>;
}

// ── Status badge colours ───────────────────────────────────────────────────────

const STATUS_BADGE: Record<string, string> = {
  New:        'bg-blue-50 text-blue-700 border-blue-200',
  NewOpened:  'bg-sky-50 text-sky-700 border-sky-200',
  Accepted:   'bg-teal-50 text-teal-700 border-teal-200',
  InProgress: 'bg-amber-50 text-amber-700 border-amber-200',
  Completed:  'bg-green-50 text-green-700 border-green-200',
  Declined:   'bg-red-50 text-red-700 border-red-200',
  Cancelled:  'bg-gray-50 text-gray-600 border-gray-200',
};

const URGENCY_BADGE: Record<string, string> = {
  Emergency: 'bg-red-50 text-red-700 border-red-200',
  Urgent:    'bg-orange-50 text-orange-700 border-orange-200',
  Normal:    'bg-blue-50 text-blue-600 border-blue-200',
  Low:       'bg-gray-50 text-gray-600 border-gray-200',
};

function rowHighlight(status: string): string {
  if (status === 'New')        return 'bg-blue-50/40 hover:bg-blue-50 border-l-4 border-l-blue-400';
  if (status === 'NewOpened')  return 'bg-sky-50/40 hover:bg-sky-50 border-l-4 border-l-sky-400';
  if (status === 'Accepted')   return 'hover:bg-gray-50 border-l-4 border-l-teal-400';
  if (status === 'InProgress') return 'bg-amber-50/30 hover:bg-amber-50/60 border-l-4 border-l-amber-400';
  if (status === 'Declined')   return 'bg-red-50/30 hover:bg-red-50/60 border-l-4 border-l-red-400';
  if (status === 'Cancelled')  return 'bg-gray-50/60 hover:bg-gray-100 border-l-4 border-l-gray-400';
  return 'hover:bg-gray-50 border-l-4 border-l-transparent';
}

function StatusBadge({ status }: { status: string }) {
  const cls = STATUS_BADGE[status] ?? 'bg-gray-100 text-gray-700';
  return (
    <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${cls}`}>
      {status === 'NewOpened' ? 'Opened' : status}
    </span>
  );
}

function UrgencyBadge({ urgency }: { urgency: string }) {
  const cls = URGENCY_BADGE[urgency] ?? 'bg-gray-100 text-gray-600';
  return (
    <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${cls}`}>
      {urgency}
    </span>
  );
}

function formatDate(iso: string, timezone: string) {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric', timeZone: timezone,
  });
}

function normalizeNetworkDisplay(value?: string | null): string | null {
  const trimmed = value?.trim();
  return trimmed && trimmed !== '-' ? trimmed : null;
}

function getNetworkDisplay(item: NetworkReferralItem): string | null {
  return normalizeNetworkDisplay(item.networkName) ?? normalizeNetworkDisplay(item.tenantName);
}

function getAdminNetworkDisplay(item: AdminReferralItem): string | null {
  return normalizeNetworkDisplay(item.networkName) ?? normalizeNetworkDisplay(item.tenantName);
}

function buildReferralsHref(params: {
  status?: string;
  search?: string;
  page?: number;
}) {
  const qs = new URLSearchParams();
  if (params.status) qs.set('status', params.status);
  if (params.search) qs.set('search', params.search);
  if (params.page && params.page > 1) qs.set('page', String(params.page));

  const query = qs.toString();
  return query ? `/careconnect/referrals?${query}` : '/careconnect/referrals';
}

function ReferralsPagination({
  page,
  pageSize,
  total,
  status,
  search,
}: {
  page: number;
  pageSize: number;
  total: number;
  status?: string;
  search?: string;
}) {
  if (total <= 0) return null;

  return (
    <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
      <p className="text-xs text-gray-400">
        Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)} of {total}
      </p>
      <div className="flex items-center gap-2">
        {page > 1 && (
          <Link
            href={buildReferralsHref({ status, search, page: page - 1 })}
            className="text-xs text-primary hover:underline"
          >
            ← Previous
          </Link>
        )}
        {page * pageSize < total && (
          <Link
            href={buildReferralsHref({ status, search, page: page + 1 })}
            className="text-xs text-primary hover:underline"
          >
            Next →
          </Link>
        )}
      </div>
    </div>
  );
}

// ── Network referral row (flat table) ─────────────────────────────────────────

function NetworkReferralRow({
  item,
  showNetworkColumn,
  timezone,
}: {
  item: NetworkReferralItem;
  showNetworkColumn: boolean;
  timezone: string;
}) {
  const clientName     = [item.clientFirstName, item.clientLastName].filter(Boolean).join(' ') || '—';
  const providerDisplay = item.providerOrganizationName ?? item.providerName ?? '—';
  const lawFirm        = item.referrerName  ?? '—';
  const email          = item.referrerEmail ?? '—';
  const networkDisplay = getNetworkDisplay(item);

  return (
    <tr className="hover:bg-gray-50 transition-colors">
      <td className="px-4 py-3">
        <div className="text-sm font-medium text-gray-900">{clientName}</div>
        {item.caseNumber && (
          <div className="text-xs text-gray-400 mt-0.5">Case #{item.caseNumber}</div>
        )}
      </td>
      <td className="px-4 py-3">
        <div className="text-sm text-gray-800 max-w-[160px] truncate" title={lawFirm}>
          {lawFirm}
        </div>
      </td>
      <td className="px-4 py-3">
        <div className="text-sm text-gray-500 max-w-[180px] truncate" title={email}>
          {email}
        </div>
      </td>
      <td className="px-4 py-3">
        <div className="text-sm text-gray-800 max-w-[160px] truncate" title={providerDisplay}>
          {providerDisplay}
        </div>
      </td>
      <td className="px-4 py-3">
        <div className="text-sm text-gray-700 max-w-[140px] truncate" title={item.requestedService}>
          {item.requestedService}
        </div>
      </td>
      {showNetworkColumn && (
        <td className="px-4 py-3">
          {networkDisplay ? (
            <div className="text-sm text-gray-800 max-w-[160px] truncate" title={networkDisplay}>
              {networkDisplay}
            </div>
          ) : (
            <span className="text-xs text-gray-400">-</span>
          )}
        </td>
      )}
      <td className="px-4 py-3">
        <StatusBadge status={item.status} />
      </td>
      <td className="px-4 py-3">
        <UrgencyBadge urgency={item.urgency} />
      </td>
      <td className="px-4 py-3">
        <span className="text-xs text-gray-400">{formatDate(item.createdAtUtc, timezone)}</span>
      </td>
      <td className="px-4 py-3 text-right">
        <Link
          href={`/careconnect/referrals/${item.id}`}
          className="text-sm text-primary hover:underline font-medium"
        >
          View →
        </Link>
      </td>
    </tr>
  );
}

// ── Network referrals view ─────────────────────────────────────────────────────

const ALL_STATUSES = ['New', 'Accepted', 'InProgress', 'Completed', 'Declined', 'Cancelled'];

async function NetworkReferralsView({
  status,
  search,
  page,
  timezone,
}: {
  status?: string;
  search?: string;
  page: number;
  timezone: string;
}) {
  let data = null;
  let fetchError: string | null = null;

  try {
    data = await careConnectServerApi.networkReferrals.list({
      page,
      pageSize: 20,
      status:   status || undefined,
      search:   search || undefined,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load network referrals.';
  }

  const showNetworkColumn = data?.items.some((item) => 'networkName' in item || 'tenantName' in item) ?? false;

  return (
    <div className="space-y-4">
      {/* Status filter pills */}
      <div className="flex flex-wrap gap-2">
        <Link
          href={buildReferralsHref({ search })}
          className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
            !status
              ? 'bg-primary text-white border-primary'
              : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
          }`}
        >
          All
        </Link>
        {ALL_STATUSES.map((s) => (
          <Link
            key={s}
            href={buildReferralsHref({ status: s, search })}
            className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
              status === s
                ? 'bg-primary text-white border-primary'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
            }`}
          >
            {s}
          </Link>
        ))}
      </div>

      {/* Error */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          Unable to load network referrals. {fetchError}
        </div>
      )}

      {/* Summary line */}
      {data && (
        <p className="text-xs text-gray-400">
          {data.total === 0
            ? (status || search ? 'No referrals match your filters.' : 'No referrals have been sent through your network yet.')
            : `${data.total} referral${data.total !== 1 ? 's' : ''}${status ? ` · Status: ${status}` : ''}`}
        </p>
      )}

      {/* Flat table */}
      {data && data.items.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50">
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Client</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Law Firm</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Email</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Provider</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Service</th>
                  {showNetworkColumn && (
                    <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Network</th>
                  )}
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Status</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Urgency</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Date</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {data.items.map((item) => (
                  <NetworkReferralRow key={item.id} item={item} showNetworkColumn={showNetworkColumn} timezone={timezone} />
                ))}
              </tbody>
            </table>
          </div>
          <ReferralsPagination
            page={data.page}
            pageSize={data.pageSize}
            total={data.total}
            status={status}
            search={search}
          />
        </div>
      )}

      {/* Empty state */}
      {data && data.items.length === 0 && !fetchError && (
        <div className="bg-white rounded-xl border border-gray-200 p-12 text-center">
          <div className="w-14 h-14 bg-gray-50 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-7 h-7 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
                d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
          </div>
          <h3 className="text-base font-semibold text-gray-900 mb-1">
            {status || search ? 'No matching referrals' : 'No referrals yet'}
          </h3>
          <p className="text-sm text-gray-500">
            {status || search
              ? 'Try clearing your filters to see all referrals.'
              : 'Referrals sent by law firms to providers in your network will appear here.'}
          </p>
          {(status || search) && (
            <Link
              href={buildReferralsHref({})}
              className="mt-3 inline-block text-sm text-primary hover:underline"
            >
              Clear filters
            </Link>
          )}
        </div>
      )}

    </div>
  );
}

async function TenantAdminReadOnlyReferralsView({
  sessionTenantId,
  status,
  page,
  timezone,
}: {
  sessionTenantId: string;
  status?: string;
  page: number;
  timezone: string;
}) {
  let data: AdminReferralPage | null = null;
  let fetchError: string | null = null;

  try {
    data = await careConnectServerApi.adminDashboard.getReferrals({
      page,
      pageSize: 20,
      status: status || undefined,
      tenantId: sessionTenantId,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load tenant referrals.';
  }

  const items = data?.items ?? [];
  const showNetworkColumn = items.some((item) => 'networkName' in item || 'tenantName' in item);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-2">
        <Link
          href={buildReferralsHref({})}
          className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
            !status
              ? 'bg-primary text-white border-primary'
              : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
          }`}
        >
          All
        </Link>
        {ALL_STATUSES.map((s) => (
          <Link
            key={s}
            href={buildReferralsHref({ status: s })}
            className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
              status === s
                ? 'bg-primary text-white border-primary'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
            }`}
          >
            {s}
          </Link>
        ))}
      </div>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          Unable to load tenant referrals. {fetchError}
        </div>
      )}

      {items.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100">
              <thead>
                <tr className="bg-gray-50">
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Referral</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Referrer</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Provider</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Service</th>
                  {showNetworkColumn && (
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Network</th>
                  )}
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Urgency</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {items.map((item) => {
                  const networkDisplay = getAdminNetworkDisplay(item);
                  return (
                    <tr key={item.id} className={`transition-colors ${rowHighlight(item.status)}`}>
                      <td className="px-4 py-3">
                        <div className="text-sm font-medium text-gray-900">{item.id.slice(0, 8)}…</div>
                      </td>
                      <td className="px-4 py-3">
                        <div className="text-sm text-gray-800 max-w-[180px] truncate" title={item.referringOrganizationName ?? item.referrerName ?? item.referrerEmail ?? '—'}>
                          {item.referringOrganizationName ?? item.referrerName ?? '—'}
                        </div>
                        {item.referrerEmail && (
                          <div className="text-xs text-gray-500 max-w-[180px] truncate" title={item.referrerEmail}>
                            {item.referrerEmail}
                          </div>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <div className="text-sm text-gray-800 max-w-[180px] truncate" title={item.providerName ?? '—'}>
                          {item.providerName ?? '—'}
                        </div>
                        {item.providerEmail && (
                          <div className="text-xs text-gray-500 max-w-[180px] truncate" title={item.providerEmail}>
                            {item.providerEmail}
                          </div>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <div className="text-sm text-gray-700 max-w-[160px] truncate" title={item.requestedService}>
                          {item.requestedService}
                        </div>
                      </td>
                      {showNetworkColumn && (
                        <td className="px-4 py-3">
                          {networkDisplay ? (
                            <div className="text-sm text-gray-800 max-w-[160px] truncate" title={networkDisplay}>
                              {networkDisplay}
                            </div>
                          ) : (
                            <span className="text-xs text-gray-400">-</span>
                          )}
                        </td>
                      )}
                      <td className="px-4 py-3">
                        <StatusBadge status={item.status} />
                      </td>
                      <td className="px-4 py-3">
                        <UrgencyBadge urgency={item.urgency} />
                      </td>
                      <td className="px-4 py-3">
                        {(() => {
                          const ts = formatTimestamp(item.createdAtUtc, timezone);
                          return (
                            <>
                              <p className="text-xs text-gray-500">{ts.date}</p>
                              <p className="text-[11px] text-gray-400">{ts.time}</p>
                            </>
                          );
                        })()}
                      </td>
                      <td className="px-4 py-3">
                        <Link
                          href={`/careconnect/referrals/${item.id}`}
                          className="text-xs font-medium px-2.5 py-1 border border-gray-200 text-gray-700 rounded hover:bg-gray-50 transition-colors whitespace-nowrap"
                        >
                          View
                        </Link>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <ReferralsPagination
            page={data?.page ?? page}
            pageSize={data?.pageSize ?? 20}
            total={data?.total ?? 0}
            status={status}
          />
        </div>
      )}

      {data && items.length === 0 && !fetchError && (
        <div className="bg-white rounded-xl border border-gray-200 p-12 text-center">
          <div className="w-14 h-14 bg-gray-50 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-7 h-7 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
                d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
          </div>
          <h3 className="text-base font-semibold text-gray-900 mb-1">
            {status ? `No ${status} referrals` : 'No referrals yet'}
          </h3>
          <p className="text-sm text-gray-500">
            Referrals tied to your tenant will appear here when law firms send cases to providers.
          </p>
        </div>
      )}
    </div>
  );
}

// ── Page ───────────────────────────────────────────────────────────────────────

export default async function ReferralsPage({ searchParams }: ReferralsPageProps) {
  const searchParamsData = await searchParams;
  const session = await requireOrg();
  const page = Math.max(1, parseInt(searchParamsData.page ?? '1') || 1);

  const isReferrer        = session.productRoles.includes(ProductRole.CareConnectReferrer);
  const isReceiver        = session.productRoles.includes(ProductRole.CareConnectReceiver);
  const isNetworkManager  = session.productRoles.includes(ProductRole.CareConnectNetworkManager);
  const isTenantAdminView = session.isTenantAdmin && !session.isPlatformAdmin;

  let tenantTimezone = 'America/Los_Angeles';
  try {
    const tzSetting = await tenantServerApi.getTimezoneSetting(session.tenantId);
    if (tzSetting.value) tenantTimezone = tzSetting.value;
  } catch { /* fall back to default */ }

  // LSCC-01-002-02: Enforce the admin-controlled access model.
  if (!isReferrer && !isReceiver && !isNetworkManager && !isTenantAdminView) {
    const readiness = checkCareConnectReceiverAccess(session);
    return <ReferralAccessBlocked reason={readiness.reason} />;
  }

  if (isTenantAdminView) {
    return (
      <div className="space-y-5">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Referrals</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              View referral activity between referrers and receivers.
            </p>
          </div>
        </div>

        <TenantAdminReadOnlyReferralsView
          sessionTenantId={session.tenantId}
          status={searchParamsData.status || undefined}
          page={page}
          timezone={tenantTimezone}
        />

        <div className="pt-1">
          <Link href="/careconnect/dashboard" className="text-xs text-gray-400 hover:text-gray-600 transition-colors">
            ← Back to Dashboard
          </Link>
        </div>
      </div>
    );
  }

  // ── Network manager view ───────────────────────────────────────────────────
  // Lien companies with the network manager role see all referrals flowing through
  // their network, grouped by law firm. This takes priority over any referrer/receiver
  // role the account may also hold — lien companies do not send referrals themselves.

  if (isNetworkManager) {
    const searchText = searchParamsData.search?.trim() || undefined;

    return (
      <div className="space-y-5">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Network Referrals</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              All referrals from law firms to providers in your network.
            </p>
          </div>
        </div>

        {/* Search bar */}
        <Suspense fallback={null}>
          <ReferralQueueToolbar
            currentSearch={searchText ?? ''}
            currentStatus={searchParamsData.status ?? ''}
          />
        </Suspense>

        {/* Network grouped view */}
        <NetworkReferralsView
          status={searchParamsData.status || undefined}
          search={searchText}
          page={page}
          timezone={tenantTimezone}
        />

        <div className="pt-1">
          <Link href="/careconnect/dashboard" className="text-xs text-gray-400 hover:text-gray-600 transition-colors">
            ← Back to Dashboard
          </Link>
        </div>
      </div>
    );
  }

  // ── Standard referrer / receiver view ─────────────────────────────────────

  const createdFrom = (searchParamsData.createdFrom && isValidIsoDate(searchParamsData.createdFrom))
    ? searchParamsData.createdFrom : undefined;
  const createdTo   = (searchParamsData.createdTo && isValidIsoDate(searchParamsData.createdTo))
    ? searchParamsData.createdTo : undefined;

  const searchText = searchParamsData.search?.trim() || undefined;

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await careConnectServerApi.referrals.search({
      status:      searchParamsData.status     || undefined,
      urgency:     searchParamsData.urgency    || undefined,
      providerId:  searchParamsData.providerId || undefined,
      clientName:  searchText,
      createdFrom,
      createdTo,
      page,
      pageSize: 20,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load referrals.';
  }

  const heading = isReferrer ? 'Sent Referrals' : 'Referral Inbox';

  const hasDateFilter = !!(createdFrom || createdTo);

  const qsParts: string[] = [];
  if (searchParamsData.status)    qsParts.push(`status=${encodeURIComponent(searchParamsData.status)}`);
  if (searchParamsData.search)    qsParts.push(`search=${encodeURIComponent(searchParamsData.search)}`);
  if (searchParamsData.createdFrom) qsParts.push(`createdFrom=${searchParamsData.createdFrom}`);
  if (searchParamsData.createdTo)   qsParts.push(`createdTo=${searchParamsData.createdTo}`);
  const currentQs = qsParts.join('&');

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">{heading}</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            {isReferrer ? 'Referrals you have sent to providers.' : 'Referrals waiting for your action.'}
          </p>
        </div>
        {isReferrer && (
          <Link
            href="/careconnect/browse-networks"
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity shrink-0"
          >
            + New Referral
          </Link>
        )}
      </div>

      {/* Active date filter indicator */}
      {hasDateFilter && (
        <div className="flex items-center gap-2 text-xs text-blue-700 bg-blue-50 border border-blue-100 rounded px-3 py-2">
          <span>&#x1F4C5;</span>
          <span>
            Filtered to{' '}
            {createdFrom ? formatDisplayDate(createdFrom) : 'start'}
            {' → '}
            {createdTo ? formatDisplayDate(createdTo) : 'today'}
          </span>
          <Link
            href="/careconnect/referrals"
            className="ml-2 text-blue-500 hover:text-blue-700 underline"
          >
            Clear
          </Link>
        </div>
      )}

      {/* Search + filter toolbar */}
      <Suspense fallback={null}>
        <ReferralQueueToolbar
          currentSearch={searchText ?? ''}
          currentStatus={searchParamsData.status ?? ''}
        />
      </Suspense>

      {/* Error */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          Unable to load referrals. {fetchError}
        </div>
      )}

      {/* Results summary when filtering */}
      {result && (searchText || searchParamsData.status) && (
        <p className="text-xs text-gray-400">
          {result.totalCount === 0
            ? 'No referrals match your filters.'
            : `${result.totalCount} referral${result.totalCount !== 1 ? 's' : ''} found`}
        </p>
      )}

      {/* Referral table */}
      {result && (
        <ReferralListTable
          referrals={result.items}
          totalCount={result.totalCount}
          page={result.page}
          pageSize={result.pageSize}
          isReferrer={isReferrer}
          isReceiver={isReceiver}
          orgId={session.orgId}
          currentQs={currentQs}
          timezone={tenantTimezone}
        />
      )}

      {/* Back to dashboard */}
      <div className="pt-1">
        <Link href="/careconnect/dashboard" className="text-xs text-gray-400 hover:text-gray-600 transition-colors">
          ← Back to Dashboard
        </Link>
      </div>
    </div>
  );
}
