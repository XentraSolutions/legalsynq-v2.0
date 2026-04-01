import { cookies }                from 'next/headers';
import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { TenantListTable }        from '@/components/control-center/tenant-list-table';
import { CCRoutes }               from '@/lib/control-center-routes';
import Link                       from 'next/link';

interface TenantsPageProps {
  searchParams: {
    page?:   string;
    search?: string;
  };
}

function readActiveTenantId(): string | null {
  const raw = cookies().get('cc_tenant_context')?.value;
  if (!raw) return null;
  try {
    const ctx = JSON.parse(raw) as { tenantId?: string };
    return ctx.tenantId ?? null;
  } catch {
    return null;
  }
}

/**
 * /control-center/tenants — Tenants list.
 *
 * Shows all tenants with their status, type, and primary contact.
 * Each row includes a "Set Active" / "Deactivate" button that sets the
 * cc_tenant_context cookie used by the Notifications section.
 */
export default async function TenantsPage({ searchParams }: TenantsPageProps) {
  await requireCCPlatformAdmin();

  const page           = Math.max(1, parseInt(searchParams.page ?? '1') || 1);
  const search         = searchParams.search ?? '';
  const activeTenantId = readActiveTenantId();

  let result     = null;
  let fetchError: string | null = null;

  try {
    result = await controlCenterServerApi.tenants.list({ page, pageSize: 20, search });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load tenants.';
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Tenants</h1>
          {activeTenantId && (
            <p className="text-xs text-amber-700 mt-0.5">
              A notification context is active.{' '}
              <Link href={CCRoutes.notifications} className="underline hover:text-amber-900">
                Manage in Notifications →
              </Link>
            </p>
          )}
        </div>
        <button
          type="button"
          disabled
          className="bg-indigo-600 text-white text-sm font-medium px-4 py-2 rounded-md opacity-50 cursor-not-allowed"
          title="Coming soon"
        >
          Create Tenant
        </button>
      </div>

      {/* Search bar */}
      <form method="GET" className="flex items-center gap-2">
        <input
          type="text"
          name="search"
          defaultValue={search}
          placeholder="Search by name, code or contact…"
          className="w-full sm:w-72 text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
        />
        <button
          type="submit"
          className="text-sm px-3 py-1.5 rounded-md border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition-colors"
        >
          Search
        </button>
        {search && (
          <a href="?" className="text-xs text-gray-400 hover:text-gray-700 underline">Clear</a>
        )}
      </form>

      {/* Error banner */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {/* Tenants table — activeTenantId prop enables the "Notif Context" column */}
      {result && (
        <TenantListTable
          tenants={result.items}
          totalCount={result.totalCount}
          page={result.page}
          pageSize={result.pageSize}
          activeTenantId={activeTenantId}
        />
      )}
    </div>
  );
}
