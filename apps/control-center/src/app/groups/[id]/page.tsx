import Link from 'next/link';
import { requirePlatformAdmin }      from '@/lib/auth-guards';
import { controlCenterServerApi }    from '@/lib/control-center-api';
import { Routes }                    from '@/lib/routes';
import { CCShell }                   from '@/components/shell/cc-shell';
import { GroupDetailCard }           from '@/components/users/group-detail-card';
import { GroupPermissionsPanel }     from '@/components/users/group-permissions-panel';

interface GroupDetailPageProps {
  params: { id: string };
}

/**
 * /groups/[id] — Group detail page showing members and permissions information.
 * Access: PlatformAdmin only.
 *
 * UIX-005: GroupPermissionsPanel wired — informational, explains the role-based
 * permission model and links to Roles & Permission Catalog.
 */
export default async function GroupDetailPage({ params }: GroupDetailPageProps) {
  const session = await requirePlatformAdmin();
  const { id }  = params;

  let group = null;
  let fetchError: string | null = null;

  try {
    group = await controlCenterServerApi.groups.getById(id);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load group.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">

        {/* Breadcrumb */}
        <nav className="flex items-center gap-1.5 text-sm text-gray-500">
          <Link href={Routes.groups} className="hover:text-gray-900 transition-colors">
            Groups
          </Link>
          <span className="text-gray-300">›</span>
          <span className="text-gray-900 font-medium">
            {group ? group.name : id}
          </span>
        </nav>

        {/* Error */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Not found */}
        {!fetchError && !group && (
          <div className="bg-white border border-gray-200 rounded-lg p-10 text-center space-y-3">
            <p className="text-sm font-medium text-gray-700">Group not found</p>
            <p className="text-xs text-gray-400">
              No group with ID <code className="font-mono bg-gray-100 px-1 rounded">{id}</code> exists.
            </p>
            <Link href={Routes.groups} className="text-xs text-indigo-600 hover:underline">
              ← Back to Groups
            </Link>
          </div>
        )}

        {/* Detail */}
        {group && (
          <>
            <div className="flex items-start justify-between gap-4 flex-wrap">
              <div>
                <h1 className="text-xl font-semibold text-gray-900">{group.name}</h1>
                {group.description && (
                  <p className="text-sm text-gray-500 mt-0.5">{group.description}</p>
                )}
              </div>
              <div className="flex items-center gap-2">
                {group.isActive ? (
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-green-50 text-green-700 border-green-200">
                    Active
                  </span>
                ) : (
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-100 text-gray-500 border-gray-200">
                    Inactive
                  </span>
                )}
                <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-50 text-gray-600 border-gray-200">
                  {group.memberCount} member{group.memberCount !== 1 ? 's' : ''}
                </span>
              </div>
            </div>

            {/* Members */}
            <GroupDetailCard group={group} />

            {/* Permissions info (UIX-005) */}
            <GroupPermissionsPanel groupName={group.name} />
          </>
        )}
      </div>
    </CCShell>
  );
}
