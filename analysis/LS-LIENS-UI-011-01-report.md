# LS-LIENS-UI-011-01: Provider Mode Backend Enforcement & Config Completion

## Feature ID
LS-LIENS-UI-011-01

## Objective
Complete Provider Mode by making it persistent, real, and enforced at the backend level. Add persistent provider mode to organization data, return real provider mode from org config API, and block sell-only backend workflows when org mode = manage.

## Current Gaps from UI-011 (Pre-Implementation)
- Backend endpoint `GET /api/organizations/my/config` always returned hardcoded `providerMode: "sell"`
- No DB column for provider mode on organization entity
- No backend enforcement — manage-mode orgs could still call sell-only APIs
- Activity/notification event categorization relied on client-side string pattern matching
- No admin API to change an org's provider mode

## Expected Backend Data Model Changes
- Add `ProviderMode` column (varchar(20), NOT NULL, default "sell") to `idt_Organizations` table
- Migration to backfill all existing orgs to "sell"

## Expected Enforcement Targets
- Offer creation (`POST /api/liens/offers`)
- Offer acceptance (`POST /api/liens/offers/{offerId}/accept`)
- BOS submission (`PUT /api/liens/bill-of-sales/{id}/submit`)
- BOS execution (`PUT /api/liens/bill-of-sales/{id}/execute`)
- BOS cancellation (`PUT /api/liens/bill-of-sales/{id}/cancel`)

---

## Implementation Log

### T001 — Gap Confirmation and Enforcement Audit
**Status:** COMPLETE

**Findings:**
1. **Org Config Endpoint** — `GET /api/organizations/my/config` in `AuthEndpoints.cs` (lines 121–158) hardcoded `providerMode = "sell"` in all three return paths (no org claim, org not found, org found).
2. **Organization Entity** — `Identity.Domain/Organization.cs` had no `ProviderMode` property. Properties were: Id, TenantId, Name, DisplayName, OrgType, OrganizationTypeId, IsActive, timestamps.
3. **JWT Claims** — `JwtTokenService.cs` emitted `org_id`, `org_type`, `org_type_id` but no `provider_mode` claim. Downstream services had no way to know the org's mode.
4. **ICurrentRequestContext** — Had `OrgId`, `OrgType`, `OrgTypeId` but no `ProviderMode`, `IsSellMode`, or `IsManageMode`.
5. **Sell-only endpoints identified:**
   - `POST /api/liens/offers` — offer creation
   - `POST /api/liens/offers/{offerId}/accept` — offer acceptance (triggers sale, BOS creation)
   - `PUT /api/liens/bill-of-sales/{id}/submit` — BOS submission
   - `PUT /api/liens/bill-of-sales/{id}/execute` — BOS execution
   - `PUT /api/liens/bill-of-sales/{id}/cancel` — BOS cancellation
6. **No backend enforcement** — all endpoints accepted requests regardless of org mode.

**Files reviewed:** `AuthEndpoints.cs`, `Organization.cs`, `OrganizationConfiguration.cs`, `JwtTokenService.cs`, `ICurrentRequestContext.cs`, `CurrentRequestContext.cs`, `LienOfferEndpoints.cs`, `BillOfSaleEndpoints.cs`

---

### T002 — Add Persistent Provider Mode to Organization Model
**Status:** COMPLETE

**Changes:**
1. Created `Identity.Domain/ProviderModes.cs` — static constants class:
   - `Sell = "sell"`, `Manage = "manage"`
   - `IsValid(mode)` — validates against allowed values
   - `Normalize(mode)` — normalizes to lowercase, defaults to "sell" for invalid values
   - `IsSellMode(mode)`, `IsManageMode(mode)` — convenience checks
2. Added `ProviderMode` property to `Organization.cs`:
   - `public string ProviderMode { get; private set; } = ProviderModes.Sell;`
   - Private setter enforces encapsulation
3. Added `SetProviderMode(mode, updatedByUserId)` method:
   - Normalizes input via `ProviderModes.Normalize()`
   - Updates timestamp
4. Updated `OrganizationConfiguration.cs`:
   - `.IsRequired().HasMaxLength(20).HasDefaultValue("sell")`
   - Seed data includes `ProviderMode = ProviderModes.Sell`

**Files created:** `apps/services/identity/Identity.Domain/ProviderModes.cs`
**Files modified:** `apps/services/identity/Identity.Domain/Organization.cs`, `apps/services/identity/Identity.Infrastructure/Data/Configurations/OrganizationConfiguration.cs`

---

### T003 — Create and Apply Migration
**Status:** COMPLETE

**Migration:** `20260414100001_AddOrganizationProviderMode.cs`
- Adds `ProviderMode` column (varchar(20), NOT NULL, default "sell") to `idt_Organizations`
- Backfill SQL: `UPDATE idt_Organizations SET ProviderMode = 'sell' WHERE ProviderMode IS NULL OR ProviderMode = ''`
- Down: drops the column
- Model snapshot updated to include `ProviderMode` property with default value

**Files created:** `apps/services/identity/Identity.Infrastructure/Persistence/Migrations/20260414100001_AddOrganizationProviderMode.cs`
**Files modified:** `apps/services/identity/Identity.Infrastructure/Persistence/Migrations/IdentityDbContextModelSnapshot.cs`

---

### T004 — Update Org Config API
**Status:** COMPLETE

Updated `GET /api/organizations/my/config` in `AuthEndpoints.cs`:
- Now queries `o.ProviderMode` from DB instead of hardcoding "sell"
- Uses `ProviderModes.Normalize(org.ProviderMode)` for the response
- Fallback paths (no org claim, org not found) still default to `ProviderModes.Sell`
- No change to response shape — frontend remains compatible

**Response contract (unchanged):**
```json
{
  "organizationId": "guid-string",
  "productCode": "LIENS",
  "settings": {
    "providerMode": "sell" | "manage"
  }
}
```

**Files modified:** `apps/services/identity/Identity.Api/Endpoints/AuthEndpoints.cs`

---

### T005 — Centralize Backend Mode Resolution
**Status:** COMPLETE

**A. JWT Claims Enhancement:**
- Added `provider_mode` claim to `JwtTokenService.GenerateToken()`:
  `claims.Add(new Claim("provider_mode", ProviderModes.Normalize(organization.ProviderMode)));`
- Emitted only when organization is present (same guard as `org_id`, `org_type`)

**B. ICurrentRequestContext Enhancement:**
Added three new members to `ICurrentRequestContext`:
- `string? ProviderMode { get; }` — raw claim value
- `bool IsSellMode { get; }` — true when mode is null, empty, or "sell"
- `bool IsManageMode { get; }` — true when mode is "manage"

**C. CurrentRequestContext Implementation:**
- `ProviderMode` reads `provider_mode` claim via `FindFirstValue`
- `IsSellMode` defaults to true when claim is missing (safe default)
- `IsManageMode` checks for explicit "manage" value

**D. ProviderModes Domain Utility:**
- `ProviderModes.Normalize()` — canonical normalization (invalid → "sell")
- `ProviderModes.IsSellMode()` / `ProviderModes.IsManageMode()` — static checks
- Used by JwtTokenService, AuthEndpoints, and admin endpoints

**Files created:** `apps/services/identity/Identity.Domain/ProviderModes.cs`
**Files modified:**
- `apps/services/identity/Identity.Infrastructure/Services/JwtTokenService.cs`
- `shared/building-blocks/BuildingBlocks/Context/ICurrentRequestContext.cs`
- `shared/building-blocks/BuildingBlocks/Context/CurrentRequestContext.cs`

---

### T006 — Enforce Mode on Sell-Only Workflows
**Status:** COMPLETE

**A. RequireSellModeFilter:**
Created `RequireSellModeFilter` as an `IEndpointFilter` in `BuildingBlocks.Authorization.Filters`:
- Reads `provider_mode` claim from JWT
- Blocks request with 403 + `PROVIDER_MODE_RESTRICTED` error code when mode ≠ sell
- Logs blocked attempts with userId, tenantId, method, path, providerMode
- Error response format:
  ```json
  {
    "error": {
      "code": "PROVIDER_MODE_RESTRICTED",
      "message": "This operation is not available in manage mode. Your organization is configured for internal lien management only."
    }
  }
  ```

**B. Extension Methods:**
Added `RequireSellMode()` extension methods for both `RouteHandlerBuilder` and `RouteGroupBuilder` in `ProductAuthorizationExtensions.cs`.

**C. Applied to Sell-Only Endpoints:**

| Endpoint | File | Effect |
|----------|------|--------|
| `POST /api/liens/offers` | `LienOfferEndpoints.cs` | Offer creation blocked in manage mode |
| `POST /api/liens/offers/{id}/accept` | `LienOfferEndpoints.cs` | Offer acceptance blocked in manage mode |
| `PUT /api/liens/bill-of-sales/{id}/submit` | `BillOfSaleEndpoints.cs` | BOS submission blocked in manage mode |
| `PUT /api/liens/bill-of-sales/{id}/execute` | `BillOfSaleEndpoints.cs` | BOS execution blocked in manage mode |
| `PUT /api/liens/bill-of-sales/{id}/cancel` | `BillOfSaleEndpoints.cs` | BOS cancellation blocked in manage mode |

**Note:** Read-only endpoints (offer search, BOS search, BOS get) are NOT gated — manage-mode orgs can still view historical data if they were previously in sell mode.

**Files created:** `shared/building-blocks/BuildingBlocks/Authorization/Filters/RequireSellModeFilter.cs`
**Files modified:**
- `shared/building-blocks/BuildingBlocks/Authorization/Filters/ProductAuthorizationExtensions.cs`
- `apps/services/liens/Liens.Api/Endpoints/LienOfferEndpoints.cs`
- `apps/services/liens/Liens.Api/Endpoints/BillOfSaleEndpoints.cs`

---

### T007 — Frontend Compatibility Validation
**Status:** COMPLETE

**Verification:**
1. **Org config API response shape** — unchanged. Frontend `OrgConfigResponseDto` expects `{ settings: { providerMode: string } }` — still compatible.
2. **BFF route** (`/api/org-config`) — unchanged, proxies to Identity endpoint transparently.
3. **Provider mode context** — `ProviderModeProvider` calls `fetchOrgConfig()` → BFF → Identity. Now returns real DB value instead of hardcoded "sell".
4. **Blocked action error handling** — Frontend already hides sell-only UI in manage mode, so 403 `PROVIDER_MODE_RESTRICTED` responses would only occur if someone manually calls the API. Standard error handling catches these.
5. **TypeScript compilation** — `tsc --noEmit` passes with zero errors.

**Admin endpoint added:**
- `PATCH /api/admin/organizations/{id}/provider-mode` — allows PlatformAdmin to change org mode
- Validates input against `ProviderModes.IsValid()`
- Returns 400 with `INVALID_PROVIDER_MODE` for invalid values

**Organization list/detail responses** — now include `providerMode` field in admin endpoints (ListOrganizations, GetOrganizationById, UpdateOrganization).

**Files modified:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` (admin endpoint + response updates)

---

### T008 — Activity / Notification Metadata Improvement
**Status:** DOCUMENTED (Limited Feasibility)

**Assessment:**
- The audit event model in `PlatformAuditEventService` uses string-based `EventCategory` and `EventType` fields. These are set by the calling service at event creation time.
- Adding a `sellModeOnly` boolean flag to audit events would require:
  1. Schema change in the Audit service
  2. All producers (Liens service) to emit the flag
  3. API changes to expose and filter by it
- This is a **separate feature** that should be its own task. The current client-side pattern matching (`filterActivityByMode()`) is sufficient for the current scope.

**Notification assessment:**
- Notification channels and statuses are operational concepts, not sell-specific
- No notification-specific changes needed for provider mode

**Current limitation:** Activity filtering uses client-side string matching (patterns: offer, bos, bill_of_sale, settlement, marketplace, sold, purchase). If new sell-only event types are added with different naming, the patterns must be updated.

---

### T009 — Validation and Regression
**Status:** COMPLETE

**Build verification:**
| Target | Result |
|--------|--------|
| Identity.Api | ✅ Build succeeded, 0 errors |
| BuildingBlocks | ✅ Build succeeded, 0 errors |
| Liens.Api | ✅ Build succeeded, 0 errors |
| Documents.Api | ✅ Build succeeded, 0 errors (1 unrelated warning) |
| Audit (PlatformAuditEventService) | ✅ Build succeeded, 0 errors |
| Next.js frontend (tsc --noEmit) | ✅ 0 errors |

**Sell mode verification:**
- All endpoints remain accessible when `provider_mode = "sell"` (or missing/null — safe default)
- Offer creation, acceptance, BOS submission/execution/cancellation all pass filter
- Read-only endpoints unaffected

**Manage mode verification:**
- `RequireSellModeFilter` blocks with 403 + `PROVIDER_MODE_RESTRICTED` for all 5 gated endpoints
- Structured error response enables clean frontend handling
- Logged at WARN level for monitoring

**Migration verification:**
- Column added with NOT NULL + default "sell"
- Backfill SQL ensures no null/empty ambiguity
- Model snapshot updated to include the column

---

## Executive Summary

### Backend Model Changes
| Change | Detail |
|--------|--------|
| New column | `ProviderMode` (varchar(20), NOT NULL, default "sell") on `idt_Organizations` |
| New domain entity | `ProviderModes` static class in `Identity.Domain` |
| New entity method | `Organization.SetProviderMode(mode, updatedByUserId)` |
| EF Configuration | Column constraint + default value + seed data |

### Migration Details
- **Migration name:** `20260414100001_AddOrganizationProviderMode`
- **Up:** AddColumn + backfill SQL
- **Down:** DropColumn
- **Snapshot:** Updated with `ProviderMode` property

### Org Config API Before/After
| Aspect | Before | After |
|--------|--------|-------|
| Source | Hardcoded "sell" | Read from `idt_Organizations.ProviderMode` |
| Normalization | None | `ProviderModes.Normalize()` — invalid → "sell" |
| Fallback | Always "sell" | Still "sell" for missing org/claim |
| Response shape | Unchanged | Unchanged |

### JWT Claim Addition
| Claim | Value | When |
|-------|-------|------|
| `provider_mode` | "sell" or "manage" | Emitted when organization is present in token |

### Enforcement Points
| Endpoint | Method | Guard | Error Code |
|----------|--------|-------|------------|
| `/api/liens/offers` | POST | `RequireSellMode()` | `PROVIDER_MODE_RESTRICTED` |
| `/api/liens/offers/{id}/accept` | POST | `RequireSellMode()` | `PROVIDER_MODE_RESTRICTED` |
| `/api/liens/bill-of-sales/{id}/submit` | PUT | `RequireSellMode()` | `PROVIDER_MODE_RESTRICTED` |
| `/api/liens/bill-of-sales/{id}/execute` | PUT | `RequireSellMode()` | `PROVIDER_MODE_RESTRICTED` |
| `/api/liens/bill-of-sales/{id}/cancel` | PUT | `RequireSellMode()` | `PROVIDER_MODE_RESTRICTED` |

### Admin API
| Endpoint | Purpose |
|----------|---------|
| `PATCH /api/admin/organizations/{id}/provider-mode` | Set org provider mode (PlatformAdmin only) |

### Blocked Workflow List (Manage Mode)
1. ❌ Create offer
2. ❌ Accept offer / initiate sale
3. ❌ Submit BOS for execution
4. ❌ Execute BOS
5. ❌ Cancel BOS

### Allowed in Both Modes
1. ✅ View/search offers (historical)
2. ✅ View/search BOS (historical)
3. ✅ Create/manage liens
4. ✅ Create/manage cases
5. ✅ Servicing tasks
6. ✅ Document management
7. ✅ Contacts
8. ✅ Notifications

### Frontend Compatibility
- ✅ No frontend changes needed
- ✅ Org config API response shape unchanged
- ✅ BFF proxy transparent
- ✅ Provider mode context resolves correctly from real API
- ✅ TypeScript compilation passes

### Activity/Notification Metadata Notes
- Client-side activity filtering (`filterActivityByMode()`) remains unchanged
- Server-side event categorization flag is a future improvement (separate task)
- Pattern matching is sufficient for current event types

### Risks/Issues
1. **JWT re-login required** — Mode change takes effect on next login (token refresh). Active sessions retain old mode until token expires or user re-authenticates.
2. **Read-only endpoints ungated** — Historical offers/BOS remain visible in manage mode. This is intentional — orgs that switch modes should retain access to their data.
3. **Activity filter is pattern-based** — Client-side filtering uses string matching. New sell-only event types need pattern updates.
4. **No mode-switching webhook** — When admin changes mode, no notification is sent to invalidate active sessions. Users must re-login.

### Validation Results
- All services build successfully (0 errors)
- TypeScript frontend compiles cleanly
- Migration applies cleanly with safe backfill
- Sell mode fully intact
- Manage mode blocks all sell-only mutations

### Files Created
| File | Purpose |
|------|---------|
| `Identity.Domain/ProviderModes.cs` | Mode constants, validation, normalization |
| `Migrations/20260414100001_AddOrganizationProviderMode.cs` | DB migration |
| `BuildingBlocks/Authorization/Filters/RequireSellModeFilter.cs` | Endpoint filter for sell-mode enforcement |

### Files Modified
| File | Changes |
|------|---------|
| `Identity.Domain/Organization.cs` | Added `ProviderMode` property + `SetProviderMode()` method |
| `Identity.Infrastructure/.../OrganizationConfiguration.cs` | Column config + seed data |
| `Identity.Infrastructure/.../IdentityDbContextModelSnapshot.cs` | Model snapshot update |
| `Identity.Infrastructure/Services/JwtTokenService.cs` | Added `provider_mode` JWT claim |
| `Identity.Api/Endpoints/AuthEndpoints.cs` | Org config reads real mode from DB |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Admin provider mode PATCH + response updates |
| `BuildingBlocks/Context/ICurrentRequestContext.cs` | Added ProviderMode, IsSellMode, IsManageMode |
| `BuildingBlocks/Context/CurrentRequestContext.cs` | Implemented provider mode properties |
| `BuildingBlocks/.../ProductAuthorizationExtensions.cs` | Added RequireSellMode() extensions |
| `Liens.Api/Endpoints/LienOfferEndpoints.cs` | Applied RequireSellMode() to mutations |
| `Liens.Api/Endpoints/BillOfSaleEndpoints.cs` | Applied RequireSellMode() to mutations |
