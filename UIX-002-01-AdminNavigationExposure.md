# UIX-002-01 — Admin Navigation Exposure
## Activity Report

**Feature track:** Admin area — navigation, user management page, identity scoping  
**Affected apps:** `apps/web` (port 5000), `apps/services/identity`  
**Status:** Complete ✅

---

## 1. Objective

Expose the Administration navigation group in the web app's sidebar exclusively to `TenantAdmin` and `PlatformAdmin` roles, then wire the `/admin/users` page to the live identity API with a real, interactive user table — replacing the placeholder that previously existed.

---

## 2. Scope of Work

The work was carried out across two sessions and covers three discrete phases:

| Phase | Description | Layer |
|-------|-------------|-------|
| 1 | Admin nav group exposed in sidebar | Frontend (web app) |
| 2 | `/admin/users` page wired to live `GET /identity/api/users` | Frontend + BFF fix |
| 3 | `GET /api/users` tenant-scoped at the identity service | Backend (.NET) |

---

## 3. Phase 1 — Admin Navigation Exposure

### 3.1 `apps/web/src/lib/nav.ts`

`buildNavGroups(session: PlatformSession)` was previously a stub that returned an empty array `[]` for all users.

**Changed to:**

```typescript
export function buildNavGroups(session: PlatformSession): NavSection[] {
  if (!session.isPlatformAdmin && !session.isTenantAdmin) return [];

  const items: NavSection['items'] = [
    { href: '/admin/users',         label: 'Users',         icon: 'ri-user-3-line'   },
    { href: '/admin/organizations', label: 'Organizations', icon: 'ri-building-line' },
    { href: '/admin/products',      label: 'Products',      icon: 'ri-grid-line'     },
  ];

  if (session.isPlatformAdmin) {
    items.push({ href: '/admin/tenants', label: 'All Tenants', icon: 'ri-building-4-line' });
  }

  return [{ heading: 'ADMINISTRATION', items }];
}
```

- Standard users receive `[]` — nothing is rendered.
- `TenantAdmin` sees: Users, Organizations, Products.
- `PlatformAdmin` sees all of the above plus **All Tenants**.

### 3.2 `apps/web/src/components/shell/sidebar.tsx`

The `Sidebar` client component previously had no awareness of admin sections.

**Added:**
- Import of `buildNavGroups` from `@/lib/nav` and `useSession` from `@/hooks/use-session`.
- `const { session } = useSession();` and `const adminSections = session ? buildNavGroups(session) : []`.
- A rendering block for `adminSections` inside the scrollable flex area, below the product nav sections. Respects the sidebar collapsed state:
  - **Expanded:** shows the `ADMINISTRATION` heading and full item labels.
  - **Collapsed:** shows icons only, with a horizontal divider separator.

### 3.3 Access control — two-layer model

No changes were made to `src/middleware.ts`. The existing design is intentional:

1. **Cookie gate (middleware):** Unauthenticated users are redirected to `/login` before reaching any `/admin/*` route.
2. **Role gate (layout):** `apps/web/src/app/(admin)/layout.tsx` calls `requireAdmin()`, which redirects authenticated non-admin users to `/dashboard`.

Adding JWT decoding in middleware would contradict the documented architectural decision in that file. The two-layer approach is correct and sufficient.

---

## 4. Phase 2 — Live User Table on `/admin/users`

### 4.1 Environment variable fix — `GATEWAY_URL`

**File:** `apps/web/.env.local`

`GATEWAY_URL` was set to `http://localhost:5000` — the web app's own port. This caused every server-side API call made via `serverApi` to loop back into Next.js at a path with no handler, silently returning 404.

**Fixed to:** `http://localhost:5010` (the actual .NET API gateway).

This also corrects the `next.config.mjs` fallback rewrite (`/api/:path* → ${GATEWAY_URL}/:path*`), which now correctly forwards unhandled API paths to the gateway instead of back to the web app.

### 4.2 `apps/web/src/types/admin.ts` (new file)

Defines `UserResponse` matching the identity service's DTO exactly:

```typescript
export interface UserResponse {
  id:              string;
  tenantId:        string;
  email:           string;
  firstName:       string;
  lastName:        string;
  isActive:        boolean;
  roles:           string[];
  organizationId?: string;
  orgType?:        string;
  productRoles?:   string[];
}
```

### 4.3 `apps/web/src/app/(admin)/admin/users/UserTable.tsx` (new file)

A `'use client'` component that accepts the full `UserResponse[]` list from the server component and handles all interactivity locally:

| Feature | Detail |
|---------|--------|
| **Search** | Filters by `firstName`, `lastName`, or `email` — case-insensitive, instant |
| **Status filter** | Toggle row: All / Active / Inactive (mapped from `isActive: boolean`) |
| **Result count** | Live count of filtered results shown in the toolbar |
| **Table columns** | User (initials avatar + full name), Email, Status badge, Roles, Org Type |
| **Status badge** | Green dot + "Active" / grey dot + "Inactive" |
| **Role badges** | Indigo pill per role; dash for users with no roles |
| **Pagination** | 15 rows per page, Previous / Next buttons; page resets on filter change |
| **Empty state** | Context-aware message distinguishing "no users" from "no filter matches" |

### 4.4 `apps/web/src/app/(admin)/admin/users/page.tsx` (rewritten)

Converted from a placeholder to a real server component:

```
requireAdmin()
  → serverApi.get<UserResponse[]>('/identity/api/users')
    → gateway:5010 /identity/api/users
      → identity service /api/users  (JWT-authenticated, tenant-scoped)
```

- Errors (service unavailable, non-200 HTTP) are caught and displayed as a red banner — the page never throws an unhandled exception.
- On success, the full user list is passed to `<UserTable users={users} />`.
- The previous `TODO` placeholder is removed.

---

## 5. Phase 3 — Tenant Scoping in the Identity Service

### Problem

`GET /api/users` called `userService.GetAllAsync()` → `_userRepository.GetAllWithRolesAsync()` with no tenant filter. Any authenticated user with a valid JWT could see every user in the database regardless of tenant.

### Changes

Four files were modified across the identity service's layered architecture:

#### `Identity.Application/IUserRepository.cs`
Added interface method:
```csharp
Task<List<User>> GetByTenantWithRolesAsync(Guid tenantId, CancellationToken ct = default);
```

#### `Identity.Infrastructure/Repositories/UserRepository.cs`
Implemented with an EF Core `.Where(u => u.TenantId == tenantId)` predicate applied before the `Include` chain, so the SQL `WHERE` clause runs at the database level:
```csharp
public Task<List<User>> GetByTenantWithRolesAsync(Guid tenantId, CancellationToken ct = default) =>
    _db.Users
        .Where(u => u.TenantId == tenantId)
        .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
            .ThenInclude(s => s.Role)
        .OrderBy(u => u.LastName)
        .ThenBy(u => u.FirstName)
        .ToListAsync(ct);
```

#### `Identity.Application/Interfaces/IUserService.cs`
Added interface method:
```csharp
Task<List<UserResponse>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
```

#### `Identity.Application/Services/UserService.cs`
Implemented by delegating to the repository:
```csharp
public async Task<List<UserResponse>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
{
    var users = await _userRepository.GetByTenantWithRolesAsync(tenantId, ct);
    return users.Select(ToResponse).ToList();
}
```

#### `Identity.Api/Endpoints/UserEndpoints.cs`
`GET /api/users` updated to inject `ClaimsPrincipal`, extract `tenant_id`, and call `GetByTenantAsync`. Also hardened with `.RequireAuthorization()`:

```csharp
app.MapGet("/api/users", async (
    ClaimsPrincipal   caller,
    IUserService      userService,
    CancellationToken ct) =>
{
    var tenantIdStr = caller.FindFirstValue("tenant_id");
    if (!Guid.TryParse(tenantIdStr, out var tenantId))
        return Results.Unauthorized();

    var users = await userService.GetByTenantAsync(tenantId, ct);
    return Results.Ok(users);
}).RequireAuthorization();
```

`GetAllAsync()` is preserved for potential future platform-admin cross-tenant use (e.g. a dedicated `/api/admin/users` endpoint in the Control Center track).

### Scoping model after Phase 3

| Caller | JWT `tenant_id` | Result |
|--------|----------------|--------|
| TenantAdmin (web portal) | Own tenant UUID | Users in their tenant only |
| PlatformAdmin (web portal) | Own tenant UUID | Users in their tenant only |
| No valid `tenant_id` claim | — | 401 Unauthorized |

> **Note:** Cross-tenant visibility for PlatformAdmin (Control Center) is handled by the separate `/identity/api/admin/users` endpoint with its own authorization policy — outside the scope of this track.

---

## 6. Files Modified — Full List

| File | Change type | Description |
|------|------------|-------------|
| `apps/web/.env.local` | Fix | `GATEWAY_URL` corrected from port 5000 → 5010 |
| `apps/web/src/lib/nav.ts` | Updated | `buildNavGroups` implemented from empty stub |
| `apps/web/src/components/shell/sidebar.tsx` | Updated | Admin sections wired via `useSession` + `buildNavGroups` |
| `apps/web/src/types/admin.ts` | New | `UserResponse` interface matching identity DTO |
| `apps/web/src/app/(admin)/admin/users/UserTable.tsx` | New | Client component: search, filter, pagination, table |
| `apps/web/src/app/(admin)/admin/users/page.tsx` | Rewritten | Server component: live fetch + error handling |
| `apps/services/identity/Identity.Application/IUserRepository.cs` | Updated | `GetByTenantWithRolesAsync` added to interface |
| `apps/services/identity/Identity.Infrastructure/Repositories/UserRepository.cs` | Updated | `GetByTenantWithRolesAsync` implemented |
| `apps/services/identity/Identity.Application/Interfaces/IUserService.cs` | Updated | `GetByTenantAsync` added to interface |
| `apps/services/identity/Identity.Application/Services/UserService.cs` | Updated | `GetByTenantAsync` implemented |
| `apps/services/identity/Identity.Api/Endpoints/UserEndpoints.cs` | Updated | `GET /api/users` scoped by `tenant_id` claim, `.RequireAuthorization()` added |

---

## 7. Validation

| Check | Result |
|-------|--------|
| Admin nav visible for TenantAdmin / PlatformAdmin | ✅ `buildNavGroups` returns `ADMINISTRATION` section |
| Admin nav hidden for standard users | ✅ `buildNavGroups` returns `[]`; nothing rendered |
| All Tenants link visible for PlatformAdmin only | ✅ Guarded by `session.isPlatformAdmin` |
| `/admin/users` page guarded | ✅ `requireAdmin()` in both layout and page |
| Page fetches live users | ✅ `serverApi.get('/identity/api/users')` via corrected `GATEWAY_URL` |
| Service unavailable handled gracefully | ✅ `ServerApiError` caught; red error banner shown |
| Users filtered to calling user's tenant | ✅ `tenant_id` claim extracted; DB query scoped with `.Where(u => u.TenantId == tenantId)` |
| Unauthenticated requests to `GET /api/users` rejected | ✅ `.RequireAuthorization()` on endpoint |
| TypeScript — web app | ✅ Zero new errors (2 pre-existing in unrelated components) |
| .NET build — identity service | ✅ `dotnet build` clean, 0 errors |
