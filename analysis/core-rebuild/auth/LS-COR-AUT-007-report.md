# LS-COR-AUT-007 — Enforcement Completion + Hardening Report

## Summary

Completed authorization enforcement across all services, removed legacy group tables,
restricted ScopedRoleAssignment to GLOBAL scope, enhanced effective access UI with
source attribution, and added hardening tests.

## Tasks Completed

### T001: Fund Service Endpoint Enforcement
- Added `.RequireProductAccess(ProductCodes.SynqFund)` at the group level on ApplicationEndpoints
- Applied role-specific filters:
  - Create/Update/Submit → `SYNQFUND_REFERRER`
  - BeginReview/Approve/Deny → `SYNQFUND_FUNDER`
- Files: `Fund.Api/Endpoints/ApplicationEndpoints.cs`

### T002: CareConnect Remaining Endpoint Enforcement
- Audit confirmed all non-admin endpoints already have `.RequireProductAccess(ProductCodes.SynqCareConnect)`
- Admin endpoints correctly use `PlatformOrTenantAdmin` policy (system admin operations that bypass product access via `IsTenantAdminOrAbove()`)
- No changes needed — CareConnect enforcement is complete

### T003: Drop Legacy TenantGroups/GroupMemberships Tables
- Deleted entity files: `TenantGroup.cs`, `GroupMembership.cs`
- Removed `[Obsolete]` DbSets from `IdentityDbContext`
- Deleted EF configurations: `TenantGroupConfiguration.cs`, `GroupMembershipConfiguration.cs`
- Created migration `20260411000001_DropLegacyGroupTables.cs` to drop tables
- Cleaned model snapshot: removed entity blocks, FK relationships, and navigation blocks for both entities
- Identity service builds clean

### T004: Restrict ScopedRoleAssignment to GLOBAL Scope
- `ScopeTypes` simplified to contain only `Global` constant with `IsValid()` method
- `Create()` factory method rejects non-GLOBAL scopes with `ArgumentException`
- `Create()` forces `OrganizationId`, `OrganizationRelationshipId`, and `ProductId` to `null`
- `AdminEndpoints.AssignRole` blocks non-GLOBAL scope assignments at the API layer
- `ScopedAuthorizationService` simplified to GLOBAL-only checks
- Product roles are exclusively managed via `UserRoleAssignment`/`GroupRoleAssignment`

### T005: Effective Access UI Completion
- Enhanced `EffectivePermissionsPanel` with visual differentiation:
  - **Direct** sources (role): blue badges with user icon
  - **Group** sources (access group): purple badges with group icon
- Added `SourceSummary` component showing counts of direct roles vs access groups
- Added color-coded legend explaining Direct vs Group source attribution
- Updated description text to explain source badge meaning
- Grouped sources by type (direct first, then group) in each permission row

### T006: Hardening Tests
- **ScopedRoleAssignmentTests** (8 tests):
  - `Create_WithGlobalScope_Succeeds` — verifies all fields set correctly
  - `Create_WithGlobalScope_CaseInsensitive_Succeeds` — case-insensitive "global" accepted
  - `Create_WithNonGlobalScope_ThrowsArgumentException` — 6 inline data variants (PRODUCT, ORGANIZATION, RELATIONSHIP, TENANT, empty, product lowercase)
  - `Create_IgnoresProductAndOrgIds_EvenIfProvided` — org/product IDs nulled out
  - `Deactivate_SetsIsActiveFalse` — deactivation works
  - `ScopeTypes_IsValid_OnlyAcceptsGlobal` — validator correctness
- **ProductRoleClaimExtensions hardening** (17 new tests):
  - Malformed claim rejection: empty string, whitespace, no colon, missing product, missing role
  - Multiple colons in claim handled correctly
  - Empty claims return empty collections
  - Empty allowed roles returns false
  - Empty role segment (`"SYNQ_FUND:"`) correctly rejected — security fix
  - Whitespace role segment still accepted (non-empty after prefix)

## Security Fix (Post-Review)

- **HasProductAccess empty role segment bypass**: `ProductRoleClaimExtensions.HasProductAccess` 
  previously accepted claims like `"SYNQ_FUND:"` (product code with colon but no role) because 
  it only checked `StartsWith(prefix)`. Fixed to also require `val.Length > prefix.Length`, 
  ensuring the role segment is non-empty. Added test coverage.
- **AdminEndpoints diagnostic endpoint**: Fixed references to removed `ScopeTypes.Organization`, 
  `ScopeTypes.Product`, `ScopeTypes.Relationship`, `ScopeTypes.Tenant` constants — replaced 
  with string literals for backward-compatible diagnostic reporting.

## Build Verification

| Service | Status |
|---|---|
| Identity.Api | Build succeeded, 0 errors |
| Fund.Api | Build succeeded, 0 errors |
| CareConnect.Api | Build succeeded, 0 errors |
| Documents.Api | Build succeeded, 0 errors (1 async warning) |
| Notifications.Api | Build succeeded, 0 errors |
| PlatformAuditEventService | Build succeeded, 0 errors |
| Gateway.Api | Build succeeded, 0 errors |
| control-center (tsc) | Type-check passed |
| web (tsc) | Type-check passed |
| Tests (45 total) | All passed (20 original + 8 scoped + 17 hardening) |

## Authorization Model Summary

```
JWT Claims:
  "role" claim     → PlatformAdmin, TenantAdmin (system roles, GLOBAL scope only)
  "product_roles"  → PRODUCT_CODE:ROLE_CODE (product roles via UserRoleAssignment/GroupRoleAssignment)

Endpoint Enforcement:
  .RequireProductAccess(code)    → user must have any role under that product (or be TenantAdmin+)
  .RequireProductRole(code, [])  → user must have specific role under that product (or be TenantAdmin+)
  .RequireAuthorization(policy)  → standard ASP.NET policy (PlatformOrTenantAdmin for admin ops)

ScopedRoleAssignment:
  GLOBAL scope only. Product/Org/Relationship scopes removed.
  Used exclusively for PlatformAdmin and TenantAdmin system role assignments.
```
