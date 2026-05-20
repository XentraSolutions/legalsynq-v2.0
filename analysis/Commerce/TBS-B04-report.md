# TBS-B04 Report — Payment Recording & Invoice Balance Logic

Service: `services/tenant-billing-api/` (standalone .NET 10 Tenant Billing API).

## 1. Codebase Analysis

The Tenant Billing service is a clean four-project layout under
`services/tenant-billing-api/`:

- `TenantBilling.Domain` — entities (`Customer`, `Invoice`, `Payment`,
  `Refund`), service interfaces (`I*Service`) and implementations, repository
  interfaces (`I*Repository`), and the typed-exception family for invariants.
- `TenantBilling.Infrastructure` — EF Core 10 + Pomelo MySQL provider,
  `TenantBillingDbContext`, repositories, `EfUnitOfWork`, design-time factory,
  and migrations.
- `TenantBilling.Api` — ASP.NET Core controllers, request/response DTOs,
  tenant-resolution middleware (`X-Tenant-Id` header), `Program.cs`
  composition root.
- `TenantBilling.Tests` (API integration) and `TenantBilling.Domain.Tests`
  (in-memory unit tests) — cover happy/edge paths.

Pre-TBS-B04 state of the payment slice:

- `Payment` entity already had `Id`, `TenantId`, `InvoiceId`, `Amount`,
  `Currency`, `Method`, `Status`, `TransactionReference`, `PaidAt`,
  `CreatedAt` but **no `Notes`** column.
- `PaymentService.CreateAsync` already enforced cross-tenant invoice safety
  (returns "not found" for both unknown and other-tenant invoices),
  invoice-status gates (Draft/Voided/Paid rejected), currency normalization,
  amount rounding to 2 decimals, overpayment detection, idempotent recovery
  on duplicate `TransactionReference`, and invoice-status promotion
  (Issued/PartiallyPaid/Paid). It **threw raw `InvalidOperationException`**
  in every guard path, which forced the controller layer to map every
  failure to the same HTTP status.
- `IPaymentRepository` only exposed `AddAsync` and `GetByIdAsync` — there
  was no list/paged query, no by-invoice query, no payments-sum helper, and
  no `UpdateAsync` (used by void flows in subsequent tickets).
- `PaymentsController` had a `Create` endpoint and a flat `List` endpoint
  returning everything for the tenant. There was no per-invoice payments
  endpoint and no payment-summary endpoint. Errors were caught as
  `InvalidOperationException` and uniformly mapped to 400.
- `CreatePaymentRequest` exposed `Status` to the client, allowing callers
  to forge any status string they wanted.
- `BillingApiTests` sent `Status = "succeeded"` on its happy path and
  asserted on the older response shape.
- A previously-undiscovered domain test file
  `tests/TenantBilling.Tests/Domain/PaymentServiceTests.cs` (sibling of
  `InvoiceServiceTests.cs`) covered amount rounding, blank-status default
  (`"Pending"`), and cross-tenant rejection — and was the source of the
  "default status" expectation that conflicted with the new spec.

## 2. Implementation Steps

1. **Report skeleton** (`analysis/TBS-B04-report.md`) created with the seven
   required sections.
2. **Typed exceptions + `Notes` column.** Added
   `src/TenantBilling.Domain/Services/PaymentExceptions.cs` containing
   `InvoiceNotFoundException`, `InvalidPaymentAmountException`,
   `InvalidInvoicePaymentStateException`, `OverpaymentException`, and
   `CurrencyMismatchException`. All five derive from
   `InvalidOperationException` so legacy `Assert.ThrowsAnyAsync<...>` (and
   any caller `catch (InvalidOperationException)`) keep working. Added
   `Notes` (nullable `string?`) to the `Payment` entity.
3. **Repository surface.** Extended `IPaymentRepository` with
   `UpdateAsync`, `GetByInvoiceIdAsync`,
   `ListAsync(filters, page, pageSize)`, `CountAsync(filters)`, and
   `SumRecordedPaymentsForInvoiceAsync` (sums non-voided payments for the
   balance-due calculation). Implemented in `PaymentRepository` (EF) with
   tenant-scoped queries and `AsNoTracking()` for read paths. Mirrored the
   same surface on `InMemoryPaymentRepository` so the domain tests keep
   compiling and exercising the same contract.
4. **Service surface.** Reworked `IPaymentService` and `PaymentService`:
   - Added a `notes` parameter (defaulted, trimmed, length-capped at 2000)
     to `CreateAsync`.
   - Default status on a blank/whitespace incoming `status` is now the new
     canonical `PaymentService.RecordedStatus = "Recorded"` constant
     (replaces the legacy `"Pending"` placeholder). An explicit non-blank
     status is preserved for back-compat with idempotency replays.
   - All guards now throw the matching typed exception from step 2.
   - New `ListPagedAsync` returns a `PaymentPage` record `(IReadOnlyList,
     total, page, pageSize)`; new `GetByInvoiceAsync` returns the per-
     invoice payment list (tenant-scoped); new
     `GetInvoicePaymentSummaryAsync` returns an `InvoicePaymentSummary`
     record `(InvoiceId, InvoiceNumber, InvoiceStatus, InvoiceTotal,
     TotalPaid, BalanceDue, Currency)` derived from the invoice plus the
     repository's payments-sum helper.
5. **API contracts (DTOs).** In `PaymentDtos.cs`:
   - Removed `Status` from `CreatePaymentRequest` (server-controlled now).
   - Added `Notes` (max 2000) to `CreatePaymentRequest` and to
     `PaymentResponse`. Currency length stays at 3.
   - Added `PaymentListResponse(IReadOnlyList<PaymentResponse> Items, int
     Page, int PageSize, int TotalCount, int TotalPages)`,
     `InvoicePaymentSummaryResponse`, and `RecordPaymentResponse(Payment
     PaymentResponse, InvoicePaymentSummaryResponse InvoiceSummary)`.
6. **Controllers.**
   - `PaymentsController.Create` now returns `RecordPaymentResponse`
     (the recorded payment **plus** the freshly-recomputed invoice
     summary), and maps each typed exception to the right HTTP status:
     `InvoiceNotFoundException` → 400 on `POST /payments` (the invoice id
     lives in the body, so an unresolvable id is a bad payload, and
     returning 400 also avoids leaking cross-tenant existence),
     `DuplicatePaymentReferenceException` → 409,
     `InvalidPaymentAmountException` / `CurrencyMismatchException` /
     `OverpaymentException` / `InvalidInvoicePaymentStateException` → 400,
     and a final `InvalidOperationException` fallback at 400 for any
     not-yet-typed validation failure.
   - `PaymentsController.List` is now paged + filtered, returning
     `PaymentListResponse`; `GetById` got an empty-id guard.
   - `InvoicesController` gained `GET /api/invoices/{id}/payments` (404
     when the invoice itself is unknown) and `GET /api/invoices/{id}/
     payment-summary` (404 likewise) — these endpoints surface the
     invoice id in the URL path, so 404 is the correct "missing
     resource" response.
7. **EF mapping + migration.** Mapped `Payment.Notes` as nullable
   `varchar(2000)` in `TenantBillingDbContext`. Generated migration
   `20260424175830_PaymentRecordingEnhancements` (single `AddColumn` for
   `Notes`, with the corresponding `DropColumn` in `Down`). All other
   schema bits already existed from previous tickets.
8. **Tests.**
   - Added `tests/TenantBilling.Domain.Tests/PaymentRecordingTests.cs`
     covering amount rounding, currency normalization, notes trimming +
     whitespace-only-collapses-to-null, default status `"Recorded"`,
     overpayment detection, partial-payment summary, paged listing across
     multiple invoices, and cross-tenant filtering.
   - Updated `BillingApiTests.cs` happy-path payment test to drop the
     `Status` field from the request body (no longer in DTO) and to
     assert on the new `RecordPaymentResponse` shape.
   - Updated the previously-undiscovered
     `tests/TenantBilling.Tests/Domain/PaymentServiceTests.cs`: renamed
     `Create_defaults_blank_status_to_Pending` →
     `Create_defaults_blank_status_to_Recorded` (asserts on
     `PaymentService.RecordedStatus`), and switched the non-positive
     amount assertion from `ArgumentException`/"Amount must be > 0" to
     `InvalidPaymentAmountException`/"must be greater than zero".
   - Updated existing `TenantBilling.Domain.Tests/PaymentServiceTests.cs`
     and `InvoiceRefundTests.cs` to use `Assert.ThrowsAnyAsync<
     InvalidOperationException>` (was `ThrowsAsync`, which requires
     exact-type match in xUnit and rejects derived typed exceptions).
   - Updated `InMemoryPaymentRepository` fake to implement the new
     repository surface so domain tests keep wiring up cleanly.
9. **Validation.** `dotnet build` clean. Both test projects green
   (initially 30/30 API + 128/128 domain = 158/158). Workflow restarted;
   full curl smoke test from customer-create through paged list run
   against the live `:5001` API.
10. **Architect-driven hardening (post-review).** Two must-fix items
    surfaced by the architect review were addressed:
    - Added `PaymentService.MaxNotesLength = 2000` and a defense-in-depth
      length guard in `CreateAsync` (the DTO already caps at 2000 via
      `[StringLength]`, but a non-HTTP caller composing the service
      directly would otherwise only fail at the EF column boundary).
    - Tightened the row-lock acquisition: added a tenant-scoped overload
      `IUnitOfWorkTransaction.LockInvoiceForUpdateAsync(tenantId,
      invoiceId)` whose `SELECT ... FOR UPDATE` SQL includes
      `AND TenantId = @tenantId`, so a caller that knows a foreign
      tenant's invoice id can no longer acquire a lock on that row at
      all. `PaymentService.CreateAsync` now uses the new overload.
    - Added four new tests covering the hardening:
      `Notes_longer_than_max_throws_ArgumentException`,
      `Notes_at_exact_max_length_is_accepted`,
      `GetByInvoiceAsync_returns_null_for_other_tenants_invoice`,
      `GetInvoicePaymentSummaryAsync_returns_null_for_other_tenants_invoice`,
      plus an API-level
      `Get_invoice_payments_for_other_tenants_invoice_returns_404`.
    - Final test count: **31/31 API + 132/132 domain = 163/163 passing.**
      Live curl re-validation confirmed 2000-char notes accepted, 2001
      rejected (400), and cross-tenant GET on both new endpoints returns
      404.

## 3. Files Created / Modified

**Created**

- `analysis/TBS-B04-report.md`
- `services/tenant-billing-api/src/TenantBilling.Domain/Services/PaymentExceptions.cs`
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Data/Migrations/20260424175830_PaymentRecordingEnhancements.cs`
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Data/Migrations/20260424175830_PaymentRecordingEnhancements.Designer.cs`
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/PaymentRecordingTests.cs`

**Modified**

- `services/tenant-billing-api/src/TenantBilling.Domain/Entities/Payment.cs`
  (added `Notes`)
- `services/tenant-billing-api/src/TenantBilling.Domain/Repositories/IPaymentRepository.cs`
  (new methods)
- `services/tenant-billing-api/src/TenantBilling.Domain/Repositories/IUnitOfWork.cs`
  (added tenant-scoped `LockInvoiceForUpdateAsync` overload — architect-
  driven hardening)
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/Fakes/InMemoryUnitOfWork.cs`
  (implements new lock overload)
- `services/tenant-billing-api/src/TenantBilling.Domain/Services/IPaymentService.cs`
  (new surface, records)
- `services/tenant-billing-api/src/TenantBilling.Domain/Services/PaymentService.cs`
  (typed exceptions, notes, default `"Recorded"`, paging, summary)
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Data/TenantBillingDbContext.cs`
  (Notes column mapping)
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Data/Migrations/TenantBillingDbContextModelSnapshot.cs`
  (regenerated by `dotnet ef migrations add`)
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Repositories/PaymentRepository.cs`
  (new query methods)
- `services/tenant-billing-api/src/TenantBilling.Api/Contracts/PaymentDtos.cs`
  (drop `Status`, add `Notes`, new responses)
- `services/tenant-billing-api/src/TenantBilling.Api/Controllers/PaymentsController.cs`
  (new return shape, paged list, typed-exception → status mapping)
- `services/tenant-billing-api/src/TenantBilling.Api/Controllers/InvoicesController.cs`
  (`GET /payments`, `GET /payment-summary`)
- `services/tenant-billing-api/tests/TenantBilling.Tests/Api/BillingApiTests.cs`
  (drop `Status`, expect new response shape)
- `services/tenant-billing-api/tests/TenantBilling.Tests/Domain/PaymentServiceTests.cs`
  (default-`"Recorded"`, typed exception)
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/InMemoryPaymentRepository.cs`
  (new query methods)
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/PaymentServiceTests.cs`
  (`ThrowsAnyAsync` for typed exceptions)
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/InvoiceRefundTests.cs`
  (`ThrowsAnyAsync` for the two payment-after-refund cases)

## 4. API Changes

All endpoints require the `X-Tenant-Id` header (resolved by middleware) and
are tenant-scoped end-to-end.

### Modified

- `POST /api/payments`
  - **Request body changes:** removed `status` (server now sets
    `"Recorded"`); added optional `notes` (≤ 2000 chars; trimmed; blank
    collapses to null).
  - **Response changes:** returns `RecordPaymentResponse` —
    `{ payment: PaymentResponse, invoiceSummary:
    InvoicePaymentSummaryResponse }`. `PaymentResponse` itself gained a
    `notes` field.
  - **Status codes:** 201 on success; 400 for unknown/cross-tenant
    invoice id (was effectively 400 already, but now via the typed
    `InvoiceNotFoundException`), 400 for non-positive amount, 400 for
    currency mismatch, 400 for overpayment, 400 for invoice in
    Draft/Voided/Paid/Refunded state, 409 for duplicate
    `transactionReference`.

- `GET /api/payments`
  - Now paged and filterable. Query params: `page` (default 1), `pageSize`
    (default 20, max 100), plus optional `invoiceId`, `status`, `method`,
    `paidAfter`, `paidBefore`. Returns `PaymentListResponse` with
    `items`, `page`, `pageSize`, `totalCount`, `totalPages`.

### Added

- `GET /api/invoices/{id}/payments` — returns the full list of payments
  for an invoice belonging to the caller's tenant. Returns 404 if the
  invoice id is unknown for the tenant.
- `GET /api/invoices/{id}/payment-summary` — returns
  `InvoicePaymentSummaryResponse` with `invoiceId`, `invoiceNumber`,
  `invoiceStatus`, `invoiceTotal`, `totalPaid`, `balanceDue`, `currency`.
  Returns 404 if the invoice id is unknown for the tenant.

### Status-mapping rationale

`InvoiceNotFoundException` is mapped to **400** on `POST /api/payments`
because the invoice id lives in the request body (the path is
`/api/payments`, which is valid), and a 400 also avoids leaking whether
the invoice exists in another tenant — cross-tenant lookups surface
through the same `InvoiceNotFoundException` as truly unknown ids. The
`GET /api/invoices/{id}/...` endpoints expose the invoice id in the URL
path, so they correctly map "unknown" to **404**.

## 5. Database Changes

Single migration: `20260424175830_PaymentRecordingEnhancements`.

Up:

```csharp
migrationBuilder.AddColumn<string>(
    name: "Notes",
    table: "payments",
    type: "varchar(2000)",
    maxLength: 2000,
    nullable: true)
    .Annotation("MySql:CharSet", "utf8mb4");
```

Down: `DropColumn("Notes", "payments")`.

No other schema changes were required: the previous payment schema
already had `TransactionReference` and the unique index per
(`TenantId`, `TransactionReference`) from `AddPaymentTransactionReferenceUniqueIndex`,
which the new flow continues to rely on for idempotency.

## 6. Validation Results

### Build

`dotnet build TenantBilling.sln` — clean, no warnings introduced by this
change.

### Tests

- `tests/TenantBilling.Tests` (API integration): **31/31 passing**.
- `tests/TenantBilling.Domain.Tests` (domain unit): **132/132 passing**.
- Total: **163/163 passing**.

### Live smoke test (curl against `http://localhost:5001`)

All run against tenant `11111111-2222-3333-4444-666666666666`:

1. `POST /api/customers` — 201, customer created.
2. `POST /api/invoices` — 201, invoice total 100.
3. `POST /api/invoices/{id}/issue` — 200, status → `Issued`.
4. `POST /api/payments` (amount 40, `notes: "  partial deposit  "`) — 201.
   - `payment.status = "Recorded"`, `payment.notes = "partial deposit"`
     (trimmed).
   - `invoiceSummary.invoiceStatus = "PartiallyPaid"`,
     `totalPaid = 40`, `balanceDue = 60`.
5. `GET /api/invoices/{id}/payment-summary` — 200, identical
   `PartiallyPaid` summary.
6. `GET /api/invoices/{id}/payments` — 200, list contains the one
   payment with the trimmed notes.
7. `POST /api/payments` (amount 60) — 201. Invoice promotes to `Paid`.
8. `GET /api/payments?page=1&pageSize=10` — 200,
   `{ items: [...2 payments...], page: 1, pageSize: 10, totalCount: 2,
   totalPages: 1 }`.
9. `POST /api/payments` (amount 50 against now-Paid invoice) — 400,
   "Invoice ... in status 'Paid' cannot accept payments."
10. `POST /api/payments` from a different tenant against tenant A's
    invoice — 400, generic "Invoice ... not found." (no cross-tenant
    leak).
11. `POST /api/payments` with currency `EUR` against a USD invoice — 400,
    "Payment currency 'EUR' does not match invoice currency 'USD'."

All flows behaved as the spec requires.

## 7. Known Gaps / Notes

- **Notes length validation surface.** `notes` is capped at 2000 in the
  DTO (`[StringLength(2000)]`) and rejected by the service-layer guard
  (`PaymentService.MaxNotesLength`) before EF ever sees it. The DTO
  surface produces the standard ASP.NET model-validation ProblemDetails
  400. The service surface throws `ArgumentException(paramName: "notes")`
  for non-HTTP callers — both surfaces map to 400 at the controller.
- **Filters on `GET /api/payments`.** The repository supports
  `invoiceId`, `status`, `method`, `paidAfter`, `paidBefore` filters and
  the controller exposes them. Other filter dimensions (e.g.
  `createdAfter`, `amountBetween`) are intentionally out of scope.
- **`InvoiceNotFoundException` → 400 on POST.** This is a deliberate
  choice (see §4 status-mapping rationale). If a future ticket wants
  the stricter REST shape "the body referenced a missing resource → 404",
  the catch in `PaymentsController.Create` is the single edit point.
- **`Status` is server-controlled now.** Existing external integrations
  that posted `status: "succeeded"` (or any other custom string) will
  silently see their value ignored and end up with `Status = "Recorded"`.
  Voiding a payment is a separate flow (TBS-B05) that uses the new
  repository `UpdateAsync` plumbing this ticket added.
- **xUnit `Assert.ThrowsAsync<T>` requires exact-type match.** The
  pre-existing payment tests used `ThrowsAsync<InvalidOperationException>`,
  which now rejects the typed derivatives even though they `IS-A`
  `InvalidOperationException`. They were updated to `ThrowsAnyAsync`,
  which is the correct primitive when the test wants "this exception or
  any subtype." This is the only test-side adaptation forced by the
  exception hierarchy.
- **Sequential test runs.** The `Tenant Billing API` workflow holds file
  locks on `bin/`, so `dotnet test` against the same source tree must run
  with the workflow stopped (or restarted afterwards). The two test
  projects can be run independently if `dotnet test TenantBilling.sln`
  picks up only one runner — both were run end-to-end here for this
  validation.
- **Tenant-scoped row-lock under InMemory provider.** The new
  tenant-scoped `LockInvoiceForUpdateAsync(tenantId, invoiceId)` is
  enforced by SQL `WHERE Id = @id AND TenantId = @tenantId FOR UPDATE`
  on the relational provider. The `EfNoopTransaction` and the
  `InMemoryUnitOfWork` fake do not actually model tenant-scoped locking
  semantics (they fall back to a per-invoice semaphore for serialization
  only) — the assumption, identical to the existing per-invoice lock, is
  that production runs against MySQL where the predicate is real.
