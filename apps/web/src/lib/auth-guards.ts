import { redirect } from 'next/navigation';
import { getServerSession, requireSession } from '@/lib/session';
import type { PlatformSession, ProductRoleValue } from '@/types';

/**
 * Ensure a valid session exists.
 * Redirects to /login if not.
 */
export async function requireAuthenticated(): Promise<PlatformSession> {
  return requireSession();
}

/**
 * Ensure the session has an org membership.
 * Redirects to /no-org if the user is authenticated but has no org.
 */
export async function requireOrg(): Promise<PlatformSession & { orgId: string }> {
  const session = await requireSession();
  if (!session.hasOrg) redirect('/no-org');
  return session as PlatformSession & { orgId: string };
}

/**
 * Ensure the session includes the given product role.
 * Redirects to /dashboard if not.
 *
 * Usage:
 *   const session = await requireProductRole(ProductRole.SynqFundReferrer);
 */
export async function requireProductRole(role: ProductRoleValue): Promise<PlatformSession> {
  const session = await requireOrg();
  if (!session.productRoles.includes(role)) redirect('/dashboard');
  return session;
}

/**
 * Ensure the session is a TenantAdmin or PlatformAdmin.
 * Redirects to /dashboard if not.
 */
export async function requireAdmin(): Promise<PlatformSession> {
  const session = await requireSession();
  if (!session.isTenantAdmin && !session.isPlatformAdmin) redirect('/dashboard');
  return session;
}

/**
 * Ensure the session is a PlatformAdmin.
 * TenantAdmins are NOT granted access — this guard is strictly for
 * LegalSynq platform administrators operating the Control Center.
 * Redirects to /dashboard if not.
 */
export async function requirePlatformAdmin(): Promise<PlatformSession> {
  const session = await requireSession();
  if (!session.isPlatformAdmin) redirect('/dashboard');
  return session;
}

/**
 * Lightweight check — no redirect. Use in layouts to conditionally render UI.
 */
export async function getOptionalSession(): Promise<PlatformSession | null> {
  return getServerSession();
}
