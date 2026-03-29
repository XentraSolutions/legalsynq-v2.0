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
 * Both actions require an active PlatformAdmin session.
 *
 * Impersonation takes priority over tenant context:
 *   - starting impersonation does NOT clear the tenant context cookie.
 *   - stopping impersonation does NOT clear the tenant context cookie.
 *   The admin returns to whatever scoped view they had beforehand.
 *
 * TODO: integrate with Identity service impersonation endpoint
 * TODO: issue temporary impersonation token
 * TODO: audit log impersonation events
 */

import { redirect } from 'next/navigation';
import { getSession } from '@/lib/auth';
import { setImpersonation, clearImpersonation } from '@/lib/auth';
import type { UserImpersonationSession } from '@/types/control-center';

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
 * After writing the cookie, redirects to "/" so the global shell immediately
 * renders the impersonation banner.
 */
export async function startImpersonationAction(user: {
  id:         string;
  email:      string;
  tenantId:   string;
  tenantName: string;
}): Promise<never> {
  const session = await getSession();
  if (!session) redirect('/login?reason=unauthenticated');

  const impersonation: UserImpersonationSession = {
    adminId:               session.userId,
    impersonatedUserId:    user.id,
    impersonatedUserEmail: user.email,
    tenantId:              user.tenantId,
    tenantName:            user.tenantName,
    startedAtUtc:          new Date().toISOString(),
  };

  setImpersonation(impersonation);

  // TODO: integrate with Identity service impersonation endpoint
  // TODO: issue temporary impersonation token
  // TODO: audit log impersonation start event

  redirect('/');
}

// ── stopImpersonationAction ───────────────────────────────────────────────────

/**
 * stopImpersonationAction() — end the current impersonation session.
 *
 * Clears the cc_impersonation cookie. The cc_tenant_context cookie is
 * intentionally preserved so the admin returns to their previous scoped view.
 *
 * Redirects to /tenant-users after clearing so the admin lands in context.
 */
export async function stopImpersonationAction(): Promise<never> {
  clearImpersonation();

  // TODO: revoke impersonation token on Identity service
  // TODO: audit log impersonation stop event

  redirect('/tenant-users');
}
