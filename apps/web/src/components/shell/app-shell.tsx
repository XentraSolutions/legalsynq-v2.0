'use client';

import type { ReactNode } from 'react';
import { TopBar } from './top-bar';
import { Sidebar } from './sidebar';

interface AppShellProps {
  children: ReactNode;
}

/**
 * Shared layout shell for all (platform) and (admin) routes.
 * Composes: TopBar + Sidebar + main content area.
 *
 * Must be used inside <SessionProvider> and <TenantBrandingProvider>.
 */
export function AppShell({ children }: AppShellProps) {
  return (
    <div className="flex flex-col h-screen bg-gray-50">
      <TopBar />
      <div className="flex flex-1 overflow-hidden">
        <Sidebar />
        <main className="flex-1 overflow-y-auto p-6">
          {children}
        </main>
      </div>
    </div>
  );
}
