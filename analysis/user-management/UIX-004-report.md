# UIX-004 — Audit & Activity Timeline Report

**Feature ID:** UIX-004  
**Status:** Complete  
**Date:** 2026-04-02  
**Scope:** Global audit log page + per-user activity timeline in the Control Center

---

## 1. Summary

UIX-004 delivers two audit surfaces in the Control Center:

1. **`/audit-logs`** — A system-wide audit log viewer available to PlatformAdmin and TenantAdmin. Supports multi-mode data sourcing (`legacy`, `canonical`, `hybrid`), server-side filtering, pagination, and interactive row detail panels for canonical events.

2. **User Activity Timeline** — A compact activity section on each user detail page (`/tenant-users/[id]`), showing the 15 most recent canonical audit events involving that user, with a link to the full log filtered to them.

---

## 2. Backend — T001: `GET /api/admin/users/{id}/activity`

**File:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`  
**Route:** `GET /api/admin/users/{id:guid}/activity`  
**Handler:** `GetUserActivity`  
**Registered at:** line 109

### What it does

Queries the local `AuditLogs` table in the Identity database, filtered by `EntityId = userId.ToString()`. Represents admin-emitted events captured by the Identity service: lock/unlock, force-logout, password reset, role assignment changes, org membership changes, group membership changes.

For richer canonical events (login, logout, invite-accepted, OAuth, etc.) the Control Center queries the Audit service directly via `auditCanonical.listForUser`.

### Request parameters

| Parameter  | Type   | Default | Description |
|---|---|---|---|
| `page`     | int    | 1       | 1-based page index |
| `pageSize` | int    | 20      | Clamped 1–100 |
| `category` | string | ""      | Optional `EntityType` filter |

### Access control

- `IsCrossTenantAccess` check: TenantAdmin can only read activity for users in their own tenant; PlatformAdmin sees all.
- Returns 404 (not 403) if the user does not exist — consistent with the existing user endpoint pattern.
- Returns 403 (Forbid) for cross-tenant access attempts.

### Response shape

```json
{
  "items": [
    {
      "id": "...",
      "actorName": "admin@example.com",
      "actorType": "Admin",
      "action": "user.lock",
      "entityType": "User",
      "entityId": "{userId}",
      "metadata": { "reason": "policy violation" },
      "createdAtUtc": "2026-04-01T12:00:00Z"
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20
}
```

**Note on metadata deserialization:** EF Core cannot translate `JsonSerializer.Deserialize` (it has optional params) inside an expression tree. The implementation materialises the raw projection first, then deserialises `MetadataJson` in-memory — safe and correct.

---

## 3. Frontend Types — T002

**File:** `apps/control-center/src/types/control-center.ts`

All required types were already present:

| Type | Purpose |
|---|---|
| `AuditLogEntry` | Shape of a local Identity audit log row |
| `UserActivityEvent` | Union / presentation type for timeline events |
| `CanonicalAuditEvent` | Platform Audit Event Service event shape |
| `AuditReadMode` | `'legacy' \| 'canonical' \| 'hybrid'` |

**File:** `apps/control-center/src/lib/api-mappers.ts`

| Export | Purpose |
|---|---|
| `AUDIT_EVENT_LABELS` | `Record<string, string>` — 30+ event type → human label mappings |
| `mapEventLabel(eventType)` | Returns label if known, falls back to raw event type |
| `mapAuditLog` | Normalises local Identity log entries (snake_case → camelCase, safe defaults) |
| `mapCanonicalAuditEvent` | Normalises canonical audit events from the Audit service |

---

## 4. CC API Client — T003

**File:** `apps/control-center/src/lib/control-center-api.ts`

### `users.getActivity(userId, { page, pageSize, category })`

Proxies via the CC BFF (`/api/identity/admin/users/{id}/activity`). Returns `{ items: AuditLogEntry[]; totalCount: number } | null`. Used by the BFF route to avoid double-proxying.

### `auditCanonical.listForUser({ userId, tenantId, page, pageSize })`

Queries the canonical Audit Event Service (`/audit-service/audit/events?targetId={userId}`). Returns `{ items: CanonicalAuditEvent[]; totalCount: number }`. This is the primary data source for `UserActivityPanel`. Tagged with `cc:auditCanonical` for Next.js cache revalidation.

---

## 5. BFF Route — T004

**File:** `apps/control-center/src/app/api/identity/admin/users/[id]/activity/route.ts`

`GET /api/identity/admin/users/[id]/activity`

- Guards with `requireAdmin()` (TenantAdmin or PlatformAdmin)
- Forwards `page`, `pageSize`, `category` query params
- Delegates to `controlCenterServerApi.users.getActivity`
- Returns JSON; 500 on upstream error with message

---

## 6. `/audit-logs` Page — T005

**File:** `apps/control-center/src/app/audit-logs/page.tsx` (463 lines)

### Data sourcing modes

Controlled by `AUDIT_READ_MODE` environment variable:

| Mode | Data source | Indicator |
|---|---|---|
| `legacy` (default) | Identity DB → `GET /identity/api/admin/audit` | Grey badge |
| `canonical` | Platform Audit Event Service → `GET /audit-service/audit/events` | Blue badge |
| `hybrid` | Canonical first; falls back to legacy on error | Violet badge |

### Filters (server-side GET form — no JS required)

| Filter | Field | Notes |
|---|---|---|
| Search | `search` | Action, entity ID, actor name |
| Event type | `eventType` | Canonical event code |
| Category | `category` | Security, Access, Business, Administrative, Compliance, DataChange |
| Severity | `severity` | Info, Low, Medium, High, Critical |
| Correlation ID | `correlationId` | Trace correlation |
| Date range | `dateFrom` / `dateTo` | ISO-8601 |

### Table components

- **Canonical mode:** `CanonicalAuditTableInteractive` — clickable rows, side panel with full event detail.
- **Legacy mode:** `AuditLogTable` — standard tabular display of Identity audit entries.

### Tenant scoping

- `requireAdmin()` enforces PlatformAdmin or TenantAdmin access.
- TenantAdmin: tenant context from JWT propagated to both legacy and canonical API calls → automatically scoped.
- PlatformAdmin: no tenant filter applied; sees cross-tenant events.

### Hybrid fallback warning

When in `hybrid` mode and the canonical service is unavailable, a visible amber banner informs operators that legacy data is being shown. No silent fallback — operators always know which data source is active.

---

## 7. `UserActivityPanel` Component — T006

**File:** `apps/control-center/src/components/users/user-activity-panel.tsx`

A server component that renders a compact timeline of a user's canonical audit events.

### Data source

`auditCanonical.listForUser({ userId, tenantId, page: 1, pageSize: 15 })` — most recent 15 events, newest first.

### UI elements

- Section header ("Activity Timeline") with a "Read-only" badge
- Per-event row: category icon + colour, human event label, "New" badge (within last 24h), actor, timestamp, outcome pill (Success / Failed / other), category pill, IP address (when available), raw event type (small mono)
- Footer: "View full history →" link to `/audit-logs?targetId={userId}`

### Error / empty states

- **Unavailable:** amber banner — "Activity feed unavailable. The audit service could not be reached."
- **Empty:** friendly empty state — "No activity recorded yet."
- No crash on service error; `unavailable` flag set via try/catch.

### Integration into user detail page

**File:** `apps/control-center/src/app/tenant-users/[id]/page.tsx`

```tsx
import { UserActivityPanel } from '@/components/users/user-activity-panel';

// Line 180:
<UserActivityPanel userId={user.id} tenantId={user.tenantId} />
```

Rendered in the read-only section of the user detail page, after `UserDetailCard` and `UserSecurityPanel`, before `EffectivePermissionsPanel`.

---

## 8. Navigation — T007

**File:** `apps/control-center/src/lib/nav.ts`

`/audit-logs` is registered in the `OPERATIONS` section with `badge: 'LIVE'`:

```ts
{ href: '/audit-logs', label: 'Audit Logs', icon: 'ri-file-list-3-line', badge: 'LIVE' },
```

Access guard (`requireAdmin`) in the page covers TenantAdmin and PlatformAdmin — consistent with all other admin pages in the Control Center.

---

## 9. Known Limitations / Deferred Items

| Item | Decision |
|---|---|
| `UserActivityPanel` uses canonical service only | The local Identity `AuditLogs` are accessible via `users.getActivity` (BFF route implemented) but `UserActivityPanel` uses canonical events for richer coverage. Admin-emitted events (lock/unlock, etc.) are also ingested into the canonical service by the Identity API, so coverage is complete when the canonical service is running. |
| Hybrid mode fallback for `UserActivityPanel` | The panel does not implement a local-log fallback — it shows "unavailable" on canonical service error. Hybrid fallback for the panel is deferred. |
| Category filter uses `EntityType` not `Category` | The local Identity `AuditLogs` schema does not have a `Category` column; the `category` param maps to `EntityType` for the local log endpoint. This is invisible to users. |
| No export from user activity panel | Export is available on the global `/audit-logs` page only. |

---

## 10. Success Criteria Verification

| Criterion | Status |
|---|---|
| `GET /api/admin/users/{id}/activity` endpoint added to Identity | ✅ Line 109 + handler at line 1267 |
| TenantAdmin cross-tenant boundary enforced | ✅ `IsCrossTenantAccess` check |
| `UserActivityEvent` type defined in `control-center.ts` | ✅ Line 307 |
| `AUDIT_EVENT_LABELS` and `mapEventLabel` in `api-mappers.ts` | ✅ Lines 584, 618 |
| `users.getActivity` in CC API client | ✅ Line 475 |
| `auditCanonical.listForUser` in CC API client | ✅ Line 929 |
| BFF route `GET /api/identity/admin/users/[id]/activity` | ✅ `route.ts` implemented |
| `/audit-logs` page with filters, pagination, labels, empty state | ✅ 463-line page with canonical/legacy/hybrid modes |
| `UserActivityPanel` server component implemented | ✅ 252-line component |
| `UserActivityPanel` wired into `/tenant-users/[id]` page | ✅ Line 180 |
| Nav `/audit-logs` marked `LIVE` | ✅ nav.ts OPERATIONS section |
| `requireAdmin` (not `requirePlatformAdmin`) on all new endpoints/pages | ✅ |
| No regressions — Identity build clean | ✅ 0 errors |
| No regressions — CC TypeScript check (UIX-004 files) | ✅ 0 new errors |
| Report at `/analysis/UIX-004-report.md` | ✅ This document |
