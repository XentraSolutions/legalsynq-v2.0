'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useSession } from '@/hooks/use-session';
import { buildNavGroups } from '@/lib/nav';
import type { NavGroup, NavItem } from '@/types';
import { clsx } from 'clsx';

export function Sidebar() {
  const { session } = useSession();
  const pathname    = usePathname();
  const groups      = session ? buildNavGroups(session) : [];

  if (!session) return null;

  return (
    <aside className="w-56 shrink-0 border-r border-gray-200 bg-white flex flex-col h-full overflow-y-auto">
      <nav className="flex-1 py-4 space-y-6 px-2">
        {groups.map(group => (
          <SidebarGroup
            key={group.id}
            group={group}
            pathname={pathname}
          />
        ))}
      </nav>
    </aside>
  );
}

function SidebarGroup({ group, pathname }: { group: NavGroup; pathname: string }) {
  return (
    <div>
      <p className="px-3 mb-1 text-[11px] font-semibold uppercase tracking-wider text-gray-400">
        {group.label}
      </p>
      <ul className="space-y-0.5">
        {group.items.map(item => (
          <SidebarItem key={item.href} item={item} pathname={pathname} />
        ))}
      </ul>
    </div>
  );
}

function SidebarItem({ item, pathname }: { item: NavItem; pathname: string }) {
  const isActive = pathname === item.href || pathname.startsWith(item.href + '/');
  return (
    <li>
      <Link
        href={item.href}
        className={clsx(
          'flex items-center gap-2 px-3 py-2 rounded-md text-sm font-medium transition-colors',
          isActive
            ? 'bg-primary/10 text-primary'
            : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900',
        )}
      >
        {item.label}
      </Link>
    </li>
  );
}
