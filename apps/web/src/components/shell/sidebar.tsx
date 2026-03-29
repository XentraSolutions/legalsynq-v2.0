'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useState, useEffect } from 'react';
import { useProduct } from '@/contexts/product-context';
import type { NavGroup, NavItem } from '@/types';
import { clsx } from 'clsx';

const STORAGE_KEY = 'ls_sidebar_collapsed';

/**
 * Persistent collapsible sidebar.
 *
 * - Always renders (never returns null) so the layout column is stable.
 * - Always shows ALL available nav groups regardless of the active route.
 * - Active item highlighted across all groups.
 * - Expanded (220px): icons + labels + group headers.
 * - Collapsed (52px): icons only, centered, with native title tooltips.
 * - Collapse state persisted in localStorage.
 */
export function Sidebar() {
  const pathname        = usePathname();
  const { availableGroups } = useProduct();

  const [collapsed, setCollapsed] = useState(false);
  const [mounted,   setMounted]   = useState(false);

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

  const width = !mounted ? 220 : collapsed ? 52 : 220;

  return (
    <aside
      className="shrink-0 flex flex-col bg-white border-r border-gray-200 overflow-hidden"
      style={{
        width,
        transition: mounted ? 'width 200ms ease' : undefined,
        /* Must fill the parent flex row height */
        alignSelf: 'stretch',
      }}
    >
      {/* ── Header (expanded only) ───────────────────────────────────────── */}
      <div
        className="shrink-0 overflow-hidden"
        style={{ height: collapsed ? 12 : undefined, transition: 'height 200ms ease' }}
      >
        {!collapsed && (
          <div className="px-5 pt-5 pb-3">
            <p className="text-[10px] font-semibold uppercase tracking-widest text-gray-400 mb-0.5">
              Navigation
            </p>
          </div>
        )}
      </div>

      {/* ── Nav groups ──────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto overflow-x-hidden">
        {availableGroups.length === 0 ? (
          /* Loading / no-groups skeleton */
          <div className={clsx('space-y-1 py-2', collapsed ? 'px-1.5' : 'px-3')}>
            {[...Array(4)].map((_, i) => (
              <div
                key={i}
                className={clsx(
                  'rounded-lg bg-gray-100 animate-pulse',
                  collapsed ? 'w-8 h-8 mx-auto' : 'h-9',
                )}
              />
            ))}
          </div>
        ) : (
          <div className={clsx('py-2', collapsed ? '' : 'px-3 space-y-5')}>
            {availableGroups.map(group => (
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

      {/* ── Collapse / expand toggle ─────────────────────────────────────── */}
      <div
        className={clsx(
          'shrink-0 border-t border-gray-100 py-2',
          collapsed ? 'flex justify-center' : 'px-3',
        )}
      >
        <button
          onClick={toggle}
          title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          className={clsx(
            'flex items-center gap-2 rounded-lg text-[12px] font-medium text-gray-400',
            'hover:bg-gray-100 hover:text-gray-700 transition-colors',
            collapsed ? 'w-8 h-8 justify-center' : 'w-full px-3 py-2',
          )}
        >
          <i
            className={clsx(
              'text-[16px] leading-none shrink-0',
              collapsed ? 'ri-sidebar-unfold-line' : 'ri-sidebar-fold-line',
            )}
          />
          {!collapsed && <span>Collapse</span>}
        </button>
      </div>
    </aside>
  );
}

// ── Group section ─────────────────────────────────────────────────────────────

function NavGroupSection({
  group, pathname, collapsed,
}: { group: NavGroup; pathname: string; collapsed: boolean }) {
  return (
    <div className={collapsed ? 'py-1' : ''}>
      {/* Group label (expanded only) */}
      {!collapsed && (
        <div className="flex items-center gap-1.5 px-3 mb-1">
          {group.icon && (
            <i className={`${group.icon} text-[12px] text-gray-400`} />
          )}
          <p className="text-[10px] font-semibold uppercase tracking-wider text-gray-400">
            {group.label}
          </p>
        </div>
      )}

      {/* Divider between groups when collapsed */}
      {collapsed && (
        <div className="mx-2 mb-1.5 border-t border-gray-100" />
      )}

      <nav className={clsx('space-y-0.5', collapsed ? 'px-1.5' : '')}>
        {group.items.map(item => (
          <SidebarItem
            key={item.href}
            item={item}
            pathname={pathname}
            collapsed={collapsed}
          />
        ))}
      </nav>
    </div>
  );
}

// ── Nav item ──────────────────────────────────────────────────────────────────

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
      {/* Expanded: left accent bar */}
      {isActive && !collapsed && (
        <span className="absolute left-0 top-1.5 bottom-1.5 w-0.5 rounded-full bg-orange-500" />
      )}

      {/* Collapsed: right pip */}
      {isActive && collapsed && (
        <span className="absolute -right-0.5 top-1/2 -translate-y-1/2 w-1 h-4 rounded-full bg-orange-500" />
      )}

      {item.icon ? (
        <i
          className={`${item.icon} text-[16px] leading-none shrink-0`}
          style={{ color: isActive ? '#f97316' : undefined }}
        />
      ) : (
        <span className="w-1.5 h-1.5 rounded-full bg-current opacity-50" />
      )}

      {!collapsed && <span>{item.label}</span>}
    </Link>
  );
}
