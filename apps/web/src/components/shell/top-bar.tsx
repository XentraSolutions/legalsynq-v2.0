'use client';

import { useSession } from '@/hooks/use-session';

/**
 * Minimal content-area header.
 * Shows the current org name + user email right-aligned,
 * matching the top-right workspace context in the design.
 */
export function TopBar() {
  const { session } = useSession();

  if (!session?.hasOrg) return null;

  return (
    <div className="flex items-center justify-end px-6 py-3 bg-white border-b border-gray-100 shrink-0">
      <div className="text-right leading-tight">
        <p className="text-sm font-semibold text-gray-900">{session.orgName}</p>
        <p className="text-xs text-gray-400">{session.email}</p>
      </div>
    </div>
  );
}
