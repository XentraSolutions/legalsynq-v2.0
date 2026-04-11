# LS-COR-AUT-006A — Residual Legacy Closure + Validation Hardening

**Status**: COMPLETE  
**Date**: 2026-04-10  
**Depends on**: LS-COR-AUT-006 (Legacy Group Merge Logic Removal)

---

## Objective

Complete closure of the legacy authorization model by fixing broken claim consumers, hardening the Fund service authorization policies, removing residual legacy runtime queries, enforcing role-model boundaries, and adding validation tests.

---

## Changes Summary

### T001 — Frontend ProductRole Constants (COMPLETE)

**Files**: `apps/web/src/types/index.ts`, `apps/control-center/src/types/index.ts`

Updated all `ProductRole` constants from bare role codes to unified `PRODUCT:Role` format matching backend JWT `product_roles` claims:
- `CareConnectReferrer: 'CARECONNECT_REFERRER'` → `'SYNQ_CARECONNECT:CARECONNECT_REFERRER'`
- `SynqFundReferrer: 'SYNQFUND_REFERRER'` → `'SYNQ_FUND:SYNQFUND_REFERRER'`
- All downstream `.includes()` checks, `filterNavByRoles`, `requireProductRole` middleware now work correctly with the prefixed format.

Also fixed pre-existing type gaps uncovered during build: `ApiResponse`, `TenantBranding`, `NavGroup.icon`, `NavItem.badgeKey`, optional `enabledProducts` null-safety.

### T002 — Fund Service Product-Role Policies (COMPLETE)

**File**: `apps/services/fund/Fund.Api/Program.cs`

Replaced broken `RequireRole(ProductRoleCodes.X)` authorization policies with claim-based checks:
- `CanReferFund` → `RequireClaim("product_roles", "SYNQ_FUND:SYNQFUND_REFERRER")`
- `CanFundApplications` → `RequireClaim("product_roles", "SYNQ_FUND:SYNQFUND_FUNDER")`

Note: These policies are defined but not yet applied to endpoints (all Fund endpoints currently use `AuthenticatedUser`). They are ready for use when Fund endpoint authorization is tightened.

### T003 — Legacy GroupMemberships Runtime Queries Removed (COMPLETE)

**File**: `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`

- **ListUsers**: Replaced `db.GroupMemberships.Count(g => g.UserId == u.Id)` with `db.AccessGroupMemberships.Count(am => am.UserId == u.Id && am.MembershipStatus == MembershipStatus.Active)`.
- **GetUser**: Replaced `db.GroupMemberships` join query with `db.AccessGroupMemberships` join using correct entity properties (`GroupId`, `MembershipStatus` enum, `AddedAtUtc`). Response field renamed from `groupMemberships` to `accessGroups`.

No runtime code now references `TenantGroups` or `GroupMemberships`.

### T004 — Legacy Entities Deprecated (COMPLETE)

**Files**: `Identity.Domain/TenantGroup.cs`, `Identity.Domain/GroupMembership.cs`, `Identity.Infrastructure/Data/IdentityDbContext.cs`

- Added `[Obsolete("LS-COR-AUT-006A: ...")]` attributes to both entity classes.
- Added `#pragma warning disable/restore CS0618` around DbSet declarations in `IdentityDbContext` to suppress obsolete warnings while retaining EF migration compatibility.
- DbSets kept active solely for migration history — no runtime reads or writes.

### T005 — ScopedRoleAssignment Boundary Documentation (COMPLETE)

**File**: `apps/services/identity/Identity.Domain/ScopedRoleAssignment.cs`

Documented the intentional dual-boundary role model:
- **ScopedRoleAssignment** (GLOBAL scope) → system/admin roles (`PlatformAdmin`, `TenantAdmin`) emitted as `role` JWT claims. Also supports fine-grained runtime authorization checks via `ScopedAuthorizationService` across all scope types.
- **UserRoleAssignment / GroupRoleAssignment** → product-scoped roles emitted as `product_roles` JWT claims via `EffectiveAccessService` in `PRODUCT:Role` format.

### T006 — Validation Tests (COMPLETE)

**File**: `shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/ProductRoleClaimExtensionsTests.cs`

20 xUnit tests covering `ProductRoleClaimExtensions`:
- `HasProductAccess` with prefixed claims, bare codes (rejected), partial product codes (rejected), case-insensitive matching, PlatformAdmin/TenantAdmin bypass
- `HasProductRole` with correct/wrong/cross-product role codes, multiple allowed roles, bare codes (rejected), admin bypass, case-insensitive
- `GetProductRoles` returns all claims
- `IsTenantAdminOrAbove` for PlatformAdmin, TenantAdmin, StandardUser, and no-roles

All 20 tests pass.

### T007 — Build Verification (COMPLETE)

| Artifact | Status |
|---|---|
| Identity.Api (Release) | PASS |
| Fund.Api (Release) | PASS |
| CareConnect.Api (Release) | PASS |
| BuildingBlocks.Tests (20 tests) | PASS |
| apps/web (tsc --noEmit) | PASS — 0 errors |
| apps/control-center (next build) | PASS |

---

## Role Architecture — Final State

```
JWT Claims Pipeline:
  ┌─────────────────────────────┐
  │ EffectiveAccessService      │
  │ reads UserRoleAssignment +  │ → product_roles claim
  │ GroupRoleAssignment         │   "SYNQ_CARECONNECT:CARECONNECT_REFERRER"
  └─────────────────────────────┘

  ┌─────────────────────────────┐
  │ ScopedRoleAssignment        │
  │ GLOBAL scope only           │ → role claim
  │ PlatformAdmin, TenantAdmin  │   "PlatformAdmin"
  └─────────────────────────────┘

Frontend Consumption:
  ProductRole.CareConnectReferrer = "SYNQ_CARECONNECT:CARECONNECT_REFERRER"
  → filterNavByRoles / requireProductRole middleware checks .includes()

Backend Policy Enforcement:
  RequireClaim("product_roles", "SYNQ_FUND:SYNQFUND_REFERRER")
  → Matches JWT product_roles array directly
```

---

## Legacy Artifacts — Retention Status

| Entity | Status | Reason |
|---|---|---|
| `TenantGroup` | `[Obsolete]` — DbSet retained | EF migration compatibility |
| `GroupMembership` | `[Obsolete]` — DbSet retained | EF migration compatibility |
| `TenantGroups` table | No runtime queries | Safe to drop in future migration |
| `GroupMemberships` table | No runtime queries | Safe to drop in future migration |

---

## Remaining Work (Out of Scope)

1. **Fund endpoint authorization**: `CanReferFund`/`CanFundApplications` policies exist but are not yet applied to Fund endpoints (all use `AuthenticatedUser`).
2. **Legacy table drop migration**: `TenantGroups`/`GroupMemberships` tables can be dropped via EF migration when ready.
3. **ScopedRoleAssignment Product-scope**: The entity supports `PRODUCT` scope but this is used for runtime `ScopedAuthorizationService` checks, not JWT claims. If product roles need runtime DB authorization (beyond JWT claims), this path is ready.
