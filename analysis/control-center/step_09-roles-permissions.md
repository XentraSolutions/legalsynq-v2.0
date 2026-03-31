# Step 09 – Roles & Permissions (RBAC)

## Files Created

| File | Description |
|---|---|
| `apps/control-center/src/app/roles/page.tsx` | `/roles` list page — Server Component |
| `apps/control-center/src/app/roles/[id]/page.tsx` | `/roles/[id]` detail page — Server Component |
| `apps/control-center/src/components/roles/role-list-table.tsx` | `RoleListTable` — table with name link, description, permission count badge, user count |
| `apps/control-center/src/components/roles/role-detail-card.tsx` | `RoleDetailCard` — Stats row, Role Information card, grouped Permissions card |

---

## Files Updated

| File | Change |
|---|---|
| `apps/control-center/src/types/control-center.ts` | Replaced existing `RoleSummary` stub; added `Permission`, new `RoleSummary`, `RoleDetail` |
| `apps/control-center/src/lib/routes.ts` | Added `Routes.roleDetail(id)` → `/roles/:id` |
| `apps/control-center/src/lib/control-center-api.ts` | Added `MOCK_PERMISSIONS`, `PERM_MAP`, `MOCK_ROLES`, `ROLE_TIMESTAMPS`, `resolvePermissions()`, `buildRoleDetail()`, `roles.list()`, `roles.getById()` |

---

## Type Changes

### `Permission` (new)
```ts
export interface Permission {
  id:          string;
  key:         string;   // e.g. "tenants.activate"
  description: string;
}
```

### `RoleSummary` (replaced)
```ts
export interface RoleSummary {
  id:          string;
  name:        string;
  description: string;
  userCount:   number;
  permissions: string[];   // permission keys (additive list)
}
```
Previous stub had `code`, `productCode`, `capabilities[]`, `isActive` — those fields have been removed and replaced by the RBAC-correct shape.

### `RoleDetail` (new)
```ts
export interface RoleDetail extends RoleSummary {
  createdAtUtc:        string;
  updatedAtUtc:        string;
  resolvedPermissions: Permission[];   // fully-hydrated from keys
}
```
`resolvedPermissions` is populated by `resolvePermissions(keys)` at the mock layer so components never need to do a second lookup.

---

## Mock Dataset

### 18 Permissions across 7 groups

| Group | Keys |
|---|---|
| platform | `platform.view`, `platform.settings.read`, `platform.settings.write` |
| tenants | `tenants.read`, `tenants.create`, `tenants.update`, `tenants.activate`, `tenants.suspend` |
| users | `users.read`, `users.create`, `users.update`, `users.lock`, `users.reset-password` |
| roles | `roles.read`, `roles.write` |
| audit | `audit.read` |
| monitoring | `monitoring.read` |
| support | `support.tools` |

### 5 Roles

| Role | Users | Permissions | Notes |
|---|---|---|---|
| SuperAdmin | 0 | 18 (all) | Reserved for platform engineering |
| PlatformAdmin | 2 | 13 | Full operational access |
| SupportAdmin | 1 | 7 | Read + limited remediation actions |
| OperationsAdmin | 0 | 8 | Tenant lifecycle + monitoring |
| ReadOnly | 2 | 6 | View-only across all sections |

---

## API Stubs Added

### `controlCenterServerApi.roles.list()`
```ts
// TODO: replace with GET /identity/api/admin/roles
list: (): Promise<RoleSummary[]> => {
  return Promise.resolve(MOCK_ROLES);
},
```
Returns all 5 roles. No pagination — roles are a fixed system-defined list.

### `controlCenterServerApi.roles.getById(id)`
```ts
// TODO: replace with GET /identity/api/admin/roles/{id}
getById: (id: string): Promise<RoleDetail | null> => {
  const summary = MOCK_ROLES.find(r => r.id === id);
  if (!summary) return Promise.resolve(null);
  return Promise.resolve(buildRoleDetail(summary));
},
```

### `buildRoleDetail(summary)` helper
- Looks up `ROLE_TIMESTAMPS[summary.id]` for `createdAtUtc` / `updatedAtUtc`
- Calls `resolvePermissions(summary.permissions)` to hydrate `resolvedPermissions: Permission[]`

### `resolvePermissions(keys)` helper
- Builds a `Map<key, Permission>` from `MOCK_PERMISSIONS` at module load
- Maps each key to its full `Permission` object; unknown keys are silently dropped (won't happen with the current clean dataset)

---

## Routes

```ts
Routes.roles                    // '/roles'          — already existed
Routes.roleDetail('role-super') // '/roles/role-super' — new builder
```

---

## Page: `/roles`

**Layout:**
```
CCShell
└─ space-y-4
   ├─ Header row: "Roles & Permissions" + subtitle
   ├─ Info banner (indigo) — "system-defined, read-only"
   ├─ Error banner (red, conditional)
   └─ RoleListTable
```

No search or pagination — 5 system-defined roles fit on one screen without either.
No "Create Role" button — roles are system-defined and not user-creatable.

---

## Page: `/roles/[id]`

**Layout:**
```
CCShell
└─ space-y-5
   ├─ Breadcrumb: Roles & Permissions › {role.name}
   ├─ [Error banner] if fetch failed
   ├─ [Not found card] if role null (ID, back link)
   └─ [Detail content] if role found
      ├─ Header: role name (h1) + description (p) + "System-defined" badge
      └─ RoleDetailCard
         ├─ Stats row: Permissions count | Assigned Users | Permission Groups
         ├─ Role Information card (Name, Description, System Role, Created, Updated)
         └─ Permissions card (grouped by prefix, monospace key chip + description)
```

---

## Components

### `RoleListTable` (Server Component)
`src/components/roles/role-list-table.tsx`

| Column | Notes |
|---|---|
| Role | Link → `Routes.roleDetail(id)`, bold |
| Description | Gray, `max-w-xs` truncation |
| Permissions | Indigo count badge (`N permissions`) |
| Users | `N user(s)` or `—` |
| (actions) | `View →` link |

Empty state: "No roles defined." full-width card.

---

### `RoleDetailCard` (Server Component)
`src/components/roles/role-detail-card.tsx`

**Section A — Stats row**
Three stat cards (white border): Permissions, Assigned Users, Permission Groups.

**Section B — Role Information**
`dl`-based info rows using the same `InfoRow(label, value)` sub-component pattern from UserDetailCard.

| Field | Value |
|---|---|
| Name | Plain text |
| Description | Plain text |
| System Role | Gray badge: "System-defined" |
| Created | Long date (`January 1, 2024`) |
| Last Updated | Long date |

**Section C — Permissions**
Card with permission count in the header bar. Permissions are grouped by their dot-prefix key.

Group order: `platform → tenants → users → roles → audit → monitoring → support → (other)`.

Each permission renders as:
```
[monospace chip: tenants.activate]  Activate or deactivate tenant accounts
```
The monospace chip is `bg-indigo-50 text-indigo-700 border-indigo-100` to visually distinguish permission keys from prose.

**Internal helpers:**
- `groupPermissions(permissions)` — deterministic group ordering via `ORDER` array
- `GROUP_LABELS` map — humanizes group prefixes (`tenants` → `Tenants`, etc.)
- `StatCard`, `InfoRow`, `PermissionGroup` — local sub-components

---

## TODOs for Backend Integration

| Location | TODO |
|---|---|
| `control-center-api.ts` `roles.list` | Replace with `serverApi.get<RoleSummary[]>('/identity/api/admin/roles')` |
| `control-center-api.ts` `roles.getById` | Replace with `serverApi.get<RoleDetail>(\`/identity/api/admin/roles/${id}\`)` |
| Identity.Api | Implement `GET /identity/api/admin/roles` → returns `RoleSummary[]` |
| Identity.Api | Implement `GET /identity/api/admin/roles/{id}` → returns `RoleDetail` |

---

## TypeScript

Zero errors confirmed (`tsc --noEmit` clean across all `apps/control-center` files).

---

## Design Decisions

1. **Permissions as `string[]` keys on `RoleSummary`, resolved to `Permission[]` on `RoleDetail`**  
   The list view only needs counts; the detail view needs full descriptions. Keeping raw keys on the summary avoids shipping 18-object arrays per row over the wire. The API layer resolves them for the detail view.

2. **No pagination on `roles.list()`**  
   Roles are system-defined and fixed in count. Even if 10–15 roles are eventually added, a single-page table remains more ergonomic than a paginated one for this use case.

3. **`resolvedPermissions` on `RoleDetail` (not fetched separately)**  
   Mirrors the `tenantDisplayName` pattern from Step 08 — the API layer joins data so page components stay simple and free of secondary fetches.

4. **Groups ordered deterministically**  
   `groupPermissions()` uses a fixed `ORDER` array rather than insertion order so the permissions table is always predictably laid out regardless of how keys are ordered in `MOCK_ROLES`.

5. **No edit/create actions**  
   Roles are system-defined in the current scope. An "Edit Role" form would be a future step requiring backend support for `PUT /identity/api/admin/roles/{id}`.
