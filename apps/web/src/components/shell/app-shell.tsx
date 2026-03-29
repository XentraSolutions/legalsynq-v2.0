'use client';

import type { ReactNode } from 'react';
import { ProductProvider } from '@/contexts/product-context';
import { TopBar } from './top-bar';
import { Sidebar } from './sidebar';

interface AppShellProps {
  children: ReactNode;
}

/**
 * Shared layout shell for all (platform) and (admin) routes.
 *
 * Structure:
 *   [dark sidebar: product brand + nav + user profile]
 *   [content area: org-info header strip (top-right) + page content]
 *
 * Must be used inside <SessionProvider> and <TenantBrandingProvider>.
 */
export function AppShell({ children }: AppShellProps) {
  return (
    <ProductProvider>
      <div className="flex h-screen overflow-hidden">
        <Sidebar />
        <div className="flex flex-col flex-1 overflow-hidden bg-white">
          <TopBar />
          <main className="flex-1 overflow-y-auto bg-gray-50">
            {children}
          </main>
        </div>
      </div>
    </ProductProvider>
  );
}
