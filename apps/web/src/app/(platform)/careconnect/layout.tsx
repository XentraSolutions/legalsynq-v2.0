import { redirect } from 'next/navigation';
import { requireProductAccess, FrontendProductCode } from '@/lib/auth-guards';
import { getServerSession } from '@/lib/session';
import { ProductRole } from '@/types';

export const dynamic = 'force-dynamic';


/**
 * LS-ID-TNT-010 — CareConnect product layout guard.
 *
 * Enforces product-level access at route-group level before any page
 * under /careconnect/* is rendered. Users without the CareConnect product
 * in their effective access list are redirected to /access-denied.
 *
 * PlatformAdmins and TenantAdmins bypass the check implicitly.
 *
 * Phase 6A defense-in-depth: also blocks any user whose JWT lacks a CC role
 * (CARECONNECT_REFERRER or CARECONNECT_RECEIVER), catching stale JWTs issued
 * before the backend guard was deployed.
 */
export default async function CareConnectLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await requireProductAccess(FrontendProductCode.CareConnect);

  // Defense-in-depth: ensure the user holds a CC product role.
  // PlatformAdmins and TenantAdmins with stale JWTs are allowed through.
  if (!session.isPlatformAdmin && !session.isTenantAdmin) {
    const hasCcRole =
      session.productRoles.includes(ProductRole.CareConnectReferrer) ||
      session.productRoles.includes(ProductRole.CareConnectReceiver);
    if (!hasCcRole) {
      redirect('/access-denied');
    }
  }

  return <>{children}</>;
}
