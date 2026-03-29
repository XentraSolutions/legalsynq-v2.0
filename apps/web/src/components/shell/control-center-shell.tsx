'use client';

import type { ReactNode } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useSession } from '@/hooks/use-session';
import { useTenantBranding } from '@/hooks/use-tenant-branding';
import { buildControlCenterNav } from '@/lib/control-center-nav';
import type { NavGroup, NavItem } from '@/types';
import { clsx } from 'clsx';

interface ControlCenterShellProps {
  children: ReactNode;
}

/**
 * Dedicated layout shell for Control Center (/control-center) routes.
 *
 * Mirrors AppShell in structure and visual style, but:
 *  - Uses buildControlCenterNav() instead of buildNavGroups()
 *  - Shows a "Control Center" identity badge instead of org/product switcher
 *  - Intended exclusively for PlatformAdmin users
 *
 * Must be used inside <SessionProvider> and <TenantBrandingProvider>.
 */
export function ControlCenterShell({ children }: ControlCenterShellProps) {
  return (
    <div className="flex flex-col h-screen bg-gray-50">
      <ControlCenterTopBar />
      <div className="flex flex-1 overflow-hidden">
        <ControlCenterSidebar />
        <main className="flex-1 overflow-y-auto p-6">
          {children}
        </main>
      </div>
    </div>
  );
}

// ── Top bar ───────────────────────────────────────────────────────────────────

function ControlCenterTopBar() {
  const { session, clearSession } = useSession();
  const branding = useTenantBranding();

  async function handleSignOut() {
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

      {/* Control Center identity badge */}
      <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-indigo-50 border border-indigo-200">
        <span className="text-xs font-semibold text-indigo-700 tracking-wide uppercase">
          Control Center
        </span>
      </div>

      <div className="flex-1" />

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

// ── Sidebar ───────────────────────────────────────────────────────────────────

function ControlCenterSidebar() {
  const { session } = useSession();
  const pathname    = usePathname();
  const groups      = session ? buildControlCenterNav(session) : [];

  if (!session) return null;

  return (
    <aside className="w-56 shrink-0 border-r border-gray-200 bg-white flex flex-col h-full overflow-y-auto">
      <nav className="flex-1 py-4 space-y-6 px-2">
        {groups.map(group => (
          <SidebarGroup key={group.id} group={group} pathname={pathname} />
        ))}
      </nav>
    </aside>
  );
}

function SidebarGroup({ group, pathname }: { group: NavGroup; pathname: string }) {
  return (
    <div>
      <p className="px-3 mb-1 text-[11px] font-semibold uppercase tracking-wider text-gray-400">
        {group.label}
      </p>
      <ul className="space-y-0.5">
        {group.items.map(item => (
          <SidebarItem key={item.href} item={item} pathname={pathname} />
        ))}
      </ul>
    </div>
  );
}

function SidebarItem({ item, pathname }: { item: NavItem; pathname: string }) {
  const isActive = pathname === item.href || pathname.startsWith(item.href + '/');
  return (
    <li>
      <Link
        href={item.href}
        className={clsx(
          'flex items-center gap-2 px-3 py-2 rounded-md text-sm font-medium transition-colors',
          isActive
            ? 'bg-indigo-50 text-indigo-700'
            : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900',
        )}
      >
        {item.label}
      </Link>
    </li>
  );
}
