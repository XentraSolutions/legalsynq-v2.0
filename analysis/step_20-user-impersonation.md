# Step 20 – User Impersonation

## Status: Complete — 0 TypeScript errors

---

## Files Created

| File | Purpose |
|------|---------|
| `src/app/actions/impersonation.ts` | Server Actions: `startImpersonationAction`, `stopImpersonationAction` |
| `src/components/layout/impersonation-banner.tsx` | Rose/red persistent banner shown while impersonating |

---

## Files Updated

| File | What changed |
|------|-------------|
| `src/types/control-center.ts` | Added `UserImpersonationSession` interface |
| `src/lib/app-config.ts` | Added `IMPERSONATION_COOKIE_NAME = 'cc_impersonation'` constant |
| `src/lib/auth.ts` | Added `getImpersonation()`, `setImpersonation()`, `clearImpersonation()` |
| `src/components/shell/cc-shell.tsx` | Imports + renders `ImpersonationBanner` above `TenantContextBanner` |
| `src/app/tenant-users/[id]/page.tsx` | "Impersonate User" button; disabled for non-Active users |

---

## Cookie Strategy

| Cookie | Name | Value | httpOnly | Priority |
|--------|------|-------|----------|----------|
| Session | `platform_session` | JWT (auth token) | true | Auth gate |
| Tenant Context | `cc_tenant_context` | JSON `TenantContext` | false | Display scoping |
| User Impersonation | `cc_impersonation` | JSON `UserImpersonationSession` | false | Elevated state |

### `cc_impersonation` cookie

- **Lifecycle**: set by `startImpersonationAction`, cleared by `stopImpersonationAction`
- **Encoding**: `JSON.stringify(UserImpersonationSession)`
- **httpOnly**: `false` — not an auth credential; client JS may read for optimistic UI
- **sameSite**: `lax`
- **secure**: `true` in production
- **path**: `/` — available across all routes
- **maxAge**: none — expires with browser session

`getImpersonation()` validates all required fields after JSON parse:
```ts
'adminId' | 'impersonatedUserId' | 'impersonatedUserEmail' | 'tenantId' | 'startedAtUtc'
```
Returns `null` if any field is absent or not a `string`, preventing malformed data from propagating.

---

## `UserImpersonationSession` Type

```ts
export interface UserImpersonationSession {
  adminId:               string;  // PlatformAdmin userId
  impersonatedUserId:    string;  // target user id
  impersonatedUserEmail: string;  // for banner display
  tenantId:              string;  // tenant the user belongs to
  tenantName:            string;  // tenant display name (extra: banner convenience)
  startedAtUtc:          string;  // ISO 8601 timestamp
}
```

`tenantName` is an addition beyond the minimal spec but is always available at impersonation
start time (from `user.tenantDisplayName`) and avoids a lookup on every page render.

---

## Server Actions

### `startImpersonationAction(user)`

```ts
startImpersonationAction.bind(null, {
  id:         user.id,
  email:      user.email,
  tenantId:   user.tenantId,
  tenantName: user.tenantDisplayName,
})
```

1. Calls `getSession()` — redirects to login if absent.
2. Builds a `UserImpersonationSession` with current UTC timestamp.
3. Calls `setImpersonation(session)` to write the cookie.
4. Redirects to `/` so the global shell immediately renders the banner.

Does **not** clear `cc_tenant_context` — tenant context is preserved throughout.

### `stopImpersonationAction()`

1. Calls `clearImpersonation()` to delete `cc_impersonation`.
2. Does **not** touch `cc_tenant_context`.
3. Redirects to `/tenant-users` to land the admin back in the user list.

---

## UI Behavior

### Tenant Users — detail page (`/tenant-users/[id]`)

New button in the action area (top-right of page header):

```
⚡ Impersonate User
```

- **Enabled**: `user.status === 'Active'`
- **Disabled**: all other statuses (Inactive, Invited) with tooltip "Only Active users can be impersonated"
- Rose/red color palette (text-rose-700, bg-rose-50, border-rose-300) distinct from all other buttons
- Calls `startImpersonationAction` via Server Action form — no JavaScript required

### Impersonation Banner (`ImpersonationBanner`)

Full-width rose-600 strip rendered above the amber `TenantContextBanner`:

```
⚡  Impersonating: margaret@hartwell.law · Hartwell & Associates    Started 14:23  [✕ Exit Impersonation]
```

- **Left**: ⚡ icon + "Impersonating: [email]" + "· [tenantName]"
- **Right**: "Started HH:MM" (from `startedAtUtc`) + "Exit Impersonation" button
- "Exit Impersonation" triggers `stopImpersonationAction` via form submit
- `role="alert"` + `aria-live="polite"` for accessibility

---

## Interaction Rules

| Scenario | Behavior |
|----------|---------|
| Start impersonation (no prior context) | Rose banner appears. Tenant context remains absent. |
| Start impersonation (with tenant context) | Both banners visible. Rose above amber. Context cookie untouched. |
| Exit impersonation (with tenant context) | Rose banner disappears. Amber banner remains. Context cookie untouched. |
| Exit impersonation (no prior context) | Rose banner disappears. No amber banner. |
| Both banners visible | Impersonation banner is displayed **above** tenant context banner — signals higher priority elevated state. |
| User status non-Active | "Impersonate User" button rendered but disabled with tooltip. |
| Cookie tampered/malformed | `getImpersonation()` returns `null` silently — no banner shown. |

---

## TODOs for Backend Integration

Added to all affected files:

```ts
// TODO: integrate with Identity service impersonation endpoint
// TODO: issue temporary impersonation token
// TODO: audit log impersonation events
```

Locations:
- `src/types/control-center.ts` — on `UserImpersonationSession`
- `src/lib/app-config.ts` — on `IMPERSONATION_COOKIE_NAME`
- `src/lib/auth.ts` — on `setImpersonation()` and `clearImpersonation()`
- `src/app/actions/impersonation.ts` — inline in both actions

---

## Independence Validation

- Zero imports from `apps/web`
- All helpers: `getSession()`, `setImpersonation()`, `clearImpersonation()` from `@/lib/auth` (local)
- All types: `UserImpersonationSession` from `@/types/control-center` (local)
- No new npm dependencies
- Cookie name constant from `@/lib/app-config` (local)

---

## Any Issues or Assumptions

1. **`tenantName` added to `UserImpersonationSession`** — The spec listed five fields; `tenantName`
   was added because the banner must display the tenant's name and it is always available at the
   point `startImpersonationAction` is called. Storing it avoids a DB lookup on every shell render.

2. **Impersonation is purely UI-side at this stage** — Writing the `cc_impersonation` cookie does
   NOT grant any access beyond what the `platform_session` already grants. The Identity service
   integration (temporary token issuance) is deferred per the TODO markers.

3. **Redirect target on stop** — redirects to `/tenant-users` (not `/`) so the admin lands in a
   meaningful page rather than the root, which would redirect to `/tenants`.

4. **Disabled vs hidden** — The "Impersonate User" button is disabled (not hidden) for non-Active
   users, so admins can see the feature exists but understand why it is unavailable.

5. **`getImpersonation()` in CCShell is synchronous** — Same pattern as `getTenantContext()`.
   Both call `cookies()` synchronously and do a synchronous JSON parse. No await needed.
