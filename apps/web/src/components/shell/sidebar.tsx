'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useProduct } from '@/contexts/product-context';
import type { NavGroup, NavItem } from '@/types';
import { clsx } from 'clsx';

/**
 * Light sidebar — product-specific navigation only.
 * Icons use Remix Icon (ri-* CSS classes).
 * Active item: orange left accent bar + light orange tint + navy text.
 *
 * When a specific product is active (user is inside /careconnect, /fund, etc.)
 * the sidebar shows only that product's nav items.
 * When no product is active (e.g. /dashboard) it shows all available groups.
 */
export function Sidebar() {
  const pathname = usePathname();
  const { activeGroup, availableGroups } = useProduct();

  if (availableGroups.length === 0) return null;

  if (activeGroup) {
    // Product-specific view: show only this product's items
    return (
      <aside className="w-[220px] shrink-0 flex flex-col h-full bg-white border-r border-gray-200 overflow-hidden">
        <div className="px-5 pt-5 pb-3 shrink-0">
          <p className="text-[10px] font-semibold uppercase tracking-widest text-gray-400 mb-1">
            Navigation
          </p>
          <p className="text-sm font-bold text-[#0f1928]">{activeGroup.label}</p>
        </div>
        <div className="flex-1 overflow-y-auto px-3 pb-4">
          <nav className="space-y-0.5">
            {activeGroup.items.map(item => (
              <SidebarItem key={item.href} item={item} pathname={pathname} />
            ))}
          </nav>
        </div>
      </aside>
    );
  }

  // No active product (dashboard / landing) — show all groups
  return (
    <aside className="w-[220px] shrink-0 flex flex-col h-full bg-white border-r border-gray-200 overflow-hidden">
      <div className="px-5 pt-5 pb-3 shrink-0">
        <p className="text-[10px] font-semibold uppercase tracking-widest text-gray-400 mb-1">
          Navigation
        </p>
        <p className="text-sm font-bold text-[#0f1928]">All Products</p>
      </div>
      <div className="flex-1 overflow-y-auto px-3 pb-4 space-y-4">
        {availableGroups.map(group => (
          <NavGroupSection key={group.id} group={group} pathname={pathname} />
        ))}
      </div>
    </aside>
  );
}

// ── Grouped nav (used on dashboard) ──────────────────────────────────────────

function NavGroupSection({ group, pathname }: { group: NavGroup; pathname: string }) {
  return (
    <div>
      <div className="flex items-center gap-1.5 px-3 mb-1">
        {group.icon && (
          <i className={`${group.icon} text-[13px] text-gray-400`} />
        )}
        <p className="text-[10px] font-semibold uppercase tracking-wider text-gray-400">
          {group.label}
        </p>
      </div>
      <nav className="space-y-0.5">
        {group.items.map(item => (
          <SidebarItem key={item.href} item={item} pathname={pathname} />
        ))}
      </nav>
    </div>
  );
}

// ── Nav item ──────────────────────────────────────────────────────────────────

function SidebarItem({ item, pathname }: { item: NavItem; pathname: string }) {
  const isActive = pathname === item.href || pathname.startsWith(item.href + '/');

  return (
    <Link
      href={item.href}
      className={clsx(
        'relative flex items-center gap-2.5 px-3 py-2.5 rounded-lg text-[12px] font-medium transition-colors',
        isActive
          ? 'bg-orange-50 text-[#0f1928]'
          : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
      )}
    >
      {/* Orange left accent bar */}
      {isActive && (
        <span className="absolute left-0 top-1.5 bottom-1.5 w-0.5 rounded-full bg-orange-500" />
      )}

      {item.icon && (
        <i
          className={`${item.icon} text-[16px] leading-none shrink-0`}
          style={{ color: isActive ? '#f97316' : undefined }}
        />
      )}
      <span>{item.label}</span>
    </Link>
  );
}
