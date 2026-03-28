import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';

/**
 * CareConnect — Referrals list.
 * Accessible to both CARECONNECT_REFERRER (sent) and CARECONNECT_RECEIVER (received).
 * The list is filtered server-side by the backend based on the caller's org.
 */
export default async function ReferralsPage() {
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.CareConnectReferrer);
  const isReceiver = session.productRoles.includes(ProductRole.CareConnectReceiver);

  if (!isReferrer && !isReceiver) {
    // No CareConnect role at all — should be caught by nav, but guard here too
    return (
      <div className="text-sm text-gray-500">
        You do not have access to CareConnect referrals.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Referrals</h1>
        {isReferrer && (
          <a
            href="/careconnect/referrals/new"
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            New Referral
          </a>
        )}
      </div>

      {/* TODO: Replace with ReferralList component fetching from apiClient */}
      <div className="bg-white border border-gray-200 rounded-lg p-8 text-center text-sm text-gray-400">
        Referral list — connect to{' '}
        <code className="font-mono bg-gray-100 px-1 rounded">
          GET /api/careconnect/api/referrals
        </code>
      </div>
    </div>
  );
}
