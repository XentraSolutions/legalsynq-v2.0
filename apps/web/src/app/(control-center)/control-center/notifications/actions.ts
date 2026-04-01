'use server';

import { cookies }                from 'next/headers';
import { requireCCPlatformAdmin } from '@/lib/auth-guards';

const COOKIE_NAME = 'cc_tenant_context';

export interface TenantContext {
  tenantId:   string;
  tenantName: string;
  tenantCode: string;
}

/**
 * setNotifTenantContextAction — activates a tenant for the CC Notifications section.
 *
 * Writes cc_tenant_context as JSON { tenantId, tenantName, tenantCode } —
 * the same format the standalone CC uses. Callers are responsible for
 * calling router.refresh() to re-render Server Components after this returns.
 */
export async function setNotifTenantContextAction(ctx: TenantContext): Promise<void> {
  await requireCCPlatformAdmin();

  const cookieStore = cookies();
  cookieStore.set(COOKIE_NAME, JSON.stringify(ctx), {
    httpOnly: false,
    sameSite: process.env.NODE_ENV === 'production' ? 'strict' : 'lax',
    path:     '/',
  });
}

/**
 * clearNotifTenantContextAction — clears the active tenant context.
 * Callers call router.refresh() to re-render after this returns.
 */
export async function clearNotifTenantContextAction(): Promise<void> {
  await requireCCPlatformAdmin();

  const cookieStore = cookies();
  cookieStore.delete(COOKIE_NAME);
}
