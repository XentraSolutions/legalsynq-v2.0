# CC-UX-NAV-001 — Control Center Navigation Hub Refactor

**Status:** IN PROGRESS  
**Date:** 2026-04-23  

---

## 1. Objective

Replace the long scrollable left sidebar in the Control Center with:
1. A compact left rail (Home + contextual links only)
2. A navigation hub landing page with category cards derived from `CC_NAV`
3. URL-driven group selection (`/?group=<slug>`) for refresh persistence

---

## 2. Codebase Analysis

### nav.ts
- `CC_NAV: NavSection[]` — 14 sections, ~52 nav items total
- Sections: OVERVIEW, PLATFORM, IDENTITY, RELATIONSHIPS, PRODUCT RULES, CARECONNECT, TENANTS, NOTIFICATIONS, AUDIT, TRACEABILITY, OPERATIONS, CATALOG, SYSTEM
- OVERVIEW contains only Dashboard (`/`) — this becomes the Home link
- Items carry `badge?: 'LIVE' | 'IN PROGRESS' | 'MOCKUP' | 'NEW'`

### cc-sidebar.tsx (before)
- Renders all 14 `CC_NAV` sections with every item visible
- Section headings are collapsible buttons with chevrons
- Entire list is `overflow-y-auto` — long scroll required
- Supports sidebar collapse to 52px icon-only mode (Ctrl+[)

### cc-shell.tsx
- Server component; renders top nav bar + `<CCSidebar />` + `<main>`
- No changes needed structurally; sidebar is self-contained

### app/page.tsx (before)
- Server component fetching stats: tenants, users, monitoring, audit, support
- Renders `SystemStatusCard`, `StatCard` grid, `TenantBreakdownCard`, `SupportSummaryCard`, `RecentAuditTable`, and 3 quick `LinkCard` items
- No group navigation hub

---

## 3. Architecture Decisions

| Decision | Choice |
|---|---|
| URL state | `/?group=<slug>` query param via `router.push` |
| Group slug format | lowercase + hyphens (`PRODUCT RULES` → `product-rules`) |
| NavHub state | Client component using `useSearchParams()` |
| Sidebar contextual | Pathname match via `getSectionForPathname()` utility |
| Source of truth | `CC_NAV` only — no duplicate nav definitions |

---

## 4. Files Changed

| File | Change Type |
|---|---|
| `apps/control-center/src/lib/nav-utils.ts` | NEW — slug/lookup/group-model utilities |
| `apps/control-center/src/components/dashboard/navigation-group-grid.tsx` | NEW — client NavHub component |
| `apps/control-center/src/components/shell/cc-sidebar.tsx` | REFACTORED — compact contextual sidebar |
| `apps/control-center/src/app/page.tsx` | UPDATED — NavHub section added |

---

## 5. Component Summary

### `nav-utils.ts`
- `slugify(heading)` — heading → URL slug
- `getSectionBySlug(slug)` — reverse lookup
- `getSectionForPathname(pathname)` — determines owning section for contextual sidebar
- `getNavGroupModels()` — builds presentable group model array for cards
- `GROUP_ICON_MAP` — explicit group → icon mapping

### `NavigationGroupGrid` (client)
- Reads `?group` via `useSearchParams()`
- Renders one card per `CC_NAV` group (excludes OVERVIEW)
- Clicking a card: `router.push('/?group=<slug>')`; clicking selected again deselects
- Renders `GroupDetailPanel` when a group is selected
- Wrapped in `<Suspense>` boundary for streaming compatibility

### `CCSidebar` (refactored)
- Home link always rendered at top
- Contextual section: only the section matching the current pathname
- On `/` (home): sidebar shows only Home
- Collapse toggle and icon-only mode preserved
- No more full multi-section list rendering

### `DashboardPage` (updated)
- Navigation Hub section inserted between page intro and metric cards
- Existing stat cards, breakdowns, audit table all preserved

---

## 6. Route Behavior

| Route | Sidebar content | Page body |
|---|---|---|
| `/` (no group) | Home only | Stats + group cards + "select a category" prompt |
| `/?group=audit` | Home only | Stats + group cards (audit highlighted) + Audit items panel |
| `/synqaudit/*` | Home + Audit section links | Normal page content |
| `/tenants` | Home + Tenants section links | Normal page content |
| `/notifications/*` | Home + Notifications section links | Normal page content |
| Any other route | Home + matching section links | Normal page content |

---

## 7. Validation Results

_To be completed after implementation._

---

## 8. Known Gaps / Follow-ups

- Recently used links / pinned shortcuts: deferred (out of scope per spec)
- Group-level health/status badges on cards: not implemented (non-blocking)
- Keyboard shortcut for cycling groups on dashboard: not implemented
