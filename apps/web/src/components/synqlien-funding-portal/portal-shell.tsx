"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ChevronDown, ChevronRight, PanelLeft } from "lucide-react";
import { useEffect, useState } from "react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import type { PlatformSession } from "@/types";
import { useSession } from "@/hooks/use-session";
import { cn } from "@/lib/utils";

const SIDEBAR_STORAGE_KEY = "ls_synqlien_funding_sidebar_collapsed";
const EXPANDED_SIDEBAR_WIDTH = 255;
const COLLAPSED_SIDEBAR_WIDTH = 64;

const NAV_ITEMS = [
  {
    href: "/funding/dashboard",
    label: "Dashboard",
    icon: "ri-dashboard-line",
  },
  {
    href: "/funding/offered-liens",
    label: "Offered Liens",
    icon: "ri-file-list-3-line",
  },
];

type HeaderBreadcrumb = {
  href?: string;
  label: string;
};

export function SynqLienFundingPortalShell({
  session,
  children,
}: {
  session: PlatformSession;
  children: React.ReactNode;
}) {
  const pathname = usePathname();
  const { session: liveSession, logout } = useSession();
  const activeSession = liveSession ?? session;
  const orgName = activeSession.orgName || "Funding portal";
  const initials = buildInitials(orgName, activeSession.email);
  const [collapsed, setCollapsed] = useState(false);
  const currentNavItem =
    NAV_ITEMS.find(item => pathname === item.href || pathname.startsWith(`${item.href}/`)) ??
    NAV_ITEMS[0];
  const breadcrumbs = buildHeaderBreadcrumbs(pathname, currentNavItem.label);

  useEffect(() => {
    setCollapsed(localStorage.getItem(SIDEBAR_STORAGE_KEY) === "true");
  }, []);

  function handleSignOut() {
    void logout("/login");
  }

  function toggleSidebar() {
    setCollapsed(value => {
      const next = !value;
      localStorage.setItem(SIDEBAR_STORAGE_KEY, String(next));
      return next;
    });
  }

  const sidebarWidth = collapsed ? COLLAPSED_SIDEBAR_WIDTH : EXPANDED_SIDEBAR_WIDTH;

  return (
    <div
      className="min-h-screen bg-white text-[#0a0a0a]"
      style={{
        fontFamily: '"Plus Jakarta Sans", Inter, Arial, "Helvetica Neue", sans-serif',
      }}
    >
      <aside
        id="synqlien-funding-sidebar"
        className="fixed inset-y-0 left-0 z-40 hidden flex-col border-r border-[#e5e5e5] bg-[#fafafa] transition-[width] duration-200 ease-out lg:flex"
        style={{ width: sidebarWidth }}
      >
        <div
          className={cn(
            "flex h-[81px] w-full items-center border-b border-[#e5e5e5]",
            collapsed ? "justify-center px-0" : "px-2",
          )}
        >
          <Link
            href="/funding/dashboard"
            className={cn(
              "flex h-7 items-center px-[4.5px]",
              collapsed ? "justify-center" : "gap-[9.5px]",
            )}
            aria-label="SynqLien dashboard"
            title={collapsed ? "SynqLien" : undefined}
          >
            <Image
              src="/product-icons/synqlien.png"
              alt=""
              width={20}
              height={20}
              priority
              unoptimized
              className="h-5 w-5"
            />
            <span
              className={cn(
                "text-[17px] font-bold leading-5 text-[#0a0a0a]",
                collapsed && "sr-only",
              )}
            >
              Synq<span className="text-[#ee7132]">Lien</span>
            </span>
          </Link>
        </div>

        <nav className="flex-1 pb-6 pt-4">
          <div className="flex w-full flex-col gap-1 p-2">
            {NAV_ITEMS.map(item => {
              const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  title={collapsed ? item.label : undefined}
                  className={cn(
                    "flex h-8 items-center rounded-[8px] text-[14px] font-normal leading-[1.6] transition-colors",
                    collapsed ? "w-12 justify-center px-0" : "w-[239px] gap-2 px-2",
                    active
                      ? "bg-[rgba(238,113,50,0.05)] text-[#ee7132]"
                      : "text-[#0a0a0a] hover:bg-white hover:text-[#ee7132]",
                  )}
                >
                  <i className={`${item.icon} text-[16px]`} />
                  <span className={cn("truncate", collapsed && "sr-only")}>{item.label}</span>
                </Link>
              );
            })}
          </div>
        </nav>
      </aside>

      <div
        className="transition-[padding] duration-200 ease-out lg:pl-[var(--synqlien-sidebar-offset)]"
        style={{
          "--synqlien-sidebar-offset": `${sidebarWidth}px`,
        } as React.CSSProperties}
      >
        <header className="sticky top-0 z-30 h-[81px] border-b border-[#e5e5e5] bg-white">
          <div className="flex h-full items-center justify-between gap-3 px-4 sm:px-6">
            <div className="flex min-w-0 flex-1 items-center gap-2">
              <Link href="/funding/dashboard" className="flex shrink-0 items-center gap-[9.5px] lg:hidden">
                <Image
                  src="/product-icons/synqlien.png"
                  alt=""
                  width={20}
                  height={20}
                  priority
                  unoptimized
                  className="h-5 w-5"
                />
                <span className="text-[17px] font-bold leading-5 text-[#0a0a0a]">
                  Synq<span className="text-[#ee7132]">Lien</span>
                </span>
              </Link>
              <button
                type="button"
                onClick={toggleSidebar}
                aria-controls="synqlien-funding-sidebar"
                aria-label={collapsed ? "Expand navigation" : "Collapse navigation"}
                aria-expanded={!collapsed}
                title={collapsed ? "Expand navigation" : "Collapse navigation"}
                className="hidden h-7 w-7 shrink-0 items-center justify-center rounded-[8px] text-[#0a0a0a] transition-colors hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] lg:flex"
              >
                <PanelLeft className="h-4 w-4" aria-hidden="true" strokeWidth={2} />
              </button>
              <span className="hidden h-[17px] w-4 items-center justify-center lg:flex">
                <span className="h-full w-px bg-[#e5e5e5]" />
              </span>
              <HeaderBreadcrumbs items={breadcrumbs} />
              <nav className="ml-auto flex items-center gap-1 lg:hidden">
                {NAV_ITEMS.map(item => {
                  const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      aria-label={item.label}
                      className={cn(
                        "flex h-9 w-9 items-center justify-center rounded-[8px] transition-colors",
                        active
                          ? "bg-[rgba(238,113,50,0.08)] text-[#ee7132]"
                          : "text-[#525252] hover:bg-[#f5f5f5] hover:text-[#0a0a0a]",
                      )}
                      title={item.label}
                    >
                      <i className={`${item.icon} text-[18px]`} />
                    </Link>
                  );
                })}
              </nav>
            </div>

            <div className="flex items-center gap-2">
              <Link
                href="/funding/offered-liens?status=Pending"
                aria-label="Notifications"
                title="Notifications"
                className="flex h-7 w-7 items-center justify-center rounded-[8px] text-[#0a0a0a] transition-colors hover:bg-[#f5f5f5]"
              >
                <i className="ri-notification-3-line text-[16px]" />
              </Link>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <button
                    type="button"
                    aria-label="Open account menu"
                    className="flex min-w-0 items-center gap-2 rounded-[8px] py-1 pl-1 pr-0 text-left transition-colors hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] sm:pr-1"
                  >
                    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#fdf1eb] text-center text-[18px] font-medium leading-[1.6] text-[#a95024]">
                      {initials}
                    </span>
                    <span className="hidden min-w-0 flex-col items-start leading-[1.6] text-[#0a0a0a] sm:flex">
                      <span className="max-w-[220px] truncate text-[14px] font-bold">
                        {orgName}
                      </span>
                      <span className="text-[12px] font-normal text-[#525252]">
                        Funding Company
                      </span>
                    </span>
                    <ChevronDown
                      className="h-4 w-4 shrink-0 text-[#737373]"
                      aria-hidden="true"
                      strokeWidth={2}
                    />
                  </button>
                </DropdownMenuTrigger>
                <DropdownMenuContent
                  align="end"
                  sideOffset={10}
                  className="w-[220px] rounded-[8px] border-[#e5e5e5] p-1 shadow-[0px_8px_24px_rgba(10,10,10,0.12)]"
                >
                  <DropdownMenuItem asChild className="h-10 gap-3 rounded-[8px] px-3 text-[14px] text-[#0a0a0a] focus:bg-[#f5f5f5]">
                    <Link href="/funding/settings">
                      <i className="ri-settings-3-line text-[16px] leading-none text-[#737373]" />
                      <span>Account Settings</span>
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuItem
                    onSelect={handleSignOut}
                    className="h-10 gap-3 rounded-[8px] px-3 text-[14px] text-red-600 focus:bg-red-50 focus:text-red-600"
                  >
                    <i className="ri-logout-box-r-line text-[16px] leading-none" />
                    <span>Log out</span>
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </div>
        </header>

        <main className="px-4 py-5 sm:px-6 lg:px-6">
          {children}
        </main>
      </div>
    </div>
  );
}

function HeaderBreadcrumbs({ items }: { items: HeaderBreadcrumb[] }) {
  return (
    <nav aria-label="Breadcrumb" className="hidden min-w-0 lg:block">
      <ol className="flex min-w-0 items-center gap-1.5">
        {items.map((item, index) => {
          const current = index === items.length - 1;
          return (
            <li key={`${item.label}-${index}`} className="flex min-w-0 items-center gap-1.5">
              {index > 0 ? (
                <ChevronRight className="h-3.5 w-3.5 shrink-0 text-[#737373]" aria-hidden="true" strokeWidth={2} />
              ) : null}
              {item.href && !current ? (
                <Link
                  href={item.href}
                  className="truncate text-[14px] font-normal leading-[1.6] text-[#737373] transition-colors hover:text-[#0a0a0a]"
                >
                  {item.label}
                </Link>
              ) : (
                <span
                  aria-current={current ? "page" : undefined}
                  className={cn(
                    "truncate text-[14px] font-normal leading-[1.6]",
                    current ? "text-[#0a0a0a]" : "text-[#737373]",
                  )}
                >
                  {item.label}
                </span>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

function buildHeaderBreadcrumbs(pathname: string, currentLabel: string): HeaderBreadcrumb[] {
  if (/^\/funding\/offered-liens\/[^/]+/.test(pathname)) {
    return [
      { href: "/funding/offered-liens", label: "Offered Liens" },
      { label: "Lien" },
    ];
  }

  if (pathname === "/funding/settings") {
    return [{ label: "Account Settings" }];
  }

  return [{ label: currentLabel }];
}

function buildInitials(orgName: string, email: string): string {
  const orgParts = orgName
    .split(/\s+/)
    .map(part => part.trim())
    .filter(Boolean);
  if (orgParts.length >= 2) {
    return `${orgParts[0][0]}${orgParts[1][0]}`.toUpperCase();
  }
  if (orgParts.length === 1) {
    return orgParts[0].slice(0, 2).toUpperCase();
  }

  const local = email.split("@")[0] ?? "";
  const parts = local.split(/[._-]/).filter(Boolean);
  if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  return (local.slice(0, 2) || "SL").toUpperCase();
}
