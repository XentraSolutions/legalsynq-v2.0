import { notFound } from 'next/navigation';
import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { ReferralDetailPanel } from '@/components/careconnect/referral-detail-panel';
import { ReferralDeliveryCard } from '@/components/careconnect/referral-delivery-card';
import { ReferralStatusActions } from '@/components/careconnect/referral-status-actions';
import { ReferralTimeline } from '@/components/careconnect/referral-timeline';

interface ReferralDetailPageProps {
  params: { id: string };
}

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

      {/* Referrer: book an appointment once the referral is accepted */}
      {referral && isReferrer && referral.status === 'Accepted' && (
        <div className="flex items-center gap-3">
          <Link
            href={`/careconnect/providers/${referral.providerId}/availability?referralId=${referral.id}`}
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            Book Appointment
          </Link>
          <span className="text-xs text-gray-400">
            Select an available slot to schedule for this referral.
          </span>
        </div>
      )}

      {/* LSCC-005-01: Email delivery status card — referrers only */}
      {referral && isReferrer && (
        <ReferralDeliveryCard referral={referral} />
      )}

      {/* Role-based status actions (Accept / Decline for receivers; Cancel for either) */}
      {referral && (
        <ReferralStatusActions
          referral={referral}
          isReceiver={isReceiver}
          isReferrer={isReferrer}
        />
      )}

      {/* Referral activity timeline */}
      {referral && (
        <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
          <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">
            Activity
          </h3>
          <ReferralTimeline referralId={referral.id} />
        </div>
      )}
    </div>
  );
}
