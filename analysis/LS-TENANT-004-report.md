# LS-TENANT-004 — Access & Explainability Report

**Generated**: 2026-04-13
**Status**: COMPLETE
**Build**: Clean (0 TypeScript errors)

---

## 1. Access Overview (Tab 1)

### Route
`/tenant/authorization/access` → Overview tab

### Summary Widgets (6 cards)
- **Total Users** — count of all tenant users
- **Active Groups** — count of groups with Active status
- **Direct Access** — users without group membership (approximation)
- **Group Access** — users with at least one active group membership
- **Permissions** — total permission count from permission catalog
- **Roles** — total role count

### Access Distribution
- **Users by Product** — horizontal bar chart showing product code → user count (derived from group product assignments × member count)
- **Users by Role** — badge grid showing role name → count (from admin user list role field)
- **Top Groups by Membership** — top 5 groups by active member count, linked to group detail

### Data Sources
- `GET /identity/api/admin/users?pageSize=500` — admin user list with role, status, groupCount
- `GET /identity/api/tenants/{tenantId}/groups` — all groups
- `GET /identity/api/admin/permissions` — permission catalog
- `GET /identity/api/admin/roles` — role list
- `GET /identity/api/tenants/{tenantId}/groups/{groupId}/members|products|roles` — per-group membership, products, roles (batch-fetched on page load for active groups)

---

## 2. User Access Explorer (Tab 2)

### Purpose
Explore user-level access deeply across the system

### Table Columns
- User (name + email + avatar)
- Role (primary role)
- Status (Active/Invited/Inactive badge)
- Groups (group count)
- Expand arrow

### Features
- **Search**: by name or email
- **Product Filter**: dropdown to filter users by product access (via group product membership)
- **Lazy Access Loading**: clicking a user row fetches `GET /admin/users/{id}/access-debug` on demand, caches result
- **Expandable Detail**: shows full access breakdown within the row

### Expanded Detail Shows
- **Effective Products**: product codes with source badges (Direct/Group/Tenant) and group attribution
- **Effective Roles**: role codes with source badges and group names
- **Access Paths**: full chain visualization: `User → Group → Role → Permission` with clickable links to user/group detail pages
- **Policy Impact**: policy restrictions with deny indicators (if any policies exist)
- **Group Membership**: clickable group badges linking to group detail
- **Full Detail Link**: link to user detail page (LS-TENANT-002)

### Performance
- No N+1 problem: access-debug fetched lazily per user on expand, cached in component state
- User list capped at 50 displayed rows with search guidance for large sets

---

## 3. Permission-Centric View (Tab 3)

### Purpose
Answer: "Who has this permission?"

### Permission List
- Searchable, filterable list from permission catalog
- Each row shows: permission code, permission name, product code, category
- Click to expand drilldown

### Drilldown Shows
- **Users with Permission**: populated from cached access-debug data (users whose `permissionSources` include this permission code). Shows user name, source badge (Direct/Group), group name
- **Groups Granting Permission**: groups that have active role assignments matching roles known to grant this specific permission (filtered by `rolesGrantingPerm` derived from cached access-debug data). Shows group name + role code, linked to group detail page

### Data Strategy
- Permission catalog: `GET /identity/api/admin/permissions` (server-side prefetch)
- User permission data: derived from `userAccessCache` (populated when users are expanded in User Explorer tab)
- Group role data: prefetched per-group `getGroupRoles()` results

---

## 4. Global Access Search (Tab 4)

### Purpose
Single search across all access entities

### Search Results Grouped By
- **Users**: name + email + role, linked to user detail
- **Permissions**: code + name + product, click navigates to Permissions tab with filter
- **Roles**: role name display
- **Groups**: group name + status badge, linked to group detail

### UX
- Auto-focused search input
- Empty state with search prompt
- No-results state
- Click-through to relevant detail pages

---

## 5. Cross-Entity Linking

### Navigation Paths Implemented
- **User → Groups**: user explorer shows group badges linking to group detail
- **User → Roles → Permissions**: access paths show full chain
- **Group → Users**: group detail (LS-TENANT-003) shows member list
- **Group → Roles → Permissions**: group detail shows effective access preview
- **Permission → Users**: permission drilldown shows users with the permission
- **Permission → Groups**: permission drilldown shows groups with roles
- **Search → Any Entity**: global search links to user detail, group detail, or permission filter

---

## 6. Access Path Visualization

### Implementation
Visual chain rendering: `User → Group → Role → Permission`

Each step shows:
- Entity name (clickable link where applicable)
- Source badge (Direct/Group/Role)
- Arrow connector between steps

### Example
```
John Doe [Direct] → Finance Team [Group] → FUND_APPROVER [Role] → SYNQ_FUND.application:approve [Group]
```

---

## 7. Visual Standards

### Source Badges
- **Direct** → Blue badge (`bg-blue-50 text-blue-700`)
- **Group** → Purple badge (`bg-purple-50 text-purple-700`)
- **Tenant** → Amber badge (`bg-amber-50 text-amber-700`)
- **Role** → Gray badge (`bg-gray-100 text-gray-600`)

### Status Badges
- **Active** → Green
- **Invited** → Blue
- **Inactive** → Gray

### Policy Impact
- Red background with deny indicators

---

## 8. API Integration

### Server-side Methods Added to `tenantServerApi`
| Method | Endpoint |
|--------|----------|
| `getAdminUsers(page, pageSize)` | `GET /identity/api/admin/users?page=X&pageSize=Y` |
| `getPermissions()` | `GET /identity/api/admin/permissions` |
| `getRolePermissions(roleId)` | `GET /identity/api/admin/roles/{id}/permissions` |

### Client-side Method Added to `tenantClientApi`
| Method | Endpoint |
|--------|----------|
| `getUserAccessDebug(userId)` | `GET /identity/api/admin/users/{id}/access-debug` |

---

## 9. Types Added

### `apps/web/src/types/tenant.ts`
- `PermissionItem` — `{ id, code, name, description?, category?, productCode, productName, isActive, ... }` (from `ListPermissions`)
- `AdminUserItem` — `{ id, firstName, lastName, email, role, status, primaryOrg?, groupCount, tenantId, tenantCode, createdAtUtc }` (from `ListUsers` admin endpoint)
- `AdminUsersResponse` — `{ items: AdminUserItem[], totalCount, page, pageSize }`

---

## 10. Security

- All routes protected by `requireTenantAdmin()` via layout guard
- Server-side API calls use `serverApi` (JWT-forwarded from HttpOnly cookie)
- Client-side access-debug calls use `apiClient` (credentials: include via BFF proxy)
- Tenant boundary enforced by backend for all endpoints
- No cross-tenant access paths introduced

---

## 11. Performance

- **No N+1**: access-debug calls are lazy (per-user on expand), not batch
- **Client-side caching**: expanded user access data cached in component state for session
- **Result limiting**: user list capped at 50 rows, permissions capped at 50 rows with search guidance
- **Server batching**: group member/product/role data prefetched in parallel via `Promise.allSettled`

---

## 12. Build Status

```
TypeScript: 0 errors
Next.js: compiles successfully
No regressions to existing routes
LS-TENANT-001 navigation and guards intact
LS-TENANT-002 user management intact
LS-TENANT-003 group management intact
```

---

## 13. Known Limitations

1. **Permission-to-user mapping**: requires users to be expanded in User Explorer tab first (access-debug data is lazy-loaded and cached). Without cached data, permission drilldown shows "No cached user data" message
2. **Product-user count**: approximated from group membership × group product assignments; does not include direct user-product assignments
3. **No dedicated aggregation endpoint**: overview statistics are derived from existing list endpoints; a `GET /tenant/{id}/access-summary` endpoint would improve accuracy
4. **Large tenant scale**: user list capped at 500 from admin API; very large tenants may need pagination in the UI
5. **Group-role-permission linkage**: groups show role codes but cannot resolve which specific permissions those roles grant without a per-role permission fetch

---

## 14. Files Created/Modified

### New Files
| File | Purpose |
|------|---------|
| `apps/web/src/app/(platform)/tenant/authorization/access/AccessExplainabilityClient.tsx` | Main client component — 4-tab access dashboard |

### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/tenant/authorization/access/page.tsx` | Replaced placeholder with functional server component |
| `apps/web/src/types/tenant.ts` | Added `PermissionItem`, `AdminUserItem`, `AdminUsersResponse` |
| `apps/web/src/lib/tenant-api.ts` | Added `getAdminUsers`, `getPermissions`, `getRolePermissions` (server), `getUserAccessDebug` (client) |

---

## Ready For

- **LS-TENANT-005**: Access Simulator (simulate access for user/group combinations)
- **Future**: Dedicated aggregation endpoints for large-scale tenants
- **Future**: Permission policy evaluation visualization
