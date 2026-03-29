import Link from 'next/link';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { Routes } from '@/lib/routes';
import { CCShell } from '@/components/shell/cc-shell';
import { UserDetailCard } from '@/components/users/user-detail-card';
import { UserActions } from '@/components/users/user-actions';
import type { UserStatus } from '@/types/control-center';

interface UserDetailPageProps {
  params: { id: string };
}

/**
 * /tenant-users/[id] — User detail page.
 *
 * Access: PlatformAdmin only.
 *
 * Data: served from mock stub in controlCenterServerApi.users.getById(id).
 * TODO: When GET /identity/api/admin/users/{id} is live, the stub auto-wires —
 *       no page change needed, only the API method in control-center-api.ts.
 */
export default async function UserDetailPage({ params }: UserDetailPageProps) {
  const session = await requirePlatformAdmin();
  const { id }  = params;

  let user = null;
  let fetchError: string | null = null;

  try {
    user = await controlCenterServerApi.users.getById(id);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load user.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">

        {/* Breadcrumb */}
        <nav className="flex items-center gap-1.5 text-sm text-gray-500">
          <Link href={Routes.tenantUsers} className="hover:text-gray-900 transition-colors">
            Tenant Users
          </Link>
          <span className="text-gray-300">›</span>
          <span className="text-gray-900 font-medium">
            {user ? `${user.firstName} ${user.lastName}` : id}
          </span>
        </nav>

        {/* Error state */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Not found state */}
        {!fetchError && !user && (
          <div className="bg-white border border-gray-200 rounded-lg p-10 text-center space-y-3">
            <p className="text-sm font-medium text-gray-700">User not found</p>
            <p className="text-xs text-gray-400">
              No user with ID <code className="font-mono bg-gray-100 px-1 rounded">{id}</code> exists.
            </p>
            <Link href={Routes.tenantUsers} className="text-xs text-indigo-600 hover:underline">
              ← Back to Tenant Users
            </Link>
          </div>
        )}

        {/* Detail content */}
        {user && (
          <>
            {/* Page header */}
            <div className="flex items-start justify-between gap-4 flex-wrap">
              <div className="space-y-2">
                {/* Name + email */}
                <div>
                  <h1 className="text-xl font-semibold text-gray-900">
                    {user.firstName} {user.lastName}
                  </h1>
                  <p className="text-sm text-gray-500 mt-0.5">{user.email}</p>
                </div>
                {/* Badge row */}
                <div className="flex items-center gap-2 flex-wrap">
                  <StatusBadge status={user.status} />
                  <RoleBadge role={user.role} />
                  <TenantBadge
                    tenantId={user.tenantId}
                    tenantCode={user.tenantCode}
                    tenantDisplayName={user.tenantDisplayName}
                  />
                  {(user.isLocked ?? false) && <LockedBadge />}
                </div>
              </div>

              {/* Action buttons */}
              <UserActions
                userId={user.id}
                currentStatus={user.status}
                isLocked={user.isLocked}
              />
            </div>

            {/* Detail sections */}
            <UserDetailCard user={user} />
          </>
        )}
      </div>
    </CCShell>
  );
}

// ── Local badge helpers ───────────────────────────────────────────────────────

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

function RoleBadge({ role }: { role: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-indigo-50 text-indigo-700 border-indigo-200">
      {role}
    </span>
  );
}

function TenantBadge({
  tenantId,
  tenantCode,
  tenantDisplayName,
}: {
  tenantId:          string;
  tenantCode:        string;
  tenantDisplayName: string;
}) {
  return (
    <Link
      href={Routes.tenantDetail(tenantId)}
      className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-50 text-gray-700 border-gray-200 hover:bg-gray-100 transition-colors"
    >
      {tenantDisplayName}
      <span className="font-mono text-[10px] text-gray-400">{tenantCode}</span>
    </Link>
  );
}

function LockedBadge() {
  return (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-red-50 text-red-700 border-red-200">
      <span className="w-1.5 h-1.5 rounded-full bg-red-500 inline-block" />
      Locked
    </span>
  );
}
