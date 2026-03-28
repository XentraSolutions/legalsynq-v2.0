import { requireOrg } from '@/lib/auth-guards';
import { AppShell } from '@/components/shell/app-shell';

/**
 * Platform layout — wraps all product routes (careconnect, fund, lien).
 * Guards: requires authentication + org membership.
 * Renders the shared AppShell (TopBar + Sidebar).
 */
export default async function PlatformLayout({ children }: { children: React.ReactNode }) {
  await requireOrg();

  return (
    <AppShell>
      {children}
    </AppShell>
  );
}
