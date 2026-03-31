import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { ReferralListTable } from '@/components/careconnect/referral-list-table';

interface ReferralsPageProps {
  searchParams: {
    status?:   string;
    urgency?:  string;
    page?:     string;
  };
}

/**
 * /careconnect/referrals — Referral list.
 *
 * Access: CARECONNECT_REFERRER or CARECONNECT_RECEIVER (either is sufficient).
 *   Users with neither role see a denial message — the nav builder already hides
 *   this route from them.
 *
 * UX shaping by role:
 *   - CARECONNECT_REFERRER (law firm):  heading = "Sent Referrals"  + "New Referral" button
 *   - CARECONNECT_RECEIVER (provider):  heading = "Received Referrals" (no create button)
 *   - Both roles:  the backend scopes results to the caller's org automatically.
 *     Law firms see only referrals they created; providers see only referrals directed at them.
 *
 * Backend handles all access enforcement — the role check here is UX only.
 */
export default async function ReferralsPage({ searchParams }: ReferralsPageProps) {
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.CareConnectReferrer);
  const isReceiver = session.productRoles.includes(ProductRole.CareConnectReceiver);

  if (!isReferrer && !isReceiver) {
    return (
      <div className="bg-yellow-50 border border-yellow-200 rounded-lg px-4 py-3 text-sm text-yellow-700">
        You do not have a CareConnect role. Contact your administrator to gain access.
      </div>
    );
  }

  const page = Math.max(1, parseInt(searchParams.page ?? '1') || 1);

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await careConnectServerApi.referrals.search({
      status:  searchParams.status  || undefined,
      urgency: searchParams.urgency || undefined,
      page,
      pageSize: 20,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load referrals.';
  }

  const heading = isReferrer ? 'Sent Referrals' : 'Received Referrals';

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">{heading}</h1>

        {/* Referrers: start a new referral from the provider search flow */}
        {isReferrer && (
          <Link
            href="/careconnect/providers"
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            New Referral
          </Link>
        )}
      </div>

      {/* Quick status filters — values match backend ReferralWorkflowRules */}
      <div className="flex items-center gap-2 flex-wrap">
        {['', 'New', 'Accepted', 'Declined', 'Scheduled', 'Completed', 'Cancelled'].map(s => (
          <Link
            key={s}
            href={s ? `/careconnect/referrals?status=${s}` : '/careconnect/referrals'}
            className={`text-sm px-3 py-1 rounded-full border transition-colors ${
              (searchParams.status ?? '') === s
                ? 'bg-primary text-white border-primary'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
            }`}
          >
            {s || 'All'}
          </Link>
        ))}
      </div>

      {/* Error */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {/* Referral table */}
      {result && (
        <ReferralListTable
          referrals={result.items}
          totalCount={result.totalCount}
          page={result.page}
          pageSize={result.pageSize}
        />
      )}
    </div>
  );
}
