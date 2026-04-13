import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { TenantDetailCard }               from '@/components/tenants/tenant-detail-card';
import { ProductEntitlementsPanel }       from '@/components/tenants/product-entitlements-panel';
import { TenantSessionSettingsPanel }     from '@/components/tenants/tenant-session-settings-panel';
import { TenantLogoUpload }              from '@/components/tenants/TenantLogoUpload';
import { TenantOrganizationsPanel }      from '@/components/tenants/tenant-organizations-panel';

interface TenantDetailPageProps {
  params: Promise<{ id: string }>;
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
  const { id } = await params;

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

  let organizations: Awaited<ReturnType<typeof controlCenterServerApi.organizations.listByTenant>> = [];
  try {
    organizations = await controlCenterServerApi.organizations.listByTenant(id);
  } catch {
    // non-fatal
  }

  return (
    <div className="space-y-5">
      <TenantDetailCard tenant={tenant} />

      <TenantLogoUpload
        tenantId={tenant.id}
        logoDocumentId={tenant.logoDocumentId}
        logoWhiteDocumentId={tenant.logoWhiteDocumentId}
      />

      <TenantOrganizationsPanel organizations={organizations} />

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
