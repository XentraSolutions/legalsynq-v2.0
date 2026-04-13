import Link from 'next/link';
import type { RoleSummary } from '@/types/control-center';
import { Routes } from '@/lib/routes';

interface RoleListTableProps {
  roles: RoleSummary[];
}

export function RoleListTable({ roles }: RoleListTableProps) {
  if (roles.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No roles defined.</p>
      </div>
    );
  }

  const systemRoles = roles.filter(r => r.isSystemRole);
  const productRoles = roles.filter(r => r.isProductRole && !r.isSystemRole);
  const otherRoles = roles.filter(r => !r.isSystemRole && !r.isProductRole);

  return (
    <div className="space-y-4">
      {systemRoles.length > 0 && (
        <RoleSection title="System Roles" subtitle="Platform-level administrative roles" roles={systemRoles} />
      )}
      {productRoles.length > 0 && (
        <RoleSection title="Product Roles" subtitle="Roles governed by product entitlements and org-type rules" roles={productRoles} />
      )}
      {otherRoles.length > 0 && (
        <RoleSection title="Other Roles" subtitle="Custom roles" roles={otherRoles} />
      )}
    </div>
  );
}

function RoleSection({ title, subtitle, roles }: { title: string; subtitle: string; roles: RoleSummary[] }) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-4 py-3 bg-gray-50 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500">{title}</h3>
          <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded border bg-gray-100 text-gray-500 border-gray-200">
            {roles.length}
          </span>
        </div>
        <p className="text-[11px] text-gray-400 mt-0.5">{subtitle}</p>
      </div>
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50/50">
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Role</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Description</th>
              {roles.some(r => r.isProductRole) && (
                <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Product</th>
              )}
              {roles.some(r => r.isProductRole) && (
                <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Allowed Org Types</th>
              )}
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Permissions</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Users</th>
              <th className="px-4 py-2.5" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {roles.map(role => (
              <tr key={role.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    <Link
                      href={Routes.roleDetail(role.id)}
                      className="text-sm font-semibold text-gray-900 hover:text-indigo-700 hover:underline transition-colors"
                    >
                      {role.name}
                    </Link>
                    {role.isProductRole && (
                      <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[9px] font-semibold border bg-purple-50 text-purple-600 border-purple-200">
                        PRODUCT
                      </span>
                    )}
                  </div>
                </td>
                <td className="px-4 py-3 text-sm text-gray-600 max-w-xs truncate">
                  {role.description}
                </td>
                {roles.some(r => r.isProductRole) && (
                  <td className="px-4 py-3 text-sm text-gray-600">
                    {role.productName ? (
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border bg-blue-50 text-blue-700 border-blue-200">
                        {role.productName}
                      </span>
                    ) : (
                      <span className="text-gray-300">—</span>
                    )}
                  </td>
                )}
                {roles.some(r => r.isProductRole) && (
                  <td className="px-4 py-3">
                    {role.allowedOrgTypes && role.allowedOrgTypes.length > 0 ? (
                      <div className="flex flex-wrap gap-1">
                        {role.allowedOrgTypes.map(t => (
                          <span key={t} className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-mono font-medium border bg-gray-50 text-gray-600 border-gray-200">
                            {t}
                          </span>
                        ))}
                      </div>
                    ) : (
                      <span className="text-gray-300 text-sm">—</span>
                    )}
                  </td>
                )}
                <td className="px-4 py-3">
                  <PermissionCountBadge count={role.capabilityCount || role.permissions.length} />
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">
                  {role.userCount > 0
                    ? `${role.userCount} user${role.userCount !== 1 ? 's' : ''}`
                    : <span className="text-gray-400">—</span>}
                </td>
                <td className="px-4 py-3 text-right">
                  <Link
                    href={Routes.roleDetail(role.id)}
                    className="text-xs text-indigo-600 font-medium hover:underline whitespace-nowrap"
                  >
                    View →
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function PermissionCountBadge({ count }: { count: number }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-indigo-50 text-indigo-700 border-indigo-200">
      {count} permission{count !== 1 ? 's' : ''}
    </span>
  );
}
