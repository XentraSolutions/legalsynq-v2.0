# UIX-002-01 Delivery Report
## Tenant User Management — Hardening Pass

**Scope:** Focused fixes only. No structural rewrites.
**Date:** 2026-04-01
**Commit baseline:** prior to `62b0578`

---

## What Already Existed (Unchanged)

The following were confirmed complete and correct before this pass began. No modifications were made.

| Area | Status |
|---|---|
| `POST /api/admin/users/{id}/deactivate` — deactivate endpoint | Already correct |
| `POST /api/admin/users/{id}/activate` — activate endpoint | Already correct |
| `UserInvitation` domain model — `Accept()`, `Revoke()`, `IsExpired()`, `Statuses` | Already correct |
| `UserInvitation.User` navigation property + EF configuration | Already correct |
| `User.Activate()` / `User.Deactivate()` domain methods | Already correct |
| SHA-256 token hashing in `InviteUser` endpoint | Already correct |
| TenantAdmin scoping badge UI on Tenant Users list header | Already correct |
| Gateway JWT forwarding to Identity service (`ClaimsPrincipal` populated) | Already correct |

---

## Fixes Applied in This Pass

### Fix 1 — (Confirmed pre-existing, no action taken)
Deactivate endpoint already enforced correctly. Verified and left unchanged.

---

### Fix 2 — `ListUsers` Tenant Scoping + Status Filter
**File:** `Identity.Api/Endpoints/AdminEndpoints.cs`

**Problem:** `ListUsers` accepted a `tenantId` query param from any caller, meaning a TenantAdmin could request users from any tenant by passing an arbitrary UUID.

**What changed:**
- Injected `ClaimsPrincipal caller` into the `ListUsers` handler.
- TenantAdmin callers: `tenantId` param is ignored; filter is always forced to `caller.FindFirstValue("tenant_id")`.
- PlatformAdmin callers: `tenantId` param is respected as before (optional).
- Added `status` query param (`active` / `inactive` / `invited`) that maps to an EF `Where` clause against `IsActive` and the `UserInvitations` join.

**Not changed:** response shape, pagination logic, search filter, existing auth middleware.

---

### Fix 3 — `RemoveMembership` Safety Guards
**File:** `Identity.Api/Endpoints/AdminEndpoints.cs`

**Problem:** An admin could remove a user's last active membership (leaving them in a broken state) or remove the primary membership while secondary ones remained active.

**What changed:**
- Before performing the remove: count the user's active memberships.
- If count == 1: return `409 LAST_MEMBERSHIP` — cannot remove the sole active membership.
- If the targeted membership is the primary and others exist: return `409 PRIMARY_MEMBERSHIP` — must reassign primary first.
- Both codes are machine-readable and surfaced to the frontend for specific error handling.

**Not changed:** the removal logic itself, audit event, cache revalidation.

---

### Fix 4 — `ListGroups` / `CreateGroup` Tenant Boundary Enforcement
**File:** `Identity.Api/Endpoints/AdminEndpoints.cs`

**Problem:** Both endpoints accepted a `tenantId` param without verifying the caller was entitled to that tenant. A TenantAdmin could read groups from or create groups in another tenant.

**What changed — `ListGroups`:**
- Injected `ClaimsPrincipal caller`.
- TenantAdmin: query always filtered to caller's own tenant; passed `tenantId` param ignored.
- PlatformAdmin: optional `tenantId` param respected as before.

**What changed — `CreateGroup`:**
- Injected `ClaimsPrincipal caller`.
- If TenantAdmin and `body.TenantId` does not match the caller's `tenant_id` claim: return `403 Forbidden`.
- PlatformAdmin: no restriction.

**Not changed:** name uniqueness check, group creation logic, response shape.

---

### Fix 5 — `POST /api/auth/accept-invite` (New Endpoint)
**Files:** `Identity.Api/Endpoints/AuthEndpoints.cs`, `Identity.Domain/User.cs`

**Problem:** No endpoint existed to redeem an invitation token and activate an invited user account. Invited users had no path to set a password and log in.

**What was added:**

**`AuthEndpoints.cs`** — new anonymous endpoint:
1. Validates `token` (required) and `newPassword` (required, ≥ 8 chars).
2. SHA-256 hashes the raw token to match the stored `TokenHash`.
3. Looks up `UserInvitation` by `TokenHash`, including the `User` navigation.
4. Rejects if: invitation not found, status is not `PENDING`, or `IsExpired()` returns true. Each case returns a distinct, user-facing error message.
5. Calls `user.SetPassword(hash)` and `user.Activate()`.
6. Calls `invitation.Accept()`.
7. Saves changes.
8. Emits `identity.user.invite_accepted` audit event (fire-and-observe, non-blocking).
9. Returns `200 { message }`.

**`Identity.Domain/User.cs`** — `SetPassword(string passwordHash)` method added:
- `PasswordHash` had a private setter; no public mutator existed.
- Method validates non-empty, sets `PasswordHash`, updates `UpdatedAtUtc`.

**`AcceptInviteRequest`** record added as private nested type in `AuthEndpoints`.

**Not changed:** `UserInvitation.Accept()`, `User.Activate()`, token generation in `InviteUser`, existing auth endpoints.

---

### Fix 6 — Invite Form Auto-Resolves Tenant
**Files:** `apps/control-center/src/app/tenant-users/invite/page.tsx` (converted), `invite-form.tsx` (new)

**Problem:** The invite form required the admin to manually type a raw Tenant UUID — error-prone and unnecessary when a tenant context is already active in the session.

**What changed:**
- `page.tsx` converted from `'use client'` to a server component.
- Calls `getSession()` (redirect to `/login` if unauthenticated) and `getTenantContext()`.
- Passes `resolvedTenantId` and `resolvedTenantName` as props to the new `InviteUserForm` client component.

**`InviteUserForm` (`invite-form.tsx`)** — extracted client component:
- When `resolvedTenantId` is set: renders a locked amber badge (`{tenantName} · auto`) instead of the UUID input; the UUID is carried via a hidden `<input>` and submitted normally.
- When no tenant context (PlatformAdmin without a selected tenant): retains the original editable UUID field.
- All submission and error-handling logic is identical to the original form.

**Not changed:** form fields (name, email, role), submission endpoint, redirect on success, cancel/back links.

---

### Fix 7 — Status Filter UI on Tenant Users List
**Files:** `apps/control-center/src/app/tenant-users/page.tsx`, `apps/control-center/src/lib/control-center-api.ts`

**Problem:** The Tenant Users list had no way to filter by user status — admins had to scan the full list to find inactive or invited users.

**What changed — `page.tsx`:**
- Added `status?: string` to `searchParams`.
- Added `StatusFilter` type and `STATUS_FILTERS` constant (`all`, `active`, `inactive`, `invited`).
- Status pill row inserted in the search form: each pill is a `type="submit"` button that posts `status=<value>` via GET. Active pill is visually highlighted (white card, indigo text, shadow).
- `status` passed to `controlCenterServerApi.users.list()`; `'all'` maps to `undefined` (no filter).
- Summary line appended with `· {status}` when a filter is active.
- "Clear" link appears when either `search` or a non-`all` status is active.

**What changed — `control-center-api.ts`:**
- Added `status?: string` to `users.list` params.
- Forwarded to `toQs()` so the backend receives it as a query string parameter.

**Not changed:** pagination, tenant scoping badge, Invite User button, table component, cache tags.

---

### Fix 8 — Permissions Page Read-Only Informational Banner
**File:** `apps/control-center/src/app/permissions/page.tsx`

**Problem:** The Permission Catalog page gave no indication that it was read-only. Admins unfamiliar with the system might expect to be able to add or modify permissions from the UI.

**What changed:**
- Added a blue informational banner below the page header.
- Banner text: explains the catalog is read-only, permissions are defined in product manifests, and changes must be made there.
- Uses an inline SVG info icon (no external dependency added).

**Not changed:** permission data fetching, product summary chips, catalog table, auth guard.

---

## Build Verification

| Check | Result |
|---|---|
| `dotnet build Identity.Api.csproj` | 0 errors, 0 warnings |
| Next.js (Control Center) TypeScript errors from this pass | 0 |
| Pre-existing TS errors in `notifications/` pages | Unchanged — not in scope |
| Application startup | Clean |

---

## Files Modified

### Identity Service (backend)
| File | Change type |
|---|---|
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Modified — Fixes 2, 3, 4 |
| `Identity.Api/Endpoints/AuthEndpoints.cs` | Modified — Fix 5 |
| `Identity.Domain/User.cs` | Modified — Fix 5 (`SetPassword` method) |

### Control Center (frontend)
| File | Change type |
|---|---|
| `src/app/tenant-users/invite/page.tsx` | Converted to server component — Fix 6 |
| `src/app/tenant-users/invite/invite-form.tsx` | New file (extracted client component) — Fix 6 |
| `src/app/tenant-users/page.tsx` | Modified — Fix 7 |
| `src/app/permissions/page.tsx` | Modified — Fix 8 |
| `src/lib/control-center-api.ts` | Modified — Fix 7 (`status` param) |

---

## Out of Scope (Not Addressed in This Pass)

The following items were noted but deliberately excluded to keep this a hardening pass:

- Email delivery for invitation tokens (notification service integration not in scope).
- Accept-invite UI page in the tenant portal (separate UIX work).
- Tenant selector dropdown on the invite form for PlatformAdmins (would require tenant search API and is a UX feature, not a hardening fix).
- Pre-existing TypeScript errors in the notifications section of Control Center.
- Pagination on `ListGroups` (pre-existing; no regression introduced).
