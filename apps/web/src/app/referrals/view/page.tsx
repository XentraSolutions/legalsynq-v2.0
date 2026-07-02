import { redirect } from 'next/navigation';
import { mapFailureReasonToInvalidReason } from '../lib/public-referral-error';
import { fetchPublicCareConnect } from '../lib/public-referral-proxy';

export const dynamic = 'force-dynamic';


/**
 * LSCC-005 / LSCC-01-002-01: Public referral view router.
 *
 * This page is intentionally outside (platform) — no auth middleware,
 * no session required. It decodes the secure view token and routes the
 * provider into the authenticated referral flow.
 *
 * LSCC-01-002-01: Both "pending" and "active" providers are now routed
 * to login with a safe returnTo so acceptance always happens from the
 * authenticated referral detail page. Direct token-only acceptance
 * is no longer supported.
 *
 *   valid token
 *     → /referrals/thread?token=...
 *
 *   "invalid" / "notfound" token
 *     → /referrals/accept/invalid  (error page)
 */

interface Props {
  searchParams: Promise<{ token?: string }>;
}

export default async function ReferralViewPage({ searchParams }: Props) {
  const sp = await searchParams;
  const token = sp.token?.trim();

  if (!token) {
    redirect('/referrals/accept/invalid?reason=missing-token');
  }

  let failureReason: string | null = null;
  let shouldOpenThread = false;

  try {
    const resp = await fetchPublicCareConnect(
      `/api/referrals/resolve-view-token?token=${encodeURIComponent(token)}`,
    );

    if (resp.ok) {
      const data = await resp.json();
      const routeType = data.routeType ?? 'invalid';
      failureReason = data.failureReason ?? null;

      if (routeType === 'pending' || routeType === 'active') {
        shouldOpenThread = true;
      }
    }
  } catch {
    // fall through to invalid screen
  }

  if (shouldOpenThread) {
    redirect(`/referrals/thread?token=${encodeURIComponent(token)}`);
  }

  redirect(`/referrals/accept/invalid?reason=${mapFailureReasonToInvalidReason(failureReason)}`);
}
