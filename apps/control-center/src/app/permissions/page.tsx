import { requireAdmin }                from '@/lib/auth-guards';
import { controlCenterServerApi }     from '@/lib/control-center-api';
import { CCShell }                    from '@/components/shell/cc-shell';
import { PermissionCatalogTable }     from '@/components/users/permission-catalog-table';
import { PermissionSearchBar }        from '@/components/users/permission-search-bar';

interface PermissionsPageProps {
  searchParams?: { search?: string; product?: string };
}

/**
 * /permissions — Platform permission catalog.
 * Access: PlatformAdmin only.
 *
 * UIX-005: Added search/filter by text and product via URL query params.
 *   ?search=referral   → filters by code/name/description substring
 *   ?product=<uuid>    → filters by product
 */
export default async function PermissionsPage({ searchParams }: PermissionsPageProps) {
  const session    = await requireAdmin();
  const search     = searchParams?.search ?? '';
  const productId  = searchParams?.product ?? '';

  let permissions = null;
  let fetchError: string | null = null;

  try {
    permissions = await controlCenterServerApi.permissions.list({
      search:    search    || undefined,
      productId: productId || undefined,
    });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load permissions.';
  }

  // Build the product chip list from a full unfiltered list
  let allProducts: Array<{ id: string; name: string }> = [];
  try {
    const all = await controlCenterServerApi.permissions.list();
    allProducts = [...new Map(all.map(p => [p.productId, p.productName])).entries()]
      .map(([id, name]) => ({ id, name }));
  } catch {
    // Non-fatal — chips won't render
  }

  const activeProduct = allProducts.find(p => p.id === productId);

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Permission Catalog</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              All platform permissions across products
            </p>
          </div>
        </div>

        {/* Read-only notice */}
        <div className="flex items-start gap-3 bg-blue-50 border border-blue-200 rounded-lg px-4 py-3">
          <svg
            className="h-4 w-4 text-blue-500 mt-0.5 flex-shrink-0"
            viewBox="0 0 20 20"
            fill="currentColor"
            aria-hidden="true"
          >
            <path
              fillRule="evenodd"
              d="M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Zm-7-4a1 1 0 1 1-2 0 1 1 0 0 1 2 0ZM9 9a.75.75 0 0 0 0 1.5h.253a.25.25 0 0 1 .244.304l-.459 2.066A1.75 1.75 0 0 0 10.747 15H11a.75.75 0 0 0 0-1.5h-.253a.25.25 0 0 1-.244-.304l.459-2.066A1.75 1.75 0 0 0 9.253 9H9Z"
              clipRule="evenodd"
            />
          </svg>
          <p className="text-sm text-blue-700">
            This catalog is <strong>read-only</strong>. Permissions are defined in product
            configuration and cannot be modified from the Control Center. To assign permissions
            to a role, visit the{' '}
            <a href="/roles" className="underline hover:text-blue-900">Roles page</a>.
          </p>
        </div>

        {/* Product filter chips */}
        {allProducts.length > 0 && (
          <div className="flex flex-wrap gap-2 items-center">
            <a
              href={`/permissions${search ? `?search=${encodeURIComponent(search)}` : ''}`}
              className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium border transition-colors ${
                !productId
                  ? 'bg-indigo-600 text-white border-indigo-600'
                  : 'bg-white text-gray-600 border-gray-300 hover:border-indigo-300 hover:bg-indigo-50'
              }`}
            >
              All
            </a>
            {allProducts.map(p => (
              <a
                key={p.id}
                href={`/permissions?product=${p.id}${search ? `&search=${encodeURIComponent(search)}` : ''}`}
                className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium border transition-colors ${
                  productId === p.id
                    ? 'bg-indigo-600 text-white border-indigo-600'
                    : 'bg-white text-gray-600 border-gray-300 hover:border-indigo-300 hover:bg-indigo-50'
                }`}
              >
                {p.name}
              </a>
            ))}
          </div>
        )}

        {/* Search bar — client component */}
        <PermissionSearchBar initialSearch={search} productId={productId} />

        {/* Active filter summary */}
        {(search || activeProduct) && (
          <div className="flex items-center gap-2 text-xs text-gray-500">
            <span>Filtering by</span>
            {activeProduct && (
              <span className="bg-indigo-50 text-indigo-700 border border-indigo-100 px-2 py-0.5 rounded">
                {activeProduct.name}
              </span>
            )}
            {search && (
              <span className="bg-gray-100 text-gray-700 border border-gray-200 px-2 py-0.5 rounded font-mono">
                &quot;{search}&quot;
              </span>
            )}
            <a
              href={`/permissions${productId ? `?product=${productId}` : ''}`}
              className="text-indigo-600 hover:underline ml-1"
            >
              Clear search
            </a>
          </div>
        )}

        {/* Summary */}
        {permissions && !fetchError && (
          <p className="text-xs text-gray-400">
            {permissions.length} permission{permissions.length !== 1 ? 's' : ''}
            {search || productId ? ' matching filters' : ' total'}
          </p>
        )}

        {/* Error */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Table */}
        {permissions && (
          <PermissionCatalogTable permissions={permissions} />
        )}
      </div>
    </CCShell>
  );
}
