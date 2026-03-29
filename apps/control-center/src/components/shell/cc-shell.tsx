import type { ReactNode } from 'react';
import Link from 'next/link';
import { SignOutButton } from './sign-out-button';
import { CCSidebar } from './cc-sidebar';
import { AppSwitcher } from './app-switcher';

interface CCShellProps {
  children:  ReactNode;
  userEmail: string;
}

/**
 * Control Center shell — wraps every authenticated CC page.
 *
 * Receives userEmail as a prop from the Server Component layout (no client-side
 * session hook needed — the layout already calls requirePlatformAdmin()).
 *
 * Layout: fixed top bar + fixed sidebar + scrollable main content area.
 */
export function CCShell({ children, userEmail }: CCShellProps) {
  return (
    <div className="flex flex-col h-screen bg-gray-50">
      {/* Top bar */}
      <header className="h-14 border-b border-gray-200 bg-white flex items-center px-4 gap-4 z-10 shrink-0">
        <Link href="/tenants" className="flex items-center gap-2 shrink-0">
          <span className="font-semibold text-gray-900 text-sm">LegalSynq</span>
        </Link>

        <div className="w-px h-6 bg-gray-200" />

        <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-indigo-50 border border-indigo-200">
          <span className="text-xs font-semibold text-indigo-700 tracking-wide uppercase">
            Control Center
          </span>
        </div>

        <div className="flex-1" />

        <div className="flex items-center gap-3 shrink-0">
          <AppSwitcher />
          <div className="w-px h-4 bg-gray-200" />
          <span className="text-sm text-gray-600">{userEmail}</span>
          <SignOutButton />
        </div>
      </header>

      {/* Body */}
      <div className="flex flex-1 overflow-hidden">
        <CCSidebar />
        <main className="flex-1 overflow-y-auto p-6">
          {children}
        </main>
      </div>
    </div>
  );
}
