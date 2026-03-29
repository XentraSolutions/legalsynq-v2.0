# Step 04 — Tenants List Page

## Summary

Implemented the Control Center Tenants List page as a production-quality Server Component backed by a realistic mock data stub. The UI is fully backend-ready — swapping the stub for a live endpoint requires a one-line change in `control-center-api.ts` with no page or component changes.

---

## Files Created

| File | Role |
|---|---|
| `apps/web/src/app/(control-center)/control-center/tenants/page.tsx` | Tenants list page (Server Component) |
| `apps/web/src/components/control-center/tenant-list-table.tsx` | Tenant table component |

## Files Updated

| File | What changed |
|---|---|
| `apps/web/src/types/control-center.ts` | Added `TenantType`, `TenantStatus` union types; added `type`, `status`, `primaryContactName` to `TenantSummary` |
| `apps/web/src/lib/control-center-api.ts` | Added `MOCK_TENANTS` (8 realistic tenants); replaced `tenants.list()` `serverApi.get()` call with `Promise.resolve()` stub supporting pagination and search filtering |

---

## Page: `/control-center/tenants`

**Pattern:** Mirrors `apps/web/src/app/(platform)/careconnect/referrals/page.tsx` exactly.

**Structure:**
- `requireCCPlatformAdmin()` at the top (Server Component guard)
- Reads `searchParams.page` and `searchParams.search`
- Calls `controlCenterServerApi.tenants.list({ page, pageSize: 20, search })` in a try/catch
- Title row: "Tenants" + disabled "Create Tenant" button (no functionality yet)
- Search bar: native `<form method="GET">` — wired to `?search=` query param, already functional with the stub's filter logic
- Error banner: red border box if fetch throws
- `<TenantListTable>` if result is non-null

---

## Table Component: `TenantListTable`

**Pattern:** Mirrors `components/careconnect/referral-list-table.tsx` exactly.

**Columns:**

| Column | Source field | Notes |
|---|---|---|
| Name | `displayName` + `code` | Code shown as secondary line in gray |
| Type | `type` | Human-formatted via `formatType()` (e.g. `LawFirm` → `Law Firm`) |
| Status | `status` | Color-coded badge: green=Active, gray=Inactive, red=Suspended |
| Primary Contact | `primaryContactName` | Plain text |
| Created | `createdAtUtc` | Formatted `Mon DD, YYYY` |
| Actions | — | `View →` link via `CCRouteBuilders.tenantDetail(id)` |

**Behaviors:**
- Empty state: centered "No tenants found." message
- Row hover: `hover:bg-gray-50 transition-colors`
- Pagination footer: shows "Showing X–Y of Z" + Previous/Next links via `?page=N` query params
- "View →" link uses indigo color to match CC theme (vs primary/blue in operator portal)

---

## Mock Data

8 realistic tenants covering all `TenantType` variants:

| Code | Name | Type | Status |
|---|---|---|---|
| HARTWELL | Hartwell & Associates | Law Firm | Active |
| MERIDIAN | Meridian Care Partners | Provider | Active |
| PINNACLE | Pinnacle Legal Group | Law Firm | Active |
| BLUEHAVEN | Blue Haven Recovery Services | Provider | Inactive |
| LEGALSYNQ | LegalSynq Platform | Corporate | Active |
| THORNFIELD | Thornfield & Yuen LLP | Law Firm | Active |
| NEXUSHEALTH | Nexus Health Network | Provider | Active |
| GRAYSTONE | Graystone Municipal Services | Government | Suspended |

The stub also supports in-memory search filtering across `displayName`, `code`, and `primaryContactName` — so the search bar is functional end-to-end even with mock data.

---

## Backend Wiring Instructions (for when Identity.Api is ready)

In `apps/web/src/lib/control-center-api.ts`, replace the `list()` implementation:

**Before (stub):**
```ts
list: (params: { page?: number; pageSize?: number; search?: string } = {}) => {
  // ... in-memory mock logic ...
  return Promise.resolve<PagedResponse<TenantSummary>>({ ... });
},
```

**After (live):**
```ts
list: (params: { page?: number; pageSize?: number; search?: string } = {}) =>
  serverApi.get<PagedResponse<TenantSummary>>(
    `/identity/api/admin/tenants${toQs(params as Record<string, unknown>)}`,
  ),
```

No changes needed in the page or table component.

---

## TypeScript

Zero errors confirmed after all changes.

---

## Next Step Recommendation

**Step 05 — Tenant Detail Page** (`/control-center/tenants/[id]`)

Implement:
- `app/(control-center)/control-center/tenants/[id]/page.tsx`
- `components/control-center/tenant-detail-card.tsx`
- Stub `controlCenterServerApi.tenants.getById(id)` with mock data
- Show: name, code, type, status, contact, created/updated dates, user count, org count
- Show: product entitlements table (which products are enabled for this tenant)
- Action buttons: Activate / Deactivate (wired to `controlCenterApi.tenants.activate/deactivate`)

**Step 06 — Tenant Users Page** (`/control-center/tenant-users`)

Parallel work that does not depend on Step 05.
