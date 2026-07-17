import { headers } from 'next/headers';
import { getServerPortalConfig } from '@/lib/portal';
import { ForgotPasswordPageClient } from './forgot-password-page-client';

export const dynamic = 'force-dynamic';

export default async function ForgotPasswordPage() {
  const hdrs    = await headers();
  // SECURITY: x-forwarded-host is trusted here for portal layout selection only
  // (CareConnect vs LegalSynq branding). The reverse proxy must strip or overwrite
  // this header from external traffic before forwarding — same requirement as route.ts.
  const rawHost = hdrs.get('x-forwarded-host') ?? hdrs.get('host') ?? '';
  const isPortal = getServerPortalConfig(rawHost)?.productId === 'careconnect';

  return <ForgotPasswordPageClient isPortal={isPortal} />;
}
