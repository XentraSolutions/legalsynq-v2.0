'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useState, useEffect } from 'react';
import { useProduct } from '@/contexts/product-context';
import { PRODUCT_NAV, PRODUCT_META } from '@/lib/nav';
import type { NavItem } from '@/types';
import { clsx } from 'clsx';

const STORAGE_KEY = 'ls_sidebar_collapsed';

/**
 * Collapsible sidebar.
 *
 * Shows only the nav items for the product selected in the top-bar app switcher.
 * When no product is selected (/dashboard), the nav area is empty.
 *
 * Expanded (220px): product name header + icons + labels.
 * Collapsed (52px): icons only with title tooltips + orange right pip for active item.
 * Collapse state persisted in localStorage. Keyboard shortcut: Ctrl+[
 */
export function Sidebar() {
  const pathname               = usePathname();
  const { selectedProductId }  = useProduct();

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

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === '[') { e.preventDefault(); toggle(); }
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const width = !mounted ? 220 : collapsed ? 52 : 220;
  const items = selectedProductId ? (PRODUCT_NAV[selectedProductId] ?? []) : [];
  const meta  = selectedProductId ? PRODUCT_META[selectedProductId]        : null;

  return (
    <aside
      className="shrink-0 flex flex-col bg-white border-r border-gray-200 overflow-hidden"
      style={{
        width,
        transition: mounted ? 'width 200ms ease' : undefined,
        alignSelf: 'stretch',
      }}
    >
      {/* ── Header row ──────────────────────────────────────────────────── */}
      <div
        className={clsx(
          'shrink-0 flex items-center border-b border-gray-100 h-12',
          collapsed ? 'justify-center' : 'justify-between px-4',
        )}
      >
        {/* Product name + icon when a product is selected (expanded) */}
        {!collapsed && meta && (
          <div className="flex items-center gap-2 min-w-0">
            <i className={`${meta.icon} text-[15px]`} style={{ color: meta.color }} />
            <span className="text-[12px] font-semibold text-gray-700 truncate">
              {meta.label}
            </span>
          </div>
        )}

        {/* Generic label when no product is active */}
        {!collapsed && !meta && (
          <span className="text-[11px] font-semibold uppercase tracking-widest text-gray-400 select-none">
            Navigation
          </span>
        )}

        {/* Collapse / expand toggle */}
        <button
          onClick={toggle}
          title={collapsed ? 'Expand sidebar (Ctrl+[)' : 'Collapse sidebar (Ctrl+[)'}
          className="flex items-center justify-center rounded-md w-7 h-7 text-gray-400 hover:bg-gray-100 hover:text-gray-700 transition-colors shrink-0"
        >
          <i className={clsx(
            'text-[17px] leading-none',
            collapsed ? 'ri-sidebar-unfold-line' : 'ri-sidebar-fold-line',
          )} />
        </button>
      </div>

      {/* ── Nav items ───────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto overflow-x-hidden py-2">
        {items.length > 0 && (
          <nav className={clsx('space-y-0.5', collapsed ? 'px-1.5' : 'px-3')}>
            {items.map(item => (
              <SidebarItem
                key={item.href}
                item={item}
                pathname={pathname}
                collapsed={collapsed}
                accentColor={meta?.color ?? '#f97316'}
              />
            ))}
          </nav>
        )}
      </div>
    </aside>
  );
}

// ── Nav item ──────────────────────────────────────────────────────────────────

function SidebarItem({
  item, pathname, collapsed, accentColor,
}: { item: NavItem; pathname: string; collapsed: boolean; accentColor: string }) {
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
      {/* Expanded: left accent bar in product colour */}
      {isActive && !collapsed && (
        <span
          className="absolute left-0 top-1.5 bottom-1.5 w-0.5 rounded-full"
          style={{ backgroundColor: accentColor }}
        />
      )}

      {/* Collapsed: right orange pip */}
      {isActive && collapsed && (
        <span className="absolute -right-0.5 top-1/2 -translate-y-1/2 w-1 h-4 rounded-full bg-orange-500" />
      )}

      {item.icon ? (
        <i
          className={`${item.icon} text-[16px] leading-none shrink-0`}
          style={{ color: isActive ? accentColor : undefined }}
        />
      ) : (
        <span className="w-1.5 h-1.5 rounded-full bg-current opacity-50" />
      )}

      {!collapsed && <span>{item.label}</span>}
    </Link>
  );
}
