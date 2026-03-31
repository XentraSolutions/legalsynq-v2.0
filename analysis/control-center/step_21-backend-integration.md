# Step 21 – Backend Integration

## Status: Complete — 0 TypeScript errors

---

## Files Created

| File | Purpose |
|------|---------|
| `src/lib/api-client.ts` | New canonical HTTP client with `CONTROL_CENTER_API_BASE` env var, 401 redirect, 403/5xx `ApiError`, and all three future TODOs |
| `apps/control-center/.env.local` | Adds `CONTROL_CENTER_API_BASE=http://localhost:5010` for local dev |

---

## Files Updated

| File | What changed |
|------|-------------|
| `src/lib/control-center-api.ts` | **Full rewrite** — all MOCK_* data removed; every method now calls a real endpoint via `apiClient` |

---

## APIs Integrated

### Identity Admin — `/identity/api/admin/...`

| Method | HTTP | Endpoint |
|--------|------|---------|
| `tenants.list()` | GET | `/identity/api/admin/tenants?page&pageSize&search&tenantId` |
| `tenants.getById(id)` | GET | `/identity/api/admin/tenants/{id}` |
| `tenants.updateEntitlement(id, code, enabled)` | POST | `/identity/api/admin/tenants/{id}/entitlements/{code}` |
| `users.list()` | GET | `/identity/api/admin/users?page&pageSize&search&tenantId` |
| `users.getById(id)` | GET | `/identity/api/admin/users/{id}` |
| `roles.list()` | GET | `/identity/api/admin/roles` |
| `roles.getById(id)` | GET | `/identity/api/admin/roles/{id}` |
| `audit.list()` | GET | `/identity/api/admin/audit?page&pageSize&search&entityType&actor&tenantId` |
| `settings.list()` | GET | `/identity/api/admin/settings` |
| `settings.update(key, value)` | PATCH | `/identity/api/admin/settings/{key}` |
| `support.list()` | GET | `/identity/api/admin/support?page&pageSize&search&status&priority&tenantId` |
| `support.getById(id)` | GET | `/identity/api/admin/support/{id}` |
| `support.create(data)` | POST | `/identity/api/admin/support` |
| `support.addNote(caseId, msg)` | POST | `/identity/api/admin/support/{caseId}/notes` |
| `support.updateStatus(caseId, status)` | PATCH | `/identity/api/admin/support/{caseId}/status` |

### Platform Monitoring — `/platform/monitoring/...`

| Method | HTTP | Endpoint |
|--------|------|---------|
| `monitoring.getSummary()` | GET | `/platform/monitoring/summary` |

---

## `api-client.ts` — Key Design Decisions

### `ApiError` class

```ts
export class ApiError extends Error {
  status:  number;
  isForbidden:  boolean;  // status === 403
  isNotFound:   boolean;  // status === 404
  isServerError: boolean; // status >= 500
}
```

### 401 handling

`apiFetch` calls `redirect('/login?reason=session_expired')` immediately on HTTP 401.
This matches the existing behaviour of `requirePlatformAdmin()` in `auth-guards.ts`.

### 403/404/5xx handling

Throws `ApiError(status, message)`. Callers catch and display `fetchError` banners.
All existing pages already have `fetchError` state wired to error banner UI.

### `getById` null mapping

All `getById` methods catch `ApiError` with `status === 404` and return `null`,
preserving the existing `user === null → "Not found" UI` on detail pages.

### Cookie forwarding

`apiFetch` reads `platform_session` from `cookies()` and attaches it as
`Authorization: Bearer <token>` on every outbound request — same pattern as the
prior `serverApi` in `server-api-client.ts`.

### `CONTROL_CENTER_API_BASE` env var

```
CONTROL_CENTER_API_BASE=http://localhost:5010   # .env.local
```

Resolution order:
1. `CONTROL_CENTER_API_BASE` (new, spec-required)
2. `GATEWAY_URL` (existing legacy var — backward compatibility)
3. `http://localhost:5010` (hard-coded fallback)

---

## Error Handling Strategy

| Scenario | Behaviour |
|----------|---------|
| HTTP 401 | `redirect('/login?reason=session_expired')` — no error banner |
| HTTP 403 | `ApiError(403)` thrown → page `fetchError` banner: "Forbidden" |
| HTTP 404 on list | `ApiError(404)` thrown → page shows empty list error banner |
| HTTP 404 on getById | Caught → `null` returned → page shows "Not found" UI |
| HTTP 500 | `ApiError(500)` thrown → page `fetchError` banner with message |
| Network error | Native fetch throws → page `fetchError` banner |
| Non-JSON error body | Falls back to `HTTP {status} {statusText}` message |

---

## Removed Mock Logic

All of the following were **deleted** from `control-center-api.ts`:

- `MOCK_TENANTS[]` — 8 tenant records
- `MOCK_USERS[]` — 21 user records
- `USER_DETAIL_EXTRAS{}` — per-user detail overrides
- `buildUserDetail()` — function that assembled UserDetail from mock summary + extras
- `ALL_PRODUCTS[]` — 6 product definitions
- `ENABLED_BY_TYPE{}` — per-TenantType default product sets
- `ENTITLEMENT_OVERRIDES` — in-memory `Map<tenantId, Map<productCode, boolean>>`
- `getEntitlementOverrides()` — in-memory override accessor
- `DETAIL_EXTRAS{}` — per-tenant email / activeUserCount overrides
- `buildProductEntitlements()` — entitlement list builder
- `buildTenantDetail()` — full TenantDetail assembler
- `MOCK_PERMISSIONS[]` — 17 permission definitions
- `PERM_MAP` — permission lookup map
- `resolvePermissions()` — permission key → Permission[] mapper
- `MOCK_ROLES[]` — 5 role definitions
- `ROLE_TIMESTAMPS{}` — per-role created/updated timestamps
- `buildRoleDetail()` — RoleDetail assembler
- `MOCK_AUDIT_LOGS[]` — 28 audit log entries
- `MOCK_SUPPORT_CASES[]` — 7 support cases + notes
- `MOCK_NOTE_SEQ` / `MOCK_CASE_SEQ` — auto-increment counters
- `MOCK_MONITORING_SUMMARY` — static MonitoringSummary snapshot
- `MOCK_SETTINGS_STORE[]` — 5 platform settings

**Total removal**: ~800 lines of mock data and builder logic.

---

## Remaining TODOs

### In `api-client.ts`
```ts
// TODO: add retry/backoff
// TODO: add request tracing (correlation-id header)
// TODO: add API caching layer (Next.js fetch cache tags)
```

### In `control-center-api.ts` (per namespace)
```ts
// tenants, users, audit:
// TODO: enforce tenant scoping server-side
// TODO: validate tenant context against session

// entitlements, settings, support, monitoring:
// TODO: integrate with [specific] endpoint
```

---

## Independence Validation

- Zero imports from `apps/web`
- `api-client.ts` imports only: `next/navigation`, `next/headers`
- `control-center-api.ts` imports only: `@/lib/api-client`, `@/types/control-center`
- `server-api-client.ts` is left untouched (not removed — it is still valid)
- No new npm dependencies

---

## Any Issues or Assumptions

1. **Backend endpoint shapes** — The Identity admin endpoints are assumed to return
   data matching the TypeScript types in `types/control-center.ts`. If the backend
   returns different field names (e.g. `display_name` vs `displayName`), a response
   mapper will need to be added to `api-client.ts`. This is a **deferred integration
   concern** tracked by the per-method TODO markers.

2. **Admin endpoints not yet live** — The Identity service may not have implemented
   `/api/admin/tenants`, `/api/admin/users`, etc. yet. Until those endpoints are
   deployed, all Control Center pages will display `fetchError` banners. The mock
   data is removed as specified; the error banners are the correct "pre-live" state.

3. **Monitoring endpoint** — `GET /platform/monitoring/summary` is on the Platform
   service (not Identity). It will return 404/connection-refused until the Platform
   monitoring API is deployed.

4. **Support mutations** — `support.create`, `support.addNote`, `support.updateStatus`
   now make real HTTP calls. The `SupportDetailPanel` client component calls a Server
   Action that calls these methods; it will surface an error toast if the endpoint
   is unavailable.

5. **Entitlement toggle** — The `ProductEntitlementsPanel` "toggle" component calls
   `tenants.updateEntitlement()`. With the in-memory override store removed, toggling
   now requires the real endpoint. The panel will show an error if the endpoint is
   not yet live.

6. **`server-api-client.ts` retained** — The old client is kept as-is for backward
   compatibility. It is no longer referenced by `control-center-api.ts` but may be
   useful for direct gateway calls in future route handlers.
