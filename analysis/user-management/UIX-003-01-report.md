# UIX-003-01 — TenantAdmin Enablement: Access Control Management

**Date:** 2026-04-01  
**Scope:** Allow TenantAdmins to manage their own tenant's users, roles, memberships, and group memberships from the Control Center — with full tenant isolation enforced at every layer.

---

## Summary

UIX-003-01 unlocks 11 mutation operations for TenantAdmins while ensuring they can only ever affect users and groups within their own tenant. Enforcement is applied at two independent layers: the BFF (Next.js Route Handlers) and the Identity service backend (ClaimsPrincipal checks in AdminEndpoints.cs). Neither layer alone is sufficient — both must agree before an operation proceeds.

---

## Changes Delivered

### Layer 1 — Auth Guard (`auth-guards.ts`)

Added `requireAdmin()` alongside the existing `requirePlatformAdmin()`:

```ts
export async function requireAdmin(): Promise<PlatformSession> {
  const session = await getServerSession();
  if (!session) redirect(`${BASE_PATH}/login?reason=unauthenticated`);
  if (!session.isPlatformAdmin && !session.isTenantAdmin)
    redirect(`${BASE_PATH}/login?reason=unauthorized`);
  return session;
}
```

- Passes if the caller holds **either** `PlatformAdmin` or `TenantAdmin`.
- Returns the full `PlatformSession` (unchanged type — already carries `isTenantAdmin`, `tenantId`, `isPlatformAdmin`).
- `requirePlatformAdmin()` is unchanged — all existing PlatformAdmin-only pages continue to work exactly as before.

---

### Layer 2 — Frontend Pages (2 files)

| File | Before | After |
|------|--------|-------|
| `tenant-users/page.tsx` | `requirePlatformAdmin()` | `requireAdmin()` |
| `tenant-users/[id]/page.tsx` | `requirePlatformAdmin()` | `requireAdmin()` |

TenantAdmin users can now navigate to the user list and user detail pages. The backend's `ListUsers` and `GetUser` handlers already enforce TenantAdmin tenant scoping via ClaimsPrincipal (pre-existing — no changes needed).

---

### Layer 3 — BFF Routes (11 files)

All 11 mutation BFF routes updated from `requirePlatformAdmin()` to `requireAdmin()`.

| Route | Method | Special scope logic |
|-------|--------|-------------------|
| `users/invite` | POST | + TenantAdmin scope: `body.tenantId` must equal `session.tenantId` (403 otherwise) |
| `users/[id]/roles` | POST | — |
| `users/[id]/roles/[roleId]` | DELETE | — |
| `users/[id]/activate` | POST | — |
| `users/[id]/deactivate` | POST | — |
| `users/[id]/resend-invite` | POST | — |
| `users/[id]/memberships` | POST | — |
| `users/[id]/memberships/[membershipId]` | DELETE | — |
| `users/[id]/memberships/[membershipId]/set-primary` | POST | — |
| `groups/[id]/members` | POST | — |
| `groups/[id]/members/[userId]` | DELETE | — |

The invite route is the only one where the BFF adds a scope check — because the tenantId is caller-supplied in the request body. All other mutations target a specific entity by URL (user id or group id); tenant isolation for those is enforced exclusively by the backend via JWT claims.

---

### Layer 4 — Backend: `AdminEndpoints.cs` (Identity service)

#### New helper method

```csharp
private static bool IsCrossTenantAccess(ClaimsPrincipal caller, Guid targetTenantId)
{
    if (caller.IsInRole("PlatformAdmin")) return false;
    var raw = caller.FindFirstValue("tenant_id");
    return raw is null || !Guid.TryParse(raw, out var callerTid) || callerTid != targetTenantId;
}
```

Returns `true` if the caller is a non-PlatformAdmin whose `tenant_id` JWT claim does not match the target entity's tenant. PlatformAdmins are never restricted. A missing or unparseable claim is treated as a mismatch (deny by default).

#### Handlers hardened (ClaimsPrincipal + boundary check added)

| Handler | Check target |
|---------|-------------|
| `DeactivateUser` | `user.TenantId` |
| `ActivateUser` | `user.TenantId` |
| `ResendInvite` | `user.TenantId` |
| `AssignRole` | `user.TenantId` (uses existing `ctx.User`) |
| `RevokeRole` | `user.TenantId` |
| `InviteUser` | `body.TenantId` (after tenant lookup, before email uniqueness check) |
| `AssignMembership` | `user.TenantId` (before org cross-tenant check) |
| `SetPrimaryMembership` | `user.TenantId` (user loaded via `id` param) |
| `RemoveMembership` | `user.TenantId` (user loaded via `id` param) |
| `AddGroupMember` | `group.TenantId` (group already loaded first) |
| `RemoveGroupMember` | `group.TenantId` (group loaded before membership) |

The pattern is consistent across all 11:
1. Load the target entity (user or group).
2. If `IsCrossTenantAccess(caller, entity.TenantId)` → `return Results.Forbid()` (403).
3. Continue with business logic.

For `SetPrimaryMembership` and `RemoveMembership`, the user is loaded first (before the membership query) since the `id` URL param is the user id. The existing membership safety rules (LAST_MEMBERSHIP, PRIMARY_MEMBERSHIP) are unaffected.

---

## Invariants Preserved

- **PlatformAdmin** behaviour is completely unchanged — all operations work as before, no extra checks run.
- **Read endpoints** (`ListUsers`, `GetUser`, `ListGroups`, `ListOrganizations`) already had TenantAdmin scoping from earlier phases — not touched.
- **Pre-existing TS errors** in notification pages were not modified.
- **`requirePlatformAdmin()`** is preserved and still used by all non-user-management pages.

---

## Defense-in-Depth Model

```
TenantAdmin → BFF Route
                 ↓
         requireAdmin() — allows TenantAdmin through
                 ↓
         (invite only) tenantId == session.tenantId check → 403 if mismatch
                 ↓
         → CC API lib → Gateway → Identity Service
                                        ↓
                               ClaimsPrincipal injected from JWT
                                        ↓
                               IsCrossTenantAccess() check → Forbid() if cross-tenant
                                        ↓
                               Business logic executes
```

Even if the BFF layer were bypassed (direct API call with a valid TenantAdmin JWT), the Identity service would still return 403 for any cross-tenant mutation. Both layers must pass independently.
