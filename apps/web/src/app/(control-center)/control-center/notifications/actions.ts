'use server';

import { cookies }               from 'next/headers';
import { redirect }              from 'next/navigation';
import { requireCCPlatformAdmin } from '@/lib/auth-guards';

const COOKIE_NAME = 'cc_tenant_context';

export interface TenantContext {
  tenantId:   string;
  tenantName: string;
  tenantCode: string;
}

/**
 * setNotifTenantContextAction — sets the active tenant context for the
 * embedded Control Center's Notifications section.
 *
 * Writes cc_tenant_context as JSON { tenantId, tenantName, tenantCode }
 * (same format as the standalone CC) so notifWebApi can extract tenantId
 * and forward it as x-tenant-id to the notifications gateway.
 */
export async function setNotifTenantContextAction(ctx: TenantContext): Promise<never> {
  await requireCCPlatformAdmin();

  const cookieStore = cookies();
  cookieStore.set(COOKIE_NAME, JSON.stringify(ctx), {
    httpOnly: false,
    sameSite: process.env.NODE_ENV === 'production' ? 'strict' : 'lax',
    path:     '/',
  });

  redirect('/control-center/notifications');
}

/**
 * clearNotifTenantContextAction — clears the active tenant context.
 */
export async function clearNotifTenantContextAction(): Promise<never> {
  await requireCCPlatformAdmin();

  const cookieStore = cookies();
  cookieStore.delete(COOKIE_NAME);

  redirect('/control-center/notifications');
}
