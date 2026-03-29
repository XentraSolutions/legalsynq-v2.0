import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { UserListTable } from '@/components/users/user-list-table';

interface TenantUsersPageProps {
  searchParams: {
    page?:   string;
    search?: string;
  };
}

/**
 * /tenant-users — Platform-wide user list (all tenants).
 *
 * Access: PlatformAdmin only.
 *
 * Data: served from mock stub in controlCenterServerApi.users.list().
 * TODO: When GET /identity/api/admin/users is live, the stub auto-wires —
 *       no page change needed.
 */
export default async function TenantUsersPage({ searchParams }: TenantUsersPageProps) {
  const session = await requirePlatformAdmin();

  const page   = Math.max(1, parseInt(searchParams.page ?? '1') || 1);
  const search = searchParams.search ?? '';

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await controlCenterServerApi.users.list({ page, pageSize: 20, search });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load users.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Tenant Users</h1>
            <p className="text-sm text-gray-500 mt-0.5">All users across all tenants</p>
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

        {/* Summary chips */}
        {result && !fetchError && (
          <p className="text-xs text-gray-400">
            {result.totalCount} user{result.totalCount !== 1 ? 's' : ''} found
            {search && ` matching "${search}"`}
          </p>
        )}

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Table */}
        {result && (
          <UserListTable
            users={result.items}
            totalCount={result.totalCount}
            page={result.page}
            pageSize={result.pageSize}
            showTenantColumn
          />
        )}
      </div>
    </CCShell>
  );
}
