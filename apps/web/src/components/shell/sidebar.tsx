"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState, useEffect } from "react";
import { useProduct } from "@/contexts/product-context";
import { useSettings } from "@/contexts/settings-context";
import {
  PRODUCT_NAV,
  PRODUCT_META,
  GLOBAL_BOTTOM_NAV,
  buildNavGroups,
  filterNavByAccess,
} from "@/lib/nav";
import { getClientPortalConfig, type PortalConfig } from "@/lib/portal";
import { useSession } from "@/hooks/use-session";
import { useNavBadges } from "@/hooks/use-nav-badges";
import { useProviderMode } from "@/hooks/use-provider-mode";
import { toast } from "sonner";
import type { NavItem } from "@/types";
import { clsx } from "clsx";

const STORAGE_KEY = "ls_sidebar_collapsed";

// Helper: Generate stable key for nav items (use href, fallback to index-based key)
function getNavItemKey(item: NavItem, index: number): string {
  return item.href ?? `grouping-${index}`;
}

// Helper: Check if a nav item is currently active
function isItemActive(item: NavItem, activeNavItem: NavItem | null): boolean {
  return item.href ? item.href === activeNavItem?.href : false;
}

export function Sidebar() {
  const pathname = usePathname();
  const { selectedProductId } = useProduct();
  const settings = useSettings();
  const nav = settings.appearance.nav;
  const { session } = useSession();
  const badges = useNavBadges();
  const { isSellMode } = useProviderMode();
  const adminSections = session ? buildNavGroups(session) : [];

  const [collapsed, setCollapsed] = useState(false);
  const [mounted, setMounted] = useState(false);
  const [portalConfig, setPortalConfig] = useState<PortalConfig | null>(null);

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === "true") setCollapsed(true);
    setMounted(true);
    setPortalConfig(getClientPortalConfig());
  }, []);

  function toggle() {
    setCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem(STORAGE_KEY, String(next));
      return next;
    });
  }

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === "[") {
        e.preventDefault();
        toggle();
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const width = !mounted ? 220 : collapsed ? 52 : 220;
  const rawSections = selectedProductId
    ? (PRODUCT_NAV[selectedProductId] ?? [])
    : [];
  const isProductPortal = portalConfig?.productId === selectedProductId;
  const sections = session
    ? filterNavByAccess(
        rawSections,
        session.productRoles,
        isSellMode,
        session.orgType,
        session.isTenantAdmin,
        isProductPortal,
        session.isPlatformAdmin || session.isTenantAdmin,
      )
    : rawSections;
  const meta = selectedProductId ? PRODUCT_META[selectedProductId] : null;

  const currentPathname = pathname ?? "";

  const allNavItems = [
    ...sections.flatMap((s) =>
      s.items.flatMap((item) => [item, ...(item.children ?? [])]),
    ),
    ...adminSections.flatMap((s) => s.items),
    ...GLOBAL_BOTTOM_NAV.items,
  ];

  const activeNavItem =
    allNavItems.find((item) => currentPathname === item.href) ?? null;

  const bottomNavItems = GLOBAL_BOTTOM_NAV.items
    .filter(
      (item) => !(selectedProductId === "lien" && item.href === "/my-work"),
    )
    .filter(
      (item) =>
        !item.adminOnly || session?.isPlatformAdmin || session?.isTenantAdmin,
    );

  return (
    <aside
      className="shrink-0 flex flex-col bg-white border-r border-gray-200 overflow-hidden"
      style={{
        width,
        transition: mounted ? "width 200ms ease" : undefined,
        alignSelf: "stretch",
      }}
    >
      {/* ── Header ────────────────────────────────────────────────────────── */}
      <div
        className={clsx(
          "shrink-0 flex items-center border-b border-gray-100 h-12",
          collapsed ? "justify-center" : "justify-between px-4",
        )}
      >
        {!collapsed && meta && (
          <div className="flex items-center gap-2 min-w-0">
            {meta.iconSrc ? (
              <img
                src={meta.iconSrc}
                alt=""
                aria-hidden
                className="w-4 h-4 shrink-0 object-contain"
              />
            ) : (
              <i
                className={`${meta.icon} text-[15px]`}
                style={{ color: nav.activeColor }}
              />
            )}
            <span className="text-[12px] font-semibold text-gray-700 truncate">
              {meta.label}
            </span>
          </div>
        )}
        {!collapsed && !meta && (
          <span className="text-[11px] font-semibold uppercase tracking-widest text-gray-400 select-none">
            Navigation
          </span>
        )}
        <button
          onClick={toggle}
          title={
            collapsed ? "Expand sidebar (Ctrl+[)" : "Collapse sidebar (Ctrl+[)"
          }
          className="flex items-center justify-center rounded-md w-7 h-7 text-gray-400 hover:bg-gray-100 hover:text-gray-700 transition-colors shrink-0"
        >
          <i
            className={clsx(
              "text-[17px] leading-none",
              collapsed ? "ri-sidebar-unfold-line" : "ri-sidebar-fold-line",
            )}
          />
        </button>
      </div>

      {/* ── Nav sections ──────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto overflow-x-hidden py-2">
        {sections.map((section, si) => (
          <div key={si} className={si > 0 ? "mt-4" : ""}>
            {/* Section heading (expanded only) */}
            {section.heading && !collapsed && (
              <p className="px-5 mb-1 text-[10px] font-semibold uppercase tracking-widest text-gray-400 select-none">
                {section.heading}
              </p>
            )}
            {/* Divider between sections when collapsed */}
            {si > 0 && collapsed && (
              <div className="mx-2 mb-2 border-t border-gray-100" />
            )}
            <nav className={clsx("space-y-0.5", collapsed ? "px-1.5" : "px-3")}>
              {section.items.map((item, itemIdx) =>
                item.children?.length ? (
                  <SidebarDropdownItem
                    key={getNavItemKey(item, itemIdx)}
                    item={item}
                    pathname={currentPathname}
                    collapsed={collapsed}
                    activeColor={nav.activeColor}
                    activeBg={nav.activeBg}
                    badges={badges}
                    activeNavItem={activeNavItem}
                    isActive={isItemActive(item, activeNavItem)}
                  />
                ) : (
                  <SidebarItem
                    key={getNavItemKey(item, itemIdx)}
                    item={item}
                    pathname={currentPathname}
                    collapsed={collapsed}
                    activeColor={nav.activeColor}
                    activeBg={nav.activeBg}
                    badgeCount={
                      item.badgeKey ? badges[item.badgeKey] : undefined
                    }
                    isActive={isItemActive(item, activeNavItem)}
                  />
                ),
              )}
            </nav>
          </div>
        ))}

        {/* ── Administration sections (PlatformAdmin / TenantAdmin only) ──── */}
        {adminSections.map((section, si) => (
          <div
            key={`admin-${si}`}
            className={sections.length > 0 || si > 0 ? "mt-4" : ""}
          >
            {section.heading && !collapsed && (
              <p className="px-5 mb-1 text-[10px] font-semibold uppercase tracking-widest text-gray-400 select-none">
                {section.heading}
              </p>
            )}
            {(sections.length > 0 || si > 0) && collapsed && (
              <div className="mx-2 mb-2 border-t border-gray-100" />
            )}
            <nav className={clsx("space-y-0.5", collapsed ? "px-1.5" : "px-3")}>
              {section.items.map((item, itemIdx) => (
                <SidebarItem
                  key={getNavItemKey(item, itemIdx)}
                  item={item}
                  pathname={currentPathname}
                  collapsed={collapsed}
                  activeColor={nav.activeColor}
                  activeBg={nav.activeBg}
                  isActive={isItemActive(item, activeNavItem)}
                />
              ))}
            </nav>
          </div>
        ))}
      </div>

      {/* ── Global bottom section (Account / Activity Log) ─────────────────── */}
      {bottomNavItems.length > 0 &&
        (portalConfig ? portalConfig.showBottomNav : true) && (
          <div className="shrink-0 border-t border-gray-100 py-2">
            {GLOBAL_BOTTOM_NAV.heading && !collapsed && (
              <p className="px-5 mb-1 text-[10px] font-semibold uppercase tracking-widest text-gray-400 select-none">
                {GLOBAL_BOTTOM_NAV.heading}
              </p>
            )}
            <nav className={clsx("space-y-0.5", collapsed ? "px-1.5" : "px-3")}>
              {bottomNavItems.map((item, itemIdx) => (
                <SidebarItem
                  key={getNavItemKey(item, itemIdx)}
                  item={item}
                  pathname={currentPathname}
                  collapsed={collapsed}
                  activeColor={nav.activeColor}
                  activeBg={nav.activeBg}
                  isActive={isItemActive(item, activeNavItem)}
                />
              ))}
            </nav>
          </div>
        )}
    </aside>
  );
}

function SidebarItem({
  item,
  pathname,
  collapsed,
  activeColor,
  activeBg,
  badgeCount,
  isActive: isActiveProp,
}: {
  item: NavItem;
  pathname: string;
  collapsed: boolean;
  activeColor: string;
  activeBg: string;
  badgeCount?: number;
  isActive?: boolean;
}) {
  const isActive = isActiveProp ?? false;
  const showBadge = typeof badgeCount === "number" && badgeCount > 0;

  const content = (
    <>
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
      {item.icon ? (
        <i
          className={`${item.icon} text-[16px] leading-none shrink-0`}
          style={{ color: isActive ? activeColor : undefined }}
        />
      ) : (
        <span className="w-1.5 h-1.5 rounded-full bg-current opacity-50" />
      )}
      {!collapsed && <span className="flex-1">{item.label}</span>}
      {showBadge && !collapsed && (
        <span className="ml-auto inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 rounded-full bg-red-500 text-white text-[10px] font-semibold leading-none">
          {badgeCount > 99 ? "99+" : badgeCount}
        </span>
      )}
      {showBadge && collapsed && (
        <span className="absolute -top-0.5 -right-0.5 w-2.5 h-2.5 rounded-full bg-red-500 ring-2 ring-white" />
      )}
    </>
  );

  const className = clsx(
    "relative flex items-center rounded-lg text-[12px] font-medium transition-colors",
    collapsed ? "w-8 h-8 justify-center mx-auto" : "gap-2.5 px-3 py-2.5",
    !isActive && "text-gray-600 hover:bg-gray-100 hover:text-gray-900",
  );
  const style = isActive
    ? { backgroundColor: activeBg, color: "#0f1928" }
    : undefined;

  if (item.disabledMessage) {
    return (
      <button
        type="button"
        onClick={() => toast.info(item.disabledMessage!)}
        title={
          collapsed
            ? `${item.label}${showBadge ? ` (${badgeCount})` : ""}`
            : undefined
        }
        className={clsx(
          className,
          "w-full text-left appearance-none bg-transparent border-0 cursor-pointer",
        )}
        style={style}
      >
        {content}
      </button>
    );
  }

  // If there's a disabledMessage we already returned a button; when there's
  // no href (grouping-only / heading items), render a non-clickable container
  // so the sidebar can display grouping parents without a link.
  if (!item.href) {
    return (
      <div
        title={
          collapsed
            ? `${item.label}${showBadge ? ` (${badgeCount})` : ""}`
            : undefined
        }
        className={className}
        style={style}
      >
        {content}
      </div>
    );
  }

  return (
    <Link
      href={item.href}
      title={
        collapsed
          ? `${item.label}${showBadge ? ` (${badgeCount})` : ""}`
          : undefined
      }
      className={className}
      style={style}
    >
      {content}
    </Link>
  );
}

function SidebarDropdownItem({
  item,
  pathname,
  collapsed,
  activeColor,
  activeBg,
  badges,
  activeNavItem,
  isActive: isSelfActive,
}: {
  item: NavItem;
  pathname: string;
  collapsed: boolean;
  activeColor: string;
  activeBg: string;
  badges: Record<string, number>;
  activeNavItem: NavItem | null;
  isActive: boolean;
}) {
  const children = item.children ?? [];
  const isChildActive = children.some(
    (child) => child.href === activeNavItem?.href,
  );
  const shouldBeOpen = isSelfActive || isChildActive;
  const [open, setOpen] = useState(shouldBeOpen);

  useEffect(() => {
    if (shouldBeOpen) setOpen(true);
  }, [shouldBeOpen]);

  if (collapsed) {
    return (
      <>
        <SidebarItem
          item={item}
          pathname={pathname}
          collapsed={collapsed}
          activeColor={activeColor}
          activeBg={activeBg}
          isActive={isSelfActive || isChildActive}
        />
        {children.map((child, childIdx) => (
          <SidebarItem
            key={getNavItemKey(child, childIdx)}
            item={child}
            pathname={pathname}
            collapsed={collapsed}
            activeColor={activeColor}
            activeBg={activeBg}
            badgeCount={child.badgeKey ? badges[child.badgeKey] : undefined}
            isActive={child.href === activeNavItem?.href}
          />
        ))}
      </>
    );
  }

  return (
    <div>
      <div
        className={clsx(
          "relative flex items-center rounded-lg text-[12px] font-medium transition-colors pr-1.5 cursor-pointer",
          !(isSelfActive || isChildActive) &&
            "text-gray-600 hover:bg-gray-100 hover:text-gray-900",
        )}
        style={
          isSelfActive || isChildActive
            ? { backgroundColor: activeBg, color: "#0f1928" }
            : undefined
        }
        onClick={(e) => {
          e.preventDefault();
          setOpen((prev) => !prev);
        }}
      >
        {(isSelfActive || isChildActive) && (
          <span
            className="absolute left-0 top-1.5 bottom-1.5 w-0.5 rounded-full"
            style={{ backgroundColor: activeColor }}
          />
        )}
        {item.href ? (
          <Link
            href={item.href}
            className="flex-1 flex items-center gap-2.5 px-3 py-2.5 min-w-0"
          >
            {item.icon ? (
              <i
                className={`${item.icon} text-[16px] leading-none shrink-0`}
                style={{ color: isSelfActive ? activeColor : undefined }}
              />
            ) : (
              <span className="w-1.5 h-1.5 rounded-full bg-current opacity-50" />
            )}
            <span className="flex-1 truncate">{item.label}</span>
          </Link>
        ) : (
          <div className="flex-1 flex items-center gap-2.5 px-3 py-2.5 min-w-0">
            {item.icon ? (
              <i
                className={`${item.icon} text-[16px] leading-none shrink-0`}
                style={{ color: isSelfActive ? activeColor : undefined }}
              />
            ) : (
              <span className="w-1.5 h-1.5 rounded-full bg-current opacity-50" />
            )}
            <span className="flex-1 truncate font-semibold">
              {item.heading ?? item.label}
            </span>
          </div>
        )}
        <button
          type="button"
          title={open ? "Collapse" : "Expand"}
          className="flex items-center justify-center w-6 h-6 shrink-0 rounded-md"
        >
          <i
            className={clsx(
              "ri-arrow-down-s-line text-[15px] leading-none transition-transform",
              !open && "-rotate-90",
            )}
          />
        </button>
      </div>
      {open && (
        <div className="ml-4 pl-2.5 border-l border-gray-100 space-y-0.5 mt-0.5">
          {children.map((child, childIdx) => (
            <SidebarItem
              key={getNavItemKey(child, childIdx)}
              item={child}
              pathname={pathname}
              collapsed={collapsed}
              activeColor={activeColor}
              activeBg={activeBg}
              badgeCount={child.badgeKey ? badges[child.badgeKey] : undefined}
              isActive={isItemActive(child, activeNavItem)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
