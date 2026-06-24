# COM-B06 — Invoicing, Account Standing & Provider Reconciliation

## 1. Summary

Adds Commerce-owned invoicing, payment-record persistence, account-standing
evaluation, subscription/provider reconciliation, and a provider event
replay endpoint. Extends the Stripe webhook flow so payment-intent /
invoice-payment events produce `Payment` + `PaymentAttempt` rows and
trigger subscription reconciliation that writes `SubscriptionChange`
audit rows. All persistence remains MySQL via Pomelo. Build is green;
all 81 unit/integration tests pass.

## 2. Stories Completed

- COM-B06-S01 — Invoice + InvoiceLine domain model and creation API.
- COM-B06-S02 — Payment + PaymentAttempt domain model.
- COM-B06-S03 — Provider event → Payment mapping (extends Stripe webhook).
- COM-B06-S04 — Subscription/provider reconciliation (writes
  `SubscriptionChange` rows).
- COM-B06-S05 — `AccountStanding` engine with `AccountStandingPolicy`.
- COM-B06-S06 — Provider event replay endpoint.

## 3. Architecture Implemented

- **Domain (`Commerce.Domain`)**
  - `Invoicing.Invoice`, `Invoicing.InvoiceLine`,
    `Invoicing.InvoiceNumber`, `Invoicing.Enums.{InvoiceStatus,InvoiceLineKind}`.
  - `Payments.Payment`, `Payments.PaymentAttempt`,
    `Payments.Enums.{PaymentStatus,PaymentAttemptOutcome}`.
  - `AccountStanding.AccountStanding`, `AccountStanding.AccountStandingPolicy`,
    `AccountStanding.AccountStandingState`.
- **Application (`Commerce.Application`)**
  - Service abstractions: `IInvoiceService`, `IPaymentRecordingService`,
    `IPaymentRecordQueryService`, `IAccountStandingService`,
    `ISubscriptionReconciliationService`, `IProviderEventReplayService`.
  - FluentValidation: `CreateInvoiceRequestValidator`.
  - Exceptions: `InvoiceNotOpenException`,
    `ProviderEventReprocessNotAllowedException`,
    `FinancialRecordConflictException`.
- **Infrastructure (`Commerce.Infrastructure`)**
  - `InvoiceNumberGenerator`, `InvoiceService`, `PaymentRecordingService`,
    `PaymentRecordQueryService`, `AccountStandingService`,
    `SubscriptionReconciliationService`, `ProviderEventReplayService`.
  - EF configurations for new tables; one migration
    `20260424031253_InvoicingAccountStanding`.
  - `PaymentWebhookService.ApplyAsync` extended to dispatch payment and
    subscription events to the recording + reconciliation services.
  - `StripeEventTranslator` extended with payment_intent and invoice
    payment events plus rich amount/currency/failure/subscription-status
    fields on `NormalizedProviderEvent`.
- **API (`Commerce.Api`)**
  - `InvoicesController`, `BillingAccountInvoicesController`,
    `PaymentRecordsController`, `AccountStandingController`,
    `ProviderEventsReprocessController`.
  - `ProblemDetailsExceptionMiddleware` maps the new exceptions.

## 4. Files Created/Changed

Created:

- `src/Commerce.Domain/Invoicing/{Invoice,InvoiceLine,InvoiceNumber}.cs`
- `src/Commerce.Domain/Invoicing/Enums/{InvoiceStatus,InvoiceLineKind}.cs`
- `src/Commerce.Domain/Payments/{Payment,PaymentAttempt}.cs`
- `src/Commerce.Domain/Payments/Enums/PaymentStatus.cs` (PaymentAttemptOutcome reused)
- `src/Commerce.Domain/AccountStanding/{AccountStanding,AccountStandingPolicy,AccountStandingState}.cs`
- `src/Commerce.Application/Common/Exceptions/InvoicingExceptions.cs`
- `src/Commerce.Application/Invoicing/**` (abstractions, validators)
- `src/Commerce.Application/AccountStanding/**`
- `src/Commerce.Application/Payments/Abstractions/IPaymentRecordServices.cs`
- `src/Commerce.Contracts/{Invoicing,AccountStanding}/**` and
  `src/Commerce.Contracts/Payments/PaymentRecordDtos.cs`
- `src/Commerce.Infrastructure/Invoicing/**`
- `src/Commerce.Infrastructure/AccountStanding/**`
- `src/Commerce.Infrastructure/Payments/Mapping/PaymentRecordMapping.cs`
- `src/Commerce.Infrastructure/Payments/Services/{PaymentRecordingService,
   PaymentRecordQueryService, ProviderEventReplayService}.cs`
- `src/Commerce.Infrastructure/Subscriptions/Services/SubscriptionReconciliationService.cs`
- `src/Commerce.Infrastructure/Persistence/Configurations/{InvoiceConfigurations,
   PaymentRecordConfigurations, AccountStandingConfigurations}.cs`
- `src/Commerce.Infrastructure/Persistence/Migrations/20260424031253_InvoicingAccountStanding.{cs,Designer.cs}`
- `src/Commerce.Api/Controllers/{Invoicing,AccountStanding}/**`
- `src/Commerce.Api/Controllers/Payments/PaymentRecordsController.cs`
- `tests/Commerce.Tests/Invoicing/**`,
  `tests/Commerce.Tests/AccountStanding/**`,
  `tests/Commerce.Tests/Payments/ProviderEventReplayServiceTests.cs`
- `analysis/invoicing-account-standing.sql` (idempotent migration script)

Modified:

- `src/Commerce.Application/Payments/Abstractions/IPaymentProvider.cs`
  (extended `NormalizedProviderEvent` and `NormalizedProviderEventKind`).
- `src/Commerce.Infrastructure/Payments/Stripe/StripeEventTranslator.cs`
  (translates the new payment_intent / invoice_payment events).
- `src/Commerce.Infrastructure/Payments/Services/PaymentWebhookService.cs`
  (dispatches to recording + reconciliation services).
- `src/Commerce.Infrastructure/Persistence/CommerceDbContext.cs`
  (DbSets for `Invoices`, `InvoiceLines`, `Payments`, `PaymentAttempts`,
  `AccountStanding`).
- `src/Commerce.Infrastructure/Persistence/Migrations/CommerceDbContextModelSnapshot.cs`
- `src/Commerce.Infrastructure/DependencyInjection.cs`
- `src/Commerce.Infrastructure/Commerce.Infrastructure.csproj`
  (`InternalsVisibleTo` for `Commerce.Tests`).
- `src/Commerce.Api/Middleware/ProblemDetailsExceptionMiddleware.cs`
- `tests/Commerce.Tests/Payments/PaymentTestHost.cs`
  (exposes the Stripe translator for replay tests).

## 5. Database/Migration Changes

Migration: `20260424031253_InvoicingAccountStanding`.

New tables:

- `invoices` — keyed on `Id`; unique index on `InvoiceNumber`; FK to
  `billing_accounts`; nullable FK to `subscriptions`; provider correlation
  columns (`Provider`, `ProviderInvoiceId`).
- `invoice_lines` — FK to `invoices` (cascade); keyed on `Id`.
- `payments` — keyed on `Id`; FKs to `billing_accounts`, `invoices`
  (nullable, restrict), `subscriptions` (nullable, restrict); unique
  index `ux_payments_provider_provider_payment_id` on
  `(Provider, ProviderPaymentId)` (NULL-tolerant).
- `payment_attempts` — keyed on `Id`; FKs to `payments` (nullable),
  `billing_accounts`, `subscriptions`; **unique** index
  `ux_payment_attempts_provider_event_id` on `(Provider, ProviderEventId)`
  to enforce DB-level idempotency for replay/concurrent webhook delivery.
  Multiple NULL `ProviderEventId`s are allowed (legacy/internal triggers).
- `account_standing` — singleton-per-account via unique index on
  `BillingAccountId`; stores `Status`, `Reason`, `GracePeriodEndsAt`,
  `PastDueSince`, `SuspendedAt`, audit timestamps.

Idempotent SQL: `services/Commerce/analysis/invoicing-account-standing.sql`
(regenerated via `dotnet ef migrations script --idempotent`).

## 6. API Endpoints Added

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/commerce/invoices` | Create an `Open` invoice with lines. |
| GET  | `/api/commerce/invoices/{id}` | Fetch a single invoice. |
| GET  | `/api/commerce/invoices` | List invoices (optional billing-account filter). |
| GET  | `/api/commerce/billing-accounts/{accountId}/invoices` | Per-account invoice list. |
| GET  | `/api/commerce/payments` | List payment records (filter by billing-account / subscription). |
| POST | `/api/commerce/billing-accounts/{accountId}/account-standing/evaluate` | Recompute and persist standing. |
| GET  | `/api/commerce/billing-accounts/{accountId}/account-standing` | Read the latest standing snapshot. |
| POST | `/api/commerce/payments/event-logs/{id}/reprocess` | Replay a stored provider event. |

## 7. Invoice Domain Model

`Invoice` is the aggregate root. Invariants:

- Currency required and stored as 3-char code; total/subtotal/balance are
  derived from line subtotals + payments.
- Lines may be added only while the invoice is `Draft`/`Open` and require
  `Quantity >= 1` and non-negative unit amount.
- `Open()` transitions Draft→Open and freezes line edits.
- `RegisterPayment(amount,...)` updates `BalanceDueMinor`. Reaching zero
  flips the status to `Paid` and records `PaidAtUtc`.
- `Void()` is allowed only on Draft/Open and is irreversible.
- Provider correlation pair `(Provider, ProviderInvoiceId)` lets the
  webhook flow attach incoming payments to local invoices when present.

## 8. Payment Record Model

`Payment` is an independent record of a money movement (an invoice may
have many payments; subscription-only flows allow `InvoiceId == null`).

- Created with `Pending` / `Succeeded` / `Failed` based on the inbound
  event; `MarkSucceeded` / `MarkFailed` mutate state with timestamps.
- `AttachInvoice(invoiceId, nowUtc)` is called on a best-effort match by
  `(Provider, ProviderInvoiceId)`.
- `(Provider, ProviderPaymentId)` carries a unique index so webhook
  retries dedupe to the same `Payment` row.

`PaymentAttempt` is an immutable per-event audit row (one per provider
event the recording service consumes). Idempotency:

- Pre-check by `(Provider, ProviderEventId)` short-circuits duplicates.
- DB-level guarantee via unique index
  `ux_payment_attempts_provider_event_id` defends against concurrent
  webhook + replay races.

## 9. Provider Reconciliation Behavior

`PaymentWebhookService.ApplyAsync` dispatches by `NormalizedProviderEventKind`:

- `PaymentIntentSucceeded`, `InvoicePaymentSucceeded` →
  `PaymentRecordingService.RecordFromEventAsync(succeeded: true)` then
  `SubscriptionReconciliationService.ReconcileFromEventAsync`.
- `PaymentIntentFailed`, `InvoicePaymentFailed` →
  same pair with `succeeded: false`.
- `SubscriptionCreated/Updated/Deleted` → reconciliation only.

`SubscriptionReconciliationService` resolves the local `Subscription`
(by id or `(Provider, ProviderSubscriptionId)` mapping), maps
`ProviderSubscriptionStatus` → `SubscriptionStatus`, and on a real change
applies the appropriate domain transition (`MarkActive`, `Suspend`,
`Cancel`, etc.) and writes a `SubscriptionChange` row. Returns `true`
when something changed (caller commits via `SaveChangesAsync`).

## 10. Account Standing Behavior

`AccountStandingService.Evaluate` is an internal pure function over
`(BillingAccount, Subscriptions, OpenInvoices, nowUtc, AccountStandingPolicy)`
producing `(Status, Reason, GracePeriodEndsAt, PastDueSince, SuspendedAt)`.
States:

- `Closed` if the account itself is closed.
- `Cancelled` if there are no active/trial subscriptions.
- `Trialing` if at least one subscription is in trial and no overdue
  invoices exist.
- `Suspended` once an unpaid invoice has aged past the policy's grace
  window.
- `PastDue` once an unpaid invoice is past its due date but still inside
  the grace window (also publishes the grace-end timestamp).
- `Good` otherwise.

The policy (`AccountStandingPolicy.Default`) controls the grace window
length. `EvaluateAndPersistAsync` upserts the singleton row per
`BillingAccountId`.

## 11. Provider Event Replay Behavior

`ProviderEventReplayService.ReprocessAsync(eventLogId, ct)`:

- Loads the `PaymentProviderEventLog`. Allowed only when current
  processing status is `Failed`, `Received`, or `Ignored`; any other
  status throws `ProviderEventReprocessNotAllowedException`. (Operator
  replays of `Ignored` rows are explicitly supported because the original
  processing may have short-circuited before recording side effects.)
- Re-translates the stored payload via the provider registry; on
  translation failure marks the log `Failed` and returns.
- Dispatches into the same recording / reconciliation services used by
  the live webhook path. Idempotency is preserved by the
  `ux_payment_attempts_provider_event_id` unique index plus the
  pre-check, and by the unique payment index on
  `(Provider, ProviderPaymentId)`.
- Marks the log `Processed` if anything changed, otherwise `Ignored`,
  and on exception marks `Failed` with the captured message.

## 12. Validation Rules Implemented

`CreateInvoiceRequestValidator`:

- `BillingAccountId` is required (non-empty Guid).
- `Currency` must be exactly 3 ASCII letters.
- `Lines` must be non-empty; each line: `Description` required (≤200),
  `Quantity >= 1`, `UnitAmountMinor >= 0`.

API surface returns ProblemDetails for validation failures via the
existing `ProblemDetailsExceptionMiddleware`.

## 13. Tests Added

xUnit, in-memory EF (`UseInMemoryDatabase`), and `WebApplicationFactory`.

- `Invoicing.InvoiceDomainTests` — totals, line invariants, status
  transitions, `RegisterPayment` boundary conditions.
- `Invoicing.InvoiceServiceTests` — create/get/list happy paths,
  validator integration, invoice-number formatting, post-create
  uniqueness against persisted invoices.
- `Invoicing.PaymentRecordingServiceTests` — record success/failure,
  idempotency on `(Provider, ProviderEventId)`, missing customer
  handling, invoice attach by `ProviderInvoiceId`.
- `Invoicing.SubscriptionReconciliationServiceTests` — local-id and
  provider-mapping resolution, status transitions write
  `SubscriptionChange`, unknown subscription returns false.
- `Invoicing.InvoicingApiTests` — POST/GET/list endpoints including 400
  on bad input.
- `AccountStanding.AccountStandingEngineTests` — every branch of the
  evaluate function.
- `AccountStanding.AccountStandingApiTests` — POST evaluate + GET.
- `Payments.ProviderEventReplayServiceTests` — disallowed statuses,
  re-translation failure, applied-changes Processed transition,
  no-op Ignored transition, idempotency through replay.
- Existing `Payments.PaymentWebhookServiceTests` updated for the new
  event kinds and recording side effects.

Total: **81 tests, all passing**.

## 14. Validation Results

- `dotnet build` of the `Commerce.Tests` graph (and therefore all five
  src projects): **0 errors, 0 warnings**.
- `dotnet test` (filtered batches due to console-buffer limits in this
  environment) — combined: **81 passed, 0 failed, 0 skipped**.
- `dotnet ef migrations script --idempotent` regenerated to
  `analysis/invoicing-account-standing.sql` (1674 lines, includes the
  new tables, the unique payment-attempt event index, and uses
  `IF NOT EXISTS` guards on every step).

## 15. Known Gaps or Deferred Items

- Account-standing evaluation considers overdue-invoice signals; a
  failed-payment-only signal (no open invoice yet) does not by itself
  flip an account to `PastDue`. This is consistent with the policy and
  the constraint that COM-B06 owns no dunning behavior, but is worth
  flagging if a future story wants attempt-driven standing changes.
- No multi-process concurrency test for the unique attempt index — the
  in-memory provider does not enforce indexes. The constraint is exercised
  by the idempotency unit test plus the explicit pre-check, and by the
  generated MySQL DDL.
- Currency conversion / FX: not in scope; all amounts stay in their
  invoice currency.

## 16. Confirmation of Strict Exclusions

The following are explicitly **not** implemented:

- Tax calculation, refunds, credit memos, coupons/promotions.
- Dunning workflows or scheduled reminders.
- Email / notification delivery.
- Entitlement / feature-flag gating.
- Identity / user management.
- Any UI surface.
- LegalSynq integration.

## 17. Code Review Outcomes

Architect review (responsibility=evaluate_task) returned two actionable
findings, both addressed in this commit:

1. **Idempotency at DB level** — the original migration created a
   *non-unique* index on `payment_attempts.ProviderEventId`. Replaced
   with a unique composite index `(Provider, ProviderEventId)` named
   `ux_payment_attempts_provider_event_id`, with NULL tolerance for
   internal/legacy attempts. Migration, snapshot, configuration, and
   regenerated idempotent SQL are all aligned. Service-level pre-check
   remains as a fast path.
2. **Replay contract mismatch** — `ProviderEventReplayService` accepts
   `Failed`, `Received`, **and** `Ignored`. Updated the XML-doc to match
   the implementation and noted the `Ignored`-replay use case
   (operator-driven recovery after early short-circuit).

Architect's third comment — that account standing does not yet react to
isolated failed-payment signals — is recorded as a deferred item under
section 15 because failed-payment-only triggers fall outside the
COM-B06 scope (no dunning).
