import type { ReactNode } from 'react';
import Link from 'next/link';
import type { UserDetail, UserStatus } from '@/types/control-center';
import { Routes } from '@/lib/routes';

interface UserDetailCardProps {
  user: UserDetail;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'long',
    day:   'numeric',
    year:  'numeric',
  });
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    month:  'short',
    day:    'numeric',
    year:   'numeric',
    hour:   '2-digit',
    minute: '2-digit',
  });
}

/**
 * User detail card — sections: User Information, Account Status, Activity placeholder.
 * Pure Server Component — receives a fully-resolved UserDetail prop.
 */
export function UserDetailCard({ user }: UserDetailCardProps) {
  const isLocked  = user.isLocked ?? false;
  const isInvited = user.status === 'Invited';

  return (
    <div className="space-y-5">

      {/* ── User Information ──────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            User Information
          </h2>
        </div>
        <dl className="divide-y divide-gray-100">
          <InfoRow label="First Name"   value={user.firstName} />
          <InfoRow label="Last Name"    value={user.lastName} />
          <InfoRow label="Email"        value={
            <a href={`mailto:${user.email}`} className="text-indigo-600 hover:underline">
              {user.email}
            </a>
          } />
          <InfoRow label="Role"         value={<RolePill role={user.role} />} />
          <InfoRow label="Status"       value={<StatusPill status={user.status} />} />
          <InfoRow label="Tenant"       value={
            <Link
              href={Routes.tenantDetail(user.tenantId)}
              className="text-indigo-600 hover:underline inline-flex items-center gap-1.5"
            >
              {user.tenantDisplayName}
              <span className="font-mono text-[10px] bg-gray-100 px-1 py-0.5 rounded text-gray-500">
                {user.tenantCode}
              </span>
            </Link>
          } />
          <InfoRow label="Created"      value={formatDate(user.createdAtUtc)} />
          <InfoRow label="Last Updated" value={formatDate(user.updatedAtUtc)} />
          <InfoRow label="Last Login"   value={
            user.lastLoginAtUtc
              ? formatDateTime(user.lastLoginAtUtc)
              : <span className="text-gray-400 italic">Never</span>
          } />
        </dl>
      </div>

      {/* ── Account Status ────────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Account Status
          </h2>
        </div>
        <dl className="divide-y divide-gray-100">
          <InfoRow label="Account State" value={<StatusPill status={user.status} />} />
          <InfoRow
            label="Locked"
            value={
              isLocked
                ? <LockedIndicator locked />
                : <LockedIndicator locked={false} />
            }
          />
          <InfoRow
            label="Invite State"
            value={
              isInvited
                ? <span className="text-sm text-blue-700 font-medium">Pending acceptance</span>
                : <span className="text-sm text-gray-400 italic">—</span>
            }
          />
          {user.inviteSentAtUtc && (
            <InfoRow label="Invite Sent" value={formatDateTime(user.inviteSentAtUtc)} />
          )}
          <InfoRow
            label="Last Login"
            value={
              user.lastLoginAtUtc
                ? formatDateTime(user.lastLoginAtUtc)
                : <span className="text-gray-400 italic">Never</span>
            }
          />
        </dl>
      </div>

      {/* ── Org Memberships ───────────────────────────────────────────── */}
      {user.memberships !== undefined && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
            <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
              Organization Memberships
            </h2>
            <span className="text-xs text-gray-400">
              {user.memberships.length} org{user.memberships.length !== 1 ? 's' : ''}
            </span>
          </div>
          {user.memberships.length === 0 ? (
            <div className="px-5 py-8 text-center">
              <p className="text-sm text-gray-400 italic">Not a member of any organization.</p>
            </div>
          ) : (
            <div className="divide-y divide-gray-100">
              {user.memberships.map(m => (
                <div key={m.membershipId} className="px-5 py-3 flex items-center justify-between gap-4">
                  <div>
                    <p className="text-sm font-medium text-gray-900">{m.orgName}</p>
                    <p className="text-xs text-gray-400 mt-0.5">
                      Joined {formatDate(m.joinedAtUtc)}
                    </p>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-50 text-gray-700 border-gray-200">
                      {m.memberRole}
                    </span>
                    {m.isPrimary && (
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-amber-50 text-amber-700 border-amber-200">
                        Primary
                      </span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ── Group Memberships ──────────────────────────────────────────── */}
      {user.groups !== undefined && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
            <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
              Groups
            </h2>
            <span className="text-xs text-gray-400">
              {user.groups.length} group{user.groups.length !== 1 ? 's' : ''}
            </span>
          </div>
          {user.groups.length === 0 ? (
            <div className="px-5 py-8 text-center">
              <p className="text-sm text-gray-400 italic">Not a member of any group.</p>
            </div>
          ) : (
            <div className="divide-y divide-gray-100">
              {user.groups.map(g => (
                <div key={g.groupId} className="px-5 py-3 flex items-center justify-between gap-4">
                  <p className="text-sm font-medium text-gray-900">{g.groupName}</p>
                  <p className="text-xs text-gray-400">Joined {formatDate(g.joinedAtUtc)}</p>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ── Role Assignments ───────────────────────────────────────────── */}
      {user.roles !== undefined && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
            <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
              Role Assignments
            </h2>
            <span className="text-xs text-gray-400">
              {user.roles.length} role{user.roles.length !== 1 ? 's' : ''}
            </span>
          </div>
          {user.roles.length === 0 ? (
            <div className="px-5 py-8 text-center">
              <p className="text-sm text-gray-400 italic">No explicit roles assigned.</p>
            </div>
          ) : (
            <div className="px-5 py-3 flex flex-wrap gap-2">
              {user.roles.map(r => (
                <span
                  key={r.assignmentId}
                  className="inline-flex items-center px-2.5 py-1 rounded text-[12px] font-semibold border bg-indigo-50 text-indigo-700 border-indigo-100"
                >
                  {r.roleName}
                </span>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ── Recent Activity (placeholder) ─────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Recent Activity
          </h2>
        </div>
        <div className="px-5 py-10 text-center space-y-2">
          <p className="text-sm text-gray-500 font-medium">Activity log coming soon</p>
          <p className="text-xs text-gray-400 max-w-sm mx-auto">
            Login history and audit events will appear here once the audit log
            backend endpoint is connected.
          </p>
        </div>
      </div>

    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────────

function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="px-5 py-3 flex items-center gap-4">
      <dt className="w-36 shrink-0 text-xs font-medium text-gray-500">{label}</dt>
      <dd className="text-sm text-gray-800">{value}</dd>
    </div>
  );
}

function StatusPill({ status }: { status: UserStatus }) {
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

function RolePill({ role }: { role: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-indigo-50 text-indigo-700 border-indigo-200">
      {role}
    </span>
  );
}

function LockedIndicator({ locked }: { locked: boolean }) {
  return locked
    ? <span className="inline-flex items-center gap-1.5 text-sm text-red-700 font-medium">
        <span className="w-1.5 h-1.5 rounded-full bg-red-500 inline-block" />
        Locked
      </span>
    : <span className="inline-flex items-center gap-1.5 text-sm text-green-700 font-medium">
        <span className="w-1.5 h-1.5 rounded-full bg-green-500 inline-block" />
        Unlocked
      </span>;
}
