import { redirect }                 from 'next/navigation';
import { FirmStatusClient }         from './firm-status-client';
import { createEnrollmentToken }    from '@/app/enroll/actions';
import { mapFailureReasonToInvalidReason, readPublicReferralFailureReason } from '../lib/public-referral-error';
import { fetchPublicCareConnect } from '../lib/public-referral-proxy';
import { buildCareConnectReferralLoginUrl } from '@/lib/careconnect-login-url';
import {
  ReferrerPortalAccessStatuses,
  type ReferrerPortalAccessStatusValue,
} from '@/types/careconnect';

export const dynamic = 'force-dynamic';

interface Props {
  searchParams: Promise<{ token?: string }>;
}

function normalizeNameForComparison(value?: string | null): string {
  return value?.trim().replace(/\s+/g, ' ').toLowerCase() ?? '';
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
  let failureReason: string | null = null;

  try {
    const resp = await fetchPublicCareConnect(
      `/api/public/referrals/thread?token=${encodeURIComponent(token)}`,
    );

    if (resp.ok) {
      threadData = await resp.json();
    } else {
      failureReason = await readPublicReferralFailureReason(resp);
    }
  } catch {
    threadData = null;
  }

  if (!threadData) {
    redirect(`/referrals/accept/invalid?reason=${mapFailureReasonToInvalidReason(failureReason)}`);
  }

  // CC-PORTAL-CHECK: tenant-aware access status for the referrer email.
  // Failure → safe default (no_account) so the enrollment CTA is shown instead.
  let portalAccessStatus: ReferrerPortalAccessStatusValue = ReferrerPortalAccessStatuses.NoAccount;
  const referrerEmail = threadData.referrerEmail as string | null;
  if (referrerEmail) {
    try {
      const checkResp = await fetchPublicCareConnect(
        `/api/public/referrer-status?email=${encodeURIComponent(referrerEmail)}`,
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

  const firmName = (threadData.referrerFirmName as string | null)?.trim() || null;
  const referrerPhone = (threadData.referrerPhone as string | null)?.trim() || null;

  // Prefer the split ReferrerFirstName/ReferrerLastName fields (no full-name slicing
  // ambiguity); fall back to the legacy single ReferrerName for referrals created
  // before the split existed (e.g. via the authenticated/JWT path).
  const hasSplitReferrerName = !!(threadData.referrerFirstName || threadData.referrerLastName);
  const legacyReferrerName = (threadData.referrerName as string | null)?.trim() || null;
  const shouldIncludeLegacyContact =
    !!legacyReferrerName &&
    normalizeNameForComparison(legacyReferrerName) !== normalizeNameForComparison(firmName);

  const enrollToken = await createEnrollmentToken({
    tenantId: threadData.tenantId as string,
    ...(threadData.referrerEmail ? { email: threadData.referrerEmail as string } : {}),
    ...(firmName                 ? { firm:  firmName                          } : {}),
    ...(hasSplitReferrerName
      ? {
          ...(threadData.referrerFirstName ? { contactFirstName: threadData.referrerFirstName as string } : {}),
          ...(threadData.referrerLastName  ? { contactLastName:  threadData.referrerLastName  as string } : {}),
        }
      : (shouldIncludeLegacyContact ? { contact: legacyReferrerName } : {})),
    ...(referrerPhone ? { phone: referrerPhone } : {}),
  }).catch((err) => { console.error('[firm-status] createEnrollmentToken failed:', err); return null; });

  const loginUrl = buildCareConnectReferralLoginUrl(
    process.env.CC_COMMON_PORTAL_HOSTNAME,
    `/careconnect/referrals/${threadData.referralId as string}`,
  );

  return <FirmStatusClient token={token} data={threadData} portalAccessStatus={portalAccessStatus} loginUrl={loginUrl} enrollToken={enrollToken} />;
}
