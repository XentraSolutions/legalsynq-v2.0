import Link from 'next/link';
import type { GroupSummary } from '@/types/control-center';
import { Routes } from '@/lib/routes';

interface GroupListTableProps {
  groups:      GroupSummary[];
  totalCount:  number;
  page:        number;
  pageSize:    number;
}

function formatDate(iso: string): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

function ActiveBadge({ isActive }: { isActive: boolean }) {
  return isActive ? (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-green-50 text-green-700 border-green-200">
      Active
    </span>
  ) : (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-100 text-gray-500 border-gray-200">
      Inactive
    </span>
  );
}

export function GroupListTable({
  groups,
  totalCount,
  page,
  pageSize,
}: GroupListTableProps) {
  if (groups.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No groups found.</p>
      </div>
    );
  }

  const pageStart = (page - 1) * pageSize + 1;
  const pageEnd   = Math.min(page * pageSize, totalCount);
  const hasPrev   = page > 1;
  const hasNext   = page * pageSize < totalCount;

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Description</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Members</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {groups.map(group => (
              <tr key={group.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <Link
                    href={Routes.groupDetail(group.id)}
                    className="text-sm font-medium text-gray-900 hover:text-indigo-700 hover:underline transition-colors"
                  >
                    {group.name}
                  </Link>
                  <p className="text-[11px] text-gray-400 font-mono mt-0.5">{group.tenantId}</p>
                </td>
                <td className="px-4 py-3 text-sm text-gray-500 max-w-xs truncate">
                  {group.description ?? <span className="text-gray-300 italic">—</span>}
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">
                  {group.memberCount}
                </td>
                <td className="px-4 py-3">
                  <ActiveBadge isActive={group.isActive} />
                </td>
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                  {formatDate(group.createdAtUtc)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Showing {pageStart}–{pageEnd} of {totalCount}
        </p>
        <div className="flex items-center gap-2">
          {hasPrev && (
            <Link href={`?page=${page - 1}`} className="text-xs text-indigo-600 hover:underline">
              ← Previous
            </Link>
          )}
          {hasNext && (
            <Link href={`?page=${page + 1}`} className="text-xs text-indigo-600 hover:underline">
              Next →
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
