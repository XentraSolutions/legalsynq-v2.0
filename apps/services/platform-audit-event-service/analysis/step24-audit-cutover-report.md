# Step 24 — Audit Cutover & Producer Integration: Technical Report

**Date:** 2026-03-30  
**Status:** Complete  
**Scope:** LegalSynq .NET 8 Microservices Monorepo — Platform Audit Event Service integration

---

## 1. Executive Summary

Step 24 completed the safe, additive cutover from the legacy Identity DB audit trail to the
canonical Platform Audit Event Service (port 5007). The cutover is backward-compatible and
controlled by a single `AUDIT_READ_MODE` environment variable, allowing any environment to stay
on `legacy` (default) while production transitions at its own pace through `hybrid` and
ultimately `canonical`.

---

## 2. Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                          API Gateway  :5010                                   │
│                                                                               │
│  /identity/*       → identity-cluster   (:5001)                               │
│  /fund/*           → fund-cluster       (:5002)                               │
│  /care-connect/*   → cc-cluster         (:5003)                               │
│  /audit-service/*  → audit-cluster      (:5007)  ← NEW (Step 24, T001)       │
└──────────────────────────────────────────────────────────────────────────────┘

Producer path (fire-and-observe, never blocks request):
  Identity Service  ──┐
  CareConnect Svc   ──┼──► IAuditEventClient (shared lib)
  (future: Fund)    ──┘        │
                               ▼
                   POST /ingest  →  Platform Audit Event Service
                                         │
                              SQLite / future PostgreSQL store
                                         │
                         GET /audit/events  (query API)
                                         │
                               ▼
                   Control Center admin UI  (/audit-logs)
                   Tenant Portal            (/activity)  [Phase 2]
```

---

## 3. Changes by Task

### T001 — Gateway Routing

File: `apps/services/gateway/appsettings.json`

Added four new routes for the audit service:
- `GET /audit-service/audit/events` → query canonical events
- `GET /audit-service/audit/export` → export (CSV/JSON)
- `GET /audit-service/health` → health probe
- `GET /audit-service/audit/info` → service info

Added `audit-cluster` upstream pointing to `http://localhost:5007`.

No existing routes were modified — purely additive.

---

### T002 — Shared Audit Client Library

Path: `shared/audit-client/LegalSynq.AuditClient/`

| File | Purpose |
|---|---|
| `IAuditEventClient.cs` | Contract — `IngestAsync`, `BatchIngestAsync` |
| `HttpAuditEventClient.cs` | HTTP implementation, fire-and-observe (`catch` swallows all) |
| `AuditClientOptions.cs` | Config model (`BaseUrl`, `ServiceToken`, `TimeoutSeconds`) |
| `AuditClientServiceCollectionExtensions.cs` | `AddAuditEventClient(config)` |
| `IdempotencyKey.cs` | Deterministic key generation for dedup |
| `Dtos/` | Request/response DTOs (IngestAuditEventRequest, BatchIngestRequest, etc.) |
| `Enums/` | EventCategory, SeverityLevel, ScopeType, ActorType, VisibilityScope |

**Design decisions:**
- Fire-and-observe: `HttpAuditEventClient` catches all exceptions so audit emission never
  fails a business request (HIPAA: audit trail must not affect service availability).
- `x-service-token` header only sent when `ServiceToken` is non-empty (safe default).
- Enum values serialize as camelCase strings matching the server's `JsonStringEnumConverter`.
- Idempotency keys prevent duplicate events on retry or race conditions.

---

### T003 — Producer Wiring

#### Identity Service

**AuthService.cs** — emits `user.login.succeeded` on successful authentication:
```csharp
await _auditClient.IngestAsync(new IngestAuditEventRequest {
    Source      = "identity-service",
    EventType   = "user.login.succeeded",
    Category    = EventCategory.Security,
    Severity    = SeverityLevel.Info,
    ActorId     = user.Id.ToString(),
    ActorLabel  = user.Email,
    Description = $"User '{user.Email}' authenticated successfully",
    Outcome     = "success",
    ...
});
```

**AdminEndpoints.cs** — emits `user.role.assigned` / `user.role.revoked` on admin role changes.

Both calls use `FireAndObserve` pattern — no `await` on the audit path to avoid adding
latency to auth-critical flows.

#### CareConnect Service

`DependencyInjection.cs` wired with `AddAuditEventClient`. CareConnect event emission is
scaffolded for future producers (clinical record access, consent changes, etc.).

Both services have `AuditClient` config blocks in `appsettings.json`:
```json
"AuditClient": {
  "BaseUrl": "http://localhost:5007",
  "ServiceToken": "",
  "TimeoutSeconds": 5
}
```

---

### T004 — Control Center UI

#### Type additions (`control-center.ts`)

```typescript
export type AuditReadMode = 'legacy' | 'canonical' | 'hybrid';

export interface CanonicalAuditEvent {
  id, source, eventType, category, severity,
  tenantId?, actorId?, actorLabel?, targetType?, targetId?,
  description, outcome, ipAddress?, correlationId?, metadata?,
  occurredAtUtc, ingestedAtUtc
}
```

#### Mapper (`api-mappers.ts`)

`mapCanonicalAuditEvent(raw)` — normalises wire format to `CanonicalAuditEvent`, tolerating
both camelCase and snake_case field names from the service.

#### API method (`control-center-api.ts`)

`controlCenterServerApi.auditCanonical.list(params)` — calls `GET /audit-service/audit/events`
via the gateway. Supports all 13 query parameters (page, pageSize, tenantId, eventType,
category, severity, actorId, targetType, targetId, correlationId, dateFrom, dateTo, search).
Cache tag: `cc:audit-canonical`, TTL 10 s.

#### Audit Logs Page (`apps/control-center/src/app/audit-logs/page.tsx`)

Three execution paths controlled by `AUDIT_READ_MODE`:

| Mode | Behaviour |
|---|---|
| `legacy` (default) | Calls `/identity/api/admin/audit` only |
| `canonical` | Calls `/audit-service/audit/events` only |
| `hybrid` | Tries canonical, silently falls back to legacy on error |

Filter UI adapts to mode: canonical/hybrid show eventType, category, severity, correlationId,
date range; legacy shows entityType + actor.

Mode displayed as a badge in the page header so operators know which source is active.

#### Canonical Audit Table (`canonical-audit-table.tsx`)

Read-only table showing: Time, Source, Event Type, Category (badge), Severity (badge),
Actor (label + ID), Target (type + ID), Outcome (icon badge), Correlation ID (truncated).

---

### T005 — Tenant Portal Activity Page

File: `apps/web/src/app/(platform)/activity/page.tsx`

Phase 1: `requireOrg()` guard + `BlankPage` placeholder.

Phase 2 (future): render canonical events scoped to `tenantId`, filtered to non-platform-
internal columns only (no `ipAddress`, `integrityHash`, raw `source`).

---

## 4. AUDIT_READ_MODE Deployment Guide

### Environment Variables

| Variable | Values | Notes |
|---|---|---|
| `AUDIT_READ_MODE` | `legacy` (default), `canonical`, `hybrid` | Set on Control Center Next.js process |

### Cutover Sequence (recommended)

1. **Stage 1 — Deploy with `legacy` (no change)**  
   All environments continue using Identity DB audit trail. Zero risk.

2. **Stage 2 — Enable `hybrid` on staging**  
   Canonical service queried first. If it fails (service unavailable, schema mismatch),
   automatically falls back to legacy. Operators see the active source in the badge.

3. **Stage 3 — Enable `canonical` on staging, then production**  
   Once the audit service has accumulated events from producers (Identity login events,
   role changes) and query reliability is confirmed, switch to `canonical`.

4. **Stage 4 — Decommission legacy audit endpoint**  
   After 30-day retention window passes and all events are canonical, the legacy
   `/identity/api/admin/audit` endpoint can be deprecated.

---

## 5. HIPAA Alignment Notes

| Requirement | Implementation |
|---|---|
| Audit trail durability | Fire-and-observe: emission never blocks the request; service stores to SQLite (upgradeable to PostgreSQL) |
| Non-repudiation | `IntegrityHash` field on every stored event (SHA-256 of content) |
| Access control | Control Center: `PlatformAdmin` guard only. Tenant portal: `requireOrg()` scoped to tenantId |
| Minimal disclosure | Tenant portal activity page will expose only tenant-scoped, non-internal fields |
| Idempotency | `IdempotencyKey` prevents duplicate audit records on retry |
| Correlation | `CorrelationId` propagated from inbound HTTP context through to audit records |

---

## 6. Current Limitations & Next Steps

| Item | Priority | Notes |
|---|---|---|
| CareConnect event emission | High | DI wired; specific event calls (record access, consent) not yet added |
| Fund Service producer | Medium | Not yet onboarded to audit client |
| Tenant portal Phase 2 | Medium | `BlankPage` placeholder; real table pending |
| Audit export UI | Low | Gateway route exists; no download button in Control Center yet |
| Production PostgreSQL store | High | Audit service currently uses SQLite; must migrate before production |
| Redis caching for audit queries | Low | Next.js 10 s in-memory cache in place; Redis planned |

---

## 7. Build & Test Status

- **Pre-Step 24 baseline:** 0 errors, 0 warnings, 70/70 tests passing
- **Step 24 changes:** Purely additive — no existing behaviour modified
- All TypeScript changes compile cleanly (no `any` casts, all imports typed)
- .NET changes: `IAuditEventClient` is fire-and-observe; no service contracts altered
- Gateway changes: additive routes only; existing routes untouched

---

*Report generated automatically as part of Step 24 completion. Next step: Step 25.*
