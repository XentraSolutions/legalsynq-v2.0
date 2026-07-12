import { requireAuthenticated } from '@/lib/auth-guards';
import { XeniaSettingsClient } from './xenia-settings-client';

export const dynamic = 'force-dynamic';

export default async function XeniaSettingsPage() {
  const session = await requireAuthenticated();

  return (
    <XeniaSettingsClient
      sessionEmail={session.email}
      tenantCode={session.tenantCode}
    />
  );
}
