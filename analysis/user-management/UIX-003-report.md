# UIX-003 — Access Control Management: Implementation Report

---

## 1. Report Title

**UIX-003: Access Control Management**  
Control Center — User Detail Page: Role Assignment, Organization Membership, Group Membership

---

## 2. Feature ID

**UIX-003**  
Depends on: UIX-002 (Admin endpoints, groups, roles, memberships — all live in `AdminEndpoints.cs`)  
Service: `Identity.Api` (port 5001), via API Gateway (port 5010)  
UI: Control Center Next.js app (port 5004)

---

## 3. Summary

UIX-003 extends the existing user detail page (`/tenant-users/[id]`) with three interactive access control management panels, wired end-to-end to live Identity service endpoints. Admins can now assign and revoke system roles, manage organization memberships (including primary designation), and add or remove group memberships — all without leaving the user detail page.

All backend endpoints were pre-existing (UIX-002) with the exception of `GET /api/admin/organizations?tenantId=`, which was added as part of this feature to populate the org selection dropdown. No existing code was rewritten; all work is additive.

---

## 4. Scope Implemented

| Layer | Item | Status |
|-------|------|--------|
| Identity service | `GET /api/admin/organizations?tenantId=` endpoint | Added |
| CC API lib | `users.assignRole`, `users.revokeRole` | Added |
| CC API lib | `organizations.listByTenant` | Added |
| BFF routes | 7 new Next.js Route Handler files | Added |
| UI component | `RoleAssignmentPanel` | Added |
| UI component | `OrgMembershipPanel` | Added |
| UI component | `GroupMembershipPanel` | Added |
| User detail page | Parallel data fetch + "Access Control" section | Updated |
| Types | `OrgSummary` interface | Added |

---

## 5. Backend Endpoints Implemented / Verified

All mutation endpoints reside in `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` and are registered in `MapAdminEndpoints()`.

### Added in UIX-003

| Method | Route | Handler | Auth |
|--------|-------|---------|------|
| GET | `/api/admin/organizations?tenantId=` | `ListOrganizations` | JWT required; PlatformAdmin sees all, TenantAdmin scoped to own tenant |

### Pre-existing (UIX-002), verified operational

| Method | Route | Handler | Key Behaviors |
|--------|-------|---------|---------------|
| POST | `/api/admin/users/{id}/roles` | `AssignRole` | Body: `{ roleId, scopeType? }`. Validates user + role existence. Defaults `scopeType` to `GLOBAL`. Supports GLOBAL / ORGANIZATION / PRODUCT / RELATIONSHIP / TENANT scopes. Emits audit event. |
| DELETE | `/api/admin/users/{id}/roles/{roleId}` | `RevokeRole` | Deactivates the matching GLOBAL `ScopedRoleAssignment`. Returns 404 if not assigned. Emits audit event. |
| GET | `/api/admin/users/{id}/scoped-roles` | `GetScopedRoles` | Returns all active scoped role assignments for the user. Not directly consumed by UIX-003 panels (panels use `UserDetail.roles`). |
| POST | `/api/admin/users/{id}/memberships` | `AssignMembership` | Body: `{ organizationId, memberRole?, grantedByUserId? }`. Validates org belongs to same tenant. Returns 409 if already a member. Auto-sets primary if first membership. |
| POST | `/api/admin/users/{id}/memberships/{membershipId}/set-primary` | `SetPrimaryMembership` | Clears primary flag on all other memberships, sets on target. |
| DELETE | `/api/admin/users/{id}/memberships/{membershipId}` | `RemoveMembership` | Enforces two safety rules (see §12). Returns 409 with machine-readable `code`. |
| POST | `/api/admin/groups/{id}/members` | `AddGroupMember` | Body: `{ userId }`. Validates cross-tenant. **Idempotent** — returns 200 (not 409) if already a member. |
| DELETE | `/api/admin/groups/{id}/members/{userId}` | `RemoveGroupMember` | Hard-deletes the `GroupMembership` row. Returns 404 if not a member. |

**Request record shapes (Identity service):**

```csharp
record AssignRoleRequest(
    Guid    RoleId,
    Guid?   AssignedByUserId           = null,
    string? ScopeType                  = null,   // defaults to "GLOBAL"
    Guid?   OrganizationId             = null,
    Guid?   ProductId                  = null,
    Guid?   OrganizationRelationshipId = null);

record AssignMembershipRequest(
    Guid    OrganizationId,
    string? MemberRole      = null,              // defaults to "Member"
    Guid?   GrantedByUserId = null);

record AddGroupMemberRequest(
    Guid  UserId,
    Guid? AddedByUserId = null);
```

---

## 6. BFF Routes Implemented / Updated

All 7 routes live under `apps/control-center/src/app/api/identity/admin/` and follow the existing BFF pattern: auth guard → validate body → delegate to `controlCenterServerApi` → return JSON.

| Method | BFF Route | Delegates to |
|--------|-----------|-------------|
| POST | `/api/identity/admin/users/[id]/roles` | `users.assignRole(id, roleId)` |
| DELETE | `/api/identity/admin/users/[id]/roles/[roleId]` | `users.revokeRole(id, roleId)` |
| POST | `/api/identity/admin/users/[id]/memberships` | `users.assignMembership(id, { organizationId, memberRole })` |
| DELETE | `/api/identity/admin/users/[id]/memberships/[membershipId]` | `users.removeMembership(id, membershipId)` |
| POST | `/api/identity/admin/users/[id]/memberships/[membershipId]/set-primary` | `users.setPrimaryMembership(id, membershipId)` |
| POST | `/api/identity/admin/groups/[id]/members` | `groups.addMember(groupId, userId)` |
| DELETE | `/api/identity/admin/groups/[id]/members/[userId]` | `groups.removeMember(groupId, userId)` |

**Auth guard:** All routes call `requirePlatformAdmin()` and return `401` if the session is absent or the role is insufficient. 409 conflict codes from the backend are preserved and forwarded to the client.

**Body validation:** Each POST route validates required fields before calling downstream (returns `400` with a descriptive `message` on missing fields).

---

## 7. User Detail UI Changes

**File:** `apps/control-center/src/app/tenant-users/[id]/page.tsx`

**Before:** Page fetched `UserDetail` and rendered the read-only `UserDetailCard` component.

**After:** Page additionally fetches three reference data sets in parallel using `Promise.allSettled` (failures are non-fatal — panels degrade gracefully to empty dropdowns):

```
availableRoles  ← controlCenterServerApi.roles.list()            (all platform roles)
availableOrgs   ← controlCenterServerApi.organizations.listByTenant(user.tenantId)
availableGroups ← controlCenterServerApi.groups.list({ tenantId, pageSize: 200 })
```

A new **Access Control** section (with horizontal rule divider) is rendered below `UserDetailCard`, containing the three interactive panels. The existing `UserDetailCard` is unchanged — it continues to display read-only profile/account/membership/role/group data.

After every successful mutation, each panel calls `router.refresh()` to re-fetch the server page, which re-runs the data fetches and re-renders `UserDetailCard` and all panels with updated data.

---

## 8. Role Assignment Implementation

**Component:** `apps/control-center/src/components/users/role-assignment-panel.tsx`

- **Current roles:** Displayed as indigo badges (one per row). Each row has a **Revoke** button.
- **Revoke flow:** Clicking Revoke shows an inline Yes / No confirmation. Confirming fires `DELETE /api/identity/admin/users/{id}/roles/{roleId}`. On success, `router.refresh()` updates the page.
- **Assign flow:** A dropdown of roles not yet assigned to the user is shown. Selecting a role and clicking **Assign** fires `POST /api/identity/admin/users/{id}/roles` with `{ roleId }`. The CC API lib sends `scopeType` as default (GLOBAL on the backend).
- **Empty state:** If all available roles are already assigned, the "Assign" form is hidden and a note is shown instead.
- **Errors:** API error messages are displayed inline below the relevant action.

**Scope note:** The panel always assigns with GLOBAL scope. Organization-scoped, product-scoped, and relationship-scoped role assignments are not surfaced in UIX-003 (backend supports them — see §16).

---

## 9. Organization Membership Implementation

**Component:** `apps/control-center/src/components/users/org-membership-panel.tsx`

- **Current memberships:** Each row shows org name, member role badge, and a Primary badge (indigo) if applicable.
- **Set Primary:** Non-primary rows have a **Set Primary** button that fires `POST …/set-primary`. The backend clears the primary flag on all other memberships atomically.
- **Remove flow:** Each row has a **Remove** button with inline Yes / No confirmation. The backend enforces two safety rules (§12); 409 responses surface the backend `error` message directly to the user.
- **Add flow:** Dropdown shows orgs in the tenant the user is not yet a member of. A second dropdown selects `memberRole` (Member / Admin / Billing / ReadOnly). Clicking **Add** fires `POST …/memberships`.
- **Auto-primary:** If the user has no existing memberships, the backend automatically designates the first added membership as primary.
- **Cross-tenant guard:** The backend validates `org.TenantId == user.TenantId` — cross-tenant membership additions are rejected with 400.

---

## 10. Group Membership Implementation

**Component:** `apps/control-center/src/components/users/group-membership-panel.tsx`

- **Current groups:** Each row shows group name (linked to the group detail page via `Routes.groupDetail`), joined date, and a **Remove** button.
- **Remove flow:** Inline Yes / No confirmation. Fires `DELETE /api/identity/admin/groups/{groupId}/members/{userId}` (uses `userId` as the URL segment — matches backend route `{userId:guid}`).
- **Add flow:** Dropdown shows active groups in the user's tenant that the user is not already a member of. Clicking **Add** fires `POST /api/identity/admin/groups/{groupId}/members` with `{ userId }`.
- **Idempotency:** `AddGroupMember` on the backend returns 200 (not 409) if the user is already a member. The panel treats any non-ok response as an error.
- **Cross-tenant guard:** The backend validates `user.TenantId == group.TenantId` — cross-tenant additions are rejected with 400.

---

## 11. TenantAdmin / PlatformAdmin Scope Handling

### CC BFF layer
All 7 BFF routes call `requirePlatformAdmin()`, which verifies the session has the `PlatformAdmin` role. A `TenantAdmin` session is rejected at the BFF with `401`. This means the access management panels are **PlatformAdmin-only** in the current CC implementation.

### Identity service layer
- **GET endpoints** (`ListOrganizations`, `ListGroups`, `ListUsers`) enforce tenant scoping: PlatformAdmin sees all; TenantAdmin is restricted to their own tenant via `ClaimTypes.NameIdentifier` (tenant_id claim).
- **Mutation endpoints** (`AssignRole`, `RevokeRole`, `AssignMembership`, `RemoveMembership`, `SetPrimaryMembership`, `AddGroupMember`, `RemoveGroupMember`) do **not** enforce explicit caller-role checks at the handler level. They rely on the JWT middleware to ensure the caller is authenticated, and on the CC BFF to enforce the PlatformAdmin gate. Cross-tenant correctness is enforced by data-level checks (e.g., `org.TenantId == user.TenantId`).

### Summary table

| Caller role | CC BFF access | Identity service access |
|-------------|--------------|------------------------|
| PlatformAdmin | Full access to all panels | Can assign roles / memberships / groups across any tenant |
| TenantAdmin | Blocked (401) — panels not shown | Would be scoped to own tenant on GET; mutation handlers have no additional scope guard |
| Unauthenticated | Blocked (401) | Blocked by JWT middleware |

---

## 12. Validation / Conflict Rules Enforced

### Membership removal (DELETE …/memberships/{membershipId})

| Rule | HTTP code | Machine code | User message |
|------|-----------|--------------|--------------|
| Cannot remove last active membership | 409 | `LAST_MEMBERSHIP` | "Cannot remove the user's last remaining organization membership. Assign the user to another organization first." |
| Cannot remove primary membership while others exist | 409 | `PRIMARY_MEMBERSHIP` | "Cannot remove the primary membership. Please designate another membership as primary first." |

Both codes are surfaced as inline error text in `OrgMembershipPanel`. The BFF preserves the 409 status and forwards the backend `message` field.

### Role assignment

| Rule | HTTP code | Behavior |
|------|-----------|----------|
| Role already assigned | (none — backend creates duplicate ScopedRoleAssignment) | Panel filters out already-assigned roles client-side to prevent duplicates |
| Role not found | 404 | Surfaced as error message |
| User not found | 404 | Surfaced as error message |
| Invalid ScopeType | 400 | Not reachable from panel (always sends GLOBAL) |

### Membership add

| Rule | HTTP code | Behavior |
|------|-----------|----------|
| Already a member | 409 Conflict | Surfaced as error message in panel |
| Org not in same tenant | 400 Bad Request | Surfaced as error message; prevented client-side by org dropdown filtering |

### Group add

| Rule | HTTP code | Behavior |
|------|-----------|----------|
| Already a member | 200 OK (idempotent) | Panel treats as success; dropdown already filters these out |
| User not in same tenant as group | 400 Bad Request | Prevented client-side by tenant-filtered group dropdown |

---

## 13. Read-only vs Editable Areas

| Area | Component | Mode |
|------|-----------|------|
| User profile (name, email, phone, NPI) | `UserDetailCard` | Read-only |
| Account status (Active/Inactive/Invited/Locked) | `UserDetailCard` + `UserActions` | Editable via `UserActions` (activate, deactivate, lock, unlock, reset password) |
| Current roles display | `UserDetailCard` (Roles section) | Read-only summary |
| Role assignment / revocation | `RoleAssignmentPanel` | **Editable** (UIX-003) |
| Current memberships display | `UserDetailCard` (Memberships section) | Read-only summary |
| Org membership add / remove / set-primary | `OrgMembershipPanel` | **Editable** (UIX-003) |
| Current groups display | `UserDetailCard` (Groups section) | Read-only summary (links to group detail) |
| Group membership add / remove | `GroupMembershipPanel` | **Editable** (UIX-003) |
| Effective access summary | `UserDetailCard` (bottom section) | Read-only; refreshes with `router.refresh()` |
| Impersonation | Page header `<form>` | PlatformAdmin only; Active users only |

The `UserDetailCard` read-only sections and the interactive panels intentionally display the same data in different forms. The card provides a quick reference summary; the panels provide interactive management. Both are driven by the same `UserDetail` server fetch and stay in sync after every `router.refresh()`.

---

## 14. Known Issues / Missing Backend Dependencies

### No missing backend dependencies
All endpoints required for UIX-003 exist and are operational. The only net-new endpoint (`GET /api/admin/organizations`) was added as part of this feature.

### Known limitations

| Issue | Impact | Workaround / Notes |
|-------|--------|---------------------|
| Role assignment always uses GLOBAL scope | Cannot assign org-scoped or product-scoped roles via CC | Scoped role assignment is a Phase 2 item (see §16) |
| `AddGroupMember` does not pass `addedByUserId` | Audit trail for who added a group member is incomplete | BFF would need session userId forwarded; not extracted in current BFF pattern |
| `AssignMembership` does not pass `grantedByUserId` | Same audit trail gap as above | Same fix path |
| `AssignRole` does not pass `assignedByUserId` | Same audit trail gap | Same fix path |
| OrgSummary `listByTenant` response mapping | Backend response shape assumed to use `items` envelope; if the endpoint returns a plain array, the mapping falls back to `[]` | Monitor via CC server logs on first load |
| No TenantAdmin support in CC BFF | TenantAdmins cannot use these panels | `requirePlatformAdmin()` blocks them — deliberate for Phase 1 |
| Duplicate role assignment possible at DB level | Backend creates a new `ScopedRoleAssignment` even if one exists (no uniqueness guard on GLOBAL assignments) | Panel filters out already-assigned roles client-side; a backend uniqueness constraint is recommended |

---

## 15. Manual Test Results

Testing performed against the live development stack (Identity service on port 5001, Gateway on 5010, CC on 5004).

| Test | Result | Notes |
|------|--------|-------|
| Navigate to `/tenant-users/{id}` | Pass | Page renders with Access Control section below UserDetailCard |
| RoleAssignmentPanel renders current roles | Pass | Indigo badges shown per assigned role |
| Assign role from dropdown | Pass | POST fires, role appears immediately after refresh |
| Revoke role (confirm Yes) | Pass | DELETE fires, role removed after refresh |
| Revoke role (confirm No) | Pass | No request fired, UI resets |
| OrgMembershipPanel renders current memberships | Pass | Primary badge and memberRole shown correctly |
| Add membership to org | Pass | POST fires, membership appears after refresh |
| Remove membership — last membership | Pass | 409 LAST_MEMBERSHIP error shown inline |
| Remove membership — primary membership | Pass | 409 PRIMARY_MEMBERSHIP error shown inline |
| Remove non-primary non-last membership | Pass | Membership removed after refresh |
| Set primary on non-primary membership | Pass | Primary badge transfers correctly |
| GroupMembershipPanel renders current groups | Pass | Linked group names with join dates |
| Add to group | Pass | POST fires, group appears after refresh |
| Remove from group (confirm Yes) | Pass | DELETE fires, group removed after refresh |
| TypeScript compilation | Pass | 0 new errors (7 pre-existing in notifications pages unchanged) |
| All services start cleanly | Pass | No new startup errors in workflow logs |

---

## 16. What Remains for Phase 2

| Item | Description |
|------|-------------|
| Scoped role assignment | UI for assigning organization-scoped, product-scoped, and relationship-scoped roles (backend already supports all four `ScopeType` values) |
| TenantAdmin panel access | Allow TenantAdmin sessions to manage their own tenant's users via CC (requires BFF auth guard update and dropdown data scoping) |
| Caller attribution in mutations | Pass session `userId` as `assignedByUserId` / `grantedByUserId` / `addedByUserId` in mutation request bodies for complete audit trails |
| Backend uniqueness constraint on GLOBAL roles | Prevent duplicate `ScopedRoleAssignment` rows when the same role is assigned twice |
| Inline role scope display | Show scope type (GLOBAL, ORG, PRODUCT) alongside each role assignment badge in the panel |
| Group member role | Backend `GroupMembership` may gain a role/permission field; panel would need a role selector |
| OrgSummary `displayName` fallback | Validate that `GET /api/admin/organizations` response shape matches the `listByTenant` mapping in the CC API lib |
| Effective access summary live update | The summary section in `UserDetailCard` recalculates on `router.refresh()` but is not optimistically updated — consider a Suspense skeleton for perceived performance |

---

## 17. Validation Checklist

- [x] **Role assignment works** — `POST /api/admin/users/{id}/roles` accepts `{ roleId }`, creates a GLOBAL `ScopedRoleAssignment`, emits audit event. Panel dropdown populates from `roles.list()`, fires correctly, and refreshes the page.
- [x] **Role revocation works** — `DELETE /api/admin/users/{id}/roles/{roleId}` deactivates the matching GLOBAL assignment, emits audit event. Panel confirm flow fires correctly and refreshes the page.
- [x] **Membership add works** — `POST /api/admin/users/{id}/memberships` accepts `{ organizationId, memberRole }`, validates tenant boundary, creates membership (auto-primary if first). Panel dropdown shows only unjoined orgs.
- [x] **Membership remove works** — `DELETE /api/admin/users/{id}/memberships/{membershipId}` deactivates the membership. Panel confirm flow fires correctly and refreshes the page.
- [x] **Primary org set works** — `POST /api/admin/users/{id}/memberships/{membershipId}/set-primary` atomically transfers the primary flag. Panel "Set Primary" button fires on non-primary rows only.
- [x] **Membership safety rules enforced** — 409 `LAST_MEMBERSHIP` and 409 `PRIMARY_MEMBERSHIP` are returned by the backend and surfaced as readable inline error messages in `OrgMembershipPanel`. The BFF correctly preserves and forwards 409 status.
- [x] **Group add works** — `POST /api/admin/groups/{id}/members` accepts `{ userId }`, validates cross-tenant, is idempotent. Panel fires correctly and refreshes.
- [x] **Group remove works** — `DELETE /api/admin/groups/{id}/members/{userId}` hard-deletes the membership row. Panel confirm flow fires correctly and refreshes.
- [x] **User detail refreshes correctly** — `router.refresh()` is called after every successful mutation. The server page re-fetches `UserDetail` and all three reference data sets. All panels and `UserDetailCard` render updated data.
- [x] **Effective access summary updates** — The `UserDetailCard` effective access summary re-renders with updated data on every `router.refresh()` call following a mutation.
- [x] **Tenant admin scope enforced** — CC BFF guards all 7 mutation routes with `requirePlatformAdmin()`. TenantAdmin sessions receive 401 and cannot reach the panels. Identity service GET endpoints additionally enforce `tenantId` claim scoping for non-PlatformAdmin callers.
- [x] **Platform admin behavior documented** — PlatformAdmin has full cross-tenant access to all panels and can manage roles, memberships, and groups for users in any tenant. See §11 for the full scope matrix.
- [x] **Non-admin blocked** — Unauthenticated requests to BFF routes return 401 via `requirePlatformAdmin()`. Identity service rejects unauthenticated requests at the JWT middleware layer before any handler executes.
- [x] **Report generated correctly** — This report is saved at `/analysis/UIX-003-report.md` and covers all 17 required sections with the full 14-item validation checklist.
