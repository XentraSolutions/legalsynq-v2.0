import Link from 'next/link';
import type { TenantSummary, TenantStatus, TenantType } from '@/types/control-center';
import { CCRouteBuilders } from '@/lib/control-center-routes';
import { ActivateTenantButton, ClearTenantContextButton } from '@/components/control-center/tenant-context-button';
import type { TenantContext } from '@/app/(control-center)/control-center/notifications/actions';

interface TenantListTableProps {
  tenants:         TenantSummary[];
  totalCount:      number;
  page:            number;
  pageSize:        number;
  activeTenantId?: string | null;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

function formatType(type: TenantType): string {
  switch (type) {
    case 'LawFirm':    return 'Law Firm';
    case 'Provider':   return 'Provider';
    case 'Corporate':  return 'Corporate';
    case 'Government': return 'Government';
    default:           return 'Other';
  }
}

function StatusBadge({ status }: { status: TenantStatus }) {
  const styles: Record<TenantStatus, string> = {
    Active:    'bg-green-50 text-green-700 border-green-200',
    Inactive:  'bg-gray-100 text-gray-500 border-gray-200',
    Suspended: 'bg-red-50 text-red-700 border-red-200',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status] ?? styles.Inactive}`}>
      {status}
    </span>
  );
}

export function TenantListTable({ tenants, totalCount, page, pageSize, activeTenantId }: TenantListTableProps) {
  if (tenants.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No tenants found.</p>
      </div>
    );
  }

  const showContextCol = activeTenantId !== undefined;

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Primary Contact</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
              {showContextCol && (
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Notif Context</th>
              )}
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {tenants.map(tenant => {
              const isActive = tenant.id === activeTenantId;
              const ctx: TenantContext = {
                tenantId:   tenant.id,
                tenantName: tenant.displayName,
                tenantCode: tenant.code,
              };
              return (
                <tr key={tenant.id} className={`transition-colors ${isActive ? 'bg-amber-50' : 'hover:bg-gray-50'}`}>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <div>
                        <p className="text-sm font-medium text-gray-900">{tenant.displayName}</p>
                        <p className="text-xs text-gray-400 mt-0.5">{tenant.code}</p>
                      </div>
                      {isActive && (
                        <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[10px] font-semibold text-amber-700 shrink-0">
                          <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                          Notif Active
                        </span>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-700">
                    {formatType(tenant.type)}
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={tenant.status} />
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-700">
                    {tenant.primaryContactName}
                  </td>
                  <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                    {formatDate(tenant.createdAtUtc)}
                  </td>
                  {showContextCol && (
                    <td className="px-4 py-3">
                      {isActive ? (
                        <ClearTenantContextButton label="Deactivate" />
                      ) : (
                        <ActivateTenantButton tenant={ctx} />
                      )}
                    </td>
                  )}
                  <td className="px-4 py-3 text-right">
                    <Link
                      href={CCRouteBuilders.tenantDetail(tenant.id)}
                      className="text-xs text-indigo-600 font-medium hover:underline whitespace-nowrap"
                    >
                      View →
                    </Link>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount}
        </p>
        <div className="flex items-center gap-2">
          {page > 1 && (
            <Link href={`?page=${page - 1}`} className="text-xs text-indigo-600 hover:underline">
              ← Previous
            </Link>
          )}
          {page * pageSize < totalCount && (
            <Link href={`?page=${page + 1}`} className="text-xs text-indigo-600 hover:underline">
              Next →
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
