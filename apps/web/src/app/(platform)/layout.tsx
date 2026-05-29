import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import { AppShell } from '@/components/shell/app-shell';
import { ToastProvider } from '@/lib/toast-context';
import { ToastContainer } from '@/components/toast-container';
import { getServerSession } from '@/lib/session';
import { ProductRole } from '@/types';

export const dynamic = 'force-dynamic';


/**
 * Platform layout — wraps all product routes (careconnect, fund, lien).
 * Guards: requires authentication + org membership.
 * Renders the shared AppShell (TopBar + Sidebar) and the global toast system.
 *
 * Phase 6B defense-in-depth: CC-only users (no system roles) who have a stale
 * JWT are redirected to /careconnect rather than blocked with an error.
 */
export default async function PlatformLayout({ children }: { children: React.ReactNode }) {
  const session = await requireOrg();

  // Defense-in-depth: CC-only users belong in the CC portal, not the operator portal.
  const hasCcRole =
    session.productRoles.includes(ProductRole.CareConnectReferrer) ||
    session.productRoles.includes(ProductRole.CareConnectReceiver);
  if (session.systemRoles.length === 0 && hasCcRole) {
    redirect('/careconnect');
  }

  return (
    <ToastProvider>
      <AppShell>
        {children}
      </AppShell>
      <ToastContainer />
    </ToastProvider>
  );
}
