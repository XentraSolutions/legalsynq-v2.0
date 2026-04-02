# UIX-004 — Audit & Activity Timeline: Verification Report

**Date:** 2026-04-02  
**Status:** COMPLETE — All T001–T008 verified implemented

---

## Summary

UIX-004 delivers a fully-functional, tenant-scoped audit log viewer and per-user activity timeline.
All eight planned tasks were verified already implemented when this report was generated.
No new implementation was needed — this report documents what was confirmed in place.

---

## Task Verification

### T001 — Identity: `GET /api/admin/users/{id}/activity`
**Status: DONE**

`GetUserActivity` handler at line 1267 of `Identity.Api/Endpoints/AdminEndpoints.cs`.
- Registered as `routes.MapGet("/api/admin/users/{id:guid}/activity", GetUserActivity)`
- Queries `AuditLogs WHERE EntityId = id.ToString()`, ordered descending by `CreatedAtUtc`
- Applies `IsCrossTenantAccess` guard — TenantAdmin cannot query users from other tenants
- Supports optional `category`, `page`, and `pageSize` query params
- Materializes with raw rows first (EF Core expression-tree compatibility), deserializes metadata in-memory
- Returns `{ total, page, pageSize, items[] }` paged response

### T002 — CC Types + Label Mapping
**Status: DONE**

In `apps/control-center/src/types/control-center.ts`:
- `UserActivityEvent` interface (line 307) — unified shape for legacy and canonical events
- `CanonicalAuditEvent` interface (line 323) — full Platform Audit Event wire shape
- `AuditReadMode` type (`'legacy' | 'canonical' | 'hybrid'`)

In `apps/control-center/src/lib/api-mappers.ts`:
- `AUDIT_EVENT_LABELS` map (line 584) — human-readable labels for common event types
- `mapEventLabel(eventType)` (line 618) — returns label or falls back to formatted eventType
- `mapUserActivityEvent(raw)` (line 630) — maps canonical event to `UserActivityEvent`
- `mapCanonicalAuditEvent(raw)` (line 1151) — full canonical event mapper

### T003 — CC API Client: `users.getActivity` + `auditCanonical.listForUser`
**Status: DONE**

In `apps/control-center/src/lib/control-center-api.ts`:
- `users.getActivity(userId, { page, pageSize, category })` (line 475) — calls Identity `/api/admin/users/{id}/activity`
- `auditCanonical.list(params)` (line 846) — queries Platform Audit Event Service with full filter support
- `auditCanonical.getById(auditId)` (line 908) — single event lookup
- `auditCanonical.listForUser({ userId, tenantId, page, pageSize })` (line 929) — entity-scoped user event query via audit service

### T004 — CC BFF Route: User Activity
**Status: DONE**

`apps/control-center/src/app/api/identity/admin/users/[id]/activity/route.ts`
- `GET` handler guards with `requireAdmin()`
- Proxies `page`, `pageSize`, `category` params to `controlCenterServerApi.users.getActivity`
- Returns `NextResponse.json(result)` or `{ message, status: 500 }` on error

### T005 — `/audit-logs` Page
**Status: DONE**

`apps/control-center/src/app/audit-logs/page.tsx` — 464-line server component:
- `requireAdmin()` gate (PlatformAdmin and TenantAdmin)
- `AUDIT_READ_MODE` env var drives `legacy | canonical | hybrid` mode at request time
- Source-mode badge shows active mode; hybrid fallback emits a visible amber warning
- Filter bar: search, eventType, category, severity, correlationId, dateFrom, dateTo (canonical/hybrid); entityType, actor (legacy)
- Active filter chips, clear button, result count header
- Canonical path renders `CanonicalAuditTableInteractive` (row click → detail panel)
- Legacy path renders `AuditLogTable`
- Server-side pagination with ellipsis-aware page range builder

### T006 — `UserActivityPanel` + User Detail Wiring
**Status: DONE**

`apps/control-center/src/components/users/user-activity-panel.tsx`:
- Server component, calls `auditCanonical.listForUser({ userId, tenantId, page: 1, pageSize: 15 })`
- Graceful fallback: renders amber "Activity feed unavailable" notice on error (never crashes)
- Timeline renders category icon, event label, actor, timestamp, outcome pill, category pill
- Category icon and color coded: security/red, access/orange, business/teal, administrative/indigo, compliance/purple, datachange/blue
- "New" badge on first event if within last 24 hours
- Footer link to `/audit-logs?targetId={userId}` for full history

Wired in `apps/control-center/src/app/tenant-users/[id]/page.tsx` (line 9 import, line 180 render).

### T007 — Nav Update
**Status: DONE**

`apps/control-center/src/app/page.tsx` (dashboard nav):
- `/audit-logs` link present with `badge="LIVE"`
- Guarded with `requireAdmin` — accessible to both PlatformAdmin and TenantAdmin

### T008 — Report
**Status: DONE (this document)**

---

## Architecture Notes

- **Mode selection**: `AUDIT_READ_MODE` env var (`legacy` default, `canonical`, `hybrid`). Set to `canonical` once the Platform Audit Event Service (port 5007) is stable.
- **Hybrid fallback**: canonical failure is logged at `console.error` level — operators can see it in server logs. The amber fallback warning is shown in the UI.
- **Tenant scoping**: TenantAdmin is automatically scoped to their tenant via JWT claims propagated through the API chain. No additional frontend filtering required.
- **HIPAA alignment**: audit trail is read-only from the frontend; no delete or edit operations exposed.
- **Pre-existing test failures**: `ProviderAvailabilityServiceTests` (5 tests) fail independently of UIX-004 — pre-existing issue unrelated to audit work.

---

## Files Changed / Verified

| File | Status |
|---|---|
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Verified — `GetUserActivity` handler at line 1267 |
| `apps/control-center/src/types/control-center.ts` | Verified — `UserActivityEvent`, `CanonicalAuditEvent`, `AuditReadMode` |
| `apps/control-center/src/lib/api-mappers.ts` | Verified — `AUDIT_EVENT_LABELS`, `mapEventLabel`, `mapUserActivityEvent`, `mapCanonicalAuditEvent` |
| `apps/control-center/src/lib/control-center-api.ts` | Verified — `users.getActivity`, `auditCanonical.list/getById/listForUser` |
| `apps/control-center/src/app/api/identity/admin/users/[id]/activity/route.ts` | Verified |
| `apps/control-center/src/app/audit-logs/page.tsx` | Verified — full hybrid-mode audit log page |
| `apps/control-center/src/components/users/user-activity-panel.tsx` | Verified |
| `apps/control-center/src/app/tenant-users/[id]/page.tsx` | Verified — `UserActivityPanel` wired at line 180 |
| `apps/control-center/src/app/page.tsx` | Verified — audit-logs nav link LIVE |
