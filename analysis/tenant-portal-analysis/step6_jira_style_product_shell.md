# Step 6 — Jira-Style Product Shell

## 1. Executive Summary

The tenant portal layout has been refactored to a Jira-inspired product-aware shell. The top bar now contains a persistent product switcher that shows only products the user can access. The left sidebar dynamically re-renders to show only the navigation items for the selected product. Product selection is automatically inferred from the current pathname — no user gesture required. All existing routes, auth guards, and product pages are unchanged.

---

## 2. Current Layout Findings

| Concern | Before |
|---|---|
| Shell definition | `AppShell` in `components/shell/app-shell.tsx` — composes `TopBar` + `Sidebar` |
| Top bar | Light white bar (`bg-white`); `ProductSwitcher` showed all product tabs but had no active state |
| Sidebar | Showed **all** accessible product groups simultaneously — CareConnect + Fund + Lien stacked vertically |
| Product active state | Not tracked; `ProductSwitcher.activeGroupId` was never wired to the actual pathname |
| Product access | `buildNavGroups(session)` in `lib/nav.ts` is the correct role-filtered source of truth |
| Route groups | `(platform)` wraps all product routes; `(admin)` wraps admin routes |
| Auth | `requireOrg()` guard in `(platform)/layout.tsx`; `requireProductRole()` in individual pages |

---

## 3. Product Navigation Model

Defined in `apps/web/src/lib/product-config.ts`:

| Product ID | Label | Route Prefix | Icon | Required Roles |
|---|---|---|---|---|
| `careconnect` | CareConnect | `/careconnect` | Heart | `CARECONNECT_REFERRER`, `CARECONNECT_RECEIVER` |
| `fund` | SynqFund | `/fund` | Banknote | `SYNQFUND_REFERRER`, `SYNQFUND_FUNDER` |
| `lien` | SynqLien | `/lien` | FileStack | `SYNQLIEN_SELLER`, `SYNQLIEN_BUYER`, `SYNQLIEN_HOLDER` |
| `admin` | Admin | `/admin` | Settings | — (gated by `isTenantAdmin` / `isPlatformAdmin` in `buildNavGroups`) |

Nav items per product remain defined in `lib/nav.ts` via `buildNavGroups()`, which is the existing role-filtered function. No nav logic was duplicated.

---

## 4. Files Created

| File | Purpose |
|---|---|
| `apps/web/src/lib/product-config.ts` | Central product definitions: id, label, iconName, routePrefix, requiredRoles. Also exports `inferProductIdFromPath()` and `getProductDef()`. |
| `apps/web/src/contexts/product-context.tsx` | React context (`ProductProvider` + `useProduct()`) that infers `activeProductId`, `availableGroups`, and `activeGroup` from the current pathname and session. |

---

## 5. Files Modified

| File | Changes |
|---|---|
| `apps/web/src/components/shell/app-shell.tsx` | Wraps shell in `<ProductProvider>` so `TopBar` and `Sidebar` share derived product state. Removed direct `TopBar`/`Sidebar` imports that used `buildNavGroups`; those are now mediated through context. |
| `apps/web/src/components/shell/top-bar.tsx` | Full redesign. Dark `bg-slate-900` top bar; product tabs with Lucide icons and active state; org context section; Control Center link (platform admin only); sign-out button. Uses `useProduct()` to determine which tab is active. |
| `apps/web/src/components/shell/sidebar.tsx` | Full redesign. Now renders **only** `activeGroup.items` from `useProduct()`. Includes product icon + name header. Active nav item shows left-border accent + blue highlight. Empty sidebar shown if no product matches the current route. |

**Not modified:**
- `lib/nav.ts` — unchanged; still the sole source of role-filtered nav groups.
- `types/index.ts` — unchanged; `NavGroup`, `NavItem`, `PlatformSession`, `ProductRole` all preserved.
- `lib/auth-guards.ts` — unchanged; all server-side route guards intact.
- All page files — unchanged.

**No longer imported (but kept):**
- `components/shell/product-switcher.tsx` — superseded by inline product tab rendering in `top-bar.tsx`. File retained to avoid any unexpected import errors; can be removed in a future cleanup.

---

## 6. Routing Behavior

- **Deep-link into a product route** → `inferProductIdFromPath(pathname)` extracts the product ID from the path prefix → correct product tab is highlighted in top bar → correct product nav shown in sidebar.
- **Selecting a product tab** → navigates to `group.items[0].href` (the first nav item for that product).
- **`/dashboard`** → already redirects to the first available product via `buildNavGroups`. No change required.
- **`/admin/*` routes** → Admin tab appears in top bar only for `isTenantAdmin`/`isPlatformAdmin` users; sidebar shows admin nav items when path is under `/admin`.
- **Route with no product prefix** (e.g. `/no-org`, `/login`) → `activeProductId` is null; sidebar renders an empty placeholder. These pages are not inside the `AppShell` anyway.

---

## 7. Session / Product Access Behavior

- `buildNavGroups(session)` (unchanged) remains the single role-gating function. Only products with at least one matching `productRoles` entry appear in `availableGroups`.
- `ProductProvider` calls `buildNavGroups` with the live client session, so the available tabs update immediately after login without a page reload.
- The admin tab uses `buildNavGroups`'s own `isTenantAdmin || isPlatformAdmin` check — not re-implemented.
- Backend authorization is unaffected; this is purely a UX layout change.

---

## 8. Styling / Layout Notes

| Element | Style |
|---|---|
| Top bar background | `bg-slate-900` (dark navy — Jira-inspired) |
| Top bar height | `h-12` (48 px — compact) |
| Product tab — inactive | `text-slate-300 hover:text-white hover:bg-white/10` |
| Product tab — active | `bg-blue-600 text-white` |
| Sidebar background | `bg-white` with `border-r border-gray-200` |
| Sidebar width | `220px` (same as before) |
| Nav item — inactive | `text-gray-600 hover:bg-gray-100`, transparent left border |
| Nav item — active | `bg-blue-50 text-blue-700 border-l-2 border-blue-600` |
| Product icon | Lucide icons (`Heart`, `Banknote`, `FileStack`, `Settings`) at 14–15 px |
| Org badge | Placed between branding and product tabs; shows org type + name |

Primary color CSS variable (`--color-primary`) is preserved for product pages. The top bar and sidebar active states use explicit Tailwind `blue-*` values since they live on fixed-color backgrounds.

---

## 9. Validation Results

| Check | Result |
|---|---|
| Top bar renders | ✓ Compiles; dark top bar with product tabs |
| Only allowed products appear | ✓ Driven by `buildNavGroups` — no role = no tab |
| Selecting a product updates sidebar | ✓ Tab click routes to product; `inferProductIdFromPath` matches; sidebar re-renders |
| Sidebar links navigate correctly | ✓ All hrefs from `buildNavGroups` are unchanged |
| Deep-linking selects correct product | ✓ `inferProductIdFromPath` fires on every render via `usePathname()` |
| Existing pages still render | ✓ No routes renamed; no layout guards modified |
| No broken imports / layout errors | ✓ `product-switcher.tsx` not deleted; all shell imports updated |

---

## 10. Remaining Limitations

- **`product-switcher.tsx`** is now dead code. It can safely be deleted in a future pass.
- The `iconName` field in `NavGroup` (in `lib/nav.ts`) and `NavItem` types are still string-based and not used for rendering nav item icons. Icons appear only in the top bar tabs and sidebar product header. A future enhancement could add icons to individual nav items.
- If a user navigates to `/careconnect/providers` before the session loads (e.g. very slow network), the sidebar will show an empty placeholder briefly until the session resolves.
- The active tab uses `bg-blue-600` (hardcoded Tailwind class) rather than `var(--color-primary)`. This is intentional: on the dark `bg-slate-900` top bar, the tenant primary color could clash or be unreadable. A future enhancement could derive a safe contrast color.
