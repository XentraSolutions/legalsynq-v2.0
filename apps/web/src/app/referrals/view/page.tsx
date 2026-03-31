import { redirect } from 'next/navigation';

/**
 * LSCC-005: Public referral view router.
 *
 * This page is intentionally outside (platform) — no auth middleware,
 * no session required. It decodes the secure view token and routes the
 * provider to the correct experience:
 *
 *   "pending" provider (OrganizationId = null)
 *     → /referrals/accept/{referralId}?token={token}  (public accept page)
 *
 *   "active" tenant provider (OrganizationId != null)
 *     → /login?returnTo=/careconnect/referrals/{referralId}  (platform login → deep link)
 *
 *   "invalid" / "notfound" token
 *     → /referrals/accept/invalid  (error page)
 */

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5010';

interface Props {
  searchParams: { token?: string };
}

export default async function ReferralViewPage({ searchParams }: Props) {
  const token = searchParams.token?.trim();

  if (!token) {
    redirect('/referrals/accept/invalid?reason=missing-token');
  }

  let routeType = 'invalid';
  let referralId: string | null = null;

  try {
    const resp = await fetch(
      `${GATEWAY_URL}/careconnect/api/referrals/resolve-view-token?token=${encodeURIComponent(token)}`,
      { cache: 'no-store' },
    );

    if (resp.ok) {
      const data = await resp.json();
      routeType  = data.routeType  ?? 'invalid';
      referralId = data.referralId ?? null;
    }
  } catch {
    routeType = 'invalid';
  }

  if (routeType === 'pending' && referralId) {
    redirect(`/referrals/accept/${referralId}?token=${encodeURIComponent(token)}`);
  }

  if (routeType === 'active' && referralId) {
    const returnTo = encodeURIComponent(`/careconnect/referrals/${referralId}`);
    redirect(`/login?returnTo=${returnTo}&reason=referral-view`);
  }

  redirect('/referrals/accept/invalid?reason=expired-or-invalid');
}
