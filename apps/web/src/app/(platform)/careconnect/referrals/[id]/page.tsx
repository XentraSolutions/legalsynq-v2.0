import { notFound } from 'next/navigation';
import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { resolveReferralDetailBack } from '@/lib/referral-nav';
import { ReferralPageHeader } from '@/components/careconnect/referral-page-header';
import { ReferralDetailPanel } from '@/components/careconnect/referral-detail-panel';
import { ReferralDeliveryCard } from '@/components/careconnect/referral-delivery-card';
import { ReferralStatusActions } from '@/components/careconnect/referral-status-actions';
import { ReferralTimeline } from '@/components/careconnect/referral-timeline';
import { ReferralAuditTimeline } from '@/components/careconnect/referral-audit-timeline';

interface ReferralDetailPageProps {
  params:       { id: string };
  searchParams: {
    from?:        string;
    status?:      string;
    search?:      string;
    createdFrom?: string;
    createdTo?:   string;
  };
}

export default async function ReferralDetailPage({ params, searchParams }: ReferralDetailPageProps) {
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

  const { href: backHref, label: backLabel } = resolveReferralDetailBack(searchParams);

  return (
    <div className="space-y-4">
      {/* Back navigation */}
      <nav className="flex items-center justify-between">
        <Link
          href={backHref}
          className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
        >
          {backLabel}
        </Link>
      </nav>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {referral && (
        <>
          {/* 1. Header — identity + prominent status */}
          <ReferralPageHeader referral={referral} />

          {/* 2. Primary action area */}
          <ReferralStatusActions
            referral={referral}
            isReceiver={isReceiver}
            isReferrer={isReferrer}
          />

          {/* Book appointment — referrer action once accepted */}
          {isReferrer && referral.status === 'Accepted' && (
            <div className="bg-teal-50 border border-teal-200 rounded-lg px-5 py-4 flex items-center gap-4">
              <div className="flex-1">
                <p className="text-sm font-medium text-teal-800">This referral has been accepted.</p>
                <p className="text-xs text-teal-600 mt-0.5">You can now schedule an appointment with the provider.</p>
              </div>
              <Link
                href={`/careconnect/providers/${referral.providerId}/availability?referralId=${referral.id}`}
                className="bg-teal-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-teal-700 transition-colors shrink-0"
              >
                Book Appointment
              </Link>
            </div>
          )}

          {/* 3. Referral details — body only (header rendered above) */}
          <ReferralDetailPanel referral={referral} hideHeader />

          {/* 4. Delivery / access controls — referrers only */}
          {isReferrer && <ReferralDeliveryCard referral={referral} />}

          {/* 5. Audit timeline — referrers only */}
          {isReferrer && <ReferralAuditTimeline referralId={referral.id} />}

          {/* 5b. Activity / status history — all roles */}
          <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
            <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">
              Activity
            </h3>
            <ReferralTimeline referralId={referral.id} />
          </div>
        </>
      )}
    </div>
  );
}
