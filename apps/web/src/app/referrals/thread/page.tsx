import { redirect } from 'next/navigation';
import { ThreadClient } from './thread-client';
import { mapFailureReasonToInvalidReason, readPublicReferralFailureReason } from '../lib/public-referral-error';
import { fetchPublicCareConnect } from '../lib/public-referral-proxy';
import { buildCareConnectReferralLoginUrl } from '@/lib/careconnect-login-url';

export const dynamic = 'force-dynamic';

interface Props {
  searchParams: Promise<{ token?: string }>;
}

export default async function ReferralThreadPage({ searchParams }: Props) {
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

  const loginUrl = buildCareConnectReferralLoginUrl(
    process.env.CC_COMMON_PORTAL_HOSTNAME,
    `/careconnect/referrals/${threadData.referralId}`,
  );

  return <ThreadClient token={token} data={threadData} loginUrl={loginUrl} />;
}
