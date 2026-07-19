'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import type { ReactNode } from 'react';
import { clsx } from 'clsx';

const navItems = [
  { label: 'Dashboard', href: '/lien/dashboard', icon: 'ri-dashboard-line' },
  { label: 'Offered Liens', href: '/lien/liens', icon: 'ri-file-list-3-line' },
];

export function SynqLienPortalShell({ children }: { children: ReactNode }) {
  const pathname = usePathname() ?? '';
  const activeNavItem = navItems.find(
    (item) => pathname === item.href || pathname.startsWith(`${item.href}/`),
  );

  return (
    <div className="flex h-screen overflow-hidden bg-white text-neutral-950">
      <aside className="hidden w-[255px] shrink-0 flex-col border-r border-neutral-200 bg-[#fafafa] md:flex">
        <div className="flex h-[81px] shrink-0 items-center border-b border-neutral-200 px-4">
          <Link href="/lien/dashboard" className="flex items-center gap-2.5">
            <img
              src="/product-icons/synqlien.png"
              alt=""
              aria-hidden
              className="h-5 w-5 object-contain"
            />
            <span className="text-[15px] font-semibold leading-none text-neutral-950">
              Synq<span className="text-[#ee7132]">Lien</span>
            </span>
          </Link>
        </div>

        <nav className="flex flex-1 flex-col gap-1 px-2 py-4">
          {navItems.map((item) => {
            const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
            return (
              <Link
                key={item.href}
                href={item.href}
                className={clsx(
                  'flex h-8 items-center gap-2 rounded-lg px-2 text-sm transition-colors',
                  active
                    ? 'bg-[#ee7132]/5 text-[#ee7132]'
                    : 'text-neutral-900 hover:bg-neutral-100',
                )}
              >
                <i className={clsx(item.icon, 'text-base leading-none')} aria-hidden />
                <span className="truncate">{item.label}</span>
              </Link>
            );
          })}
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        <header className="flex h-[81px] shrink-0 items-center gap-3 border-b border-neutral-200 bg-white px-4 md:px-6">
          <Link href="/lien/dashboard" className="flex items-center gap-2 md:hidden">
            <img
              src="/product-icons/synqlien.png"
              alt=""
              aria-hidden
              className="h-5 w-5 object-contain"
            />
            <span className="text-sm font-semibold text-neutral-950">
              Synq<span className="text-[#ee7132]">Lien</span>
            </span>
          </Link>

          <div className="hidden items-center gap-2 text-sm text-neutral-950 md:flex">
            <button
              type="button"
              className="inline-flex h-7 w-7 items-center justify-center rounded-md text-neutral-500 hover:bg-neutral-100 hover:text-neutral-950"
              aria-label="Toggle sidebar"
            >
              <i className="ri-sidebar-fold-line text-base" aria-hidden />
            </button>
            <span className="h-4 w-px bg-neutral-200" />
            <span>{activeNavItem?.label ?? 'Dashboard'}</span>
          </div>

          <div className="ml-auto flex items-center gap-3">
            <button
              type="button"
              className="inline-flex h-8 w-8 items-center justify-center rounded-md text-neutral-500 hover:bg-neutral-100 hover:text-neutral-950"
              aria-label="Notifications"
            >
              <i className="ri-notification-3-line text-base" aria-hidden />
            </button>

            <button
              type="button"
              className="flex items-center gap-2 rounded-lg px-1.5 py-1 text-left hover:bg-neutral-50"
              aria-label="Account menu"
            >
              <span className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-[#fdf1eb] text-lg font-medium text-[#a95024]">
                SF
              </span>
              <span className="hidden min-w-0 flex-col leading-tight sm:flex">
                <span className="truncate text-sm font-bold text-neutral-950">
                  Summit Funding Group
                </span>
                <span className="truncate text-xs text-neutral-600">Funding Company</span>
              </span>
              <i className="ri-arrow-down-s-line hidden text-base text-neutral-500 sm:block" aria-hidden />
            </button>
          </div>
        </header>

        <main className="flex-1 overflow-y-auto bg-white px-4 py-6 md:px-6">
          {children}
        </main>
      </div>
    </div>
  );
}
