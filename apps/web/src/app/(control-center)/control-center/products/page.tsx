import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-server-api';
import { TenantProductEntitlements } from '@/components/control-center/tenant-product-entitlements';

export const dynamic = 'force-dynamic';

interface ProductEntitlementsPageProps {
  searchParams: Promise<{
    tenantId?: string;
  }>;
}

export default async function ProductEntitlementsPage({ searchParams }: ProductEntitlementsPageProps) {
  await requireCCPlatformAdmin();

  const sp = await searchParams;

  let tenantsResult;
  try {
    tenantsResult = await controlCenterServerApi.tenants.list({ page: 1, pageSize: 200 });
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Failed to load tenants.';
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
        {message}
      </div>
    );
  }

  const tenants = tenantsResult.items;
  const selectedTenantId = (sp.tenantId && tenants.some((tenant) => tenant.id === sp.tenantId))
    ? sp.tenantId
    : tenants[0]?.id ?? '';

  let tenantDetail = null;
  let fetchError: string | null = null;

  if (selectedTenantId) {
    try {
      tenantDetail = await controlCenterServerApi.tenants.getById(selectedTenantId);
    } catch (error) {
      fetchError = error instanceof Error ? error.message : 'Failed to load tenant entitlements.';
    }
  }

  return (
    <TenantProductEntitlements
      tenants={tenants}
      selectedTenantId={selectedTenantId}
      tenantDetail={tenantDetail}
      fetchError={fetchError}
    />
  );
}
