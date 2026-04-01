import { requirePlatformAdmin } from '@/lib/auth-guards';
import { getTenantContext } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { GroupListTable } from '@/components/users/group-list-table';

interface GroupsPageProps {
  searchParams: {
    page?:     string;
    tenantId?: string;
  };
}

/**
 * /groups — Platform-wide tenant group list.
 * Access: PlatformAdmin only.
 */
export default async function GroupsPage({ searchParams }: GroupsPageProps) {
  const session   = await requirePlatformAdmin();
  const tenantCtx = getTenantContext();

  const page     = Math.max(1, parseInt(searchParams.page ?? '1') || 1);
  const tenantId = searchParams.tenantId ?? tenantCtx?.tenantId;

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await controlCenterServerApi.groups.list({
      tenantId,
      page,
      pageSize: 20,
    });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load groups.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Tenant Groups</h1>
              {tenantCtx && (
                <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[11px] font-semibold text-amber-700">
                  <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                  Scoped to {tenantCtx.tenantName}
                </span>
              )}
            </div>
            <p className="text-sm text-gray-500 mt-0.5">
              {tenantCtx
                ? `Groups within ${tenantCtx.tenantName}`
                : 'All groups across all tenants'}
            </p>
          </div>
        </div>

        {/* Summary */}
        {result && !fetchError && (
          <p className="text-xs text-gray-400">
            {result.totalCount} group{result.totalCount !== 1 ? 's' : ''} found
          </p>
        )}

        {/* Error */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Table */}
        {result && (
          <GroupListTable
            groups={result.items}
            totalCount={result.totalCount}
            page={result.page}
            pageSize={result.pageSize}
          />
        )}
      </div>
    </CCShell>
  );
}
