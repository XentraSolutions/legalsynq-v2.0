# TB-INT-04 — Durable Entitlement Outbox

> Status: **DONE**

## 1. Summary

Adds an EF-backed durable outbox for Commerce → Tenant Billing entitlement publish work in `services/Commerce/`. Trigger sites (subscription lifecycle + account-standing recalculation) write a row per event; a background worker (`TenantBillingEntitlementOutboxWorker`) polls the table, dispatches each due row through the existing `ITenantBillingEntitlementPublisher`, retries with linear backoff, abandons after `MaxAttempts`, and recovers stale `Processing` rows on the next sweep. Gated by `Commerce:TenantBilling:OutboxEnabled` (default **false**); when off, the TB-INT-03 in-memory queue path is preserved unchanged. No Tenant Billing changes; no enforcement; no UI; no shared DB.

## 2. Codebase Analysis

- TB-INT-03 already provides `BoundedTenantBillingEntitlementPublishQueue` (in-memory `Channel<T>`-backed) and `TenantBillingEntitlementPublishWorker` for fire-and-forget post-commit publish dispatch. Trigger sites: `SubscriptionService.{CreateAsync, ActivateAsync, SuspendAsync, ReactivateAsync, CancelAsync, ChangePlanAsync}` + `AccountStandingService.RecalculateAsync`.
- The publisher (`TenantBillingEntitlementPublisher.PublishForBillingAccountAsync`) returns a `PublishEntitlementResult` with one of three outcomes (`Published` / `Skipped` / `Failed`) plus a reason string, HTTP status, and short response body summary — the outbox processor reuses this contract verbatim.
- The Commerce `CommerceDbContext` is MySQL via Pomelo (`UseMySql`) with an InMemory fallback for preview / tests. Existing migrations follow utf8mb4 + ascii_general_ci on `char(36)` GUID columns (see `InvoiceConfigurations`).
- Diagnostics for TB-INT-03 are already wired into `GET /api/commerce/tenant-billing/diagnostics` via `ITenantBillingEntitlementPublisher.GetDiagnosticsAsync`. Adding outbox fields to the same endpoint avoided introducing a new surface.

## 3. Outbox Architecture

**Entity**: `TenantBillingEntitlementPublishOutboxRow` (`services/Commerce/src/Commerce.Infrastructure/Integration/TenantBilling/Outbox/`)

| Column | Purpose |
|---|---|
| `Id` (PK, GUID) | Row identity. |
| `BillingAccountId` (GUID) | Commerce billing account whose snapshot must be republished. |
| `TriggerSource` (varchar(120)) | Lifecycle label, e.g. `subscription-created`, `account-standing-recalculated`. |
| `Status` (int) | 1=Pending, 2=Processing, 3=Published, 4=Failed, 5=Abandoned. (`Failed` is currently not a terminal state — failures move to `Pending` until `MaxAttempts`.) |
| `Attempts` / `MaxAttempts` | Retry budget; abandoned when `Attempts >= MaxAttempts`. |
| `NextAttemptAtUtc` | Earliest poll time the row becomes due again. |
| `LastAttemptAtUtc`, `PublishedAtUtc` | Audit timestamps. |
| `LastOutcome` (varchar(32)), `LastReason` (varchar(120)), `LastHttpStatus`, `LastErrorSummary` (varchar(2000)) | Diagnostic snapshot of the last attempt. |
| `CorrelationId` (varchar(128), nullable) | Optional caller correlation for log correlation. |
| `LockedAtUtc`, `LockId` | Set when the processor moves a row to `Processing`; cleared on terminal/retry outcome; used by stale-recovery sweep. |
| `CreatedAtUtc`, `UpdatedAtUtc` | Bookkeeping. |

**Indexes** (all created by the EF migration): `BillingAccountId`, `Status`, `NextAttemptAtUtc`, `(Status, NextAttemptAtUtc)` (the polling index), `TriggerSource`, `CreatedAtUtc`.

**Status transitions**: `Pending → Processing → Published` (terminal) | `Processing → Pending` (retry) | `Processing → Abandoned` (terminal, on `MaxAttempts` or terminal-skip reason). `RecoverStaleProcessing` moves an orphaned `Processing` row back to `Pending` without consuming an attempt.

**Components**:

- `EfTenantBillingEntitlementOutbox` (`ITenantBillingEntitlementOutbox`): persists a single row per `EnqueueAsync`; catches every persistence exception and returns `Guid.Empty` so a Commerce commit is never rolled back by an outbox failure.
- `TenantBillingEntitlementOutboxProcessor` (`ITenantBillingEntitlementOutboxProcessor`): batched processing via `ProcessDueAsync(batchSize, ct)` — recovers stale rows, then claims & publishes up to `batchSize` due `Pending` rows, ordered by `(NextAttemptAtUtc ASC, CreatedAtUtc ASC)`.
- `TenantBillingEntitlementOutboxWorker` (`BackgroundService`): polling loop honouring `OutboxEnabled` / `OutboxPollSeconds` / `OutboxBatchSize`. Always registered; no-op when disabled.

## 4. Configuration Changes

`TenantBillingClientOptions` (`Commerce:TenantBilling`) gains five outbox knobs, all clamped in `Normalised()`:

| Key | Default | Clamp |
|---|---|---|
| `OutboxEnabled` | `false` | n/a |
| `OutboxBatchSize` | 25 | [1, 1_000] |
| `OutboxPollSeconds` | 10 | [1, 600] |
| `OutboxMaxAttempts` | 10 | [1, 100] |
| `OutboxRetryBaseDelaySeconds` | 30 | [1, 3_600] |

`appsettings.json` ships with all defaults; the existing TB-INT-03 keys are untouched.

## 5. Database / Migration Changes

EF migration **`20260515132027_TenantBillingEntitlementPublishOutbox`** scaffolds the new table with utf8mb4 charset, the 6 indexes listed above, and matching `Down` (drops the table). Generated via a new `CommerceDbContextDesignTimeFactory` that pins MySQL provider at design time so migrations scaffold real column types regardless of the developer's local connection string. **No** existing column or migration is modified — purely additive.

Migrations run automatically only when `ASPNETCORE_ENVIRONMENT=Development` (per pre-existing Commerce policy). No production migration is auto-applied by this change.

## 6. Trigger Routing Behavior

`SubscriptionService` and `AccountStandingService` both gain optional `IOptions<TenantBillingClientOptions>` and `ITenantBillingEntitlementOutbox` constructor parameters and a single shared `TryEnqueueAutoPublishAsync(billingAccountId, trigger, ct)` helper. Routing rule:

1. If `OutboxEnabled == true` AND an outbox is wired → write a row via `EnqueueAsync`. The in-memory queue is **not** touched.
2. Otherwise → fall back to the legacy TB-INT-03 `ITenantBillingEntitlementPublishQueue.Enqueue(...)` path.
3. Any exception or non-success result from either path is swallowed and logged. **The Commerce commit is never rolled back by a publish-side failure.**

Trigger labels published into the outbox are identical to those used by TB-INT-03 (`subscription-created`, `subscription-activated`, `subscription-suspended`, `subscription-reactivated`, `subscription-cancelled`, `subscription-plan-changed`, `account-standing-recalculated`). `Renew` continues to **not** auto-publish.

## 7. Worker / Processor Behavior

`TenantBillingEntitlementOutboxWorker` polls every `OutboxPollSeconds`. Each tick:

1. Calls `processor.ProcessDueAsync(OutboxBatchSize, ct)`.
2. Sleeps until the next interval (or exits on cancellation).
3. Catches and logs any unexpected exception so the worker never dies — the next tick simply tries again.

`ProcessDueAsync`:

1. **Stale recovery**: any row in `Processing` with `LockedAtUtc < now − max(OutboxPollSeconds × 3, 60s)` is returned to `Pending` (no attempt consumed). This handles app restarts mid-publish and orphaned locks.
2. **Claim**: select up to `batchSize` due `Pending` rows ordered by `(NextAttemptAtUtc ASC, CreatedAtUtc ASC)`. For each id, re-load and atomically transition `Pending → Processing` with a fresh `LockId`. Concurrency exceptions on the claim are treated as "another worker won this row" and silently skipped. (Production deployments should still run only one outbox worker pod until a relational `SELECT … FOR UPDATE SKIP LOCKED` claim is added — flagged in §14.)
3. **Dispatch**: invoke `IPublisher.PublishForBillingAccountAsync(BillingAccountId, ct)`. Per-row publisher exceptions are caught and routed to `ScheduleRetryOrAbandon` so one bad row never stops the batch.
4. **Outcome**: `HandleResultAsync` maps the publisher's `PublishEntitlementResult` to one of `Published` / `Retried` / `Abandoned` / `Skipped` (see §8).

## 8. Retry / Abandon Behavior

| Publisher outcome | Outbox action |
|---|---|
| `Published` | `MarkPublished` — terminal. Attempt count incremented. |
| `Skipped` with reason in `{ no-external-tenant-id, external-tenant-id-not-a-guid, billing-account-not-found, tenant-id-empty }` | **Terminal abandon**: these reasons reflect missing/invalid Commerce-side data that won't change with retries. |
| `Skipped` with any other reason (e.g. `publisher-disabled`) | **Reschedule** with `NextAttemptAtUtc = now + OutboxRetryBaseDelaySeconds`; **does not consume an attempt** (no real wire call was made). |
| `Failed` (or publisher exception) AND `Attempts + 1 < MaxAttempts` | **Linear backoff**: `NextAttemptAtUtc = now + OutboxRetryBaseDelaySeconds × min(Attempts+1, 10)`. |
| `Failed` (or publisher exception) AND `Attempts + 1 >= MaxAttempts` | **Terminal abandon** with `LastReason` set from the failure. |

`OperationCanceledException` during dispatch is rethrown without mutating the row — the next stale-recovery sweep returns it to `Pending`.

## 9. Stale Processing Recovery

The worker can crash, the host can be restarted, or a publisher call can hang past cancellation. Without recovery, a row stuck in `Processing` would never be retried. The processor's stale-recovery step (run on every tick before claiming new rows) returns any `Processing` row whose `LockedAtUtc` is older than `max(OutboxPollSeconds × 3, 60s)` back to `Pending`, clears its `LockId` / `LockedAtUtc`, and **does not consume an attempt** (no real attempt was completed). Recovered rows are picked up in the same batch's claim phase if they are due.

## 10. Diagnostics Changes

`TenantBillingDiagnostics` gains 11 outbox-related fields (all default to safe back-compat values when no outbox is wired), surfaced via the existing `GET /api/commerce/tenant-billing/diagnostics`:

`OutboxEnabled`, `OutboxBatchSize`, `OutboxPollSeconds`, `OutboxMaxAttempts`, `OutboxRetryBaseDelaySeconds`, `OutboxRegistered`, `OutboxPendingCount`, `OutboxProcessingCount`, `OutboxPublishedCount`, `OutboxFailedCount`, `OutboxAbandonedCount`.

`TenantBillingEntitlementPublisher.GetDiagnosticsAsync` now optionally injects `ITenantBillingEntitlementOutbox` and reads counts via `GetCountsAsync` (NoTracking grouped scan).

## 11. Logging / Metrics Behavior

Seven new counters added to `TenantBillingPublisherMetrics`, all tagged with `trigger_source`:

- `tb_outbox_enqueued_total{trigger_source}`
- `tb_outbox_enqueue_failed_total{trigger_source, reason}`
- `tb_outbox_processed_total{trigger_source, outcome, reason?}`
- `tb_outbox_published_total{trigger_source}`
- `tb_outbox_failed_total{trigger_source, reason}`
- `tb_outbox_retried_total{trigger_source}`
- `tb_outbox_abandoned_total{trigger_source, reason}`

Structured logs emit `OutboxId`, `BillingAccountId`, `TriggerSource`, `Attempts`, `MaxAttempts`, `LockId`, `HttpStatus`, `Reason`, `NextAttemptAtUtc` for every state transition. Worker startup line includes the active config so an operator can confirm the gate from logs alone (`OutboxEnabled=False PollSeconds=10 BatchSize=25 MaxAttempts=10 RetryBaseDelaySeconds=30`).

## 12. Tests Added

`services/Commerce/tests/Commerce.Tests/`:

- **`Integration/TenantBilling/TenantBillingEntitlementOutboxTests.cs`** (11 tests) — repo enqueue happy-path & invalid-input swallowing, processor publish, failed-then-retry with linear backoff, failed-after-max abandon, terminal-skip-reason abandon, transient-skip reschedule (no attempt consumed), publisher-exception retry, due-time gating, stale-`Processing` recovery + immediate re-claim, `ORDER BY NextAttemptAtUtc` + batch-size cap.
- **`Subscriptions/SubscriptionServiceOutboxRoutingTests.cs`** (2 tests) — `OutboxEnabled=true` routes to outbox and skips the in-memory queue; `OutboxEnabled=false` falls back to the in-memory queue and never touches the outbox.
- **`Integration/TenantBilling/TenantBillingOutboxAdditionalCoverageTests.cs`** (3 tests) — linear-backoff multiplier caps at 10× regardless of attempt count, diagnostics endpoint surfaces `OutboxEnabled / OutboxBatchSize / OutboxPollSeconds / OutboxMaxAttempts / OutboxRetryBaseDelaySeconds / OutboxWorkerRegistered / OutboxPendingCount / OutboxAbandonedCount`, diagnostics swallows a throwing `GetCountsAsync` and reports zeros.

## 13. Validation Results

- `dotnet build services/Commerce/Commerce.sln` — succeeds clean.
- `dotnet test services/Commerce/tests/Commerce.Tests/Commerce.Tests.csproj` — **359 / 360 passing**. The single failure (`SubscriptionServiceTests.ChangePlan_closes_old_item_and_creates_new`) is the pre-existing wall-clock validator drift documented before TB-INT-04 began (`EffectiveAtUtc cannot be more than 1 day in the past`); unrelated to this change.
- `Commerce API` workflow restarts cleanly. Startup log shows both workers wired with their gating posture, and no migration is applied (production env, gate off).

## 14. Risks / Deferred Items

- **Multi-worker claim race**: the current claim path uses optimistic EF semantics (re-read + status-guarded update). On Pomelo MySQL with multiple worker pods, the loser of a race throws `DbUpdateConcurrencyException` and the row is skipped on this tick. Until a relational `SELECT … FOR UPDATE SKIP LOCKED` is added, **deploy with one outbox worker pod** (the same constraint as TB-INT-03's in-memory queue, so this is not a regression).
- **`Failed` status not currently used as a terminal state**: rows always move from `Processing` to `Pending` (retry) or `Abandoned`. The bucket is reserved for a future "permanent failure inspection queue" without forcing another migration.
- **Outbox dead-letter inspection**: there is no admin endpoint to list `Abandoned` rows yet. The diagnostics endpoint exposes the count, which is enough for alerting; a list-endpoint can be added without schema changes.
- **No automatic backfill** when `OutboxEnabled` is flipped from `false` to `true`: in-flight TB-INT-03 in-memory items at the moment of the flip are still drained by the in-memory worker; new triggers go to the outbox. Both workers run concurrently — this is intentional during the cutover.
- **Production migration application is still gated on `ASPNETCORE_ENVIRONMENT=Development` or `BILLING_RUN_MIGRATIONS=true`.** Operators must apply the migration manually before flipping `OutboxEnabled` in production.
- **No Tenant Billing changes**: idempotency of repeated publishes remains the responsibility of Tenant Billing's snapshot upsert (already documented in TB-INT-01/03).
