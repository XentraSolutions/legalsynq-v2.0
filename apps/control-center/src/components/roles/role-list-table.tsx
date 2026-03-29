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

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Role</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Description</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Permissions</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Users</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {roles.map(role => (
              <tr key={role.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <Link
                    href={Routes.roleDetail(role.id)}
                    className="text-sm font-semibold text-gray-900 hover:text-indigo-700 hover:underline transition-colors"
                  >
                    {role.name}
                  </Link>
                </td>
                <td className="px-4 py-3 text-sm text-gray-600 max-w-xs">
                  {role.description}
                </td>
                <td className="px-4 py-3">
                  <PermissionCountBadge count={role.permissions.length} />
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
