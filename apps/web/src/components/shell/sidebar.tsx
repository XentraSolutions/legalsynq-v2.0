'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { usePathname } from 'next/navigation';
import { useState, useEffect } from 'react';
import { useProduct } from '@/contexts/product-context';
import { PRODUCT_NAV, PRODUCT_META } from '@/lib/nav';
import type { NavItem } from '@/types';
import { clsx } from 'clsx';

const STORAGE_KEY = 'ls_sidebar_collapsed';

// Product order + accent colours
const PRODUCTS = [
  { id: 'careconnect', bg: '#eff6ff' },
  { id: 'fund',        bg: '#f0fdf4' },
  { id: 'lien',        bg: '#f5f3ff' },
  { id: 'ai',          bg: '#fffbeb' },
  { id: 'insights',    bg: '#ecfeff' },
] as const;

/**
 * Sidebar — two-level navigation.
 *
 * Expanded (240px):
 *   • All products listed as rows (coloured icon + label + chevron).
 *   • The active product expands inline to reveal its sub-nav items.
 *
 * Collapsed (52px):
 *   • Only coloured icon tiles; active product has an orange right pip.
 *   • Sub-items are hidden (tooltip on icon tile shows product name).
 */
export function Sidebar() {
  const pathname                              = usePathname();
  const { selectedProductId, setSelectedProductId } = useProduct();
  const router                               = useRouter();

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

  const width = !mounted ? 240 : collapsed ? 52 : 240;

  function handleProductClick(id: string) {
    setSelectedProductId(id);
    const firstHref = PRODUCT_NAV[id]?.[0]?.href;
    if (firstHref) router.push(firstHref);
  }

  return (
    <aside
      className="shrink-0 flex flex-col bg-white border-r border-gray-200 overflow-hidden"
      style={{
        width,
        transition: mounted ? 'width 200ms ease' : undefined,
        alignSelf: 'stretch',
      }}
    >
      {/* ── Header / collapse toggle ─────────────────────────────────────── */}
      <div
        className={clsx(
          'shrink-0 flex items-center border-b border-gray-100 h-12',
          collapsed ? 'justify-center' : 'justify-between px-4',
        )}
      >
        {!collapsed && (
          <span className="text-[11px] font-semibold uppercase tracking-widest text-gray-400 select-none">
            Apps
          </span>
        )}
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

      {/* ── Product list ────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto overflow-x-hidden py-2">
        {PRODUCTS.map(({ id, bg }) => {
          const meta      = PRODUCT_META[id];
          const navItems  = PRODUCT_NAV[id] ?? [];
          const isOpen    = selectedProductId === id;

          if (!meta) return null;

          return (
            <div key={id}>
              {/* ── Product row ─────────────────────────────────────── */}
              <button
                onClick={() => handleProductClick(id)}
                title={collapsed ? meta.label : undefined}
                className={clsx(
                  'w-full flex items-center transition-colors',
                  collapsed
                    ? 'justify-center h-10 mx-auto'
                    : 'gap-3 px-3 py-2.5',
                  isOpen && !collapsed
                    ? 'bg-gray-50'
                    : 'hover:bg-gray-50',
                )}
              >
                {/* Coloured icon tile */}
                <div
                  className={clsx(
                    'flex items-center justify-center rounded-lg shrink-0 relative',
                    collapsed ? 'w-8 h-8' : 'w-7 h-7',
                  )}
                  style={{ backgroundColor: bg }}
                >
                  <i
                    className={`${meta.icon} text-[15px] leading-none`}
                    style={{ color: meta.color }}
                  />
                  {/* Collapsed active pip */}
                  {isOpen && collapsed && (
                    <span className="absolute -right-1 top-1/2 -translate-y-1/2 w-1 h-4 rounded-full bg-orange-500" />
                  )}
                </div>

                {/* Label + chevron (expanded only) */}
                {!collapsed && (
                  <>
                    <span className={clsx(
                      'flex-1 text-left text-[12px] font-medium truncate',
                      isOpen ? 'text-gray-900' : 'text-gray-600',
                    )}>
                      {meta.label}
                    </span>
                    <i className={clsx(
                      'text-[14px] text-gray-300 shrink-0 transition-transform duration-200',
                      isOpen ? 'ri-arrow-down-s-line' : 'ri-arrow-right-s-line',
                    )} />
                  </>
                )}
              </button>

              {/* ── Sub-nav items (expanded product, expanded sidebar) ── */}
              {isOpen && !collapsed && navItems.length > 0 && (
                <div className="pb-1">
                  {navItems.map(item => (
                    <SubItem
                      key={item.href}
                      item={item}
                      pathname={pathname}
                      accentColor={meta.color}
                    />
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </aside>
  );
}

// ── Sub-navigation item ───────────────────────────────────────────────────────

function SubItem({
  item, pathname, accentColor,
}: { item: NavItem; pathname: string; accentColor: string }) {
  const isActive = pathname === item.href || pathname.startsWith(item.href + '/');

  return (
    <Link
      href={item.href}
      className={clsx(
        'relative flex items-center gap-2.5 pl-[46px] pr-3 py-2 text-[12px] font-medium transition-colors',
        isActive
          ? 'text-gray-900 bg-orange-50'
          : 'text-gray-500 hover:bg-gray-50 hover:text-gray-800',
      )}
    >
      {/* Left accent bar */}
      {isActive && (
        <span
          className="absolute left-0 top-1.5 bottom-1.5 w-0.5 rounded-full"
          style={{ backgroundColor: accentColor }}
        />
      )}

      {item.icon && (
        <i
          className={`${item.icon} text-[14px] leading-none shrink-0`}
          style={{ color: isActive ? accentColor : undefined }}
        />
      )}
      <span>{item.label}</span>
    </Link>
  );
}
