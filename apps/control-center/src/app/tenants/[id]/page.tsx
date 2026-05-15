import { cookies } from 'next/headers';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { getCachedTenantById }            from '@/lib/tenant-fetch';
import { TenantDetailCard }               from '@/components/tenants/tenant-detail-card';
import { ProductEntitlementsPanel }       from '@/components/tenants/product-entitlements-panel';
import { TenantSessionSettingsPanel }     from '@/components/tenants/tenant-session-settings-panel';
import { TenantLogoUpload }              from '@/components/tenants/TenantLogoUpload';
import { TenantOrganizationsPanel }      from '@/components/tenants/tenant-organizations-panel';
import { TenantBillingPanel }            from '@/components/billing/tenant-billing-panel';
import type { TenantBillingSummary }     from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface TenantDetailPageProps {
  params: Promise<{ id: string }>;
}

async function fetchTenantBillingSummary(tenantId: string): Promise<TenantBillingSummary> {
  const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const cookieStore = await cookies();
  const cookieHeader = cookieStore.getAll().map(c => `${c.name}=${c.value}`).join('; ');
  const res = await fetch(`${base}/api/billing/tenant-summary/${tenantId}`, {
    cache: 'no-store',
    headers: { cookie: cookieHeader },
  });
  if (!res.ok) throw new Error(`Tenant billing summary failed: ${res.status}`);
  return res.json();
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

  let tenant = null;

  try {
    tenant = await getCachedTenantById(id);
  } catch {
    // The layout already renders the error banner for this tenant fetch.
    // Returning null here prevents a duplicate error box from appearing.
    return null;
  }

  if (!tenant) return null;

  let organizations: Awaited<ReturnType<typeof controlCenterServerApi.organizations.listByTenant>> = [];
  let tenantBillingSummary: TenantBillingSummary | null = null;
  let tenantBillingError:   string | null              = null;

  const [orgsResult, billingResult] = await Promise.allSettled([
    controlCenterServerApi.organizations.listByTenant(id),
    fetchTenantBillingSummary(id),
  ]);

  if (orgsResult.status === 'fulfilled') {
    organizations = orgsResult.value;
  }

  if (billingResult.status === 'fulfilled') {
    tenantBillingSummary = billingResult.value;
  } else {
    tenantBillingError = billingResult.reason instanceof Error
      ? billingResult.reason.message
      : 'Failed to load tenant billing data.';
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

      <TenantBillingPanel
        summary={tenantBillingSummary}
        error={tenantBillingError}
      />
    </div>
  );
}
