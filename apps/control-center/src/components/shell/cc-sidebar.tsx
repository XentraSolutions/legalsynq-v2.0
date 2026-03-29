'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { buildCCNav } from '@/lib/nav';
import type { NavGroup, NavItem } from '@/types';
import { clsx } from 'clsx';

/**
 * Control Center sidebar.
 * White bg, orange active accent bar — matches the web app sidebar style.
 */
export function CCSidebar() {
  const pathname = usePathname();
  const groups   = buildCCNav();

  return (
    <aside className="w-56 shrink-0 border-r border-gray-200 bg-white flex flex-col h-full overflow-y-auto">
      {/* Product name header */}
      <div className="px-4 py-3 border-b border-gray-100">
        <p className="text-[11px] font-semibold uppercase tracking-wider text-gray-400">
          Admin Portal
        </p>
      </div>

      <nav className="flex-1 py-3 space-y-5 px-2">
        {groups.map(group => (
          <NavGroupSection key={group.id} group={group} pathname={pathname} />
        ))}
      </nav>
    </aside>
  );
}

function NavGroupSection({ group, pathname }: { group: NavGroup; pathname: string }) {
  return (
    <div>
      <p className="px-3 mb-1 text-[11px] font-semibold uppercase tracking-wider text-gray-400">
        {group.label}
      </p>
      <ul className="space-y-0.5">
        {group.items.map(item => (
          <NavItemLink key={item.href} item={item} pathname={pathname} />
        ))}
      </ul>
    </div>
  );
}

function NavItemLink({ item, pathname }: { item: NavItem; pathname: string }) {
  const isActive = pathname === item.href || pathname.startsWith(item.href + '/');
  return (
    <li className="relative">
      {/* Orange left accent bar */}
      {isActive && (
        <span className="absolute left-0 top-1 bottom-1 w-0.5 rounded-full bg-orange-500" />
      )}
      <Link
        href={item.href}
        className={clsx(
          'flex items-center gap-2.5 px-3 py-2.5 rounded-lg text-[12px] font-medium transition-colors',
          isActive
            ? 'bg-orange-50 text-[#0f1928]'
            : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
        )}
      >
        {item.riIcon && (
          <i className={clsx(item.riIcon, 'text-[16px] leading-none shrink-0',
            isActive ? 'text-orange-500' : 'text-gray-400',
          )} />
        )}
        <span>{item.label}</span>
      </Link>
    </li>
  );
}
