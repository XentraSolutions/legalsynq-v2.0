# Step 11 — Product Entitlements Management

**Date**: 2026-03-29
**App**: `apps/control-center`
**Status**: Complete — 0 TypeScript errors

---

## Objective

Implement the first editable configuration surface per tenant inside the Control Center:
a **Product Entitlements** panel on the Tenant Detail page where platform admins can
enable or disable products for any tenant using toggle switches.

---

## Changes Made

### 1. Types — `apps/control-center/src/types/control-center.ts`

| Change | Detail |
|--------|--------|
| Added `ProductCode` union type | `'SynqFund' \| 'SynqLien' \| 'SynqBill' \| 'SynqRx' \| 'SynqPayout' \| 'CareConnect'` |
| Updated `EntitlementStatus` | `'Active' \| 'Disabled'` (spec-aligned; was `'Enabled' \| 'Disabled' \| 'NotProvisioned'`) |
| `ProductEntitlementSummary.productCode` | Now typed as `ProductCode` (was `string`) |

### 2. API — `apps/control-center/src/lib/control-center-api.ts`

- `ALL_PRODUCTS` codes updated to spec values: `'SynqFund'`, `'SynqLien'`, `'SynqBill'`, `'SynqRx'`, `'SynqPayout'`, `'CareConnect'`
- `ENABLED_BY_TYPE` updated to use `ProductCode` union values
- `buildProductEntitlements` now accepts `tenantId`; consults override store before defaults; uses `'Active'`/`'Disabled'` status
- `ENTITLEMENT_OVERRIDES` — module-level `Map<string, Map<ProductCode, boolean>>` persists edits within the server process
- `getEntitlementOverrides(tenantId)` — helper that lazily initialises a per-tenant map
- `tenants.updateEntitlement(tenantId, productCode, enabled)` added:
  - Writes override to in-memory store
  - Returns updated `ProductEntitlementSummary`
  - `// TODO: replace with POST /identity/api/admin/tenants/{id}/entitlements`

### 3. TenantDetailCard — `apps/control-center/src/components/tenants/tenant-detail-card.tsx`

- Removed static read-only entitlements table (section D) — superseded by the interactive panel
- Removed unused sub-components `EntitlementRow` and `EntitlementStatusBadge`
- Removed unused `ProductEntitlementSummary` / `EntitlementStatus` imports

### 4. Server Action — `apps/control-center/src/app/tenants/[id]/actions.ts` *(new)*

```ts
'use server';

export async function updateProductEntitlement(
  tenantId:    string,
  productCode: ProductCode,
  enabled:     boolean,
): Promise<UpdateEntitlementResult>
```

Calls `controlCenterServerApi.tenants.updateEntitlement`; returns `{ success, entitlement?, error? }`.

### 5. ProductEntitlementsPanel — `apps/control-center/src/components/tenants/product-entitlements-panel.tsx` *(new)*

- `'use client'` component
- 3-column responsive product card grid (1 col mobile → 2 col sm → 3 col lg)
- Each card displays: icon, product name, short description, toggle switch, status badge
- Toggle calls `updateProductEntitlement` via `useTransition` (no blocking)
- **Optimistic UI**: state flips immediately on click; reverts if the action returns `success: false`
- Per-product loading spinner overlaid on the toggle while in-flight
- Dismissible error banner appears on failure
- Panel header shows a live `N of 6 active` counter

Product metadata:

| ProductCode | Icon | Description |
|-------------|------|-------------|
| SynqFund | 💰 | Litigation funding management and case financing |
| SynqLien | ⚖️ | Medical lien tracking and settlement workflows |
| SynqBill | 🧾 | Billing, invoicing, and fee management |
| SynqRx | 💊 | Prescription and pharmacy benefit coordination |
| SynqPayout | 📤 | Disbursement and payout processing |
| CareConnect | 🏥 | Care coordination and provider network management |

### 6. Tenant Detail Page — `apps/control-center/src/app/tenants/[id]/page.tsx`

- Imported `ProductEntitlementsPanel`
- Rendered below `TenantDetailCard`, passing `tenantId` and `entitlements` from the server-fetched `TenantDetail`

---

## Architecture

```
TenantDetailPage (Server Component)
  └─ controlCenterServerApi.tenants.getById(id)   ← fetch tenant + entitlements
  └─ TenantDetailCard                             ← stats row + core info (Server)
  └─ ProductEntitlementsPanel                     ← interactive toggle grid (Client)
       └─ updateProductEntitlement()              ← Server Action
            └─ controlCenterServerApi.tenants.updateEntitlement(...)
                 └─ ENTITLEMENT_OVERRIDES Map     ← in-memory mock store
```

---

## Live-endpoint wiring (one-line change)

When `POST /identity/api/admin/tenants/{id}/entitlements` is ready, replace the
body of `tenants.updateEntitlement` in `control-center-api.ts`:

```ts
// Replace mock body with:
return serverApi.post(`/identity/api/admin/tenants/${tenantId}/entitlements`, {
  productCode,
  enabled,
});
```

No changes needed in any component, action, or page.

---

## TypeScript verification

```
cd apps/control-center && tsc --noEmit
# → no output (0 errors)
```
