import { ProductRole, type PlatformSession } from '@/types';

const ALLOWED_FUNDING_PORTAL_ROLES = new Set<string>([
  ProductRole.SynqLienBuyer,
]);

export function isEligibleForSynqLienFundingPortal(
  session: Pick<PlatformSession, 'isPlatformAdmin' | 'isTenantAdmin' | 'productRoles'> &
    Partial<Pick<PlatformSession, 'systemRoles'>>,
): boolean {
  if (session.isPlatformAdmin || session.isTenantAdmin) {
    return false;
  }

  if ((session.systemRoles?.length ?? 0) > 0) {
    return false;
  }

  const synqLienRoles = session.productRoles.filter(role => role.startsWith('SYNQ_LIENS:'));

  if (!synqLienRoles.includes(ProductRole.SynqLienBuyer)) {
    return false;
  }

  if (synqLienRoles.includes(ProductRole.SynqLienSeller)) {
    return false;
  }

  return synqLienRoles.every(role => ALLOWED_FUNDING_PORTAL_ROLES.has(role));
}
