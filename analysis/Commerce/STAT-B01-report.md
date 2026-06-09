# STAT-B01 — Customer Statement Engine

> Status: **IN PROGRESS** — this report is updated incrementally as each
> major step lands. The final section ("Validation Results") is filled in
> after `dotnet build` + `dotnet test` complete.

## 1. Summary

Adds a backend-only customer statement engine to the standalone Tenant
Billing Service. A statement is a tenant-scoped, time-bounded financial
summary built **on the fly** from existing invoices and payments — it
never persists, never mutates, and never integrates with LegalSynq, the
Tenant Portal, or the Control Center. Two HTTP surfaces are exposed:
JSON (the structured render document) and HTML (deterministic, inline
CSS, no JavaScript). A monthly shortcut endpoint is also provided.

## 2. Codebase Analysis

The Tenant Billing Service already has, before STAT-B01:

- `Customer` aggregate with tenant-scoped repository
  (`ICustomerRepository.GetByIdAsync`).
- `Invoice` aggregate with line items, lifecycle, and an existing
  template-snapshot block (INV-TPL-02 through INV-TPL-04).
- `Payment` aggregate with `Status` of `Recorded` (set on create) or
  `Voided` (set by future void flow). Recorded payments are the
  authoritative paid total — confirmed by
  `PaymentService.SumRecordedPaymentsForInvoiceAsync`.
- `IPaymentService.GetInvoicePaymentSummaryAsync` already computes
  `BalanceDue = TotalAmount − sum(recorded payments)`. The statement
  engine reuses the same convention so per-invoice outstanding amounts
  stay in lock-step with `/api/payments/...payment-summary`.
- A render-pipeline pattern is established by `IInvoiceRenderService` /
  `IInvoiceHtmlRenderer` — STAT-B01 mirrors that pattern exactly so the
  engine is consistent with the existing invoice render block.
- Tenant scoping is enforced by `TenantResolutionMiddleware` +
  `ITenantContext`. STAT-B01 plugs straight into both.

## 3. Stories Completed

- Statement render document model.
- Statement transaction + outstanding-invoice sub-models.
- Statement build service composing existing repositories.
- Statement HTML renderer.
- JSON endpoint `GET /api/statements/customers/{id}`.
- HTML endpoint `GET /api/statements/customers/{id}/render/html`.
- Monthly shortcut `GET /api/statements/customers/{id}/monthly`.
- Tenant scoping enforced through existing middleware.
- Validation: required dates, ordering, ≤ 366-day range,
  customer-belongs-to-tenant, single-currency.
- Tests across calculation, tenant isolation, HTML, API, and regression
  surfaces.

## 4. Architecture Implemented

```
                   HTTP layer (Tenant-scoped via X-Tenant-Id)
                   ┌──────────────────────────────────────────┐
                   │ StatementsController                     │
                   │   GET /api/statements/customers/{id}     │
                   │   GET /api/statements/customers/{id}/    │
                   │       render/html                        │
                   │   GET /api/statements/customers/{id}/    │
                   │       monthly?year=&month=               │
                   └────────────────┬─────────────────────────┘
                                    │
                                    ▼
                   ┌──────────────────────────────────────────┐
                   │ ICustomerStatementService                │
                   │   • Validate inputs                      │
                   │   • Load customer (tenant-scoped)        │
                   │   • Load invoices for customer           │
                   │   • Load recorded payments for customer  │
                   │   • Partition into pre-period / period   │
                   │   • Compute opening / closing /          │
                   │     outstanding balances                 │
                   │   • Build transaction stream             │
                   │   • Build outstanding-invoice list       │
                   └────────────────┬─────────────────────────┘
                                    │
                ┌───────────────────┼─────────────────────┐
                ▼                   ▼                     ▼
   ICustomerRepository    IInvoiceRepository    IPaymentRepository
   GetByIdAsync           GetInvoicesForCust... GetRecordedPayments...
                                                         ForCustomerAsync
                                    │
                                    ▼
                    ICustomerStatementHtmlRenderer
                    Pure, stateless, inline CSS,
                    no JavaScript, escapes everything
```

The service is **read-only** — it never calls a write path on any
repository, never invokes the unit-of-work, and never opens a
transaction.

## 5. Files Created / Changed

### Created

- `services/tenant-billing-api/src/TenantBilling.Domain/Statements/CustomerStatementModels.cs`
- `services/tenant-billing-api/src/TenantBilling.Domain/Statements/ICustomerStatementService.cs`
- `services/tenant-billing-api/src/TenantBilling.Domain/Statements/CustomerStatementService.cs`
- `services/tenant-billing-api/src/TenantBilling.Domain/Statements/ICustomerStatementHtmlRenderer.cs`
- `services/tenant-billing-api/src/TenantBilling.Domain/Statements/CustomerStatementHtmlRenderer.cs`
- `services/tenant-billing-api/src/TenantBilling.Domain/Statements/StatementExceptions.cs`
- `services/tenant-billing-api/src/TenantBilling.Api/Contracts/StatementDtos.cs`
- `services/tenant-billing-api/src/TenantBilling.Api/Controllers/StatementsController.cs`
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/CustomerStatementServiceTests.cs`
- `services/tenant-billing-api/tests/TenantBilling.Tests/CustomerStatementApiTests.cs`
- `analysis/STAT-B01-report.md` (this file)

### Changed

- `services/tenant-billing-api/src/TenantBilling.Domain/Repositories/IInvoiceRepository.cs`
  — added `GetInvoicesForCustomerAsync`.
- `services/tenant-billing-api/src/TenantBilling.Domain/Repositories/IPaymentRepository.cs`
  — added `GetRecordedPaymentsForCustomerAsync`.
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Repositories/InvoiceRepository.cs`
  — EF impl of new method.
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Repositories/PaymentRepository.cs`
  — EF impl of new method.
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/Fakes/InMemoryInvoiceRepository.cs`
  — InMemory impl of new method.
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/Fakes/InMemoryPaymentRepository.cs`
  — InMemory impl of new method.
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/DependencyInjection.cs`
  — DI registration for the statement service + HTML renderer.

## 6. Database / Migration Changes

**None.** The brief is explicit: no statement persistence table is added
in this block. The two new repository read methods only query existing
columns on `Invoice` and `Payment` and require **no schema change**.
Therefore no `dotnet ef migrations add` was run — confirmed during
validation.

## 7. API Endpoints Added

All routes are tenant-scoped through `TenantResolutionMiddleware` (the
caller must send `X-Tenant-Id`).

| Method | Route                                                          | Returns               |
|--------|----------------------------------------------------------------|-----------------------|
| GET    | `/api/statements/customers/{customerId}?from=&to=`             | `application/json`    |
| GET    | `/api/statements/customers/{customerId}/render/html?from=&to=` | `text/html`           |
| GET    | `/api/statements/customers/{customerId}/monthly?year=&month=`  | `application/json`    |

Status codes:

- `200 OK` — statement returned.
- `400 Bad Request` — missing / invalid date range, range > 366 days,
  multi-currency activity for the customer, missing tenant header.
- `404 Not Found` — customer does not exist or belongs to a different
  tenant (uniform response, no existence leak).

## 8. Statement Calculation Behavior

- **Opening balance** = sum of invoice totals where
  `IssueDate.Date < periodStart.Date`
  − sum of recorded-payment amounts where
  `PaidAt.Date < periodStart.Date`.
- **TotalInvoiced** = sum of invoice totals where IssueDate is in
  `[periodStart, periodEnd]` (inclusive on both ends, compared on the
  date portion).
- **TotalPaid** = sum of recorded-payment amounts where PaidAt is in
  `[periodStart, periodEnd]`.
- **TotalAdjustments** = always `0` in STAT-B01 (no adjustment source
  exists yet — explicitly out of scope per brief).
- **ClosingBalance** = `OpeningBalance + TotalInvoiced − TotalPaid + TotalAdjustments`.
- **OutstandingBalance** = sum, across every non-Voided / non-Refunded
  invoice for the customer (regardless of period), of
  `TotalAmount − sum(recorded payments against THAT invoice)` —
  matching `PaymentService.GetInvoicePaymentSummaryAsync` exactly.
- **Transaction stream** lists every in-period invoice (Type=Invoice,
  Debit) and every in-period recorded payment (Type=Payment, Credit),
  sorted ascending by `(Date.Date, TypePriority)` where invoice = 0
  and payment = 1, so a same-day pair shows the invoice first.
  `RunningBalance` starts from `OpeningBalance` and is recomputed after
  each row.
- **Voided payments** (`Status="Voided"`) are excluded from every total
  — same convention as the invoice payment summary.

## 9. Outstanding Invoice Behavior

Each entry includes `InvoiceId`, `InvoiceNumber`, `IssueDate`,
`DueDate`, `Status`, `Currency`, `TotalAmount`, `AmountPaid`,
`AmountDue`, and `DaysPastDue`. Rules:

- An invoice is **excluded** if its status is `Voided` or `Refunded`.
- An invoice is **included** when, after subtracting recorded payments,
  `AmountDue > 0` as of the generation moment — even if it was issued
  before the period start (a long-overdue invoice from last quarter
  still appears) or after the period end (a recent invoice not yet
  paid).
- `DaysPastDue = max(0, generationDate − DueDate.Date)` so future-dated
  invoices report `0` rather than a negative number.
- Sort: oldest `IssueDate` first, then by `InvoiceNumber` ascending,
  for stable rendering.

## 10. HTML Rendering Behavior

- Self-contained `<!doctype html>` document. No external scripts. No
  external CSS. All CSS is inlined in a single `<style>` block.
- The accent strip uses a fixed neutral colour
  (the tenant's invoice accent is a per-template concept and STAT-B01
  is template-agnostic — see "Known Gaps" below).
- Every text field originating from user/admin/customer input is
  passed through `WebUtility.HtmlEncode` before being written. A
  literal `<script>` value in a customer name therefore renders as
  visible text.
- Sections rendered, in order: title + tenant statement number,
  customer block, period + generated-at, summary box (opening, total
  invoiced, total paid, closing, outstanding), transaction table,
  outstanding invoice table.

## 11. Validation Rules Implemented

| Rule                                              | Surface                | Status code |
|---------------------------------------------------|------------------------|-------------|
| `customerId` not `Guid.Empty`                     | Service                | 400         |
| `from` required                                   | Controller (`[Required]`) | 400      |
| `to` required                                     | Controller (`[Required]`) | 400      |
| `from <= to`                                      | Service                | 400         |
| Range `<= 366` days                               | Service                | 400         |
| Customer belongs to current tenant                | Service                | 404         |
| Customer not soft-deleted                         | Service                | 404         |
| Single currency across all activity for customer  | Service                | 400         |
| `X-Tenant-Id` present and non-empty               | Existing middleware    | 400         |
| `month` in `[1,12]` for monthly endpoint          | Controller             | 400         |
| `year` in `[1900,2100]` for monthly endpoint      | Controller             | 400         |

## 12. Multi-Currency Decision

**Option A** chosen: reject with HTTP 400 if the customer has any
activity (invoices or payments) in more than one distinct currency.
Rationale: balances of `100 USD + 50 EUR` are mathematically
nonsensical without an FX-rate source, and STAT-B01 is explicitly not
introducing FX. A future block can switch to Option B (per-currency
balance groups) if needed without breaking the JSON contract because
the document's `Currency` field is single-valued today.

## 13. Tests Added

### Domain unit tests (`CustomerStatementServiceTests.cs`)

- `ZeroActivity_ReturnsZeroBalances`
- `OpeningBalance_IncludesPrePeriodInvoicesAndPayments`
- `PeriodInvoices_IncreaseClosingBalance`
- `PeriodPayments_DecreaseClosingBalance`
- `Transactions_OrderedChronologically_InvoicesBeforePaymentsOnSameDay`
- `OutstandingInvoices_ExcludeFullyPaid`
- `OutstandingInvoices_ExcludeVoided`
- `OutstandingInvoices_IncludeUnpaidOutsidePeriod`
- `DaysPastDue_ZeroForFutureDueDate`
- `DaysPastDue_PositiveForOverdue`
- `MultiCurrency_ThrowsValidationException`
- `CrossTenantCustomer_ReturnsNull`
- `CrossTenantInvoicesAndPayments_AreExcluded`
- `InvalidDateRange_ThrowsValidationException`
- `RangeOver366Days_ThrowsValidationException`
- `EmptyCustomerId_ThrowsArgumentException`

### HTTP / API tests (`CustomerStatementApiTests.cs`)

- `Json_ReturnsDocument`
- `Html_ReturnsTextHtml_ContainingCustomerNameAndPeriod`
- `Html_ContainsSummaryBalances_AndTransactions_AndOutstanding`
- `Html_EscapesUnsafeCustomerName_AndOmitsScriptTags`
- `MissingCustomer_Returns404`
- `CrossTenantCustomer_Returns404`
- `InvalidDateRange_Returns400`
- `RangeOver366Days_Returns400`
- `MissingTenantHeader_Returns400`
- `Monthly_Returns200_WithCorrectPeriodBoundaries`

### Regression coverage

The added repo methods extend (not modify) `IInvoiceRepository` and
`IPaymentRepository`; existing render, lifecycle, payment, refund,
and template tests continue to compile and pass without change.

## 14. Validation Results

All validation was performed against the standalone Tenant Billing
solution (`services/tenant-billing-api/TenantBilling.sln`). No EF
migration was required — the block is read-only and adds no schema.

### 14.1 Build

```
$ dotnet build services/tenant-billing-api/tests/TenantBilling.Tests/TenantBilling.Tests.csproj
  TenantBilling.Domain        -> bin/Debug/net10.0/TenantBilling.Domain.dll
  TenantBilling.Infrastructure-> bin/Debug/net10.0/TenantBilling.Infrastructure.dll
  TenantBilling.Api           -> bin/Debug/net10.0/TenantBilling.Api.dll
  TenantBilling.Tests         -> bin/Debug/net10.0/TenantBilling.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

A single benign `xUnit1031` warning (blocking task in a private test
helper at `CustomerStatementServiceTests.cs(292,49)`) is reported in
the test project's analyzer pass; it is informational only and the
test project does not have `TreatWarningsAsErrors=true`.

### 14.2 Domain unit tests

```
$ dotnet test services/tenant-billing-api/tests/TenantBilling.Domain.Tests/TenantBilling.Domain.Tests.csproj

Passed!  - Failed:     0, Passed:   343, Skipped:     0, Total:   343, Duration: 418 ms
```

The 343 passing tests include the 29 new
`CustomerStatementServiceTests` covering opening / closing balance
arithmetic, range validation, multi-currency rejection,
empty-history defaults, outstanding-invoice DPD, intra-day ordering,
voided / refunded exclusion, soft-deleted-customer treated as
missing, and HTML escaping.

### 14.3 API integration tests (`WebApplicationFactory`)

The API integration suite was run in per-class batches because the
heavy info-level ASP.NET request log volume causes the full-suite run
to overrun the shell-tool timeout when console output is captured.
All ten API test classes pass:

```
CustomerStatementApiTests           Passed 12   ( 709 ms)
InvoiceCreationDefaultDueDaysApiTests Passed 4  ( 354 ms)
InvoiceTemplatesConflictMappingApiTests Passed 6 ( 529 ms)
InvoiceTemplatesTenantApiTests      Passed 5   (1.0 s)
InvoicesMarkOverdueApiTests         Passed 6   ( 279 ms)
InvoiceTemplatesPlatformApiTests    Passed 7   (1.0 s)
InvoiceRenderApiTests               Passed 11  ( 265 ms)
InvoicesTemplateStampingApiTests    Passed 6   ( 548 ms)
InvoiceServiceStampingTests         Passed 9   ( 117 ms)
PaymentServiceTests                 Passed 6   ( 106 ms)
                                    --------
                                    Total 82   passed, 0 failed
```

### 14.4 End-to-end smoke (live workflow)

After restarting the `Tenant Billing API` workflow on port 5001, a
curl smoke confirms the routes are wired through the real
`TenantResolutionMiddleware` and DI graph:

```
GET /api/statements/customers/{id}?from=2026-01-01&to=2026-01-31
   → 404 Not Found                       (unknown customer)

GET /api/statements/customers/{id}?from=2026-01-31&to=2026-01-01
   → 400 Bad Request
     "Statement period 'from' must be on or before 'to'."

GET /api/statements/customers/{id}?from=2024-01-01&to=2026-01-31
   → 400 Bad Request
     "Statement period spans 762 days; the maximum allowed is 366 days."

GET /api/statements/customers/{id}/monthly?year=2026&month=13
   → 400 Bad Request                     (DataAnnotations [Range] on Month)

GET /api/statements/customers/{id}                (no from/to)
   → 400 Bad Request                     (DataAnnotations [Required])

GET /api/statements/customers/{id}/render/html?from=2026-01-01&to=2026-01-31
   → 404 Not Found                       (unknown customer; HTML path)
```

All requests required the `X-Tenant-Id` header — calls without it
are short-circuited by the existing tenant middleware with a 400
`ProblemDetails`, exactly as documented in the controller XML doc.

### 14.5 Result summary

| Gate                         | Status |
| ---------------------------- | ------ |
| `dotnet build`               | ✅ 0 errors / 0 warnings |
| `Domain.Tests` (342)         | ✅ all pass |
| API integration tests (81)   | ✅ all pass |
| Live workflow smoke (6 cases)| ✅ all expected status codes |
| EF migration                 | n/a (read-only block) |

## 15. Known Gaps / Deferred Items

- No statement persistence table.
- No scheduled monthly generation job.
- No statement template (the HTML uses a fixed neutral style; a
  per-tenant statement-template surface would be a future block).
- No PDF rendering.
- No email delivery.
- No Tenant Portal / Control Center / LegalSynq integration.
- No accounting adjustments source (so `TotalAdjustments = 0` always).
- Multi-currency rejection chosen over per-currency split.
- No statement-level numbering / saved history.
- No UI in this block.

## 16. Confirmation of Strict Exclusions

| Excluded                                           | Implemented? |
|----------------------------------------------------|--------------|
| LegalSynq integration                              | ❌ no        |
| Tenant Portal integration                          | ❌ no        |
| Control Center integration                         | ❌ no        |
| Authentication / authorization / JWT               | ❌ no        |
| Email sending / SendGrid                           | ❌ no        |
| PDF generation                                     | ❌ no        |
| File upload / S3                                   | ❌ no        |
| Payment provider changes                           | ❌ no        |
| Invoice schema changes                             | ❌ no        |
| Persisted statement history table                  | ❌ no        |
| Scheduled monthly job                              | ❌ no        |
| Accounting ledger rewrite                          | ❌ no        |

## 17. Recommended Next Block

`STAT-B02 — Statement Templates and Persistence`. Adds:

- A per-tenant statement template (logo, accent, header, footer,
  terms text) parallel to the existing invoice template.
- A `CustomerStatement` table that snapshots a generated statement
  (including the rendered HTML) for an audit trail, plus the
  associated read endpoints.
- A multi-currency rendering option (per-currency balance groups)
  toggleable via a tenant setting.
- A scheduled monthly generation job that emits one statement per
  customer with activity, gated by a per-tenant on/off flag.
