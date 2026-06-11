import { ProductRole, type PlatformSession } from '@/types';

const ALLOWED_COMMON_PORTAL_ROLES = new Set<string>([
  ProductRole.CareConnectReferrer,
  ProductRole.CareConnectReceiver,
]);

export function isEligibleForCareConnectCommonPortal(
  session: Pick<PlatformSession, 'isPlatformAdmin' | 'isTenantAdmin' | 'productRoles'>,
): boolean {
  if (session.isPlatformAdmin || session.isTenantAdmin) {
    return false;
  }

  if (session.productRoles.length === 0) {
    return false;
  }

  return session.productRoles.every(role => ALLOWED_COMMON_PORTAL_ROLES.has(role));
}
