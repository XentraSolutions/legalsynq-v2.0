import { redirect }                 from 'next/navigation';
import { FirmStatusClient }         from './firm-status-client';
import { createEnrollmentToken }    from '@/app/enroll/actions';
import {
  ReferrerPortalAccessStatuses,
  type ReferrerPortalAccessStatusValue,
} from '@/types/careconnect';

export const dynamic = 'force-dynamic';

const GATEWAY_URL          = process.env.GATEWAY_URL          ?? 'http://127.0.0.1:5010';
const PROVISIONING_TOKEN   = process.env.IdentityService__ProvisioningToken ?? '';

interface Props {
  searchParams: Promise<{ token?: string }>;
}

/**
 * Law firm referral status page.
 * Reachable only via a secure HMAC-signed token from the referral confirmation email.
 * Shows referral status progress, provider info, messaging, and a CTA to upgrade to
 * the full portal for managing all referrals in one place.
 *
 * CC-PORTAL-CHECK: tenant-scoped portal status decides whether the referrer sees
 * a login prompt or the enrollment CTA.
 */
export default async function FirmStatusPage({ searchParams }: Props) {
  const sp    = await searchParams;
  const token = sp.token?.trim();

  if (!token) {
    redirect('/referrals/accept/invalid?reason=missing-token');
  }

  let threadData = null;

  try {
    const resp = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/referrals/thread?token=${encodeURIComponent(token)}`,
      { cache: 'no-store' },
    );

    if (resp.ok) {
      threadData = await resp.json();
    } else if (resp.status === 404) {
      redirect('/referrals/accept/invalid?reason=expired-or-invalid');
    }
  } catch {
    threadData = null;
  }

  if (!threadData) {
    redirect('/referrals/accept/invalid?reason=expired-or-invalid');
  }

  // CC-PORTAL-CHECK: tenant-aware access status for the referrer email.
  // Failure → safe default (no_account) so the enrollment CTA is shown instead.
  let portalAccessStatus: ReferrerPortalAccessStatusValue = ReferrerPortalAccessStatuses.NoAccount;
  const referrerEmail = threadData.referrerEmail as string | null;
  const referralTenantId = threadData.tenantId as string | null;
  if (referrerEmail && referralTenantId) {
    try {
      const checkResp = await fetch(
        `${GATEWAY_URL}/identity/api/internal/users/portal-access?tenantId=${encodeURIComponent(referralTenantId)}&email=${encodeURIComponent(referrerEmail)}`,
        {
          cache:   'no-store',
          headers: { 'X-Provisioning-Token': PROVISIONING_TOKEN },
        },
      );
      if (checkResp.ok) {
        const checkData = await checkResp.json() as { status?: string };
        if (checkData.status && Object.values(ReferrerPortalAccessStatuses).includes(checkData.status as ReferrerPortalAccessStatusValue)) {
          portalAccessStatus = checkData.status as ReferrerPortalAccessStatusValue;
        }
      }
    } catch {
      // non-fatal — keep no_account
    }
  }

  // Extract "Firm phone: xxx" embedded by public-network-view in the referral notes.
  const notes = threadData.notes as string | null;
  const referrerPhone = notes?.split('\n')
    .find(l => l.trim().toLowerCase().startsWith('firm phone:'))
    ?.slice('firm phone:'.length).trim() ?? null;

  const enrollToken = await createEnrollmentToken({
    tenantId: threadData.tenantId as string,
    ...(threadData.referrerEmail ? { email:   threadData.referrerEmail as string } : {}),
    ...(threadData.referrerName  ? { contact: threadData.referrerName  as string } : {}),
    ...(referrerPhone            ? { phone:   referrerPhone                      } : {}),
  }).catch((err) => { console.error('[firm-status] createEnrollmentToken failed:', err); return null; });

  return <FirmStatusClient token={token} data={threadData} portalAccessStatus={portalAccessStatus} loginUrl="/login" enrollToken={enrollToken} />;
}
