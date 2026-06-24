import { headers } from 'next/headers';
import { buildCareConnectLoginUrl, isCareConnectCommonPortalHost } from '@/lib/careconnect-login-url';
import { ResetPasswordPageClient } from './reset-password-page-client';

export const dynamic = 'force-dynamic';

export default async function ResetPasswordPage() {
  const hdrs    = await headers();
  // SECURITY: x-forwarded-host is trusted here for login-redirect selection only
  // (CareConnect common portal vs default /login). The reverse proxy must strip
  // or overwrite this header from external traffic before forwarding — same
  // requirement as proxy.ts and the forgot-password/login BFF routes.
  const rawHost = hdrs.get('x-forwarded-host') ?? hdrs.get('host') ?? '';
  const isPortal = isCareConnectCommonPortalHost(rawHost);
  const loginHref = isPortal
    ? buildCareConnectLoginUrl(process.env.CC_COMMON_PORTAL_HOSTNAME)
    : '/login';

  return <ResetPasswordPageClient isPortal={isPortal} loginHref={loginHref} />;
}
