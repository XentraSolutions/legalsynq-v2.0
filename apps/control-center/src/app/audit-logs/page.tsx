import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { AuditLogTable } from '@/components/audit-logs/audit-log-table';
import type { AuditLogEntry } from '@/types/control-center';

interface AuditLogsPageProps {
  searchParams: {
    search?:     string;
    entityType?: string;
    actor?:      string;
    page?:       string;
  };
}

const PAGE_SIZE = 15;

/**
 * /audit-logs — System-wide audit log viewer.
 *
 * Access: PlatformAdmin only.
 * Read-only — no mutations on this page.
 *
 * Filtering is handled server-side via URL searchParams (plain GET form).
 * Data: served from mock stub in controlCenterServerApi.audit.list().
 * TODO: When GET /identity/api/admin/audit is live, the stub auto-wires —
 *       no page change needed, only the API method in control-center-api.ts.
 */
export default async function AuditLogsPage({ searchParams }: AuditLogsPageProps) {
  const session = await requirePlatformAdmin();

  const search     = searchParams.search     ?? '';
  const entityType = searchParams.entityType ?? '';
  const actor      = searchParams.actor      ?? '';
  const page       = Math.max(1, parseInt(searchParams.page ?? '1', 10));

  let result: { items: AuditLogEntry[]; totalCount: number } | null = null;
  let fetchError: string | null = null;

  try {
    result = await controlCenterServerApi.audit.list({
      page,
      pageSize:   PAGE_SIZE,
      search:     search     || undefined,
      entityType: entityType || undefined,
      actor:      actor      || undefined,
    });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load audit logs.';
  }

  const totalCount  = result?.totalCount ?? 0;
  const totalPages  = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const hasFilters  = !!(search || entityType || actor);
  const startItem   = totalCount === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const endItem     = Math.min(page * PAGE_SIZE, totalCount);

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Page header */}
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Audit Logs</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            System-wide activity log — all platform and tenant events
          </p>
        </div>

        {/* Filter bar (native GET form — no JS required) */}
        <form method="GET" action="/audit-logs" className="flex flex-wrap items-end gap-3">

          {/* Full-text search */}
          <div className="flex-1 min-w-48">
            <label htmlFor="search" className="block text-xs font-medium text-gray-600 mb-1">
              Search
            </label>
            <input
              id="search"
              name="search"
              type="search"
              defaultValue={search}
              placeholder="Action, entity, actor…"
              className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
            />
          </div>

          {/* Entity type filter */}
          <div className="w-44">
            <label htmlFor="entityType" className="block text-xs font-medium text-gray-600 mb-1">
              Entity Type
            </label>
            <select
              id="entityType"
              name="entityType"
              defaultValue={entityType}
              className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 bg-white"
            >
              <option value="">All types</option>
              <option value="User">User</option>
              <option value="Tenant">Tenant</option>
              <option value="Entitlement">Entitlement</option>
              <option value="Role">Role</option>
              <option value="System">System</option>
            </select>
          </div>

          {/* Actor filter */}
          <div className="w-52">
            <label htmlFor="actor" className="block text-xs font-medium text-gray-600 mb-1">
              Actor
            </label>
            <input
              id="actor"
              name="actor"
              type="search"
              defaultValue={actor}
              placeholder="Email or service name…"
              className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
            />
          </div>

          <div className="flex items-center gap-2">
            <button
              type="submit"
              className="px-4 py-1.5 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-1"
            >
              Filter
            </button>
            {hasFilters && (
              <a
                href="/audit-logs"
                className="px-3 py-1.5 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-300 hover:border-gray-400 rounded-md transition-colors"
              >
                Clear
              </a>
            )}
          </div>
        </form>

        {/* Active filter summary */}
        {hasFilters && !fetchError && (
          <div className="flex items-center gap-2 text-sm text-gray-500">
            <span>Showing results for</span>
            {search     && <FilterChip label={`"${search}"`} />}
            {entityType && <FilterChip label={`type: ${entityType}`} />}
            {actor      && <FilterChip label={`actor: ${actor}`} />}
          </div>
        )}

        {/* Error state */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Result count + table */}
        {result && (
          <>
            <div className="flex items-center justify-between text-xs text-gray-400">
              <span>
                {totalCount === 0
                  ? 'No entries found'
                  : `Showing ${startItem}–${endItem} of ${totalCount} event${totalCount !== 1 ? 's' : ''}`}
              </span>
              {totalPages > 1 && (
                <span>Page {page} of {totalPages}</span>
              )}
            </div>

            <AuditLogTable entries={result.items} />

            {/* Pagination */}
            {totalPages > 1 && (
              <Pagination
                page={page}
                totalPages={totalPages}
                search={search}
                entityType={entityType}
                actor={actor}
              />
            )}
          </>
        )}

      </div>
    </CCShell>
  );
}

// ── Local helpers ─────────────────────────────────────────────────────────────

function FilterChip({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700 border border-indigo-200">
      {label}
    </span>
  );
}

interface PaginationProps {
  page:        number;
  totalPages:  number;
  search:      string;
  entityType:  string;
  actor:       string;
}

function Pagination({ page, totalPages, search, entityType, actor }: PaginationProps) {
  function buildHref(p: number) {
    const params = new URLSearchParams();
    if (search)     params.set('search',     search);
    if (entityType) params.set('entityType', entityType);
    if (actor)      params.set('actor',      actor);
    if (p > 1)      params.set('page',       String(p));
    const qs = params.toString();
    return `/audit-logs${qs ? `?${qs}` : ''}`;
  }

  const pages: (number | '…')[] = buildPageRange(page, totalPages);

  return (
    <nav className="flex items-center justify-center gap-1" aria-label="Pagination">
      <PagerLink href={buildHref(page - 1)} disabled={page <= 1} label="← Prev" />
      {pages.map((p, i) =>
        p === '…' ? (
          <span key={`ellipsis-${i}`} className="px-2 py-1 text-xs text-gray-400">…</span>
        ) : (
          <PagerLink key={p} href={buildHref(p)} active={p === page} label={String(p)} />
        ),
      )}
      <PagerLink href={buildHref(page + 1)} disabled={page >= totalPages} label="Next →" />
    </nav>
  );
}

function PagerLink({
  href,
  label,
  active   = false,
  disabled = false,
}: {
  href:      string;
  label:     string;
  active?:   boolean;
  disabled?: boolean;
}) {
  if (disabled) {
    return (
      <span className="px-3 py-1.5 text-xs rounded-md text-gray-300 cursor-not-allowed">
        {label}
      </span>
    );
  }
  return (
    <a
      href={href}
      className={[
        'px-3 py-1.5 text-xs rounded-md font-medium transition-colors',
        active
          ? 'bg-indigo-600 text-white'
          : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
      ].join(' ')}
    >
      {label}
    </a>
  );
}

function buildPageRange(current: number, total: number): (number | '…')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  const pages: (number | '…')[] = [1];
  if (current > 3)         pages.push('…');
  for (let p = Math.max(2, current - 1); p <= Math.min(total - 1, current + 1); p++) {
    pages.push(p);
  }
  if (current < total - 2) pages.push('…');
  pages.push(total);
  return pages;
}
