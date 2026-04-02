# UIX-005-01: Permissions Hardening — Implementation Report

**Date**: 2026-04-02  
**Feature**: TenantAdmin access controls for role permissions; PermissionCatalogTable UX improvements.

---

## Summary

UIX-005-01 closes security and access-control gaps in the role-permissions subsystem introduced by UIX-005. It extends the working assign/revoke flows to TenantAdmins (previously PlatformAdmin-only), adds system-role protection at the API layer, and improves the permissions catalog UI with product-grouped section headers.

---

## Changes by Layer

### 1. Identity Backend — `AdminEndpoints.cs`

Three handlers hardened:

#### `GetRolePermissions` (GET `/api/admin/roles/{id}/permissions`)
- Added `ClaimsPrincipal caller` parameter (previously absent).
- Changed `AnyAsync` role existence check to `FirstOrDefaultAsync` to load the entity.
- Added cross-tenant guard: TenantAdmin may not read non-system roles from other tenants; system roles remain globally readable (read-only view access for all admins).

#### `AssignRolePermission` (POST `/api/admin/roles/{id}/permissions`)
- Added system-role guard: `if (role.IsSystemRole && !caller.IsInRole("PlatformAdmin")) → 403` with a clear human-readable error message.
- Added cross-tenant guard: TenantAdmin may not assign permissions to roles outside their tenant.
- Guards execute after the role null-check but before any business logic.

#### `RevokeRolePermission` (DELETE `/api/admin/roles/{id}/permissions/{capabilityId}`)
- Added system-role guard (same pattern as AssignRolePermission).
- Added cross-tenant guard.
- Guards execute after the assignment null-check; uses `assignment.Role` navigation property (already included via `.Include(a => a.Role)`).

**Backend build**: `Build succeeded.` (no warnings, no errors).

---

### 2. BFF Routes — CC Next.js API

Both routes widened from `requirePlatformAdmin` → `requireAdmin`.
Identity service now enforces system-role and cross-tenant boundaries, so the BFF does not need to duplicate that logic.

| Route | Before | After |
|---|---|---|
| `GET /api/identity/admin/roles/[id]/permissions` | `requirePlatformAdmin` | `requireAdmin` |
| `POST /api/identity/admin/roles/[id]/permissions` | `requirePlatformAdmin` | `requireAdmin` |
| `DELETE /api/identity/admin/roles/[id]/permissions/[capabilityId]` | `requirePlatformAdmin` | `requireAdmin` |

Error status mapping extended to handle `403` from the Identity service (system-role guard responses).

---

### 3. CC Pages

| Page | Before | After |
|---|---|---|
| `/permissions/page.tsx` | `requirePlatformAdmin` | `requireAdmin` |
| `/roles/[id]/page.tsx` | `requirePlatformAdmin` | `requireAdmin` + reads `session.isTenantAdmin` |

The `/roles/[id]` page passes `isTenantAdmin` down to `RolePermissionPanel`.

---

### 4. UI — `RolePermissionPanel`

**New `isTenantAdmin` prop** (optional, defaults `false`):
- Controls the edit buttons and system-role notice text.
- `canEdit` derived as `!isSystemRole` (same semantics, now surfaced as a named variable).

**Context-aware system-role notice**:
- *PlatformAdmin viewing system role*: "System roles cannot be modified. Permissions for this role are managed by the platform engineering team."
- *TenantAdmin viewing system role*: "This is a platform-managed system role. You can view its permissions but cannot modify them. Contact your platform administrator for changes."

**Success banner**:
- After a successful assign or revoke, a green confirmation banner auto-dismisses after 3.5 seconds.
- Replaces the silent refresh-only feedback from UIX-005.

---

### 5. UI — `PermissionCatalogTable`

**Replaced flat table with product-grouped section layout**:
- Each product rendered as a separate card (`bg-white border border-gray-200 rounded-lg`).
- Product section header with colour-coded badge (CareConnect = teal, SynqLien = amber, SynqFund = violet, others = gray) and per-product permission count.
- Within each card, a compact table (Code / Name / Description / Status) with hover states.
- Footer on the last card shows total count across all products.
- When a `productFilter` is active, the same grouped layout is preserved for consistency.
- TypeScript clean — uses only `PermissionCatalogItem` fields (no `productCode`).

---

## Access Matrix (Post-Hardening)

| Operation | PlatformAdmin | TenantAdmin |
|---|---|---|
| View own-tenant role permissions | ✓ | ✓ |
| View system-role permissions | ✓ | ✓ (read-only) |
| View other-tenant role permissions | ✓ | ✗ (403) |
| Assign permission to own-tenant role | ✓ | ✓ |
| Assign permission to system role | ✓ | ✗ (403) |
| Assign permission to other-tenant role | ✓ | ✗ (403) |
| Revoke permission from own-tenant role | ✓ | ✓ |
| Revoke permission from system role | ✓ | ✗ (403) |
| Revoke permission from other-tenant role | ✓ | ✗ (403) |

---

## Files Changed

| File | Change |
|---|---|
| `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` | `GetRolePermissions` + `AssignRolePermission` + `RevokeRolePermission` guards |
| `apps/control-center/src/app/api/identity/admin/roles/[id]/permissions/route.ts` | `requireAdmin`; 403 handling |
| `apps/control-center/src/app/api/identity/admin/roles/[id]/permissions/[capabilityId]/route.ts` | `requireAdmin`; 403 handling |
| `apps/control-center/src/app/permissions/page.tsx` | `requireAdmin` |
| `apps/control-center/src/app/roles/[id]/page.tsx` | `requireAdmin`; `isTenantAdmin` → `RolePermissionPanel` |
| `apps/control-center/src/components/roles/role-permission-panel.tsx` | `isTenantAdmin` prop; success banner; context-aware system-role notice |
| `apps/control-center/src/components/users/permission-catalog-table.tsx` | Product-grouped section layout (full rewrite) |

---

## UIX-004 Audit (verified complete, no new work)

All UIX-004 tasks were found already implemented:
- T001: `GetUserActivity` backend handler registered and implemented.
- T002: `UserActivityEvent` type, `AUDIT_EVENT_LABELS`, `mapEventLabel()` all present.
- T003: `users.getActivity()` and `auditCanonical.listForUser()` in `control-center-api.ts`.
- T004: BFF route `/api/identity/admin/users/[id]/activity/route.ts` exists.
- T005: `/audit-logs/page.tsx` — full implementation with canonical/legacy/hybrid mode.
- T006: `UserActivityPanel` exists and is wired into `/tenant-users/[id]/page.tsx`.
- T007: `audit-logs` in OPERATIONS nav section with `badge: 'LIVE'`.
- T008: `analysis/UIX-004-report.md` exists.
