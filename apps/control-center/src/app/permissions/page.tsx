import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { PermissionCatalogTable } from '@/components/users/permission-catalog-table';

/**
 * /permissions — Platform permission catalog.
 * Access: PlatformAdmin only.
 */
export default async function PermissionsPage() {
  const session = await requirePlatformAdmin();

  let permissions = null;
  let fetchError: string | null = null;

  try {
    permissions = await controlCenterServerApi.permissions.list();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load permissions.';
  }

  const products = permissions
    ? [...new Map(permissions.map(p => [p.productId, p.productName])).entries()]
    : [];

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
            configuration and cannot be modified from the Control Center. To add or change
            permissions, update the relevant product&apos;s permission manifest.
          </p>
        </div>

        {/* Product summary chips */}
        {products.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {products.map(([productId, productName]) => {
              const count = permissions!.filter(p => p.productId === productId).length;
              return (
                <span
                  key={productId}
                  className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700 border border-indigo-100"
                >
                  {productName}
                  <span className="bg-indigo-100 text-indigo-600 px-1.5 py-0.5 rounded-full text-[10px] font-bold">
                    {count}
                  </span>
                </span>
              );
            })}
          </div>
        )}

        {/* Summary */}
        {permissions && !fetchError && (
          <p className="text-xs text-gray-400">
            {permissions.length} permission{permissions.length !== 1 ? 's' : ''} total
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
