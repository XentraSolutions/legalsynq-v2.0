import { requirePlatformAdmin } from '@/lib/auth-guards';
import { getTenantContext } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { SupportCaseTable } from '@/components/support/support-case-table';
import type { SupportCase } from '@/types/control-center';

interface SupportPageProps {
  searchParams: Promise<{
    page?:     string;
    search?:   string;
    status?:   string;
    priority?: string;
  }>;
}

const PAGE_SIZE = 10;

/**
 * /support — Support Tools list page.
 *
 * Access: PlatformAdmin only.
 * Data: served from mock stub in controlCenterServerApi.support.list().
 * TODO: When /identity/api/admin/support endpoints are live, the stub auto-wires —
 *       no page change needed, only the API methods in control-center-api.ts.
 *
 * Filtering: search (title/tenant/user), status, priority — all via URL params.
 */
export default async function SupportPage({ searchParams }: SupportPageProps) {
  const searchParamsData = await searchParams;
  const session   = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();

  const page     = Math.max(1, parseInt(searchParamsData.page ?? '1', 10) || 1);
  const search   = searchParamsData.search   ?? '';
  const status   = searchParamsData.status   ?? '';
  const priority = searchParamsData.priority ?? '';

  let result: { items: SupportCase[]; totalCount: number } | null = null;
  let fetchError: string | null = null;

  try {
    result = await controlCenterServerApi.support.list({
      page,
      pageSize: PAGE_SIZE,
      search,
      status,
      priority,
      tenantId: tenantCtx?.tenantId,
    });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load support cases.';
  }

  const openCount         = result?.items.filter(c => c.status === 'Open').length ?? 0;
  const investigatingCount = result?.items.filter(c => c.status === 'Investigating').length ?? 0;

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-5xl mx-auto px-6 py-8">

          {/* Page header */}
          <div className="mb-6 flex items-start justify-between gap-4">
            <div>
              <div className="flex items-center gap-3">
                <h1 className="text-xl font-semibold text-gray-900">Support Tools</h1>
                <span className="inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-700">
                  IN PROGRESS
                </span>
                {tenantCtx && (
                  <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[11px] font-semibold text-amber-700">
                    <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                    Scoped to {tenantCtx.tenantName}
                  </span>
                )}
              </div>
              <p className="text-sm text-gray-500 mt-1">
                {tenantCtx
                  ? `Cases for ${tenantCtx.tenantName} — track, investigate, and resolve.`
                  : 'Internal case management — track, investigate, and resolve tenant issues.'}
              </p>
            </div>

            {/* Quick-glance counts */}
            {result && (
              <div className="flex items-center gap-3 shrink-0">
                {openCount > 0 && (
                  <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-blue-100 text-blue-700 border border-blue-300">
                    {openCount} Open
                  </span>
                )}
                {investigatingCount > 0 && (
                  <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-700 border border-amber-300">
                    {investigatingCount} Investigating
                  </span>
                )}
              </div>
            )}
          </div>

          {/* Error state */}
          {fetchError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
              <p className="text-sm text-red-700 font-medium">Failed to load support cases</p>
              <p className="text-xs text-red-600 mt-1">{fetchError}</p>
            </div>
          ) : (
            <SupportCaseTable
              cases={result?.items ?? []}
              totalCount={result?.totalCount ?? 0}
              page={page}
              pageSize={PAGE_SIZE}
              search={search}
              status={status}
              priority={priority}
            />
          )}

        </div>
      </div>
    </CCShell>
  );
}
