import { requireOrg } from '@/lib/auth-guards';
import { tenantServerApi } from '@/lib/tenant-api';
import { AppShell } from '@/components/shell/app-shell';
import { ToastProvider } from '@/lib/toast-context';
import { ToastContainer } from '@/components/toast-container';

export const dynamic = 'force-dynamic';


/**
 * Platform layout — wraps all product routes (careconnect, fund, lien).
 * Guards: requires authentication + org membership.
 * Renders the shared AppShell (TopBar + Sidebar) and the global toast system.
 */
export default async function PlatformLayout({ children }: { children: React.ReactNode }) {
  const session = await requireOrg();

  let initialMapProvider: 'google' | 'osm' = 'google';
  let initialTimezone: string | undefined;

  try {
    const [mapSetting, tzSetting] = await Promise.all([
      tenantServerApi.getMapProviderSetting(session.tenantId),
      tenantServerApi.getTimezoneSetting(session.tenantId),
    ]);
    if (mapSetting.value === 'osm' || mapSetting.value === 'google') {
      initialMapProvider = mapSetting.value;
    }
    if (tzSetting.value) {
      initialTimezone = tzSetting.value;
    }
  } catch {
    // Fall back to global defaults — non-fatal
  }

  return (
    <ToastProvider>
      <AppShell initialMapProvider={initialMapProvider} initialTimezone={initialTimezone}>
        {children}
      </AppShell>
      <ToastContainer />
    </ToastProvider>
  );
}
