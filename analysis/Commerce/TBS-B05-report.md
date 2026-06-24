# TBS-B05 Report — Invoice Lifecycle & State Engine

## 1. Codebase Analysis

The standalone Tenant Billing Service (`services/tenant-billing-api/`)
already has a fairly complete invoice lifecycle from blocks B01 through
B04. TBS-B05 is largely a centralisation + tightening pass on top of
that, plus the new Mark-Overdue surface and a hosted scheduler.

### Canonical statuses

`TenantBilling.Domain/Entities/InvoiceStatus.cs` already defines the
canonical status constants in one place (no magic strings elsewhere in
the production code):

`Draft`, `Issued`, `PartiallyPaid`, `Paid`, `Overdue`, `Voided`,
`PartiallyRefunded`, `Refunded`.

The last two arrived with the refund flow (post-B04). We keep them in
the lifecycle and the state machine recognises them as legal post-Paid
sinks.

### Where transitions are performed today

Status transitions happen in three places:

1. `InvoiceService.IssueAsync(tenantId, invoiceId)` — guards
   `Status == Draft` inline and otherwise throws
   `InvalidOperationException`.
2. `InvoiceService.VoidAsync(tenantId, invoiceId)` — guards
   `Status in {Draft, Issued, Overdue}` inline AND a separate
   "no recorded payments" guard. Anything else throws.
3. `InvoiceService.ReevaluateAsync(tenantId, invoiceId)` — recomputes
   from `InvoiceStatus.ComputeStatus(currentStatus, totalAmount,
   paidSum, dueDate, now)` and writes the result via
   `IInvoiceRepository.UpdateStatusAsync`.

`PaymentService.CreateAsync` also drives a transition: after recording
a payment it calls `ComputeStatus` and writes the new status. The
guard `InvoiceStatus.AcceptsPayments(invoice.Status)` blocks payments
against Draft / Paid / Voided / Refunded / PartiallyRefunded.

Refund transitions live inside `InvoiceService.RefundAsync` and use
`InvoiceStatus.AcceptsRefunds`. They derive Paid → PartiallyRefunded
and PartiallyRefunded → Refunded based on the cumulative refunded
total.

### Are invalid transitions blocked consistently?

Mostly yes — every entry point that mutates `Status` runs a guard
before calling `UpdateStatusAsync`. But the guards are duplicated
across `IssueAsync` and `VoidAsync` and the engine that drives them
(`InvoiceStatus.ComputeStatus`) is a different surface from the
imperative checks. TBS-B05 introduces a central
`InvoiceLifecycleService` so the same allowed-transition graph drives
both the imperative actions (Issue, Void, Mark Overdue) and the
recompute path.

### Is there overdue logic?

Partial. `ComputeStatus` derives Overdue when an invoice has no
payments and the due date has passed, and `ReevaluateAsync` is the
only way to actually flip an invoice into Overdue today. There is no
dedicated `MarkOverdueAsync`, no batch operation, and no scheduler.
TBS-B05 adds all three.

There is also a subtle bug in the current `ComputeStatus`: a
PartiallyPaid invoice that has gone past its due date is currently
recomputed back to PartiallyPaid (because `paidSum > 0` is checked
before the past-due check). The TBS-B05 spec requires this case to
land in / stay in Overdue, so `ComputeStatus` must be tightened.

### Is there a void path? Can payment status conflict with lifecycle?

There is a void path on `InvoiceService.VoidAsync` and the matching
controller endpoint `POST /api/invoices/{id}/void`. It is hardened
against voiding invoices that have any recorded payments — the spec
prefers this behaviour, so we keep it.

Payment status (`PaymentService.RecordedStatus = "Recorded"`,
`"Voided"` reserved) is independent of invoice lifecycle status. The
two never directly conflict because `PaymentService` always recomputes
the parent invoice status after each successful insert, and Voided
payments are excluded from the paid-sum that drives the recompute.

### Existing tests

Domain tests:

- `InvoiceStatusMachineTests` — exhaustive `ComputeStatus` matrix.
- `InvoiceServiceTransitionTests` — Issue, Void, Reevaluate happy
  paths + rejections.
- `PaymentRecordingTests`, `PaymentServiceTests` — payment driven
  transitions (Issued → PartiallyPaid → Paid, overpayment guards).
- `InvoiceRefundTests` — refund driven transitions to
  PartiallyRefunded / Refunded.
- `InvoiceManagementCoreTests`, `CustomerServiceTests` — pre-B05
  coverage.

API tests live in `tests/TenantBilling.Tests/` (pre-B05 count: 31/31
passing as of TBS-B04).

### Existing scheduler / hosted services

None. There is no `BackgroundService`, no Hangfire, no Quartz —
nothing periodic. TBS-B05 introduces the first one
(`InvoiceOverdueHostedService`) gated entirely on appsettings so it
stays disabled by default in CI / tests.

### Existing tenant middleware

`TenantResolutionMiddleware` parses `X-Tenant-Id` on `/api/*` and
short-circuits with HTTP 400 when the header is missing. All
controller actions read the tenant via the scoped `ITenantContext`.
TBS-B05 preserves this — the new mark-overdue endpoints scope by
header just like the rest. The batch endpoint does too (operators
target a specific tenant) and the hosted scheduler runs across all
tenants explicitly (no `X-Tenant-Id`, runs in a server-scoped scope).

### Existing refund / void methods

- `InvoiceService.RefundAsync` — recorded above.
- `InvoiceService.VoidAsync` — recorded above.
- `IRefundRepository` — already exists with `AddAsync`, used by the
  refund flow.

We do not extend or change refund behaviour in this block.

## 2. Implementation Steps

1. **Lifecycle exceptions + state machine.** Introduced
   `InvoiceLifecycleExceptions.cs` defining three typed exceptions —
   `InvalidInvoiceTransitionException`, `UnknownInvoiceStatusException`,
   `InvalidInvoiceStateException` — all deriving `InvalidOperationException`
   for back-compat with existing callers / test assertions that catch
   the base type. Added `InvoiceLifecycleService` with the full
   allowed-transition graph (Draft → Issued/Voided; Issued →
   PartiallyPaid/Paid/Overdue/Voided; PartiallyPaid →
   Paid/Overdue/Voided; Overdue → PartiallyPaid/Paid/Voided; plus the
   refund continuations Paid → PartiallyRefunded/Refunded and
   PartiallyRefunded → Refunded). The service exposes `CanTransition`,
   `ValidateTransition`, `IsTerminal`, `CanAcceptPayment`,
   `CanBeVoided`, and `CanBeMarkedOverdue`.
2. **ComputeStatus tightened.** `InvoiceStatus.ComputeStatus` now
   evaluates the past-due predicate before the partial-paid predicate.
   A PartiallyPaid invoice past its due date now resolves to Overdue
   instead of silently rolling back to PartiallyPaid. Full payment
   (Paid) and zero payment (still Issued/Overdue) cases are unchanged.
3. **InvoiceService refactor.** `IssueAsync`, `VoidAsync`, and
   `ReevaluateAsync` now route their structural status checks through
   the lifecycle engine via `ValidateTransition`. `VoidAsync` keeps
   its additional payment-existence guard, but it now raises
   `InvalidInvoiceStateException` rather than a bare
   `InvalidOperationException`. `ReevaluateAsync` re-validates the
   computed target through the engine as defence in depth.
4. **MarkOverdueAsync (single).** Validates ownership (cross-tenant /
   missing returns null) and runs the engine's `CanBeMarkedOverdue`
   gate plus the future-due gate against a pre-read invoice for
   accurate diagnostic 400 responses. The actual transition is
   performed via `IInvoiceRepository.TryMarkOverdueAsync` (the same
   conditional update used by the batch path), so a concurrent
   payment landing between the pre-read and the write cannot be
   silently overwritten. When the conditional update misses, the
   service re-reads the row to surface the freshest state: a
   vanished row maps to 404, a row whose new status no longer fits
   the eligibility predicate raises `InvalidInvoiceTransitionException`
   so the API returns 400 with the truthful current status (not a
   stale "transitioned to Overdue" success).
5. **Repository — eligible-overdue lookup.** Added
   `IInvoiceRepository.GetInvoicesEligibleForOverdueAsync(tenantId?,
   nowUtc, take, ct)`. EF implementation filters
   `Status in (Issued, PartiallyPaid)` AND `DueDate < nowUtc.Date`
   (date-boundary semantics so the batch path agrees bit-for-bit with
   the single-invoice rule and `ComputeStatus`), ordered by oldest
   DueDate. The optional `tenantId` parameter scopes the query for
   the per-tenant operator endpoint; passing `null` does the
   cross-tenant sweep needed by the scheduler. In-memory mirrors the
   same predicate + ordering. A second method
   `TryMarkOverdueAsync(tenantId, invoiceId, nowUtc, ct)` performs a
   conditional update against the same eligibility predicate
   `(TenantId, Id, Status in {Issued, PartiallyPaid}, DueDate <
   nowUtc.Date)`. On a relational provider it is a single-statement
   `ExecuteUpdateAsync` — the WHERE clause and the SET happen in
   one SQL UPDATE wrapped in the provider's implicit transaction,
   so a concurrent transaction that has just committed Status=Paid
   will be observed by the WHERE filter and the UPDATE affects 0
   rows (true atomic CAS, no row-version column required). If the
   provider is not relational (the InMemory provider used in
   integration tests, which does not faithfully execute
   `ExecuteUpdateAsync`), the method falls back to a tracked-load
   + `SaveChanges` pair that re-checks the same predicate at write
   time — best-effort, but fine for tests where there is no real
   concurrency. Either path returns null when the predicate did
   not hold. This is what makes the batch loop race-safe against
   concurrent payments / voids — see step 6.
6. **MarkEligibleOverdueAsync (batch).** Pulls eligible invoices,
   then for each candidate calls `TryMarkOverdueAsync` so the
   eligibility predicate is re-checked at write time. Three counters
   feed the response:
   `updatedCount` (predicate held, transition applied),
   `skippedCount` (predicate did NOT hold at write time — the row
   raced to a newer valid state like Paid via a concurrent payment;
   we deliberately do not overwrite), and
   `failedCount` (an unexpected `InvalidOperationException` thrown
   from the per-row code, captured in `failures`). Per-invoice
   failure isolation: one bad row does not abort the batch.
7. **Hosted scheduler.** New `InvoiceOverdueHostedService`
   (`BackgroundService`) gated by `InvoiceLifecycle:OverdueJobEnabled`
   (default `false`). When enabled it polls
   `InvoiceLifecycle:OverdueJobIntervalMinutes` minutes (default 15)
   and processes up to `InvoiceLifecycle:OverdueBatchSize` invoices
   (default 200) per tick. When disabled the service parks for the
   process lifetime — no polling, no resource use, no test
   interference. The job opens its own DI scope per tick so it picks
   up scoped repositories cleanly.
8. **Controller endpoints + DTOs.** Added
   `InvoiceLifecycleResponse(Id, InvoiceNumber, PreviousStatus,
   CurrentStatus, UpdatedAt, IssuedAt?, Message?)` and
   `OverdueBatchResponse(UpdatedCount, SkippedCount, FailedCount,
   Failures)`.
   Wired two new actions on `InvoicesController`:
   `POST /api/invoices/{id}/mark-overdue` and
   `POST /api/invoices/mark-overdue` (`take` query, clamped 1-1000,
   tenant-scoped via `ITenantContext`).
9. **PaymentService.** No code changes needed — it already routes
   through `InvoiceStatus.AcceptsPayments` and `ComputeStatus`. The
   tightened ComputeStatus from step 2 automatically keeps Overdue
   invoices in Overdue after a partial payment. Verified by
   regression tests.
10. **Tests.** New: `InvoiceLifecycleServiceTests` (transition matrix
    + helpers), `InvoiceOverdueTests` (single mark-overdue),
    `InvoiceOverdueBatchTests` (batch sweep), regression facts on
    `PaymentRecordingTests` (partial / full payment on past-due),
    `InvoicesMarkOverdueApiTests` (HTTP surface). Updated:
    `InvoiceServiceTransitionTests` switched its `ThrowsAsync` to
    `ThrowsAnyAsync` to match the new typed subclasses;
    `PaymentServiceTests.Payment_to_overdue_invoice_marks_PartiallyPaid`
    renamed to `Partial_payment_to_overdue_invoice_keeps_Overdue` and
    its assertion updated to match the new ComputeStatus rule.
11. **Migration check.** Status is already persisted as
    `nvarchar(32)` (`b.Property(x => x.Status).IsRequired()
    .HasMaxLength(32)` in `TenantBillingDbContext`). No new columns,
    no length change. No EF migration required for TBS-B05.
12. **Validation.** `dotnet build` clean (0 warnings, 0 errors).
    Domain tests 217/217 green, integration tests 37/37 green. Smoke
    against the live workflow returned 400 for a malformed id, 200
    `{updatedCount:0,skippedCount:0,failedCount:0,failures:[]}` for
    the empty batch, and the new endpoints are reachable from
    `http://localhost:5001`.

## 3. Files Created / Modified

### Created

- `services/tenant-billing-api/src/TenantBilling.Domain/Services/InvoiceLifecycleService.cs`
- `services/tenant-billing-api/src/TenantBilling.Domain/Services/InvoiceLifecycleExceptions.cs`
- `services/tenant-billing-api/src/TenantBilling.Api/Hosting/InvoiceOverdueHostedService.cs`
- `services/tenant-billing-api/src/TenantBilling.Api/Hosting/InvoiceLifecycleOptions.cs`
- `services/tenant-billing-api/src/TenantBilling.Api/Contracts/InvoiceLifecycleDtos.cs` (`InvoiceLifecycleResponse`, `OverdueBatchResponse`, `OverdueBatchFailureDto`)
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/InvoiceLifecycleServiceTests.cs`
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/InvoiceOverdueTests.cs`
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/InvoiceOverdueBatchTests.cs`
- `services/tenant-billing-api/tests/TenantBilling.Tests/InvoicesMarkOverdueApiTests.cs`

### Modified

- `services/tenant-billing-api/src/TenantBilling.Domain/Entities/InvoiceStatus.cs` — past-due dominates partial in `ComputeStatus`.
- `services/tenant-billing-api/src/TenantBilling.Domain/Services/IInvoiceService.cs` — `MarkOverdueAsync`, `MarkEligibleOverdueAsync`, `OverdueBatchResult`, `OverdueBatchFailure`.
- `services/tenant-billing-api/src/TenantBilling.Domain/Services/InvoiceService.cs` — engine wiring for Issue/Void/Reevaluate; new mark-overdue (single + batch).
- `services/tenant-billing-api/src/TenantBilling.Domain/Repositories/IInvoiceRepository.cs` — `GetInvoicesEligibleForOverdueAsync` + `TryMarkOverdueAsync`.
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Repositories/InvoiceRepository.cs` — EF implementations (eligibility query + conditional update).
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/Fakes/InMemoryInvoiceRepository.cs` — in-memory implementations (mirrors EF predicate semantics).
- `services/tenant-billing-api/src/TenantBilling.Api/Controllers/InvoicesController.cs` — two new POST endpoints.
- `services/tenant-billing-api/src/TenantBilling.Api/Program.cs` — DI registration for engine, hosted service, options binding.
- `services/tenant-billing-api/src/TenantBilling.Api/appsettings.json` — `InvoiceLifecycle` block (job disabled by default).
- `services/tenant-billing-api/tests/TenantBilling.Tests/DomainTestHost.cs`,
  `tests/TenantBilling.Tests/ApiDomainTestHost.cs`,
  `tests/TenantBilling.Domain.Tests/InvoiceServiceTransitionTests.cs`,
  `tests/TenantBilling.Domain.Tests/InvoiceManagementCoreTests.cs`,
  `tests/TenantBilling.Domain.Tests/InvoiceRefundTests.cs` — pass `new InvoiceLifecycleService()` to the updated `InvoiceService` constructor.
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/InvoiceServiceTransitionTests.cs` — `ThrowsAnyAsync` to accept the new typed subclasses.
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/PaymentServiceTests.cs` — partial payment on past-due now asserts Overdue.
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/PaymentRecordingTests.cs` — added regression tests for partial / full payment on past-due invoice.
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/InvoiceStatusMachineTests.cs` — `Overdue_with_partial_payment_becomes_PartiallyPaid` flipped to expect Overdue; two new past-due-vs-partial cases added.

## 4. API Changes

### `POST /api/invoices/{id}/mark-overdue`

Tenant-scoped (`X-Tenant-Id` required). Marks a single invoice as
Overdue.

- **200 OK** — `InvoiceLifecycleResponse { id, invoiceNumber,
  previousStatus, currentStatus = "Overdue", updatedAt, issuedAt? }`.
- **400 Bad Request** — invoice id missing/empty, due date in the
  future, lifecycle gate fails (e.g. invoice already Paid/Voided/
  Refunded/Overdue).
- **404 Not Found** — invoice missing or owned by another tenant.

### `POST /api/invoices/mark-overdue?take=N`

Tenant-scoped batch sweep. Operator-style endpoint that processes the
oldest-first eligible invoices (Issued/PartiallyPaid past due, where
"past due" means strictly before the current UTC day — invoices due
"any time today" are not yet overdue) for the calling tenant.

- `take` is clamped to `[1, 1000]`; default 200.
- **200 OK** — `OverdueBatchResponse { updatedCount, skippedCount,
  failedCount, failures: [ { tenantId, invoiceId, reason } ... ] }`.
  `skippedCount` covers candidates whose state changed between the
  eligibility query and the conditional update (e.g. a payment
  arrived first); the row is left in its newer valid state.
- The hosted scheduler invokes the same underlying
  `InvoiceService.MarkEligibleOverdueAsync` with `tenantId = null`
  for the cross-tenant sweep.

No existing endpoint shapes change.

## 5. Database Changes

None. Invoice `Status` is already mapped as `nvarchar(32)` and the
new statuses (Overdue, PartiallyRefunded, Refunded) were already in
the catalogue from earlier blocks. The eligible-overdue query
filters on `Status` + `DueDate`, both of which already have indexes
from B01. No `dotnet ef migrations add` step required.

## 6. Validation Results

- **Build.** `dotnet build TenantBilling.sln` — 0 warnings, 0 errors,
  ~22s.
- **Domain tests.** `tests/TenantBilling.Domain.Tests` — 217/217
  passed (was 209 in B04; +8 net from the new TBS-B05 cases including
  date-boundary and TOCTOU regressions).
- **Integration tests.** `tests/TenantBilling.Tests` — 37/37 passed
  (was 31 in B04; +6 from the new mark-overdue API cases).
- **Smoke against the live workflow** (`http://localhost:5001`).
  - `POST /api/invoices/00000…0/mark-overdue` → 400 with
    `{"detail":"InvoiceId is required."}` (GuidEmpty validator
    fires — endpoint reachable, validation wired).
  - `POST /api/invoices/mark-overdue?take=10` → 200 with
    `{"updatedCount":0,"skippedCount":0,"failedCount":0,"failures":[]}`
    (no eligible invoices in seed for the smoke tenant).
- **Hosted job.** Disabled by default in `appsettings.json`; the
  WebApplicationFactory tests confirm the API boots without the job
  interfering with the per-test InMemory database.

## 7. Known Gaps / Notes

- **Lifecycle engine is structural.** It encodes _which_ status
  changes are legal, but service-layer guards still own the
  contextual checks (no payments before voiding; due date in the
  past before marking overdue; sufficient refund headroom).
  Consolidating those into the engine would couple it to
  repositories and was out of scope for B05.
- **Date-boundary unification across single + batch.** Both the
  single-invoice `MarkOverdueAsync` rule (`now.Date > dueDate.Date`),
  the eligibility query (`DueDate < nowUtc.Date`), the conditional
  `TryMarkOverdueAsync` predicate, and `InvoiceStatus.ComputeStatus`
  use the same date-only boundary. An invoice that is "due any time
  today UTC" is NOT yet overdue. The earlier draft had a time-based
  predicate in the batch path which would have flagged "due 9 AM
  today" at 4 PM today — that disagreement is now closed and
  regression-tested in `InvoiceOverdueBatchTests
  .MarkEligibleOverdueAsync_does_not_mark_invoices_due_today`.
- **Race-safe transition (single + batch).** Both the single-invoice
  endpoint (`InvoiceService.MarkOverdueAsync`) and the batch loop
  (`MarkEligibleOverdueAsync`) perform the actual write through
  `IInvoiceRepository.TryMarkOverdueAsync` rather than a
  re-validate-then-`UpdateStatusAsync` pair. On the production
  relational provider this is a single-statement `ExecuteUpdateAsync`
  whose WHERE clause embeds the eligibility predicate; the database
  decides atomically whether to write or skip, so a concurrently
  committed Paid status is observed by the WHERE and the UPDATE
  affects 0 rows (a true atomic CAS — no row-version column
  required). The InMemory test provider does not faithfully execute
  `ExecuteUpdateAsync`, so on non-relational providers the method
  falls back to a tracked-load + `SaveChanges` that re-checks the
  same predicate (best-effort; sufficient for tests where there is
  no real concurrency). Either path returns null on mismatch. The
  batch records a `skippedCount`; the single endpoint re-reads the
  row and either returns 404 (vanished) or raises
  `InvalidInvoiceTransitionException` so the API surfaces the truthful
  current status (400) instead of falsely reporting a transition.
  Regression-tested in
  `InvoiceOverdueBatchTests.TryMarkOverdueAsync_returns_null_when_status_changed_concurrently`,
  `..._when_status_is_terminal`, and
  `InvoiceOverdueTests.MarkOverdueAsync_throws_when_status_changes_between_pre_read_and_CAS`.
- **Hosted scheduler is intentionally minimal.** No leader election,
  no distributed lock — fine for single-instance deployments. If a
  multi-instance topology is introduced, gate the job behind a
  database advisory lock (`SELECT pg_try_advisory_lock(…)`-style
  on SQL Server use `sp_getapplock`) or move to Hangfire / Quartz.
- **Re-marking already-Overdue invoices is a no-op rejection** by the
  engine (no Overdue→Overdue self-loop). The single-invoice endpoint
  surfaces this as a 400. The batch endpoint does not enumerate
  Overdue invoices in the first place because they fail the
  `Status in (Issued, PartiallyPaid)` filter in
  `GetInvoicesEligibleForOverdueAsync`, so they never reach the
  per-row try/catch and are never reported as failures. This matches
  the spec: the sweep is for invoices that need to _become_ Overdue.
- **Cross-tenant 404 is intentional.** Mark-overdue treats a
  cross-tenant invoice id identically to a missing invoice — the
  service returns null and the controller responds with 404. This
  prevents id-existence leakage to a non-owner tenant. Documented in
  `InvoiceOverdueTests` and `InvoicesMarkOverdueApiTests`.

### Deviations from the session plan

All twelve tasks (T01-T12) landed as designed. Two additive deltas
were introduced mid-flight, both as a direct response to architect
review findings:

1. **`OverdueBatchResponse` gained a `SkippedCount` field.** Plan T08
   listed `OverdueBatchResponse(UpdatedCount, FailedCount,
   Failures?)`. The race-safety fix needed a third counter to
   distinguish "raced to a newer state — no action needed" from a
   genuine failure. The new field is additive, defaults to 0, and
   does not break existing JSON consumers.
2. **New repo method `TryMarkOverdueAsync`.** Plan T05 only listed
   `GetInvoicesEligibleForOverdueAsync`. The batch path originally
   re-validated via the engine and called the generic
   `UpdateStatusAsync`, which is a non-atomic read-then-write and
   could overwrite a concurrently-paid invoice. Switching the batch
   to a conditional update repo method closed that TOCTOU window.

A pre-existing test
`PaymentServiceTests.Payment_to_overdue_invoice_marks_PartiallyPaid`
was renamed to `Partial_payment_to_overdue_invoice_keeps_Overdue`
and its assertion flipped to expect `Overdue`, matching the
tightened `ComputeStatus` rule from T03. Five other
`ThrowsAsync<InvalidOperationException>` assertions in
`InvoiceServiceTransitionTests` switched to `ThrowsAnyAsync<…>` so
they accept the new typed subclasses; behaviour unchanged.
