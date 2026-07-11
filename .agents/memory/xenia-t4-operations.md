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

**Why:** Complete audit of the operations monitoring layer for XENIA-P1-T4.
