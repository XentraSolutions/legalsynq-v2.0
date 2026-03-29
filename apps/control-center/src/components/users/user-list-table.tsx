import Link from 'next/link';
import type { UserSummary, UserStatus } from '@/types/control-center';

interface UserListTableProps {
  users:             UserSummary[];
  totalCount:        number;
  page:              number;
  pageSize:          number;
  showTenantColumn?: boolean;
  baseHref?:         string;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

function formatLoginDate(iso: string): string {
  const d = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffDays === 0) return 'Today';
  if (diffDays === 1) return 'Yesterday';
  if (diffDays < 7)  return `${diffDays}d ago`;
  return formatDate(iso);
}

function fullName(user: UserSummary): string {
  return `${user.firstName} ${user.lastName}`;
}

function StatusBadge({ status }: { status: UserStatus }) {
  const styles: Record<UserStatus, string> = {
    Active:   'bg-green-50 text-green-700 border-green-200',
    Inactive: 'bg-gray-100 text-gray-500 border-gray-200',
    Invited:  'bg-blue-50 text-blue-700 border-blue-200',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status]}`}>
      {status}
    </span>
  );
}

export function UserListTable({
  users,
  totalCount,
  page,
  pageSize,
  showTenantColumn = true,
  baseHref = '?',
}: UserListTableProps) {
  if (users.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No users found.</p>
      </div>
    );
  }

  const pageStart = (page - 1) * pageSize + 1;
  const pageEnd   = Math.min(page * pageSize, totalCount);
  const hasPrev   = page > 1;
  const hasNext   = page * pageSize < totalCount;

  function pageHref(p: number, search?: string): string {
    const base = baseHref.includes('?') ? baseHref : `${baseHref}?`;
    const sep  = base.endsWith('?') || base.endsWith('&') ? '' : '&';
    const searchPart = search ? `search=${encodeURIComponent(search)}&` : '';
    return `${base}${sep}${searchPart}page=${p}`;
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Email</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Role</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              {showTenantColumn && (
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Tenant</th>
              )}
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Last Login</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {users.map(user => (
              <tr key={user.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <p className="text-sm font-medium text-gray-900">{fullName(user)}</p>
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">
                  {user.email}
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">
                  {user.role}
                </td>
                <td className="px-4 py-3">
                  <StatusBadge status={user.status} />
                </td>
                {showTenantColumn && (
                  <td className="px-4 py-3">
                    <p className="text-sm text-gray-700">{user.tenantCode}</p>
                  </td>
                )}
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                  {user.lastLoginAtUtc ? formatLoginDate(user.lastLoginAtUtc) : '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination footer */}
      <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Showing {pageStart}–{pageEnd} of {totalCount}
        </p>
        <div className="flex items-center gap-2">
          {hasPrev && (
            <Link href={pageHref(page - 1)} className="text-xs text-indigo-600 hover:underline">
              ← Previous
            </Link>
          )}
          {hasNext && (
            <Link href={pageHref(page + 1)} className="text-xs text-indigo-600 hover:underline">
              Next →
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
