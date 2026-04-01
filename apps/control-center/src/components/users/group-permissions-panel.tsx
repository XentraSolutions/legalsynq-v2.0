import Link from 'next/link';
import { Routes } from '@/lib/routes';

interface GroupPermissionsPanelProps {
  groupName: string;
}

/**
 * GroupPermissionsPanel — informational notice explaining that group-level
 * permission management is not yet available.
 *
 * In the current model, permissions are granted via Role → Capability
 * assignments. Groups control membership scoping but do not have direct
 * permission grants. Users acquire permissions through their role assignments.
 *
 * UIX-005
 */
export function GroupPermissionsPanel({ groupName }: GroupPermissionsPanelProps) {
  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-base font-semibold text-gray-900">Group Permissions</h2>
        <p className="text-xs text-gray-500 mt-0.5">
          How permissions apply to members of <strong>{groupName}</strong>.
        </p>
      </div>

      <div className="bg-blue-50 border border-blue-200 rounded-lg p-5 space-y-3">
        <div className="flex items-start gap-3">
          <svg
            className="h-5 w-5 text-blue-500 mt-0.5 shrink-0"
            viewBox="0 0 20 20"
            fill="currentColor"
            aria-hidden="true"
          >
            <path
              fillRule="evenodd"
              d="M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Zm-7-4a1 1 0 1 1-2 0 1 1 0 0 1 2 0ZM9 9a.75.75 0 0 0 0 1.5h.253a.25.25 0 0 1 .244.304l-.459 2.066A1.75 1.75 0 0 0 10.747 15H11a.75.75 0 0 0 0-1.5h-.253a.25.25 0 0 1-.244-.304l.459-2.066A1.75 1.75 0 0 0 9.253 9H9Z"
              clipRule="evenodd"
            />
          </svg>
          <div className="space-y-2 text-sm text-blue-800">
            <p>
              <strong>Groups control membership, not permissions.</strong>{' '}
              In LegalSynq, permissions are granted via <em>Roles</em> — each role is a named
              collection of capabilities (granular permissions). Users acquire permissions
              through their role assignments.
            </p>
            <p>
              Group membership determines which users belong to an organizational context. To
              give group members access to specific features, assign the appropriate role to each
              user directly.
            </p>
          </div>
        </div>

        <div className="border-t border-blue-100 pt-3 flex items-center gap-3">
          <Link
            href={Routes.roles}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md bg-white border border-blue-200 text-blue-700 hover:bg-blue-50 transition-colors"
          >
            <svg className="h-3.5 w-3.5" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
              <path d="M3 4.75a1 1 0 1 1-2 0 1 1 0 0 1 2 0ZM6.25 4a.75.75 0 0 0 0 1.5h7a.75.75 0 0 0 0-1.5h-7ZM6.25 7.25a.75.75 0 0 0 0 1.5h7a.75.75 0 0 0 0-1.5h-7ZM6.25 10.5a.75.75 0 0 0 0 1.5h7a.75.75 0 0 0 0-1.5h-7ZM3 8.25a1 1 0 1 1-2 0 1 1 0 0 1 2 0ZM3 11.75a1 1 0 1 1-2 0 1 1 0 0 1 2 0Z" />
            </svg>
            Manage Roles
          </Link>
          <Link
            href={Routes.permissions}
            className="inline-flex items-center gap-1.5 text-sm text-blue-600 hover:text-blue-800 hover:underline transition-colors"
          >
            View permission catalog →
          </Link>
        </div>
      </div>
    </div>
  );
}
