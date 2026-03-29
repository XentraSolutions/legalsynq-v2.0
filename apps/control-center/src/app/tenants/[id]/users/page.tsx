import Link from 'next/link';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { Routes } from '@/lib/routes';
import { CCShell } from '@/components/shell/cc-shell';
import { UserListTable } from '@/components/users/user-list-table';

interface TenantUsersPageProps {
  params:       { id: string };
  searchParams: { page?: string; search?: string };
}

/**
 * /tenants/[id]/users — Users scoped to a single tenant.
 *
 * Access: PlatformAdmin only.
 *
 * Data:
 *   - Tenant name fetched via controlCenterServerApi.tenants.getById(id)
 *   - Users fetched via controlCenterServerApi.users.list({ tenantId: id })
 *
 * TODO: When live endpoints are ready, both stubs auto-wire in control-center-api.ts.
 *       No page changes needed.
 */
export default async function TenantScopedUsersPage({ params, searchParams }: TenantUsersPageProps) {
  const session  = await requirePlatformAdmin();
  const { id }   = params;
  const page     = Math.max(1, parseInt(searchParams.page ?? '1') || 1);
  const search   = searchParams.search ?? '';

  // Fetch tenant name and users in parallel
  const [tenant, usersResult] = await Promise.allSettled([
    controlCenterServerApi.tenants.getById(id),
    controlCenterServerApi.users.list({ tenantId: id, page, pageSize: 20, search }),
  ]);

  const tenantData   = tenant.status   === 'fulfilled' ? tenant.value   : null;
  const usersData    = usersResult.status === 'fulfilled' ? usersResult.value : null;
  const usersError   = usersResult.status === 'rejected'
    ? String((usersResult.reason as Error)?.message ?? 'Failed to load users.')
    : null;

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Breadcrumb */}
        <nav className="flex items-center gap-1.5 text-sm text-gray-500">
          <Link href={Routes.tenants} className="hover:text-gray-900 transition-colors">
            Tenants
          </Link>
          <span className="text-gray-300">›</span>
          {tenantData ? (
            <Link
              href={Routes.tenantDetail(id)}
              className="hover:text-gray-900 transition-colors"
            >
              {tenantData.displayName}
            </Link>
          ) : (
            <span className="text-gray-400">Tenant</span>
          )}
          <span className="text-gray-300">›</span>
          <span className="text-gray-900 font-medium">Users</span>
        </nav>

        {/* Sub-navigation tabs */}
        <div className="flex items-center gap-0 border-b border-gray-200 -mb-1">
          <SubNavLink href={Routes.tenantDetail(id)}    label="Overview" active={false} />
          <SubNavLink href={Routes.tenantUsers_(id)}    label="Users"    active />
        </div>

        {/* Header */}
        <div className="flex items-center justify-between pt-2">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">
              Users{tenantData ? ` — ${tenantData.displayName}` : ''}
            </h1>
            {tenantData && (
              <p className="text-sm text-gray-500 mt-0.5">
                <span className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-600">
                  {tenantData.code}
                </span>
              </p>
            )}
          </div>
          <button
            type="button"
            disabled
            className="bg-indigo-600 text-white text-sm font-medium px-4 py-2 rounded-md opacity-50 cursor-not-allowed"
            title="Coming soon"
          >
            Invite User
          </button>
        </div>

        {/* Tenant not found */}
        {!tenantData && (
          <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-700">
            Tenant not found — showing users by tenant ID only.
          </div>
        )}

        {/* Search */}
        <form method="GET" className="flex items-center gap-2">
          <input
            type="text"
            name="search"
            defaultValue={search}
            placeholder="Search by name, email or role…"
            className="w-full sm:w-80 text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          />
          <button
            type="submit"
            className="text-sm px-3 py-1.5 rounded-md border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition-colors"
          >
            Search
          </button>
          {search && (
            <a href="?" className="text-xs text-gray-400 hover:text-gray-700 underline">
              Clear
            </a>
          )}
        </form>

        {/* Summary */}
        {usersData && !usersError && (
          <p className="text-xs text-gray-400">
            {usersData.totalCount} user{usersData.totalCount !== 1 ? 's' : ''}
            {search && ` matching "${search}"`}
          </p>
        )}

        {/* Error banner */}
        {usersError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {usersError}
          </div>
        )}

        {/* Table — tenant column hidden since we're already scoped */}
        {usersData && (
          <UserListTable
            users={usersData.items}
            totalCount={usersData.totalCount}
            page={usersData.page}
            pageSize={usersData.pageSize}
            showTenantColumn={false}
          />
        )}
      </div>
    </CCShell>
  );
}

// ── Local sub-nav component ───────────────────────────────────────────────────

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
