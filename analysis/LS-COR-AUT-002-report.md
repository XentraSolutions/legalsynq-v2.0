# LS-COR-AUT-002 — Access Source-of-Truth Foundation

## Summary

This implementation establishes the foundational data model and API surface for managing
tenant product entitlements, user product access, and user role assignments as explicit,
audited source-of-truth records in the Identity service.

## Entities Created

### 1. TenantProductEntitlement
- **Table**: `TenantProductEntitlements`
- **Purpose**: Records which products a tenant is entitled to use.
- **Key Fields**: Id, TenantId, ProductCode (string, uppercased), Status (Active/Disabled/Suspended), EnabledAtUtc, DisabledAtUtc, audit timestamps and actor fields.
- **Unique Index**: `(TenantId, ProductCode)` — one entitlement per product per tenant.
- **Domain Methods**: `Create()`, `Enable()`, `Disable()`.

### 2. UserProductAccess
- **Table**: `UserProductAccess`
- **Purpose**: Records which products a specific user has been granted access to within a tenant.
- **Key Fields**: Id, TenantId, UserId, ProductCode, AccessStatus (Granted/Revoked), OrganizationId (optional), SourceType ("Direct"), GrantedAtUtc, RevokedAtUtc, audit timestamps and actor fields.
- **Index**: `(TenantId, UserId, ProductCode)` — fast lookup by user and product.
- **Validation**: User must belong to tenant. Product must have an Active TenantProductEntitlement for the tenant.
- **Domain Methods**: `Create()`, `Grant()`, `Revoke()`.

### 3. UserRoleAssignment
- **Table**: `UserRoleAssignments`
- **Purpose**: Records role assignments for users, optionally scoped to a product and/or organization.
- **Key Fields**: Id, TenantId, UserId, RoleCode (string), ProductCode (nullable), OrganizationId (nullable), AssignmentStatus (Active/Removed), SourceType ("Direct"), AssignedAtUtc, RemovedAtUtc, audit timestamps and actor fields.
- **Index**: `(TenantId, UserId, RoleCode)` — fast lookup by user and role.
- **Validation**: User must belong to tenant. If ProductCode is specified, must have an Active TenantProductEntitlement. Duplicate active assignments are rejected.
- **Domain Methods**: `Create()`, `Remove()`.
- **Note**: This is a NEW entity distinct from the retired Phase G `UserRoleAssignment` table and distinct from `ScopedRoleAssignment`. It uses string-based `RoleCode`/`ProductCode` instead of FK-based GUIDs, making it a self-contained source-of-truth record.

### Supporting Enums
- `EntitlementStatus`: Active, Disabled, Suspended
- `AccessStatus`: Granted, Revoked
- `AssignmentStatus`: Active, Removed

## Services

### IAuditPublisher (+ AuditPublisher)
- Wraps the existing `IAuditEventClient` shared library.
- Provides a simplified `Publish()` method that builds canonical `IngestAuditEventRequest` objects with source-of-truth event types.
- Fire-and-observe pattern — never blocks business operations on audit delivery.

### ITenantProductEntitlementService
- `GetByTenantAsync(tenantId)` — list all entitlements for a tenant.
- `GetByTenantAndCodeAsync(tenantId, productCode)` — get single entitlement.
- `UpsertAsync(tenantId, productCode, actorUserId)` — create or re-enable. Validates tenant and product existence.
- `DisableAsync(tenantId, productCode, actorUserId)` — soft-disable. Publishes audit event.

### IUserProductAccessService
- `GetByTenantUserAsync(tenantId, userId)` — list all product access records for a user in a tenant.
- `GrantAsync(tenantId, userId, productCode, actorUserId)` — grant or re-grant. Validates user membership and product entitlement.
- `RevokeAsync(tenantId, userId, productCode, actorUserId)` — soft-revoke. Publishes audit event.

### IUserRoleAssignmentService
- `GetByTenantUserAsync(tenantId, userId)` — list all role assignments for a user.
- `AssignAsync(tenantId, userId, roleCode, productCode, organizationId, actorUserId)` — create new assignment. Rejects duplicates.
- `RemoveAsync(tenantId, assignmentId, actorUserId)` — soft-remove. Enforces tenant boundary.

### IAccessSourceQueryService
- `GetSnapshotAsync(tenantId, userId)` — returns a combined `AccessSourceSnapshot` containing all tenant products, user products, and user roles for a tenant+user pair.

## API Endpoints

All endpoints are under `/api/tenants/{tenantId}/...` and are registered in `Program.cs` via `app.MapAccessSourceEndpoints()`.

### Authorization
- **PlatformAdmin / SuperAdmin**: Full access to all tenants.
- **TenantAdmin**: Access restricted to their own tenant (validated via JWT `tenantId` claim).

### Tenant Products
| Method | Route | Description |
|--------|-------|-------------|
| GET    | `/api/tenants/{tenantId}/products` | List all entitlements |
| PUT    | `/api/tenants/{tenantId}/products/{productCode}` | Create or re-enable |
| DELETE | `/api/tenants/{tenantId}/products/{productCode}` | Disable |

### User Products
| Method | Route | Description |
|--------|-------|-------------|
| GET    | `/api/tenants/{tenantId}/users/{userId}/products` | List user product access |
| PUT    | `/api/tenants/{tenantId}/users/{userId}/products/{productCode}` | Grant access |
| DELETE | `/api/tenants/{tenantId}/users/{userId}/products/{productCode}` | Revoke access |

### User Roles
| Method | Route | Description |
|--------|-------|-------------|
| GET    | `/api/tenants/{tenantId}/users/{userId}/roles` | List role assignments |
| POST   | `/api/tenants/{tenantId}/users/{userId}/roles` | Create assignment (body: `{ roleCode, productCode?, organizationId? }`) |
| DELETE | `/api/tenants/{tenantId}/users/{userId}/roles/{assignmentId}` | Remove assignment |

### Snapshot
| Method | Route | Description |
|--------|-------|-------------|
| GET    | `/api/tenants/{tenantId}/users/{userId}/access-snapshot` | Combined view of all source-of-truth data |

## Migration

- **Migration ID**: `20260410190647_AddAccessSourceOfTruth`
- **Tables Created**: TenantProductEntitlements, UserProductAccess, UserRoleAssignments
- **Indexes Created**:
  - `IX_TenantProductEntitlements_TenantId_ProductCode` (unique)
  - `IX_UserProductAccess_TenantId_UserId_ProductCode`
  - `IX_UserRoleAssignments_TenantId_UserId_RoleCode`
- **Applied**: Successfully against identity_db on RDS.

## Files Modified/Created

### Domain (Identity.Domain/)
- `EntitlementStatus.cs` — new enum
- `AccessStatus.cs` — new enum
- `AssignmentStatus.cs` — new enum
- `TenantProductEntitlement.cs` — new entity
- `UserProductAccess.cs` — new entity
- `UserRoleAssignment.cs` — new entity

### Infrastructure (Identity.Infrastructure/)
- `Data/IdentityDbContext.cs` — added 3 DbSets
- `Data/Configurations/TenantProductEntitlementConfiguration.cs` — new EF config
- `Data/Configurations/UserProductAccessConfiguration.cs` — new EF config
- `Data/Configurations/UserRoleAssignmentConfiguration.cs` — new EF config
- `Persistence/Migrations/20260410190647_AddAccessSourceOfTruth.cs` — migration
- `Services/AuditPublisher.cs` — new service
- `Services/TenantProductEntitlementService.cs` — new service
- `Services/UserProductAccessService.cs` — new service
- `Services/UserRoleAssignmentService.cs` — new service
- `Services/AccessSourceQueryService.cs` — new service
- `DependencyInjection.cs` — registered 5 new services

### Application (Identity.Application/)
- `Interfaces/IAuditPublisher.cs` — new interface
- `Interfaces/ITenantProductEntitlementService.cs` — new interface
- `Interfaces/IUserProductAccessService.cs` — new interface
- `Interfaces/IUserRoleAssignmentService.cs` — new interface
- `Interfaces/IAccessSourceQueryService.cs` — new interface + AccessSourceSnapshot record

### API (Identity.Api/)
- `Endpoints/AccessSourceEndpoints.cs` — new endpoint file (10 routes)
- `Program.cs` — registered `MapAccessSourceEndpoints()`

## Design Decisions

1. **String-based ProductCode/RoleCode** instead of FK GUIDs: Makes records self-contained and portable. Product codes are validated on write but not enforced via FK constraints — this allows the source-of-truth records to survive product catalog changes.

2. **Soft-state transitions** (Enable/Disable, Grant/Revoke, Assign/Remove): No hard deletes. All status changes are recorded with timestamps and actor fields for full audit trail.

3. **Separate from ScopedRoleAssignment**: The new `UserRoleAssignment` is a distinct source-of-truth entity. `ScopedRoleAssignment` remains the runtime authorization model. A future migration step will reconcile them.

4. **TenantProductEntitlement vs TenantProduct**: The existing `TenantProduct` uses FK-based `ProductId` and simple `IsEnabled` boolean. The new `TenantProductEntitlement` uses `ProductCode` with richer status, audit fields, and lifecycle. Both can coexist until the legacy model is deprecated.

## Verification

- `dotnet build` passes with 0 warnings, 0 errors.
- Service starts on :5001 with health check returning `{"status":"ok","service":"identity"}`.
- Migration applied cleanly to production RDS database.
- All new services registered in DI container.
