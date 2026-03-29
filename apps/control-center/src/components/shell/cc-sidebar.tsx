'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { buildCCNav } from '@/lib/nav';
import type { NavGroup, NavItem } from '@/types';
import { clsx } from 'clsx';

/**
 * Control Center sidebar — Client Component for active-link highlighting.
 * Nav structure is built from lib/nav.ts (no cross-app imports).
 */
export function CCSidebar() {
  const pathname = usePathname();
  const groups   = buildCCNav();

  return (
    <aside className="w-56 shrink-0 border-r border-gray-200 bg-white flex flex-col h-full overflow-y-auto">
      <nav className="flex-1 py-4 space-y-6 px-2">
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
    <li>
      <Link
        href={item.href}
        className={clsx(
          'flex items-center gap-2 px-3 py-2 rounded-md text-sm font-medium transition-colors',
          isActive
            ? 'bg-indigo-50 text-indigo-700'
            : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900',
        )}
      >
        {item.label}
      </Link>
    </li>
  );
}
