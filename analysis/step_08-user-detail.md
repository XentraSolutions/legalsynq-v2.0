# Step 08 – User Detail Page

## Files Created

| File | Description |
|---|---|
| `apps/control-center/src/app/tenant-users/[id]/page.tsx` | Server Component detail page — breadcrumb, header badges, action buttons, detail card |
| `apps/control-center/src/components/users/user-detail-card.tsx` | Server Component — User Information card, Account Status card, Activity placeholder |
| `apps/control-center/src/components/users/user-actions.tsx` | Client Component — Activate/Deactivate, Lock/Unlock, Reset Password, Resend Invite |

---

## Files Updated

| File | Change |
|---|---|
| `apps/control-center/src/types/control-center.ts` | `UserDetail` extended with `tenantDisplayName`, `isLocked?`, `inviteSentAtUtc?` |
| `apps/control-center/src/lib/routes.ts` | Added `Routes.userDetail(id)` → `/tenant-users/:id` |
| `apps/control-center/src/lib/control-center-api.ts` | Added `USER_DETAIL_EXTRAS`, `buildUserDetail()`, `users.getById()` |
| `apps/control-center/src/components/users/user-list-table.tsx` | Name column now a link to `Routes.userDetail(user.id)` |

---

## Components Added

### `UserDetailCard` (Server Component)
`src/components/users/user-detail-card.tsx`

**Section A — User Information**
| Field | Notes |
|---|---|
| First Name | Plain text |
| Last Name | Plain text |
| Email | `mailto:` link, indigo styled |
| Role | Indigo pill badge |
| Status | Color-coded pill: green/gray/blue |
| Tenant | Link to `/tenants/:tenantId` showing displayName + monospace code chip |
| Created | Long date format |
| Last Updated | Long date format |
| Last Login | Formatted datetime or "Never" (italic gray) |

**Section B — Account Status**
| Field | Notes |
|---|---|
| Account State | Status pill |
| Locked | Green dot "Unlocked" / Red dot "Locked" indicator |
| Invite State | "Pending acceptance" (blue) or "—" |
| Invite Sent | Only rendered if `inviteSentAtUtc` is present |
| Last Login | Datetime or "Never" |

**Section C — Recent Activity**
Placeholder with dimmed text: "Activity log coming soon". Includes `// TODO` comment pointing to `GET /identity/api/admin/users/{id}/activity`.

**Internal sub-components:**
- `InfoRow` — label + value row with consistent `w-36` label column
- `StatusPill` — Active=green / Inactive=gray / Invited=blue
- `RolePill` — indigo badge
- `LockedIndicator` — colored dot + text

---

### `UserActions` (Client Component)
`src/components/users/user-actions.tsx`

**Button groups:**

| Group | Buttons | Behavior |
|---|---|---|
| Account state | Activate, Deactivate | Activate disabled if Active or Invited; Deactivate disabled if Inactive |
| Divider | — | Visual `1px` separator |
| Security | Lock/Unlock (toggled by `isLocked`), Reset Password | Lock disabled if Invited; Reset Password disabled if Invited |
| Invite | Resend Invite | Only rendered when `status === 'Invited'`, separated by divider |

Each `onClick` contains `// TODO` pointing to the exact BFF proxy endpoint.

**Button variants:** `primary` (indigo fill), `success` (green fill), `neutral` (white border), `danger` (red text).

---

## Page Structure — `/tenant-users/[id]`

```
CCShell
└─ space-y-5
   ├─ Breadcrumb: Tenant Users › Full Name
   ├─ [Error banner] if fetch failed
   ├─ [Not found card] if user null
   └─ [Detail content] if user found
      ├─ Header row (flex justify-between)
      │   ├─ Left: Full name (h1) + email (p) + badge row
      │   │         [Status badge] [Role badge] [Tenant badge link] [Locked badge?]
      │   └─ Right: UserActions (Client Component)
      └─ UserDetailCard (Server Component)
         ├─ User Information card
         ├─ Account Status card
         └─ Recent Activity placeholder
```

---

## API Stubs Added

### `controlCenterServerApi.users.getById(id)`

```ts
// TODO: replace with GET /identity/api/admin/users/{id}
getById: (id: string): Promise<UserDetail | null> => {
  const summary = MOCK_USERS.find(u => u.id === id);
  if (!summary) return Promise.resolve(null);
  return Promise.resolve(buildUserDetail(summary));
},
```

### `buildUserDetail(summary)` helper
- Looks up `USER_DETAIL_EXTRAS[summary.id]` for `createdAtUtc`, `updatedAtUtc`, `isLocked`, `inviteSentAtUtc`
- Looks up `MOCK_TENANTS` by `summary.tenantId` to resolve `tenantDisplayName`
- Falls back gracefully for any missing data

### `USER_DETAIL_EXTRAS` map
21 entries keyed by user ID (`u-001`–`u-021`). Notable values:
- `u-003`, `u-015`, `u-019` — Invited users with `inviteSentAtUtc`
- `u-007`, `u-010`, `u-020` — Inactive/suspended users with `isLocked: true`

---

## Type Changes

### `UserDetail` (updated)
```ts
export interface UserDetail extends UserSummary {
  tenantDisplayName: string;   // resolved from tenantId — avoids a second API call
  createdAtUtc:      string;
  updatedAtUtc:      string;
  isLocked?:         boolean;
  inviteSentAtUtc?:  string;
}
```

`tenantDisplayName` is embedded directly so the detail page can render the tenant link without an extra `getById` tenant call.

---

## Routes Updated

```ts
Routes.userDetail(id)  // '/tenant-users/:id'   ← new
```

`user-list-table.tsx` now wraps each user's name in `<Link href={Routes.userDetail(user.id)}>`, making the full list clickable.

---

## TODOs for Backend Integration

| Location | TODO |
|---|---|
| `control-center-api.ts` `users.getById` | Replace with `serverApi.get<UserDetail>('/identity/api/admin/users/${id}')` |
| `user-actions.tsx` Activate | `POST /api/identity/api/admin/users/{userId}/activate` + `router.refresh()` |
| `user-actions.tsx` Deactivate | `POST /api/identity/api/admin/users/{userId}/deactivate` + `router.refresh()` |
| `user-actions.tsx` Lock | `POST /api/identity/api/admin/users/{userId}/lock` + `router.refresh()` |
| `user-actions.tsx` Unlock | `POST /api/identity/api/admin/users/{userId}/unlock` + `router.refresh()` |
| `user-actions.tsx` Reset Password | `POST /api/identity/api/admin/users/{userId}/reset-password` |
| `user-actions.tsx` Resend Invite | `POST /api/identity/api/admin/users/{userId}/resend-invite` |
| `user-detail-card.tsx` Activity section | `GET /identity/api/admin/users/{id}/activity` → render `AuditLogEntry[]` |

---

## TypeScript

Zero errors confirmed (`tsc --noEmit` clean across all files in `apps/control-center`).

---

## Assumptions

1. **`tenantDisplayName` on `UserDetail`** — Embedded at the API layer so the detail page doesn't need a separate tenant fetch. When the live backend is used, the Identity service may include `tenantName` directly in the user response, or the BFF can join it. Either way, only `control-center-api.ts` changes.

2. **Locked users** — Three mock users have `isLocked: true` (u-007 Amara Diallo/Inactive, u-010 Tanya Bridges/Inactive, u-020 Patricia Langford/Inactive). All belong to inactive or suspended tenants, which is a realistic scenario.

3. **Action button alert placeholders** — All action buttons call `alert()` as a temporary UX indicator. These are replaced with `fetch` + `router.refresh()` once BFF routes exist.

4. **No tab navigation on detail page** — The user detail page does not have Overview/Users sub-nav tabs (unlike the tenant detail page). A user is a leaf entity — there are no sub-pages planned at this scope.
