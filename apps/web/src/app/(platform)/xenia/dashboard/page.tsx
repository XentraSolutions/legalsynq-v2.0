import { requireAuthenticated } from '@/lib/auth-guards';
import { XeniaWorkspaceClient } from './xenia-workspace-client';

export const dynamic = 'force-dynamic';

export default async function XeniaDashboardPage() {
  const session = await requireAuthenticated();

  return (
    <XeniaWorkspaceClient
      sessionEmail={session.email}
      tenantCode={session.tenantCode}
    />
  );
}
