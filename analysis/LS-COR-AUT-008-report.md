# LS-COR-AUT-008 — Observability + Scale Hardening Report

**Status**: Complete  
**Date**: 2026-04-11

## Summary

Added authorization caching, observability logging, debug endpoint, audit quick-filters, admin access explanation UI, performance baselines, and supporting tests.

---

## T001: Effective Access Caching

**File**: `apps/services/identity/Identity.Infrastructure/Services/EffectiveAccessService.cs`

- `IMemoryCache` with key format `ea:{tenantId}:{userId}:{accessVersion}`
- 5-minute sliding TTL
- Cache hit/miss counters with `Interlocked.Increment`
- `Stopwatch`-based timing on every call
- Cache MISS: `LogInformation`, Cache HIT: `LogDebug`
- AccessVersion increment on any role/product/group change automatically invalidates the cache key

## T002: AccessVersion Batch Optimization

**Files**:
- `Identity.Infrastructure/Services/GroupRoleAssignmentService.cs`
- `Identity.Infrastructure/Services/GroupProductAccessService.cs`

- `ExecuteUpdateAsync` for batch `AccessVersion` increment (single SQL `UPDATE ... SET AccessVersion = AccessVersion + 1 WHERE ...`)
- Eliminates N round-trips for group-level role/product changes
- `Stopwatch`-based timing logged at `Information` level

## T003: Authorization Observability Logging

**Files**:
- `BuildingBlocks/Authorization/Filters/RequireProductAccessFilter.cs`
- `BuildingBlocks/Authorization/Filters/RequireProductRoleFilter.cs`
- `BuildingBlocks/Authorization/Filters/RequireOrgProductAccessFilter.cs`

- Structured `AuthzDecision` log entries on every filter evaluation
- Fields: `userId`, `tenantId`, `method`, `endpoint`, `product`, `requiredRoles`, `source`, `accessVersion`
- Sources: `AdminBypass`, `NoProductAccess`, `InsufficientRole`, `RoleClaim`, `OrgProductAccess`
- DENY: `LogWarning`, ALLOW: `LogInformation` (production-visible)

## T004: Authorization Debug Endpoint

**File**: `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`

- `GET /api/admin/users/{id}/access-debug`
- Returns: `userId`, `tenantId`, `accessVersion`, `products` (with source attribution), `roles` (with source attribution), `systemRoles`, `groups`, `entitlements`, `productRolesFlat`, `tenantRoles`
- Explicit `PlatformAdmin || TenantAdmin` role check (defense-in-depth)
- Cross-tenant access enforced via `IsCrossTenantAccess()`
- Consumes `IEffectiveAccessService` for source-attributed product/role data

## T005: Access Audit Viewer UI

**File**: `apps/control-center/src/app/audit-logs/page.tsx`

- Quick-filter presets for canonical/hybrid mode: Access Changes, Security Events, Role Assignments, Group Membership, Product Access
- Renders as colored pill buttons above the filter chips when no active filters
- Each preset links to the audit-logs page with pre-populated `category` or `eventType` query params

## T006: Admin Access Explanation UI

**Files**:
- `apps/control-center/src/components/users/access-explanation-panel.tsx` (new)
- `apps/control-center/src/app/tenant-users/[id]/page.tsx`
- `apps/control-center/src/types/control-center.ts`
- `apps/control-center/src/lib/api-mappers.ts`
- `apps/control-center/src/lib/control-center-api.ts`

- New `AccessExplanationPanel` component on user detail page
- Shows: system roles, tenant entitlements, product access (expandable per product), group memberships, JWT claims preview
- Direct vs Group source badges (blue/purple)
- Expandable product sections with role table showing roleCode, source, groupName
- AccessVersion displayed in header
- Fetches from `GET /api/admin/users/{id}/access-debug` via BFF

## T007: Performance Baselines

**Files**:
- `apps/services/identity/Identity.Application/Services/AuthService.cs`
- `apps/services/identity/Identity.Infrastructure/Services/EffectiveAccessService.cs`

- `Stopwatch` timing on `LoginAsync()` — logs `LoginPerf` with `userId`, `tenantId`, `elapsedMs`, `accessVersion`
- `EffectiveAccessService` already instrumented (T001) with per-call timing and cache hit/miss ratio logging
- `GroupRoleAssignmentService` / `GroupProductAccessService` batch operations timed (T002)

## T008: Tests

**File**: `shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/ObservabilityTests.cs` (new)

12 new tests:
1. `CacheKey_Format_MatchesExpected` — validates `ea:{tenantId}:{userId}:{version}` format
2. `CacheKey_DifferentVersions_ProduceDifferentKeys`
3. `CacheKey_DifferentUsers_ProduceDifferentKeys`
4. `CacheKey_DifferentTenants_ProduceDifferentKeys`
5. `CacheKey_SameInputs_ProduceIdenticalKeys`
6. `AuthzDecision_DenyFields_ArePopulated`
7. `AuthzDecision_AllowFields_ArePopulated`
8. `ProductRoles_FromEffectiveAccess_MatchJwtClaimFormat`
9. `HasProductAccess_EmptyRoleSegment_ReturnsFalse`
10. `HasProductAccess_ValidRoleSegment_ReturnsTrue`
11. `AccessVersion_InCacheKey_MatchesDbVersion`
12. `AccessDebugResponse_ProductSources_DistinguishDirectAndGroup`

## T009: Build Verification

| Target | Result |
|---|---|
| Identity.Api | 0 errors, 0 warnings |
| Fund.Api | 0 errors |
| CareConnect.Api | 0 errors |
| Control Center (tsc) | 0 errors |
| Web frontend (tsc) | 0 errors |
| Tests | 57 passed, 0 failed |
