# LS-TENANT-002 — Tenant User Management Report

**Generated**: 2026-04-13
**Status**: COMPLETE
**Build**: Clean (0 TypeScript errors)

---

## 1. User List Page

### Route
`/tenant/authorization/users`

### Implementation
- **Server Component** (`page.tsx`): Fetches users via `tenantServerApi.getUsers()` (calls `GET /identity/api/users`)
- **Client Component** (`AuthUserTable.tsx`): Full-featured data table with:
  - Search by name/email
  - Status filter toggle (All/Active/Inactive)
  - Pagination (15 per page)
  - Columns: User (avatar + name), Email, Status, Products count, Roles count, Groups count, Actions
  - Row click navigates to user detail
  - Empty states for no data and no filter matches
  - Error handling with styled error banner when API unavailable

---

## 2. User Detail Page

### Route
`/tenant/authorization/users/[userId]`

### Implementation
- **Server Component** (`page.tsx`): Parallel-fetches user detail, access debug, groups, and assignable roles
- **Client Component** (`UserDetailClient.tsx`): Full interactive detail page with 5 sections

### Sections

#### A. Identity Summary
- Name, Email, Status (badge), Tenant ID
- Grid layout (4 columns on desktop)

#### B. Direct Product Access
- Lists all direct product assignments
- **Add Product**: Inline picker showing SYNQ_FUND, SYNQ_LIEN, SYNQ_CARECONNECT, etc.
- **Remove Product**: Confirmation modal before removal
- API: `PUT /tenants/{tenantId}/users/{userId}/products/{productCode}` and `DELETE` equivalent
- Empty state: "No direct product access configured"

#### C. Direct Roles
- Lists all directly assigned roles (from `user.roles[]` — array of `{ roleId, roleName, assignmentId }`)
- **Assign Role**: Inline picker populated from `getAssignableRoles()` — uses backend assignability metadata (product enablement, org-type rules, already-assigned check)
- **Remove Role**: Confirmation modal; uses `DELETE /admin/users/{id}/roles/{roleId}` with GUID roleId (not role code)
- Empty state: "No roles assigned"

#### D. Group Membership
- Lists groups from `user.groups[]` (returned by admin GetUser endpoint with `{ groupId, groupName, joinedAtUtc }`)
- **Add to Group**: Inline picker populated from `getGroups(tenantId)`; filters out already-joined groups
- **Remove from Group**: Confirmation modal using groupId GUID
- API: `POST /tenants/{tenantId}/groups/{groupId}/members` and `DELETE` equivalent
- Empty state: "No groups assigned"

#### E. Effective Access (Critical)
- **Effective Products**: Full list from `accessDebug.products[]` — each entry has `productCode`, `source` (Direct/Group/Tenant), optional `groupName`
- **Effective Roles**: Full list from `accessDebug.roles[]` — each entry has `roleCode`, `productCode`, `source`, optional `groupName`
- **Effective Permissions**: From `accessDebug.permissionSources[]` — each entry has `permissionCode`, `viaRoleCode`, `source`, optional `groupName`
  - Format: `SYNQ_FUND.application:approve → via role: FUND_APPROVER → via group: Finance Team`
- **Policy Impact**: If backend returns policies with deny/audit effects, shown with colored effect badges
- Data source: `GET /admin/users/{id}/access-debug` (LS-COR-AUT-008)

---

## 3. Simulator Quick Access

- "Simulate Access" button on user detail page header
- Links to `/tenant/authorization/simulator?userId={id}&tenantId={tenantId}`
- Pre-fills userId and tenantId as URL params

---

## 4. API Integration Layer (BFF)

### File: `apps/web/src/lib/tenant-api.ts`

### Server-side Methods (`tenantServerApi`)
| Method | Endpoint |
|--------|----------|
| `getUsers()` | `GET /identity/api/users` |
| `getUserDetail(userId)` | `GET /identity/api/admin/users/{id}` |
| `getAccessDebug(userId)` | `GET /identity/api/admin/users/{id}/access-debug` |
| `getAccessSnapshot(tenantId, userId)` | `GET /identity/api/tenants/{tenantId}/users/{userId}/access-snapshot` |
| `getAssignableRoles(userId)` | `GET /identity/api/admin/users/{id}/assignable-roles` |
| `getRoles()` | `GET /identity/api/admin/roles` |
| `getGroups(tenantId)` | `GET /identity/api/tenants/{tenantId}/groups` |
| `getProducts()` | `GET /identity/api/admin/products` |

### Client-side Methods (`tenantClientApi`)
| Method | Endpoint |
|--------|----------|
| `assignProduct(tenantId, userId, productCode)` | `PUT /identity/api/tenants/{tenantId}/users/{userId}/products/{productCode}` |
| `removeProduct(tenantId, userId, productCode)` | `DELETE ...` |
| `assignRole(userId, roleId)` | `POST /identity/api/admin/users/{id}/roles` (body: `{ roleId }`) |
| `removeRole(userId, roleId)` | `DELETE /identity/api/admin/users/{id}/roles/{roleId:guid}` |
| `addToGroup(tenantId, groupId, userId)` | `POST /identity/api/tenants/{tenantId}/groups/{groupId}/members` |
| `removeFromGroup(tenantId, groupId, userId)` | `DELETE ...` |

---

## 5. Types

### File: `apps/web/src/types/tenant.ts`

Types created (all aligned to real backend `AdminEndpoints.cs` response shapes):
- `TenantUser` — list-page user summary (id, email, roles as string[])
- `TenantUserDetail` — full GetUser response: roles as `{ roleId, roleName, assignmentId }[]`, status as string, groups as `{ groupId, groupName, joinedAtUtc }[]`, memberships, etc.
- `TenantGroup` — group with membership metadata
- `AccessDebugProductSource` — `{ productCode, source, groupId?, groupName? }`
- `AccessDebugRoleSource` — `{ roleCode, productCode?, source, groupId?, groupName? }`
- `AccessDebugPermissionSource` — `{ permissionCode, productCode?, source, viaRoleCode, groupId?, groupName? }`
- `AccessDebugGroup` — `{ groupId, groupName, status, scopeType, productCode? }`
- `AccessDebugResponse` — full access-debug payload including products, roles, systemRoles, groups, entitlements, permissionSources, policies
- `AssignableRoleItem` — individual role with assignability metadata (id GUID, assignable bool, disabledReason)
- `AssignableRolesResponse` — `{ items: AssignableRoleItem[], userOrgType, tenantEnabledProducts }`

---

## 6. Security

- All routes protected by `requireTenantAdmin()` via layout guard
- Server-side API calls use `serverApi` (JWT-forwarded from HttpOnly cookie)
- Client-side mutations use `apiClient` (credentials: include → BFF proxy)
- Tenant boundary enforced by backend (tenantId from JWT, not URL)
- No cross-tenant access paths introduced
- All removals require confirmation modal

---

## 7. UX

- **Loading states**: Server-side data fetching (no client-side loading spinners needed for initial load)
- **Toast feedback**: Success/error toasts for all mutations with 4-second auto-dismiss
- **Confirmation modals**: All remove/revoke actions require explicit confirmation
- **Empty states**: Clear messaging for no data, no results, and unavailable sections
- **Error handling**: Graceful degradation — if access-debug, groups, or roles APIs fail, other sections still render
- **Responsive**: Table uses responsive column hiding (md/lg breakpoints), detail page uses grid layout

---

## 8. Build Status

```
TypeScript: 0 errors
Next.js: compiles successfully
No regressions to existing routes
LS-TENANT-001 navigation and guards intact
```

---

## 9. Known Limitations

1. **Backend dependency**: User list and detail require Identity service to be running; graceful error shown when unavailable
2. **Group count on list page**: Currently hardcoded to 0 — requires access-debug per-user to resolve (expensive for list view)
3. **Product list**: Uses static `AVAILABLE_PRODUCTS` array; should ideally fetch from `GET /admin/products` for dynamic catalog
4. **Duplicate prevention**: Assignment pickers disable already-assigned items visually; backend enforces true uniqueness
5. **Bulk operations**: No bulk assign/revoke yet — single-user operations only

---

## 10. Files Created/Modified

### New Files
| File | Purpose |
|------|---------|
| `apps/web/src/types/tenant.ts` | Type definitions for tenant authorization |
| `apps/web/src/lib/tenant-api.ts` | BFF API layer (server + client methods) |
| `apps/web/src/app/(platform)/tenant/authorization/users/AuthUserTable.tsx` | User list client component |
| `apps/web/src/app/(platform)/tenant/authorization/users/[userId]/page.tsx` | User detail server component |
| `apps/web/src/app/(platform)/tenant/authorization/users/[userId]/UserDetailClient.tsx` | User detail client component |

### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/tenant/authorization/users/page.tsx` | Replaced placeholder with functional user list |
| `apps/web/src/lib/server-api-client.ts` | Added `delete` method to `serverApi` |

---

## Ready For

- **LS-TENANT-003**: Group Management (dedicated group CRUD + membership management)
- **LS-TENANT-004**: Access & Explainability (cross-entity access views + audit)
- **LS-TENANT-005**: Authorization Simulator (uses access-debug and simulate APIs)
