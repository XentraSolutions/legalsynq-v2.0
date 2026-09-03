"use client";

import Image from "next/image";
import Link from "next/link";
import { useState, useRef, useEffect, type ComponentType } from "react";
import { useSession } from "@/hooks/use-session";
import { useProduct } from "@/contexts/product-context";
import { useSidebar } from "@/contexts/sidebar-context";
import { useBreadcrumbOverride } from "@/contexts/breadcrumb-context";
import { useActiveNavTrail } from "@/hooks/use-active-nav-item";
import {
  orgTypeLabel,
  PRODUCT_CODE_TO_NAV_KEY,
  PRODUCT_META,
  getProductDefaultHref,
} from "@/lib/nav";
import { getClientPortalConfig, type PortalConfig } from "@/lib/portal";
import { isEligibleForCareConnectCommonPortal } from "@/lib/careconnect-common-portal-access";
import { useTenantBranding } from "@/providers/tenant-branding-provider";
import { NotificationBell } from "@/components/shell/notification-bell";
import { XeniaAssistant } from "@/components/xenia/xenia-assistant";
import type { NavItem, PlatformSession } from "@/types";
import { GlobalSearch } from "../ui/global-search";
import { Search, Settings, ChevronDown, Building2, Users } from "lucide-react";
import { clsx } from "clsx";
import { isMacPlatform } from "@/lib/platform";

// ── All platform products shown in the app switcher ──────────────────────────

const ALL_PRODUCTS = [
  {
    id: "careconnect",
    label: "Synq CareConnect",
    href: getProductDefaultHref("careconnect"),
    iconSrc: "/product-icons/synqconnect.png",
    bg: "#eff6ff",
  },
  {
    id: "fund",
    label: "Synq Funds",
    href: getProductDefaultHref("fund"),
    iconSrc: "/product-icons/synqfund.png",
    bg: "#f0fdf4",
  },
  {
    id: "xenia",
    label: "Xenia",
    href: getProductDefaultHref("xenia"),
    iconSrc: "/product-icons/synqai.png",
    bg: "#fffbeb",
  },
  {
    id: "insights",
    label: "Synq Insights",
    href: getProductDefaultHref("insights"),
    iconSrc: "/product-icons/synqinsight.png",
    bg: "#ecfeff",
  },
];

// Per-product global search implementations. Only Synq Liens (buying) has one
// today. A product gets its own header search icon only once it's registered
// here; add `<productId>: <ItsSearchComponent>` and the header icon +
// popover swap to it automatically. Everything else has no working search
// yet, so the icon stays hidden rather than opening a search that silently
// returns lien-only results.
const PRODUCT_SEARCH: Partial<Record<string, ComponentType<{ onClose?: () => void }>>> = {
  lien: GlobalSearch,
};

// Synq Liens is a toggle-only group in the switcher: "Selling" and "Buying"
// (the existing standalone "lien" product) are its two sub-modes rather than
// separate top-level products.
const LIENS_GROUP = {
  label: "Synq Liens",
  iconSrc: "/product-icons/synqlien.png",
  bg: "#f5f3ff",
  children: [
    { id: "lien", label: "Buying", href: getProductDefaultHref("lien") },
    { id: "selling", label: "Selling", href: getProductDefaultHref("selling") },
  ],
} as const;

/**
 * Full-width white top bar.
 *
 * Inside a product (sidebar visible), the bar is split by a vertical divider
 * aligned with the sidebar's own right edge:
 *   Left zone (sidebar-width):  logo + sidebar collapse toggle
 *   Right zone:                 product switcher + breadcrumb → search /
 *                                settings / notifications → user menu
 *
 * On the landing dashboard (no sidebar), the left zone is just the logo.
 */
export function TopBar({ isDashboard = false }: { isDashboard?: boolean }) {
  const { session, clearSession, logout } = useSession();
  const branding = useTenantBranding();
  const { selectedProductId } = useProduct();
  const { collapsed, mounted, toggle: toggleSidebar } = useSidebar();
  const [portalConfig, setPortalConfig] = useState<PortalConfig | null>(null);
  const navTrail = useActiveNavTrail();
  const breadcrumbOverride = useBreadcrumbOverride();
  const activeNavTrail = breadcrumbOverride ?? navTrail;
  useEffect(() => {
    setPortalConfig(getClientPortalConfig());
  }, []);

  const showSwitcher = (portalConfig ? portalConfig.showAppSwitcher : true) && !isDashboard;
  // The header search icon only appears for a product that has registered
  // its own search in PRODUCT_SEARCH — everything else has no working
  // search yet, so no icon is shown for it.
  const ProductSearch = selectedProductId
    ? PRODUCT_SEARCH[selectedProductId]
    : undefined;

  const logo = portalConfig ? (
    <Link href={portalConfig.landingPath} className="flex items-center min-w-0">
      <img
        src={portalConfig.logoSrc}
        alt={portalConfig.logoLabel}
        style={{ height: 36, width: "auto", maxWidth: 240 }}
        className="object-contain"
      />
    </Link>
  ) : (
    <Link href="/dashboard" className="flex items-center min-w-0">
      <TenantLogo branding={branding} hasSession={!!session} />
    </Link>
  );

  const hasTrail = showSwitcher && activeNavTrail.length > 0;

  return (
    <header className="platform-chrome flex items-center shrink-0 bg-white border-b border-gray-200">
      {/* ── Left zone: logo (+ sidebar toggle, sized to match the sidebar) ── */}
      {/* Stretches the full header height (not just the h-14 top row) so its
          border-r runs down alongside the breadcrumb row underneath too —
          otherwise the divider stops short at h-14 and the breadcrumb row's
          own border-t reads as a disconnected stub, leaving an ugly notch
          where the two would otherwise meet. */}
      {isDashboard ? (
        <div className="flex items-center h-14 px-4 shrink-0">{logo}</div>
      ) : (
        <div
          className="flex flex-col justify-center shrink-0 border-r border-gray-200 box-border self-stretch"
          style={{
            width: !mounted ? 220 : collapsed ? 52 : 220,
            transition: mounted ? "width 200ms ease" : undefined,
          }}
        >
          <div
            className={clsx(
              "flex items-center py-2 gap-2 shrink-0",
              collapsed ? "justify-center px-0" : "px-4",
            )}
          >
            {!collapsed && <div className="flex-1 min-w-0">{logo}</div>}
            <button
              type="button"
              onClick={toggleSidebar}
              title={
                collapsed
                  ? "Expand sidebar (Ctrl+[)"
                  : "Collapse sidebar (Ctrl+[)"
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
        </div>
      )}

      {/* ── Right zone ──────────────────────────────────────────────────── */}
      {/* A container-query context of its own width (not the header's or the
          viewport's) so this can react to how much room is actually left
          after the sidebar-width left zone.
          Normal width: one row — switcher + action icons, with the
          breadcrumb trail underneath.
          Squeezed: three stacked rows — switcher, then action icons
          (right-aligned), then the breadcrumb trail — so nothing gets
          crowded or clipped. */}
      <div className="flex flex-col flex-1 min-w-0 @container">
        <div className="flex items-center pt-5 pl-4 pr-6 pb-2 @[680px]:pb-5 gap-3">
          {showSwitcher && <ProductBreadcrumb />}

          {/* Inline breadcrumb trail: sits next to the switcher, separated by
              a vertical rule, once the container has room for it. Below that
              width it drops to its own row underneath instead (see hasTrail
              block at the bottom of this container). */}
          {hasTrail && (
            <div className="hidden @[680px]:flex items-center gap-3 min-w-0">
              <span className="h-4 w-px shrink-0 bg-gray-300" aria-hidden />
              <div className="flex items-center gap-1.5 min-w-0 text-sm">
                <BreadcrumbTrailCrumbs trail={activeNavTrail} />
              </div>
            </div>
          )}

          <div className="hidden @[640px]:flex flex-1" />

          <div className="relative hidden @[640px]:flex items-center gap-2 shrink-0">
            <HeaderControls
              isDashboard={isDashboard}
              session={session}
              ProductSearch={ProductSearch}
              clearSession={clearSession}
              logout={logout}
            />
          </div>
        </div>

        <div className="relative flex @[640px]:hidden items-center justify-between pl-4 pr-6 pb-2 gap-2">
          <HeaderControls
            isDashboard={isDashboard}
            session={session}
            ProductSearch={ProductSearch}
            clearSession={clearSession}
            logout={logout}
          />
        </div>

        {hasTrail && (
          <div className="flex @[680px]:hidden items-center px-6 pb-5 gap-1.5 text-sm">
            <BreadcrumbTrailCrumbs trail={activeNavTrail} />
          </div>
        )}
      </div>
    </header>
  );
}

// ── Header action icons: Xenia, search, settings, notifications, profile ────

function HeaderControls({
  isDashboard,
  session,
  ProductSearch,
  clearSession,
  logout,
}: {
  isDashboard: boolean;
  session: PlatformSession | null;
  ProductSearch?: ComponentType<{ onClose?: () => void }>;
  clearSession: () => void;
  logout: () => Promise<void>;
}) {
  return (
    <>
      {!isDashboard && session && hasProductAccess(session, "xenia") && (
        <XeniaAssistantDrawer />
      )}

      {ProductSearch && <HeaderSearch Search={ProductSearch} />}

      {session && (session.isTenantAdmin || session.isPlatformAdmin) && (
        <HeaderSettings />
      )}

      <NotificationBell />

      {/* ── User menu ────────────────────────────────────────────────────── */}
      {/* Always render something so the top-right corner never goes blank: */}
      {/* - skeleton while loading or if session is unavailable             */}
      {/* - UserMenu once the session is confirmed                         */}
      {session ? (
        <UserMenu session={session} clearSession={clearSession} logout={logout} />
      ) : (
        <div className="w-8 h-8 rounded-full bg-gray-100 animate-pulse shrink-0" />
      )}
    </>
  );
}

// ── Header search icon + popover ───────────────────────────────────────────────

function HeaderSearch({
  Search: SearchPanel,
}: {
  Search: ComponentType<{ onClose?: () => void }>;
}) {
  const [open, setOpen] = useState(false);
  // Defaults to the Windows/Linux label on the server render and flips to
  // ⌘ after mount if we detect macOS — avoids a hydration mismatch from
  // reading navigator.platform during the initial render.
  const [shortcutLabel, setShortcutLabel] = useState("Ctrl+/");
  useEffect(() => {
    if (isMacPlatform()) setShortcutLabel("⌘/");
  }, []);

  // ⌘/ (Ctrl+/ on Windows/Linux) opens the spotlight-style search from
  // anywhere in the product — one shortcut, not two, so it's the only one
  // users need to remember.
  // ⌘K/Ctrl+K was tried first but Chrome itself already owns that combo
  // (it focuses the omnibox), so the page never even saw the keydown.
  // ⌘/ isn't claimed by the browser or OS, and since holding the modifier
  // means no character actually gets typed, it's safe to fire even while
  // focus is inside a field — unless that field's own handler already
  // captured/prevented the keydown first.
  useEffect(() => {
    function handler(e: KeyboardEvent) {
      if (e.key !== "/" || e.altKey || !(e.metaKey || e.ctrlKey)) return;
      e.preventDefault();
      setOpen(true);
    }
    // Capture phase so this fires even if something else on the page
    // (a dropdown, a popover, an input's own keydown handler) calls
    // stopPropagation() before it would otherwise bubble up to document.
    document.addEventListener("keydown", handler, true);
    return () => document.removeEventListener("keydown", handler, true);
  }, []);

  return (
    <div className="relative flex items-center shrink-0">
      <button
        type="button"
        onClick={() => setOpen(true)}
        title={`Search (${shortcutLabel})`}
        aria-haspopup="true"
        aria-expanded={open}
        className={[
          "w-8 h-8 flex items-center justify-center rounded-lg transition-colors",
          open
            ? "bg-gray-100 text-gray-900"
            : "text-gray-400 hover:bg-gray-100 hover:text-gray-700",
        ].join(" ")}
      >
        <Search className="w-[18px] h-[18px]" />
      </button>

      {open && <SearchPanel onClose={() => setOpen(false)} />}
    </div>
  );
}

// ── Header settings icon + popover ─────────────────────────────────────────────
// Mirrors the Jira-style gear menu: tenant-level settings for admins, or a
// "contact your admin" notice for everyone else. Personal account settings
// live under the user's own name in UserMenu, not here.

function HeaderSettings() {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node))
        setOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function handler(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [open]);

  return (
    <div ref={ref} className="relative flex items-center shrink-0">
      <button
        type="button"
        onClick={() => setOpen((p) => !p)}
        title="Settings"
        aria-haspopup="true"
        aria-expanded={open}
        className={[
          "w-8 h-8 flex items-center justify-center rounded-lg transition-colors",
          open
            ? "bg-gray-100 text-gray-900"
            : "text-gray-400 hover:bg-gray-100 hover:text-gray-700",
        ].join(" ")}
      >
        <Settings className="w-[18px] h-[18px]" />
      </button>

      {open && (
        <div className="absolute right-0 top-[calc(100%+10px)] w-80 rounded-xl bg-white shadow-2xl border border-gray-200 overflow-hidden z-50">
          <div className="px-4 py-3 border-b border-gray-100">
            <p className="text-sm font-semibold text-gray-900">
              Tenant settings
            </p>
          </div>

          <div className="py-2">
            <SettingsMenuItem
              href="/tenant/settings"
              icon={Building2}
              label="Tenant Settings"
              description="Manage your organization's configuration."
              onClick={() => setOpen(false)}
            />
            <SettingsMenuItem
              href="/tenant/authorization/users"
              icon={Users}
              label="User Management"
              description="Manage users and their roles."
              onClick={() => setOpen(false)}
            />
          </div>
        </div>
      )}
    </div>
  );
}

function SettingsMenuItem({
  href,
  icon: Icon,
  label,
  description,
  onClick,
}: {
  href: string;
  icon: typeof Building2;
  label: string;
  description: string;
  onClick: () => void;
}) {
  return (
    <Link
      href={href}
      onClick={onClick}
      className="flex items-start gap-3 px-4 py-2.5 hover:bg-gray-50 transition-colors"
    >
      <Icon className="w-[18px] h-[18px] text-gray-400 mt-0.5 shrink-0" />
      <div className="min-w-0">
        <p className="text-sm font-medium text-gray-900">{label}</p>
        <p className="text-xs text-gray-500 mt-0.5">{description}</p>
      </div>
    </Link>
  );
}

function hasProductAccess(
  session: PlatformSession,
  productId: string,
): boolean {
  const productList = session.userProducts?.length
    ? session.userProducts
    : (session.enabledProducts ?? []);
  return productList
    .map((code) => PRODUCT_CODE_TO_NAV_KEY[code])
    .filter(Boolean)
    .includes(productId);
}

function XeniaAssistantDrawer() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    function handler(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [open]);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        title="Open Xenia"
        className="flex h-8 shrink-0 cursor-pointer items-center justify-center gap-1.5 rounded-lg px-1.5 transition-colors hover:bg-gray-100 @[700px]:px-2.5"
      >
        <img
          src="/product-icons/synqai.png"
          alt=""
          aria-hidden
          className="h-[18px] w-[18px] shrink-0 object-contain"
        />
        <span className="hidden text-sm font-medium text-gray-700 @[700px]:inline">
          Xenia
        </span>
      </button>

      {open && (
        <div className="fixed inset-0 z-[80] flex justify-end bg-black/30">
          <button
            type="button"
            aria-label="Close Xenia"
            className="absolute inset-0 cursor-default"
            onClick={() => setOpen(false)}
          />
          <section className="relative flex h-full w-full max-w-[460px] flex-col bg-white shadow-2xl">
            <div className="flex h-14 items-center justify-between border-b border-gray-200 px-4">
              <div className="flex items-center gap-2">
                <span className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-amber-50">
                  <img
                    src="/product-icons/synqai.png"
                    alt=""
                    aria-hidden
                    className="h-4 w-4 object-contain"
                  />
                </span>
                <div>
                  <p className="text-sm font-semibold text-gray-900">Xenia</p>
                  <p className="text-xs text-gray-500">Assistant drawer</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setOpen(false)}
                title="Close"
                className="inline-flex h-8 w-8 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 hover:text-gray-900"
              >
                <i className="ri-close-line text-lg" />
              </button>
            </div>
            <div className="min-h-0 flex-1">
              <XeniaAssistant mode="drawer" />
            </div>
          </section>
        </div>
      )}
    </>
  );
}

// ── Page breadcrumb trail: current page's nav trail ─────────────────────────
// Renders as "Contacts / Companies" etc. Shown inline next to the product
// switcher by default; the header switches it to its own row underneath
// (via container query, based on the right zone's own width) once there
// isn't room to keep it on the same line without crowding the action icons.

function BreadcrumbTrailCrumbs({ trail }: { trail: NavItem[] }) {
  return (
    <>
      {trail.map((item, index) => {
        const isLast = index === trail.length - 1;
        return (
          <span key={item.href ?? index} className="flex items-center gap-1.5">
            {index > 0 && (
              <span className="text-gray-300" aria-hidden>
                /
              </span>
            )}
            {item.href && !isLast ? (
              <Link
                href={item.href}
                className="text-gray-500 hover:text-gray-700"
              >
                {item.label}
              </Link>
            ) : (
              <span
                className={
                  isLast ? "text-gray-900 font-medium" : "text-gray-500"
                }
              >
                {item.label}
              </span>
            )}
          </span>
        );
      })}
    </>
  );
}

// ── Product breadcrumb: the product switcher trigger ────────────────────────
// Replaces the old 9-dot app switcher — the current product's icon/name is now
// the switcher trigger itself.

function ProductBreadcrumb() {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const { selectedProductId, setSelectedProductId } = useProduct();
  const { session, isLoading } = useSession();

  // Compute visible products only once the session is confirmed loaded.
  // LS-ID-TNT-009: prefer userProducts (user-level effective access from JWT product_codes)
  // over enabledProducts (tenant-level) so the switcher shows only products the user can
  // actually use. Both empty → show none.
  // Note: portal-level restriction is enforced at the TopBar level — this is hidden
  // entirely on restricted portals, so no portal filtering is needed here.
  const enabledProductIds: Set<string> = (() => {
    if (isLoading || !session) return new Set(); // not ready yet
    const up = session.userProducts ?? [];
    const ep = session.enabledProducts ?? [];
    const productList = up.length > 0 ? up : ep; // user-level beats tenant-level
    return new Set(
      productList.map((code) => PRODUCT_CODE_TO_NAV_KEY[code]).filter(Boolean),
    );
  })();

  const visibleProducts = ALL_PRODUCTS.filter((p) =>
    enabledProductIds.has(p.id),
  );
  const visibleLiensChildren = LIENS_GROUP.children.filter((c) =>
    enabledProductIds.has(c.id),
  );

  const meta = selectedProductId ? PRODUCT_META[selectedProductId] : null;
  const activeLiensChild = LIENS_GROUP.children.find(
    (c) => c.id === selectedProductId,
  );

  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node))
        setOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function handler(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [open]);

  return (
    <div className="flex items-center gap-3 min-w-0 shrink-0">
      <div ref={ref} className="relative flex items-center shrink-0">
        <button
          onClick={() => setOpen((p) => !p)}
          title="Switch product"
          aria-haspopup="true"
          aria-expanded={open}
          className={clsx(
            "flex items-center gap-2 h-8 pl-1.5 pr-2 rounded-lg transition-colors",
            open ? "bg-gray-100" : "hover:bg-gray-100",
          )}
        >
          {meta?.iconSrc && (
            <img
              src={meta.iconSrc}
              alt=""
              aria-hidden
              className="w-5 h-5 object-contain shrink-0"
            />
          )}
          {activeLiensChild ? (
            <span className="flex items-center gap-2 min-w-0">
              <span className="text-sm font-semibold text-gray-900 truncate max-w-[120px]">
                {LIENS_GROUP.label}
              </span>
              <span className="h-3 w-px shrink-0 bg-gray-300" />
              <span className="text-sm font-medium text-gray-500 truncate max-w-[100px]">
                {activeLiensChild.label}
              </span>
            </span>
          ) : (
            <span className="text-sm font-semibold text-gray-900 truncate max-w-[180px]">
              {meta?.label ?? "Switch product"}
            </span>
          )}
          <ChevronDown className="w-4 h-4 text-gray-400 shrink-0" />
        </button>

        {open && (
          <div className="absolute left-0 top-[calc(100%+10px)] w-64 rounded-xl bg-white shadow-2xl border border-gray-200 overflow-hidden z-50">
            <div className="px-4 py-3 border-b border-gray-100">
              <p className="text-[11px] font-semibold uppercase tracking-widest text-gray-400">
                LegalSynq Products
              </p>
            </div>

            <div className="p-1">
              {visibleProducts
                .filter((p) => p.id === "careconnect")
                .map((product) => (
                  <ProductRow
                    key={product.id}
                    product={product}
                    onClick={() => {
                      setSelectedProductId(product.id);
                      setOpen(false);
                    }}
                  />
                ))}

              {visibleLiensChildren.length > 0 && (
                <LiensGroupItem
                  liensChildren={visibleLiensChildren}
                  selectedProductId={selectedProductId}
                  onNavigate={(id) => {
                    setSelectedProductId(id);
                    setOpen(false);
                  }}
                />
              )}

              {visibleProducts
                .filter((p) => p.id !== "careconnect")
                .map((product) => (
                  <ProductRow
                    key={product.id}
                    product={product}
                    onClick={() => {
                      setSelectedProductId(product.id);
                      setOpen(false);
                    }}
                  />
                ))}

              {visibleProducts.length === 0 &&
                visibleLiensChildren.length === 0 && (
                  <p className="px-4 py-3 text-xs text-gray-400">
                    No products enabled for your account.
                  </p>
                )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function ProductRow({
  product,
  onClick,
  nested,
  isActive,
}: {
  product: { label: string; href: string; iconSrc?: string; bg?: string };
  onClick: () => void;
  /** Sub-item of a product group: no icon, indented 16px, with a left accent border marking it as its parent's active module. */
  nested?: boolean;
  isActive?: boolean;
}) {
  return (
    <Link
      href={product.href}
      onClick={onClick}
      className={clsx(
        "flex items-center gap-3 min-h-11 py-2 border-l-2 hover:bg-gray-50 transition-colors group",
        nested ? "ml-4 px-4" : "px-2",
        isActive ? "border-orange-500 bg-orange-50/40" : "border-transparent",
      )}
    >
      {!nested && (
        <div
          className="w-9 h-9 rounded-lg flex items-center justify-center shrink-0"
          style={{ backgroundColor: product.bg }}
        >
          <img
            src={product.iconSrc}
            alt=""
            aria-hidden
            className="w-5 h-5 object-contain"
          />
        </div>
      )}
      <span
        className={clsx(
          "text-sm font-medium transition-colors",
          isActive
            ? "text-gray-900"
            : "text-gray-700 group-hover:text-gray-900",
        )}
      >
        {product.label}
      </span>
    </Link>
  );
}

// Toggle-only "Synq Liens" group: clicking the row expands/collapses its
// children (Selling / Buying) rather than navigating anywhere itself.
function LiensGroupItem({
  liensChildren,
  selectedProductId,
  onNavigate,
}: {
  liensChildren: (typeof LIENS_GROUP.children)[number][];
  selectedProductId: string | null;
  onNavigate: (id: string) => void;
}) {
  const isActiveGroup = liensChildren.some((c) => c.id === selectedProductId);
  const [expanded, setExpanded] = useState(isActiveGroup);

  return (
    <div>
      <button
        type="button"
        onClick={() => setExpanded((p) => !p)}
        aria-expanded={expanded}
        className={clsx(
          "flex items-center gap-3 w-full px-2 py-2 hover:bg-gray-50 transition-colors group",
          isActiveGroup && "bg-orange-50/60",
        )}
      >
        <div
          className="w-9 h-9 rounded-lg flex items-center justify-center shrink-0"
          style={{ backgroundColor: LIENS_GROUP.bg }}
        >
          <img
            src={LIENS_GROUP.iconSrc}
            alt=""
            aria-hidden
            className="w-5 h-5 object-contain"
          />
        </div>
        <span className="flex-1 text-left text-sm font-medium text-gray-700 group-hover:text-gray-900 transition-colors">
          {LIENS_GROUP.label}
        </span>
        <ChevronDown
          className={clsx(
            "w-4 h-4 text-gray-400 shrink-0 transition-transform",
            expanded && "rotate-180",
          )}
        />
      </button>

      {expanded && (
        <div className="flex flex-col mt-2">
          {liensChildren.map((child) => (
            <ProductRow
              key={child.id}
              product={{ label: child.label, href: child.href }}
              onClick={() => onNavigate(child.id)}
              nested
              isActive={child.id === selectedProductId}
            />
          ))}
        </div>
      )}
    </div>
  );
}

// ── Profile dropdown ──────────────────────────────────────────────────────────

interface UserMenuProps {
  session: NonNullable<ReturnType<typeof useSession>["session"]>;
  clearSession: () => void;
  logout: () => Promise<void>;
}

function UserMenu({
  session,
  clearSession,
  logout,
}: UserMenuProps & { logout: () => Promise<void> }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  /** COMMENTED THIS AS THE FEATURE IS STILL NOT AVAILABLE PER QA TICKET: LSV3-862 Tenant Portal: Hide navigation items for features that are currently under development */
  const hideActivityLog = true; //isEligibleForCareConnectCommonPortal(session);

  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node))
        setOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function handler(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [open]);

  async function handleSignOut() {
    setOpen(false);
    logout();
  }

  const initials = session.orgName
    ? session.orgName
        .split(" ")
        .slice(0, 2)
        .map((w) => w[0])
        .join("")
        .toUpperCase()
    : session.email.slice(0, 2).toUpperCase();

  const avatarSrc = session.avatarDocumentId
    ? `/api/profile/avatar/${session.avatarDocumentId}`
    : null;

  return (
    <div ref={ref} className="relative flex items-center shrink-0">
      <button
        onClick={() => setOpen((p) => !p)}
        title={session.orgName ?? session.email}
        className="flex h-10 items-center gap-2 rounded-full pr-0 transition-colors hover:bg-gray-100 focus:outline-none shrink-0 @[700px]:rounded-lg @[700px]:pr-2"
        aria-haspopup="true"
        aria-expanded={open}
      >
        {avatarSrc ? (
          <img
            src={avatarSrc}
            alt="Profile"
            className="w-10 h-10 rounded-full object-cover shrink-0"
          />
        ) : (
          <div
            className="w-10 h-10 rounded-full flex items-center justify-center text-xs font-bold text-white shrink-0"
            style={{ backgroundColor: "#f97316" }}
          >
            {initials}
          </div>
        )}
        <span className="hidden max-w-[140px] flex-col items-start text-left @[700px]:flex">
          <span className="w-full truncate text-left text-sm font-medium text-gray-700">
            {session.orgName ?? session.email}
          </span>
          <span className="w-full truncate text-left text-xs text-gray-500">
            {session.email}
          </span>
        </span>
        <ChevronDown className="hidden h-4 w-4 shrink-0 text-gray-400 @[700px]:inline" />
      </button>

      {open && (
        <div
          className="absolute right-0 top-[calc(100%+10px)] w-64 rounded-xl bg-white shadow-xl border border-gray-200 overflow-hidden z-50"
          role="menu"
        >
          <div className="flex items-center gap-2 px-4 py-3.5 bg-gray-50 border-b border-gray-100">
            {avatarSrc ? (
              <img
                src={avatarSrc}
                alt="Profile"
                className="w-10 h-10 rounded-full object-cover shrink-0"
              />
            ) : (
              <div
                className="w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold text-white shrink-0"
                style={{ backgroundColor: "#f97316" }}
              >
                {initials}
              </div>
            )}
            <div className="min-w-0">
              <p className="text-sm font-semibold text-gray-900 truncate">
                {session.orgName ?? session.email}
              </p>
              <p className="text-xs text-gray-500 truncate">{session.email}</p>
              <p className="text-[10px] text-gray-400 mt-0.5">
                {orgTypeLabel(session.orgType)}
              </p>
            </div>
          </div>

          <div className="py-1.5">
            <ProfileMenuItem
              href="/profile"
              icon="ri-user-3-line"
              label="Profile"
              onClick={() => setOpen(false)}
            />
            <ProfileMenuItem
              href="/settings"
              icon="ri-settings-3-line"
              label="Account Settings"
              onClick={() => setOpen(false)}
            />
            {!hideActivityLog && (
              <ProfileMenuItem
                href="/activity"
                icon="ri-history-line"
                label="Activity Log"
                onClick={() => setOpen(false)}
              />
            )}
          </div>

          <div className="border-t border-gray-100" />

          <div className="py-1.5">
            <button
              onClick={handleSignOut}
              role="menuitem"
              className="flex w-full items-center gap-3 px-4 py-2.5 text-sm text-red-600 hover:bg-red-50 transition-colors"
            >
              <i className="ri-logout-box-r-line text-base leading-none" />
              <span>Log out</span>
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function TenantLogo({
  branding,
  hasSession,
}: {
  branding: ReturnType<typeof useTenantBranding>;
  hasSession: boolean;
}) {
  const sources: string[] = [];
  // White header bg: prefer the regular (dark) logo first, then fall back to
  // the white variant (better than nothing, even if it's low-contrast).
  if (branding.logoDocumentId && hasSession)
    sources.push(`/api/branding/logo/${branding.logoDocumentId}`);
  if (branding.logoWhiteDocumentId && hasSession)
    sources.push(`/api/branding/logo/${branding.logoWhiteDocumentId}`);
  // Direct CDN/URL logo (no auth required)
  if (branding.logoUrl) sources.push(branding.logoUrl);
  // Non-authenticated public fallback (e.g. login page): the BFF will attempt
  // /public/logo/{id} which serves scan-clean, IsPublishedAsLogo=true documents
  // without requiring a session cookie.  Only add when there is an actual GUID —
  // the old fallback of '/api/branding/logo/public' passed the literal string
  // "public" which is never a valid GUID and always returned 404.
  if (!hasSession) {
    if (branding.logoDocumentId)
      sources.push(`/api/branding/logo/${branding.logoDocumentId}`);
    if (branding.logoWhiteDocumentId)
      sources.push(`/api/branding/logo/${branding.logoWhiteDocumentId}`);
  }

  const [srcIndex, setSrcIndex] = useState(0);
  const [exhausted, setExhausted] = useState(false);

  const sourcesKey = sources.join("|");
  useEffect(() => {
    setSrcIndex(0);
    setExhausted(false);
  }, [sourcesKey]);

  function handleError() {
    const next = srcIndex + 1;
    if (next < sources.length) {
      setSrcIndex(next);
    } else {
      setExhausted(true);
    }
  }

  if (exhausted || sources.length === 0) {
    return (
      <Image
        src="/legalsynq-logo.svg"
        alt="LegalSynq"
        width={130}
        height={32}
        priority
        unoptimized
        className="h-8 w-auto"
      />
    );
  }

  const currentSrc = sources[srcIndex] ?? "";
  // The white logo variant is meant for dark backgrounds — on this white
  // header it needs inverting back to dark to stay visible.
  const isWhiteSrc =
    !!branding.logoWhiteDocumentId &&
    currentSrc.includes(branding.logoWhiteDocumentId);

  return (
    <img
      src={currentSrc}
      alt={branding.displayName || "Tenant logo"}
      className="w-auto object-contain max-w-[180px]"
      style={{
        height: 32,
        ...(isWhiteSrc ? { filter: "brightness(0)" } : {}),
      }}
      onError={handleError}
    />
  );
}

function ProfileMenuItem({
  href,
  icon,
  label,
  onClick,
}: {
  href: string;
  icon: string;
  label: string;
  onClick: () => void;
}) {
  return (
    <Link
      href={href}
      role="menuitem"
      onClick={onClick}
      className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 transition-colors"
    >
      <i className={`${icon} text-base leading-none text-gray-400`} />
      <span>{label}</span>
    </Link>
  );
}
