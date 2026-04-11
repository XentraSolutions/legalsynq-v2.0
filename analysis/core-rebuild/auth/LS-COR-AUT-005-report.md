# LS-COR-AUT-005: Admin UI Access Management Layer

## Summary

Upgrades the Control Center Groups UI to use the new LS-COR-AUT-004 tenant-scoped Access Group endpoints (`/api/tenants/{tenantId}/groups/...`) instead of the legacy `/identity/api/admin/groups` endpoints. Adds full CRUD for groups, membership management, product access grants, and role assignments. Integrates access group membership into the user detail page.

## Prerequisite

- **LS-COR-AUT-004** (Groups + Inherited Access) — Backend entities, services, and endpoints for `AccessGroup`, `AccessGroupMembership`, `GroupProductAccess`, and `GroupRoleAssignment` in the Identity service.

## Architecture

```
Browser (CC Admin)
  │
  ├─ Server Components → controlCenterServerApi.accessGroups.*
  │     └─ apiClient.get/post/patch/put/del
  │           └─ /identity/api/tenants/{tenantId}/groups/...
  │                 └─ Gateway (YARP :5010) strips /identity prefix
  │                       └─ Identity Service (:5001) /api/tenants/{tenantId}/groups/...
  │
  └─ Client Components ('use client') → fetch('/api/access-groups/...')
        └─ BFF Route Handlers (Next.js API routes)
              └─ controlCenterServerApi.accessGroups.*
                    └─ (same gateway path as above)
```

## Changes

### T001: Types + API Mappers

| File | Change |
|------|--------|
| `types/control-center.ts` | Added `AccessGroupStatus`, `AccessGroupScopeType`, `AccessGroupMembershipStatus`, `GroupProductAccessStatus`, `GroupRoleAssignmentStatus` union types |
| `types/control-center.ts` | Added `AccessGroupSummary`, `AccessGroupMember`, `GroupProductAccess`, `GroupRoleAssignment` interfaces |
| `lib/api-mappers.ts` | Added `mapAccessGroupSummary()` — snake_case/camelCase normalization with defaults |
| `lib/api-mappers.ts` | Added `mapAccessGroupMember()` — membership status normalization |
| `lib/api-mappers.ts` | Added `mapGroupProductAccess()` — product access status normalization |
| `lib/api-mappers.ts` | Added `mapGroupRoleAssignment()` — role assignment status normalization |
| `lib/api-client.ts` | Added `cc:access-groups` cache tag |

### T002: API Client Methods

| File | Change |
|------|--------|
| `lib/control-center-api.ts` | Added `accessGroups` namespace with 15 methods |

**Methods added to `controlCenterServerApi.accessGroups`:**

| Method | HTTP | Gateway Path | Returns |
|--------|------|-------------|---------|
| `list(tenantId)` | GET | `/identity/api/tenants/{tenantId}/groups` | `AccessGroupSummary[]` |
| `getById(tenantId, groupId)` | GET | `/identity/api/tenants/{tenantId}/groups/{groupId}` | `AccessGroupSummary \| null` |
| `create(tenantId, body)` | POST | `/identity/api/tenants/{tenantId}/groups` | `AccessGroupSummary` |
| `update(tenantId, groupId, body)` | PATCH | `/identity/api/tenants/{tenantId}/groups/{groupId}` | `AccessGroupSummary` |
| `archive(tenantId, groupId)` | DELETE | `/identity/api/tenants/{tenantId}/groups/{groupId}` | `void` |
| `listMembers(tenantId, groupId)` | GET | `…/groups/{groupId}/members` | `AccessGroupMember[]` |
| `addMember(tenantId, groupId, userId)` | POST | `…/groups/{groupId}/members` | `void` |
| `removeMember(tenantId, groupId, userId)` | DELETE | `…/groups/{groupId}/members/{userId}` | `void` |
| `listProducts(tenantId, groupId)` | GET | `…/groups/{groupId}/products` | `GroupProductAccess[]` |
| `grantProduct(tenantId, groupId, productCode)` | PUT | `…/groups/{groupId}/products/{productCode}` | `void` |
| `revokeProduct(tenantId, groupId, productCode)` | DELETE | `…/groups/{groupId}/products/{productCode}` | `void` |
| `listRoles(tenantId, groupId)` | GET | `…/groups/{groupId}/roles` | `GroupRoleAssignment[]` |
| `assignRole(tenantId, groupId, body)` | POST | `…/groups/{groupId}/roles` | `void` |
| `removeRole(tenantId, groupId, assignmentId)` | DELETE | `…/groups/{groupId}/roles/{assignmentId}` | `void` |
| `listUserGroups(tenantId, userId)` | GET | `/identity/api/tenants/{tenantId}/users/{userId}/groups` | `AccessGroupMember[]` |

**Cache invalidation:** All write operations revalidate `cc:access-groups` tag. Membership/product/role mutations also revalidate `cc:users` to ensure user detail pages reflect changes.

### T003: BFF Routes for Client Mutations

7 route files created under `app/api/access-groups/[tenantId]/...`:

| Route | Methods | Purpose |
|-------|---------|---------|
| `/api/access-groups/[tenantId]` | POST | Create group |
| `/api/access-groups/[tenantId]/[groupId]` | PATCH, DELETE | Update / Archive group |
| `/api/access-groups/[tenantId]/[groupId]/members` | POST | Add member |
| `/api/access-groups/[tenantId]/[groupId]/members/[userId]` | DELETE | Remove member |
| `/api/access-groups/[tenantId]/[groupId]/products/[productCode]` | PUT, DELETE | Grant / Revoke product |
| `/api/access-groups/[tenantId]/[groupId]/roles` | POST | Assign role |
| `/api/access-groups/[tenantId]/[groupId]/roles/[assignmentId]` | DELETE | Remove role |

**Security:**
- All routes enforce `requireAdmin()` (returns 401 on failure)
- Request body validation with early 400 returns for missing required fields
- `ServerApiError` status passthrough — upstream 400/403/404/409 codes are preserved, not blanket 500

**Total BFF route lines:** 253

### T004: Groups List Page Upgrade

| File | Change |
|------|--------|
| `app/groups/page.tsx` | Rewritten to be tenant-context-aware |
| `components/access-groups/access-group-list-table.tsx` | New component — sortable table with scope/status badges |
| `components/access-groups/create-access-group-button.tsx` | New component — modal dialog for group creation |
| `lib/routes.ts` | Added `Routes.accessGroupDetail(tenantId, groupId)` |
| `lib/nav.ts` | Groups sidebar entry updated with `badge: 'LIVE'` |

**Page behavior:**
- **With tenant context:** Fetches and displays `AccessGroupSummary[]` from new API; shows "Create Group" button
- **Without tenant context:** Shows warning banner; falls back to legacy `groups.list()` in read-only mode
- **Scope badges:** Tenant (blue), Product (purple) with product code label
- **Status badges:** Active (green), Archived (gray)

**Create Group modal:**
- Fields: Name (required), Description (optional), Scope (Tenant/Product), Product Code (conditional on Product scope)
- Organization scope intentionally omitted from UI (backend supports it but no organizationId picker exists yet)

### T005: Group Detail Page

| File | Change |
|------|--------|
| `app/access-groups/[tenantId]/[groupId]/page.tsx` | New detail page with parallel data fetching |
| `components/access-groups/access-group-info-card.tsx` | Group metadata card (ID, tenant, scope, timestamps) |
| `components/access-groups/access-group-members-panel.tsx` | Members list with add/remove (user picker dropdown) |
| `components/access-groups/group-product-access-panel.tsx` | Product grant/revoke toggles (FUND, CARECONNECT, DOCUMENTS, NOTIFICATIONS) |
| `components/access-groups/group-role-assignment-panel.tsx` | Role assign/remove with role code + optional product/org scope |
| `components/access-groups/access-group-actions.tsx` | Archive confirmation dialog |

**Data loading strategy:**
- Group summary fetched first; if successful, members, products, roles, and available users fetched in parallel via `Promise.allSettled`
- Active-only filtering applied client-side (`membershipStatus === 'Active'`, `accessStatus === 'Granted'`, `assignmentStatus === 'Active'`)

**Layout:** Two-column grid on large screens — members panel (left), products + roles stacked (right)

**Route:** `/access-groups/[tenantId]/[groupId]` — uses different path prefix from legacy `/groups/[id]` to avoid Next.js dynamic segment name conflicts

### T006: User Detail Integration

| File | Change |
|------|--------|
| `components/access-groups/access-group-membership-panel.tsx` | New panel showing user's access group memberships |
| `app/tenant-users/[id]/page.tsx` | Updated to fetch access groups list and user group memberships in parallel; renders `AccessGroupMembershipPanel` below legacy `GroupMembershipPanel` |

**AccessGroupMembershipPanel features:**
- Lists groups the user belongs to (filtered to Active memberships)
- "Add to Group" dropdown with groups the user is not already a member of
- Remove membership with confirmation
- Links group names to the group detail page via `Routes.accessGroupDetail()`
- Graceful "no tenant context" handling — panel hidden when no tenant is selected

## New Files Created

```
apps/control-center/src/
  app/
    access-groups/[tenantId]/[groupId]/page.tsx          (128 lines)
    api/access-groups/[tenantId]/route.ts                 (38 lines)
    api/access-groups/[tenantId]/[groupId]/route.ts       (55 lines)
    api/access-groups/[tenantId]/[groupId]/members/route.ts       (32 lines)
    api/access-groups/[tenantId]/[groupId]/members/[userId]/route.ts   (24 lines)
    api/access-groups/[tenantId]/[groupId]/products/[productCode]/route.ts  (44 lines)
    api/access-groups/[tenantId]/[groupId]/roles/route.ts          (36 lines)
    api/access-groups/[tenantId]/[groupId]/roles/[assignmentId]/route.ts   (24 lines)
  components/access-groups/
    access-group-actions.tsx             (80 lines)
    access-group-info-card.tsx           (79 lines)
    access-group-list-table.tsx         (101 lines)
    access-group-membership-panel.tsx   (224 lines)
    access-group-members-panel.tsx      (207 lines)
    create-access-group-button.tsx      (156 lines)
    group-product-access-panel.tsx      (193 lines)
    group-role-assignment-panel.tsx     (206 lines)
```

**Total new code:** ~1,627 lines across 16 files

## Modified Files

| File | Change Summary |
|------|---------------|
| `types/control-center.ts` | +48 lines (types + interfaces) |
| `lib/api-mappers.ts` | +56 lines (4 mapper functions) |
| `lib/api-client.ts` | +1 line (cache tag) |
| `lib/control-center-api.ts` | +156 lines (`accessGroups` namespace) |
| `lib/routes.ts` | +1 line (`accessGroupDetail` route builder) |
| `lib/nav.ts` | +1 line (Groups badge) |
| `app/groups/page.tsx` | Rewritten (139 lines) |
| `app/tenant-users/[id]/page.tsx` | +15 lines (parallel fetch + panel render) |

## Gateway Proxy Path Verification

```
CC Frontend request:  /identity/api/tenants/{tenantId}/groups/...
                         │
Gateway (YARP :5010):    Route "identity-route"
  Match:                 /identity/{**catch-all}
  Transform:             PathRemovePrefix("/identity")
  Destination:           http://127.0.0.1:5001
                         │
Identity Service (:5001): /api/tenants/{tenantId}/groups/...
  Endpoint:              GroupEndpoints.cs — MapGroup("/api/tenants/{tenantId}/groups")
  Authorization:         CanReadTenant / CanMutateTenant policies
```

All paths verified consistent with LS-COR-AUT-004 `GroupEndpoints.cs` route definitions.

## Design Decisions

1. **Separate `/access-groups/` route prefix** — The new detail page uses `/access-groups/[tenantId]/[groupId]` instead of `/groups/[tenantId]/[groupId]` because Next.js App Router prohibits different dynamic segment names at the same path level (`[id]` vs `[tenantId]`). The legacy `/groups/[id]` route is preserved for backward compatibility.

2. **Organization scope deferred in UI** — The backend supports `Organization` scope type, but the create group UI only offers `Tenant` and `Product` scopes. Organization scope requires an organization picker that doesn't exist yet.

3. **Active-only filtering client-side** — Members, products, and roles are fetched in full (including removed/revoked) from the backend, then filtered to active status on the frontend. This allows future UI additions (e.g., "Show archived") without additional API calls.

4. **BFF error passthrough** — BFF routes check for `ServerApiError` instances and forward the upstream HTTP status code. Non-`ServerApiError` exceptions fall back to 500. This preserves semantic error responses (400 validation, 403 forbidden, 404 not found) for the client.

5. **Hardcoded product list** — `GroupProductAccessPanel` uses a hardcoded `KNOWN_PRODUCTS` array (`['FUND', 'CARECONNECT', 'DOCUMENTS', 'NOTIFICATIONS']`) for the grant UI. This should be replaced with a dynamic product catalog API when available.

## Known Limitations

| Limitation | Impact | Future Resolution |
|-----------|--------|-------------------|
| Organization scope not in create UI | Cannot create org-scoped groups from UI | Add organization picker component |
| Product list hardcoded | New products require code change | Replace with `/api/products` catalog endpoint |
| Mapper type casts | Invalid backend values bypass compile-time unions | Add runtime `oneOf` validation in mappers |
| No pagination on group list | May be slow for tenants with many groups | Add server-side pagination to `list()` |
| No search/filter on members panel | Manual scrolling for large groups | Add search input with debounced filtering |

## Verification

- Both Next.js apps (web :5000/:3050, control-center :5004) compile and start without errors
- No dynamic route conflicts after `/access-groups/` path separation
- All .NET backend services start successfully
- Sidebar nav updated with `LIVE` badge on Groups entry
