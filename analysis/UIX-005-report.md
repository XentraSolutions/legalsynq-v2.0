# UIX-005: Permissions & Effective Access Management — Implementation Report

**Date**: 2026-04-01  
**Feature**: Role capability assignment, effective permission resolution, and permission catalog UX.

---

## Summary

UIX-005 delivers end-to-end permissions management across three surfaces:

1. **Permission Catalog page** (`/permissions`) — searchable, filterable view of all platform capabilities; product chip navigation; server-side URL-param filtering.
2. **Role Permission Panel** (`/roles/[id]`) — interactive assign/revoke UI for role capabilities; idempotent backend; real-time capability picker.
3. **Effective Permissions Panel** (`/tenant-users/[id]`) — read-only union of all permissions a user holds through their role assignments, with per-role source attribution badges.
4. **Group Permissions Panel** (`/groups/[id]`) — informational notice explaining the role-based delegation model (groups derive permissions only through roles).

---

## Deliverables

### Backend — Identity Service

#### `RoleCapabilityAssignment` Domain Entity
- **File**: `apps/services/identity/Identity.Domain/RoleCapabilityAssignment.cs`
- Composite PK `(RoleId, CapabilityId)` — enforces uniqueness at DB level.
- `AssignedByUserId`, `AssignedAtUtc` for auditability.
- `Create()` static factory follows the domain model conventions.

#### EF Core Configuration
- **File**: `apps/services/identity/Identity.Infrastructure/Persistence/Configuration/RoleCapabilityAssignmentConfig.cs`
- Composite key, FK relations to `Roles` and `Capabilities`, char(36) GUIDs.

#### Migration
- **File**: `apps/services/identity/Identity.Infrastructure/Persistence/Migrations/20260401220001_UIX005_AddRoleCapabilityAssignments.cs`
- Creates `RoleCapabilityAssignments` table; safe to apply incrementally.

#### New Admin Endpoints (4)

| Endpoint | Handler | Description |
|----------|---------|-------------|
| `GET /api/admin/roles/{id}/permissions` | `GetRolePermissions` | Lists all capability assignments for a role |
| `POST /api/admin/roles/{id}/permissions` | `AssignRolePermission` | Assigns a capability to a role (idempotent) |
| `DELETE /api/admin/roles/{id}/permissions/{capabilityId}` | `RevokeRolePermission` | Hard-deletes the assignment |
| `GET /api/admin/users/{id}/permissions` | `GetUserEffectivePermissions` | Returns union of all permissions from active role assignments |

- `AssignRolePermission` and `RevokeRolePermission` emit canonical audit events (`role.permission.assigned`, `role.permission.revoked`) using the correct `IngestAuditEventRequest` shape with `Actor`, `Entity`, and JSON-serialized `Metadata`.
- `GetUserEffectivePermissions` joins `ScopedRoleAssignments → RoleCapabilityAssignments → Capabilities` and returns each permission with a `sources` array (role name + assignment date).
- `GetRole` and `ListRoles` updated: return `isSystemRole`, `capabilityCount`, and `resolvedPermissions` from the real junction table.

#### `ListPermissions` Search Support
- `GET /api/admin/capabilities` now accepts `?search=` and `?productId=` for server-side filtering.

---

### Control Center — Types

- **File**: `apps/control-center/src/types/control-center.ts`
- `RoleSummary` extended: `isSystemRole: boolean`, `capabilityCount: number`.
- New types: `RoleCapabilityItem`, `EffectivePermission`, `PermissionSource`, `EffectivePermissionsResult`.

---

### Control Center — Mappers

- **File**: `apps/control-center/src/lib/api-mappers.ts`
- `mapRoleSummary` updated: maps `is_system_role` → `isSystemRole`, `capability_count` → `capabilityCount`.
- `mapRoleDetail` cleaned up: no longer duplicates `capabilityCount` (inherited via `...base`).
- `mapRoleCapabilityItem()` — maps raw capability assignment row.
- `mapEffectivePermission()` — maps a resolved permission with its source attribution.
- `mapEffectivePermissionsResult()` — maps the full paged result including `roleCount`.
- `permissions.list()` bug fixed: backend returns `{ items, totalCount }` envelope; client now uses `mapPagedResponse` instead of `Array.isArray`.

---

### Control Center — API Client

- **File**: `apps/control-center/src/lib/control-center-api.ts`
- `roles.getPermissions(id)` — GET role capability assignments.
- `roles.assignPermission(id, capabilityId)` — POST assign.
- `roles.revokePermission(id, capabilityId)` — DELETE revoke.
- `users.getEffectivePermissions(id)` — GET effective permission union for a user.
- `permissions.list({ search?, productId? })` — accepts optional filter params now forwarded to the backend.

---

### Control Center — BFF Routes

| Route | Method | Handler |
|-------|--------|---------|
| `/api/identity/admin/roles/[id]/permissions` | GET, POST | Assign + list |
| `/api/identity/admin/roles/[id]/permissions/[capabilityId]` | DELETE | Revoke |
| `/api/identity/admin/users/[id]/permissions` | GET | Effective permissions |

---

### Control Center — Components

#### `RolePermissionPanel`
- **File**: `apps/control-center/src/components/roles/role-permission-panel.tsx`
- Client component — interactive assign/revoke UI.
- Capability picker popover: lists all active capabilities filtered by search, grouped by product.
- Assign button calls `POST /api/identity/admin/roles/[id]/permissions`.
- Revoke button (×) calls `DELETE .../permissions/[capabilityId]`.
- System-role guard: assignment controls disabled for system roles (badge shown).
- Disabled state for capabilities already assigned.

#### `EffectivePermissionsPanel`
- **File**: `apps/control-center/src/components/users/effective-permissions-panel.tsx`
- Read-only server component.
- Groups effective permissions by capability code; shows each source role as a badge.
- Empty state for users with no role assignments.

#### `GroupPermissionsPanel`
- **File**: `apps/control-center/src/components/groups/group-permissions-panel.tsx`
- Informational notice: groups derive permissions through roles, not direct grants.
- Links to the Roles page.

---

### Control Center — Pages Updated

| Page | Change |
|------|--------|
| `/permissions` | Full search + product chip filter via URL params; `PermissionSearchBar` client component |
| `/roles/[id]` | `RolePermissionPanel` wired in; role data fetched with `getPermissions` |
| `/tenant-users/[id]` | `EffectivePermissionsPanel` wired in with `getEffectivePermissions` |
| `/groups/[id]` | `GroupPermissionsPanel` wired in |

---

### `/permissions` Search UI (UIX-005 scope)

- **Page**: `apps/control-center/src/app/permissions/page.tsx`
- **Component**: `apps/control-center/src/components/users/permission-search-bar.tsx`
- Product chip nav: one chip per unique product, links preserve current search query.
- Text search input (`PermissionSearchBar`) — client component using `useRouter` + `useTransition` for navigation; clear (×) button; submit on Enter or button click.
- Active filter summary bar below filters.
- Count line: `N permissions [matching filters | total]`.
- Read-only notice banner with link to Roles page.

---

## Access Matrix

| Role | `/permissions` | `/roles/[id]` (assign/revoke) | `/tenant-users/[id]` (effective perms) | `/groups/[id]` (perms panel) |
|------|---------------|-------------------------------|----------------------------------------|------------------------------|
| PlatformAdmin | ✓ Read | ✓ Full CRUD | ✓ Read | ✓ Read |
| TenantAdmin | ✗ (Platform only) | ✗ (Platform only) | ✓ Own-tenant users | ✓ Own-tenant groups |
| No role | → login | → login | → login | → login |

---

## Known Limitations / Follow-on

- `RoleCapabilityAssignment` uses hard-delete for revoke. A soft-delete `IsActive` field can be added later if audit history of individual assignment state changes is required.
- The effective permissions query (`GetUserEffectivePermissions`) unions across all active `ScopedRoleAssignments` regardless of scope type. For product-scoped or org-scoped roles, the effective permissions response includes `sources[]` so the UI can display which scope the permission comes from if needed in the future.
- `PermissionSearchBar` uses client-side navigation (URL param push) for the search input. The product chips use plain `<a>` tags for native navigation, preserving the existing search term across product chip clicks.
