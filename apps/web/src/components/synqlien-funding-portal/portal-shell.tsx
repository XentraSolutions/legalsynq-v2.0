"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { PlatformSession } from "@/types";
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
  const orgName = session.orgName || "Funding portal";
  const initials = buildInitials(session.email);

  async function handleSignOut() {
    await fetch("/api/auth/logout", { method: "POST" }).catch(() => null);
    window.location.href = "/login";
  }

  return (
    <div className="min-h-screen bg-[#f5f6f8] text-slate-950">
      <aside className="fixed inset-y-0 left-0 z-40 hidden w-[244px] flex-col border-r border-slate-200 bg-white lg:flex">
        <div className="flex h-16 items-center gap-3 border-b border-slate-100 px-5">
          <Image
            src="/product-icons/synqlien.png"
            alt=""
            width={36}
            height={36}
            priority
            unoptimized
            className="h-9 w-9 rounded-lg"
          />
          <div className="min-w-0">
            <p className="truncate text-[15px] font-semibold tracking-tight text-slate-950">
              SynqLien
            </p>
            <p className="truncate text-[11px] font-medium uppercase tracking-[0.12em] text-slate-400">
              Funding Portal
            </p>
          </div>
        </div>

        <nav className="flex-1 px-3 py-4">
          <div className="space-y-1">
            {NAV_ITEMS.map(item => {
              const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={cn(
                    "flex h-10 items-center gap-3 rounded-md px-3 text-sm font-medium transition-colors",
                    active
                      ? "bg-orange-50 text-orange-700"
                      : "text-slate-600 hover:bg-slate-50 hover:text-slate-950",
                  )}
                >
                  <i className={`${item.icon} text-[17px]`} />
                  <span>{item.label}</span>
                </Link>
              );
            })}
          </div>
        </nav>

        <div className="border-t border-slate-100 px-4 py-4">
          <p className="truncate text-xs font-medium text-slate-500">{orgName}</p>
          <button
            type="button"
            onClick={handleSignOut}
            className="mt-2 inline-flex items-center gap-1.5 text-xs font-medium text-slate-500 transition-colors hover:text-slate-900"
          >
            <i className="ri-logout-box-r-line text-[14px]" />
            Sign out
          </button>
        </div>
      </aside>

      <div className="lg:pl-[244px]">
        <header className="sticky top-0 z-30 border-b border-slate-200 bg-white/95 backdrop-blur">
          <div className="flex min-h-16 items-center justify-between gap-3 px-4 py-3 sm:px-6 lg:px-8">
            <div className="flex min-w-0 items-center gap-3">
              <Link href="/funding/dashboard" className="flex items-center gap-2 lg:hidden">
                <Image
                  src="/product-icons/synqlien.png"
                  alt=""
                  width={32}
                  height={32}
                  priority
                  unoptimized
                  className="h-8 w-8 rounded-lg"
                />
                <span className="text-sm font-semibold text-slate-950">SynqLien</span>
              </Link>
              <div className="hidden min-w-0 lg:block">
                <p className="truncate text-xs font-medium uppercase tracking-[0.14em] text-slate-400">
                  Funding Common Portal
                </p>
                <p className="truncate text-sm font-medium text-slate-700">{orgName}</p>
              </div>
            </div>

            <nav className="flex items-center gap-1 lg:hidden">
              {NAV_ITEMS.map(item => {
                const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    aria-label={item.label}
                    className={cn(
                      "flex h-9 w-9 items-center justify-center rounded-md transition-colors",
                      active
                        ? "bg-orange-50 text-orange-700"
                        : "text-slate-500 hover:bg-slate-50 hover:text-slate-950",
                    )}
                    title={item.label}
                  >
                    <i className={`${item.icon} text-[18px]`} />
                  </Link>
                );
              })}
            </nav>

            <div className="flex items-center gap-3">
              <Link
                href="/funding/offered-liens?status=Pending"
                aria-label="Offer inbox"
                title="Offer inbox"
                className="hidden h-9 w-9 items-center justify-center rounded-md text-slate-500 transition-colors hover:bg-slate-50 hover:text-slate-950 sm:flex"
              >
                <i className="ri-inbox-2-line text-[18px]" />
              </Link>
              <div className="hidden min-w-0 text-right sm:block">
                <p className="max-w-[220px] truncate text-xs font-medium text-slate-700">
                  {session.email}
                </p>
                <p className="text-[11px] text-slate-400">Buyer access</p>
              </div>
              <div className="flex h-9 w-9 items-center justify-center rounded-full bg-slate-900 text-xs font-semibold text-white">
                {initials}
              </div>
            </div>
          </div>
        </header>

        <main className="px-4 py-5 sm:px-6 lg:px-8 lg:py-7">
          {children}
        </main>
      </div>
    </div>
  );
}

function buildInitials(email: string): string {
  const local = email.split("@")[0] ?? "";
  const parts = local.split(/[._-]/).filter(Boolean);
  if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  return (local.slice(0, 2) || "SL").toUpperCase();
}
