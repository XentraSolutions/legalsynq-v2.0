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
