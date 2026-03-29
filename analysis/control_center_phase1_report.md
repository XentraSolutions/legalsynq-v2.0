# Control Center Phase 1 Report

## Files Created

| File | Purpose |
|---|---|
| `apps/web/src/lib/control-center-nav.ts` | Nav builder — `buildControlCenterNav(session)` returning 5 NavGroups with 9 sections |
| `apps/web/src/components/shell/control-center-shell.tsx` | Dedicated layout shell — mirrors AppShell but uses Control Center nav and identity badge |
| `apps/web/src/app/(control-center)/layout.tsx` | Route group layout — calls `requirePlatformAdmin()`, renders `ControlCenterShell` |
| `apps/web/src/app/(control-center)/control-center/page.tsx` | Dashboard — async Server Component with 8-module card grid |
| `apps/web/src/app/api/identity/[...path]/route.ts` | Identity BFF proxy — catch-all proxy to gateway → identity:5001 |
| `apps/web/src/types/control-center.ts` | Domain types — `TenantSummary`, `TenantDetail`, `TenantProductSummary`, `TenantUserSummary`, `RoleSummary`, `ProductEntitlementSummary`, `AuditLogEntry`, `PlatformSetting`, `SystemHealthSummary`, `PagedResponse<T>` |
| `apps/web/src/lib/control-center-api.ts` | Typed API wrappers — `controlCenterServerApi` (server-side) + `controlCenterApi` (client-side via BFF) |

## Files Updated

| File | Change |
|---|---|
| `apps/web/src/lib/auth-guards.ts` | Added `requirePlatformAdmin()` — strictly `isPlatformAdmin` only; existing `requireAdmin()` behavior unchanged |

## Routing Added

| URL | Handler | Guard |
|---|---|---|
| `/control-center` | `(control-center)/control-center/page.tsx` | `requirePlatformAdmin()` via layout |
| `/control-center/*` (future) | Under `(control-center)/control-center/` | Same layout guard — inherited |
| `/api/identity/[...path]` | BFF proxy → `GATEWAY_URL/identity/...` | Cookie gate via middleware; `Authorization: Bearer` injected by proxy |

Future routes that are card-linked but not yet created:
- `/control-center/tenants`
- `/control-center/tenant-users`
- `/control-center/roles`
- `/control-center/products`
- `/control-center/support`
- `/control-center/audit-logs`
- `/control-center/monitoring`
- `/control-center/settings`

## Auth Changes

### Added: `requirePlatformAdmin()` in `lib/auth-guards.ts`

```ts
export async function requirePlatformAdmin(): Promise<PlatformSession> {
  const session = await requireSession();
  if (!session.isPlatformAdmin) redirect('/dashboard');
  return session;
}
```

- Strictly checks `session.isPlatformAdmin` — TenantAdmins are redirected to `/dashboard`
- Consistent with existing guard chain: calls `requireSession()` (which calls `getServerSession()` → `/auth/me` → identity service)
- `requireAdmin()` is unchanged — still permits both TenantAdmin and PlatformAdmin

### Middleware: No change required

The existing `middleware.ts` default branch already gates all non-public paths (including `/control-center/*`) against the `platform_session` cookie. The route is protected by cookie existence at middleware level; the real enforcement (`isPlatformAdmin` check) happens server-side in the layout.

## API Proxy Added

`apps/web/src/app/api/identity/[...path]/route.ts`

Mirrors the established BFF proxy pattern from `api/careconnect`, `api/fund`, and `api/lien`:
- Reads `platform_session` HttpOnly cookie
- Injects `Authorization: Bearer <token>` on the gateway request
- Forwards all methods: GET, POST, PUT, PATCH, DELETE
- Forwards `X-Correlation-Id` response header
- Returns `503` if the gateway is unreachable

URL mapping:
```
Browser:   POST /api/identity/api/users
→ Proxy:   POST ${GATEWAY_URL}/identity/api/users  + Authorization: Bearer
→ Gateway: → identity:5001/api/users
```

## Remaining TODOs

1. **Build all 8 sub-pages** — `tenants/`, `tenant-users/`, `roles/`, `products/`, `support/`, `audit-logs/`, `monitoring/`, `settings/` pages not yet created (Phase 2–7 in the discovery plan).

2. **Confirm identity admin endpoint contracts** — All `controlCenterServerApi` methods have `// TODO: confirm endpoint` comments. Endpoints like `GET /identity/api/admin/tenants` and `GET /identity/api/admin/users` do not exist yet in `Identity.Api`.

3. **Audit log backend** — `controlCenterServerApi.auditLogs.list()` is a stub; no audit service or schema exists. `AuditLogEntry` type will need adjustment once the backend is designed.

4. **Monitoring data depth** — Current `controlCenterServerApi.monitoring.health()` only calls `/health` on each service (returning `{ status: "ok" }`). Deep metrics (latency, error rates) require observability tooling.

5. **Control Center navigation link in operator portal** — The operator portal's existing `lib/nav.ts` `buildNavGroups()` has an Admin section (`/admin/*`) but no link to `/control-center`. Consider adding a "Control Center" link for PlatformAdmin users so they can navigate between both areas.

6. **Shared UI primitives** — `components/ui/` is still empty. As Control Center grows (forms, modals, toasts), shared primitives should be extracted before Phase 4+.

7. **Type check** — Confirmed passing: `tsc --noEmit` exits cleanly with zero errors after all Phase 1 files.

## Any Backend Dependencies or Unknowns

| Dependency | Status | Impact |
|---|---|---|
| `GET /identity/api/admin/tenants` | Not yet implemented | Phase 2 (Tenants list page) blocked |
| `GET /identity/api/admin/tenants/:id` | Not yet implemented | Phase 2 (Tenant detail page) blocked |
| `GET /identity/api/admin/users` | Not yet implemented | Phase 3 (Tenant Users page) blocked |
| `GET /identity/api/admin/roles` | Not yet implemented | Phase 4 (Roles page) blocked |
| `GET /identity/api/admin/product-entitlements` | Not yet implemented | Phase 4 (Products page) blocked |
| `POST /identity/api/admin/tenants/:id/activate` | Not yet implemented | Phase 2 activate action blocked |
| Audit backend (any service) | Does not exist | Phase 6 (Audit Logs) blocked |
| Observability / metrics API | Does not exist | Phase 5 Monitoring limited to `/health` pings |
| Gateway proxying `/identity/*` → identity:5001 | **Assumed working** — verify in `Gateway.Api/appsettings.json` | All identity BFF calls depend on this |
