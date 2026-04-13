# LS-TENANT-003 — Group Management Report

**Generated**: 2026-04-13
**Status**: COMPLETE
**Build**: Clean (0 TypeScript errors)

---

## 1. Group List Page

### Route
`/tenant/authorization/groups`

### Implementation
- **Server Component** (`page.tsx`): Fetches groups via `tenantServerApi.getGroups(tenantId)` and users via `getUsers()` (for member picker in create flow)
- **Client Component** (`GroupTable.tsx`): Full-featured group table with search, filter, pagination, and create modal

### Features
- **Search**: by group name and description
- **Status Filter**: All / Active / Archived toggle
- **Pagination**: 15 rows per page with Previous/Next controls
- **Create Group**: Modal with Name (required) and Description (optional) fields
- **Row Click**: Navigates to group detail page
- **Empty State**: Differentiated empty (no groups yet) vs. no search results

### Table Columns
- Group Name (with icon)
- Description (hidden on mobile)
- Status (Active/Archived badge)
- Scope (Tenant/Product/Organization — hidden on mobile)
- Created date (hidden on mobile)

### API
```
GET /identity/api/tenants/{tenantId}/groups
POST /identity/api/tenants/{tenantId}/groups
```

---

## 2. Group Detail Page

### Route
`/tenant/authorization/groups/[groupId]`

### Implementation
- **Server Component** (`page.tsx`): Fetches group, members, products, roles, all users (for picker), and all roles (for picker) via `Promise.allSettled`
- **Client Component** (`GroupDetailClient.tsx`): Full detail with 5 sections + impact indicator + edit/archive actions

### Layout Sections

#### A. Summary
- Status badge (Active/Archived)
- Scope type badge
- Member count
- Created date

#### B. Impact Indicator
- Amber banner: "This group affects X users"
- Shows count of products and roles inherited by members

#### C. Members (Critical)
- Lists active members with initials avatar, name, email
- **Add Member**: Searchable user picker — filters by name/email, excludes already-added users, limits to 20 results
- **Remove Member**: Confirmation modal
- API: `POST /tenants/{tenantId}/groups/{groupId}/members` and `DELETE .../members/{userId}`
- Empty state: "No members in this group"

#### D. Product Access (Group-Level)
- Lists products assigned to group with product code
- **Add Product**: Inline picker, excludes already-assigned
- **Revoke Product**: Confirmation modal with warning about inherited access loss
- API: `PUT /tenants/{tenantId}/groups/{groupId}/products/{productCode}` and `DELETE` equivalent
- Empty state: "No product access configured"

#### E. Role Assignments (Group-Level)
- Lists roles assigned to group with optional product code
- **Assign Role**: Inline picker, excludes already-assigned
- **Remove Role**: Confirmation modal with warning about member impact
- API: `POST /tenants/{tenantId}/groups/{groupId}/roles` (body: `{roleCode, productCode?}`) and `DELETE .../roles/{assignmentId:guid}`
- Empty state: "No roles assigned"

#### F. Effective Access Preview
- Shows what products and roles this group grants to its members
- Each item shows "Group" source badge
- Footer note: "All X members of this group inherit the above access"
- Derived from local products + roles data (no separate endpoint needed)

### Additional Actions
- **Edit Group**: Modal with name/description fields, uses `PATCH /tenants/{tenantId}/groups/{groupId}`
- **Archive Group**: Confirmation modal with warning, uses `DELETE /tenants/{tenantId}/groups/{groupId}`, redirects to list

---

## 3. API Integration Layer

### File: `apps/web/src/lib/tenant-api.ts`

### Server-side Methods (`tenantServerApi`)
| Method | Endpoint |
|--------|----------|
| `getGroups(tenantId)` | `GET /identity/api/tenants/{tenantId}/groups` |
| `getGroup(tenantId, groupId)` | `GET /identity/api/tenants/{tenantId}/groups/{groupId}` |
| `getGroupMembers(tenantId, groupId)` | `GET /identity/api/tenants/{tenantId}/groups/{groupId}/members` |
| `getGroupProducts(tenantId, groupId)` | `GET /identity/api/tenants/{tenantId}/groups/{groupId}/products` |
| `getGroupRoles(tenantId, groupId)` | `GET /identity/api/tenants/{tenantId}/groups/{groupId}/roles` |

### Client-side Methods (`tenantClientApi`)
| Method | Endpoint |
|--------|----------|
| `createGroup(tenantId, body)` | `POST /identity/api/tenants/{tenantId}/groups` |
| `updateGroup(tenantId, groupId, body)` | `PATCH /identity/api/tenants/{tenantId}/groups/{groupId}` |
| `archiveGroup(tenantId, groupId)` | `DELETE /identity/api/tenants/{tenantId}/groups/{groupId}` |
| `grantGroupProduct(tenantId, groupId, productCode)` | `PUT /identity/api/tenants/{tenantId}/groups/{groupId}/products/{productCode}` |
| `revokeGroupProduct(tenantId, groupId, productCode)` | `DELETE ...` |
| `assignGroupRole(tenantId, groupId, roleCode, productCode?)` | `POST /identity/api/tenants/{tenantId}/groups/{groupId}/roles` |
| `removeGroupRole(tenantId, groupId, assignmentId)` | `DELETE .../roles/{assignmentId:guid}` |

---

## 4. Types

### File: `apps/web/src/types/tenant.ts`

New types added (aligned to `GroupEndpoints.cs` response shapes):
- `TenantGroup` — `{ id, tenantId, name, description?, status, scopeType, productCode?, organizationId?, createdAtUtc, updatedAtUtc }` (from `MapGroup()`)
- `GroupMember` — `{ id, tenantId, groupId, userId, membershipStatus, addedAtUtc, removedAtUtc? }` (from `ListMembers`)
- `GroupProductAccess` — `{ id, tenantId, groupId, productCode, accessStatus, grantedAtUtc, revokedAtUtc? }` (from `ListGroupProducts`)
- `GroupRoleAssignment` — `{ id, tenantId, groupId, roleCode, productCode?, organizationId?, assignmentStatus, assignedAtUtc, removedAtUtc? }` (from `ListGroupRoles`)

---

## 5. Security

- All routes protected by `requireTenantAdmin()` via layout guard
- Server-side API calls use `serverApi` (JWT-forwarded from HttpOnly cookie)
- Client-side mutations use `apiClient` (credentials: include via BFF proxy)
- Tenant boundary enforced by backend (`CanReadTenant` / `CanMutateTenant` checks in `GroupEndpoints.cs`)
- No cross-tenant access paths introduced
- All removals/revocations require confirmation modal

---

## 6. UX

- **Toast feedback**: Success/error toasts for all mutations with 4-second auto-dismiss
- **Confirmation modals**: All destructive actions (remove member, revoke product, remove role, archive group) require explicit confirmation with impact warnings
- **Empty states**: Clear messaging for each section (no members, no products, no roles)
- **Searchable user picker**: Real-time search across name/email with avatar initials, excludes existing members
- **Responsive**: Table uses responsive column hiding (md/lg breakpoints), detail page uses grid layout
- **Error handling**: Graceful degradation — if any secondary fetch fails, other sections still render
- **Impact indicator**: Persistent amber banner showing member count and inheritance scope

---

## 7. Build Status

```
TypeScript: 0 errors
Next.js: compiles successfully
No regressions to existing routes
LS-TENANT-001 navigation and guards intact
LS-TENANT-002 user management intact
```

---

## 8. Known Limitations

1. **Product catalog**: Uses static `AVAILABLE_PRODUCTS` array; should ideally fetch from `GET /admin/products` for dynamic catalog
2. **Member count on list page**: Not available from `ListGroups` API (no count returned); shown only on detail page
3. **Access preview**: Derived from local products/roles data rather than a dedicated backend endpoint; permissions are not shown (would require per-role permission resolution)
4. **Bulk operations**: No bulk member add/remove — single operations only
5. **Role picker**: Uses flat `GET /admin/roles` list without product/org-type filtering (backend enforces validity)

---

## 9. Files Created/Modified

### New Files
| File | Purpose |
|------|---------|
| `apps/web/src/app/(platform)/tenant/authorization/groups/GroupTable.tsx` | Group list client component |
| `apps/web/src/app/(platform)/tenant/authorization/groups/[groupId]/page.tsx` | Group detail server component |
| `apps/web/src/app/(platform)/tenant/authorization/groups/[groupId]/GroupDetailClient.tsx` | Group detail client component |

### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/tenant/authorization/groups/page.tsx` | Replaced placeholder with functional group list |
| `apps/web/src/types/tenant.ts` | Added `TenantGroup` (corrected), `GroupMember`, `GroupProductAccess`, `GroupRoleAssignment` |
| `apps/web/src/lib/tenant-api.ts` | Added 10 group API methods (5 server-side, 5 client-side) |

---

## Ready For

- **LS-TENANT-004**: Access & Explainability (cross-entity access views + audit)
- **LS-TENANT-005**: Access Simulator (simulate access for user/group combinations)
