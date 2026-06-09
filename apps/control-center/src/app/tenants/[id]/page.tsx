import { cookies } from 'next/headers';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { getCachedTenantById }            from '@/lib/tenant-fetch';
import { TenantDetailCard }               from '@/components/tenants/tenant-detail-card';
import { ProductEntitlementsPanel }       from '@/components/tenants/product-entitlements-panel';
import { TenantAccessCodePanel }          from '@/components/tenants/tenant-access-code-panel';
import { TenantSessionSettingsPanel }     from '@/components/tenants/tenant-session-settings-panel';
import { TenantLogoUpload }              from '@/components/tenants/TenantLogoUpload';
import { TenantOrganizationsPanel }      from '@/components/tenants/tenant-organizations-panel';
import { TenantBillingPanel }            from '@/components/billing/tenant-billing-panel';
import { BillingEntitlementPanel }       from '@/components/billing/billing-entitlement-panel';
import { BillingProfileActionsPanel }    from '@/components/billing/billing-profile-actions-panel';
import { BillingProfileLifecyclePanel }  from '@/components/billing/billing-profile-lifecycle-panel';
import type { TenantBillingSummary, BillingEntitlementSnapshot, PlatformSetting } from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface TenantDetailPageProps {
  params: Promise<{ id: string }>;
}

async function bffFetch<T>(path: string, cookieHeader: string): Promise<T> {
  const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const res  = await fetch(`${base}${path}`, { cache: 'no-store', headers: { cookie: cookieHeader } });
  if (!res.ok) throw new Error(`BFF ${path} failed: ${res.status}`);
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
  let tenantBillingSummary:    TenantBillingSummary      | null = null;
  let tenantBillingError:      string | null                    = null;
  let billingEntitlement:      BillingEntitlementSnapshot | null = null;
  let billingEntitlementError: string | null                    = null;
  let settings:                PlatformSetting[]                = [];
  let accessCodeStatus: Awaited<ReturnType<typeof controlCenterServerApi.tenants.getAccessCode>> | null = null;

  const cookieStore  = await cookies();
  const cookieHeader = cookieStore.getAll().map(c => `${c.name}=${c.value}`).join('; ');

  const [orgsResult, billingResult, entitlementResult] = await Promise.allSettled([
    controlCenterServerApi.organizations.listByTenant(id),
    bffFetch<TenantBillingSummary>(`/api/billing/tenant-summary/${id}`, cookieHeader),
    bffFetch<BillingEntitlementSnapshot>(`/api/billing/entitlements/${id}`, cookieHeader),
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

  if (entitlementResult.status === 'fulfilled') {
    billingEntitlement = entitlementResult.value;
  } else {
    billingEntitlementError = entitlementResult.reason instanceof Error
      ? entitlementResult.reason.message
      : 'Failed to load entitlement data.';
  }

  try {
    settings = await controlCenterServerApi.settings.list();
  } catch {
    // Non-fatal — tenant.hostname remains the preferred display value.
  }

  try {
    accessCodeStatus = await controlCenterServerApi.tenants.getAccessCode(id);
  } catch {
    // Non-fatal — omit the panel if this fetch fails.
  }

  const portalBaseDomain = String(
    settings.find(s => s.key === 'platform.portalBaseDomain')?.value ?? '',
  );

  return (
    <div className="space-y-5">
      <TenantDetailCard tenant={tenant} portalBaseDomain={portalBaseDomain} />

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

      {accessCodeStatus && (
        <TenantAccessCodePanel
          tenantId={tenant.id}
          initialStatus={accessCodeStatus}
        />
      )}

      <ProductEntitlementsPanel
        tenantId={tenant.id}
        entitlements={tenant.productEntitlements}
      />

      <TenantBillingPanel
        summary={tenantBillingSummary}
        error={tenantBillingError}
      />

      <BillingEntitlementPanel
        snapshot={billingEntitlement}
        error={billingEntitlementError ?? billingEntitlement?.error ?? null}
        tenantId={id}
      />

      {tenantBillingSummary?.profileFound && tenantBillingSummary.profile && (
        <BillingProfileActionsPanel
          profileId={tenantBillingSummary.profile.id}
          currentStatus={tenantBillingSummary.profile.status}
          tenantId={id}
        />
      )}

      {tenantBillingSummary?.profileFound && tenantBillingSummary.profile && (
        <BillingProfileLifecyclePanel
          profileId={tenantBillingSummary.profile.id}
        />
      )}
    </div>
  );
}
