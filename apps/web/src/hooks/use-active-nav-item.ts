"use client";

import { usePathname, useSearchParams } from "next/navigation";
import { useProduct } from "@/contexts/product-context";
import { useSession } from "@/hooks/use-session";
import { useProviderMode } from "@/hooks/use-provider-mode";
import { getClientPortalConfig } from "@/lib/portal";
import {
  PRODUCT_NAV,
  GLOBAL_BOTTOM_NAV,
  buildNavGroups,
  filterNavByAccess,
} from "@/lib/nav";
import type { NavItem } from "@/types";

// Hrefs carrying a query string (e.g. "/selling/contacts?view=contacts") must
// match pathname AND search exactly, since several sibling items can share
// the same pathname and differ only by query (a query-less sibling is that
// route's default view, so it should stop matching once a query'd sibling
// does).
function hrefMatchesCurrent(
  href: string,
  pathname: string,
  search: string,
): boolean {
  const [hrefPath, hrefQuery] = href.split("?");
  if (hrefQuery) return pathname === hrefPath && search === hrefQuery;
  if (pathname === hrefPath) return search === "";
  return hrefPath !== "/" && pathname.startsWith(hrefPath);
}

/**
 * Resolves the sidebar section/nav items for the current product + session,
 * shared by both the active-item lookup and the trail lookup below so they
 * never drift out of sync with each other. Exported so the Sidebar itself
 * can reuse this exact computation rather than re-deriving it separately.
 */
export function useResolvedNavSections() {
  const { selectedProductId } = useProduct();
  const { session } = useSession();
  const { isSellMode } = useProviderMode();

  const adminSections = session ? buildNavGroups(session) : [];
  const rawSections = selectedProductId
    ? (PRODUCT_NAV[selectedProductId] ?? [])
    : [];
  const portalConfig = getClientPortalConfig();
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

  // Top-level items (sections + adminSections + global bottom nav), and the
  // same list with each item's children flattened in — shared here so
  // useActiveNavItem and useActiveNavTrail can't diverge on how they expand
  // `.children` when matching against the current route.
  const topLevelItems = [
    ...sections.flatMap((s) => s.items),
    ...adminSections.flatMap((s) => s.items),
    ...GLOBAL_BOTTOM_NAV.items,
  ];
  const allNavItems = topLevelItems.flatMap((item) => [
    item,
    ...(item.children ?? []),
  ]);

  return { sections, adminSections, topLevelItems, allNavItems };
}

/**
 * Resolves the sidebar nav item matching the current route — the same logic
 * the sidebar uses to highlight itself, shared so the top-bar breadcrumb
 * stays in sync with it.
 */
export function useActiveNavItem(): NavItem | null {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { allNavItems } = useResolvedNavSections();

  const currentPathname = pathname ?? "";
  const currentSearch = searchParams?.toString() ?? "";

  // Pick the most specific match (longest href) rather than the first one
  // found, since sibling routes can nest (e.g. "/selling/contacts" is a
  // prefix of "/selling/contacts/persons") and array order shouldn't decide.
  return (
    allNavItems
      .filter(
        (item) =>
          !!item.href &&
          hrefMatchesCurrent(item.href, currentPathname, currentSearch),
      )
      .sort((a, b) => (b.href?.length ?? 0) - (a.href?.length ?? 0))[0] ?? null
  );
}

/**
 * Full breadcrumb trail for the current route — [parent, ...active item] when
 * the active item is a nested child (e.g. "Contacts" > "Companies"), or just
 * [active item] for a top-level page. Used by the top-bar breadcrumb, which
 * wants the whole path rather than just the deepest label.
 */
export function useActiveNavTrail(): NavItem[] {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { topLevelItems, allNavItems } = useResolvedNavSections();

  const currentPathname = pathname ?? "";
  const currentSearch = searchParams?.toString() ?? "";

  const activeItem =
    allNavItems
      .filter(
        (item) =>
          !!item.href &&
          hrefMatchesCurrent(item.href, currentPathname, currentSearch),
      )
      .sort((a, b) => (b.href?.length ?? 0) - (a.href?.length ?? 0))[0] ??
    null;

  if (!activeItem) return [];

  const parent = topLevelItems.find((item) =>
    item.children?.some((child) => child.href === activeItem.href),
  );

  // Grouping nodes (e.g. "Contacts", "Portfolio") are keyed by `heading`
  // rather than `label` since they're never themselves a clickable nav
  // item — fall back to it here so the breadcrumb crumb isn't blank.
  return parent && parent.href !== activeItem.href
    ? [{ ...parent, label: parent.label ?? parent.heading }, activeItem]
    : [activeItem];
}
