# LS-COR-AUT-010A — Permission Model Alignment + Full Migration Report

## Summary

Complete system-wide rename of Capability → Permission, unified permission code format, enforcement migration across all services, role fallback toggle, and permission lifecycle enforcement.

## Changes

### T001: Permission Naming Unification
- **Permission.cs**: New regex `^[A-Z0-9_]+\.[a-z][a-z0-9]*(?:\:[a-z][a-z0-9]*)*$` — codes now store full `PRODUCT.domain:action` format (e.g. `SYNQ_CARECONNECT.referral:create`)
- **SeedIds.cs**: All seed constants updated with full permission codes
- **EffectiveAccessService.cs**: Removed runtime concatenation — permission codes are read directly from DB
- **Create/IsValidCode**: No longer lowercases; validates and stores as-is

### T002: Rename Capability → Permission (Domain + Infrastructure + BuildingBlocks + Services)
- **Entities**: `Capability` → `Permission`, `RoleCapability` → `RolePermissionMapping`, `RoleCapabilityAssignment` → `RolePermissionAssignment`
- **EF Configs**: `PermissionConfiguration`, `RolePermissionMappingConfiguration`, `RolePermissionAssignmentConfiguration` — all map to unchanged DB tables/columns
- **DbContext**: `Capabilities` → `Permissions`, `RoleCapabilities` → `RolePermissionMappings`, `RoleCapabilityAssignments` → `RolePermissionAssignments`
- **Navigation props**: `Product.Capabilities` → `Product.Permissions`, `Role.RoleCapabilities` → `Role.RolePermissionMappings`
- **BuildingBlocks**: `CapabilityCodes` → `PermissionCodes`, `ICapabilityService` → `IPermissionService` (HasPermissionAsync, GetPermissionsAsync)
- **Identity**: `CapabilityService` → `PermissionService` (Auth/)
- **CareConnect**: `CareConnectCapabilityService` → `CareConnectPermissionService`
- **DI registrations**: Updated in both Identity and CareConnect

### T003: Rename Capability → Permission (API + Admin Endpoints)
- **AdminEndpoints.cs**: All `db.Capabilities` → `db.Permissions`, `db.RoleCapabilityAssignments` → `db.RolePermissionAssignments`
- Route parameter `capabilityId` → `permissionId`
- `AssignRolePermissionRequest(Guid CapabilityId)` → `AssignRolePermissionRequest(Guid PermissionId)`
- Response fields `capabilityCount` → `permissionCount`, `capabilityId` → `permissionId`
- Audit descriptions updated from "Capability" to "Permission"

### T004: Frontend
- Control center page description updated: "capability assignments" → "permission assignments"
- Other frontend files had only comment references (middleware.ts, careconnect-access.ts) — no functional `Capability` types were in the frontend

### T005: Enforcement Migration (CareConnect)
- `ReferralWorkflowRules.RequiredCapabilityFor` → `RequiredPermissionFor`
- `ReferralEndpoints.cs`: Uses `RequiredPermissionFor` + `PermissionCodes`
- `ProviderEndpoints.cs`: Already using `RequirePermission` filter
- `ProviderAccessReadinessService`: Uses `IPermissionService` (was `ICapabilityService`)

### T006: Role Fallback Toggle
- **RequirePermissionFilter**: Added optional `fallbackRoles` parameter
- When `Authorization:EnableRoleFallback=true` in config AND fallback roles are specified, the filter checks product roles as a secondary gate if the permission claim is missing
- Logged as `source=RoleFallback` for auditability
- **ProductAuthorizationExtensions**: `RequirePermission()` overloads accept optional `params string[] fallbackRoles`
- Default: fallback disabled (no config entry needed)

### T007: Permission Lifecycle Enforcement
- **EffectiveAccessService** (line 233): Already joins `Permissions.Where(c => c.IsActive)` — inactive permissions excluded from effective access computation
- **PermissionService** (line 46): Already filters `rc.Permission.IsActive` in cache queries
- **AdminEndpoints**: `AssignRolePermission` checks `c.IsActive` before assignment; `ListPermissions` filters `IsActive`
- **JWT projection**: `AuthService` → `EffectiveAccessService.GetEffectiveAccessAsync` → `ResolvePermissionsAsync` → only active permissions included in token

### T008: Tests + Build Verification
- **CareConnectCapabilityServiceTests.cs** → `CareConnectPermissionServiceTests` — all method names/references updated
- **ReferralWorkflowRulesTests.cs**: `RequiredCapabilityFor` → `RequiredPermissionFor`, `CapabilityCodes` → `PermissionCodes`
- **ReferralAcceptanceLockdownTests.cs**: All capability references → permission, expected code value updated to full format
- **WorkflowIntegrationTests.cs**: All capability references → permission
- **ProviderAccessReadinessTests.cs**: `CareConnectCapabilityService` → `CareConnectPermissionService`
- **PermissionGovernanceTests.cs**: Full rewrite — all `Capability.` references → `Permission.`, test data uses full `PRODUCT.domain:action` codes, regex validation tests updated
- **Build**: `dotnet build` — clean (0 errors, 0 warnings)
- **Tests**: `dotnet test` — all passing
- **Grep verification**: Zero remaining `CapabilityCodes`, `ICapabilityService`, `HasCapabilityAsync`, `GetCapabilitiesAsync`, `RequiredCapabilityFor`, or `CareConnectCapabilityService` references in .cs/.ts/.tsx files

### Code Review Fixes
1. **Regex relaxed**: `[a-z][a-z0-9]*` → `[a-z][a-z0-9_]*` to allow underscores in domain/action segments (e.g. `referral:update_status`). Test data updated with explicit `update_status` validation case.
2. **Permission catalog CRUD restricted to PlatformAdmin**: `CreatePermission`, `UpdatePermission`, `DeactivatePermission` now require `PlatformAdmin` only (previously allowed `TenantAdmin`, which was incorrect since permissions are global platform-level definitions).
3. **AccessVersion invalidation**: Noted as a future improvement — currently JWT expiry naturally bounds stale token lifetime. The `AccessVersion` bump mechanism exists for role/product changes but is not yet wired to permission-catalog mutations (low-frequency admin operations).

## DB Impact
- **No migrations needed** — underlying table/column names unchanged
- EF configurations use `.ToTable("Capabilities")`, `.HasColumnName("CapabilityId")` etc. to alias old DB schema to new C# naming

## Files Modified (key)
| Layer | Files |
|-------|-------|
| Domain | Permission.cs, RolePermissionMapping.cs, RolePermissionAssignment.cs, Product.cs, Role.cs, SeedIds.cs |
| Infrastructure | PermissionConfiguration.cs, RolePermissionMappingConfiguration.cs, RolePermissionAssignmentConfiguration.cs, IdentityDbContext.cs, EffectiveAccessService.cs, PermissionService.cs, DependencyInjection.cs |
| BuildingBlocks | PermissionCodes.cs, IPermissionService.cs, AuthorizationService.cs, ForbiddenException.cs, RequireProductAccessFilter.cs, ProductAuthorizationExtensions.cs |
| CareConnect | CareConnectPermissionService.cs, ReferralWorkflowRules.cs, ProviderAccessReadinessService.cs, ReferralEndpoints.cs, DependencyInjection.cs |
| API | AdminEndpoints.cs |
| Frontend | control-center/page.tsx |
| Tests | CareConnectPermissionServiceTests.cs, ReferralWorkflowRulesTests.cs, ReferralAcceptanceLockdownTests.cs, WorkflowIntegrationTests.cs, ProviderAccessReadinessTests.cs, PermissionGovernanceTests.cs |
