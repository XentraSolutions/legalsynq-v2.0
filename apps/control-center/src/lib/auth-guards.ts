import { redirect } from 'next/navigation';
import { getServerSession } from '@/lib/session';
import { BASE_PATH } from '@/lib/app-config';
import type { PlatformSession } from '@/types';

// TODO: integrate with Identity service session validation
// TODO: support cross-subdomain auth

/**
 * Control Center auth guard — requires PlatformAdmin system role.
 *
 * Used at the top of every Control Center Server Component and layout.
 *
 *   No session       → redirect to /login?reason=unauthenticated
 *   Not PlatformAdmin → redirect to /login?reason=unauthorized
 *
 * This is the ONLY auth guard needed in the standalone Control Center.
 * There is no TenantAdmin path, no org requirement, no product role requirement.
 */
export async function requirePlatformAdmin(): Promise<PlatformSession> {
  const session = await getServerSession();
  if (!session)               redirect(`${BASE_PATH}/login?reason=unauthenticated`);
  if (!session.isPlatformAdmin) redirect(`${BASE_PATH}/login?reason=unauthorized`);
  return session;
}
