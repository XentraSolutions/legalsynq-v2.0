'use client';

import type { ReactNode } from 'react';
import { ProductProvider } from '@/contexts/product-context';
import { SettingsProvider } from '@/contexts/settings-context';
import { TopBar } from './top-bar';
import { Sidebar } from './sidebar';
import { SynqLienPortalShell } from '@/components/lien/synqlien-portal-shell';
import type { PortalConfig } from '@/lib/portal';

interface AppShellProps {
  children:            ReactNode;
  initialMapProvider?: 'osm' | 'google';
  initialTimezone?:    string;
  portalProductId?:    PortalConfig['productId'] | null;
}

/**
 * Shared layout shell for all (platform) and (admin) routes.
 *
 * Default structure:
 *   [navy top bar — full width: logo + product switcher + user]
 *   [light sidebar: product nav]  [gray-50 main content]
 *
 * Product-specific common portals may swap in a narrower shell while keeping
 * the same providers and route guards.
 */
export function AppShell({
  children,
  initialMapProvider,
  initialTimezone,
  portalProductId,
}: AppShellProps) {
  return (
    <SettingsProvider initialMapProvider={initialMapProvider} initialTimezone={initialTimezone}>
      <ProductProvider>
        {portalProductId === 'synqlien' ? (
          <SynqLienPortalShell>{children}</SynqLienPortalShell>
        ) : (
          <div className="flex flex-col h-screen overflow-hidden">
            <TopBar />
            <div className="flex flex-1 overflow-hidden">
              <Sidebar />
              <main className="flex-1 overflow-y-auto bg-gray-50 p-6">
                {children}
              </main>
            </div>
          </div>
        )}
      </ProductProvider>
    </SettingsProvider>
  );
}
