import type { ReactNode } from 'react';
import type { TenantDetail } from '@/types/control-center';

interface TenantDetailCardProps {
  tenant: TenantDetail;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'long',
    day:   'numeric',
    year:  'numeric',
  });
}

/**
 * Tenant detail card — sections B, C, D.
 * Pure Server Component: receives a fully-resolved TenantDetail prop.
 *
 * Sections:
 *   B. Core information (contact details, dates)
 *   C. Stats row (users, orgs, products)
 *   D. Product entitlements table
 */
export function TenantDetailCard({ tenant }: TenantDetailCardProps) {
  const enabledCount = tenant.productEntitlements.filter(p => p.enabled).length;

  return (
    <div className="space-y-5">

      {/* ── C. Stats row ──────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <StatCard label="Total Users"      value={tenant.userCount} />
        <StatCard label="Active Users"     value={tenant.activeUserCount} />
        <StatCard label="Linked Orgs"      value={tenant.linkedOrgCount ?? tenant.orgCount} />
        <StatCard label="Products Enabled" value={`${enabledCount} / ${tenant.productEntitlements.length}`} />
      </div>

      {/* ── B. Core information ───────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Core Information
          </h2>
        </div>
        <dl className="divide-y divide-gray-100">
          <InfoRow label="Tenant Type"     value={formatType(tenant.type)} />
          <InfoRow label="Primary Contact" value={tenant.primaryContactName} />
          {tenant.email && (
            <InfoRow
              label="Contact Email"
              value={
                <a href={`mailto:${tenant.email}`} className="text-indigo-600 hover:underline">
                  {tenant.email}
                </a>
              }
            />
          )}
          <InfoRow label="Tenant Code"  value={<code className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded">{tenant.code}</code>} />
          <InfoRow label="Created"      value={formatDate(tenant.createdAtUtc)} />
          <InfoRow label="Last Updated" value={formatDate(tenant.updatedAtUtc)} />
        </dl>
      </div>

    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────────

function StatCard({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <p className="text-xs text-gray-500 font-medium uppercase tracking-wide">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-gray-900">{value}</p>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="px-5 py-3 flex items-center gap-4">
      <dt className="w-36 shrink-0 text-xs font-medium text-gray-500">{label}</dt>
      <dd className="text-sm text-gray-800">{value}</dd>
    </div>
  );
}

function formatType(type: string): string {
  const labels: Record<string, string> = {
    LawFirm:    'Law Firm',
    Provider:   'Provider',
    Corporate:  'Corporate',
    Government: 'Government',
    Other:      'Other',
  };
  return labels[type] ?? type;
}

