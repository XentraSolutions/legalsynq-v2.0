import Link from 'next/link';
import { Suspense } from 'react';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { checkCareConnectReceiverAccess } from '@/lib/careconnect-access';
import { ReferralAccessBlocked } from '@/components/careconnect/referral-access-blocked';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { ReferralListTable } from '@/components/careconnect/referral-list-table';
import { ReferralQueueToolbar } from '@/components/careconnect/referral-queue-toolbar';
import { isValidIsoDate, formatDisplayDate } from '@/lib/daterange';

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

export default async function ReferralsPage({ searchParams }: ReferralsPageProps) {
  const searchParamsData = await searchParams;
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.CareConnectReferrer);
  const isReceiver = session.productRoles.includes(ProductRole.CareConnectReceiver);

  // LSCC-01-002-02: Enforce the admin-controlled access model.
  // Only users with a CareConnect role may enter the referral list.
  // No referral data is fetched or rendered in the blocked state.
  if (!isReferrer && !isReceiver) {
    const readiness = checkCareConnectReceiverAccess(session);
    return <ReferralAccessBlocked reason={readiness.reason} />;
  }

  const page = Math.max(1, parseInt(searchParamsData.page ?? '1') || 1);

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

  // Build query string for pagination links to preserve current filters
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
            href="/careconnect/providers"
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

      {/* Search + filter toolbar — Suspense because it uses useSearchParams */}
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
          currentQs={currentQs}
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
