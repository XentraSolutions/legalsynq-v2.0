# Step 07 – Tenant Users

## Files Created

| File | Description |
|---|---|
| `apps/control-center/src/app/tenant-users/page.tsx` | Global users list — all tenants, paginated, searchable |
| `apps/control-center/src/app/tenants/[id]/users/page.tsx` | Tenant-scoped users list — filtered by tenant, with breadcrumb and sub-nav |
| `apps/control-center/src/components/users/user-list-table.tsx` | Shared table component — `showTenantColumn` prop controls global vs scoped rendering |

---

## Files Updated

| File | Change |
|---|---|
| `apps/control-center/src/types/control-center.ts` | Replaced `TenantUserSummary` with `UserSummary` and `UserDetail`; added `UserStatus` type |
| `apps/control-center/src/lib/control-center-api.ts` | Replaced live-call `users.list` stub with full mock; added 21-user `MOCK_USERS` dataset; updated import to `UserSummary` |
| `apps/control-center/src/app/tenants/[id]/page.tsx` | Replaced back-link with breadcrumb; added Overview / Users sub-nav tab row |

---

## Components Added

### `UserListTable` (Server Component)
`src/components/users/user-list-table.tsx`

**Props:**
| Prop | Default | Description |
|---|---|---|
| `users` | — | `UserSummary[]` |
| `totalCount` | — | Total matching records |
| `page` | — | Current page number |
| `pageSize` | — | Items per page |
| `showTenantColumn` | `true` | Shows/hides the Tenant column |
| `baseHref` | `'?'` | Base for pagination href construction |

**Columns:**
| Column | Notes |
|---|---|
| Name | `firstName + lastName` |
| Email | Plain text |
| Role | Plain text |
| Status | Color-coded badge: green=Active, gray=Inactive, blue=Invited |
| Tenant | Tenant code — hidden when `showTenantColumn={false}` |
| Last Login | Smart relative format: Today / Yesterday / Xd ago / date |

- Empty state: "No users found."
- Pagination footer: "Showing X–Y of Z" with Previous / Next links

### Sub-navigation tabs (inline components)
Both `/tenants/[id]/page.tsx` and `/tenants/[id]/users/page.tsx` render matching `SubNavLink` tabs so the Overview ↔ Users navigation is consistent on both pages. The active tab is styled with an indigo bottom border; the inactive tab has a hover underline on gray.

---

## API Stubs Added / Updated

### `controlCenterServerApi.users.list(params)`

**Params:**
```ts
{
  page?:     number;
  pageSize?: number;
  search?:   string;  // filters firstName, lastName, email, role
  tenantId?: string;  // if provided, scopes results to that tenant
}
```

**Returns:** `Promise<PagedResponse<UserSummary>>`
(Consistent with `tenants.list` — same `{ items, totalCount, page, pageSize }` shape.)

**Mock dataset — 21 users across 8 tenants:**

| Tenant | Users | Roles represented |
|---|---|---|
| HARTWELL | 3 | TenantAdmin, Attorney, CaseManager |
| MERIDIAN | 4 | TenantAdmin, CareCoordinator ×2, BillingManager |
| PINNACLE | 2 | TenantAdmin, Attorney |
| BLUEHAVEN | 1 | TenantAdmin |
| LEGALSYNQ | 2 | PlatformAdmin ×2 |
| THORNFIELD | 3 | TenantAdmin, Attorney, CaseManager |
| NEXUSHEALTH | 4 | TenantAdmin, CareCoordinator, BillingManager, ReadOnly |
| GRAYSTONE | 2 | TenantAdmin, ReadOnly |

All three `UserStatus` values are represented: Active, Inactive, Invited.

---

## Type Changes

### `UserStatus` (new)
```ts
export type UserStatus = 'Active' | 'Inactive' | 'Invited';
```

### `UserSummary` (new, replaces `TenantUserSummary`)
```ts
export interface UserSummary {
  id:              string;
  firstName:       string;
  lastName:        string;
  email:           string;
  role:            string;
  status:          UserStatus;
  tenantId:        string;
  tenantCode:      string;
  lastLoginAtUtc?: string;
}
```

### `UserDetail` (new, for future user detail page)
```ts
export interface UserDetail extends UserSummary {
  createdAtUtc: string;
  updatedAtUtc: string;
}
```

---

## Route Summary

| URL | Component | Entry point |
|---|---|---|
| `/tenant-users` | `TenantUsersPage` | Global users list (sidebar nav) |
| `/tenant-users?search=X` | `TenantUsersPage` | Filtered global list |
| `/tenant-users?page=2` | `TenantUsersPage` | Paginated global list |
| `/tenants/[id]/users` | `TenantScopedUsersPage` | Tenant-scoped list (via detail tab) |
| `/tenants/[id]/users?search=X` | `TenantScopedUsersPage` | Filtered tenant-scoped list |

`Routes.tenantUsers_` builder (`/tenants/${tenantId}/users`) used in both the detail page tabs and the tenant-scoped users page breadcrumb.

---

## Navigation Changes

### Sidebar
No change needed — `nav.ts` already had `/tenant-users → Tenant Users` in the "Tenants" group.

`CCSidebar` active-link logic correctly activates "All Tenants" (`/tenants`) when the user is on `/tenants/[id]/users` (since it starts with `/tenants/`).

### Tenant detail page — sub-navigation tabs
Two tabs added below the tenant header on `/tenants/[id]` and `/tenants/[id]/users`:
- **Overview** — links to `/tenants/[id]`; active (indigo underline) on the detail page
- **Users** — links to `/tenants/[id]/users`; active on the users page

The same `SubNavLink` pattern is duplicated in both page files (local component, no shared file needed).

---

## TODOs for Backend Integration

| Location | TODO |
|---|---|
| `control-center-api.ts` `users.list` | Replace mock with `serverApi.get<PagedResponse<UserSummary>>('/identity/api/admin/users' + toQs(params))` |
| `tenants/[id]/users/page.tsx` header | Add Invite User button action once BFF proxy is in place |
| `tenant-users/page.tsx` header | Same — Invite User is disabled/placeholder |

---

## TypeScript

Zero errors confirmed (`tsc --noEmit` clean across all files in `apps/control-center`).

---

## Assumptions

1. **Pagination shape** — The spec described `{ data, total }` but the existing codebase uses `PagedResponse<T>` (`{ items, totalCount, page, pageSize }`) for tenant list. Using the same shape for users keeps the API layer consistent and avoids a second response contract. The live backend endpoint will be mapped accordingly when integrated.

2. **`tenantCode` in `UserSummary`** — Stored in the mock for display purposes on the global list. The live API may return it directly or require a join from the Identity service; the shape is correct either way.

3. **`lastLoginAtUtc` optional** — Invited users (who haven't logged in yet) have no `lastLoginAtUtc`. The table displays `—` in that case.

4. **`void (serverApi as unknown)` pattern** — `serverApi` is imported in `control-center-api.ts` for future live-endpoint wiring. The `void` suppressor prevents TypeScript `noUnusedLocals` errors while keeping the import in place as a clear implementation guide. This resolves cleanly under the project's current `tsconfig.json`.
