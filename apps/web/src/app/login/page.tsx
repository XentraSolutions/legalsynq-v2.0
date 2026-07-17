import { headers } from 'next/headers';
import { LoginPageClient } from './login-page-client';
import { getServerPortalConfig } from '@/lib/portal';

export const dynamic = 'force-dynamic';

/**
 * Login page — server component.
 *
 * Uses getServerPortalConfig to detect whether the visitor is on a
 * product-specific portal hostname. The product id is passed to the client
 * layout so it can render the correct branding without a client-side flash
 * or extra round-trips.
 */
export default async function LoginPage() {
  const hdrs         = await headers();
  const rawHost      = hdrs.get('x-forwarded-host') ?? hdrs.get('host') ?? '';
  const portalConfig = getServerPortalConfig(rawHost);

  return <LoginPageClient portalProductId={portalConfig?.productId ?? null} />;
}
