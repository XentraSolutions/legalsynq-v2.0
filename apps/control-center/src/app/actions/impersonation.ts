'use server';

/**
 * impersonation.ts — Server Actions for user-level impersonation.
 *
 * startImpersonationAction(user) — sets the cc_impersonation cookie and
 *   redirects to "/" so the shell immediately picks up the banner.
 *
 * stopImpersonationAction()      — clears the cookie and redirects to
 *   /tenant-users so the admin lands back in the user list.
 *
 * ── Security guards ──────────────────────────────────────────────────────────
 *
 *   Both actions call requirePlatformAdmin() before any mutation.
 *   This performs a full server-side session + role check:
 *     - No session cookie  → redirect /login?reason=unauthenticated
 *     - Session invalid    → redirect /login?reason=unauthenticated
 *     - Not PlatformAdmin  → redirect /login?reason=unauthorized
 *
 * ── Tenant context auto-alignment ────────────────────────────────────────────
 *
 *   startImpersonationAction writes the matching TenantContext whenever a
 *   context is not already set or is set to a different tenant. This ensures
 *   the cc_tenant_context and cc_impersonation cookies always agree on tenantId,
 *   satisfying the cross-tenant scope check in getImpersonation().
 *
 * ── Audit logging ─────────────────────────────────────────────────────────────
 *
 *   Both actions emit structured audit log entries (audit.impersonation.start
 *   and audit.impersonation.stop) that are visible in dev and captured in
 *   production NDJSON logs.
 *
 * TODO: integrate with Identity service impersonation endpoint
 * TODO: issue temporary impersonation token
 * TODO: persist audit events to AuditLog table via Identity service
 */

import { redirect } from 'next/navigation';
import {
  requirePlatformAdmin,
  setImpersonation,
  clearImpersonation,
  getTenantContext,
  setTenantContext,
  getImpersonation,
  logImpersonationStart,
  logImpersonationStop,
} from '@/lib/auth';
import type { UserImpersonationSession, TenantContext } from '@/types/control-center';

// ── startImpersonationAction ──────────────────────────────────────────────────

/**
 * startImpersonationAction(user) — begin impersonating a tenant user.
 *
 * Accepts a minimal user descriptor (id, email, tenantId, tenantName) rather
 * than the full UserDetail shape so the action can be bound server-side:
 *
 *   const action = startImpersonationAction.bind(null, {
 *     id:         user.id,
 *     email:      user.email,
 *     tenantId:   user.tenantId,
 *     tenantName: user.tenantDisplayName,
 *   });
 *
 * Security:
 *   - requirePlatformAdmin() is called first — unauthenticated or non-admin
 *     callers are redirected before any mutation occurs.
 *   - If the active tenant context does not match the target user's tenantId,
 *     it is automatically overwritten to prevent a cross-tenant mismatch that
 *     would cause getImpersonation() to reject the session on first read.
 *
 * After writing the cookies, redirects to "/" so the global shell immediately
 * renders the impersonation banner.
 */
export async function startImpersonationAction(user: {
  id:         string;
  email:      string;
  tenantId:   string;
  tenantName: string;
}): Promise<never> {
  const session = await requirePlatformAdmin();

  // Auto-align tenant context with the impersonation target.
  // getImpersonation() enforces tenantId matching — if we write an impersonation
  // cookie without matching the tenant context, the first read would reject it.
  const currentTenantCtx = getTenantContext();
  if (!currentTenantCtx || currentTenantCtx.tenantId !== user.tenantId) {
    const alignedCtx: TenantContext = {
      tenantId:   user.tenantId,
      tenantName: user.tenantName,
      tenantCode: user.tenantName.toUpperCase().replace(/\s+/g, '').slice(0, 10),
    };
    setTenantContext(alignedCtx);
  }

  const impersonation: UserImpersonationSession = {
    adminId:               session.userId,
    impersonatedUserId:    user.id,
    impersonatedUserEmail: user.email,
    tenantId:              user.tenantId,
    tenantName:            user.tenantName,
    startedAtUtc:          new Date().toISOString(),
  };

  setImpersonation(impersonation);

  // Emit audit log immediately after writing the cookie
  logImpersonationStart(session.userId, user.id, user.tenantId);

  // TODO: integrate with Identity service impersonation endpoint
  // TODO: issue temporary impersonation token
  // TODO: persist to AuditLog table via Identity service

  redirect('/');
}

// ── stopImpersonationAction ───────────────────────────────────────────────────

/**
 * stopImpersonationAction() — end the current impersonation session.
 *
 * Clears the cc_impersonation cookie. The cc_tenant_context cookie is
 * intentionally preserved so the admin returns to their previous scoped view.
 *
 * Security:
 *   - requirePlatformAdmin() is called first — ensures the session is still
 *     valid before attempting to read or clear the impersonation cookie.
 *
 * Redirects to /tenant-users after clearing so the admin lands in context.
 */
export async function stopImpersonationAction(): Promise<never> {
  const session = await requirePlatformAdmin();

  // Read current impersonation before clearing so we can include it in the
  // audit log entry (after clearing, the cookie is gone).
  const impersonation = getImpersonation();

  clearImpersonation();

  // Emit audit log after clearing the cookie
  if (impersonation) {
    logImpersonationStop(session.userId, impersonation.impersonatedUserId, impersonation.tenantId);
  } else {
    // Stop called with no active impersonation — log for visibility
    logImpersonationStop(session.userId, 'none', 'none');
  }

  // TODO: revoke impersonation token on Identity service
  // TODO: persist to AuditLog table via Identity service

  redirect('/tenant-users');
}
