import { requirePlatformAdmin } from '@/lib/auth-guards';
import { ControlCenterShell } from '@/components/shell/control-center-shell';

/**
 * Control Center layout — wraps all /control-center routes.
 * Guard: requires PlatformAdmin system role only.
 * TenantAdmins are not permitted access — use the /admin section instead.
 */
export default async function ControlCenterLayout({ children }: { children: React.ReactNode }) {
  await requirePlatformAdmin();

  return (
    <ControlCenterShell>
      {children}
    </ControlCenterShell>
  );
}
