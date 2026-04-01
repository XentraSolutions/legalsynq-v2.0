'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useState, useEffect, useCallback } from 'react';
import { CC_NAV } from '@/lib/nav';
import { useSettings } from '@/contexts/settings-context';
import type { NavItem, NavSection } from '@/types';
import { clsx } from 'clsx';

const STORAGE_KEY          = 'ls_cc_sidebar_collapsed';
const SECTIONS_STORAGE_KEY = 'ls_cc_sidebar_sections';

/**
 * Control Center sidebar — matches the web app sidebar structure:
 *   - Collapsible (220 px ↔ 52 px) with toggle button + Ctrl+[ shortcut
 *   - NavSection[] with uppercase section headings that can be collapsed
 *   - Icon-only collapsed mode with right-side active pip
 *   - Active colour driven by AppSettings (orange by default)
 */
export function CCSidebar() {
  const pathname = usePathname();
  const settings = useSettings();
  const nav      = settings.appearance.nav;

  const [collapsed,         setCollapsed]         = useState(false);
  const [mounted,           setMounted]           = useState(false);
  const [collapsedSections, setCollapsedSections] = useState<Record<string, boolean>>({});

  useEffect(() => {
    const stored         = localStorage.getItem(STORAGE_KEY);
    const storedSections = localStorage.getItem(SECTIONS_STORAGE_KEY);
    if (stored === 'true')    setCollapsed(true);
    if (storedSections) {
      try { setCollapsedSections(JSON.parse(storedSections)); } catch { /* ignore */ }
    }
    setMounted(true);
  }, []);

  function toggle() {
    setCollapsed(prev => {
      const next = !prev;
      localStorage.setItem(STORAGE_KEY, String(next));
      return next;
    });
  }

  const toggleSection = useCallback((heading: string) => {
    setCollapsedSections(prev => {
      const next = { ...prev, [heading]: !prev[heading] };
      localStorage.setItem(SECTIONS_STORAGE_KEY, JSON.stringify(next));
      return next;
    });
  }, []);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === '[') { e.preventDefault(); toggle(); }
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const width = !mounted ? 220 : collapsed ? 52 : 220;

  return (
    <aside
      className="shrink-0 flex flex-col bg-white border-r border-gray-200 overflow-hidden"
      style={{
        width,
        transition: mounted ? 'width 200ms ease' : undefined,
        alignSelf: 'stretch',
      }}
    >
      {/* ── Header ─────────────────────────────────────────────────────────── */}
      <div className={clsx(
        'shrink-0 flex items-center border-b border-gray-100 h-12',
        collapsed ? 'justify-center' : 'justify-between px-4',
      )}>
        {!collapsed && (
          <div className="flex items-center gap-2 min-w-0">
            <i className="ri-shield-star-line text-[15px]" style={{ color: nav.activeColor }} />
            <span className="text-[12px] font-semibold text-gray-700 truncate">Control Center</span>
          </div>
        )}
        <button
          onClick={toggle}
          title={collapsed ? 'Expand sidebar (Ctrl+[)' : 'Collapse sidebar (Ctrl+[)'}
          className="flex items-center justify-center rounded-md w-7 h-7 text-gray-400 hover:bg-gray-100 hover:text-gray-700 transition-colors shrink-0"
        >
          <i className={clsx('text-[17px] leading-none',
            collapsed ? 'ri-sidebar-unfold-line' : 'ri-sidebar-fold-line',
          )} />
        </button>
      </div>

      {/* ── Nav sections ───────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto overflow-x-hidden py-2">
        {CC_NAV.map((section, si) => (
          <SidebarSection
            key={si}
            section={section}
            sectionIndex={si}
            pathname={pathname}
            sidebarCollapsed={collapsed}
            sectionCollapsed={!!collapsedSections[section.heading ?? si]}
            onToggleSection={() => toggleSection(section.heading ?? String(si))}
            activeColor={nav.activeColor}
            activeBg={nav.activeBg}
          />
        ))}
      </div>
    </aside>
  );
}

function SidebarSection({
  section,
  sectionIndex,
  pathname,
  sidebarCollapsed,
  sectionCollapsed,
  onToggleSection,
  activeColor,
  activeBg,
}: {
  section:          NavSection;
  sectionIndex:     number;
  pathname:         string;
  sidebarCollapsed: boolean;
  sectionCollapsed: boolean;
  onToggleSection:  () => void;
  activeColor:      string;
  activeBg:         string;
}) {
  const hasActive = section.items.some(
    item => pathname === item.href || pathname.startsWith(item.href + '/'),
  );

  return (
    <div className={sectionIndex > 0 ? 'mt-4' : ''}>
      {/* Section heading — expanded sidebar only */}
      {section.heading && !sidebarCollapsed && (
        <button
          onClick={onToggleSection}
          className="w-full flex items-center justify-between px-5 mb-1 group"
          title={sectionCollapsed ? `Expand ${section.heading}` : `Collapse ${section.heading}`}
        >
          <span className="text-[10px] font-semibold uppercase tracking-widest text-gray-400 select-none group-hover:text-gray-600 transition-colors">
            {section.heading}
          </span>
          <span className="flex items-center gap-1">
            {sectionCollapsed && hasActive && (
              <span
                className="w-1.5 h-1.5 rounded-full"
                style={{ backgroundColor: activeColor }}
                title="Active page in this section"
              />
            )}
            <i
              className={clsx(
                'text-[11px] text-gray-300 group-hover:text-gray-500 transition-all duration-200',
                sectionCollapsed ? 'ri-arrow-right-s-line' : 'ri-arrow-down-s-line',
              )}
            />
          </span>
        </button>
      )}

      {/* Thin divider between sections when sidebar is in icon-only mode */}
      {sectionIndex > 0 && sidebarCollapsed && (
        <div className="mx-2 mb-2 border-t border-gray-100" />
      )}

      {/* Items — hidden when section is collapsed (unless sidebar is in icon-only mode) */}
      {(!sectionCollapsed || sidebarCollapsed) && (
        <nav className={clsx('space-y-0.5', sidebarCollapsed ? 'px-1.5' : 'px-3')}>
          {section.items.map(item => (
            <SidebarItem
              key={item.href}
              item={item}
              pathname={pathname}
              collapsed={sidebarCollapsed}
              activeColor={activeColor}
              activeBg={activeBg}
            />
          ))}
        </nav>
      )}
    </div>
  );
}

function SidebarItem({
  item, pathname, collapsed, activeColor, activeBg,
}: {
  item:        NavItem;
  pathname:    string;
  collapsed:   boolean;
  activeColor: string;
  activeBg:    string;
}) {
  const isActive = pathname === item.href || pathname.startsWith(item.href + '/');

  return (
    <Link
      href={item.href}
      title={collapsed ? item.label : undefined}
      className={clsx(
        'relative flex items-center rounded-lg text-[12px] font-medium transition-colors',
        collapsed ? 'w-8 h-8 justify-center mx-auto' : 'gap-2.5 px-3 py-2.5',
        !isActive && 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
      )}
      style={isActive ? { backgroundColor: activeBg, color: '#0f1928' } : undefined}
    >
      {/* Left accent bar (expanded active) */}
      {isActive && !collapsed && (
        <span
          className="absolute left-0 top-1.5 bottom-1.5 w-0.5 rounded-full"
          style={{ backgroundColor: activeColor }}
        />
      )}
      {/* Right pip (collapsed active) */}
      {isActive && collapsed && (
        <span
          className="absolute -right-0.5 top-1/2 -translate-y-1/2 w-1 h-4 rounded-full"
          style={{ backgroundColor: activeColor }}
        />
      )}
      {item.icon
        ? <i
            className={`${item.icon} text-[16px] leading-none shrink-0`}
            style={{ color: isActive ? activeColor : undefined }}
          />
        : <span className="w-1.5 h-1.5 rounded-full bg-current opacity-50" />
      }
      {!collapsed && (
        <span className="flex-1 min-w-0 flex items-center gap-1.5">
          <span className="truncate">{item.label}</span>
          {item.badge && <NavBadge badge={item.badge} />}
        </span>
      )}
    </Link>
  );
}

function NavBadge({ badge }: { badge: NonNullable<NavItem['badge']> }) {
  const styles: Record<string, string> = {
    'LIVE':        'bg-emerald-100 text-emerald-700',
    'IN PROGRESS': 'bg-amber-100   text-amber-700',
    'MOCKUP':      'bg-gray-100    text-gray-500',
  };
  return (
    <span className={`shrink-0 text-[9px] font-semibold px-1.5 py-0.5 rounded-full leading-none ${styles[badge] ?? styles['MOCKUP']}`}>
      {badge}
    </span>
  );
}
