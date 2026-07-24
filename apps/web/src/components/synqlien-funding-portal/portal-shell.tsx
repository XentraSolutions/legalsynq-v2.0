"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { PlatformSession } from "@/types";
import { useSession } from "@/hooks/use-session";
import { cn } from "@/lib/utils";

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
  const currentNavItem =
    NAV_ITEMS.find(item => pathname === item.href || pathname.startsWith(`${item.href}/`)) ??
    NAV_ITEMS[0];

  function handleSignOut() {
    void logout("/login");
  }

  return (
    <div
      className="min-h-screen bg-white text-[#0a0a0a]"
      style={{
        fontFamily: '"Plus Jakarta Sans", Inter, Arial, "Helvetica Neue", sans-serif',
      }}
    >
      <aside className="fixed inset-y-0 left-0 z-40 hidden w-[255px] flex-col border-r border-[#e5e5e5] bg-[#fafafa] lg:flex">
        <div className="flex h-[81px] w-full items-center border-b border-[#e5e5e5] px-2">
          <Link href="/funding/dashboard" className="flex h-6 items-center gap-[9.5px] px-[4.5px]">
            <Image
              src="/product-icons/synqlien.png"
              alt=""
              width={18}
              height={18}
              priority
              unoptimized
              className="h-[18px] w-[18px]"
            />
            <span className="text-[15px] font-bold leading-5 text-[#0a0a0a]">
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
                  className={cn(
                    "flex h-8 w-[239px] items-center gap-2 rounded-[8px] px-2 text-[14px] font-normal leading-[1.6] transition-colors",
                    active
                      ? "bg-[rgba(238,113,50,0.05)] text-[#ee7132]"
                      : "text-[#0a0a0a] hover:bg-white hover:text-[#ee7132]",
                  )}
                >
                  <i className={`${item.icon} text-[16px]`} />
                  <span className="truncate">{item.label}</span>
                </Link>
              );
            })}
          </div>
        </nav>
      </aside>

      <div className="lg:pl-[255px]">
        <header className="sticky top-0 z-30 border-b border-[#e5e5e5] bg-white">
          <div className="flex min-h-[81px] items-center justify-between gap-3 px-4 py-5 sm:px-6">
            <div className="flex min-w-0 flex-1 items-center gap-2">
              <Link href="/funding/dashboard" className="flex shrink-0 items-center gap-2 lg:hidden">
                <Image
                  src="/product-icons/synqlien.png"
                  alt=""
                  width={22}
                  height={22}
                  priority
                  unoptimized
                  className="h-[22px] w-[22px]"
                />
                <span className="text-sm font-bold text-[#0a0a0a]">
                  Synq<span className="text-[#ee7132]">Lien</span>
                </span>
              </Link>
              <div className="hidden h-7 w-7 items-center justify-center lg:flex">
                <i className="ri-sidebar-unfold-line text-[16px] text-[#0a0a0a]" />
              </div>
              <span className="hidden h-[17px] w-px bg-[#e5e5e5] lg:block" />
              <p className="hidden truncate text-[14px] font-normal leading-[1.6] text-[#0a0a0a] lg:block">
                {currentNavItem.label}
              </p>
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
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#fdf1eb] text-center text-[18px] font-medium leading-[1.6] text-[#a95024]">
                {initials}
              </div>
              <div className="hidden min-w-0 flex-col items-start leading-[1.6] text-[#0a0a0a] sm:flex">
                <p className="max-w-[220px] truncate text-[14px] font-bold">
                  {orgName}
                </p>
                <p className="text-[12px] font-normal text-[#525252]">Funding Company</p>
              </div>
              <button
                type="button"
                onClick={handleSignOut}
                aria-label="Sign out"
                title="Sign out"
                className="hidden h-7 w-7 items-center justify-center rounded-[8px] text-[#525252] transition-colors hover:bg-[#f5f5f5] hover:text-[#0a0a0a] sm:flex"
              >
                <i className="ri-logout-box-r-line text-[16px]" />
              </button>
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
