import { requirePlatformAdmin }       from '@/lib/auth-guards';
import { controlCenterServerApi }     from '@/lib/control-center-api';
import { UserListTable }              from '@/components/users/user-list-table';

interface TenantUsersPageProps {
  params:       { id: string };
  searchParams: Promise<{ page?: string; search?: string }>;
}

/**
 * /tenants/[id]/users — Tenant-scoped user list (Users tab body).
 *
 * The shared header, breadcrumb, and sub-nav tabs are rendered by layout.tsx.
 * This page returns only the users list content.
 *
 * Access: PlatformAdmin only (enforced by layout + requirePlatformAdmin below).
 */
export default async function TenantScopedUsersPage({
  params,
  searchParams,
}: TenantUsersPageProps) {
  const searchParamsData = await searchParams;
  await requirePlatformAdmin();

  const { id }   = params;
  const page     = Math.max(1, parseInt(searchParamsData.page ?? '1') || 1);
  const search   = searchParamsData.search ?? '';

  let usersData    = null;
  let usersError: string | null = null;

  try {
    usersData = await controlCenterServerApi.users.list({
      tenantId: id,
      page,
      pageSize: 20,
      search,
    });
  } catch (err) {
    usersError = err instanceof Error ? err.message : 'Failed to load users.';
  }

  return (
    <div className="space-y-4">

      {/* Controls row: search + invite */}
      <div className="flex items-center justify-between gap-4 flex-wrap">
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

        <button
          type="button"
          disabled
          className="bg-indigo-600 text-white text-sm font-medium px-4 py-2 rounded-md opacity-50 cursor-not-allowed"
          title="Coming soon"
        >
          Invite User
        </button>
      </div>

      {/* Result summary */}
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

      {/* User table — tenant column hidden since we're already scoped */}
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
  );
}
