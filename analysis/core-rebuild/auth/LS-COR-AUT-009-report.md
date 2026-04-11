# LS-COR-AUT-009 — Permission / Capability Layer Report

## Summary

Evolved RBAC into permission-driven authorization by resolving capabilities from roles,
adding them to JWT tokens, creating an enforcement filter, exposing via API, and surfacing
in the admin UI.

## Completed Tasks

### T001: Permission Resolution in EffectiveAccessService
- Added `EffectivePermissionEntry` record with full provenance tracking (PermissionCode, ProductCode, Source, ViaRoleCode, GroupId, GroupName)
- Extended `EffectiveAccessResult` with `List<string> Permissions` and `List<EffectivePermissionEntry> PermissionSources`
- Implemented `ResolvePermissionsAsync()` in `EffectiveAccessService`:
  - Resolves capabilities via UserRoleAssignment → RoleCapabilityAssignment chain (Direct source)
  - Resolves capabilities via GroupRoleAssignment → ProductRole → RoleCapability chain (Group source)
  - Filters by active tenant entitlements (effective product set)
  - Permission format: `{PRODUCT_CODE}.{capability_code}` (e.g., `SYNQ_CARECONNECT.referral:create`)
- Permissions included in `ea:{tenantId}:{userId}:{accessVersion}` cache

### T002: JWT Claim Extension
- Added `permissions` parameter to `IJwtTokenService.GenerateTokenAsync()` / `JwtTokenService`
- Multi-value `permissions` claim added to JWT alongside existing `product_roles`
- `AuthService.LoginAsync` passes `effectiveAccess.Permissions` to token generation
- Backward compatible — existing token consumers unaffected

### T003: RequirePermissionFilter + Policy Registration
- New `RequirePermissionFilter` (IEndpointFilter) checking `permissions` JWT claim
- Admin bypass: TenantAdmin and PlatformAdmin always ALLOW
- Structured `PermissionDecision` logging (DENY=Warning, ALLOW=Information) consistent with existing `AuthzDecision` pattern
- Extension methods: `.RequirePermission("PRODUCT.capability")` for both `RouteHandlerBuilder` and `RouteGroupBuilder`
- Reuses existing `ProductAccessDeniedResult` for consistent error responses

### T004: Permission Catalog API Endpoints
- `GET /api/admin/permissions` — lists all active capabilities across all products
- `GET /api/admin/permissions/by-product/{productCode}` — filters by product
- Admin-only (PlatformAdmin || TenantAdmin guard)
- Returns: id, code, name, description, productCode, productName, isActive

### T005: Access Debug Endpoint Extension
- `/access-debug` response now includes `permissions` (flat list) and `permissionSources` (with full provenance)
- Permission sources include: permissionCode, productCode, source, viaRoleCode, groupId, groupName

### T006: Admin UI — Permission Visibility
- `AccessExplanationPanel` now shows a "Permissions" section grouped by product
- Each permission displays: capability code, via-role, source badge (Direct/Group)
- JWT Claims Preview section updated with separate `product_roles` and `permissions` sub-sections
- `AccessDebugResult` TypeScript type extended with `permissions` and `permissionSources`
- API mapper updated to parse permission data from access-debug response

### T007: Tests
- 11 new permission-specific tests added to `ProductRoleClaimExtensionsTests`:
  - `HasPermission` — exact match, case-insensitive, no-match, no-claims
  - Admin bypass — PlatformAdmin, TenantAdmin
  - Cross-product isolation — SYNQ_FUND permission ≠ SYNQ_CARECONNECT
  - Partial code rejection
  - `GetPermissions` — returns all, empty returns empty
  - Multiple permissions — correct match selection
- Total test count: 68 (up from 57)

### T008: Build Verification
- Identity.Api: ✅ Build succeeded (0 warnings, 0 errors)
- Fund.Api: ✅ Build succeeded
- CareConnect.Api: ✅ Build succeeded
- BuildingBlocks.Tests: ✅ 68/68 tests passing
- Control-Center TypeScript: ✅ No errors

### Code Review Fixes
- **Removed duplicate `/api/admin/permissions` route** — existing UIX-002 handler already provides the general catalog; kept only the new `by-product/{productCode}` endpoint
- **Added cross-product consistency check** in `ResolvePermissionsAsync` — `.Where(x => x.RoleProductId == x.CapabilityProductId)` prevents privilege bleed from malformed `RoleCapability` rows
- **Fixed error taxonomy** — `RequirePermissionFilter` now returns `PERMISSION_DENIED` error code via `MissingPermission()` factory instead of `PRODUCT_ACCESS_DENIED`

## Files Changed

| File | Change |
|------|--------|
| `IEffectiveAccessService.cs` | Added `EffectivePermissionEntry`, extended `EffectiveAccessResult` |
| `EffectiveAccessService.cs` | Added `ResolvePermissionsAsync()`, integrated into main flow |
| `IJwtTokenService.cs` | Added `permissions` parameter |
| `JwtTokenService.cs` | Emit `permissions` multi-value claim |
| `AuthService.cs` | Pass permissions to token generation |
| `RequireProductAccessFilter.cs` | Added `RequirePermissionFilter` class |
| `ProductAuthorizationExtensions.cs` | Added `.RequirePermission()` extension methods |
| `ProductRoleClaimExtensions.cs` | Added `HasPermission()`, `GetPermissions()` |
| `AdminEndpoints.cs` | Permission catalog endpoints, access-debug extended |
| `control-center.ts` (types) | Added `AccessDebugPermissionEntry`, extended `AccessDebugResult` |
| `access-explanation-panel.tsx` | Permissions section, updated JWT preview |
| `api-mappers.ts` | Permission data mapping in access-debug mapper |
| `ProductRoleClaimExtensionsTests.cs` | 11 new permission tests |

## Permission Format Convention

```
{PRODUCT_CODE}.{capability_code}
```
- Product code: uppercase (e.g., `SYNQ_CARECONNECT`)
- Capability code: lowercase (e.g., `referral:create`)
- Full example: `SYNQ_CARECONNECT.referral:create`

## Usage

```csharp
// Endpoint protection
app.MapPost("/api/referrals", CreateReferral)
   .RequirePermission("SYNQ_CARECONNECT.referral:create");

// Manual check in handler
if (!user.HasPermission("SYNQ_CARECONNECT.referral:view"))
    return Results.Forbid();
```
