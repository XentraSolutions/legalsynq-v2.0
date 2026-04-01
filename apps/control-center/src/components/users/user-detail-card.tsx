import type { ReactNode } from 'react';
import Link               from 'next/link';
import type { UserDetail, UserStatus } from '@/types/control-center';
import { Routes }         from '@/lib/routes';

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
                  <Link
                    href={Routes.groupDetail(g.groupId)}
                    className="text-sm font-medium text-gray-900 hover:text-indigo-700 hover:underline transition-colors"
                  >
                    {g.groupName}
                  </Link>
                  <p className="text-xs text-gray-400 shrink-0">Joined {formatDate(g.joinedAtUtc)}</p>
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
            <div className="flex items-center gap-2">
              <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold border bg-amber-50 text-amber-700 border-amber-200">
                Read-only · Current MVP
              </span>
              <span className="text-xs text-gray-400">
                {user.roles.length} role{user.roles.length !== 1 ? 's' : ''}
              </span>
            </div>
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

      {/* ── Effective Access Summary ───────────────────────────────────── */}
      <EffectiveAccessSummary user={user} />

    </div>
  );
}

// ── Effective Access Summary panel ───────────────────────────────────────────

function EffectiveAccessSummary({ user }: { user: UserDetail }) {
  const primaryMembership = user.memberships?.find(m => m.isPrimary);
  const roleCount         = user.roles?.length ?? 0;
  const groupCount        = user.groups?.length ?? 0;
  const membershipCount   = user.memberships?.length ?? 0;
  const isActive          = user.status === 'Active';

  const accessTier = (() => {
    const roleNames = (user.roles ?? []).map(r => r.roleName.toLowerCase());
    if (roleNames.some(n => n.includes('platformadmin') || n === 'platform admin'))
      return { label: 'Platform Admin', description: 'Full platform management access across all tenants.', color: 'text-red-700 bg-red-50 border-red-200' };
    if (roleNames.some(n => n.includes('tenantadmin') || n === 'tenant admin'))
      return { label: 'Tenant Admin', description: 'Full management access within their tenant.', color: 'text-indigo-700 bg-indigo-50 border-indigo-200' };
    if (roleNames.length > 0)
      return { label: roleNames[0], description: 'Scoped access based on assigned role.', color: 'text-gray-700 bg-gray-50 border-gray-200' };
    return { label: 'No role assigned', description: 'No scoped access — user has no active role assignment.', color: 'text-amber-700 bg-amber-50 border-amber-200' };
  })();

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Effective Access Summary
        </h2>
        <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold border bg-gray-100 text-gray-500 border-gray-200">
          Read-only · Informational
        </span>
      </div>

      <div className="px-5 py-4 space-y-4">

        {/* Account state indicator */}
        <div className="flex items-center gap-3">
          <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded text-[12px] font-semibold border ${isActive ? 'bg-green-50 text-green-700 border-green-200' : 'bg-gray-100 text-gray-500 border-gray-200'}`}>
            <span className={`w-1.5 h-1.5 rounded-full inline-block ${isActive ? 'bg-green-500' : 'bg-gray-400'}`} />
            {isActive ? 'Account Active' : `Account ${user.status}`}
          </span>
          {!isActive && (
            <span className="text-xs text-gray-400 italic">
              Inactive accounts cannot access the platform.
            </span>
          )}
        </div>

        {/* Role / access tier */}
        <div className="space-y-1">
          <p className="text-xs font-medium text-gray-500">Access Tier</p>
          <div className="flex items-start gap-2">
            <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${accessTier.color}`}>
              {accessTier.label}
            </span>
            <p className="text-xs text-gray-500 pt-0.5">{accessTier.description}</p>
          </div>
        </div>

        {/* Summary stats */}
        <div className="grid grid-cols-3 gap-3">
          <SummaryStat label="Organizations" value={membershipCount} />
          <SummaryStat label="Groups" value={groupCount} />
          <SummaryStat label="Role Assignments" value={roleCount} />
        </div>

        {/* Primary org callout */}
        {primaryMembership && (
          <div className="text-xs text-gray-500 bg-gray-50 rounded-md px-3 py-2 border border-gray-100">
            Primary org:{' '}
            <span className="font-medium text-gray-800">{primaryMembership.orgName}</span>
            {' '}·{' '}
            <span className="text-gray-500">{primaryMembership.memberRole}</span>
          </div>
        )}

        {/* No primary org notice */}
        {!primaryMembership && membershipCount === 0 && (
          <p className="text-xs text-amber-600 bg-amber-50 rounded-md px-3 py-2 border border-amber-100">
            No organization membership. This user has no primary org assigned.
          </p>
        )}

      </div>
    </div>
  );
}

function SummaryStat({ label, value }: { label: string; value: number }) {
  return (
    <div className="bg-gray-50 border border-gray-100 rounded-md px-3 py-2 text-center">
      <p className="text-lg font-semibold text-gray-800">{value}</p>
      <p className="text-[11px] text-gray-500 mt-0.5">{label}</p>
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
