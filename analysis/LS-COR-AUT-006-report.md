# LS-COR-AUT-006 — Legacy Cleanup + Model Unification

**Status**: Complete  
**Date**: 2026-04-10

## Objective

Remove all legacy role resolution and group management systems, unify JWT claim format to use product-scoped roles exclusively, and update authorization filters to parse the new `PRODUCT:Role` format.

## Changes Summary

### 1. AuthService — Legacy Merge Removal (T001)

**File**: `Identity.Application/Services/AuthService.cs`

- Removed `IProductRoleResolutionService` field and constructor injection
- Removed legacy `_roleResolutionService.ResolveAsync()` call
- Removed merge loop that appended legacy bare role codes alongside effective-access roles
- JWT `product_roles` claims now come exclusively from `EffectiveAccessService.GetEffectiveAccessAsync()`

**Before**: Two role resolution paths merged at login — `EffectiveAccessService` (new) + `ProductRoleResolutionService` (legacy fallback)  
**After**: Single path through `EffectiveAccessService` only

### 2. JWT Claim Format Verification (T002)

**File**: `Identity.Infrastructure/Services/JwtTokenService.cs`

- Verified: `JwtTokenService` emits `product_roles` claims directly from the `productRoles` list parameter
- `EffectiveAccessService.ProductRolesFlat` produces `PRODUCT_CODE:ROLE_CODE` format (e.g., `SYNQ_CARECONNECT:CARECONNECT_RECEIVER`)
- System roles (`PlatformAdmin`, `TenantAdmin`) remain as standard `role` claims — unaffected

### 3. Authorization Filter Update (T003)

**File**: `BuildingBlocks/Authorization/ProductRoleClaimExtensions.cs`

Rewrote to parse the unified `PRODUCT:Role` claim format:

- `HasProductAccess(productCode)` — checks if any `product_roles` claim starts with `{productCode}:`
- `HasProductRole(productCode, requiredRoles)` — checks if any `product_roles` claim equals `{productCode}:{role}`
- `IsTenantAdminOrAbove()` — unchanged (PlatformAdmin/TenantAdmin bypass)
- Removed the static `ProductToRolesMap` dictionary — no longer needed since product prefix is parsed dynamically

**Impact**: All CareConnect endpoints using `.RequireProductAccess()` and `.RequireProductRole()` now work with the unified claim format.

### 4. Legacy Role Resolution Service Removal (T004)

**Files removed**:
- `Identity.Infrastructure/Services/ProductRoleResolutionService.cs`
- `Identity.Infrastructure/Services/CareConnectRoleMapper.cs`
- `Identity.Application/Interfaces/IProductRoleResolutionService.cs`
- `Identity.Application/Interfaces/IProductRoleMapper.cs`
- `Identity.Application/DTOs/EffectiveAccessContext.cs`

**DI cleanup**: Removed `IProductRoleMapper` → `CareConnectRoleMapper` and `IProductRoleResolutionService` → `ProductRoleResolutionService` registrations from `DependencyInjection.cs`.

### 5. Legacy Group Backend Removal (T005)

**File**: `Identity.Api/Endpoints/AdminEndpoints.cs`

Removed:
- 5 route registrations: `GET /api/admin/groups`, `GET /api/admin/groups/{id}`, `POST /api/admin/groups`, `POST /api/admin/groups/{id}/members`, `DELETE /api/admin/groups/{id}/members/{userId}`
- 5 handler methods: `ListGroups`, `GetGroup`, `CreateGroup`, `AddGroupMember`, `RemoveGroupMember`
- 2 request records: `CreateGroupRequest`, `AddGroupMemberRequest`

**Note**: `TenantGroup` and `GroupMembership` entities + DB tables are **retained** — they are referenced by the new Access Group system (`AccessGroup` has its own table, but the legacy tables remain for data migration purposes).

### 6. Legacy Group UI Removal (T006)

**Files removed**:
- `apps/control-center/src/app/groups/[id]/page.tsx` (legacy group detail page)
- `apps/control-center/src/app/groups/[id]/loading.tsx`
- `apps/control-center/src/app/api/identity/admin/groups/[id]/members/route.ts` (BFF proxy)
- `apps/control-center/src/app/api/identity/admin/groups/[id]/members/[userId]/route.ts` (BFF proxy)
- `apps/control-center/src/components/users/group-membership-panel.tsx`
- `apps/control-center/src/components/users/group-detail-card.tsx`
- `apps/control-center/src/components/users/group-list-table.tsx`
- `apps/control-center/src/components/users/group-permissions-panel.tsx`

**Files updated**:
- `apps/control-center/src/app/groups/page.tsx` — removed legacy groups fallback (no-tenant-context path). Now shows only new Access Groups with tenant context required.
- `apps/control-center/src/app/tenant-users/[id]/page.tsx` — removed `GroupMembershipPanel`, removed legacy `groups.list()` call from `Promise.allSettled`, kept only `AccessGroupMembershipPanel`.
- `apps/control-center/src/lib/control-center-api.ts` — removed entire `groups` namespace (list, getById, create, addMember, removeMember).
- `apps/control-center/src/lib/api-mappers.ts` — removed `mapGroupSummary`, `mapGroupDetail` functions.
- `apps/control-center/src/types/control-center.ts` — removed `GroupSummary`, `GroupMemberSummary`, `GroupDetail` interfaces.

### 7. ScopedRoleAssignment Assessment (T007)

**Decision**: **Keep** — `ScopedRoleAssignment` is the authoritative store for system roles (Global scope: PlatformAdmin, TenantAdmin). Used extensively by:
- `AuthService.LoginAsync()` for role resolution
- `UserRepository` for user queries
- `AdminEndpoints` for role assignment/revocation
- `ScopedAuthorizationService` for scope-aware authorization

Only the Product-scope usage (via `CareConnectRoleMapper`) was legacy — that code path is now removed.

### 8. Build Verification (T008)

| Target | Result |
|--------|--------|
| Identity.Api (.NET Release) | Pass |
| CareConnect.Api (.NET Release) | Pass |
| Control-Center (Next.js) | Pass |

## Architecture After Cleanup

```
Login Flow (AuthService.LoginAsync):
  User credentials → validate → load ScopedRoleAssignments (Global) → roleNames
                              → EffectiveAccessService.GetEffectiveAccessAsync()
                                  → Direct UserRoleAssignments
                                  → Group-inherited GroupRoleAssignments
                                  → ProductRolesFlat: ["SYNQ_CARECONNECT:CARECONNECT_RECEIVER", ...]
                              → JwtTokenService.GenerateToken(roleNames, productRolesFlat)
                                  → "role" claims: PlatformAdmin, TenantAdmin, etc.
                                  → "product_roles" claims: PRODUCT:Role format

Authorization Flow (endpoint filters):
  Request → RequireProductAccessFilter
          → HasProductAccess("SYNQ_CARECONNECT")
          → Checks: any "product_roles" claim starts with "SYNQ_CARECONNECT:"
          → RequireProductRoleFilter (optional)
          → HasProductRole("SYNQ_CARECONNECT", ["CARECONNECT_RECEIVER"])
          → Checks: any "product_roles" claim == "SYNQ_CARECONNECT:CARECONNECT_RECEIVER"
```

## What Was Removed (Legacy)

| Component | Purpose | Replacement |
|-----------|---------|-------------|
| `ProductRoleResolutionService` | Resolved roles from CareConnect config | `EffectiveAccessService` |
| `CareConnectRoleMapper` | Mapped CC org types → role codes | Direct `UserRoleAssignment` / `GroupRoleAssignment` |
| `IProductRoleMapper` / `IProductRoleResolutionService` | Abstractions | Removed (no longer needed) |
| `EffectiveAccessContext` DTO | Legacy resolution result | `EffectiveAccessResult` from `IEffectiveAccessService` |
| `ProductToRolesMap` (static dict) | Hardcoded product→roles mapping | Dynamic `PRODUCT:Role` prefix parsing |
| `/api/admin/groups/*` endpoints | Legacy group CRUD | `/api/tenants/{tenantId}/access-groups/*` |
| `GroupMembershipPanel` | Legacy group assignment UI | `AccessGroupMembershipPanel` |
| Legacy groups page fallback | No-tenant-context groups view | Removed (tenant context required) |

## Known Considerations

- **DB tables retained**: `TenantGroups` and `GroupMemberships` tables remain in the database. A future migration can drop them once all data is migrated to Access Groups.
- **Claim format is breaking**: Any external consumer reading `product_roles` JWT claims must now expect `PRODUCT:Role` format instead of bare role codes. All internal consumers are updated.
