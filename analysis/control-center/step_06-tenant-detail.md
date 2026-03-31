# Step 06 – Tenant Detail Page

## Files Created

| File | Description |
|---|---|
| `apps/control-center/src/app/tenants/[id]/page.tsx` | Server Component page — auth guard, data fetch, error/not-found states, page header |
| `apps/control-center/src/components/tenants/tenant-detail-card.tsx` | Server Component — sections B (core info), C (stats row), D (product entitlements table) |
| `apps/control-center/src/components/tenants/tenant-actions.tsx` | Client Component — Activate / Deactivate / Suspend action buttons |
| `apps/control-center/src/lib/routes.ts` | Centralized route constants and builders (Routes.tenants, Routes.tenantDetail(id), etc.) |

---

## Files Updated

| File | Change |
|---|---|
| `apps/control-center/src/types/control-center.ts` | Updated `TenantDetail` to extend `TenantSummary`; added `email?`, `updatedAtUtc`, `activeUserCount`, `linkedOrgCount?`, `productEntitlements`; replaced `TenantProductSummary` with updated `ProductEntitlementSummary`; added `EntitlementStatus` type |
| `apps/control-center/src/lib/control-center-api.ts` | Replaced live-call `getById` stub with full mock implementation; added `buildTenantDetail()`, `buildProductEntitlements()`, `DETAIL_EXTRAS`, `ENABLED_BY_TYPE`, `ALL_PRODUCTS` helpers |
| `apps/control-center/src/components/tenants/tenant-list-table.tsx` | Updated `View →` link to use `Routes.tenantDetail(tenant.id)` instead of hardcoded string |

---

## Components Added

### `TenantDetailCard` (Server Component)
`src/components/tenants/tenant-detail-card.tsx`

Composed of three sections:

**Section C — Stats row**
Four stat cards in a 2×2 / 4-col responsive grid:
- Total Users
- Active Users
- Linked Orgs
- Products Enabled (e.g. "3 / 6")

**Section B — Core Information card**
White card with labeled rows:
- Tenant Type
- Primary Contact
- Contact Email (linked `mailto:`, only shown if present)
- Tenant Code (monospace pill)
- Created date
- Last Updated date

**Section D — Product Entitlements table**
White card with overflow-x table showing all 6 LegalSynq products:
- SynqFund, SynqLien, SynqBill, SynqRx, SynqPayout, CareConnect
- Enabled column: Yes / No
- Status column: color-coded pill (green=Enabled, gray=Disabled, yellow=NotProvisioned)

### `TenantActions` (Client Component)
`src/components/tenants/tenant-actions.tsx`

Three action buttons, smart-disabled based on `currentStatus`:
- **Activate** — disabled when already Active (green solid)
- **Deactivate** — disabled when already Inactive (neutral border)
- **Suspend** — disabled when already Suspended (red border)

Each onClick contains a `// TODO` comment pointing to the exact backend endpoint.

### Sub-components (internal to their file)
- `StatCard` — single metric display card
- `InfoRow` — label + value row inside the core info card
- `EntitlementRow` — single product row in the entitlements table
- `EntitlementStatusBadge` — color-coded pill for entitlement status
- `StatusBadge` — color-coded pill for tenant status (in the page header)
- `ActionButton` — typed variant button for TenantActions

---

## API Stubs Added

### `controlCenterServerApi.tenants.getById(id)`
`src/lib/control-center-api.ts`

```ts
// TODO: replace with GET /identity/api/admin/tenants/{id}
getById: (id: string): Promise<TenantDetail | null> => {
  const summary = MOCK_TENANTS.find(t => t.id === id);
  if (!summary) return Promise.resolve(null);
  return Promise.resolve(buildTenantDetail(summary));
},
```

**Mock enrichment helpers:**

| Helper | Purpose |
|---|---|
| `ALL_PRODUCTS` | Ordered list of all 6 product codes + names |
| `ENABLED_BY_TYPE` | Default enabled products per TenantType |
| `DETAIL_EXTRAS` | Per-tenant code: email, updatedAtUtc, activeUserCount |
| `buildProductEntitlements()` | Generates all 6 entitlements for a tenant; LEGALSYNQ gets all enabled |
| `buildTenantDetail()` | Merges TenantSummary + DETAIL_EXTRAS + entitlements into TenantDetail |

---

## Type Changes

### `EntitlementStatus` (new)
```ts
export type EntitlementStatus = 'Enabled' | 'Disabled' | 'NotProvisioned';
```

### `ProductEntitlementSummary` (updated)
Old shape had `tenantId`, `tenantCode`, `productId`, `isEnabled` — these were oriented toward a platform-wide list view. Updated to the simpler per-tenant shape:
```ts
export interface ProductEntitlementSummary {
  productCode:   string;
  productName:   string;
  enabled:       boolean;
  status:        EntitlementStatus;
  enabledAtUtc?: string;
}
```

### `TenantDetail` (updated)
Now extends `TenantSummary` properly:
```ts
export interface TenantDetail extends TenantSummary {
  email?:              string;
  updatedAtUtc:        string;
  activeUserCount:     number;
  linkedOrgCount?:     number;
  productEntitlements: ProductEntitlementSummary[];
}
```
Removed the old flat-field duplication (`id`, `code`, `displayName`, etc.) — all inherited from `TenantSummary`.

---

## Routes Helper

`src/lib/routes.ts` — `Routes` const object used everywhere:

```ts
Routes.tenants               // '/tenants'
Routes.tenantDetail(id)      // '/tenants/:id'
Routes.tenantUsers_          // '/tenant-users' (platform-wide)
Routes.roles                 // '/roles'
Routes.products              // '/products'
Routes.auditLogs             // '/audit-logs'
Routes.monitoring            // '/monitoring'
Routes.settings              // '/settings'
```

Updated `TenantListTable` to use `Routes.tenantDetail(tenant.id)` — eliminating the one remaining hardcoded path string.

---

## UI Patterns Used

| Pattern | Applied in |
|---|---|
| White cards with `border border-gray-200 rounded-lg` | Detail card sections, stat cards |
| `bg-gray-50` section headers with `uppercase tracking-wide text-xs` | Core info + entitlements card headers |
| `divide-y divide-gray-100` for row separation | Info rows, table rows |
| Color-coded border badges | Status badge, entitlement status pill |
| Responsive grid `grid-cols-2 sm:grid-cols-4` | Stats row |
| `overflow-x-auto` table wrapper | Product entitlements table |
| `hover:bg-gray-50 transition-colors` | Table rows |
| `disabled:opacity-40 disabled:cursor-not-allowed` | Action buttons |
| Monospace code pill | Tenant code display |
| `mailto:` anchor with indigo link style | Contact email |

All styling is Tailwind utility classes only. No new UI library dependencies.

---

## TODOs for Backend Integration

| Location | TODO |
|---|---|
| `control-center-api.ts` `getById` | Replace with `serverApi.get<TenantDetail>('/identity/api/admin/tenants/${id}')` |
| `tenant-actions.tsx` Activate button | `POST /api/identity/api/admin/tenants/{id}/activate` via BFF proxy |
| `tenant-actions.tsx` Deactivate button | `POST /api/identity/api/admin/tenants/{id}/deactivate` via BFF proxy |
| `tenant-actions.tsx` Suspend button | `POST /api/identity/api/admin/tenants/{id}/suspend` via BFF proxy |

After backend wiring of `getById`, add `router.refresh()` in `TenantActions` after each mutation so the Server Component re-fetches and the status badge updates without a full page reload.

---

## TypeScript

Zero errors confirmed (`tsc --noEmit` clean on all files in `apps/control-center`).

---

## Any Issues or Assumptions

1. **`TenantProductSummary` removed** — The old type is no longer referenced anywhere. It has been replaced by the simpler `ProductEntitlementSummary`. If other parts of the codebase reference `TenantProductSummary`, they will need updating — but at this point the type was only defined, never consumed outside the types file.

2. **Six fixed products** — The mock hardcodes 6 products (SynqFund, SynqLien, SynqBill, SynqRx, SynqPayout, CareConnect). The live backend may return a dynamic list. `buildProductEntitlements()` can be replaced with a direct server response — `TenantDetail.productEntitlements` is already typed as `ProductEntitlementSummary[]`, so the page and card components need no changes.

3. **`linkedOrgCount` fallback** — If `linkedOrgCount` is absent (optional field), the stats card falls back to `orgCount` from `TenantSummary`. Both fields are present in mock data.

4. **Action buttons alert placeholders** — The Activate/Deactivate/Suspend buttons currently call `alert()` as a temporary placeholder. These will be replaced with `fetch` calls to BFF routes and a `router.refresh()` once backend endpoints exist.

5. **`LEGALSYNQ` all-products** — The mock grants all products to the LEGALSYNQ corporate tenant (the platform itself). This is a reasonable assumption for demonstration; the live backend will return actual entitlement records.
