# Step 24 — Cutover, Producer Integration, and UI Activation

**Date:** 2026-03-30  
**Analyst:** LegalSynq Platform Engineering  
**Status:** Implementation complete — gaps documented  
**Related files:** `step24_cutover_inventory.md`, `step24_event_matrix.md`, `step24_ui_activation_notes.md`

---

## 1. Executive Summary

### What was analyzed

The full Platform Audit/Event Service integration surface was reviewed, including:

- The canonical audit service at port 5007 (`platform-audit-event-service`) — controllers, services, repositories, DTOs, models, auth configuration, database configuration, and test suite
- The legacy Identity audit layer — `Identity.Domain.AuditLog`, the `AuditLogs` table, `GET /identity/api/admin/audit`, and every AdminEndpoint that writes a legacy log entry
- The shared `LegalSynq.AuditClient` library — HTTP client, fire-and-observe pattern, idempotency key generation, DTOs, and enums
- Identity service producer wiring — `AuthService.cs` and `AdminEndpoints.cs`
- CareConnect service wiring — `DependencyInjection.cs` and domain workflow history
- Control Center audit UI — `audit-logs/page.tsx`, `canonical-audit-table.tsx`, `AuditLogTable`, type definitions, API client, and mappers
- Tenant portal — `(platform)/activity/page.tsx`
- API Gateway routing — `appsettings.json` cluster and route configuration

### What was implemented (this step)

| Task | Status | Details |
|---|---|---|
| Gateway audit routes | Complete | 4 additive routes → `audit-cluster` (:5007) |
| `LegalSynq.AuditClient` shared library | Complete | Fire-and-observe HTTP client, DTOs, enums, idempotency |
| Identity: `user.login.succeeded` | Complete | AuthService, canonical, fire-and-observe |
| Identity: `user.role.assigned` | Complete | AdminEndpoints.AssignRole, canonical |
| Identity: `user.role.revoked` | Complete | AdminEndpoints.RevokeRole, canonical |
| CareConnect: DI scaffold | Complete | `AddAuditEventClient` wired; no events emitting yet |
| Control Center: `CanonicalAuditEvent` type | Complete | `control-center.ts` |
| Control Center: `AuditReadMode` type | Complete | `legacy` / `canonical` / `hybrid` |
| Control Center: `mapCanonicalAuditEvent` | Complete | `api-mappers.ts` |
| Control Center: `auditCanonical.list()` | Complete | `control-center-api.ts`, 13 filter params |
| Control Center: hybrid audit-logs page | Complete | AUDIT_READ_MODE env, adaptive filter UI |
| Control Center: `CanonicalAuditTable` | Complete | Severity/category/outcome badges |
| Tenant portal: activity page (Phase 1) | Complete | `requireOrg()` guard + `BlankPage` placeholder |
| Technical report | Complete | `docs/step-24-audit-cutover-report.md`, this report |

### What remains

| Gap | Priority | Blocking? |
|---|---|---|
| Audit service uses InMemory DB (dev) — events lost on restart | Critical | Yes for production |
| `user.login.failed` not emitted | High | No (functional, not audited) |
| `user.logout` not emitted | High | No |
| `user.invited`, `user.deactivated` not emitted | Medium | No |
| `tenant.created` not emitted | Medium | No |
| CareConnect events (referral, appointment) not emitted | Medium | No |
| Fund service not integrated | Low | No |
| Tenant portal activity page Phase 2 (real data) | Medium | No |
| Audit service `IngestAuth:Mode = "None"` in dev | High | No (dev only) |
| Historical data in legacy `AuditLogs` not migrated | High | No (planned) |
| `ServiceToken` empty in all appsettings | High | No (dev only) |

---

## 2. Legacy vs Canonical Audit Inventory

### 2.1 Legacy Write Paths

| Producer | Location | Table | Event Types |
|---|---|---|---|
| Identity: AdminEndpoints (historical) | `Identity.Api/Endpoints/AdminEndpoints.cs` | `AuditLogs` (Identity DB) | `AssignRole`, `RevokeRole`, `CreateUser`, `UpdateUser`, etc. via `AuditLog.Create()` |
| Identity: user management actions | Scattered throughout AdminEndpoints static methods | `AuditLogs` (Identity DB) | Various string-based action names |

> **Observation:** At time of review, legacy `AuditLog.Create()` calls still exist in AdminEndpoints for most actions. They write to the Identity DB `AuditLogs` table using the `Identity.Domain.AuditLog` entity (fields: `ActorName`, `ActorType`, `Action`, `EntityType`, `EntityId`, `MetadataJson`, `CreatedAtUtc`). These writes are NOT being removed — they remain as the safety net.

### 2.2 Legacy Read Paths

| Consumer | Endpoint | Source | Format |
|---|---|---|---|
| Control Center audit page (legacy mode) | `GET /identity/api/admin/audit` (gateway → Identity service) | `AuditLogs` table, Identity DB | `AuditLogEntry` shape: `{id, actorName, actorType, action, entityType, entityId, metadata, createdAtUtc}` |
| Control Center `AuditLogTable` component | Client-side, receives above | Same | Legacy column set |

### 2.3 Canonical Write Paths

| Producer | Library | Endpoint | Event Types |
|---|---|---|---|
| Identity: AuthService | `LegalSynq.AuditClient` | `POST /internal/audit/events` (→ :5007) | `user.login.succeeded` |
| Identity: AdminEndpoints.AssignRole | `LegalSynq.AuditClient` | `POST /internal/audit/events` (→ :5007) | `user.role.assigned` |
| Identity: AdminEndpoints.RevokeRole | `LegalSynq.AuditClient` | `POST /internal/audit/events` (→ :5007) | `user.role.revoked` |

> **All canonical writes** use the `HttpAuditEventClient` which targets `POST /internal/audit/events` directly at `http://localhost:5007` — bypassing the API gateway (correct, as this is internal M2M). Authentication is currently `IngestAuth:Mode = "None"` in development.

### 2.4 Canonical Read Paths

| Consumer | Gateway Route | Audit Service Route | Notes |
|---|---|---|---|
| Control Center (canonical/hybrid mode) | `GET /audit-service/audit/events` | `GET /audit/events` | Served by `AuditEventQueryController` |
| Direct internal | `GET /audit/events/{id}`, `/audit/entity/...`, `/audit/actor/...` | Same | Not currently surfaced in UI |

### 2.5 Active Gaps

1. **InMemory database**: The audit service `appsettings.json` sets `Database:Provider = "InMemory"`. Events ingested in one session are lost on restart. All canonical events produced in dev are ephemeral.

2. **Ingest route mismatch (potential)**: The `HttpAuditEventClient` calls `POST /internal/audit/events`. The `AuditEventIngestController` is routed at `[Route("internal/audit")]` with action `POST /internal/audit/events`. This is correct. The gateway is **not** in the ingest path (internal M2M direct to port 5007).

3. **Missing `user.login.failed`**: `AuthService.LoginAsync` throws `UnauthorizedAccessException` before the audit call is reached. A failed login is not currently recorded in the canonical store.

4. **CareConnect no-op scaffold**: `AddAuditEventClient` is registered but `IAuditEventClient` is not injected into any CareConnect service or endpoint. No events are emitted.

5. **Historical data not in canonical store**: All events before this step exist only in the legacy `AuditLogs` table. The canonical store starts empty and only receives new events.

---

## 3. Recommended Cutover Strategy

### 3.1 Phase Model

```
Phase 0 — Coexistence (CURRENT STATE)
  ├─ Legacy writes continue to AuditLogs (Identity DB)
  ├─ Canonical writes begin: login.succeeded, role.assigned, role.revoked
  ├─ Read path: AUDIT_READ_MODE=legacy (Control Center serves legacy only)
  └─ Canonical store: InMemory (ephemeral, development only)

Phase 1 — Canonical Store Stabilization
  ├─ Switch audit service Database:Provider to SQLite (dev) or PostgreSQL (prod)
  ├─ Set IngestAuth:Mode=ServiceToken, configure ServiceToken in all services
  ├─ Emit login.failed, logout, user.invited, user.deactivated
  ├─ Begin CareConnect event emission (referral.created, appointment.scheduled)
  └─ Set AUDIT_READ_MODE=hybrid on staging → canonical-first with legacy fallback

Phase 2 — Dual Read Validation
  ├─ AUDIT_READ_MODE=hybrid on staging for 2 weeks
  ├─ Validate canonical event volume matches expected admin action frequency
  ├─ Cross-check canonical events vs legacy AuditLogs entries
  ├─ Wire tenant portal activity page Phase 2 (canonical, tenant-scoped)
  └─ Fund service producer integration

Phase 3 — Canonical Primary
  ├─ AUDIT_READ_MODE=canonical on staging, then production
  ├─ Legacy AuditLogs writes downgraded to "write-but-don't-read" (WARN log on read path)
  ├─ Historical data migration: batch-import AuditLogs → canonical store via replay API
  └─ All new producers (Fund, future services) target canonical only

Phase 4 — Legacy Freeze
  ├─ AdminEndpoints: remove AuditLog.Create() calls
  ├─ Identity DB: AuditLogs table marked deprecated
  ├─ GET /identity/api/admin/audit endpoint returns 410 Gone
  └─ Legacy AuditLog domain entity archived (do not delete table yet)

Phase 5 — Legacy Retirement (deferred, requires retention review)
  ├─ After full retention window has passed with canonical coverage
  ├─ AuditLogs table dropped (with DBA approval)
  └─ Identity DB migration removes table
```

### 3.2 Feature Flag Strategy

**Current flag:** `AUDIT_READ_MODE` environment variable on the Control Center Next.js process.

| Value | Effect | When to use |
|---|---|---|
| `legacy` (default) | Reads from Identity DB `AuditLogs` only | Phase 0, fallback |
| `canonical` | Reads from audit service only | Phase 3+ |
| `hybrid` | Tries canonical, falls back to legacy on error | Phase 1-2 validation |

**Recommended additions for Phase 1:**
```json
// Identity appsettings.json (per-environment)
"AuditClient": {
  "BaseUrl": "http://localhost:5007",
  "ServiceToken": "changeme-in-production",
  "Enabled": true   // ← add: set false to disable without code change
}

// Platform Audit Service appsettings.json
"IngestAuth": {
  "Mode": "ServiceToken",   // was "None" in dev
  "ServiceTokens": ["changeme-in-production"]
}
```

### 3.3 What Should Switch First

1. **Database provider** — highest leverage, enables durable canonical store
2. **ServiceToken auth** — closes the open ingest endpoint
3. **`user.login.failed`** — high-value security event (unblocked, low risk)
4. **AUDIT_READ_MODE=hybrid on staging** — validation without production risk
5. **CareConnect events** — referral.created is highest business value

### 3.4 What Must Wait

- Historical AuditLogs migration → requires replay tool + tenant sign-off on data migration window
- `AUDIT_READ_MODE=canonical` in production → requires ≥ 2 weeks of hybrid validation
- Legacy endpoint retirement → requires retention period confirmation with compliance team
- Kafka/event bus → explicitly deferred (not required for this architecture tier)

---

## 4. Producer Integration Changes

### 4.1 Shared Helper

**Library:** `shared/audit-client/LegalSynq.AuditClient/`

| Component | File | Notes |
|---|---|---|
| Contract | `IAuditEventClient.cs` | `IngestAsync`, `BatchIngestAsync` |
| Implementation | `HttpAuditEventClient.cs` | Targets `POST /internal/audit/events` directly at BaseUrl |
| Config | `AuditClientOptions.cs` | `BaseUrl`, `ServiceToken`, `TimeoutSeconds` |
| DI extension | `AuditClientServiceCollectionExtensions.cs` | `services.AddAuditEventClient(config)` |
| Idempotency | `IdempotencyKey.cs` | `For(...)` / `ForWithTimestamp(...)` |
| DTOs | `DTOs/` | `IngestAuditEventRequest`, `BatchIngestRequest`, scope/actor/entity |
| Enums | `Enums/` | `EventCategory`, `SeverityLevel`, `ScopeType`, `ActorType`, `VisibilityScope` |

**Failure handling model:**
```csharp
// HttpAuditEventClient — all exceptions swallowed at call site
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
{
    _logger.LogWarning(ex, "AuditEvent ingest transport error: EventType={EventType}", request.EventType);
}
// Non-2xx responses are also swallowed (logged at Warning, never thrown)
```

The fire-and-observe pattern is enforced at both the library level (exception catch) and the call site (`_ = auditClient.IngestAsync(...)` — discarded Task, never awaited). This guarantees audit emission never gates the primary business operation.

### 4.2 Services Integrated

#### Identity Service

**AuthService.cs** — emitting:

| Event Type | Trigger | Category | Severity | Idempotency |
|---|---|---|---|---|
| `user.login.succeeded` | End of `LoginAsync` on success | Security | Info | `ForWithTimestamp(now, "identity-service", "user.login.succeeded", userId)` |

**AdminEndpoints.cs** — emitting:

| Event Type | Trigger | Category | Severity | Idempotency |
|---|---|---|---|---|
| `user.role.assigned` | `AssignRole` success | Administrative | Info | `For("identity-service", "user.role.assigned", userId, roleId)` |
| `user.role.revoked` | `RevokeRole` success | Administrative | Warn | `For("identity-service", "user.role.revoked", userId, roleId)` |

**Config (`appsettings.json`):**
```json
"AuditClient": {
  "BaseUrl": "http://localhost:5007",
  "ServiceToken": "",
  "SourceSystem": "identity-service",
  "SourceService": "auth-api",
  "TimeoutSeconds": 5
}
```

#### CareConnect Service

**Status:** DI registered (`AddAuditEventClient` in `CareConnect.Infrastructure/DependencyInjection.cs`), but `IAuditEventClient` not injected into any service or endpoint. Zero events emitting.

**Config (`appsettings.json`):**
```json
"AuditClient": {
  "BaseUrl": "http://localhost:5007",
  "ServiceToken": "",
  "SourceSystem": "care-connect",
  "SourceService": "referral-api",
  "TimeoutSeconds": 5
}
```

### 4.3 Event Types Now Emitting

| Event Type | Source | Volume estimate |
|---|---|---|
| `user.login.succeeded` | Identity | Every platform login |
| `user.role.assigned` | Identity Admin | Low (admin action) |
| `user.role.revoked` | Identity Admin | Low (admin action) |

### 4.4 Services Not Yet Integrated

| Service | DI Wired | Events Pending |
|---|---|---|
| CareConnect | Yes | `referral.created`, `referral.updated`, `appointment.scheduled`, `appointment.updated`, `appointment.cancelled` |
| Fund | No | `lien.created`, `fund.disbursed`, `document.uploaded` (inferred) |
| Identity (partial) | Yes | `user.login.failed`, `user.logout`, `user.invited`, `user.deactivated`, `tenant.created` |

---

## 5. Control Center Integration Changes

### 5.1 Pages / Routes / Components Changed

| File | Change Type | Notes |
|---|---|---|
| `apps/control-center/src/types/control-center.ts` | Added | `CanonicalAuditEvent` interface, `AuditReadMode` type |
| `apps/control-center/src/lib/api-client.ts` | Modified | Added `auditCanonical` cache tag |
| `apps/control-center/src/lib/api-mappers.ts` | Modified | Added `mapCanonicalAuditEvent(raw)` normaliser |
| `apps/control-center/src/lib/control-center-api.ts` | Modified | Added `auditCanonical.list(params)` method |
| `apps/control-center/src/app/audit-logs/page.tsx` | Rewritten | Full hybrid support, adaptive filter UI, source badge |
| `apps/control-center/src/components/audit-logs/canonical-audit-table.tsx` | Created | Read-only canonical event table |

### 5.2 API Integration

**Legacy path:** `GET /identity/api/admin/audit` → Identity DB `AuditLogs` table.

**Canonical path:** `GET /audit-service/audit/events` (gateway → `GET /audit/events` on audit service `AuditEventQueryController`).

**Mode selection:** `process.env['AUDIT_READ_MODE']` at server component render time (Next.js App Router, server-side).

### 5.3 Filters Supported

**Legacy mode:** search text, entity type, actor name.

**Canonical / hybrid mode:** search text, event type (free text), category (dropdown: Security / Access / Business / Administrative / Compliance / Data Change), severity (dropdown: Info / Warn / Error / Critical), correlation ID, date from / date to.

**All modes:** tenant scoping (from `getTenantContext()`), page number, page size (15).

**Not yet surfaced in UI:** organization ID, actor ID (separate from name), target type/ID (exact), source system filter, export initiation button.

### 5.4 Remaining UI Gaps

| Gap | Effort | Priority |
|---|---|---|
| Export button (→ `POST /audit/exports`) | Low | Medium |
| Event detail drawer/modal | Medium | Medium |
| Organization-level filter (for multi-org tenants) | Low | Low |
| Actor ID exact filter (separate from actorLabel) | Low | Low |
| Integrity checkpoint status indicator | Medium | Low |
| Source system filter (multi-select) | Low | Low |

---

## 6. Tenant Portal Integration Changes

### 6.1 Pages / Routes / Components Changed

| File | Change Type | Notes |
|---|---|---|
| `apps/web/src/app/(platform)/activity/page.tsx` | Created | Phase 1 placeholder |

### 6.2 API Integration (Phase 2 Plan)

Phase 2 will call `GET /audit-service/audit/events` with `tenantId` scoped to the authenticated org's tenant. The query must not be callable without the tenantId parameter — the audit service's `AuditEventQueryController` enforces scope via `QueryAuth` configuration.

**Proposed Phase 2 implementation:**
```typescript
// apps/web/src/lib/web-api.ts (to be added)
export const activityApi = {
  list: async (params: { page?: number; category?: string; dateFrom?: string; dateTo?: string }) => {
    const session = await getServerSession();
    return apiClient.get(`/audit-service/audit/events?tenantId=${session.tenantId}&...`);
  }
};
```

### 6.3 Tenant-Scope Enforcement

The `requireOrg()` guard is in place. For Phase 2, tenant isolation must be enforced at two layers:
1. The web API call must always inject `tenantId` from the server session (not from URL params)
2. The audit service `QueryAuth` must be configured to enforce `TenantId` on non-admin callers

**Current state:** `QueryAuth:Mode = "None"` in audit service development config. This must be set to `Bearer` or `ServiceToken` before enabling tenant portal access.

### 6.4 Remaining Gaps

| Gap | Priority |
|---|---|
| Phase 2 real data table (replacing BlankPage) | High |
| Tenant-safe column set (hide ipAddress, source, integrityHash) | High |
| `requireOrg()` + tenantId injection in API call | High |
| QueryAuth must be non-None before go-live | Critical |
| Pagination and filter bar | Medium |

---

## 7. Files Changed

### Gateway
```
apps/services/gateway/appsettings.json
  + audit-service-health route
  + audit-service-info route
  + audit-service-query route  (GET /audit-service/audit/events)
  + audit-service-export route
  + audit-cluster upstream → http://localhost:5007
```

### Shared Audit Client (new)
```
shared/audit-client/LegalSynq.AuditClient/
  + LegalSynq.AuditClient.csproj
  + IAuditEventClient.cs
  + HttpAuditEventClient.cs
  + AuditClientOptions.cs
  + AuditClientServiceCollectionExtensions.cs
  + IdempotencyKey.cs
  + DTOs/IngestAuditEventRequest.cs
  + DTOs/AuditEventScopeDto.cs
  + DTOs/AuditEventActorDto.cs
  + DTOs/AuditEventEntityDto.cs
  + DTOs/BatchIngestRequest.cs
  + DTOs/IngestResult.cs
  + DTOs/BatchIngestResult.cs
  + Enums/EventCategory.cs
  + Enums/SeverityLevel.cs
  + Enums/ScopeType.cs
  + Enums/ActorType.cs
  + Enums/VisibilityScope.cs
LegalSynq.sln
  ~ registered LegalSynq.AuditClient under "shared" solution folder
```

### Identity Service
```
apps/services/identity/Identity.Application/Identity.Application.csproj
  + <ProjectReference> LegalSynq.AuditClient
apps/services/identity/Identity.Infrastructure/Identity.Infrastructure.csproj
  + <ProjectReference> LegalSynq.AuditClient
apps/services/identity/Identity.Infrastructure/DependencyInjection.cs
  + services.AddAuditEventClient(configuration)
apps/services/identity/Identity.Application/Services/AuthService.cs
  + IAuditEventClient injection
  + user.login.succeeded emission (fire-and-observe)
apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs
  + IAuditEventClient injection in AssignRole, RevokeRole
  + user.role.assigned emission
  + user.role.revoked emission
apps/services/identity/Identity.Api/appsettings.json
  + AuditClient config block
```

### CareConnect Service
```
apps/services/careconnect/CareConnect.Infrastructure/CareConnect.Infrastructure.csproj
  + <ProjectReference> LegalSynq.AuditClient
apps/services/careconnect/CareConnect.Infrastructure/DependencyInjection.cs
  + services.AddAuditEventClient(configuration)
apps/services/careconnect/CareConnect.Api/appsettings.json
  + AuditClient config block
```

### Control Center
```
apps/control-center/src/types/control-center.ts
  + CanonicalAuditEvent interface
  + AuditReadMode type
apps/control-center/src/lib/api-client.ts
  + auditCanonical cache tag
apps/control-center/src/lib/api-mappers.ts
  + mapCanonicalAuditEvent() function
apps/control-center/src/lib/control-center-api.ts
  + auditCanonical.list() method
apps/control-center/src/app/audit-logs/page.tsx
  ~ rewritten: AUDIT_READ_MODE hybrid, adaptive filters, source badge
apps/control-center/src/components/audit-logs/canonical-audit-table.tsx
  + NEW: read-only canonical event table
```

### Tenant Portal
```
apps/web/src/app/(platform)/activity/page.tsx
  + NEW: requireOrg() + BlankPage placeholder
```

### Documentation
```
docs/step-24-audit-cutover-report.md  + initial report
analysis/step24_cutover_producer_ui_report.md  + this report
analysis/step24_cutover_inventory.md  + inventory detail
analysis/step24_event_matrix.md  + event type matrix
analysis/step24_ui_activation_notes.md  + UI gap tracker
```

---

## 8. Risks and Gaps

### 8.1 Critical Risks

**R1 — InMemory database (audit service):**  
`Database:Provider = "InMemory"` means every restart of the audit service clears all canonical events. Identity is now emitting real events that are immediately lost. This must be fixed before Phase 1 is meaningful.
- **Action:** Set `Database:Provider = "Sqlite"` (dev) or `"MySql"` / `"PostgreSQL"` (prod) with a real connection string.

**R2 — Open ingest endpoint:**  
`IngestAuth:Mode = "None"` accepts any caller on port 5007. In dev this is acceptable; in production this is a critical security gap.
- **Action:** Set `IngestAuth:Mode = "ServiceToken"` and configure pre-shared tokens before any non-localhost deployment.

**R3 — Open query endpoint:**  
`QueryAuth:Mode` is also `"None"` in dev. Before the tenant portal Phase 2 goes live, query auth must be enabled.
- **Action:** Set `QueryAuth:Mode = "Bearer"` and enforce `TenantId` scope for non-platform-admin callers.

### 8.2 High-Priority Gaps

**G1 — `user.login.failed` not captured:**  
`AuthService.LoginAsync` throws `UnauthorizedAccessException` before the canonical audit call. Failed logins are a critical security signal.

**Remediation (safe, additive):**
```csharp
// In AuthService.LoginAsync, wrap the existing throw:
catch (UnauthorizedAccessException)
{
    _ = _auditClient.IngestAsync(new IngestAuditEventRequest {
        EventType    = "user.login.failed",
        Category     = EventCategory.Security,
        Severity     = SeverityLevel.Warn,
        SourceSystem = "identity-service",
        Actor        = new AuditEventActorDto { Label = request.Email },
        Description  = $"Failed login attempt for '{request.Email}'",
        Outcome      = "failure",
        ...
    });
    throw; // re-throw — audit does not gate the auth response
}
```

**G2 — `user.logout` not captured:**  
Logout is handled client-side (cookie clear + redirect in Next.js BFF). The Identity service has no logout endpoint. Capturing this event requires either a BFF audit call or a dedicated identity logout endpoint that emits the event.

**G3 — ServiceToken is empty string:**  
Both Identity and CareConnect `appsettings.json` have `"ServiceToken": ""`. The `HttpAuditEventClient` skips the header when empty. This is safe in dev (auth is `None`) but will silently fail to authenticate in production.

**G4 — Historical data gap:**  
The canonical store starts with zero history. All pre-cutover AuditLogs data exists only in the Identity DB. A historical migration tool (batch replay via `POST /internal/audit/events/batch`) needs to be built and run as a one-time job per tenant.

### 8.3 Intentionally Deferred

| Item | Rationale |
|---|---|
| Fund service producer | Requires Fund domain event model review |
| Kafka / event bus | Explicitly excluded (adds infrastructure dependency) |
| Retention policy activation | Risk of premature data deletion — deferred to Step 25 |
| Legal hold UI | Not needed for initial activation |
| Integrity checkpoint UI | Nice-to-have, not blocking |
| Export download UI button | Backend exists; UI button deferred |

---

## 9. Safe Rollout Order

```
Step 1: Fix audit service database (BLOCKING — must be first)
  → Set Database:Provider=Sqlite with a file path, or configure PostgreSQL
  → Restart audit service, verify events persist across restart

Step 2: Configure service tokens
  → Generate a strong pre-shared token
  → Set IngestAuth:Mode=ServiceToken + ServiceTokens=[...]
  → Set ServiceToken in Identity and CareConnect appsettings (non-production: env var)
  → Verify identity login still emits audit event with 200 OK

Step 3: Wire user.login.failed
  → Low-risk, additive, high security value
  → Re-test AuthService tests pass

Step 4: Enable AUDIT_READ_MODE=hybrid on staging Control Center
  → Monitor that canonical events appear for login/role actions
  → Monitor that fallback to legacy works when audit service is unrestarted

Step 5: Wire CareConnect events
  → Inject IAuditEventClient into ReferralService, AppointmentService
  → Emit referral.created, appointment.scheduled
  → Verify fire-and-observe (CareConnect integration tests still pass)

Step 6: Wire remaining identity events
  → user.logout (requires BFF audit call or new logout endpoint)
  → user.invited (requires user invitation flow — review if present)
  → user.deactivated (review deactivation endpoint in AdminEndpoints)

Step 7: Enable AUDIT_READ_MODE=canonical on staging
  → 2-week validation window
  → Verify event counts are realistic

Step 8: Wire tenant portal activity page (Phase 2)
  → Enable QueryAuth:Mode=Bearer on audit service
  → Implement tenant-scoped API call in web app
  → Replace BlankPage with tenant-safe audit table

Step 9: AUDIT_READ_MODE=canonical in production
  → Pre-announce to tenant admins
  → Monitor error rates

Step 10: Historical data migration (one-time batch)
  → Build replay tool using POST /internal/audit/events/batch
  → Run per-tenant in maintenance window
  → Verify record counts post-migration

Step 11: Legacy freeze
  → Deprecation warning log on GET /identity/api/admin/audit
  → Remove AuditLog.Create() calls from AdminEndpoints
  → Set AUDIT_READ_MODE=canonical everywhere (remove legacy code path 90 days later)
```

---

## 10. Recommended Next Step

**Step 25 should be: Audit Service Durability & Security Hardening**

Specifically:

1. **Persistent storage** — Switch `Database:Provider` to SQLite for development and configure the production PostgreSQL connection string via secret injection
2. **Ingest authentication** — Enable `IngestAuth:Mode = "ServiceToken"` with per-service tokens managed as secrets
3. **Query authentication** — Enable `QueryAuth:Mode = "Bearer"` using the platform's existing JWT validation
4. **`user.login.failed` emission** — Single additive change in AuthService, high security value
5. **AUDIT_READ_MODE=hybrid on staging** — First real validation of the canonical pipeline end-to-end
6. **CareConnect event emission** — Wire referral.created and appointment.scheduled (DI already done)

This unblocks Phase 1 in the cutover plan and gives the platform its first fully durable, authenticated canonical audit trail.

---

## 11. Revalidation Notes

### Assumptions Made

1. **InMemory is intentional for dev** — The audit service `appsettings.json` uses `InMemory` provider. It is assumed this was a deliberate dev convenience choice, not an oversight.

2. **Gateway port is 5010** — The audit service routes in `appsettings.json` point to `http://localhost:5007` which is what the `audit-cluster` upstream uses. The gateway listens on 5010. This matches the documented architecture.

3. **Identity DB is MySQL on RDS** — Based on the connection string secrets and EF Core query logs, Identity uses MySQL on an AWS RDS endpoint. The `AuditLogs` table is in this same DB.

4. **CareConnect domain history tables are intentional** — `AppointmentStatusHistory` and `ReferralStatusHistory` are domain-specific history, not general audit. They should be complemented by canonical audit events, not replaced.

5. **`user.role.assigned` fire-and-observe does not write to legacy AuditLog** — Based on code review, `AssignRole` in AdminEndpoints calls `auditClient.IngestAsync` (canonical) but does NOT call `AuditLog.Create()` (legacy). This means role assignment events are canonical-only. Other admin actions may still write legacy only.

6. **Fund service has no audit integration** — No Fund service code was found with audit client references. The Fund service integration is assumed to be a future task.

### Areas Needing Architectural Review

1. **Dual-write decision** — Should AdminEndpoints continue writing to `AuditLogs` (legacy) for actions other than role changes? A brief architectural review is recommended before Phase 3 to decide if dual-write is needed during the transition.

2. **Tenant portal QueryAuth design** — When the tenant portal queries `/audit/events`, the scoping relies on the caller injecting `tenantId`. The audit service must validate that callers cannot request events outside their tenantId. This requires design of the query authorization layer in the audit service.

3. **Historical migration scope** — How far back should `AuditLogs` be migrated? The Identity `AuditLogs` table contains admin action history of unknown depth. A compliance decision is needed on the migration window.

4. **CareConnect event schema** — CareConnect's `referral` and `appointment` events do not yet have canonical event type strings defined. These must be agreed on before emission (e.g., `careconnect.referral.created` vs. `referral.created`).

### Inferences / Uncertain Areas

- The `LegacyAuditEventConfiguration.cs` file referenced in an earlier analysis note was not found in the repository at time of review. The legacy/canonical boundary is enforced by separate tables in separate services, not by a compatibility mapping layer.
- The `AUDIT_READ_MODE=hybrid` fallback (canonical → legacy) is based on catching any exception from `auditCanonical.list()`. If the canonical service returns a 4xx error (e.g., 401 Unauthorized due to auth config), the hybrid mode will silently fall back to legacy without surfacing the root cause.
- The `str()` helper in `api-mappers.ts` is used for `CanonicalAuditEvent` field normalization. Its behavior with `undefined` values should be tested when the canonical service returns sparse records.
