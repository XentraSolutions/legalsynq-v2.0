import type { ReactNode } from 'react';
import type { TenantDetail, ProvisioningStatus } from '@/types/control-center';
import { RetryProvisioningButton } from './retry-provisioning-button';

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

function provisioningStatusBadge(status?: ProvisioningStatus) {
  if (!status) return null;
  const styles: Record<ProvisioningStatus, string> = {
    Pending:    'bg-gray-100 text-gray-600 border-gray-200',
    InProgress: 'bg-blue-50 text-blue-700 border-blue-200',
    Active:     'bg-green-50 text-green-700 border-green-200',
    Failed:     'bg-red-50 text-red-700 border-red-200',
  };
  const labels: Record<ProvisioningStatus, string> = {
    Pending:    'Pending',
    InProgress: 'In Progress',
    Active:     'Active',
    Failed:     'Failed',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status]}`}>
      {labels[status] ?? status}
    </span>
  );
}

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

      {/* ── Subdomain / Provisioning ─────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Subdomain &amp; Provisioning
          </h2>
        </div>
        <dl className="divide-y divide-gray-100">
          <InfoRow label="Subdomain" value={
            tenant.subdomain
              ? <code className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded">{tenant.subdomain}</code>
              : <span className="text-gray-400 italic">Not set</span>
          } />
          <InfoRow label="Provisioning" value={provisioningStatusBadge(tenant.provisioningStatus)} />
          {tenant.hostname && (
            <InfoRow label="Hostname" value={
              <code className="font-mono text-xs bg-blue-50 text-blue-700 px-1.5 py-0.5 rounded">{tenant.hostname}</code>
            } />
          )}
          {tenant.provisioningFailureReason && (
            <InfoRow label="Failure Reason" value={
              <span className="text-xs text-red-600">{tenant.provisioningFailureReason}</span>
            } />
          )}
          {tenant.lastProvisioningAttemptUtc && (
            <InfoRow label="Last Attempt" value={formatDate(tenant.lastProvisioningAttemptUtc)} />
          )}
          {(tenant.provisioningStatus === 'Failed' || tenant.provisioningStatus === 'Pending') && (
            <div className="px-5 py-3">
              <RetryProvisioningButton tenantId={tenant.id} />
            </div>
          )}
        </dl>
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
    Funder:     'Funder',
    LienOwner:  'Lien Owner',
    Corporate:  'Corporate',
    Government: 'Government',
    Other:      'Other',
  };
  return labels[type] ?? type;
}

