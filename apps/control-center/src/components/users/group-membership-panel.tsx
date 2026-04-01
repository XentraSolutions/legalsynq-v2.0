'use client';

/**
 * GroupMembershipPanel — UIX-003 / UIX-003-02
 *
 * Shows current group memberships for a user and allows
 * adding / removing via live BFF endpoints.
 *
 * BFF routes:
 *   POST   /api/identity/admin/groups/{groupId}/members            → add
 *   DELETE /api/identity/admin/groups/{groupId}/members/{userId}   → remove
 */

import { useState, useEffect }      from 'react';
import { useRouter }                from 'next/navigation';
import Link                         from 'next/link';
import type { UserGroupSummary, GroupSummary } from '@/types/control-center';
import { Routes }                   from '@/lib/routes';

function fmtDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch { return iso; }
}

interface GroupMembershipPanelProps {
  userId:          string;
  currentGroups:   UserGroupSummary[];
  availableGroups: GroupSummary[];
}

export function GroupMembershipPanel({
  userId,
  currentGroups,
  availableGroups,
}: GroupMembershipPanelProps) {
  const router = useRouter();

  const [addGroupId,  setAddGroupId]  = useState('');
  const [adding,      setAdding]      = useState(false);
  const [addError,    setAddError]    = useState<string | null>(null);
  const [addOk,       setAddOk]       = useState(false);

  const [removeConfirm, setRemoveConfirm] = useState<string | null>(null);
  const [removing,      setRemoving]      = useState<string | null>(null);
  const [removeError,   setRemoveError]   = useState<string | null>(null);

  /* Auto-dismiss success after 3 s */
  useEffect(() => {
    if (!addOk) return;
    const t = setTimeout(() => setAddOk(false), 3000);
    return () => clearTimeout(t);
  }, [addOk]);

  const memberGroupIds = new Set(currentGroups.map(g => g.groupId));
  const groupsToAdd    = availableGroups.filter(g => !memberGroupIds.has(g.id) && g.isActive);

  async function handleAdd() {
    if (!addGroupId) return;
    setAdding(true);
    setAddError(null);
    setAddOk(false);
    try {
      const res = await fetch(
        `/api/identity/admin/groups/${encodeURIComponent(addGroupId)}/members`,
        {
          method:  'POST',
          headers: { 'Content-Type': 'application/json' },
          body:    JSON.stringify({ userId }),
        },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Failed to add to group.');
      }
      setAddOk(true);
      setAddGroupId('');
      router.refresh();
    } catch (err) {
      setAddError(err instanceof Error ? err.message : 'An error occurred.');
    } finally {
      setAdding(false);
    }
  }

  async function handleRemove(groupId: string) {
    setRemoving(groupId);
    setRemoveError(null);
    try {
      const res = await fetch(
        `/api/identity/admin/groups/${encodeURIComponent(groupId)}/members/${encodeURIComponent(userId)}`,
        { method: 'DELETE' },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Failed to remove from group.');
      }
      router.refresh();
    } catch (err) {
      setRemoveError(err instanceof Error ? err.message : 'An error occurred.');
    } finally {
      setRemoving(null);
      setRemoveConfirm(null);
    }
  }

  const groupCount = currentGroups.length;

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      {/* Header */}
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Group Memberships
          </h2>
          <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold border bg-indigo-50 text-indigo-600 border-indigo-200">
            {groupCount} {groupCount === 1 ? 'group' : 'groups'}
          </span>
        </div>
        <span className="text-[11px] text-gray-400">Groups control resource-level permissions</span>
      </div>

      {/* Current groups */}
      {currentGroups.length > 0 ? (
        <ul className="divide-y divide-gray-100">
          {currentGroups.map(g => (
            <li key={g.groupId} className="flex items-center justify-between px-5 py-2.5 gap-3">
              <div className="flex items-center gap-3">
                <Link
                  href={Routes.groupDetail(g.groupId)}
                  className="text-sm font-medium text-indigo-600 hover:text-indigo-800 hover:underline"
                >
                  {g.groupName}
                </Link>
                <span className="text-[11px] text-gray-400">
                  Joined {fmtDate(g.joinedAtUtc)}
                </span>
              </div>

              {removeConfirm === g.groupId ? (
                <span className="inline-flex items-center gap-1 text-xs">
                  <span className="text-red-700 font-medium">Remove from group?</span>
                  <button
                    type="button"
                    disabled={removing === g.groupId}
                    onClick={() => handleRemove(g.groupId)}
                    className="px-2 py-0.5 rounded bg-red-600 text-white text-[11px] font-medium hover:bg-red-700 disabled:opacity-50 transition-colors"
                  >
                    {removing === g.groupId ? '…' : 'Yes, remove'}
                  </button>
                  <button
                    type="button"
                    onClick={() => setRemoveConfirm(null)}
                    className="px-2 py-0.5 rounded border border-gray-200 bg-white text-gray-500 text-[11px] hover:bg-gray-50 transition-colors"
                  >
                    Cancel
                  </button>
                </span>
              ) : (
                <button
                  type="button"
                  disabled={removing !== null}
                  onClick={() => setRemoveConfirm(g.groupId)}
                  className="text-xs px-2 py-1 rounded border border-red-200 bg-white text-red-600 hover:bg-red-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  Remove
                </button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <div className="px-5 py-6 text-center">
          <p className="text-sm font-medium text-gray-500">No group memberships</p>
          <p className="text-xs text-gray-400 mt-1">
            Groups control shared permissions within an organization. Add the user to a group below.
          </p>
        </div>
      )}

      {removeError && (
        <div className="mx-5 my-2 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
          {removeError}
        </div>
      )}

      {/* Add to group */}
      {groupsToAdd.length > 0 && (
        <div className="px-5 py-3 border-t border-gray-100 bg-gray-50 flex items-end gap-3 flex-wrap">
          <div className="flex-1 min-w-48">
            <label className="block text-xs font-medium text-gray-600 mb-1">Add to group</label>
            <select
              value={addGroupId}
              onChange={e => { setAddGroupId(e.target.value); setAddOk(false); setAddError(null); }}
              className="w-full h-8 rounded-md border border-gray-300 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white"
            >
              <option value="">Select group…</option>
              {groupsToAdd.map(g => (
                <option key={g.id} value={g.id}>{g.name}</option>
              ))}
            </select>
          </div>
          <button
            type="button"
            disabled={!addGroupId || adding}
            onClick={handleAdd}
            className="h-8 px-4 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {adding ? 'Adding…' : 'Add to Group'}
          </button>
          {addOk && (
            <span className="text-xs text-green-700 font-medium flex items-center gap-1">
              <span className="w-1.5 h-1.5 rounded-full bg-green-500 inline-block" />
              Added to group.
            </span>
          )}
        </div>
      )}

      {addError && (
        <div className="mx-5 mb-3 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
          {addError}
        </div>
      )}

      {groupsToAdd.length === 0 && currentGroups.length > 0 && (
        <p className="px-5 py-2 text-xs text-gray-400 italic border-t border-gray-100">
          User is already a member of all active groups in this tenant.
        </p>
      )}

      {groupsToAdd.length === 0 && currentGroups.length === 0 && (
        <p className="px-5 py-2 text-xs text-gray-400 italic border-t border-gray-100">
          No active groups are available in this tenant yet.
        </p>
      )}
    </div>
  );
}
