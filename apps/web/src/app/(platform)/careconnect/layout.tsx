import { redirect } from 'next/navigation';
import { requireProductAccess, FrontendProductCode } from '@/lib/auth-guards';
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
 * Phase 6A defense-in-depth: also blocks any user whose JWT lacks a CareConnect
 * role altogether, catching stale JWTs issued before the backend guard was deployed.
 * Platform/Tenant admins are allowed through because downstream admin pages apply
 * their own stricter guards.
 */
export default async function CareConnectLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await requireProductAccess(FrontendProductCode.CareConnect);

  // Defense-in-depth: ensure the user holds a CareConnect-capable role.
  // Admins are allowed through because admin routes apply their own requireAdmin guards.
  const hasCcRole =
    session.isPlatformAdmin ||
    session.isTenantAdmin ||
    session.productRoles.includes(ProductRole.CareConnectReferrer) ||
    session.productRoles.includes(ProductRole.CareConnectReceiver) ||
    session.productRoles.includes(ProductRole.CareConnectNetworkManager);
  if (!hasCcRole) {
    redirect('/access-denied');
  }

  return <>{children}</>;
}
