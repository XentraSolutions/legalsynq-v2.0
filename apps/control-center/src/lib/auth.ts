/**
 * auth.ts — Control Center authentication facade.
 *
 * Provides getSession() and requirePlatformAdmin() as the
 * canonical entry points for all auth checks in this app.
 *
 * Also owns the tenant context cookie helpers that power
 * Tenant Context Switching and Impersonation flows.
 *
 * Implementation delegates to session.ts (which calls
 * GET /identity/api/auth/me via the gateway) and auth-guards.ts.
 *
 * TODO: integrate with Identity service session validation
 * TODO: move to HttpOnly secure cookies
 * TODO: support cross-subdomain auth
 * TODO: persist tenant context in backend session
 */

import { cookies } from 'next/headers';
import { getServerSession } from '@/lib/session';
import { requirePlatformAdmin as _requirePlatformAdmin } from '@/lib/auth-guards';
import { TENANT_CONTEXT_COOKIE_NAME, IMPERSONATION_COOKIE_NAME } from '@/lib/app-config';
import type { SessionUser } from '@/types/auth';
import type { PlatformSession } from '@/types';
import type { TenantContext, UserImpersonationSession } from '@/types/control-center';

// ── Session ───────────────────────────────────────────────────────────────────

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

// ── Tenant Context ────────────────────────────────────────────────────────────

/**
 * getTenantContext() — reads the active tenant context cookie.
 *
 * Returns a TenantContext if the platform admin has switched into a tenant,
 * or null when no tenant is selected (global admin view).
 *
 * Safe to call from Server Components, Server Actions, and Route Handlers.
 *
 * The value is stored as JSON in the cc_tenant_context cookie. If the cookie
 * is present but malformed the function returns null to avoid hard failures.
 */
export function getTenantContext(): TenantContext | null {
  const cookieStore = cookies();
  const raw = cookieStore.get(TENANT_CONTEXT_COOKIE_NAME)?.value;
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      'tenantId'   in parsed && typeof (parsed as Record<string, unknown>).tenantId   === 'string' &&
      'tenantName' in parsed && typeof (parsed as Record<string, unknown>).tenantName === 'string' &&
      'tenantCode' in parsed && typeof (parsed as Record<string, unknown>).tenantCode === 'string'
    ) {
      return parsed as TenantContext;
    }
    return null;
  } catch {
    return null;
  }
}

/**
 * setTenantContext() — writes the active tenant context cookie.
 *
 * IMPORTANT: Must only be called from a Server Action or Route Handler.
 * Calling this inside a Server Component render will throw:
 *   "Cookies can only be modified in a Server Action or Route Handler."
 *
 * Cookie options:
 *   - httpOnly: false — not an auth credential; client JS may read it for
 *               optimistic UI state (e.g. banners) without a server round-trip.
 *   - sameSite: 'lax' — adequate for non-sensitive UI state.
 *   - path: '/' — available across all Control Center routes.
 *   - No maxAge — expires with the browser session; cleared on logout anyway.
 *
 * TODO: persist tenant context in backend session
 */
export function setTenantContext(tenant: TenantContext): void {
  const cookieStore = cookies();
  cookieStore.set(TENANT_CONTEXT_COOKIE_NAME, JSON.stringify(tenant), {
    httpOnly: false,
    secure:   process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    path:     '/',
  });
}

/**
 * clearTenantContext() — removes the active tenant context cookie.
 *
 * IMPORTANT: Must only be called from a Server Action or Route Handler.
 *
 * Called:
 *   - on logout (BFF /api/auth/logout)
 *   - when the admin clicks "Exit tenant context"
 *   - when navigating back to the global admin view
 *
 * TODO: persist tenant context in backend session
 */
export function clearTenantContext(): void {
  const cookieStore = cookies();
  cookieStore.delete(TENANT_CONTEXT_COOKIE_NAME);
}

// ── User Impersonation ────────────────────────────────────────────────────────

/**
 * getImpersonation() — reads the active user impersonation cookie.
 *
 * Returns a UserImpersonationSession when a platform admin is actively
 * impersonating a tenant user, or null otherwise.
 *
 * Safe to call from Server Components, Server Actions, and Route Handlers.
 *
 * The value is stored as JSON in the cc_impersonation cookie. If the cookie
 * is present but malformed the function returns null to avoid hard failures.
 *
 * TODO: integrate with Identity service impersonation endpoint
 * TODO: validate impersonation token server-side
 */
export function getImpersonation(): UserImpersonationSession | null {
  const cookieStore = cookies();
  const raw = cookieStore.get(IMPERSONATION_COOKIE_NAME)?.value;
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      'adminId'               in parsed && typeof (parsed as Record<string, unknown>).adminId               === 'string' &&
      'impersonatedUserId'    in parsed && typeof (parsed as Record<string, unknown>).impersonatedUserId    === 'string' &&
      'impersonatedUserEmail' in parsed && typeof (parsed as Record<string, unknown>).impersonatedUserEmail === 'string' &&
      'tenantId'              in parsed && typeof (parsed as Record<string, unknown>).tenantId              === 'string' &&
      'startedAtUtc'          in parsed && typeof (parsed as Record<string, unknown>).startedAtUtc          === 'string'
    ) {
      return parsed as UserImpersonationSession;
    }
    return null;
  } catch {
    return null;
  }
}

/**
 * setImpersonation() — writes the user impersonation cookie.
 *
 * IMPORTANT: Must only be called from a Server Action or Route Handler.
 *
 * Cookie options:
 *   - httpOnly: false — not an auth credential; client JS may read it for
 *               optimistic UI state (e.g. banners) without a server round-trip.
 *   - sameSite: 'lax' — adequate for non-sensitive UI state.
 *   - path: '/' — available across all Control Center routes.
 *   - No maxAge — expires with the browser session.
 *
 * TODO: integrate with Identity service impersonation endpoint
 * TODO: issue temporary impersonation token
 * TODO: audit log impersonation start event
 */
export function setImpersonation(session: UserImpersonationSession): void {
  const cookieStore = cookies();
  cookieStore.set(IMPERSONATION_COOKIE_NAME, JSON.stringify(session), {
    httpOnly: false,
    secure:   process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    path:     '/',
  });
}

/**
 * clearImpersonation() — removes the user impersonation cookie.
 *
 * IMPORTANT: Must only be called from a Server Action or Route Handler.
 *
 * Called:
 *   - on logout (BFF /api/auth/logout)
 *   - when the admin clicks "Exit Impersonation"
 *
 * Tenant context (cc_tenant_context) is NOT cleared — it persists so the admin
 * returns to the scoped view they had before starting impersonation.
 *
 * TODO: revoke impersonation token on Identity service
 * TODO: audit log impersonation stop event
 */
export function clearImpersonation(): void {
  const cookieStore = cookies();
  cookieStore.delete(IMPERSONATION_COOKIE_NAME);
}
