/**
 * auth.ts — Control Center authentication facade.
 *
 * Provides getSession() and requirePlatformAdmin() as the
 * canonical entry points for all auth checks in this app.
 *
 * Implementation delegates to session.ts (which calls
 * GET /identity/api/auth/me via the gateway) and auth-guards.ts.
 *
 * TODO: integrate with Identity service session validation
 * TODO: move to HttpOnly secure cookies
 * TODO: support cross-subdomain auth
 */

import { getServerSession } from '@/lib/session';
import { requirePlatformAdmin as _requirePlatformAdmin } from '@/lib/auth-guards';
import type { SessionUser } from '@/types/auth';
import type { PlatformSession } from '@/types';

/**
 * getSession() — reads the current session from the platform_session cookie.
 *
 * Calls GET /identity/api/auth/me to validate the token and populate
 * the session shape. Returns null if the cookie is absent, invalid,
 * or the token has expired.
 *
 * Call only from Server Components, Server Actions, or Route Handlers.
 * Never call from Client Components.
 *
 * TODO: replace remote /auth/me call with a local JWT decode +
 *       periodic revalidation once the Identity key endpoint is stable.
 */
export async function getSession(): Promise<PlatformSession | null> {
  return getServerSession();
}

/**
 * requirePlatformAdmin() — session guard for PlatformAdmin-only pages.
 *
 * - No session cookie  → redirect to /login?reason=unauthenticated
 * - Session invalid    → redirect to /login?reason=unauthenticated
 * - Not PlatformAdmin  → redirect to /login?reason=unauthorized
 * - Otherwise          → returns the full PlatformSession
 *
 * Use at the top of every Control Center Server Component / layout.
 * The middleware provides a first-pass cookie presence check; this
 * guard performs the definitive role check.
 *
 * TODO: add SupportAdmin role bypass once that role is defined
 */
export async function requirePlatformAdmin(): Promise<PlatformSession> {
  return _requirePlatformAdmin();
}

/**
 * toSessionUser() — maps a full PlatformSession to the lighter SessionUser
 * shape used by display components and client-safe auth checks.
 */
export function toSessionUser(session: PlatformSession): SessionUser {
  return {
    id:              session.userId,
    email:           session.email,
    roles:           session.systemRoles,
    isPlatformAdmin: session.isPlatformAdmin,
  };
}
