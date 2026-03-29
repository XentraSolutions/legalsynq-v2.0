'use client';

import { useSession } from '@/hooks/use-session';
import { useTenantBranding } from '@/hooks/use-tenant-branding';
import { buildNavGroups } from '@/lib/nav';
import { OrgBadge } from './org-badge';
import { ProductSwitcher } from './product-switcher';

export function TopBar() {
  const { session, clearSession } = useSession();
  const branding = useTenantBranding();
  const navGroups = session ? buildNavGroups(session) : [];

  async function handleSignOut() {
    // POST to the Next.js BFF logout route — it deletes the HttpOnly cookie
    // and fire-and-forgets the backend logout notification.
    await fetch('/api/auth/logout', { method: 'POST' });
    clearSession();
    window.location.href = '/login';
  }

  return (
    <header className="h-14 border-b border-gray-200 bg-white flex items-center px-4 gap-4 z-10">
      {/* Tenant logo / name */}
      <div className="flex items-center gap-2 shrink-0">
        {branding.logoUrl ? (
          <img src={branding.logoUrl} alt={branding.displayName} className="h-7 w-auto" />
        ) : (
          <span className="font-semibold text-gray-900">{branding.displayName}</span>
        )}
      </div>

      <div className="w-px h-6 bg-gray-200" />

      {/* Org badge */}
      {session?.hasOrg && (
        <OrgBadge orgType={session.orgType} orgName={session.orgName} />
      )}

      {/* Product switcher */}
      <div className="flex-1 flex items-center">
        <ProductSwitcher groups={navGroups} />
      </div>

      {/* Control Center switcher — PlatformAdmin only */}
      {session?.isPlatformAdmin && (
        <button
          onClick={() => {
            const { protocol, hostname } = window.location;
            window.location.href = `${protocol}//${hostname}:5004`;
          }}
          title="Switch to Control Center (port 5004)"
          className="flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium border border-indigo-200 text-indigo-700 bg-indigo-50 hover:bg-indigo-100 hover:border-indigo-300 transition-colors shrink-0"
        >
          <span>Control Center</span>
          <span>→</span>
        </button>
      )}

      {/* User menu */}
      {session && (
        <div className="flex items-center gap-3 shrink-0">
          <span className="text-sm text-gray-600">{session.email}</span>
          <button
            onClick={handleSignOut}
            className="text-sm text-gray-500 hover:text-gray-900 transition-colors"
          >
            Sign out
          </button>
        </div>
      )}
    </header>
  );
}
