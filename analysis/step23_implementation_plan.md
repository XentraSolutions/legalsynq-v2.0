# Step 23: Production Enablement and Rollout Plan
## Platform Audit/Event Service

**Author:** Principal Engineer / Staff Architect  
**Date:** 2026-03-30  
**Service:** `apps/services/platform-audit-event-service`  
**Test project:** `apps/services/platform-audit-event-service.Tests`  
**Status at start of Step 23:** Structurally complete. Operationally not production-safe.

---

## 1. Executive Summary

The Platform Audit/Event Service has completed a thorough 22-step build cycle that produced:

- A fully implemented canonical ingest pipeline with integrity chain signing
- A Bearer-capable query authorization layer with scope constraints
- An integrity checkpoint service with HMAC-SHA256 aggregate hashing
- A retention policy service (evaluation-only) with configurable tier thresholds
- An export pipeline (sync, file-system gated) and forwarding pipeline (filter/map/publish)
- 48 passing integration tests covering ingest, query, batch, export status, and auth
- A production hardening pass (security headers, startup assertions, CORS, structured logging)
- A 40-item production readiness checklist

**The service is structurally sound and ready for controlled production adoption.**

Step 23's purpose is to produce a fully actionable delivery plan that takes the service from its current state (all critical abstractions present, several key paths still in placeholder or incomplete-wiring state) to a genuine production deployment with no HIPAA-violating gaps.

**Three critical blockers identified through direct code audit that must be resolved before any production traffic is routed to this service:**

1. **`AddJwtBearer()` is not registered.** `Program.cs` never calls `builder.Services.AddAuthentication().AddJwtBearer()`. `ClaimsCallerResolver` reads from `HttpContext.User.Identity.IsAuthenticated`, which is only populated by JWT middleware. In `Mode=Bearer`, all requests will be rejected 401 regardless of token validity. This is the single highest-priority gap.

2. **`LegalHoldEnabled=true` in `appsettings.Production.json` has zero runtime effect.** `RetentionService` explicitly documents it as a v1 flag with no implementation. `DryRun=false` is also set in production config, meaning when a real archival provider is wired and `JobEnabled=true`, records will be evaluated for deletion with no legal hold protection.

3. **Neither background job is scheduled.** `RetentionPolicyJob` and `IntegrityCheckpointJob` are registered as `Transient` services only. No `IHostedService`, `BackgroundService`, or Quartz.NET trigger is wired. The cron expressions in `RetentionOptions.JobCronUtc` are informational only. These jobs will never run autonomously.

---

## 2. Current-State Snapshot

| Area | Current State | Step 23 Action Needed |
|---|---|---|
| **Ingest auth** | Production-ready. `ServiceToken` mode fully implemented, tested. `IngestAuthMiddleware` correctly gates `/internal/audit`. Source allowlist supported. | Wire service tokens via env vars in deployment. Validate AllowedSources in staging. |
| **Query auth** | Structurally complete but critically broken in Bearer mode. `ClaimsCallerResolver` is correct, but `AddAuthentication().AddJwtBearer()` is never registered in `Program.cs`. Mode=Bearer will 401 every request. | Register JWT middleware in Program.cs. Add JWT configuration options class. Write Bearer-mode integration tests. |
| **Query APIs** | Production-ready. All 5 controller surfaces implemented. Scoped constraints enforced. Pagination correct. | Load test. Add cursor/keyset pagination for high-volume stores. |
| **Exports** | `AuditExportService` is synchronous and filesystem-only. `Export:Provider=None` in base config disables endpoints with 503. No cloud provider implemented. | Wire async background job for large exports. Implement S3 or AzureBlob provider. Export polling endpoint already exists. |
| **Integrity checkpoints** | `IIntegrityCheckpointService.GenerateAsync()` fully implemented and correct. HTTP endpoint functional. `IntegrityCheckpointJob` is a placeholder — requires caller to supply window bounds; no auto-window calculation. | Implement auto-window calculation in job. Register as `BackgroundService` or Quartz trigger. Write verification pass. |
| **Retention/archival** | `RetentionService.EvaluateAsync()` is read-only classification only. `RetentionPolicyJob` logs but never archives or deletes. `IArchivalProvider = NoOpArchivalProvider`. No archival execution. | Implement `LocalArchivalProvider` or `S3ArchivalProvider`. Wire archival into job. Implement legal hold check before Phase 2 activates. |
| **Forwarding** | Full filter pipeline implemented in `NoOpAuditEventForwarder`. `IIntegrationEventPublisher` interface is clean and ready for extension. No real broker. | Implement `RabbitMqIntegrationEventPublisher` or `OutboxIntegrationEventPublisher`. Wire via DI switch on `EventForwarding:BrokerType`. |
| **Producer adoption** | Docs and example contracts produced (Steps 19–20). No producers are actively integrated yet. | Run structured onboarding for Identity, Fund, CareConnect services. |
| **UI consumption** | UI integration docs delivered (Step 20). No live UI queries against real data. | Wire Control Center "Audit Logs" stub page to real query endpoint. |
| **Testing/QA** | 48 passing tests. Gaps: no Bearer-mode query auth test; no retention tests; no checkpoint service tests; no forwarding filter tests; no export streaming tests; no failure-path (DB-down, chaos) tests. | Systematic coverage expansion per Epic G. |
| **Operations/observability** | Serilog structured logging, CorrelationId middleware, `/health` + `/health/detail` endpoints, security response headers. No alerting, no log aggregation, no distributed tracing wired. | Wire log shipper. Add alert rules. Instrument with OpenTelemetry traces. |

---

## 3. Epic Breakdown

### Epic A: Production Query Authentication Enablement

**Goal:** Make `QueryAuth:Mode=Bearer` actually work end-to-end.

**Why it matters:** Without `AddJwtBearer()` in `Program.cs`, the entire query surface is effectively locked (401 for all valid-token callers) the moment `Mode=Bearer` is activated. This is the single highest-priority functional gap.

**In-scope components/files:**
- `Program.cs` — add `builder.Services.AddAuthentication().AddJwtBearer()`
- New `Configuration/JwtOptions.cs` — Authority, Audience, ValidIssuers
- `appsettings.Production.json` — JWT options block (values injected via env vars)
- `appsettings.Development.json` — dev JWT override or anonymous bypass documentation
- `ClaimsCallerResolver.cs` — verify claim extraction aligns with token claims
- `QueryAuthMiddleware.cs` — no changes needed, but verify `HttpContext.User` is populated after JWT middleware
- Tests: `AuthorizationTests.cs` — extend; new `QueryAuthBearerTests.cs`

**Out of scope:** Identity provider setup, token issuance, user management.

**Exit criteria:**
- `GET /audit/events` with a valid JWT returns 200 with correctly-scoped results
- `GET /audit/events` with no token returns 401
- `GET /audit/events` with a valid tenant-user JWT cannot see another tenant's records
- All existing 48 tests still pass

---

### Epic B: Asynchronous Export Processing

**Goal:** Enable export for production use with a real storage provider and async job processing.

**Why it matters:** The current export pipeline is synchronous and writes to the local filesystem (lost on pod restart). Large compliance exports over 100K records would block the HTTP request thread. `Export:Provider=None` disables endpoints entirely.

**In-scope components/files:**
- `Services/Export/IExportStorageProvider.cs` — interface already correct
- `Services/Export/LocalExportStorageProvider.cs` — reference implementation
- New `Services/Export/S3ExportStorageProvider.cs` (or AzureBlob) — real cloud provider
- `Services/AuditExportService.cs` — move large exports to background processing
- New `Jobs/ExportProcessingJob.cs` — hosted background worker polling `AuditExportJob` queue
- `Controllers/AuditExportController.cs` — already has polling endpoint; wire async pattern
- `appsettings.Production.json` — set `Export:Provider`, bucket name via env vars
- `Program.cs` — register real provider; register `ExportProcessingJob` as `IHostedService`

**Out of scope:** Export billing, SLA enforcement, multi-tenant quota management.

**Exit criteria:**
- `POST /audit/export` creates a job and returns `ExportJobId`
- `GET /audit/export/{jobId}/status` returns `Pending → Processing → Completed`
- Exported file is retrievable from cloud storage (not lost on pod restart)
- Export of 50K records completes without timing out the HTTP request

---

### Epic C: Integrity Verification and Scheduled Checkpoints

**Goal:** Make integrity checkpoint generation automatic and add a verification pass.

**Why it matters:** HIPAA requires tamper-evident audit logs. The checkpoint service is fully implemented but never runs autonomously. There is also no automated verification step to detect gaps between checkpoint windows.

**In-scope components/files:**
- `Jobs/IntegrityCheckpointJob.cs` — add auto-window calculation logic
- New `Jobs/IntegrityCheckpointHostedService.cs` — wraps job as `BackgroundService` with configurable cron
- `Configuration/IntegrityOptions.cs` — add `CheckpointJobEnabled`, `CheckpointCronUtc`
- `Services/IntegrityCheckpointService.cs` — add `VerifyAsync(from, to)` — verify that checkpoint windows are contiguous and aggregate hashes match
- `Controllers/IntegrityCheckpointController.cs` — expose `GET /audit/integrity/gaps` endpoint
- `Program.cs` — register `IntegrityCheckpointHostedService`
- `appsettings.Production.json` — set `Integrity:CheckpointJobEnabled=true`

**Out of scope:** Cross-service checkpoint federation, real-time tamper alerting.

**Exit criteria:**
- `IntegrityCheckpointHostedService` runs on schedule without manual invocation
- Two consecutive checkpoint windows share contiguous boundary timestamps
- Deleting a record and running checkpoint verification produces a detectable hash mismatch
- Gap detection endpoint returns gaps when a window is skipped

---

### Epic D: Retention Execution and Archival Provider

**Goal:** Activate real retention enforcement with a safe archival provider.

**Why it matters:** `Retention:DryRun=false` is already set in `appsettings.Production.json`. Once the job is scheduled and legal hold is implemented, records will be evaluated for deletion. Without a real archival provider, records older than the retention window will be unrecoverable.

**In-scope components/files:**
- `Services/Archival/IArchivalProvider.cs` — interface is correct
- New `Services/Archival/LocalArchivalProvider.cs` — archive to local/network path (staging validation)
- New `Services/Archival/S3ArchivalProvider.cs` — production archival to S3/compatible store
- `Jobs/RetentionPolicyJob.cs` — implement Phase 2 (archival execution, deletion) gated on `DryRun=false` and `ArchiveBeforeDelete=true`
- New `Jobs/RetentionHostedService.cs` — `BackgroundService` triggered by `Retention:JobCronUtc`
- `Program.cs` — register `RetentionHostedService` and real archival provider
- `appsettings.Production.json` — set archival config

**Out of scope:** Archival retrieval / restore API (separate concern), per-tenant archival buckets.

**Exit criteria:**
- `RetentionHostedService` runs on schedule
- Records older than `DefaultRetentionDays` are archived to the configured provider
- Records protected by legal hold (Epic E) are skipped
- `DryRun=true` mode still produces correct evaluation log with zero side effects
- Archived file format is JSON Lines (NDJSON) for compatibility with query tooling

---

### Epic E: Legal Hold and Compliance Safeguards

**Goal:** Implement the legal hold entity and pre-check so that records under litigation hold cannot be deleted by the retention pipeline.

**Why it matters:** `LegalHoldEnabled=true` in `appsettings.Production.json` is a config trap — it signals intent but has no runtime enforcement. Enabling destructive retention without legal hold protection is a HIPAA compliance violation.

**In-scope components/files:**
- New `Models/Entities/LegalHold.cs` — `HoldId`, `AuditId`, `HeldByUserId`, `HeldAtUtc`, `ReleasedAtUtc`, `LegalAuthority`, `Notes`
- `Data/AuditEventDbContext.cs` — add `DbSet<LegalHold> LegalHolds`
- New `Data/Configurations/LegalHoldConfiguration.cs` — EF fluent mapping
- New `Data/Migrations/` — migration for `LegalHolds` table
- New `Repositories/ILegalHoldRepository.cs` + `EfLegalHoldRepository.cs`
- `Services/RetentionService.cs` — add legal hold pre-check in `ClassifyTier()`
- New `Controllers/LegalHoldController.cs` — `POST /audit/legal-hold` (platform admin only), `DELETE /audit/legal-hold/{holdId}` (release), `GET /audit/legal-hold?auditId=`
- `Configuration/RetentionOptions.cs` — `LegalHoldEnabled` now gates runtime enforcement

**Out of scope:** Hold workflow approval chains, court order document storage, hold auditing (recursive).

**Exit criteria:**
- A record with an active hold is classified as `LegalHold` tier, not `Cold`
- Retention job skips legal-hold records even when `DryRun=false`
- `LegalHoldEnabled=false` bypasses the check (backward-compatible)
- Hold creation and release are themselves audit-logged via the ingest pipeline

---

### Epic F: Event Forwarding Productionization

**Goal:** Replace `NoOpIntegrationEventPublisher` with a real broker publisher.

**Why it matters:** Downstream services (notifications, compliance dashboards, monitoring pipelines) cannot receive real-time audit signals until a real broker is wired. The forwarding abstraction is complete; only the broker implementation is missing.

**In-scope components/files:**
- `Services/Forwarding/IIntegrationEventPublisher.cs` — interface is correct and stable
- `Services/Forwarding/NoOpIntegrationEventPublisher.cs` — reference for interface shape
- New `Services/Forwarding/RabbitMqIntegrationEventPublisher.cs` — RabbitMQ implementation
  - OR: New `Services/Forwarding/OutboxIntegrationEventPublisher.cs` — transactional outbox pattern (safer for HIPAA durability)
- `Configuration/EventForwardingOptions.cs` — add connection string, exchange, routing options
- `Program.cs` — DI switch on `EventForwarding:BrokerType`
- `appsettings.Production.json` — `EventForwarding:BrokerType`, `EventForwarding:ConnectionString` (env var)

**Out of scope:** Consumer implementations, dead-letter queue handling, broker provisioning.

**Exit criteria:**
- An ingested event with `EventForwarding:Enabled=true` and matching filters produces a message on the configured broker exchange
- `EventForwarding:Enabled=false` produces no broker I/O
- Publisher failure is non-fatal (already enforced in `AuditEventIngestionService.cs` line 360–370)
- Publisher handles connection loss with configurable retry

**Recommendation:** Implement the outbox pattern first (`OutboxIntegrationEventPublisher`) — it writes a relay record to the same MySQL transaction as the audit event, so no audit event can be published without a corresponding durable record. This is the most HIPAA-appropriate approach.

---

### Epic G: Test Expansion and Failure-Mode Validation

**Goal:** Close the material coverage gaps identified in the Step 22 test project.

**Why it matters:** The current test suite validates the happy path for ingest, query, batch, and auth. It does not cover query authorization with real JWT tokens, retention logic, integrity checkpoint generation, forwarding filter behavior, export streaming, or any failure path.

**In-scope files:**
- `Tests/IntegrationTests/QueryAuthBearerTests.cs` — new file, Bearer mode with mock JWT
- `Tests/IntegrationTests/RetentionTests.cs` — new file, EvaluateAsync classification scenarios
- `Tests/IntegrationTests/IntegrityCheckpointTests.cs` — new file, generate + verify + gap detection
- `Tests/IntegrationTests/ForwardingFilterTests.cs` — new file, filter rules per `NoOpAuditEventForwarder`
- `Tests/IntegrationTests/ExportEndpointTests.cs` — extend existing file, full lifecycle
- `Tests/IntegrationTests/LegalHoldTests.cs` — new file (after Epic E)
- `Tests/IntegrationTests/ChaosTests.cs` — DB failure, partial batch, concurrent duplicate keys

**Out of scope:** Load tests, performance benchmarks (separate concern).

**Exit criteria:**
- Bearer-mode: valid token → scoped data; no token → 401; cross-tenant → 403
- Retention: record in hot/warm/cold/indefinite/legal-hold classified correctly
- Integrity: generate + verify round-trip with SHA-256 and HMAC-SHA256
- Forwarding: all 5 filter conditions covered (master off, replay, category, prefix, severity)
- Export: Pending → Completed lifecycle tested end-to-end
- Chaos: single DB failure → correct 500 ApiResponse envelope; concurrent duplicate → 409 or 201 (idempotent)

---

### Epic H: Deployment / Rollout / Operational Readiness

**Goal:** Ensure the service can be safely deployed and operated in production with full observability.

**Why it matters:** The service has no log shipper, no distributed tracing, no alerting rules, no k8s deployment manifests, and no DB migration pre-deploy procedure documented.

**In-scope components/files:**
- `Docs/production-readiness-checklist.md` — update with Epic A–G completion status
- New `Docs/runbook.md` — startup verification procedure, health check interpretation, token rotation, legal hold procedure
- New `Docs/migration-guide.md` — pre-deploy migration steps (`dotnet ef database update`)
- New `k8s/` or `deploy/` directory — Deployment, Service, ConfigMap, Secret manifests (or Helm chart)
- `Program.cs` — add OpenTelemetry tracing and metrics (optional: structured metrics via `System.Diagnostics.Metrics`)
- CI pipeline — add `dotnet test` gate, `dotnet build -c Release` gate

**Out of scope:** Kubernetes cluster provisioning, cloud account setup, DNS.

**Exit criteria:**
- Service can be deployed to a Kubernetes cluster with zero manual config file edits (all secrets via env vars / Secrets)
- `GET /health` is configured as liveness and readiness probe
- Structured logs are shipped to the designated aggregation target
- Alert fires within 5 minutes of `Log.Error` on startup (auth mode misconfiguration)
- DB migration is runnable as a pre-deploy init container or job

---

## 4. Story Backlog by Epic

### Epic A: Production Query Authentication Enablement

---

#### S23-A1: Register JWT Bearer middleware in Program.cs

**Title:** Wire `AddAuthentication().AddJwtBearer()` in the DI container

**Description:**  
`ClaimsCallerResolver` reads `HttpContext.User.Identity.IsAuthenticated`, which is populated by ASP.NET Core's JWT Bearer middleware. Without this registration, `Mode=Bearer` rejects every request with 401 regardless of token validity. This story adds the middleware registration and a matching config options class.

**Technical tasks:**
1. Add `JwtOptions` class to `Configuration/` with `Authority`, `Audience`, `ValidIssuers`, `RequireHttpsMetadata`, `NameClaimType`, `RoleClaimType`
2. In `Program.cs`, read `JwtOptions` from config section `"Jwt"`
3. Add `builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opts => { ... })` using `JwtOptions` fields
4. Add `app.UseAuthentication()` in the middleware pipeline BEFORE `app.UseMiddleware<QueryAuthMiddleware>()` — exact position matters
5. Add `"Jwt": {}` block to `appsettings.json` (empty defaults for dev)
6. Add `"Jwt": { "Authority": null, "Audience": null }` to `appsettings.Production.json` with comment that env vars must supply values
7. Update startup log to emit `Jwt:Authority` at Information level when Mode=Bearer

**Acceptance criteria:**
- `GET /audit/events` with a well-formed JWT signed by the configured Authority returns 200
- `GET /audit/events` with no Authorization header returns 401 when `Mode=Bearer`
- `GET /audit/events` with an expired JWT returns 401
- `Mode=None` behavior is unchanged (anonymous, no JWT middleware interaction)
- Existing 48 tests pass without modification

**Dependencies:** None (this is the foundational item for Epic A)

**Risk notes:** The JWT authority and audience values must match what the Identity service issues. Misconfiguration silently breaks all query access. Validate in staging with a real token before production.

**Owner:** Backend

---

#### S23-A2: Integration tests for Bearer-mode query authorization

**Title:** Write `QueryAuthBearerTests.cs` covering all scope levels and enforcement rules

**Description:**  
No tests currently exercise the query auth path with actual JWT tokens. A test factory that injects a mock JWT (using `TestServer` + `MockJwtTokens` or an in-process JWT generator) is needed to verify all `CallerScope` variants, cross-tenant rejection, and the visibility floor rules.

**Technical tasks:**
1. Create `Tests/Helpers/JwtTokenFactory.cs` — generates HS256 test tokens with configurable claims
2. Create `Tests/Helpers/BearerAuditFactory.cs` — extends `AuditServiceFactory`, sets `QueryAuth:Mode=Bearer`, registers in-memory JWT authority (matching the test token generator's key)
3. Create `Tests/IntegrationTests/QueryAuthBearerTests.cs` with test cases:
   - No token → 401
   - Valid token, PlatformAdmin role → 200, unrestricted
   - Valid token, TenantAdmin role → 200, results filtered to caller's `tenant_id`
   - Valid token, TenantUser role → 200, results filtered to UserScope records only
   - Valid token, cross-tenant query (TenantAdmin requesting another tenant's tenantId) → 403
   - Valid token, no `tenant_id` claim, `EnforceTenantScope=true` → 403
   - Expired token → 401
   - Token with unrecognized role → 200, TenantUser fallback scope

**Acceptance criteria:**
- All 8 scenarios pass
- Cross-tenant isolation is verified by seeding records in two tenants and confirming each TenantAdmin sees only their own
- Test factory can be reused by future test classes

**Dependencies:** S23-A1

**Risk notes:** In-memory JWT issuer for tests must use a key that matches what `AddJwtBearer` validates; mis-keyed tests will produce false positives (all 401) rather than meaningful coverage.

**Owner:** Backend / QA

---

#### S23-A3: Document JWT claim mapping configuration

**Title:** Add IdP-specific JWT claim mapping examples to Docs

**Description:**  
`ClaimsCallerResolver` is deliberately IdP-neutral and reads claim types from `QueryAuthOptions`. The mapping between claim names and LegalSynq role names must be documented per supported IdP (Auth0, Entra ID, Keycloak) so operators don't guess.

**Technical tasks:**
1. Update `Docs/query-authorization-model.md` with a "JWT Claim Mapping" section
2. For each of Auth0 / Entra ID / Keycloak, document:
   - The correct `RoleClaimType` value
   - How roles surface (flat array, nested object, namespace claim)
   - The `TenantIdClaimType` convention
3. Add a worked example `appsettings.Production.json` fragment per IdP
4. Note that Keycloak `realm_access` requires a custom `IQueryCallerResolver` (not supported by `ClaimsCallerResolver`)

**Acceptance criteria:**
- A new operator can configure Bearer mode for Auth0 from the docs alone without looking at code
- Docs reference the correct `QueryAuthOptions` property names

**Dependencies:** S23-A1

**Risk notes:** Low risk. Documentation only.

**Owner:** Backend / Platform

---

### Epic B: Asynchronous Export Processing

---

#### S23-B1: Implement S3-compatible export storage provider

**Title:** Create `S3ExportStorageProvider` implementing `IExportStorageProvider`

**Description:**  
`LocalExportStorageProvider` writes to pod-local filesystem — data is lost on pod restart. A cloud storage provider is required for production. S3 (or compatible, e.g. MinIO, GCS) is the recommended target as the interface is already designed for it (`ExportOptions.S3BucketName`, `S3KeyPrefix`, `AwsRegion` are all defined).

**Technical tasks:**
1. Add `AWSSDK.S3` NuGet package to `.csproj`
2. Create `Services/Export/S3ExportStorageProvider.cs` implementing `IExportStorageProvider`
   - `StoreAsync(stream, fileName)` → `PutObjectAsync` to S3
   - `GetDownloadUrlAsync(key)` → pre-signed URL with configurable TTL
   - `DeleteAsync(key)` → `DeleteObjectAsync`
3. In `Program.cs`, DI switch: when `Export:Provider = "S3"`, register `S3ExportStorageProvider` in place of `LocalExportStorageProvider`
4. Update `appsettings.Production.json` with commented S3 config block
5. Add validation at startup: if `Provider=S3` but `S3BucketName` is null, throw descriptive `InvalidOperationException`

**Acceptance criteria:**
- An export completes and stores to the configured S3 bucket
- Pre-signed download URL is returned and resolvable
- `Provider=Local` still works unchanged for development
- Startup fails with clear error when `Provider=S3` and bucket is not configured

**Dependencies:** None

**Risk notes:** AWS credentials must be injected as env vars (IAM role recommended over access keys). Do not commit any credential.

**Owner:** Backend / Platform

---

#### S23-B2: Move large exports to a background hosted service

**Title:** Implement `ExportProcessingJob` as an `IHostedService` polling `AuditExportJob` records

**Description:**  
Currently `AuditExportService` processes exports synchronously on the HTTP request thread. For large exports, this will exceed HTTP timeout limits. The `AuditExportJob` entity already has `Status` and `CompletedAtUtc` fields — the infrastructure for async processing is designed in but not wired.

**Technical tasks:**
1. Create `Jobs/ExportProcessingJob.cs` as an `IHostedService` (or `BackgroundService`)
   - Poll `IAuditExportJobRepository.GetPendingAsync()` on a configurable interval
   - For each pending job, call `IAuditExportService.ProcessAsync(jobId, ct)`
   - Update job status to `Processing` before starting, `Completed` or `Failed` on finish
   - Use `IServiceScopeFactory` for scoped service resolution (jobs are singleton lifecycle)
2. Update `AuditExportService.cs` to separate job creation (HTTP request) from job processing (background)
3. Register `ExportProcessingJob` as `IHostedService` in `Program.cs`
4. Add `Export:ProcessingIntervalSeconds` to `ExportOptions` (default: 30)
5. Add `ExportOptions.MaxConcurrentJobs` to prevent runaway parallelism

**Acceptance criteria:**
- `POST /audit/export` returns `202 Accepted` with `jobId` immediately
- Polling `GET /audit/export/{jobId}/status` transitions `Pending → Processing → Completed`
- Export of 100K records completes without HTTP timeout
- `ExportProcessingJob` starts on service startup and stops cleanly on `CancellationToken` cancellation

**Dependencies:** S23-B1

**Risk notes:** The existing `EfAuditExportJobRepository` must support a `GetPendingAsync()` method — verify and add if missing. Concurrent job pickup (two replicas claiming the same job) requires either a DB-level claim (SELECT FOR UPDATE or status CAS) or a unique job assignment mechanism.

**Owner:** Backend

---

### Epic C: Integrity Verification and Scheduled Checkpoints

---

#### S23-C1: Add auto-window calculation to IntegrityCheckpointJob

**Title:** Make `IntegrityCheckpointJob` compute its own window from the last persisted checkpoint

**Description:**  
`IntegrityCheckpointJob.ExecuteAsync()` currently requires the caller to supply `fromUtc` and `toUtc`. The job's own doc comment describes the correct algorithm: read the latest checkpoint for the type, compute the next window as `[lastCheckpoint.ToRecordedAtUtc, now)`. This needs to be implemented.

**Technical tasks:**
1. In `Jobs/IntegrityCheckpointJob.cs`, refactor `ExecuteAsync(checkpointType, fromUtc, toUtc, ct)` to `ExecuteAsync(checkpointType, ct)`
2. At start of `ExecuteAsync`, call `IIntegrityCheckpointRepository.GetLatestAsync(checkpointType, ct)` to find the last window end
3. If no prior checkpoint exists, use service start time minus a configurable `InitialLookbackHours` as `fromUtc`
4. Log clearly if a gap exists between the prior window's end and this run's start (non-contiguous timestamps)
5. Add `IntegrityOptions.CheckpointJobEnabled` (bool, default false) and `IntegrityOptions.HourlyCheckpointCronUtc` / `DailyCheckpointCronUtc`

**Acceptance criteria:**
- Running the job twice produces two checkpoints with contiguous `FromRecordedAtUtc`/`ToRecordedAtUtc` boundaries
- First run with no prior checkpoint uses `InitialLookbackHours` correctly
- A gap between runs is logged as a Warning with the exact missing window

**Dependencies:** None (refinement of existing job)

**Risk notes:** If the job misses a scheduled run (pod restart, OOM), the gap will be detectable but must NOT be silently backfilled without operator review (the existing job comment correctly states this).

**Owner:** Backend

---

#### S23-C2: Register IntegrityCheckpointHostedService

**Title:** Wrap `IntegrityCheckpointJob` in a `BackgroundService` and register in `Program.cs`

**Description:**  
Without a hosted service, checkpoints never run autonomously. This story creates the wrapper and wires it into the DI container.

**Technical tasks:**
1. Create `Jobs/IntegrityCheckpointHostedService.cs` as a `BackgroundService`
   - Parse `IntegrityOptions.HourlyCheckpointCronUtc` and `DailyCheckpointCronUtc`
   - On each tick, create a scope, resolve `IntegrityCheckpointJob`, and call `ExecuteAsync`
   - Use a simple `PeriodicTimer` for initial implementation; Quartz.NET is recommended for production cron fidelity
2. Register in `Program.cs` when `IntegrityOptions.CheckpointJobEnabled = true`
3. Add to `appsettings.Production.json`: `"Integrity": { "CheckpointJobEnabled": true, "HourlyCheckpointCronUtc": "0 * * * *", "DailyCheckpointCronUtc": "0 0 * * *" }`

**Acceptance criteria:**
- On service startup with `CheckpointJobEnabled=true`, checkpoints begin generating without any HTTP call
- On service shutdown, the hosted service stops cleanly within 5 seconds
- Checkpoint records appear in the database after each scheduled interval

**Dependencies:** S23-C1

**Risk notes:** Multi-replica deployments will produce duplicate checkpoint writes for the same window unless a distributed lock or leader election is implemented. For v1, document this constraint and recommend single-replica checkpoint worker.

**Owner:** Backend / Platform

---

#### S23-C3: Add checkpoint gap detection endpoint

**Title:** Implement `GET /audit/integrity/gaps` to surface missing checkpoint windows

**Description:**  
HIPAA compliance requires operators to know if checkpoint coverage has gaps. A query endpoint that identifies windows where no checkpoint was generated adds operational confidence.

**Technical tasks:**
1. Add `IIntegrityCheckpointService.GetGapsAsync(from, to, checkpointType, ct)` returning a list of `(fromUtc, toUtc)` gap tuples
2. Implement in `IntegrityCheckpointService.cs` — load ordered checkpoints for the window, find time gaps > expected interval
3. Add `GET /audit/integrity/gaps` to `IntegrityCheckpointController.cs`
4. Gate behind `PlatformAdmin` scope in the authorizer

**Acceptance criteria:**
- If checkpoints exist for hours 1, 2, 4 (missing hour 3), `GET /audit/integrity/gaps` returns the gap `[hour3_start, hour3_end]`
- No gaps returns an empty list with 200
- Non-PlatformAdmin callers receive 403

**Dependencies:** S23-C1, S23-C2

**Owner:** Backend

---

### Epic D: Retention Execution and Archival Provider

---

#### S23-D1: Implement LocalArchivalProvider for staging validation

**Title:** Create `LocalArchivalProvider` implementing `IArchivalProvider`

**Description:**  
Before connecting a cloud archival store, a local/network path archiver allows staging validation of the archival pipeline without cloud credentials. Replaces `NoOpArchivalProvider` in non-production environments.

**Technical tasks:**
1. Create `Services/Archival/LocalArchivalProvider.cs`
   - Reads `Archival:LocalOutputPath` from `ArchivalOptions`
   - Archives each `AuditEventRecord` as a line in an NDJSON file named `{FileNamePrefix}-{tenantId}-{date}.ndjson`
   - Returns `ArchivalResult` with file path and record count
2. Register in `Program.cs` DI switch when `Archival:Strategy = "Local"`
3. Validate that `LocalOutputPath` is non-null and the directory is writable at startup

**Acceptance criteria:**
- Records classified as `Cold` tier are written to the archive file in NDJSON format
- Archive file contains exactly the records that were evaluated as expired
- `Strategy=NoOp` still works unchanged

**Dependencies:** None

**Owner:** Backend

---

#### S23-D2: Implement Phase 2 (archival + deletion) in RetentionPolicyJob

**Title:** Activate archival execution in `RetentionPolicyJob` behind `DryRun=false`

**Description:**  
The current `RetentionPolicyJob` Phase 2 block logs a warning and does nothing. This story implements the actual archive-then-delete loop.

**Technical tasks:**
1. In `RetentionPolicyJob.ExecuteAsync`, after Phase 1 evaluation:
   - If `!_opts.DryRun && _opts.ArchiveBeforeDelete`: call `IArchivalProvider.ArchiveAsync(expiredRecords, ct)`; on success, delete from repository
   - If `!_opts.DryRun && !_opts.ArchiveBeforeDelete`: delete directly (only allowed when `DefaultRetentionDays > 0` to prevent accidental full purge)
   - Respect `MaxDeletesPerRun` as a hard cap per execution cycle
   - Skip records with active legal holds (requires S23-E1 to be complete; gate on `LegalHoldEnabled`)
2. Add batch deletion method to `IAuditEventRecordRepository` and `EfAuditEventRecordRepository`
3. Log every deletion batch with count, oldest record date, and archive path

**Acceptance criteria:**
- `DryRun=true`: zero records deleted, correct evaluation log
- `DryRun=false, ArchiveBeforeDelete=true`: records are in archive file AND removed from primary store
- `DryRun=false, ArchiveBeforeDelete=false`: records are deleted without archiving
- Legal hold records skipped when `LegalHoldEnabled=true`
- Deletion is capped at `MaxDeletesPerRun` per invocation

**Dependencies:** S23-D1, S23-E1 (legal hold check — do not activate before Epic E is complete)

**Risk notes:** This story must not be merged until Epic E (legal hold) is complete and tested. The config comment "LegalHoldEnabled=true" in Production appsettings creates a false expectation of safety. **Add a startup assertion that refuses to proceed with `DryRun=false` if `LegalHoldEnabled=true` but the `LegalHolds` DB table does not exist.**

**Owner:** Backend

---

#### S23-D3: Register RetentionHostedService

**Title:** Wrap `RetentionPolicyJob` as a `BackgroundService`

**Description:**  
Parallel to S23-C2. `RetentionPolicyJob` must run on schedule autonomously.

**Technical tasks:**
1. Create `Jobs/RetentionHostedService.cs` as a `BackgroundService`
   - Parse `Retention:JobCronUtc` (default: `"0 2 * * *"`)
   - Create scope, resolve `RetentionPolicyJob`, call `ExecuteAsync`
2. Register in `Program.cs` when `Retention:JobEnabled = true`
3. Update `appsettings.Production.json`: `"Retention": { "JobEnabled": true }`

**Acceptance criteria:**
- At scheduled time, retention evaluation (or execution) runs without manual trigger
- Shutdown cancels mid-run gracefully with no partial-delete corruption
- `JobEnabled=false` registers nothing and emits a startup Warning

**Dependencies:** S23-D2

**Owner:** Backend / Platform

---

### Epic E: Legal Hold and Compliance Safeguards

---

#### S23-E1: Implement LegalHold entity and repository

**Title:** Add `LegalHold` DB table, entity, and repository

**Description:**  
Without a legal hold entity, `LegalHoldEnabled=true` is a documentation assertion with no enforcement. This story creates the data foundation.

**Technical tasks:**
1. Create `Models/Entities/LegalHold.cs`:
   ```
   LegalHoldId (Guid, PK)
   AuditId     (Guid, FK to AuditEventRecords)
   HeldByUserId (string)
   HeldAtUtc    (DateTimeOffset)
   ReleasedAtUtc (DateTimeOffset?, nullable)
   LegalAuthority (string, e.g. "litigation-hold-2026-001")
   Notes          (string?)
   IsActive       (bool, computed: ReleasedAtUtc is null)
   ```
2. Add `LegalHoldConfiguration.cs` with index on `AuditId` and `IsActive`
3. Add `DbSet<LegalHold>` to `AuditEventDbContext`
4. Create `ILegalHoldRepository` with `ExistsActiveHoldAsync(Guid auditId, CancellationToken)` and `CreateAsync` / `ReleaseAsync`
5. Implement `EfLegalHoldRepository`
6. Generate and apply EF migration

**Acceptance criteria:**
- `EfLegalHoldRepository.ExistsActiveHoldAsync` returns true for records with unreleased holds
- `ExistsActiveHoldAsync` returns false after hold is released
- FK constraint prevents holds on non-existent `AuditId` values
- Migration runs cleanly on the existing schema

**Dependencies:** None

**Owner:** Backend

---

#### S23-E2: Wire legal hold check into RetentionService

**Title:** Skip legal-hold records in `RetentionService.ClassifyTier()`

**Description:**  
`RetentionService` currently classifies records only by age. This story adds the legal hold pre-check so that `ClassifyTier()` returns `StorageTier.LegalHold` for any record with an active hold, bypassing deletion eligibility.

**Technical tasks:**
1. Add `ILegalHoldRepository` as a constructor dependency of `RetentionService`
2. In `ClassifyTier(record)`, add: if `_opts.LegalHoldEnabled && await _legalHoldRepo.ExistsActiveHoldAsync(record.AuditId)` → return `LegalHold`
3. Update `RetentionEvaluationResult` DTO to include `RecordsOnLegalHold` count
4. Update `RetentionPolicyJob` Phase 1 log to surface the legal hold count

**Acceptance criteria:**
- Record with active hold → classified as `LegalHold`, not `Cold`
- Record with released hold → classified normally by age
- `LegalHoldEnabled=false` → hold check is skipped entirely (performance: no DB call)
- `RetentionEvaluationResult.RecordsOnLegalHold` count is accurate

**Dependencies:** S23-E1

**Owner:** Backend

---

#### S23-E3: Legal hold management API

**Title:** Implement `LegalHoldController` with create, release, and query endpoints

**Description:**  
Compliance and legal teams need a controlled API to place and release holds. This surfaces the legal hold capability for internal tooling.

**Technical tasks:**
1. Create `Controllers/LegalHoldController.cs`:
   - `POST /audit/legal-hold` — create hold (body: `{ auditId, heldByUserId, legalAuthority, notes }`)
   - `DELETE /audit/legal-hold/{holdId}` — release hold (sets `ReleasedAtUtc`)
   - `GET /audit/legal-hold?auditId={id}` — query holds for a record
2. Gate all endpoints to `PlatformAdmin` scope via `IQueryAuthorizer`
3. Audit-log hold creation and release via `IAuditEventIngestionService` (self-ingest pattern)
4. Return `409 Conflict` if an active hold already exists for the same `AuditId`

**Acceptance criteria:**
- `POST /audit/legal-hold` returns 201 with `holdId`
- Attempting to create a duplicate active hold returns 409
- `DELETE /audit/legal-hold/{holdId}` releases and returns 200
- Hold creation and release events appear in `/audit/events` (self-audited)
- Non-PlatformAdmin callers receive 403

**Dependencies:** S23-E1, S23-E2

**Owner:** Backend

---

### Epic F: Event Forwarding Productionization

---

#### S23-F1: Implement OutboxIntegrationEventPublisher

**Title:** Create a transactional outbox publisher for durable event forwarding

**Description:**  
Rather than publish directly to a broker from the ingest pipeline (which creates a distributed transaction problem), an outbox pattern writes a relay record to the same MySQL transaction as the audit event. A separate relay worker then publishes to the broker. This is the safest pattern for HIPAA-aligned durability.

**Technical tasks:**
1. Create `Models/Entities/OutboxMessage.cs` — `MessageId`, `EventType`, `PayloadJson`, `CreatedAtUtc`, `ProcessedAtUtc?`, `RetryCount`, `Error?`
2. Add `DbSet<OutboxMessage>` to `AuditEventDbContext` with migration
3. Create `Services/Forwarding/OutboxIntegrationEventPublisher.cs` — writes to `OutboxMessage` table instead of broker
4. Create `Jobs/OutboxRelayJob.cs` as a `BackgroundService` — polls unpublished outbox messages, publishes to broker, marks processed
5. Wire in `Program.cs` when `EventForwarding:BrokerType = "Outbox"`
6. Add dead-letter handling: after `MaxRetryCount` failures, log Error and mark `ProcessedAtUtc` to prevent infinite retry

**Acceptance criteria:**
- Ingesting an event with `EventForwarding:Enabled=true` writes an `OutboxMessage` record in the same transaction
- `OutboxRelayJob` picks up unpublished messages and publishes them
- Broker unavailability does not fail ingest (outbox message persisted, relay retries)
- At-least-once delivery: if relay dies mid-publish, message is retried on next run

**Dependencies:** None (can be developed in parallel with other epics)

**Risk notes:** Requires choosing a broker (RabbitMQ, Kafka, Azure Service Bus). Broker selection is a deployment decision, not a code decision — `OutboxRelayJob` should be broker-agnostic by depending on `IIntegrationEventPublisher`. A `RabbitMqIntegrationEventPublisher` or `KafkaIntegrationEventPublisher` is added separately.

**Owner:** Backend / Platform

---

### Epic G: Test Expansion and Failure-Mode Validation

---

#### S23-G1: Write RetentionService classification unit tests

**Title:** Test Hot/Warm/Cold/Indefinite/LegalHold tier classification

**Technical tasks:**
1. Create `Tests/Unit/RetentionServiceTests.cs`
2. Test `ResolveRetentionDays` with tenant override, category override, default
3. Test `ClassifyTier` at each boundary: day 0, day `HotRetentionDays-1`, day `HotRetentionDays`, day `DefaultRetentionDays-1`, day `DefaultRetentionDays`, day `DefaultRetentionDays+1`
4. Test legal hold bypass when `LegalHoldEnabled=true`

**Acceptance criteria:** All boundary conditions verified. 100% branch coverage of `ClassifyTier`.

**Owner:** QA / Backend

---

#### S23-G2: Write IntegrityCheckpointService integration tests

**Title:** Test checkpoint generation, verification, and gap detection

**Technical tasks:**
1. Create `Tests/IntegrationTests/IntegrityCheckpointTests.cs`
2. Seed 10 records, generate checkpoint, verify aggregate hash matches independent computation
3. Modify a record hash in-memory, re-generate checkpoint, verify mismatch is detectable
4. Skip one hour's checkpoint, call gap detection, verify gap is returned

**Acceptance criteria:** Round-trip integrity verified with both SHA-256 and HMAC-SHA256. Gap detection returns correct intervals.

**Owner:** QA / Backend

---

#### S23-G3: Write forwarding filter unit tests

**Title:** Cover all 5 filter conditions in `NoOpAuditEventForwarder`

**Technical tasks:**
1. Create `Tests/Unit/ForwardingFilterTests.cs`
2. Test: master off → no publish; replay=true + ForwardReplayRecords=false → skip; category mismatch → skip; prefix mismatch → skip; severity below min → skip; all pass → publish called once

**Acceptance criteria:** All 5 filter paths produce correct publish/skip behavior.

**Owner:** QA / Backend

---

#### S23-G4: Write chaos / failure-path tests

**Title:** Verify correct error handling when DB is unavailable and when duplicate keys race

**Technical tasks:**
1. Extend `AuditServiceFactory` with a factory variant that uses a broken `IAuditEventRecordRepository` (throws `InvalidOperationException`)
2. Test: single ingest with DB failure → 500 with `ApiResponse.Success=false`, correct `traceId`
3. Test: concurrent batch with duplicate `IdempotencyKey` → one 201, one with `DuplicateIdempotencyKey` rejection
4. Test: batch with `StopOnFirstError=true` → first failure stops, remaining items are `Skipped`

**Acceptance criteria:** All failure responses use the `ApiResponse` envelope. No stack traces leaked to clients.

**Owner:** QA / Backend

---

### Epic H: Deployment / Rollout / Operational Readiness

---

#### S23-H1: Add OpenTelemetry tracing

**Title:** Instrument the service with `System.Diagnostics.Activity` traces

**Technical tasks:**
1. Add `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http` packages
2. In `Program.cs`, register `builder.Services.AddOpenTelemetry().WithTracing(b => b.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter())`
3. Add `Telemetry:OtlpEndpoint` config option (injected via env var in production)
4. Add custom spans in `AuditEventIngestionService` for the chain lookup step (high I/O)

**Acceptance criteria:** Traces visible in local Jaeger / Zipkin / OTLP collector. `X-Correlation-ID` propagated as trace attribute.

**Owner:** Platform

---

#### S23-H2: Write pre-deploy DB migration runbook

**Title:** Document migration procedure and add migration smoke test to CI

**Technical tasks:**
1. Create `Docs/migration-guide.md`:
   - How to generate a migration: `dotnet ef migrations add <Name> --project ...`
   - How to apply: `dotnet ef database update --project ...`
   - How to verify: check `__EFMigrationsHistory` table
   - Rollback procedure: `dotnet ef database update <PreviousMigration>`
2. Add `dotnet ef database update --dry-run` as a CI validation step
3. Verify the `InitialSchema` migration applies cleanly to a fresh MySQL 8 instance

**Acceptance criteria:** A new team member can apply migrations from docs alone. CI fails if migration generates a destructive operation (DROP TABLE, DROP COLUMN).

**Owner:** Backend / DevOps

---

#### S23-H3: Write production deployment manifests

**Title:** Create k8s Deployment, Service, ConfigMap, and Secret template

**Technical tasks:**
1. Create `deploy/k8s/deployment.yaml` — references `ASPNETCORE_ENVIRONMENT=Production`, all secrets as env vars
2. Create `deploy/k8s/service.yaml` — ClusterIP (not LoadBalancer — internal service)
3. Create `deploy/k8s/configmap.yaml` — non-secret configuration (port, CORS origins)
4. Document the full set of required env vars in a `deploy/k8s/README.md`
5. Configure liveness probe: `GET /health` with `failureThreshold: 3, periodSeconds: 10`
6. Configure readiness probe: `GET /health/detail` with `failureThreshold: 1, periodSeconds: 5` and network policy to restrict to internal only

**Acceptance criteria:** Service deploys to a k8s namespace with `kubectl apply -f deploy/k8s/`. Liveness and readiness probes are active. Swagger UI is unreachable from outside the cluster.

**Owner:** DevOps / Platform

---

## 5. Rollout Order

### Phase 1: Security and Auth Completion
**Stories:** S23-A1, S23-A2, S23-A3, S23-G4

**Why this order is safest:**  
The query auth gap (S23-A1) is a functional blocker — `Mode=Bearer` cannot work without `AddJwtBearer()`. This must be resolved before any production query traffic is allowed. The chaos tests (S23-G4) are included here because they verify the error envelope contract that operators will depend on for alerting.

**Blockers/prerequisites:**  
- `Jwt:Authority` and `Jwt:Audience` must be known before S23-A1 can be fully configured
- Identity service JWT issuer configuration must be finalized

**Gate before Phase 2:** All 48 existing tests pass + S23-A2 Bearer tests pass. No `Log.Error` on startup with production config.

---

### Phase 2: Integrity and Compliance Controls
**Stories:** S23-C1, S23-C2, S23-C3, S23-E1, S23-E2, S23-E3, S23-G2

**Why this order is safest:**  
Integrity checkpoints and legal hold are HIPAA-mandated controls. Legal hold (S23-E1/E2) must be complete before any retention enforcement is activated (Phase 3). Integrity checkpoint automation (S23-C1/C2) makes tamper-evidence continuous rather than on-demand.

**Blockers/prerequisites:**  
- S23-E1 (DB migration) must be applied before S23-E2 or S23-D2 can be deployed
- Checkpoint schedule should be tuned after observing first 48 hours of data volume

**Gate before Phase 3:** Legal hold pre-check is tested and verified. Checkpoint gaps endpoint returns clean results over 72 hours of data.

---

### Phase 3: Async Operations and Archival Execution
**Stories:** S23-B1, S23-B2, S23-D1, S23-D2, S23-D3, S23-G1

**Why this order is safest:**  
Archival must come before deletion. S23-D1 (local archiver) should be validated in staging before S23-D2 (deletion) is activated. S23-D2 has a hard dependency on Epic E being complete. Export async processing (S23-B1/B2) is independent and can run in parallel.

**Blockers/prerequisites:**  
- S23-E1 and S23-E2 complete and deployed before S23-D2 is activated
- S3 bucket / archival storage provisioned before S23-D1 staging validation

**Gate before Phase 4:** `DryRun=false` is activated in staging, evaluated over one full retention cycle (7 days min). Zero unintended deletions. Legal hold records survive.

---

### Phase 4: Forwarding and Scale Hardening
**Stories:** S23-F1, S23-G3, S23-H1, S23-A3

**Why this order is safest:**  
Forwarding is a secondary concern — audit records are already durable at this point. The outbox pattern (S23-F1) is additive and non-breaking. OpenTelemetry (S23-H1) improves observability without changing behavior.

**Blockers/prerequisites:**  
- Broker (RabbitMQ / Kafka) must be provisioned
- `OutboxRelayJob` needs a real `IIntegrationEventPublisher` implementation per chosen broker

**Gate before Phase 5:** Broker integration validated in staging. No audit event ingest failures caused by broker unavailability.

---

### Phase 5: Rollout Validation and Production Cutover
**Stories:** S23-H2, S23-H3, production cutover

**Why this order is safest:**  
Deployment manifests and migration runbook finalize the operational posture. The cutover procedure should be a staged rollout (shadow traffic → canary → full).

**Blockers/prerequisites:**  
- All Phase 1–4 gates passed
- Production DB migration applied (not via MigrateOnStartup — as a pre-deploy job)
- Full `production-readiness-checklist.md` signed off

---

## 6. Critical Path

The minimum path to "safe controlled production rollout" is:

```
S23-A1 (JWT middleware)
  └→ S23-A2 (Bearer tests)
       └→ [Phase 1 gate]
            └→ S23-E1 (LegalHold entity + migration)
                 └→ S23-E2 (hold check in RetentionService)
                      └→ S23-D1 (LocalArchivalProvider)
                           └→ S23-D2 (Phase 2 retention — archive+delete)
                                └→ S23-D3 (RetentionHostedService)
                                     └→ S23-H2 (migration runbook)
                                          └→ S23-H3 (k8s manifests)
                                               └→ Production cutover
```

**Gating items (must complete before production cutover):**
- S23-A1: Without JWT middleware, query auth is broken in Bearer mode
- S23-E1 + S23-E2: Without legal hold enforcement, `DryRun=false` can delete protected records
- S23-D1: Without a real archival provider, `ArchiveBeforeDelete=true` has no effect
- S23-H2: Without a migration procedure, the schema change for LegalHolds cannot be applied safely

**Non-gating items (can follow after initial rollout):**
- S23-B1/B2: Export async — service works with `Provider=None`; exports disabled until this ships
- S23-C1/C2: Checkpoint automation — manual HTTP trigger still works
- S23-F1: Forwarding — `Enabled=false` is the safe default; service functional without it
- S23-H1: OpenTelemetry — console logs are sufficient initially

---

## 7. Risk Register

| Risk | Severity | Likelihood | Mitigation | Blocking? |
|---|---|---|---|---|
| **JWT misconfiguration** — Bearer mode wired with wrong Authority/Audience silently 401s all legitimate queries | Critical | High | Validate with a real token in staging before prod. Add startup log of `Jwt:Authority` at Info level. Write S23-A2 tests against real token first. | Yes |
| **Cross-tenant data exposure** — Bug in `QueryAuthorizer.ApplyTenantConstraint()` allows tenant leak | Critical | Low | S23-A2 tests explicitly verify cross-tenant isolation. Code reviewed in Step 14; constraint applied server-side in all paths. | Yes |
| **Retention deletion before legal hold** — `DryRun=false` + `JobEnabled=true` activated before S23-E1/E2 | Critical | Medium | Add startup assertion in `Program.cs`: if `DryRun=false && LegalHoldEnabled=true && LegalHolds table not detected` → log Error and refuse to enable deletion. Block S23-D3 deployment on S23-E2 complete. | Yes |
| **Archival failure silent data loss** — Phase 2 deletes before confirming archive success | High | Medium | In S23-D2, deletion must be transactional: archive result must be verified before `DELETE` is executed. If archival throws, abort deletion and log Error. | Yes |
| **Checkpoint gap coverage** — Multi-replica deployment generates duplicate checkpoints or misses windows | Medium | Medium | Document single-replica constraint for checkpoint worker in S23-C2. Add distributed lock (e.g. DB advisory lock) in a follow-up story for multi-replica support. | No |
| **Export load spike** — Large export of 10M records exhausts DB connection pool | Medium | Medium | S23-B2 sets `MaxConcurrentJobs`. Add `MaxRecordsPerFile` pagination in `AuditExportService`. Stream query results instead of materializing full result set. | No |
| **Forwarding delivery gap** — Without outbox, broker unavailability during ingest silently drops events | Medium | Low | Forwarding failure is already non-fatal in `AuditEventIngestionService`. Outbox pattern (S23-F1) eliminates the gap. Log Warning on every forwarding failure for monitoring visibility. | No |
| **Insufficient failure-path test coverage** — DB-down or partial-batch failures surface unhandled exceptions | Medium | Medium | S23-G4 adds chaos tests. `ExceptionMiddleware` already catches all unhandled exceptions and returns 500 ApiResponse. | No |
| **HMAC key rotation gap** — Rotating `Integrity:HmacKeyBase64` invalidates all prior hashes | High | Low | Document in `Docs/runbook.md` that the HMAC key must never be rotated without re-hashing or migrating all records. Consider key versioning in a follow-up. | No |
| **MigrateOnStartup=false breaks first deploy** — DBA forgets to run migration before deploying LegalHolds schema | High | Medium | S23-H2 creates a pre-deploy checklist with migration step. Consider a `--dry-run` migration CI check. | No |

---

## 8. Recommended Milestones

### Milestone 23.1 — Secure Production Enablement

**Stories:** S23-A1, S23-A2, S23-A3, S23-G4

**Expected outcome:** The service can be deployed to production with `QueryAuth:Mode=Bearer` working correctly. All callers receive appropriate scoped responses. No cross-tenant data leaks. Error envelope is correct in failure scenarios.

**Validation before close:**
- Deploy to staging with a real JWT from the Identity service
- Run S23-A2 Bearer tests against staging environment
- Verify `/audit/events` 401 without token, 200 with valid token, 403 with cross-tenant request
- Confirm startup log shows no `Error` level entries with production config
- All 48 original tests + new Bearer tests pass

---

### Milestone 23.2 — Compliance Controls

**Stories:** S23-C1, S23-C2, S23-C3, S23-E1, S23-E2, S23-E3, S23-G1, S23-G2

**Expected outcome:** Integrity checkpoints generate automatically on schedule. Legal hold prevents retention deletion of protected records. Compliance team has API access to manage holds.

**Validation before close:**
- Run checkpoint scheduler for 48 hours and verify no gaps in coverage
- Create a legal hold, run retention evaluation, verify hold record is classified `LegalHold` not `Cold`
- Verify gap detection endpoint returns empty when no gaps exist
- S23-G1 retention classification tests pass
- S23-G2 integrity round-trip tests pass

---

### Milestone 23.3 — Async Operations

**Stories:** S23-B1, S23-B2, S23-D1, S23-D2, S23-D3, S23-G3

**Expected outcome:** Exports work end-to-end with cloud storage. Retention enforcement is active with archival protection. Events are forwarded with filter rules correctly applied.

**Validation before close:**
- Export 10K records; verify file appears in S3; verify polling endpoint transitions to Completed
- Run retention job with `DryRun=false` in staging; verify cold records are archived then deleted; verify legal hold records survive
- S23-G3 forwarding filter tests pass
- Confirm zero unintended deletions over 7-day staging period

---

### Milestone 23.4 — Production Integration Hardening

**Stories:** S23-F1, S23-H1, S23-H2, S23-H3

**Expected outcome:** The service is fully deployable from manifests, observable via distributed tracing, and broker-integrated for event forwarding.

**Validation before close:**
- Deploy using `kubectl apply -f deploy/k8s/` with no manual edits
- Verify traces visible in OpenTelemetry collector
- Verify outbox relay publishes events to broker within 30 seconds of ingest
- Migration runbook successfully applied against a fresh MySQL instance
- All items in `production-readiness-checklist.md` checked

---

## 9. Final Recommended Execution Plan

1. **Fix JWT Bearer middleware wiring** (S23-A1). This is the only functional blocker for production query auth and must be the first commit. Without it, `Mode=Bearer` is non-functional.

2. **Write Bearer-mode integration tests** (S23-A2). Validate that JWT wiring actually works with scoped constraints. Do not proceed to production until cross-tenant isolation is test-verified.

3. **Implement the LegalHold entity and DB migration** (S23-E1). Must be done before any retention enforcement is activated. Apply migration in staging as soon as it is ready.

4. **Wire legal hold check into RetentionService** (S23-E2). Legal hold pre-check must be in production before DryRun=false is left as the active config.

5. **Add startup guard: refuse DryRun=false without LegalHolds table** (part of S23-D2). Defensive assertion prevents operator error from causing unintended deletion.

6. **Implement IntegrityCheckpointJob auto-window and host as BackgroundService** (S23-C1, S23-C2). Checkpoint automation is a HIPAA control that should be active as soon as the service accepts production traffic.

7. **Implement LocalArchivalProvider and RetentionPolicyJob Phase 2** (S23-D1, S23-D2). Validate in staging over a full retention cycle before enabling in production.

8. **Register RetentionHostedService in production** (S23-D3). Only after S23-D1/D2 are staging-validated and S23-E2 is deployed.

9. **Write retention classification tests and chaos tests** (S23-G1, S23-G4). These provide the confidence bar needed before any destructive operation is activated in production.

10. **Implement S3 export provider and async export job** (S23-B1, S23-B2). Export can follow retention activation — it is high-value but not a safety blocker.

11. **Implement OutboxIntegrationEventPublisher** (S23-F1). Add a real broker publisher once the outbox relay infrastructure is stable.

12. **Add OpenTelemetry tracing** (S23-H1). Operational observability improvement — schedule in parallel with S23-F1.

13. **Write and validate k8s deployment manifests and migration runbook** (S23-H2, S23-H3). Finalize before production cutover.

14. **Production cutover** — staged rollout with shadow traffic → canary → full. Monitor `/health`, `/health/detail`, and log aggregation for 48 hours post-cutover.

---

## 10. Suggested Next Step 24

**Step 24: Producer Onboarding and Multi-Service Integration Testing**

After Step 23 delivers a fully hardened, production-enabled audit service, Step 24 should focus on:

1. **Structured onboarding** for the Identity, Fund, and CareConnect services to the audit ingest pipeline — service token provisioning, AllowedSources configuration, event type catalog alignment
2. **Cross-service integration tests** — verify that an action in Identity (user created, role assigned) produces an audit event visible via `/audit/events?tenantId=...`
3. **End-to-end HIPAA audit trail validation** — a compliance review scenario: select a user action, trace it from source service ingest through to query response, verify hash chain integrity
4. **Volume testing** — simulate 6 months of production ingest volume against MySQL with realistic event distribution; verify query performance, index efficiency, and checkpoint generation latency

Step 24 would be the first step where the audit service operates as a live dependency of other services rather than in isolation. It would close the loop between the service-level work done in Steps 1–23 and the platform-level HIPAA audit trail requirement.

---

## Appendix: Recommended First Sprint

**Sprint goal:** Unblock production query auth and establish the compliance data foundation.

**Stories:**
- S23-A1 (JWT Bearer wiring) — 2 days
- S23-A2 (Bearer integration tests) — 1 day
- S23-E1 (LegalHold entity + migration) — 2 days
- S23-G4 (chaos/failure-path tests) — 1 day

**Sprint output:** The service can run in production with `Mode=Bearer` working. LegalHold table is in the schema. Error handling is verified. Ready for Milestone 23.1 sign-off.

---

## Appendix: Do Not Do Yet

The following items should **not** be started until their prerequisites are complete:

- **Do not activate `Retention:JobEnabled=true` in production** until S23-E2 (legal hold check) and S23-D1 (real archival provider) are both deployed and staging-validated. The current config state (`DryRun=false, LegalHoldEnabled=true`) is deceptive — it signals readiness that does not exist.

- **Do not implement Quartz.NET scheduling** yet. `PeriodicTimer`-based `BackgroundService` is sufficient for v1 and adds zero dependency risk. Quartz.NET adds significant complexity (job stores, clustering, misfire handling) that is premature until multi-replica scheduler requirements are confirmed.

- **Do not implement per-tenant archival buckets** in the first archival provider. A single bucket with a tenant-namespaced key prefix is the correct starting point. Per-tenant buckets require IAM per-tenant role assumption, which is a platform-level concern outside this service's scope.

- **Do not add Kafka support** before RabbitMQ or the outbox pattern is validated. Kafka has fundamentally different delivery semantics (consumer group, offset management) and should be introduced as a separate story after the forwarding abstraction is proven with a simpler broker.

- **Do not enable `ExposeIntegrityHash=true`** in production until a formal decision is made about whether HMAC hash exposure in API responses is acceptable under the organization's security model. Hashes are currently suppressed for all non-PlatformAdmin callers; this is the correct default.

- **Do not run database migrations via `MigrateOnStartup=true` in production** for multi-replica deployments. This creates a race condition where multiple pods attempt to migrate simultaneously. Use a pre-deploy init container or CI pipeline step per S23-H2.

---

*Report generated from direct code analysis of `apps/services/platform-audit-event-service` and `apps/services/platform-audit-event-service.Tests`. All recommendations are grounded in the current implementation seams identified in the source tree.*
