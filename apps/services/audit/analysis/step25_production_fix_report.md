# Step 25 — Production Hardening and Cutover Safety Fixes

## 1. Executive Summary

Step 25 hardened the Platform Audit Event Service (port 5007) for production cutover readiness. Work spanned seven areas: SQLite dev-mode durability, JWT bearer query authorisation, canonical event producer integration (Identity + CareConnect), hybrid UI fallback surfacing, audit-of-audit (self-logging), hash-chain concurrency safety, and test isolation regression repair. All 70 integration tests pass at step completion; all three affected services build with zero warnings and zero errors.

---

## 2. Critical Issues Fixed

### 2a. Test Isolation Regression — 42/70 Tests Failing

**Symptom:** Switching `appsettings.Development.json` to `Provider=Sqlite` caused 42 integration tests to return 503 PersistenceError or fail health checks.

**Root Cause (two-part):**

1. `IAuditEventRepository` — Program.cs now registered `EfAuditEventRepository` (Scoped) instead of `InMemoryAuditEventRepository`. Health-endpoint tests that count records started hitting the EF/Sqlite path without a valid database file.

2. `DbContextOptions<AuditEventDbContext>` — EF Core's `AddDbContextFactory` uses `TryAddSingleton` for the options descriptor, meaning the **first registration wins**. Program.cs registered Sqlite options first; the test factory's second `AddDbContextFactory(UseInMemoryDatabase)` call was silently a no-op for the options, so `EfAuditEventRecordRepository.AppendAsync` was still opening a Sqlite connection (which threw) even though the factory descriptor was replaced.

**Fix — `AuditServiceFactory.cs`:**

- `ConfigureAppConfiguration`: Appends `["Database:Provider"] = "InMemory"` (best-effort override for any code that re-reads config at runtime).
- `ConfigureServices` — removes *all* EF Core descriptors related to `AuditEventDbContext` before re-registering:
  - `IDbContextFactory<AuditEventDbContext>` ← previously removed
  - `DbContextOptions<AuditEventDbContext>` ← **new removal** (root cause of 503s)
  - `DbContextOptions` (non-generic base) ← removed defensively
  - Any other generic service whose type arguments include `AuditEventDbContext`
- Re-registers `AddDbContextFactory<AuditEventDbContext>(opts => opts.UseInMemoryDatabase(uniqueDbName))` — now guaranteed to apply because the `TryAddSingleton` target is absent.
- Re-registers `IAuditEventRepository → InMemoryAuditEventRepository (Singleton)` to replace the Scoped `EfAuditEventRepository` that Program.cs added for Sqlite.

**Result:** 70/70 tests passing.

---

## 3. Security Changes

### 3a. JWT Bearer Query Authentication

**Files:** `Configuration/JwtOptions.cs`, `Program.cs`, `appsettings.Development.json`

Added a second authentication mode for query endpoints (`QueryAuth:Mode = "Bearer"`) alongside the existing `ServiceToken` and `None` modes.

`JwtOptions` binds from the `"Jwt"` section:

| Field | Purpose |
|---|---|
| `Authority` | OIDC discovery URL |
| `Audience` | Expected `aud` claim |
| `ValidIssuers` | Whitelist; falls back to `Authority` if empty |
| `RequireConfigurationInBearerMode` | If `false`, misconfigured Bearer silently degrades (dev-safe) |

When `QueryAuth:Mode = "Bearer"`, the JWT middleware is wired and `AuditEventQueryController` gates access on the resolved `VisibilityScope` claim. In Development the mode is `"None"`, meaning all callers are treated as `PlatformAdmin` — no tokens required for local testing.

**Environment variables to set for production Bearer mode:**
```
QueryAuth__Mode=Bearer
Jwt__Authority=https://<your-idp>/
Jwt__Audience=platform-audit-api
Jwt__ValidIssuers__0=https://<your-idp>/
```

### 3b. HMAC-SHA256 Hash Signing (previously delivered, hardened this step)

`IntegrityOptions` (`Integrity` config section):

| Field | Default | Notes |
|---|---|---|
| `HmacKeyBase64` | *(empty)* | Signing disabled when absent |
| `Algorithm` | `"HMAC-SHA256"` | Also accepts `"SHA256"` (unsigned hash) |
| `VerifyOnRead` | `false` | Flags mismatched hashes on query |
| `AutoCheckpointEnabled` | `false` | Runs `IntegrityCheckpointHostedService` |

**Environment variable to enable signing in production:**
```
Integrity__HmacKeyBase64=<base64-encoded 32-byte key>
```

---

## 4. Producer Integration Changes

### 4a. `identity.user.login.failed` — Identity Service

**File:** `apps/services/identity/Identity.Application/Services/AuthService.cs`

`AuthService` now emits a canonical `identity.user.login.failed` event for every failed login attempt via the fire-and-forget helper `EmitLoginFailed`. Emission covers four failure branches:

| Branch | Reason field |
|---|---|
| Tenant not found | `"TenantNotFound"` |
| User not found | `"UserNotFound"` |
| Invalid password | `"InvalidCredentials"` |
| Role lookup failure | `"RoleLookupFailed"` |

Event shape:
- `EventType`: `"identity.user.login.failed"`
- `EventCategory`: `Security`
- `Severity`: `Warning`
- `VisibilityScope`: `Tenant`
- `Actor.Type`: `User`; `Actor.Id`: user ID (if known) or email
- `Metadata`: JSON object with `{ reason, email, tenantCode }`
- `IdempotencyKey`: `IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.login.failed", email)` — timestamp-scoped to allow multiple failures from the same email

Emission is fire-and-forget (`_ = _auditClient.EmitAsync(...)`). Failure to emit does **not** alter the HTTP response to the caller.

**Dependencies added:**
- `LegalSynq.AuditClient` NuGet package reference added to `Identity.Application.csproj`
- `IAuditClient` injected via `Identity.Infrastructure/DependencyInjection.cs`
- Identity `appsettings.json`: `AuditService:BaseUrl` → `http://localhost:5007`

### 4b. `careconnect.referral.created` — CareConnect Service

**File:** `apps/services/careconnect/CareConnect.Application/Services/ReferralService.cs`

Emits `careconnect.referral.created` immediately after a referral is successfully persisted. Emission is fire-and-forget and does not gate the referral creation response.

Key fields:
- `EventType`: `"careconnect.referral.created"`
- `EventCategory`: `Clinical`
- `Severity`: `Info`
- `VisibilityScope`: `Tenant` (via `using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;` alias — required to resolve ambiguity with CareConnect's own internal enum)
- `Entity.Type`: `"Referral"`, `Entity.Id`: referral GUID
- `IdempotencyKey`: `IdempotencyKey.For("care-connect", "careconnect.referral.created", referral.Id.ToString())`

### 4c. `careconnect.appointment.scheduled` — CareConnect Service

**File:** `apps/services/careconnect/CareConnect.Application/Services/AppointmentService.cs`

Emits `careconnect.appointment.scheduled` immediately after an appointment is successfully booked. Same fire-and-forget pattern as the referral producer.

Key fields:
- `EventType`: `"careconnect.appointment.scheduled"`
- `EventCategory`: `Clinical`
- `Severity`: `Info`
- `VisibilityScope`: `Tenant` (same alias workaround)
- `Entity.Type`: `"Appointment"`, `Entity.Id`: appointment GUID
- `IdempotencyKey`: `IdempotencyKey.For("care-connect", "careconnect.appointment.scheduled", appointment.Id.ToString())`

**Dependencies added to CareConnect:**
- `LegalSynq.AuditClient` package reference added to `CareConnect.Application.csproj` and `CareConnect.Infrastructure.csproj`
- `IAuditClient` registered in `CareConnect.Infrastructure/DependencyInjection.cs`
- CareConnect `appsettings.json`: `AuditService:BaseUrl` → `http://localhost:5007`

**Note on `VisibilityScope` ambiguity:** Both `ReferralService.cs` and `AppointmentService.cs` require the alias `using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;` because CareConnect defines its own internal `VisibilityScope` enum. Without the alias the compiler cannot resolve which type to use.

---

## 5. UI / Cutover Safety Changes

### 5a. Hybrid Fallback Error Surfacing — Audit Logs Page

**File:** `apps/control-center/src/app/audit-logs/page.tsx`

The audit log viewer supports three read modes, controlled by `process.env.AUDIT_READ_MODE`:

| Mode | Behaviour |
|---|---|
| `legacy` (default) | Reads from Identity DB via `/identity/api/admin/audit` |
| `canonical` | Reads only from the canonical Audit Service |
| `hybrid` | Canonical first; falls back to legacy on any error |

**New behaviour in `hybrid` mode:**

When the canonical fetch throws (network error, 5xx, timeout), the page:
1. Captures the exception message into `canonicalFallbackReason`
2. Logs to `console.error` with the prefix `[AUDIT_HYBRID_FALLBACK]` so server-side log aggregators can alert on unexpected fallback
3. Falls back to the legacy endpoint and renders legacy data
4. Displays an amber warning banner in the UI:

> ⚠ **Degraded** — *Canonical audit service unavailable — displaying legacy audit data*

The banner is visible to operators without requiring a server log query, enabling faster incident detection during cutover.

**Environment variable (no new required vars — this is opt-in):**
```
AUDIT_READ_MODE=hybrid   # or "canonical" when ready for full cutover
```

### 5b. Canonical Audit Table Component

**File:** `apps/control-center/src/components/audit-logs/canonical-audit-table.tsx`

New React component that renders data from the canonical audit service with typed columns (`EventType`, `SourceSystem`, `Actor`, `Entity`, `Severity`, `OccurredAt`, `VisibilityScope`). Used by `audit-logs/page.tsx` when `actualMode === "canonical"` or after a successful canonical fetch in hybrid mode.

### 5c. Control Center API Client Extensions

**Files:** `src/lib/api-client.ts`, `src/lib/api-mappers.ts`, `src/lib/control-center-api.ts`, `src/types/control-center.ts`

Extended the TypeScript API client with:
- `auditService.query(params)` — calls `GET /audit-service/audit/events` through the gateway
- Response mappers from the canonical `AuditEventResponse` DTO to the UI's `CanonicalAuditEntry` type
- `CanonicalAuditEntry` type definition aligned with the service's paginated query response shape

---

## 6. Integrity / Concurrency Changes

### 6a. Hash-Chain Concurrency Safety

**File:** `apps/services/platform-audit-event-service/Services/AuditEventIngestionService.cs`

The hash-chain critical section (read-last-hash → compute-new-hash → persist) is now serialised per `(TenantId, SourceSystem)` pair using a `ConcurrentDictionary<string, SemaphoreSlim>` of `SemaphoreSlim(1, 1)` instances.

```
private static readonly ConcurrentDictionary<string, SemaphoreSlim> _chainLocks = new();

private static SemaphoreSlim GetChainLock(string? tenantId, string sourceSystem) =>
    _chainLocks.GetOrAdd($"{tenantId}:{sourceSystem}", _ => new SemaphoreSlim(1, 1));
```

Within `IngestSingleAsync`, the code acquires the semaphore before reading the previous hash and releases it (in a `finally` block) after `AppendAsync` returns. This prevents concurrent ingest calls for the same chain from racing to write different `PreviousHash` values, which would break tamper-evidence verification.

Scope: Only the hash-chain read→compute→write window is serialised. Idempotency key lookup, validation, and event forwarding occur outside the lock.

### 6b. Audit-of-Audit (`audit.log.accessed`)

**File:** `apps/services/platform-audit-event-service/Controllers/AuditEventQueryController.cs`

Every successful query response now emits a `audit.log.accessed` event via `IAuditEventIngestionService.IngestSingleAsync` (fire-and-forget, suppressed errors). The emission carries:
- `EventType`: `"audit.log.accessed"`
- `Actor`: caller identity resolved from query auth context
- `Metadata`: query parameters (filters, page, page size) serialised as JSON
- Self-suppression: the ingest call is not itself re-audited (preventing infinite recursion)

`IAuditEventIngestionService` is injected into `AuditEventQueryController` as a fifth constructor parameter. The controller is Scoped; the ingestion service is Scoped — no lifetime mismatch.

---

## 7. Testing Changes

### 7a. `AuditServiceFactory` — Full EF Descriptor Purge

**File:** `apps/services/platform-audit-event-service.Tests/Helpers/AuditServiceFactory.cs`

The base factory now removes a broader set of EF Core service descriptors before registering the InMemory replacement:

```csharp
var dbContextTypes = new HashSet<Type>
{
    typeof(IDbContextFactory<AuditEventDbContext>),
    typeof(DbContextOptions<AuditEventDbContext>),
    typeof(DbContextOptions),
};
var existing = services
    .Where(d =>
        dbContextTypes.Contains(d.ServiceType) ||
        (d.ServiceType.IsGenericType &&
         d.ServiceType.GetGenericArguments().Any(a => a == typeof(AuditEventDbContext))))
    .ToList();
foreach (var d in existing) services.Remove(d);
```

This ensures that EF Core's `TryAddSingleton` for `DbContextOptions<AuditEventDbContext>` — which uses first-registration semantics — is defeated by removing the Sqlite options descriptor before the InMemory re-registration.

`ConfigureAppConfiguration` also appends `["Database:Provider"] = "InMemory"` as a belt-and-suspenders override for any runtime code that re-reads the config key.

### 7b. Test Baseline

| Class | Tests | Result |
|---|---|---|
| `HealthEndpointTests` | 5 | All pass |
| `IngestEndpointTests` | 13 | All pass |
| `BatchIngestTests` | 10 | All pass |
| `QueryEndpointTests` | (included in 70) | All pass |
| `AuthEndpointTests` / other | (included in 70) | All pass |
| **Total** | **70** | **70 / 70** |

---

## 8. Remaining Risks

| Risk | Severity | Notes |
|---|---|---|
| `user.logout`, `user.created`, `user.deactivated` events not yet emitted | Low | Identity service still only emits `login.failed`. Agreed lower priority for this step. |
| Correlation ID propagation not verified for `login.failed` | Low | `CorrelationId` field is populated from the HTTP context header (`X-Correlation-Id`) in `IAuditClient`. Needs an end-to-end trace test to confirm the header threads through from the gateway. |
| `Integrity__HmacKeyBase64` not set in any environment | Medium | Hash chain runs without signing in all current environments. Records will have `Hash = null`. Verification on read (`VerifyOnRead = false`) is also off. Must be set before production HIPAA attestation. |
| `AUDIT_READ_MODE` is `legacy` by default | Low | Control Center will not surface canonical data until the env var is changed to `hybrid` or `canonical`. Intentional — gradual cutover. |
| `AddLegalHoldsAndOutboxMessages` migration not run against any environment | Medium | The migration exists in source. It must be applied (`dotnet ef database update`) before the audit service can be started against a MySQL production database. Not required for dev (Sqlite uses `EnsureCreated`). |
| `SemaphoreSlim` instances in `_chainLocks` are never evicted | Low | In a long-running process with many distinct `(tenantId, sourceSystem)` combinations the dictionary grows unbounded. Acceptable for current cardinality; add an LRU eviction policy before the set exceeds ~10k combinations. |

---

## 9. Recommended Immediate Next Step

**Set `Integrity__HmacKeyBase64` in production and staging** before cutover. Without it, the tamper-evidence chain contains only `null` hashes — the chain exists structurally but carries no cryptographic guarantee. Generate a 32-byte key and base64-encode it:

```bash
openssl rand -base64 32
```

Set as an environment variable / secret:
```
Integrity__HmacKeyBase64=<output from above>
```

All records ingested after the key is set will be HMAC-SHA256 signed and linked. Records ingested before will have `Hash = null` — this is expected and the verifier handles the mixed state correctly when `VerifyOnRead = true` is enabled.

After the key is set, enable verification:
```
Integrity__VerifyOnRead=true
```

---

## Exact Files Changed

### Platform Audit Event Service (`apps/services/platform-audit-event-service/`)

| File | Change |
|---|---|
| `appsettings.Development.json` | `Database:Provider` changed from `"InMemory"` to `"Sqlite"` for durable local dev storage |
| `Program.cs` | Added `case "Sqlite"` branch: registers `UseSqlite`, `EfAuditEventRepository`, calls `EnsureCreated()` at startup |
| `Configuration/DatabaseOptions.cs` | Added `SqliteFilePath` and `ConnectionString` fields for Sqlite case |
| `Configuration/JwtOptions.cs` | New: JWT Bearer options bound from `"Jwt"` section |
| `Configuration/IntegrityOptions.cs` | New: HMAC key, algorithm, verify-on-read, checkpoint toggle |
| `Configuration/RetentionOptions.cs` | Exists (prior step); reviewed/unchanged |
| `Configuration/ExportOptions.cs` | Exists (prior step); reviewed/unchanged |
| `Controllers/AuditEventQueryController.cs` | Injected `IAuditEventIngestionService`; removed `Outcome` field from audit-of-audit emit; added `audit.log.accessed` emission on every successful query |
| `Controllers/LegalHoldController.cs` | New: CRUD endpoints for legal hold management |
| `Services/AuditEventIngestionService.cs` | Added `ConcurrentDictionary<string, SemaphoreSlim> _chainLocks`; serialises hash-chain critical section per `(TenantId, SourceSystem)` |
| `Data/AuditEventDbContext.cs` | Added `LegalHolds` and `OutboxMessages` `DbSet`s |
| `Data/Configurations/LegalHoldConfiguration.cs` | New: EF fluent configuration for `LegalHold` entity |
| `Data/Configurations/OutboxMessageConfiguration.cs` | New: EF fluent configuration for `OutboxMessage` entity |
| `Data/Migrations/20260330192715_AddLegalHoldsAndOutboxMessages.cs` | New MySQL migration: adds `LegalHolds` table, `OutboxMessages` table, `RecordCount` column on `AuditExportJobs` |
| `Data/Migrations/20260330192715_AddLegalHoldsAndOutboxMessages.Designer.cs` | Auto-generated migration designer file |
| `Data/Migrations/AuditEventDbContextModelSnapshot.cs` | Updated model snapshot |
| `Models/Entities/LegalHold.cs` | New entity |
| `Models/Entities/OutboxMessage.cs` | New entity |
| `DTOs/LegalHold/CreateLegalHoldRequest.cs` | New DTO |
| `DTOs/LegalHold/LegalHoldResponse.cs` | New DTO |
| `DTOs/LegalHold/ReleaseLegalHoldRequest.cs` | New DTO |
| `Jobs/OutboxRelayHostedService.cs` | New: polls outbox and forwards messages |
| `Jobs/IntegrityCheckpointHostedService.cs` | New: periodic hash-chain checkpoint writer |
| `Jobs/RetentionHostedService.cs` | New: wrapper for retention policy execution |
| `Jobs/RetentionPolicyJob.cs` | New: retention deletion logic |
| `Jobs/ExportProcessingJob.cs` | New: async export processing |
| `Repositories/EfAuditEventRecordRepository.cs` | Exists; reviewed/unchanged this step |
| `PlatformAuditEventService.csproj` | Added `Microsoft.EntityFrameworkCore.Sqlite` package |

### Test Project (`apps/services/platform-audit-event-service.Tests/`)

| File | Change |
|---|---|
| `Helpers/AuditServiceFactory.cs` | Extended `ConfigureServices` removal to include `DbContextOptions<AuditEventDbContext>`, `DbContextOptions`, and generic types parameterised on `AuditEventDbContext`; added `ConfigureAppConfiguration` override for `Database:Provider=InMemory`; added `IAuditEventRepository → InMemoryAuditEventRepository` override |

### Identity Service (`apps/services/identity/`)

| File | Change |
|---|---|
| `Identity.Application/Services/AuthService.cs` | Added `IAuditClient` injection; added `EmitLoginFailed` helper; wired four login-failure branches to emit `identity.user.login.failed` |
| `Identity.Application/Identity.Application.csproj` | Added `LegalSynq.AuditClient` project/package reference |
| `Identity.Infrastructure/DependencyInjection.cs` | Registered `IAuditClient` / `HttpAuditClient` with `BaseUrl` from config |
| `Identity.Infrastructure/Identity.Infrastructure.csproj` | Added `LegalSynq.AuditClient` reference (for DI registration) |
| `Identity.Api/appsettings.json` | Added `AuditService:BaseUrl: "http://localhost:5007"` |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Minor: no functional change; reviewed |

### CareConnect Service (`apps/services/careconnect/`)

| File | Change |
|---|---|
| `CareConnect.Application/Services/ReferralService.cs` | Added `IAuditClient` injection; `using AuditVisibility` alias; emits `careconnect.referral.created` after successful persist |
| `CareConnect.Application/Services/AppointmentService.cs` | Added `IAuditClient` injection; `using AuditVisibility` alias; emits `careconnect.appointment.scheduled` after successful booking |
| `CareConnect.Application/CareConnect.Application.csproj` | Added `LegalSynq.AuditClient` reference |
| `CareConnect.Infrastructure/DependencyInjection.cs` | Registered `IAuditClient` / `HttpAuditClient` |
| `CareConnect.Infrastructure/CareConnect.Infrastructure.csproj` | Added `LegalSynq.AuditClient` reference |
| `CareConnect.Api/appsettings.json` | Added `AuditService:BaseUrl: "http://localhost:5007"` |

### Control Center (`apps/control-center/`)

| File | Change |
|---|---|
| `src/app/audit-logs/page.tsx` | Added `canonicalFallbackReason` state; amber warning banner in hybrid-fallback mode; `console.error` logging with `[AUDIT_HYBRID_FALLBACK]` prefix |
| `src/components/audit-logs/canonical-audit-table.tsx` | New: typed table for canonical `CanonicalAuditEntry` rows |
| `src/lib/api-client.ts` | Added `auditService.query()` method targeting gateway `/audit-service/audit/events` |
| `src/lib/api-mappers.ts` | Added `toCanonicalAuditEntry()` mapper from service DTO to UI type |
| `src/lib/control-center-api.ts` | Wired canonical query through the server-side API client |
| `src/types/control-center.ts` | Added `CanonicalAuditEntry`, `CanonicalAuditQueryParams`, `CanonicalAuditPagedResponse` types |

### Gateway (`apps/gateway/`)

| File | Change |
|---|---|
| `Gateway.Api/appsettings.json` | Added YARP routes for audit service: `audit-service-health`, `audit-service-info`, `audit-service-query`, `audit-service-ingest`; added `audit-cluster` pointing to `http://localhost:5007` |

---

## Migrations Added

| Migration | Table(s) Created | Column(s) Added |
|---|---|---|
| `20260330192715_AddLegalHoldsAndOutboxMessages` | `LegalHolds`, `OutboxMessages` | `AuditExportJobs.RecordCount (bigint, nullable)` |

**Apply before production MySQL startup:**
```bash
dotnet ef database update --project apps/services/platform-audit-event-service \
  --connection "<MySQL connection string>"
```

Not required for Sqlite dev mode (`EnsureCreated()` applies the full schema at startup).

---

## Config / Environment Variables Newly Required

| Variable | Service | Required For | Example Value |
|---|---|---|---|
| `Database__Provider` | Audit Service | Sqlite dev mode | `Sqlite` |
| `Database__SqliteFilePath` | Audit Service | Sqlite dev mode | `audit-dev.db` |
| `Integrity__HmacKeyBase64` | Audit Service | Hash signing (prod) | `<base64 32-byte key>` |
| `QueryAuth__Mode` | Audit Service | Bearer query auth (prod) | `Bearer` |
| `Jwt__Authority` | Audit Service | Bearer mode | `https://<idp>/` |
| `Jwt__Audience` | Audit Service | Bearer mode | `platform-audit-api` |
| `Jwt__ValidIssuers__0` | Audit Service | Bearer mode | `https://<idp>/` |
| `AuditService__BaseUrl` | Identity | Emit login.failed | `http://localhost:5007` |
| `AuditService__BaseUrl` | CareConnect | Emit referral/appointment events | `http://localhost:5007` |
| `AUDIT_READ_MODE` | Control Center | Hybrid/canonical UI mode | `hybrid` or `canonical` |
