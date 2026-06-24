import { headers } from 'next/headers';
import { LoginPageClient } from './login-page-client';
import { getServerPortalConfig } from '@/lib/portal';

export const dynamic = 'force-dynamic';

/**
 * Login page — server component.
 *
 * Uses getServerPortalConfig to detect whether the visitor is on a portal
 * hostname (e.g. careconnect-demo.legalsynq.com in prod, or localhost when
 * CC_PORTAL_SUBDOMAIN=localhost in dev). Passes `isPortal` to the
 * client layout so it can render CareConnect branding without a client-side
 * flash or extra round-trips.
 */
export default async function LoginPage() {
  const hdrs      = await headers();
  const rawHost   = hdrs.get('x-forwarded-host') ?? hdrs.get('host') ?? '';
  const isPortal  = getServerPortalConfig(rawHost) !== null;

  return <LoginPageClient isPortal={isPortal} />;
}
