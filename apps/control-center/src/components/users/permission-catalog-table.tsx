import type { PermissionCatalogItem } from '@/types/control-center';

interface PermissionCatalogTableProps {
  permissions: PermissionCatalogItem[];
  productFilter?: string;
}

function ActiveBadge({ isActive }: { isActive: boolean }) {
  return isActive ? (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-green-50 text-green-700 border-green-200">
      Active
    </span>
  ) : (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-100 text-gray-500 border-gray-200">
      Inactive
    </span>
  );
}

export function PermissionCatalogTable({
  permissions,
  productFilter,
}: PermissionCatalogTableProps) {
  const filtered = productFilter
    ? permissions.filter(p => p.productId === productFilter)
    : permissions;

  if (filtered.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No permissions found.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Code</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Description</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Product</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {filtered.map(perm => (
              <tr key={perm.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <code className="text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-700 font-mono">
                    {perm.code}
                  </code>
                </td>
                <td className="px-4 py-3 text-sm font-medium text-gray-900">
                  {perm.name}
                </td>
                <td className="px-4 py-3 text-sm text-gray-500 max-w-xs">
                  {perm.description ?? <span className="text-gray-300 italic">—</span>}
                </td>
                <td className="px-4 py-3">
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-indigo-50 text-indigo-700 border-indigo-100">
                    {perm.productName}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <ActiveBadge isActive={perm.isActive} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="px-4 py-2 border-t border-gray-100">
        <p className="text-xs text-gray-400">{filtered.length} permission{filtered.length !== 1 ? 's' : ''}</p>
      </div>
    </div>
  );
}
