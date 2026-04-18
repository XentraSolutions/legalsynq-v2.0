'use client';

import { useState, useMemo, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import type { TenantUser } from '@/types/tenant';
import { AddUserModal } from './AddUserModal';
import { EditUserModal } from './EditUserModal';
import { ConfirmDialog } from '@/components/lien/modal';
import { tenantClientApi, ApiError } from '@/lib/tenant-client-api';
import { useToast } from '@/lib/toast-context';

type StatusFilter = 'All' | 'Active' | 'Inactive';
type StatusAction = 'activate' | 'deactivate';

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

function RowActionsMenu({
  user,
  onView,
  onEdit,
  onActivate,
  onDeactivate,
}: {
  user: TenantUser;
  onView: () => void;
  onEdit: () => void;
  onActivate: () => void;
  onDeactivate: () => void;
}) {
  const [open, setOpen] = useState(false);
  function close() { setOpen(false); }

  return (
    <div className="relative inline-block">
      <button
        onClick={(e) => { e.stopPropagation(); setOpen(v => !v); }}
        aria-label="User actions"
        className="p-1.5 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
      >
        <i className="ri-more-2-fill text-base" />
      </button>

      {open && (
        <>
          <div
            className="fixed inset-0 z-10"
            onClick={(e) => { e.stopPropagation(); close(); }}
          />
          <div className="absolute right-0 z-20 mt-1 w-44 bg-white rounded-lg border border-gray-200 shadow-lg py-1 text-sm">
            <button
              onClick={(e) => { e.stopPropagation(); close(); onView(); }}
              className="w-full text-left px-3 py-2 hover:bg-gray-50 text-gray-700 flex items-center gap-2"
            >
              <i className="ri-user-line text-gray-400" />
              View Profile
            </button>
            <button
              onClick={(e) => { e.stopPropagation(); close(); onEdit(); }}
              className="w-full text-left px-3 py-2 hover:bg-gray-50 text-gray-700 flex items-center gap-2"
            >
              <i className="ri-edit-line text-gray-400" />
              Edit
            </button>
            <div className="border-t border-gray-100 my-1" />
            {user.isActive ? (
              <button
                onClick={(e) => { e.stopPropagation(); close(); onDeactivate(); }}
                className="w-full text-left px-3 py-2 hover:bg-red-50 text-red-600 flex items-center gap-2"
              >
                <i className="ri-user-unfollow-line" />
                Deactivate
              </button>
            ) : (
              <button
                onClick={(e) => { e.stopPropagation(); close(); onActivate(); }}
                className="w-full text-left px-3 py-2 hover:bg-green-50 text-green-700 flex items-center gap-2"
              >
                <i className="ri-user-follow-line" />
                Activate
              </button>
            )}
          </div>
        </>
      )}
    </div>
  );
}

export function AuthUserTable({ users, tenantId }: { users: TenantUser[]; tenantId: string }) {
  const router = useRouter();
  const { show: showToast } = useToast();

  const [search, setSearch] = useState('');
  const [statusFilter, setStatus] = useState<StatusFilter>('All');
  const [page, setPage] = useState(1);

  const [showAddUser,  setShowAddUser]  = useState(false);
  const [editUser,     setEditUser]     = useState<TenantUser | null>(null);
  const [confirmState, setConfirmState] = useState<{ user: TenantUser; action: StatusAction } | null>(null);
  const [actioning,    setActioning]    = useState(false);

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

  const handleUserEdited = useCallback(() => {
    setEditUser(null);
    router.refresh();
  }, [router]);

  async function handleStatusAction() {
    if (!confirmState) return;
    const { user: u, action } = confirmState;
    setActioning(true);
    try {
      if (action === 'activate') {
        await tenantClientApi.activateUser(u.id);
        showToast(`${displayName(u)} has been activated.`, 'success');
      } else {
        await tenantClientApi.deactivateUser(u.id);
        showToast(`${displayName(u)} has been deactivated.`, 'success');
      }
      setConfirmState(null);
      router.refresh();
    } catch (err) {
      let msg = 'Something went wrong. Please try again.';
      if (err instanceof ApiError) {
        if (err.isForbidden)  msg = 'You do not have permission to perform this action.';
        else if (err.isNotFound) msg = 'User not found.';
        else if (err.message) msg = err.message;
      }
      showToast(msg, 'error');
      setConfirmState(null);
    } finally {
      setActioning(false);
    }
  }

  const productCount = (u: TenantUser) => u.productRoles?.filter((r) => !r.includes(':')).length ?? 0;
  const roleCount    = (u: TenantUser) => u.roles?.length ?? 0;

  const confirmUser     = confirmState?.user;
  const confirmName     = confirmUser ? displayName(confirmUser) : '';
  const isDeactivating  = confirmState?.action === 'deactivate';

  return (
    <>
      <AddUserModal
        open={showAddUser}
        tenantId={tenantId}
        onClose={() => setShowAddUser(false)}
        onSuccess={handleUserCreated}
      />

      {editUser && (
        <EditUserModal
          open={!!editUser}
          user={editUser}
          onClose={() => setEditUser(null)}
          onSuccess={handleUserEdited}
        />
      )}

      <ConfirmDialog
        open={!!confirmState}
        onClose={() => { if (!actioning) setConfirmState(null); }}
        onConfirm={handleStatusAction}
        loading={actioning}
        title={isDeactivating ? `Deactivate ${confirmName}?` : `Activate ${confirmName}?`}
        description={
          isDeactivating
            ? 'They will immediately lose access to the platform.'
            : 'They will regain access based on their assigned role.'
        }
        confirmLabel={isDeactivating ? 'Deactivate' : 'Activate'}
        confirmVariant={isDeactivating ? 'danger' : 'primary'}
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
                    <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                      <RowActionsMenu
                        user={u}
                        onView={() => router.push(`/tenant/authorization/users/${u.id}`)}
                        onEdit={() => setEditUser(u)}
                        onActivate={() => setConfirmState({ user: u, action: 'activate' })}
                        onDeactivate={() => setConfirmState({ user: u, action: 'deactivate' })}
                      />
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
