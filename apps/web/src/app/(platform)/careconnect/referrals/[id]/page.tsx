import { notFound } from 'next/navigation';
import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { ReferralDetailPanel } from '@/components/careconnect/referral-detail-panel';

interface ReferralDetailPageProps {
  params: { id: string };
}

/**
 * /careconnect/referrals/[id] — Referral detail.
 *
 * Access: CARECONNECT_REFERRER or CARECONNECT_RECEIVER.
 *   The backend enforces that the caller's org is either the referring or
 *   receiving org — a 403 on a valid ID means the user's org is not a participant.
 *
 * Rendering: Server Component — no interactive mutations on this page.
 *   Status updates (future) will be a Client Component modal or a Server Action.
 */
export default async function ReferralDetailPage({ params }: ReferralDetailPageProps) {
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.CareConnectReferrer);
  const isReceiver = session.productRoles.includes(ProductRole.CareConnectReceiver);

  if (!isReferrer && !isReceiver) {
    return (
      <div className="bg-yellow-50 border border-yellow-200 rounded-lg px-4 py-3 text-sm text-yellow-700">
        You do not have a CareConnect role.
      </div>
    );
  }

  let referral = null;
  let fetchError: string | null = null;

  try {
    referral = await careConnectServerApi.referrals.getById(params.id);
  } catch (err) {
    if (err instanceof ServerApiError) {
      if (err.isNotFound) notFound();
      // 403: user's org is not a participant in this referral
      if (err.isForbidden) {
        fetchError = 'You do not have access to this referral. Your organization is not a participant.';
      } else {
        fetchError = err.message;
      }
    } else {
      fetchError = 'Failed to load referral.';
    }
  }

  return (
    <div className="space-y-4">
      {/* Back link */}
      <nav>
        <Link
          href="/careconnect/referrals"
          className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
        >
          ← Back to Referrals
        </Link>
      </nav>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {referral && <ReferralDetailPanel referral={referral} />}

      {/* Referrer: book an appointment for this referral */}
      {referral && isReferrer && referral.status !== 'Cancelled' && referral.status !== 'Declined' && (
        <div className="flex items-center gap-3">
          <Link
            href={`/careconnect/providers/${referral.providerId}/availability?referralId=${referral.id}`}
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            Book Appointment
          </Link>
          <span className="text-xs text-gray-400">
            Opens the provider's availability calendar with this referral pre-loaded.
          </span>
        </div>
      )}

      {/* Phase 2 placeholder for receiver status updates */}
      {referral && isReceiver && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg px-4 py-3 text-xs text-gray-400">
          Status updates (Accept / Decline) are coming in Phase 2.
        </div>
      )}
    </div>
  );
}
