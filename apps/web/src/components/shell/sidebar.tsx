'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { Heart, Banknote, FileStack, Settings } from 'lucide-react';
import { useProduct } from '@/contexts/product-context';
import type { NavItem } from '@/types';
import type { LucideIcon } from 'lucide-react';
import { clsx } from 'clsx';

const PRODUCT_ICONS: Record<string, LucideIcon> = {
  careconnect: Heart,
  fund: Banknote,
  lien: FileStack,
  admin: Settings,
};

export function Sidebar() {
  const { activeGroup } = useProduct();
  const pathname = usePathname();

  if (!activeGroup) return <aside className="w-[220px] shrink-0 border-r border-gray-200 bg-white" />;

  const Icon = PRODUCT_ICONS[activeGroup.id];

  return (
    <aside className="w-[220px] shrink-0 border-r border-gray-200 bg-white flex flex-col h-full overflow-y-auto">

      {/* ── Product header ──────────────────────────────────────────────────── */}
      <div className="flex items-center gap-2.5 px-4 py-3.5 border-b border-gray-100">
        {Icon && (
          <div className="w-7 h-7 rounded-md bg-blue-50 flex items-center justify-center shrink-0">
            <Icon size={15} className="text-blue-600" strokeWidth={2} />
          </div>
        )}
        <span className="text-sm font-semibold text-gray-800">{activeGroup.label}</span>
      </div>

      {/* ── Nav items ───────────────────────────────────────────────────────── */}
      <nav className="flex-1 py-3 px-2 space-y-0.5">
        {activeGroup.items.map(item => (
          <SidebarItem key={item.href} item={item} pathname={pathname} />
        ))}
      </nav>
    </aside>
  );
}

function SidebarItem({ item, pathname }: { item: NavItem; pathname: string }) {
  const isActive = pathname === item.href || pathname.startsWith(item.href + '/');

  return (
    <Link
      href={item.href}
      className={clsx(
        'flex items-center gap-2 px-3 py-2 rounded-md text-sm font-medium transition-colors',
        isActive
          ? 'bg-blue-50 text-blue-700 border-l-2 border-blue-600 pl-[10px]'
          : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900 border-l-2 border-transparent pl-[10px]',
      )}
    >
      {item.label}
    </Link>
  );
}
