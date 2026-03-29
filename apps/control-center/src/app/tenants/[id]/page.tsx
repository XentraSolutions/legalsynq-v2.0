import Link from 'next/link';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { Routes } from '@/lib/routes';
import { CCShell } from '@/components/shell/cc-shell';
import { TenantDetailCard } from '@/components/tenants/tenant-detail-card';
import { TenantActions } from '@/components/tenants/tenant-actions';
import type { TenantStatus, TenantType } from '@/types/control-center';

interface TenantDetailPageProps {
  params: { id: string };
}

/**
 * /tenants/[id] — Tenant detail (Overview tab).
 *
 * Access: PlatformAdmin only (enforced by requirePlatformAdmin).
 *
 * Data: served from mock stub in controlCenterServerApi.tenants.getById(id).
 * TODO: When GET /identity/api/admin/tenants/{id} is live, the stub auto-wires —
 *       no page change needed, only the API method in control-center-api.ts.
 */
export default async function TenantDetailPage({ params }: TenantDetailPageProps) {
  const session = await requirePlatformAdmin();
  const { id }  = params;

  let tenant = null;
  let fetchError: string | null = null;

  try {
    tenant = await controlCenterServerApi.tenants.getById(id);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load tenant.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">

        {/* Breadcrumb */}
        <nav className="flex items-center gap-1.5 text-sm text-gray-500">
          <Link href={Routes.tenants} className="hover:text-gray-900 transition-colors">
            Tenants
          </Link>
          <span className="text-gray-300">›</span>
          <span className="text-gray-900 font-medium">
            {tenant ? tenant.displayName : id}
          </span>
        </nav>

        {/* Error state */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Not found state */}
        {!fetchError && !tenant && (
          <div className="bg-white border border-gray-200 rounded-lg p-10 text-center space-y-3">
            <p className="text-sm font-medium text-gray-700">Tenant not found</p>
            <p className="text-xs text-gray-400">
              No tenant with ID <code className="font-mono bg-gray-100 px-1 rounded">{id}</code> exists.
            </p>
            <Link href={Routes.tenants} className="text-xs text-indigo-600 hover:underline">
              ← Back to Tenants
            </Link>
          </div>
        )}

        {/* Detail content */}
        {tenant && (
          <>
            {/* Page header */}
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-1">
                <div className="flex items-center gap-3">
                  <h1 className="text-xl font-semibold text-gray-900">{tenant.displayName}</h1>
                  <StatusBadge status={tenant.status} />
                </div>
                <p className="text-sm text-gray-500">
                  <span className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-600">
                    {tenant.code}
                  </span>
                  <span className="ml-2">{formatType(tenant.type)}</span>
                </p>
              </div>

              {/* Action buttons */}
              <TenantActions tenantId={tenant.id} currentStatus={tenant.status} />
            </div>

            {/* Sub-navigation tabs */}
            <div className="flex items-center gap-0 border-b border-gray-200">
              <SubNavLink href={Routes.tenantDetail(id)}    label="Overview" active />
              <SubNavLink href={Routes.tenantUsers_(id)}    label="Users"    active={false} />
            </div>

            {/* Detail sections */}
            <TenantDetailCard tenant={tenant} />
          </>
        )}
      </div>
    </CCShell>
  );
}

// ── Local helpers ─────────────────────────────────────────────────────────────

function SubNavLink({ href, label, active }: { href: string; label: string; active: boolean }) {
  return (
    <Link
      href={href}
      className={[
        'px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors',
        active
          ? 'border-indigo-600 text-indigo-700'
          : 'border-transparent text-gray-600 hover:text-gray-900 hover:border-gray-300',
      ].join(' ')}
    >
      {label}
    </Link>
  );
}

function StatusBadge({ status }: { status: TenantStatus }) {
  const styles: Record<TenantStatus, string> = {
    Active:    'bg-green-50 text-green-700 border-green-200',
    Inactive:  'bg-gray-100 text-gray-500 border-gray-200',
    Suspended: 'bg-red-50 text-red-700 border-red-200',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status]}`}>
      {status}
    </span>
  );
}

function formatType(type: TenantType): string {
  const labels: Record<TenantType, string> = {
    LawFirm:    'Law Firm',
    Provider:   'Provider',
    Corporate:  'Corporate',
    Government: 'Government',
    Other:      'Other',
  };
  return labels[type] ?? type;
}
