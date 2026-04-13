import Link from 'next/link';
import { requireAdmin }            from '@/lib/auth-guards';
import { controlCenterServerApi }  from '@/lib/control-center-api';
import { Routes }                  from '@/lib/routes';
import { CCShell }                 from '@/components/shell/cc-shell';
import { RoleDetailCard }          from '@/components/roles/role-detail-card';
import { RolePermissionPanel }     from '@/components/roles/role-permission-panel';

interface RoleDetailPageProps {
  params: Promise<{ id: string }>;
}

/**
 * /roles/[id] — Role detail page.
 *
 * Access: PlatformAdmin only.
 *
 * Loads:
 *   - Role detail (id, name, description, userCount, isSystemRole)
 *   - Current capability assignments for the role
 *   - Full permissions catalog (for the "Assign Permission" picker)
 *
 * UIX-005: RolePermissionPanel wired — assign / revoke capabilities.
 */
export default async function RoleDetailPage({ params }: RoleDetailPageProps) {
  const session       = await requireAdmin();
  const isTenantAdmin = session.isTenantAdmin;
  const { id }  = await params;

  let role = null;
  let fetchError: string | null = null;

  try {
    role = await controlCenterServerApi.roles.getById(id);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load role.';
  }

  // Load permissions data only if role was found — failures are non-fatal
  const [permissionsResult, catalogResult] = await Promise.allSettled([
    role ? controlCenterServerApi.roles.getPermissions(id) : Promise.resolve([]),
    controlCenterServerApi.permissions.list(),
  ]);

  const assignedPermissions = permissionsResult.status === 'fulfilled' ? permissionsResult.value : [];
  const catalog             = catalogResult.status    === 'fulfilled' ? catalogResult.value    : [];
  const permFetchError      = permissionsResult.status === 'rejected'
    ? (permissionsResult.reason instanceof Error ? permissionsResult.reason.message : 'Failed to load permissions.')
    : null;

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6">

        {/* Breadcrumb */}
        <nav className="flex items-center gap-1.5 text-sm text-gray-500">
          <Link href={Routes.roles} className="hover:text-gray-900 transition-colors">
            Roles & Permissions
          </Link>
          <span className="text-gray-300">›</span>
          <span className="text-gray-900 font-medium">
            {role ? role.name : id}
          </span>
        </nav>

        {/* Error state */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Not found state */}
        {!fetchError && !role && (
          <div className="bg-white border border-gray-200 rounded-lg p-10 text-center space-y-3">
            <p className="text-sm font-medium text-gray-700">Role not found</p>
            <p className="text-xs text-gray-400">
              No role with ID <code className="font-mono bg-gray-100 px-1 rounded">{id}</code> exists.
            </p>
            <Link href={Routes.roles} className="text-xs text-indigo-600 hover:underline">
              ← Back to Roles
            </Link>
          </div>
        )}

        {/* Detail content */}
        {role && (
          <>
            {/* Page header */}
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-1">
                <h1 className="text-xl font-semibold text-gray-900">{role.name}</h1>
                <p className="text-sm text-gray-500">{role.description}</p>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                {role.isSystemRole !== false && (
                  <span className="inline-flex items-center px-2.5 py-1 rounded text-xs font-semibold border bg-gray-100 text-gray-600 border-gray-200">
                    System-defined
                  </span>
                )}
                <span className="inline-flex items-center px-2.5 py-1 rounded text-xs font-semibold border bg-indigo-50 text-indigo-700 border-indigo-100">
                  {assignedPermissions.length} permission{assignedPermissions.length !== 1 ? 's' : ''}
                </span>
              </div>
            </div>

            {/* Role metadata */}
            <RoleDetailCard role={role} />

            {/* Divider */}
            <div className="border-t border-gray-200" />

            {/* Permissions section */}
            {permFetchError ? (
              <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
                Failed to load permissions: {permFetchError}
              </div>
            ) : (
              <RolePermissionPanel
                roleId={id}
                isSystemRole={role.isSystemRole ?? false}
                assignedItems={assignedPermissions}
                catalog={catalog}
                isTenantAdmin={isTenantAdmin}
              />
            )}
          </>
        )}
      </div>
    </CCShell>
  );
}
