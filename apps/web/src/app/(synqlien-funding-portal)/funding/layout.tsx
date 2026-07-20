import { redirect } from 'next/navigation';
import { SynqLienFundingPortalShell } from '@/components/synqlien-funding-portal/portal-shell';
import { FrontendProductCode, requireOrg, sessionHasProductAccess } from '@/lib/auth-guards';
import { isEligibleForSynqLienFundingPortal } from '@/lib/synqlien-funding-portal';

export const dynamic = 'force-dynamic';

export const metadata = { title: 'SynqLien Funding Portal' };

export default async function SynqLienFundingLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await requireOrg();

  if (!sessionHasProductAccess(session, FrontendProductCode.SynqLien)) {
    redirect('/access-denied');
  }

  if (!isEligibleForSynqLienFundingPortal(session)) {
    redirect('/access-denied');
  }

  return (
    <SynqLienFundingPortalShell session={session}>
      {children}
    </SynqLienFundingPortalShell>
  );
}
