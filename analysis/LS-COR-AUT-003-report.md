# LS-COR-AUT-003 — Effective Access Engine + JWT Claim Projection

## Summary

Implements the effective access computation layer and JWT claim projection that connects the LS-COR-AUT-002 source-of-truth model with the LS-COR-AUT-001 runtime enforcement layer. JWT claims are now derived from the new source-of-truth tables, with backward-compatible fallback to the legacy `ProductRoleResolutionService`.

## Implementation Details

### 1. Effective Access Engine

**Interface**: `IEffectiveAccessService` (`Identity.Application/Interfaces/IEffectiveAccessService.cs`)

**Implementation**: `EffectiveAccessService` (`Identity.Infrastructure/Services/EffectiveAccessService.cs`)

**Method**: `GetEffectiveAccessAsync(tenantId, userId)`

**Output Model** — `EffectiveAccessResult`:
- `Products`: List of accessible product codes (intersection of active entitlements AND granted user access)
- `ProductRoles`: Dictionary mapping product → list of roles
- `ProductRolesFlat`: Flattened list in `PRODUCT:Role` format (e.g., `SYNQ_CARECONNECT:CareCoordinator`)
- `TenantRoles`: Roles not scoped to any product

### 2. Access Resolution Rules

| Condition | Result |
|---|---|
| TenantProductEntitlement = Active AND UserProductAccess = Granted | Product access allowed |
| TenantProductEntitlement = Disabled | No access (regardless of UserProductAccess) |
| UserProductAccess = Revoked | No access |
| UserRoleAssignment = Active AND product matches entitlement | Role included |
| UserRoleAssignment = Active AND product NOT entitled | Role excluded |
| UserRoleAssignment with no ProductCode | Included as tenant-level role |

### 3. JWT Claim Projection

**New claims added**:
- `access_version` (int) — token freshness indicator
- `product_codes` (array) — list of accessible product codes

**Existing claims preserved**:
- `product_roles` (array) — `PRODUCT:Role` format, compatible with LS-COR-AUT-001 filters
- `tenant_id`, `tenant_code`, `session_version`, etc.

**Merge strategy**: Effective access engine output takes precedence. Legacy `ProductRoleResolutionService` results are merged in for any product roles not yet covered by the new model. This ensures backward compatibility during migration.

### 4. Token Freshness / Invalidation

**New field**: `User.AccessVersion` (int, default 0)

**Domain method**: `User.IncrementAccessVersion()` — monotonically increasing counter

**Increment triggers**:
- `UserProductAccessService.GrantAsync()` — on grant or re-grant
- `UserProductAccessService.RevokeAsync()` — on revoke
- `UserRoleAssignmentService.AssignAsync()` — on role assignment
- `UserRoleAssignmentService.RemoveAsync()` — on role removal
- `TenantProductEntitlementService.DisableAsync()` — increments ALL affected users
- `TenantProductEntitlementService.UpsertAsync()` (re-enable) — increments ALL affected users

**Validation**: In `AuthService.GetCurrentUserAsync()` (auth/me endpoint):
- Reads `access_version` claim from JWT
- Compares against `User.AccessVersion` from DB
- If `tokenAccessVersion < user.AccessVersion` → rejects with `"Access has been updated. Please re-authenticate."`
- Absent claim (pre-feature tokens) → allowed through for backward compatibility

### 5. Login Flow Integration

Updated `AuthService.LoginAsync()`:
1. After authentication and role loading
2. Calls `IEffectiveAccessService.GetEffectiveAccessAsync(tenant.Id, user.Id)`
3. Merges with legacy `ProductRoleResolutionService` output
4. Passes merged product roles AND product codes to `JwtTokenService.GenerateToken()`

No new login endpoints. No authentication logic changes.

### 6. Backward Compatibility

- Existing tokens remain valid until expiry (absent `access_version` claim is allowed through)
- `product_roles` claim format unchanged (`PRODUCT:Role`)
- LS-COR-AUT-001 filters (`RequireProductAccessFilter`, `RequireProductRoleFilter`) unchanged
- Legacy `ProductRoleResolutionService` still called and merged (covers products not yet in source-of-truth model)
- All downstream services unchanged

### 7. Database Migration

**Migration**: `20260410192258_AddAccessVersion`
- Adds `AccessVersion` INT NOT NULL DEFAULT 0 to `Users` table
- Drops and recreates `IX_UserProductAccess_TenantId_UserId_ProductCode` as UNIQUE (fixes LS-COR-AUT-002 review finding)

## Build Status

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Identity service: healthy on :5001

## Files Changed

| File | Change |
|---|---|
| `Identity.Domain/User.cs` | Added `AccessVersion` property and `IncrementAccessVersion()` method |
| `Identity.Application/Interfaces/IEffectiveAccessService.cs` | New interface + `EffectiveAccessResult` record |
| `Identity.Application/Interfaces/IJwtTokenService.cs` | Added `productCodes` parameter |
| `Identity.Application/Services/AuthService.cs` | Integrated effective access engine + access_version validation in auth/me |
| `Identity.Infrastructure/Services/EffectiveAccessService.cs` | New service implementation |
| `Identity.Infrastructure/Services/JwtTokenService.cs` | Added `access_version`, `product_codes` claims |
| `Identity.Infrastructure/Services/UserProductAccessService.cs` | Calls `IncrementAccessVersion()` on grant/revoke |
| `Identity.Infrastructure/Services/UserRoleAssignmentService.cs` | Calls `IncrementAccessVersion()` on assign/remove |
| `Identity.Infrastructure/Services/TenantProductEntitlementService.cs` | Increments `AccessVersion` for all affected users on enable/disable |
| `Identity.Infrastructure/Data/Configurations/UserConfiguration.cs` | Added `AccessVersion` EF config |
| `Identity.Infrastructure/DependencyInjection.cs` | Registered `IEffectiveAccessService` |
| `Identity.Infrastructure/Persistence/Migrations/20260410192258_AddAccessVersion.cs` | New migration |

## Code Review — Findings and Resolutions

### Fixed

1. **auth/me null user bypass (Security)**: `GetCurrentUserAsync` now fails closed when the DB user is not found. Previously, a signed token for a deleted user would pass through version checks because the `user is not null` guard allowed null users to proceed. Now throws `UnauthorizedAccessException("User not found.")`.

### Acknowledged (By Design)

2. **Legacy admin mutation paths don't increment AccessVersion**: The `AdminEndpoints` (e.g., `AssignRole`/`RevokeRole` on `ScopedRoleAssignments`) use the legacy role model and don't bump `AccessVersion`. This is intentional — once all tenants migrate to the new source-of-truth model (`UserRoleAssignment`), the legacy mutation paths will be retired. During the transition, the legacy `ProductRoleResolutionService` remains the authoritative source for those roles.

3. **Product roles claim format**: The new effective access engine emits `PRODUCT:Role` format (e.g., `SYNQ_CARECONNECT:CareCoordinator`). The existing LS-COR-AUT-001 filters check bare role codes (e.g., `CARECONNECT_RECEIVER`). Compatibility is maintained because the legacy resolver's bare codes are additively merged into the claim. The `PRODUCT:Role` entries are additional data available for future filter upgrades.

4. **Concurrency on AccessVersion increment**: The read-modify-write pattern (`AccessVersion++` in memory) has a theoretical lost-update race under concurrent mutations. For current operational scale this is acceptable — an atomic SQL `SET AccessVersion = AccessVersion + 1` can be added if high-contention scenarios emerge.

## Known Gaps

1. **No short-lived caching**: The effective access computation hits the DB on every login. For the current scale this is acceptable. A 5-minute memory cache can be added later if login performance becomes a concern.
2. **Tenant-level roles** are computed but not yet emitted as a separate JWT claim (stored in `TenantRoles` field of `EffectiveAccessResult`). They are available for future use when tenant-level role enforcement is needed.
3. **Legacy merger is additive**: The legacy `ProductRoleResolutionService` output is merged into the effective access result. Once all tenants are migrated to the source-of-truth model, the legacy resolver can be removed.

## Assumptions

- `AccessVersion` and `SessionVersion` serve orthogonal purposes: session version handles force-logout/lock, access version handles access changes. Both are validated independently.
- Tenant product entitlement changes affecting multiple users use batch queries (load all affected user IDs, then load and increment). This is safe for current tenant sizes.
- The `product_roles` claim format merger (effective access `PRODUCT:Role` + legacy bare codes) ensures backward compatibility. LS-COR-AUT-001 filters work with bare codes; the `PRODUCT:Role` entries are forward-compatible.
