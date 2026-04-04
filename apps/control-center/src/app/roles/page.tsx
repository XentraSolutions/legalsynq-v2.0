import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { RoleListTable } from '@/components/roles/role-list-table';

/**
 * /roles — Platform roles & permissions list.
 *
 * Access: PlatformAdmin only.
 *
 * Roles are system-defined and not user-creatable through the UI.
 * This page is read-only — there is no "Create Role" action.
 *
 * Data: served from mock stub in controlCenterServerApi.roles.list().
 * TODO: When GET /identity/api/admin/roles is live, the stub auto-wires —
 *       no page change needed.
 */
export default async function RolesPage() {
  const session = await requirePlatformAdmin();

  let roles = null;
  let fetchError: string | null = null;

  try {
    roles = await controlCenterServerApi.roles.list();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load roles.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Roles & Permissions</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Platform-level roles that govern Control Center access
            </p>
          </div>
        </div>

        {/* Info banner */}
        <div className="bg-indigo-50 border border-indigo-100 rounded-lg px-4 py-3 text-sm text-indigo-700">
          System roles are platform-managed. Product roles are auto-provisioned from product definitions
          and enforce org-type eligibility and product enablement rules during assignment.
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Table */}
        {roles && <RoleListTable roles={roles} />}

      </div>
    </CCShell>
  );
}
