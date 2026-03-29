import { redirect } from 'next/navigation';
import { getServerSession } from '@/lib/session';
import type { PlatformSession } from '@/types';

/**
 * Control Center auth guard — requires PlatformAdmin system role.
 *
 * Used at the top of every Control Center Server Component and layout.
 *
 *   No session      → redirect to /login
 *   Not PlatformAdmin → redirect to /login (no operator portal dashboard on this host)
 *
 * This is the ONLY auth guard needed in the standalone Control Center.
 * There is no TenantAdmin path, no org requirement, no product role requirement.
 */
export async function requirePlatformAdmin(): Promise<PlatformSession> {
  const session = await getServerSession();
  if (!session) redirect('/login');
  if (!session.isPlatformAdmin) redirect('/login');
  return session;
}
