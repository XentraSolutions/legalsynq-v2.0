'use server';

/**
 * tenant-context.ts — Server Actions for Tenant Context Switching.
 *
 * These actions are the only write path for the cc_tenant_context cookie.
 * All cookie mutations (set/clear) must go through Server Actions — calling
 * cookies().set() inside a Server Component render throws.
 *
 * TODO: persist tenant context in backend session
 * TODO: integrate impersonation with Identity service
 */

import { redirect } from 'next/navigation';
import { setTenantContext, clearTenantContext } from '@/lib/auth';
import type { TenantContext } from '@/types/control-center';

/**
 * switchTenantContextAction — activates a tenant context for the current admin.
 *
 * Writes the TenantContext to the cc_tenant_context cookie and redirects to
 * the root page. The CCShell banner will appear on every subsequent page
 * while the context is active.
 *
 * Usage (Server Component with bound action):
 *   const action = switchTenantContextAction.bind(null, tenantCtx);
 *   <form action={action}><button type="submit">Switch</button></form>
 *
 * TODO: persist tenant context in backend session
 * TODO: emit audit log entry for context switch
 */
export async function switchTenantContextAction(tenant: TenantContext): Promise<never> {
  setTenantContext(tenant);
  redirect('/');
}

/**
 * exitTenantContextAction — clears the active tenant context.
 *
 * Removes the cc_tenant_context cookie and redirects to the tenants list,
 * returning the admin to the global platform view.
 *
 * TODO: persist tenant context in backend session
 * TODO: integrate impersonation with Identity service
 */
export async function exitTenantContextAction(): Promise<never> {
  clearTenantContext();
  redirect('/tenants');
}
