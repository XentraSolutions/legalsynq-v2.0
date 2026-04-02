# UIX-004: Audit & Activity Timeline — Implementation Report

**Date**: 2026-04-01
**Feature**: Global audit log page + per-user activity panel using the canonical audit service.

---

## Summary

UIX-004 delivers two capabilities:

1. **Global Audit Log page** (`/audit-logs`) — now accessible to both PlatformAdmins and TenantAdmins. Scoped automatically per-role via JWT claims. Supports legacy, canonical, and hybrid data source modes controlled by the `AUDIT_READ_MODE` environment variable.

2. **Per-User Activity Timeline** (`/tenant-users/[id]`) — a new `UserActivityPanel` server component rendered on the user detail page. Queries the canonical audit service filtered to the target user (`targetId=userId`, `targetType=User`). Degrades gracefully when the audit service is unavailable.

---

## Deliverables

### T001 — Identity: `GET /api/admin/users/{id}/activity`
- **File**: `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`
- Handler `GetUserActivity` added; queries `AuditLogs` by `EntityId = userId`.
- `IsCrossTenantAccess` check enforces TenantAdmin boundary.
- Supports `page`, `pageSize`, `category` query params.
- Returns paged `{ items, totalCount }` matching the standard paged response shape.

### T002 — CC Types + Label Mapping
- **Files**: `apps/control-center/src/types/control-center.ts`, `apps/control-center/src/lib/api-mappers.ts`
- `UserActivityEvent` type added to `control-center.ts`.
- `AUDIT_EVENT_LABELS` dictionary maps canonical event type codes to human-readable strings.
- `mapEventLabel(eventType)` maps codes to labels, falls back to title-cased code.
- `mapUserActivityEvent()` converts `CanonicalAuditEvent` → `UserActivityEvent`.

### T003 — CC API Client
- **File**: `apps/control-center/src/lib/control-center-api.ts`
- `users.getActivity(id, { page, pageSize, category })` — proxies through gateway to Identity's new endpoint.
- `auditCanonical.listForUser({ userId, tenantId, page, pageSize })` — convenience wrapper that calls the canonical audit service with `targetId=userId` and `targetType=User`.
- Both methods never throw; they return `null` / `[]` on error so dependent UI degrades gracefully.

### T004 — CC BFF Route: User Activity
- **File**: `apps/control-center/src/app/api/identity/admin/users/[id]/activity/route.ts`
- `GET` handler calls `controlCenterServerApi.users.getActivity()`.
- Protected by `requireAdmin()` — accessible to both PlatformAdmin and TenantAdmin.
- Returns `{ items, totalCount }` as JSON, 500 on upstream error.

### T005 — Global Audit Log Page
- **File**: `apps/control-center/src/app/audit-logs/page.tsx`
- Access guard changed: `requirePlatformAdmin` → `requireAdmin`.
- Doc comment updated to reflect TenantAdmin access scope.
- Mode-driven architecture retained: `AUDIT_READ_MODE=legacy|canonical|hybrid` env var controls the data source.
- Existing canonical/hybrid filter UX (eventType, category, severity, correlationId, dateFrom, dateTo) preserved.

### T006 — UserActivityPanel Component
- **File**: `apps/control-center/src/components/users/user-activity-panel.tsx`
- Server component; fetches via `auditCanonical.listForUser()`.
- Shows compact timeline: event icon (category-colour-coded), readable label, actor, UTC timestamp, description, outcome pill, category pill, IP address if present.
- "New" badge on events within the last 24 hours.
- "View full history →" footer link to `/audit-logs?targetId=<userId>`.
- Unavailable state: amber banner instead of crash.
- Empty state: friendly notice with icon.
- **Wired into**: `apps/control-center/src/app/tenant-users/[id]/page.tsx` between `UserSecurityPanel` and the Access Control Management section.

### T007 — Nav Update
- **File**: `apps/control-center/src/lib/nav.ts`
- `/audit-logs` badge changed from `'IN PROGRESS'` → `'LIVE'`.

---

## Access Matrix

| Role          | `/audit-logs`            | `/tenant-users/[id]` (Activity Panel) |
|---------------|--------------------------|---------------------------------------|
| PlatformAdmin | Full access (all tenants) | Full access (cross-tenant)            |
| TenantAdmin   | Scoped to own tenant     | Scoped to own tenant                  |
| No role       | → redirect /login        | → redirect /login                     |

---

## Data Flow

```
UserActivityPanel (server)
  └─ auditCanonical.listForUser({ userId, tenantId })
       └─ GET /audit-service/audit/events?targetId=&targetType=User&tenantId=
            └─ Canonical Platform Audit Event Service (port 5007)

/audit-logs page (server)
  └─ auditCanonical.list({ ... }) OR audit.list({ ... })   [MODE-driven]
       └─ GET /audit-service/audit/events OR /identity/api/admin/audit
```

---

## Known Limitations / Follow-on

- The `users.getActivity()` API client method and its BFF route (`/api/identity/admin/users/[id]/activity`) are fully wired but the `UserActivityPanel` currently uses the canonical audit service directly for richer data. The identity endpoint can be used in a future "Admin Actions" sub-tab if a dedicated admin-action history view is desired.
- `AUDIT_READ_MODE` defaults to `legacy` unless overridden at deployment. In production, setting `canonical` or `hybrid` is recommended once the audit service is stable.
- `mapUserActivityEvent()` in `api-mappers.ts` is available if a typed `UserActivityEvent[]` array is ever needed (e.g., for a typed client API call instead of rendering raw `CanonicalAuditEvent`).
