import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { TenantDetailCard }               from '@/components/tenants/tenant-detail-card';
import { ProductEntitlementsPanel }       from '@/components/tenants/product-entitlements-panel';
import { TenantSessionSettingsPanel }     from '@/components/tenants/tenant-session-settings-panel';

interface TenantDetailPageProps {
  params: { id: string };
}

/**
 * /tenants/[id] — Tenant detail body (Overview tab).
 *
 * The shared header (breadcrumb, tenant name/status/actions, sub-nav tabs)
 * is rendered by the parent layout.tsx — this page returns only body content.
 *
 * Access: PlatformAdmin only (enforced by layout + requirePlatformAdmin below).
 */
export default async function TenantDetailPage({ params }: TenantDetailPageProps) {
  await requirePlatformAdmin();
  const { id } = params;

  let tenant     = null;
  let fetchError: string | null = null;

  try {
    tenant = await controlCenterServerApi.tenants.getById(id);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load tenant.';
  }

  if (fetchError) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
        {fetchError}
      </div>
    );
  }

  if (!tenant) return null;

  return (
    <div className="space-y-5">
      <TenantDetailCard tenant={tenant} />

      <TenantSessionSettingsPanel
        tenantId={tenant.id}
        sessionTimeoutMinutes={tenant.sessionTimeoutMinutes}
      />

      <ProductEntitlementsPanel
        tenantId={tenant.id}
        entitlements={tenant.productEntitlements}
      />
    </div>
  );
}
