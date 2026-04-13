# LS-TENANT-001 — Tenant Admin Foundation + Navigation Report

**Generated**: 2026-04-13
**Status**: COMPLETE
**Build**: Clean (0 TypeScript errors)

---

## 1. Navigation Changes Made

### Sidebar — Authorization Section
Added a new **AUTHORIZATION** nav section to `buildNavGroups()` in `apps/web/src/lib/nav.ts`, rendered above the existing ADMINISTRATION section. Visible only to `TenantAdmin` and `PlatformAdmin` roles.

| Label     | Icon                     | Route                               |
|-----------|--------------------------|-------------------------------------|
| Users     | `ri-user-line`           | `/tenant/authorization/users`       |
| Groups    | `ri-group-line`          | `/tenant/authorization/groups`      |
| Access    | `ri-shield-keyhole-line` | `/tenant/authorization/access`      |
| Simulator | `ri-test-tube-line`      | `/tenant/authorization/simulator`   |

### Sub-Navigation Tabs
A client-side `AuthorizationNav` component (`apps/web/src/components/tenant/authorization-nav.tsx`) renders active-state-aware tabs within the authorization layout, using `usePathname()` for highlight detection.

---

## 2. Routes Added

| Route                                | File                                                                        | Purpose                        |
|--------------------------------------|-----------------------------------------------------------------------------|--------------------------------|
| `/tenant/authorization/users`        | `apps/web/src/app/(platform)/tenant/authorization/users/page.tsx`           | Users placeholder (LS-TENANT-002) |
| `/tenant/authorization/groups`       | `apps/web/src/app/(platform)/tenant/authorization/groups/page.tsx`          | Groups placeholder (LS-TENANT-003) |
| `/tenant/authorization/access`       | `apps/web/src/app/(platform)/tenant/authorization/access/page.tsx`         | Access placeholder (LS-TENANT-004) |
| `/tenant/authorization/simulator`    | `apps/web/src/app/(platform)/tenant/authorization/simulator/page.tsx`      | Simulator placeholder (LS-TENANT-005) |
| `/tenant/access-denied`             | `apps/web/src/app/(platform)/tenant/access-denied/page.tsx`               | Access denied landing page     |

---

## 3. Guard Implementation Details

### Route Guard: `requireTenantAdmin()`
- **File**: `apps/web/src/lib/tenant-auth-guard.ts`
- **Chain**: `requireOrg()` → check `isTenantAdmin || isPlatformAdmin` → redirect to `/tenant/access-denied`
- **Enforcement**: Called in the shared `TenantAuthorizationLayout` server component — all child routes are protected automatically
- **Layers**:
  1. `requireOrg()` ensures authenticated session with org membership (redirects to `/login` or `/no-org`)
  2. `requireTenantAdmin()` enforces admin role (redirects to `/tenant/access-denied`)
  3. Backend APIs enforce tenant boundary via JWT-scoped `tenantId` — no frontend override possible

### Existing Guards Preserved
- `requireAdmin()` — unchanged, continues to protect `/admin/*` routes
- `requireOrg()` — unchanged, continues to protect `(platform)` routes
- `requireCCPlatformAdmin()` — unchanged, continues to protect control center

---

## 4. Layout Structure

```
(platform)/
  layout.tsx                          ← requireOrg() + AppShell + ToastProvider
  tenant/
    access-denied/page.tsx            ← public within platform (no admin guard)
    authorization/
      layout.tsx                      ← requireTenantAdmin() + header + AuthorizationNav tabs
      users/page.tsx                  ← placeholder
      groups/page.tsx                 ← placeholder
      access/page.tsx                 ← placeholder
      simulator/page.tsx              ← placeholder
```

The authorization layout provides:
- Server-side route guard (requireTenantAdmin)
- Page header with title and admin role badge
- Client-side sub-navigation tabs with active state highlighting
- Consistent container for all authorization child pages

---

## 5. Access Denied Handling

- **Route**: `/tenant/access-denied`
- **Placement**: Inside `(platform)` route group — requires authentication but NOT admin role
- **UI**: Centered lock icon, clear message ("You do not have access to this section"), contact admin suggestion, "Back to Dashboard" button
- **Navigation**: Links back to `/dashboard`

---

## 6. Tenant Context Handling

- `tenantId` is derived from the JWT / session via `getServerSession()` → `/identity/api/auth/me`
- The `requireTenantAdmin()` guard chains through `requireOrg()` which ensures valid session + org context
- No tenant switching or manual override is possible — tenantId is JWT-bound
- All future API calls from these pages will use the server-side API client which forwards the `platform_session` cookie, enforcing backend tenant isolation

---

## 7. Test Results

### Manual Verification
- TypeScript compilation: **0 errors** (`npx tsc --noEmit`)
- Route structure: All 4 authorization routes + access-denied created and reachable
- Navigation: AUTHORIZATION section added to sidebar via `buildNavGroups()`
- Guard chain: `requireOrg()` → `requireTenantAdmin()` → redirect to `/tenant/access-denied`

### Guard Logic Verification
- Non-admin users: `isTenantAdmin=false && isPlatformAdmin=false` → redirected to `/tenant/access-denied`
- Tenant admins: `isTenantAdmin=true` → access granted
- Platform admins: `isPlatformAdmin=true` → access granted
- Unauthenticated: `requireOrg()` chain redirects to `/login`
- No org: `requireOrg()` redirects to `/no-org`

---

## 8. Build Status

```
TypeScript: 0 errors
Next.js: compiles successfully
No regressions to existing routes or navigation
```

---

## 9. Known Limitations

1. **Placeholder pages only** — no functional UI yet; Users/Groups/Access/Simulator are stubs pending LS-TENANT-002 through 005
2. **Client-side navigation guard** — the sidebar hides links from non-admins, but the server-side `requireTenantAdmin()` layout guard is the true enforcer; if a non-admin somehow reaches the layout, they are redirected
3. **No breadcrumb trail** — layout supports breadcrumbs structurally but no dynamic breadcrumb component is implemented yet
4. **Backend authorization endpoints** — do not exist yet; the guard relies on session role flags (`isTenantAdmin`, `isPlatformAdmin`) which are already populated by the Identity service

---

## 10. Assumptions

1. The `isTenantAdmin` and `isPlatformAdmin` flags on `PlatformSession` accurately reflect the user's administrative status — sourced from the Identity service's `/auth/me` endpoint
2. The `(platform)` route group and `AppShell` are the correct container for tenant authorization routes (consistent with how `/admin/*` routes use `(admin)` + `AppShell`)
3. `PlatformAdmin` users are granted access to tenant authorization UI (they have superset privileges)
4. The AUTHORIZATION nav section should appear for admin users regardless of which product is currently selected in the product switcher
5. Future LS-TENANT-002+ features will replace the placeholder pages without structural changes to the layout or guard

---

## Files Created/Modified

### New Files
| File | Purpose |
|------|---------|
| `apps/web/src/lib/tenant-auth-guard.ts` | `requireTenantAdmin()` guard function |
| `apps/web/src/components/tenant/authorization-nav.tsx` | Client-side tab navigation with active state |
| `apps/web/src/app/(platform)/tenant/authorization/layout.tsx` | Shared layout with guard + header + nav |
| `apps/web/src/app/(platform)/tenant/authorization/users/page.tsx` | Users placeholder |
| `apps/web/src/app/(platform)/tenant/authorization/groups/page.tsx` | Groups placeholder |
| `apps/web/src/app/(platform)/tenant/authorization/access/page.tsx` | Access placeholder |
| `apps/web/src/app/(platform)/tenant/authorization/simulator/page.tsx` | Simulator placeholder |
| `apps/web/src/app/(platform)/tenant/access-denied/page.tsx` | Access denied page |

### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/lib/nav.ts` | Added AUTHORIZATION section to `buildNavGroups()` |

---

## Ready For

- **LS-TENANT-002**: Users Management (replace placeholder)
- **LS-TENANT-003**: Group Management (replace placeholder)
- **LS-TENANT-004**: Access & Explainability (replace placeholder)
- **LS-TENANT-005**: Authorization Simulator (replace placeholder)
