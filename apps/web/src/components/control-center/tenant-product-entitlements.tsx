'use client';

import { useEffect, useMemo, useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { CCRoutes } from '@/lib/control-center-routes';
import { controlCenterClientApi } from '@/lib/control-center-client-api';
import { PRODUCT_CODE_TO_NAV_KEY, PRODUCT_META } from '@/lib/nav';
import type { TenantDetail, TenantSummary } from '@/types/control-center';
import { ApiError } from '@/lib/api-client';

interface TenantProductEntitlementsProps {
  tenants: TenantSummary[];
  selectedTenantId: string;
  tenantDetail: TenantDetail | null;
  fetchError?: string | null;
}

export function TenantProductEntitlements({
  tenants,
  selectedTenantId,
  tenantDetail,
  fetchError,
}: TenantProductEntitlementsProps) {
  const router = useRouter();
  const [isNavigating, startNavigation] = useTransition();
  const [pendingCode, setPendingCode] = useState<string | null>(null);
  const [localDetail, setLocalDetail] = useState<TenantDetail | null>(tenantDetail);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    setLocalDetail(tenantDetail);
    setActionError(null);
    setPendingCode(null);
  }, [tenantDetail]);

  const selectedTenant = useMemo(
    () => tenants.find((tenant) => tenant.id === selectedTenantId) ?? null,
    [selectedTenantId, tenants],
  );

  function handleTenantChange(nextTenantId: string) {
    startNavigation(() => {
      router.push(`${CCRoutes.products}?tenantId=${encodeURIComponent(nextTenantId)}`);
    });
  }

  async function handleToggle(productCode: string, enabled: boolean) {
    if (!localDetail) return;

    setPendingCode(productCode);
    setActionError(null);

    try {
      await controlCenterClientApi.products.setTenantEntitlement(localDetail.id, productCode, enabled);
      setLocalDetail({
        ...localDetail,
        productEntitlements: localDetail.productEntitlements.map((entitlement) =>
          entitlement.productCode === productCode
            ? {
                ...entitlement,
                enabled,
                status: enabled ? 'Active' : 'Disabled',
                enabledAtUtc: enabled ? (entitlement.enabledAtUtc ?? new Date().toISOString()) : undefined,
              }
            : entitlement,
        ),
      });
      router.refresh();
    } catch (error) {
      setActionError(error instanceof ApiError ? error.message : 'Failed to update product entitlement.');
    } finally {
      setPendingCode(null);
    }
  }

  if (tenants.length === 0) {
    return (
      <div className="rounded-lg border border-gray-200 bg-white p-10 text-center">
        <p className="text-sm text-gray-400">No tenants available.</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-4 rounded-lg border border-gray-200 bg-white p-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="space-y-1">
          <h1 className="text-xl font-semibold text-gray-900">Product Entitlements</h1>
          <p className="text-sm text-gray-500">
            Enable or disable platform products at the tenant level. User-level assignment can only use products enabled here.
          </p>
        </div>

        <div className="w-full sm:w-80">
          <label htmlFor="tenantId" className="mb-1 block text-xs font-medium uppercase tracking-wide text-gray-500">
            Tenant
          </label>
          <select
            id="tenantId"
            value={selectedTenantId}
            onChange={(e) => handleTenantChange(e.target.value)}
            disabled={isNavigating}
            className="w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-indigo-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 disabled:opacity-60"
          >
            {tenants.map((tenant) => (
              <option key={tenant.id} value={tenant.id}>
                {tenant.displayName} ({tenant.code})
              </option>
            ))}
          </select>
        </div>
      </div>

      {fetchError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {actionError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {actionError}
        </div>
      )}

      {localDetail && (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <SummaryCard label="Tenant" value={localDetail.displayName} subvalue={localDetail.code} />
            <SummaryCard label="Status" value={localDetail.status} subvalue={localDetail.isActive ? 'Active' : 'Inactive'} />
            <SummaryCard label="Users" value={String(localDetail.activeUserCount ?? localDetail.userCount)} subvalue={`${localDetail.userCount} total`} />
            <SummaryCard
              label="Products Enabled"
              value={String(localDetail.productEntitlements.filter((item) => item.enabled).length)}
              subvalue={`${localDetail.productEntitlements.length} available`}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-3">
            {localDetail.productEntitlements.map((entitlement) => (
              <ProductCard
                key={entitlement.productCode}
                entitlement={entitlement}
                disabled={pendingCode === entitlement.productCode || isNavigating}
                onToggle={handleToggle}
              />
            ))}
          </div>
        </>
      )}

      {!localDetail && !fetchError && selectedTenant && (
        <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 text-sm text-gray-500">
          Loading entitlements for {selectedTenant.displayName}…
        </div>
      )}
    </div>
  );
}

function SummaryCard({ label, value, subvalue }: { label: string; value: string; subvalue?: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <p className="text-xs font-medium uppercase tracking-wide text-gray-400">{label}</p>
      <p className="mt-2 text-lg font-semibold text-gray-900">{value}</p>
      {subvalue && <p className="mt-1 text-xs text-gray-500">{subvalue}</p>}
    </div>
  );
}

function ProductCard({
  entitlement,
  disabled,
  onToggle,
}: {
  entitlement: TenantDetail['productEntitlements'][number];
  disabled: boolean;
  onToggle: (productCode: string, enabled: boolean) => Promise<void>;
}) {
  const navKey = PRODUCT_CODE_TO_NAV_KEY[entitlement.productCode];
  const meta = navKey ? PRODUCT_META[navKey] : null;

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-gray-50 border border-gray-100">
            {meta?.iconSrc ? (
              <img src={meta.iconSrc} alt="" aria-hidden className="h-5 w-5 object-contain" />
            ) : (
              <i className="ri-apps-line text-base text-gray-400" />
            )}
          </div>
          <div>
            <p className="text-sm font-semibold text-gray-900">{entitlement.productName}</p>
            <p className="text-[11px] font-mono text-gray-400">{entitlement.productCode}</p>
          </div>
        </div>
        <span
          className={[
            'inline-flex rounded-full px-2 py-0.5 text-[11px] font-semibold border',
            entitlement.enabled
              ? 'border-green-200 bg-green-50 text-green-700'
              : 'border-gray-200 bg-gray-100 text-gray-500',
          ].join(' ')}
        >
          {entitlement.enabled ? 'Enabled' : 'Disabled'}
        </span>
      </div>

      <div className="mt-4 flex items-center justify-between gap-3">
        <div className="text-xs text-gray-500">
          {entitlement.enabled
            ? `Enabled${entitlement.enabledAtUtc ? ` on ${formatDateTime(entitlement.enabledAtUtc)}` : ''}`
            : 'Not currently available to tenant users'}
        </div>
        <button
          type="button"
          disabled={disabled}
          onClick={() => onToggle(entitlement.productCode, !entitlement.enabled)}
          className={[
            'rounded-md px-3 py-1.5 text-xs font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-60',
            entitlement.enabled
              ? 'border border-red-200 bg-red-50 text-red-700 hover:bg-red-100'
              : 'bg-indigo-600 text-white hover:bg-indigo-500',
          ].join(' ')}
        >
          {entitlement.enabled ? 'Disable' : 'Enable'}
        </button>
      </div>
    </div>
  );
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}
