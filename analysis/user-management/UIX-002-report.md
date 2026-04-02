# UIX-002 — Tenant User Management (MVP) — Implementation Report

**Date:** 2026-04-01  
**Status:** COMPLETED — MVP scope fully implemented  
**Scope:** Control Center admin app (`apps/control-center`) + Identity service (`apps/services/identity`)

---

## 1. Summary

UIX-002 delivers the Tenant User Management MVP for the Control Center. It covers the backend domain layer, EF Core infrastructure, database migration, 12 new API endpoints, and the complete CC frontend (types, API client, mappers, BFF routes, nav, pages, and components).

---

## 2. Backend — Identity Service

### 2.1 Domain Layer Changes

| File | Change |
|---|---|
| `Identity.Domain/User.cs` | Added `Activate()` lifecycle method |
| `Identity.Domain/UserOrganizationMembership.cs` | Added `IsPrimary`, `SetPrimary()`, `ClearPrimary()` |
| `Identity.Domain/TenantGroup.cs` | **New entity** — `Create()`, `Deactivate()` |
| `Identity.Domain/GroupMembership.cs` | **New entity** — `Create()`, lifecycle |
| `Identity.Domain/UserInvitation.cs` | **New entity** — `Create()`, `MarkAccepted()`, `MarkExpired()`, `PortalOrigin` enum |

### 2.2 Infrastructure Layer Changes

| File | Change |
|---|---|
| `Identity.Infrastructure/Data/IdentityDbContext.cs` | Added `TenantGroups`, `GroupMemberships`, `UserInvitations` DbSets |
| `Identity.Infrastructure/Persistence/EntityConfigurations/TenantGroupConfiguration.cs` | **New** — EF Core config |
| `Identity.Infrastructure/Persistence/EntityConfigurations/GroupMembershipConfiguration.cs` | **New** — EF Core config |
| `Identity.Infrastructure/Persistence/EntityConfigurations/UserInvitationConfiguration.cs` | **New** — EF Core config |

### 2.3 Migration

**`20260401000001_UIX002_UserManagement`** (manually authored, auto-applies on startup via `db.Database.Migrate()`)

- Adds `IsPrimary` column to `UserOrganizationMemberships`
- Creates `TenantGroups` table (Id, TenantId, Name, Description, IsActive, CreatedAt, UpdatedAt)
- Creates `GroupMemberships` table (Id, GroupId, UserId, JoinedAt)
- Creates `UserInvitations` table (Id, UserId, Email, TenantId, TokenHash, PortalOrigin, ExpiresAt, AcceptedAt, CreatedAt)

### 2.4 Admin Endpoints

All endpoints are registered in `Identity.Api/Endpoints/AdminEndpoints.cs`.

#### User Lifecycle
| Method | Route | Handler | Status |
|---|---|---|---|
| POST | `/admin/users/{userId}/activate` | `ActivateUser` | MVP ✓ |
| POST | `/admin/users/invite` | `InviteUser` | MVP ✓ |
| POST | `/admin/users/{userId}/resend-invite` | `ResendInvite` | MVP ✓ |

#### Org Memberships
| Method | Route | Handler | Status |
|---|---|---|---|
| POST | `/admin/users/{userId}/memberships` | `AssignMembership` | MVP ✓ |
| POST | `/admin/users/{userId}/memberships/{membershipId}/set-primary` | `SetPrimaryMembership` | MVP ✓ |
| DELETE | `/admin/users/{userId}/memberships/{membershipId}` | `RemoveMembership` | MVP ✓ |

#### Groups
| Method | Route | Handler | Status |
|---|---|---|---|
| GET | `/admin/groups` | `ListGroups` | MVP ✓ |
| GET | `/admin/groups/{id}` | `GetGroup` | MVP ✓ |
| POST | `/admin/groups` | `CreateGroup` | MVP ✓ |
| POST | `/admin/groups/{id}/members` | `AddGroupMember` | MVP ✓ |
| DELETE | `/admin/groups/{id}/members/{membershipId}` | `RemoveGroupMember` | MVP ✓ |

#### Permissions
| Method | Route | Handler | Status |
|---|---|---|---|
| GET | `/admin/permissions` | `ListPermissions` | MVP ✓ |

#### Enhanced Existing Endpoints
- **`GetUser`** now returns `memberships[]`, `groups[]`, `roles[]` arrays
- **`ListUsers`** now returns `status: "Invited"` for pending invitations, `primaryOrg`, `groupCount`

### 2.5 Invite Flow
- Raw invite token logged to console in dev (no email sender wired yet)
- SHA-256 hash stored in `UserInvitations.TokenHash`
- `PortalOrigin.CONTROL_CENTER` carried in the invitation record

---

## 3. Frontend — Control Center

### 3.1 Type Layer (`types/control-center.ts`)

New interfaces added:
- `OrgMembershipSummary` — org membership record for user detail
- `UserGroupSummary` — group membership stub for user detail
- `UserRoleSummary` — role assignment for user detail
- `GroupSummary` — group list item
- `GroupMemberSummary` — member within a group detail
- `GroupDetail` (extends `GroupSummary`) — full group record with members array
- `PermissionCatalogItem` — platform permission entry

Extended interfaces:
- `UserSummary` — added `primaryOrg?`, `groupCount?`
- `UserDetail` — added `memberships?`, `groups?`, `roles?`

### 3.2 API Client (`lib/control-center-api.ts`)

New sections:
```
users.activate(id)
users.deactivate(id)
users.invite(payload)
users.resendInvite(id)
users.assignMembership(id, payload)
users.setPrimaryMembership(id, membershipId)
users.removeMembership(id, membershipId)

groups.list(params)
groups.getById(id)
groups.create(payload)
groups.addMember(groupId, userId)
groups.removeMember(groupId, membershipId)

permissions.list()
```

### 3.3 Mappers (`lib/api-mappers.ts`)

- `mapUserSummary` — extended to map `primaryOrg`, `groupCount`
- `mapUserDetail` — extended to map `memberships[]`, `groups[]`, `roles[]` sub-arrays
- `mapGroupSummary` — **new**
- `mapGroupDetail` — **new** (includes members sub-array)
- `mapPermissionCatalogItem` — **new**

### 3.4 Navigation (`lib/nav.ts`)

Added to IDENTITY section:
- **Groups** — `/groups` (icon: `ri-team-line`)
- **Permissions** — `/permissions` (icon: `ri-key-2-line`)

### 3.5 Routes (`lib/routes.ts`)

- `Routes.groups` — `/groups`
- `Routes.groupDetail(id)` — `/groups/:id`
- `Routes.permissions` — `/permissions`

### 3.6 New Pages

| Path | Component | Notes |
|---|---|---|
| `/groups` | `GroupsPage` | Server component, paginated group list |
| `/groups/[id]` | `GroupDetailPage` | Server component, group detail + member table |
| `/permissions` | `PermissionsPage` | Server component, permission catalog grouped by product |
| `/tenant-users/invite` | `InviteUserPage` | Client component, invite form |

All new pages have corresponding `loading.tsx` skeletons.

### 3.7 New Components (`components/users/`)

| Component | Purpose |
|---|---|
| `GroupListTable` | Paginated group list with name, members, status, created columns |
| `GroupDetailCard` | Group info panel + member table with user links |
| `PermissionCatalogTable` | Permission list with code, name, description, product, status |

### 3.8 Updated Components

| Component | Change |
|---|---|
| `UserListTable` | Added Primary Org and Groups columns |
| `UserDetailCard` | Added Org Memberships, Groups, Role Assignments panels below Account Status |
| `UserActions` | **Wired** `activate`, `deactivate`, `resend-invite` to real BFF routes; `router.refresh()` after success |
| `tenant-users/page.tsx` | Invite User button now links to `/tenant-users/invite` |

### 3.9 BFF Proxy Routes (`app/api/identity/admin/`)

| Route | Handler |
|---|---|
| `POST /api/identity/admin/users/[id]/activate` | `controlCenterServerApi.users.activate()` |
| `POST /api/identity/admin/users/[id]/deactivate` | `controlCenterServerApi.users.deactivate()` |
| `POST /api/identity/admin/users/[id]/resend-invite` | `controlCenterServerApi.users.resendInvite()` |
| `POST /api/identity/admin/users/invite` | `controlCenterServerApi.users.invite()` |

---

## 4. Phase 2 Items (Deferred)

Per UIX-001-01.md strict MVP/Phase 2 boundary:

| Feature | Reason Deferred |
|---|---|
| Avatar upload (A09) | Requires blob storage |
| MFA enrollment (A10–A11) | Requires TOTP infrastructure |
| Backup codes (A12–A13) | Depends on MFA |
| Lock/unlock user (A18–A19) | Phase 2 security feature |
| Force logout / session version bump (A22) | Phase 2 |
| Real email sender for invitations | Requires SMTP/provider config |
| Group deactivation endpoint | Not surfaced in CC yet |
| Tenant scoping in invite form | Currently requires manual UUID entry |

---

## 5. Build Verification

| Layer | Result |
|---|---|
| .NET Identity service (`Identity.Api.csproj`) | **Build succeeded** — 0 errors, 0 warnings |
| CC TypeScript (`npx tsc --noEmit`) | **Clean** — 0 new errors (3 pre-existing notifications errors excluded) |

---

## 6. Files Changed / Created

### Backend (Identity service)
```
Identity.Domain/User.cs                                              (modified)
Identity.Domain/UserOrganizationMembership.cs                       (modified)
Identity.Domain/TenantGroup.cs                                      (new)
Identity.Domain/GroupMembership.cs                                  (new)
Identity.Domain/UserInvitation.cs                                   (new)
Identity.Infrastructure/Data/IdentityDbContext.cs                   (modified)
Identity.Infrastructure/Persistence/EntityConfigurations/TenantGroupConfiguration.cs     (new)
Identity.Infrastructure/Persistence/EntityConfigurations/GroupMembershipConfiguration.cs (new)
Identity.Infrastructure/Persistence/EntityConfigurations/UserInvitationConfiguration.cs  (new)
Identity.Infrastructure/Persistence/Migrations/20260401000001_UIX002_UserManagement.cs  (new)
Identity.Api/Endpoints/AdminEndpoints.cs                            (modified — 12 new handlers)
```

### Frontend (Control Center)
```
src/types/control-center.ts                                          (modified)
src/lib/api-mappers.ts                                               (modified)
src/lib/control-center-api.ts                                        (modified)
src/lib/nav.ts                                                       (modified)
src/lib/routes.ts                                                    (modified)
src/components/users/user-list-table.tsx                             (modified)
src/components/users/user-detail-card.tsx                            (modified)
src/components/users/user-actions.tsx                                (modified — wired)
src/components/users/group-list-table.tsx                            (new)
src/components/users/group-detail-card.tsx                           (new)
src/components/users/permission-catalog-table.tsx                    (new)
src/app/groups/page.tsx                                              (new)
src/app/groups/loading.tsx                                           (new)
src/app/groups/[id]/page.tsx                                         (new)
src/app/groups/[id]/loading.tsx                                      (new)
src/app/permissions/page.tsx                                         (new)
src/app/permissions/loading.tsx                                      (new)
src/app/tenant-users/invite/page.tsx                                 (new)
src/app/tenant-users/page.tsx                                        (modified — Invite User button wired)
src/app/api/identity/admin/users/[id]/activate/route.ts             (new)
src/app/api/identity/admin/users/[id]/deactivate/route.ts           (new)
src/app/api/identity/admin/users/[id]/resend-invite/route.ts        (new)
src/app/api/identity/admin/users/invite/route.ts                    (new)
```
