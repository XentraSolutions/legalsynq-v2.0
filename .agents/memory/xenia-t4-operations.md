---
name: Xenia T4 operations domain
description: What was built for the XENIA-P1-T4 email operations/monitoring layer and what patterns were established.
---

## What was built

### New domain entities
- `EmailAlertType`, `EmailAlertSeverity`, `EmailAlertStatus` — enums
- `EmailRetentionMode` (includes `EmailRetentionRunStatus`) — enum
- `EmailOperationalAlert` — lifecycle: Open→Acknowledged→Resolved/Suppressed; deduplication key; fencing-token-aware
- `EmailOperationalSettings` — per-tenant config (1 row), 20+ settings, versioned
- `EmailRetentionRun` — audit trail for retention execution; RecordProgress/Complete/Fail/Cancel

### Domain updates
- `EmailSourceSyncLock` — added `FencingToken` (long, increments on Acquire, stable on Renew), `RenewalFailureCount` (int), `RecordRenewalFailure(threshold=3)`, `ValidateFencingToken(token)`
- `EmailIngestionRun` — added `RetryOfRunId` (Guid?), `CreateRetry()` factory method

### Application interfaces (in Email/Operations/)
- `IAlertService`, `IAlertRuleEngine`, `IOperationsSummaryService`
- `ISourceHealthService`, `IProviderHealthService`
- `IRunQueryService`, `IRetentionService`, `IEmailOperationalSettingsService`
- `IEmailHeaderSanitizer` (in Ingestion/)

### Infrastructure services
- `EmailHeaderSanitizer` — denied list + substring check + allowed-override list + 1024 char truncation + 50 header count cap
- `EfAlertService`, `EfEmailOperationalSettingsService`, `EfOperationsSummaryService`
- `EfSourceHealthService`, `EfProviderHealthService`
- `EfRunQueryService`, `EfRetentionService`
- `DefaultAlertRuleEngine`, `LockLeaseRenewalService`

### EF / Migration
- Migration 7: `AddOperationsDomain` — 3 new tables, fencing_token + renewal_failure_count on sync_locks, retry_of_run_id on ingestion_runs
- 3 new EF configurations; EmailIngestionRunConfiguration updated with RetryOfRunId mapping
- XeniaDbContext: 3 new DbSets

### API
- 4 new endpoint classes: `XeniaEmailOperationsEndpoints`, `XeniaEmailRunEndpoints`, `XeniaEmailAlertEndpoints`, `XeniaEmailRetentionEndpoints`
- 4 new policies: `EmailOperationsRead`, `EmailOperationsManage`, `EmailAlertsManage`, `EmailRetentionManage`
- All registered in `Program.cs`

### Frontend (Control Center)
- 8 new pages under `/xenia/email/`: operations, runs, runs/[runId], alerts, retention, retention/settings, operations/sources, operations/providers
- Layout nav updated with 4 new links
- `xenia-email-api.ts` extended with ~250 lines of new types and API functions

### Tests
- `EmailHeaderSanitizerTests` — 13 cases including denied/allowed headers, length/count limits, Unicode
- `EmailOperationalAlertTests` — lifecycle, versioning
- `EmailSourceSyncLockFencingTests` — fencing token increment/validation, renewal failure count
- `EmailRetentionRunTests` — lifecycle, dry-run vs execute
- `EmailIngestionRunRetryTests` — CreateRetry factory

## Bugs found and fixed during validation

- **Migration `[Migration]` attribute is in Designer.cs.** A manually-authored migration `.cs` file without a companion Designer.cs won't be listed by `ef migrations list` AND won't apply at runtime (runtime also uses the `[Migration]` attribute to discover migrations). Fix: add `[DbContext(typeof(XeniaDbContext))] [Migration("20260710000007_AddOperationsDomain")]` directly to the migration file; the `partial class` pattern is fine without the Designer counterpart as long as the attribute is present.
- **`ImapEmailIngestionConnector` must be `AddScoped` not `AddSingleton`** because its constructor injects `ISecretReferenceService` which is registered as scoped. Pattern: if any injected service is scoped, the dependent must be scoped too. dotnet validates this at startup with DI validation enabled (EF tools surface it as a startup error during `ef migrations list`).
- **Xunit must be imported explicitly per test file.** `using Xunit;` is NOT a global using in this project. Every file that uses `[Fact]`, `[Theory]`, or `Assert` needs the directive. Files missing it compile in isolation but fail when `dotnet build` does a full rebuild.

**Why:** Complete audit of the operations monitoring layer for XENIA-P1-T4.
