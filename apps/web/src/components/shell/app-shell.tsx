"use client";

import type { ReactNode } from "react";
import { usePathname } from "next/navigation";
import { BreadcrumbProvider } from "@/contexts/breadcrumb-context";
import { ProductProvider } from "@/contexts/product-context";
import { SettingsProvider } from "@/contexts/settings-context";
import { SidebarProvider } from "@/contexts/sidebar-context";
import { TopBar } from "./top-bar";
import { Sidebar } from "./sidebar";

interface AppShellProps {
  children: ReactNode;
  initialMapProvider?: "osm" | "google";
  initialTimezone?: string;
}

/**
 * Shared layout shell for all (platform) and (admin) routes.
 *
 * Structure:
 *   [white top bar — full width: logo + product switcher + user]
 *   [light sidebar: product nav]  [gray-50 main content]
 *
 * The landing dashboard (/dashboard) has no product selected, so it renders
 * without the product sidebar — it's a hub page, not a product workspace.
 */
export function AppShell({
  children,
  initialMapProvider,
  initialTimezone,
}: AppShellProps) {
  const pathname = usePathname();
  const isDashboard = pathname === "/dashboard" || pathname === "/";

  return (
    <SettingsProvider
      initialMapProvider={initialMapProvider}
      initialTimezone={initialTimezone}
    >
      <ProductProvider>
        <SidebarProvider>
          <BreadcrumbProvider>
            <div className="flex flex-col h-screen overflow-hidden">
              <TopBar isDashboard={isDashboard} />
              <div className="flex flex-1 overflow-hidden">
                {!isDashboard && <Sidebar />}
                <main className="flex-1 overflow-y-auto bg-white p-6">
                  {children}
                </main>
              </div>
            </div>
          </BreadcrumbProvider>
        </SidebarProvider>
      </ProductProvider>
    </SettingsProvider>
  );
}
