'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useState, useEffect } from 'react';
import { useProduct } from '@/contexts/product-context';
import type { NavGroup, NavItem } from '@/types';
import { clsx } from 'clsx';

const STORAGE_KEY = 'ls_sidebar_collapsed';

/**
 * Collapsible sidebar.
 *
 * Expanded (220px): icons + labels + group headers.
 * Collapsed (48px): icons only, centered, with title tooltips.
 *
 * Collapse state is persisted in localStorage.
 *
 * When a product is active (/careconnect, /fund, etc.) shows only that group.
 * When no product is active (/dashboard) shows all groups.
 */
export function Sidebar() {
  const pathname = usePathname();
  const { activeGroup, availableGroups } = useProduct();

  const [collapsed, setCollapsed] = useState(false);
  const [mounted, setMounted] = useState(false);

  // Hydrate from localStorage after mount to avoid SSR mismatch
  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'true') setCollapsed(true);
    setMounted(true);
  }, []);

  function toggle() {
    setCollapsed(prev => {
      const next = !prev;
      localStorage.setItem(STORAGE_KEY, String(next));
      return next;
    });
  }

  if (availableGroups.length === 0) return null;

  // Decide which groups to show
  const groups: NavGroup[] = activeGroup ? [activeGroup] : availableGroups;
  const headerLabel = activeGroup ? activeGroup.label : 'All Products';

  // Suppress width flash before localStorage is read
  const width = !mounted ? 220 : collapsed ? 48 : 220;

  return (
    <aside
      className="shrink-0 flex flex-col h-full bg-white border-r border-gray-200 overflow-hidden"
      style={{ width, transition: mounted ? 'width 200ms ease' : undefined }}
    >
      {/* ── Header ──────────────────────────────────────────────────────────── */}
      {!collapsed && (
        <div className="px-5 pt-5 pb-3 shrink-0">
          <p className="text-[10px] font-semibold uppercase tracking-widest text-gray-400 mb-1">
            Navigation
          </p>
          <p className="text-sm font-bold text-[#0f1928] truncate">{headerLabel}</p>
        </div>
      )}

      {/* Spacer when collapsed so toggle sits at the same vertical rhythm */}
      {collapsed && <div className="h-[52px] shrink-0" />}

      {/* ── Nav items ───────────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto overflow-x-hidden pb-2">
        {groups.length === 1 || activeGroup ? (
          // Single-group view (inside a product or only one product available)
          <nav className={clsx('space-y-0.5', collapsed ? 'px-1.5 pt-1' : 'px-3')}>
            {groups[0]?.items.map(item => (
              <SidebarItem key={item.href} item={item} pathname={pathname} collapsed={collapsed} />
            ))}
          </nav>
        ) : (
          // Multi-group view (dashboard — all products)
          <div className={clsx('space-y-4', collapsed ? 'pt-1' : 'px-3')}>
            {groups.map(group => (
              <NavGroupSection
                key={group.id}
                group={group}
                pathname={pathname}
                collapsed={collapsed}
              />
            ))}
          </div>
        )}
      </div>

      {/* ── Collapse toggle ─────────────────────────────────────────────────── */}
      <div className={clsx('shrink-0 border-t border-gray-100 py-2', collapsed ? 'flex justify-center' : 'px-3')}>
        <button
          onClick={toggle}
          title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          className={clsx(
            'flex items-center gap-2 rounded-lg text-[12px] font-medium text-gray-400',
            'hover:bg-gray-100 hover:text-gray-700 transition-colors',
            collapsed ? 'w-8 h-8 justify-center' : 'w-full px-3 py-2',
          )}
        >
          <i className={clsx('text-[16px] leading-none shrink-0 transition-transform duration-200',
            collapsed ? 'ri-sidebar-unfold-line' : 'ri-sidebar-fold-line',
          )} />
          {!collapsed && <span>Collapse</span>}
        </button>
      </div>
    </aside>
  );
}

// ── Grouped section (multi-group / dashboard view) ─────────────────────────

function NavGroupSection({
  group, pathname, collapsed,
}: { group: NavGroup; pathname: string; collapsed: boolean }) {
  return (
    <div>
      {!collapsed && (
        <div className="flex items-center gap-1.5 px-3 mb-1">
          {group.icon && <i className={`${group.icon} text-[13px] text-gray-400`} />}
          <p className="text-[10px] font-semibold uppercase tracking-wider text-gray-400">
            {group.label}
          </p>
        </div>
      )}
      {/* Collapsed: just a tiny divider between groups */}
      {collapsed && <div className="mx-2 border-t border-gray-100 mb-1" />}
      <nav className={clsx('space-y-0.5', collapsed ? 'px-1.5' : '')}>
        {group.items.map(item => (
          <SidebarItem key={item.href} item={item} pathname={pathname} collapsed={collapsed} />
        ))}
      </nav>
    </div>
  );
}

// ── Individual nav item ────────────────────────────────────────────────────

function SidebarItem({
  item, pathname, collapsed,
}: { item: NavItem; pathname: string; collapsed: boolean }) {
  const isActive = pathname === item.href || pathname.startsWith(item.href + '/');

  return (
    <Link
      href={item.href}
      title={collapsed ? item.label : undefined}
      className={clsx(
        'relative flex items-center rounded-lg text-[12px] font-medium transition-colors',
        collapsed ? 'w-8 h-8 justify-center mx-auto' : 'gap-2.5 px-3 py-2.5',
        isActive
          ? 'bg-orange-50 text-[#0f1928]'
          : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
      )}
    >
      {/* Orange left accent bar — only when expanded */}
      {isActive && !collapsed && (
        <span className="absolute left-0 top-1.5 bottom-1.5 w-0.5 rounded-full bg-orange-500" />
      )}

      {/* Active dot indicator when collapsed */}
      {isActive && collapsed && (
        <span className="absolute -right-0.5 top-1/2 -translate-y-1/2 w-1 h-4 rounded-full bg-orange-500" />
      )}

      {item.icon ? (
        <i
          className={`${item.icon} text-[16px] leading-none shrink-0`}
          style={{ color: isActive ? '#f97316' : undefined }}
        />
      ) : (
        /* Fallback dot when no icon */
        <span className="w-1.5 h-1.5 rounded-full bg-current opacity-50" />
      )}

      {!collapsed && <span>{item.label}</span>}
    </Link>
  );
}
