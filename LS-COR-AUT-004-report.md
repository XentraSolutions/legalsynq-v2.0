# LS-COR-AUT-004 — Groups + Inherited Access

## Status: COMPLETE

**Scope:** Introduce access groups that allow users to inherit product access and role assignments through group membership, extending the effective access engine (LS-COR-AUT-003) to merge direct and inherited permissions.

---

## 1. Domain Model

### Entities

| Entity | File | Purpose |
|---|---|---|
| `AccessGroup` | `Identity.Domain/AccessGroup.cs` | Tenant-scoped group with name, description, status (`Active`/`Archived`), and scope type (`Tenant`/`Product`/`Organization`). Factory method `Create()` validates scope constraints (ProductCode required for Product scope, OrganizationId required for Org scope). Supports `Update()` and `Archive()` state transitions with actor tracking. |
| `AccessGroupMembership` | `Identity.Domain/AccessGroupMembership.cs` | Links a user to a group within a tenant. Status: `Active`/`Removed`. Factory method `Create()` sets `AddedAtUtc`. `Remove()` transitions to `Removed` and stamps `RemovedAtUtc`. |
| `GroupProductAccess` | `Identity.Domain/GroupProductAccess.cs` | Grants a product code to all active members of a group. Reuses `AccessStatus` enum (`Granted`/`Revoked`). `Grant()` and `Revoke()` transition status and stamp timestamps. |
| `GroupRoleAssignment` | `Identity.Domain/GroupRoleAssignment.cs` | Assigns a role (optionally scoped to a product and/or organization) to all active members of a group. Reuses `AssignmentStatus` enum (`Active`/`Revoked`). `Remove()` transitions and stamps `RemovedAtUtc`. |

### Enums

| Enum | Values | File |
|---|---|---|
| `GroupStatus` | `Active`, `Archived` | `Identity.Domain/GroupStatus.cs` |
| `GroupScopeType` | `Tenant`, `Product`, `Organization` | `Identity.Domain/GroupScopeType.cs` |
| `MembershipStatus` | `Active`, `Removed` | `Identity.Domain/MembershipStatus.cs` |

---

## 2. Persistence

### EF Configurations

| Configuration | Table | Key Indexes |
|---|---|---|
| `AccessGroupConfiguration` | `AccessGroups` | `IX_AccessGroups_TenantId_Name` (unique) |
| `AccessGroupMembershipConfiguration` | `AccessGroupMemberships` | `IX_AccessGroupMemberships_TenantId_GroupId_UserId` (unique) |
| `GroupProductAccessConfiguration` | `GroupProductAccess` | `IX_GroupProductAccess_TenantId_GroupId_ProductCode` (unique) |
| `GroupRoleAssignmentConfiguration` | `GroupRoleAssignments` | `IX_GroupRoleAssignments_TenantId_GroupId_RoleCode` (non-unique, allows same role with different product/org scopes) |

All configurations located in `Identity.Infrastructure/Data/Configurations/`.

### DbContext

4 new `DbSet<T>` properties added to `IdentityDbContext`:
- `AccessGroups`
- `AccessGroupMemberships`
- `GroupProductAccessRecords`
- `GroupRoleAssignments`

### Migration

**`20260410195442_AddAccessGroups`** — Creates 4 tables with columns, primary keys, and indexes. Enum columns stored as `varchar(20)` strings (consistent with LS-COR-AUT-002 pattern). All Guid columns use `char(36)` with `ascii_general_ci` collation.

---

## 3. Service Layer

### Interfaces

| Interface | File | Operations |
|---|---|---|
| `IGroupService` | `Identity.Application/Interfaces/IGroupService.cs` | `CreateAsync`, `UpdateAsync`, `ArchiveAsync`, `GetByIdAsync`, `ListByTenantAsync` |
| `IGroupMembershipService` | `Identity.Application/Interfaces/IGroupMembershipService.cs` | `AddMemberAsync`, `RemoveMemberAsync`, `ListMembersAsync`, `ListGroupsForUserAsync` |
| `IGroupProductAccessService` | `Identity.Application/Interfaces/IGroupProductAccessService.cs` | `GrantAsync`, `RevokeAsync`, `ListAsync` |
| `IGroupRoleAssignmentService` | `Identity.Application/Interfaces/IGroupRoleAssignmentService.cs` | `AssignAsync`, `RemoveAsync`, `ListAsync` |

### Implementations

All located in `Identity.Infrastructure/Services/`.

#### Cross-cutting concerns applied to every mutation:

| Concern | Detail |
|---|---|
| **Input validation** | Null/whitespace/`Guid.Empty` guards at method entry — throws `ArgumentException` before any DB call |
| **Tenant isolation** | All queries filter by `tenantId`; group, user, entitlement, and organization lookups scoped to tenant |
| **Archived group guard** | Mutations on members, products, roles reject if group status is `Archived` |
| **AccessVersion increment** | All mutations that change a user's effective access increment `User.AccessVersion` for affected users, forcing JWT refresh on next `auth/me` call |
| **Audit events** | All mutations publish structured audit events via `IAuditPublisher` with before/after snapshots |
| **Entitlement validation** | Product grants and role assignments with `productCode` verify tenant has an active `TenantProductEntitlement` |
| **Organization validation** | Role assignments with `organizationId` verify the org belongs to the tenant and is active |

#### AccessVersion increment scope by operation:

| Operation | Users affected |
|---|---|
| Add member | The added user |
| Remove member | The removed user |
| Archive group | All active members of the group |
| Grant/revoke group product | All active members of the group |
| Assign/remove group role | All active members of the group |

---

## 4. API Endpoints

All endpoints registered via `GroupEndpoints.MapGroupEndpoints()` in `Identity.Api/Program.cs`.

### Groups CRUD

| Method | Route | AuthZ | Description |
|---|---|---|---|
| `GET` | `/api/tenants/{tenantId}/groups` | `CanReadTenant` | List all groups for a tenant |
| `POST` | `/api/tenants/{tenantId}/groups` | `CanMutateTenant` | Create a new group |
| `GET` | `/api/tenants/{tenantId}/groups/{groupId}` | `CanReadTenant` | Get group by ID |
| `PATCH` | `/api/tenants/{tenantId}/groups/{groupId}` | `CanMutateTenant` | Update group name/description |
| `DELETE` | `/api/tenants/{tenantId}/groups/{groupId}` | `CanMutateTenant` | Archive group (soft delete) |

### Membership

| Method | Route | AuthZ | Description |
|---|---|---|---|
| `GET` | `/api/tenants/{tenantId}/groups/{groupId}/members` | `CanReadTenant` | List group members |
| `POST` | `/api/tenants/{tenantId}/groups/{groupId}/members` | `CanMutateTenant` | Add user to group |
| `DELETE` | `/api/tenants/{tenantId}/groups/{groupId}/members/{userId}` | `CanMutateTenant` | Remove user from group |
| `GET` | `/api/tenants/{tenantId}/users/{userId}/groups` | `CanReadTenant` | List groups a user belongs to |

### Group Product Access

| Method | Route | AuthZ | Description |
|---|---|---|---|
| `GET` | `/api/tenants/{tenantId}/groups/{groupId}/products` | `CanReadTenant` | List group's product grants |
| `PUT` | `/api/tenants/{tenantId}/groups/{groupId}/products/{productCode}` | `CanMutateTenant` | Grant product to group (idempotent) |
| `DELETE` | `/api/tenants/{tenantId}/groups/{groupId}/products/{productCode}` | `CanMutateTenant` | Revoke product from group |

### Group Role Assignments

| Method | Route | AuthZ | Description |
|---|---|---|---|
| `GET` | `/api/tenants/{tenantId}/groups/{groupId}/roles` | `CanReadTenant` | List group's role assignments |
| `POST` | `/api/tenants/{tenantId}/groups/{groupId}/roles` | `CanMutateTenant` | Assign role to group |
| `DELETE` | `/api/tenants/{tenantId}/groups/{groupId}/roles/{assignmentId}` | `CanMutateTenant` | Remove role assignment |

### Authorization Model

- **`CanReadTenant`**: PlatformAdmin/SuperAdmin, or user's `tenantId` claim matches the route parameter
- **`CanMutateTenant`**: PlatformAdmin/SuperAdmin, or user has `TenantAdmin` role AND `tenantId` claim matches

---

## 5. Effective Access Engine — Inherited Access

### Changes to `EffectiveAccessService`

The effective access computation (`GetEffectiveAccessAsync`) now merges two access sources:

1. **Direct access** — `UserProductAccess` (granted) + `UserRoleAssignment` (active)
2. **Inherited access** — Products and roles inherited through active memberships in active groups (`GroupProductAccess` + `GroupRoleAssignment`)

### Merge Rules

| Rule | Detail |
|---|---|
| **Entitlement gate** | Both direct and inherited products must be entitled at the tenant level to be effective |
| **Deduplication** | If a product/role exists both directly and via group, the direct source wins (first-seen) |
| **Archived group exclusion** | Groups with `Status == Archived` are excluded from inherited access computation |
| **Removed membership exclusion** | Memberships with `MembershipStatus == Removed` are excluded |

### Source Attribution

`EffectiveAccessResult` now includes two additional fields for introspection:

```csharp
public record EffectiveProductEntry(
    string ProductCode, string Source, Guid? GroupId = null, string? GroupName = null);

public record EffectiveRoleEntry(
    string RoleCode, string? ProductCode, string Source,
    Guid? GroupId = null, string? GroupName = null);

public record EffectiveAccessResult(
    List<string> Products,
    Dictionary<string, List<string>> ProductRoles,
    List<string> ProductRolesFlat,
    List<string> TenantRoles,
    List<EffectiveProductEntry> ProductSources,    // NEW
    List<EffectiveRoleEntry> RoleSources);          // NEW
```

- `Source` is `"Direct"` or `"Inherited"`
- `GroupId`/`GroupName` populated for inherited entries

### Backward Compatibility

- `Products`, `ProductRoles`, `ProductRolesFlat`, `TenantRoles` — unchanged semantics
- JWT claim projection (`AuthService`) uses only `Products` and `ProductRolesFlat` — no changes required
- `access_version` claim forces JWT refresh when group mutations affect a user's access
- Legacy `ProductRoleResolutionService` merge in `AuthService` continues to work (bare role codes from legacy system merged with `PRODUCT:Role` format from effective access)

---

## 6. DI Registration

4 new scoped services registered in `DependencyInjection.AddInfrastructure()`:

```csharp
services.AddScoped<IGroupService, GroupService>();
services.AddScoped<IGroupMembershipService, GroupMembershipService>();
services.AddScoped<IGroupProductAccessService, GroupProductAccessService>();
services.AddScoped<IGroupRoleAssignmentService, GroupRoleAssignmentService>();
```

---

## 7. Design Decisions

| Decision | Rationale |
|---|---|
| **New entities, not extending legacy `TenantGroup`/`GroupMembership`** | Legacy entities support UIX-002 admin endpoints; clean separation avoids coupling and mirrors LS-COR-AUT-002 approach |
| **Scope types on `AccessGroup`** | Groups can be scoped to Tenant (general), Product (product-specific teams), or Organization (org-level teams) — enabling fine-grained access patterns |
| **Soft-delete via `Archive`/`Remove` status** | No hard deletes; audit trail preserved; archived groups excluded from effective access automatically |
| **Unique composite indexes** | Prevent duplicate memberships, duplicate product grants, and duplicate group names within a tenant at the DB level |
| **GroupRoleAssignment index non-unique** | Same role code can be assigned with different product/org scopes on the same group |

---

## 8. Files Changed

### New Files (15)

| File | Purpose |
|---|---|
| `Identity.Domain/AccessGroup.cs` | Domain entity |
| `Identity.Domain/AccessGroupMembership.cs` | Domain entity |
| `Identity.Domain/GroupProductAccess.cs` | Domain entity |
| `Identity.Domain/GroupRoleAssignment.cs` | Domain entity |
| `Identity.Domain/GroupStatus.cs` | Enum |
| `Identity.Domain/GroupScopeType.cs` | Enum |
| `Identity.Domain/MembershipStatus.cs` | Enum |
| `Identity.Infrastructure/Data/Configurations/AccessGroupConfiguration.cs` | EF config |
| `Identity.Infrastructure/Data/Configurations/AccessGroupMembershipConfiguration.cs` | EF config |
| `Identity.Infrastructure/Data/Configurations/GroupProductAccessConfiguration.cs` | EF config |
| `Identity.Infrastructure/Data/Configurations/GroupRoleAssignmentConfiguration.cs` | EF config |
| `Identity.Infrastructure/Services/GroupService.cs` | Service impl |
| `Identity.Infrastructure/Services/GroupMembershipService.cs` | Service impl |
| `Identity.Infrastructure/Services/GroupProductAccessService.cs` | Service impl |
| `Identity.Infrastructure/Services/GroupRoleAssignmentService.cs` | Service impl |
| `Identity.Application/Interfaces/IGroupService.cs` | Interface |
| `Identity.Application/Interfaces/IGroupMembershipService.cs` | Interface |
| `Identity.Application/Interfaces/IGroupProductAccessService.cs` | Interface |
| `Identity.Application/Interfaces/IGroupRoleAssignmentService.cs` | Interface |
| `Identity.Api/Endpoints/GroupEndpoints.cs` | API endpoints |
| `Identity.Infrastructure/Persistence/Migrations/20260410195442_AddAccessGroups.cs` | Migration |

### Modified Files (4)

| File | Change |
|---|---|
| `Identity.Infrastructure/Data/IdentityDbContext.cs` | Added 4 DbSets |
| `Identity.Infrastructure/Services/EffectiveAccessService.cs` | Extended to merge inherited access |
| `Identity.Application/Interfaces/IEffectiveAccessService.cs` | Added `ProductSources`, `RoleSources` to result record |
| `Identity.Infrastructure/DependencyInjection.cs` | Registered 4 new scoped services |
| `Identity.Api/Program.cs` | Added `app.MapGroupEndpoints()` |

---

## 9. Verification

- **Build:** Identity.Api compiles with 0 errors, 0 warnings
- **Migration:** `20260410195442_AddAccessGroups` generates correct DDL for 4 tables + 4 indexes
- **Health:** Identity service returns HTTP 200 on `/health` after restart
- **Code Review:** Architect review passed after input validation fixes (null/whitespace/Guid.Empty guards added to all service mutation methods)
