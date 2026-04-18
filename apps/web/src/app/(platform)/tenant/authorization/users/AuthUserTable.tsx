'use client';

import { useState, useMemo, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import type { TenantUser } from '@/types/tenant';
import { AddUserModal } from './AddUserModal';

type StatusFilter = 'All' | 'Active' | 'Inactive';

const PAGE_SIZE = 15;

function initials(firstName?: string | null, lastName?: string | null): string {
  const f = (firstName ?? '').trim();
  const l = (lastName ?? '').trim();
  if (!f && !l) return '?';
  if (!f) return l.charAt(0).toUpperCase();
  if (!l) return f.charAt(0).toUpperCase();
  return `${f.charAt(0)}${l.charAt(0)}`.toUpperCase();
}

function displayName(u: TenantUser): string {
  const f = (u.firstName ?? '').trim();
  const l = (u.lastName ?? '').trim();
  if (!f && !l) return (u.email ?? '').trim() || 'Unknown User';
  return [f, l].filter(Boolean).join(' ');
}

function StatusBadge({ isActive }: { isActive: boolean }) {
  const cls = isActive
    ? 'bg-green-50 text-green-700 border-green-200'
    : 'bg-gray-100 text-gray-500 border-gray-200';
  return (
    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border ${cls}`}>
      <span className={`w-1.5 h-1.5 rounded-full inline-block ${isActive ? 'bg-green-500' : 'bg-gray-400'}`} />
      {isActive ? 'Active' : 'Inactive'}
    </span>
  );
}

function CountBadge({ count, color = 'gray' }: { count: number; color?: string }) {
  const colors: Record<string, string> = {
    gray: 'bg-gray-100 text-gray-600',
    blue: 'bg-blue-50 text-blue-700',
    indigo: 'bg-indigo-50 text-indigo-700',
    purple: 'bg-purple-50 text-purple-700',
  };
  if (count === 0) return <span className="text-gray-300 text-xs">0</span>;
  return (
    <span className={`inline-flex items-center justify-center min-w-[20px] px-1.5 py-0.5 rounded text-[11px] font-semibold ${colors[color] ?? colors.gray}`}>
      {count}
    </span>
  );
}

export function AuthUserTable({ users, tenantId }: { users: TenantUser[]; tenantId: string }) {
  const router = useRouter();
  const [search, setSearch] = useState('');
  const [statusFilter, setStatus] = useState<StatusFilter>('All');
  const [page, setPage] = useState(1);
  const [showAddUser, setShowAddUser] = useState(false);

  const filtered = useMemo(() => {
    const q = search.toLowerCase().trim();
    return users.filter((u) => {
      const matchesStatus =
        statusFilter === 'All' ||
        (statusFilter === 'Active' && !!u.isActive) ||
        (statusFilter === 'Inactive' && !u.isActive);
      if (!matchesStatus) return false;
      if (!q) return true;
      const fullName = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim();
      return (
        (u.email ?? '').toLowerCase().includes(q) ||
        (u.firstName ?? '').toLowerCase().includes(q) ||
        (u.lastName ?? '').toLowerCase().includes(q) ||
        fullName.toLowerCase().includes(q)
      );
    });
  }, [users, search, statusFilter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const slice = filtered.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);

  function handleSearch(value: string) { setSearch(value); setPage(1); }
  function handleStatus(value: StatusFilter) { setStatus(value); setPage(1); }

  const handleUserCreated = useCallback(() => {
    setShowAddUser(false);
    router.refresh();
  }, [router]);

  const productCount = (u: TenantUser) => u.productRoles?.filter((r) => !r.includes(':')).length ?? 0;
  const roleCount = (u: TenantUser) => u.roles?.length ?? 0;

  return (
    <>
      <AddUserModal
        open={showAddUser}
        tenantId={tenantId}
        onClose={() => setShowAddUser(false)}
        onSuccess={handleUserCreated}
      />

      <div className="space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center gap-3">
          <div className="relative flex-1 max-w-sm">
            <span className="absolute inset-y-0 left-3 flex items-center text-gray-400 pointer-events-none">
              <i className="ri-search-line text-base" />
            </span>
            <input
              type="text"
              placeholder="Search by name or email..."
              value={search}
              onChange={(e) => handleSearch(e.target.value)}
              className="w-full rounded-md border border-gray-300 bg-white py-2 pl-9 pr-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary"
            />
          </div>

          <div className="flex items-center gap-1 rounded-md border border-gray-200 bg-gray-50 p-1">
            {(['All', 'Active', 'Inactive'] as StatusFilter[]).map((s) => (
              <button
                key={s}
                onClick={() => handleStatus(s)}
                className={`px-3 py-1 rounded text-xs font-medium transition-colors ${
                  statusFilter === s
                    ? 'bg-white text-gray-900 shadow-sm border border-gray-200'
                    : 'text-gray-500 hover:text-gray-900'
                }`}
              >
                {s}
              </button>
            ))}
          </div>

          <span className="text-xs text-gray-400 whitespace-nowrap">
            {filtered.length} {filtered.length === 1 ? 'user' : 'users'}
          </span>

          <button
            onClick={() => setShowAddUser(true)}
            className="ml-auto inline-flex items-center gap-1.5 px-3 py-2 rounded-lg bg-primary hover:bg-primary/90 text-white text-sm font-medium transition-colors whitespace-nowrap"
          >
            <i className="ri-user-add-line text-base" />
            Add User
          </button>
        </div>

        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
          {slice.length === 0 ? (
            <div className="px-6 py-14 text-center">
              <i className="ri-user-search-line text-3xl text-gray-300 mb-2 block" />
              <p className="text-sm text-gray-400">
                {search || statusFilter !== 'All'
                  ? 'No users match your current search or filters.'
                  : 'No users found for this tenant.'}
              </p>
            </div>
          ) : (
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead>
                <tr className="bg-gray-50 text-xs font-medium text-gray-500 uppercase tracking-wider">
                  <th className="px-4 py-3 text-left">User</th>
                  <th className="px-4 py-3 text-left">Email</th>
                  <th className="px-4 py-3 text-left">Status</th>
                  <th className="px-4 py-3 text-center">Products</th>
                  <th className="px-4 py-3 text-center hidden md:table-cell">Roles</th>
                  <th className="px-4 py-3 text-center hidden lg:table-cell">Groups</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {slice.map((u) => (
                  <tr
                    key={u.id}
                    onClick={() => router.push(`/tenant/authorization/users/${u.id}`)}
                    className="hover:bg-gray-50 transition-colors cursor-pointer"
                  >
                    <td className="px-4 py-3 whitespace-nowrap">
                      <div className="flex items-center gap-3">
                        <span className="inline-flex h-8 w-8 items-center justify-center rounded-full bg-indigo-100 text-indigo-700 text-xs font-semibold flex-shrink-0">
                          {initials(u.firstName, u.lastName)}
                        </span>
                        <span className="font-medium text-gray-900">
                          {displayName(u)}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-gray-600">{u.email || '—'}</td>
                    <td className="px-4 py-3 whitespace-nowrap"><StatusBadge isActive={!!u.isActive} /></td>
                    <td className="px-4 py-3 text-center"><CountBadge count={productCount(u)} color="blue" /></td>
                    <td className="px-4 py-3 text-center hidden md:table-cell"><CountBadge count={roleCount(u)} color="indigo" /></td>
                    <td className="px-4 py-3 text-center hidden lg:table-cell"><CountBadge count={0} color="purple" /></td>
                    <td className="px-4 py-3 text-right">
                      <button
                        onClick={(e) => { e.stopPropagation(); router.push(`/tenant/authorization/users/${u.id}`); }}
                        className="text-xs text-primary hover:text-primary/80 font-medium"
                      >
                        View
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {totalPages > 1 && (
          <div className="flex items-center justify-between text-sm">
            <span className="text-gray-500">Page {safePage} of {totalPages}</span>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={safePage === 1}
                className="px-3 py-1.5 rounded border border-gray-200 text-gray-600 text-xs font-medium hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                Previous
              </button>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={safePage === totalPages}
                className="px-3 py-1.5 rounded border border-gray-200 text-gray-600 text-xs font-medium hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                Next
              </button>
            </div>
          </div>
        )}
      </div>
    </>
  );
}
