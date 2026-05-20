# TBS-B03 Report — Invoice Management Core

## 1. Codebase Analysis

The Tenant Billing Service (`services/tenant-billing-api/`) is a standalone
.NET 10 / EF Core 10 / MySQL service organized into Domain (entities, services,
repository interfaces), Infrastructure (EF DbContext, repositories, migrations),
and Api (controllers, contracts, middleware) projects, with a dedicated test
project (`tests/TenantBilling.Domain.Tests/`) that exercises the domain layer
through in-memory fakes plus an integration-style project
(`tests/TenantBilling.Tests/`) that boots the API via `WebApplicationFactory`.

Before B03 the `Invoice` entity had Subtotal, TaxAmount, TotalAmount, and a
status column but no `DiscountAmount` or `IssuedAt` audit timestamp, and the
repository only exposed `GetAllForTenantAsync` (no filtering, paging, or
duplicate-number lookups). The service required callers to pre-allocate a
unique invoice number, performed only basic money validation, and the
controller listed every invoice for a tenant in one round-trip with no query
parameters. Tenant scoping is already enforced by `TenantResolutionMiddleware`
(added in TBS-B02), which mandates the `X-Tenant-Id` header on every
`/api/*` request.

## 2. Implementation Steps

1. Extended the `Invoice` entity with `DiscountAmount` (decimal, default 0) and
   nullable `IssuedAt`, and mapped them in `TenantBillingDbContext` with
   precision 18,2 plus new non-unique indexes `IX_invoices_Status` and
   `IX_invoices_DueDate` to support filtered listing.
2. Generated EF migration `InvoiceManagementCoreEnhancements` adding the two
   columns and two indexes.
3. Expanded `IInvoiceRepository` (and both concrete + in-memory fakes) with:
   `ListAsync(tenant, search, status, customerId, fromDate, toDate, page,
   pageSize)`, `CountAsync` (same filters), `ExistsByTenantAndNumberAsync`
   (with optional `excludingInvoiceId` for future edit support), and
   `GetLatestInvoiceNumberAsync(tenantId, year)`. `UpdateStatusAsync` gained
   an optional `issuedAt` parameter that is only persisted on the first
   transition (idempotent stamp).
4. Rewrote `InvoiceService.CreateAsync` to: accept a nullable invoice number,
   accept an optional `discountAmount` (defaults to 0 for backward
   compatibility with existing callers), validate `DiscountAmount >= 0` and
   `DiscountAmount <= Subtotal + Tax`, normalize the currency to a 3-letter
   ISO code, and auto-generate `INV-YYYY-NNNNNN` per tenant/year via a
   regex-based sequence walk that walks forward up to 1000 slots if a
   collision occurs. Number resolution runs before money validation so
   duplicate-number conflicts (409) win consistently over money-shape errors
   (400).
5. Added `InvoiceService.ListPagedAsync` returning `InvoicePage(items, total)`
   with `page` clamped to `>= 1` and `pageSize` clamped to `[1, 100]`
   (default 25); `IssueAsync` now passes `DateTime.UtcNow` as the issuedAt
   stamp through `UpdateStatusAsync`. `GetAsync` and `ListPagedAsync` reject
   empty tenant/invoice ids with `ArgumentException`.
6. Added DTOs `InvoiceListResponse` and `IssueInvoiceResponse`, extended
   `CreateInvoiceRequest` with optional `InvoiceNumber` + `DiscountAmount`,
   and added `DiscountAmount` / `IssuedAt` to `InvoiceResponse`. Line items
   now include `CreatedAt`.
7. Updated `InvoicesController.List` to accept `search`, `status`,
   `customerId`, `fromDate`, `toDate`, `page`, `pageSize` query parameters
   and return `InvoiceListResponse`. Added empty-Guid guards on
   `Get/Issue/Void/Reevaluate/Refund`. `Issue` now returns the slim
   `IssueInvoiceResponse`. `Refund` was tightened to source the tenant id
   from the resolved tenant context rather than the body.
8. Updated `PaymentService.UpdateStatusAsync` call sites to the new 6-arg
   signature; updated `DomainTestHost` to inject `IRefundRepository`.
9. Repaired the previously-broken `tests/TenantBilling.Tests/` project by
   adding a `CreateClientForTenant(Guid)` helper to the
   `TenantBillingWebApplicationFactory` (every `/api/*` request now sets
   `X-Tenant-Id`), removing references to the long-removed `TenantId` body
   fields on `CreateCustomerRequest` / `CreatePaymentRequest`, and aligning
   one Payment test message to the current "not found" wording (which is
   intentionally generic so cross-tenant invoice existence cannot leak).
10. Added a new `InvoiceManagementCoreTests` suite (18 tests) in
    `TenantBilling.Domain.Tests` covering discount math + validation,
    auto-numbering (first, sequential, blank, manual-collision walk-forward,
    per-tenant isolation, IssueDate-year basis), `IssuedAt` stamping, and
    paged-list filtering / clamping / tenant isolation, plus empty-id
    guards.

## 3. Files Created / Modified

**Created**
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Data/Migrations/20260424171644_InvoiceManagementCoreEnhancements.cs` (+ Designer)
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/InvoiceManagementCoreTests.cs`

**Modified — Domain**
- `src/TenantBilling.Domain/Entities/Invoice.cs` — `DiscountAmount`, `IssuedAt`
- `src/TenantBilling.Domain/Repositories/IInvoiceRepository.cs` — new methods
- `src/TenantBilling.Domain/Services/IInvoiceService.cs` — new signatures
- `src/TenantBilling.Domain/Services/InvoiceService.cs` — auto-numbering,
  discount validation, paged listing, IssuedAt stamping, empty-id guards
- `src/TenantBilling.Domain/Services/PaymentService.cs` — adjusted to new
  `UpdateStatusAsync` signature

**Modified — Infrastructure**
- `src/TenantBilling.Infrastructure/Data/TenantBillingDbContext.cs` —
  `DiscountAmount` / `IssuedAt` mapping + `IX_invoices_Status` /
  `IX_invoices_DueDate`
- `src/TenantBilling.Infrastructure/Data/Migrations/TenantBillingDbContextModelSnapshot.cs` — refreshed
- `src/TenantBilling.Infrastructure/Repositories/InvoiceRepository.cs` —
  `ListAsync`, `CountAsync`, `ExistsByTenantAndNumberAsync`,
  `GetLatestInvoiceNumberAsync`, expanded `UpdateStatusAsync`

**Modified — Api**
- `src/TenantBilling.Api/Contracts/InvoiceDtos.cs` — request/response shape
- `src/TenantBilling.Api/Controllers/InvoicesController.cs` — filtered list +
  empty-id guards + slim issue response

**Modified — Tests**
- `tests/TenantBilling.Domain.Tests/Fakes/InMemoryInvoiceRepository.cs` —
  matches new repository surface
- `tests/TenantBilling.Domain.Tests/Helpers/DomainTestHost.cs` — injects
  `IRefundRepository`
- `tests/TenantBilling.Tests/TenantBillingWebApplicationFactory.cs` —
  `CreateClientForTenant(Guid)` helper
- `tests/TenantBilling.Tests/Api/BillingApiTests.cs` — refactored to use the
  per-tenant client; removed references to the removed body `TenantId` fields
- `tests/TenantBilling.Tests/Domain/PaymentServiceTests.cs` — corrected call
  to `IssueAsync(tenantId, invoiceId)` and the cross-tenant message
  expectation to the current "not found" wording

## 4. API Changes

`POST /api/invoices` (`CreateInvoiceRequest`):
- `invoiceNumber` is now optional. When null/blank the service auto-generates
  the next `INV-YYYY-NNNNNN` for that tenant + IssueDate year (collision-safe
  walk-forward).
- New optional `discountAmount` (defaults to 0). Validated to be `>= 0` and
  `<= subtotal + tax`. Total is computed as `subtotal + tax - discount`.
- `currency` is normalized to upper-case 3-letter ISO.
- The body field `tenantId` is preserved on the request DTO for backward
  compatibility but the controller continues to source the tenant from the
  `X-Tenant-Id` header (TBS-B02 contract).

`GET /api/invoices`:
- New query parameters: `search`, `status`, `customerId`, `fromDate`,
  `toDate`, `page` (default 1, min 1), `pageSize` (default 25, max 100).
- Response is now `InvoiceListResponse { items, page, pageSize, totalCount,
  totalPages }`.

`POST /api/invoices/{id}/issue`:
- Stamps `IssuedAt = UtcNow` on the first Draft → Issued transition (kept
  immutable on subsequent calls if the entity ever returns to Draft and is
  re-issued, since `UpdateStatusAsync` only writes when `IssuedAt` is null).
- Returns the slim `IssueInvoiceResponse { id, status, issuedAt, updatedAt }`.

`GET /api/invoices/{id}`, `POST .../void`, `POST .../reevaluate`,
`POST .../refund`: now reject `Guid.Empty` ids with HTTP 400 instead of
silently looking up an empty id.

`InvoiceResponse` adds `discountAmount` and `issuedAt`. Each line now
includes `createdAt`.

## 5. Database Changes

Migration `20260424171644_InvoiceManagementCoreEnhancements`:
- `ALTER TABLE invoices ADD COLUMN DiscountAmount decimal(18,2) NOT NULL DEFAULT 0`
- `ALTER TABLE invoices ADD COLUMN IssuedAt datetime(6) NULL`
- `CREATE INDEX IX_invoices_Status ON invoices(Status)`
- `CREATE INDEX IX_invoices_DueDate ON invoices(DueDate)`

Both indexes are non-unique and chosen to support the new filterable list
endpoint without imposing any new uniqueness constraints. The existing
unique index on `(TenantId, InvoiceNumber)` continues to enforce the
duplicate-number invariant; auto-generated numbers go through the same
constraint via the walk-forward loop.

## 6. Validation Results

- `dotnet build services/tenant-billing-api/TenantBilling.sln` — succeeds with
  0 warnings, 0 errors.
- `dotnet test services/tenant-billing-api/TenantBilling.sln` — **135 passed
  / 0 failed** (`TenantBilling.Domain.Tests`: 105/105;
  `TenantBilling.Tests`: 30/30). The 105 includes the 18 new
  `InvoiceManagementCoreTests`.
- `dotnet ef migrations add InvoiceManagementCoreEnhancements` — succeeds and
  produces the migration file shown above.
- API smoke (against the running `Tenant Billing API` workflow on port 5001)
  with `X-Tenant-Id` header:
  - `POST /api/customers` → 201.
  - `POST /api/invoices` with `invoiceNumber` omitted, taxAmount=10,
    discountAmount=5 → 201 with `invoiceNumber=INV-2026-000001`,
    `subtotal=100`, `taxAmount=10`, `discountAmount=5`, `totalAmount=105`,
    `currency=USD`, `issuedAt=null`.
  - Second invoice with `"invoiceNumber":""` → 201 with
    `invoiceNumber=INV-2026-000002` (auto-walks the sequence).
  - `POST /api/invoices/{id}/issue` → 200 with the slim issue response and
    `issuedAt` populated to a UTC timestamp.
  - `POST /api/invoices` with `discountAmount=1000` against subtotal=50 +
    tax=5 → 400 `"DiscountAmount 1000 cannot exceed Subtotal+Tax (55)"`.
  - `GET /api/invoices?status=Issued&page=1&pageSize=10` → 200 with
    `{page:1, pageSize:10, totalCount:1, totalPages:1, items:[...]}` and
    only the issued invoice in the page.
  - `GET /api/invoices/00000000-0000-0000-0000-000000000000` → 400 (empty-id
    guard).

## 7. Known Gaps / Notes

- **Header-based tenancy preserved (deviation from spec)**: the spec wording
  hints at body-derived tenant resolution. We keep the TBS-B02
  `X-Tenant-Id` header contract because removing it would silently weaken
  cross-tenant isolation enforced by `TenantResolutionMiddleware`. The
  request DTOs still expose `TenantId` for backward compatibility with
  upstream callers, but the controller never reads them.
- **`tests/TenantBilling.Tests/` repaired in passing**: this test project
  did not compile against `main` prior to B03 because earlier work removed
  `TenantId` from `CreateCustomerRequest` / `CreatePaymentRequest`,
  introduced the X-Tenant-Id middleware, and added a 2-arg `IssueAsync`
  signature without updating the integration suite. We added a
  `CreateClientForTenant(Guid)` helper, removed dead body-`TenantId`
  references, and aligned one assertion to the current cross-tenant "not
  found" message. This was scope-adjacent but unavoidable to restore a
  green build.
- **`discountAmount` is given a default of 0 on the service signature** so
  pre-existing callers (and the Refund / cross-feature paths) do not need
  to be touched. Controller-bound `CreateInvoiceRequest.DiscountAmount`
  defaults to 0 with the same semantics.
- **Auto-numbering uses IssueDate year**, not UtcNow year, so back-dated
  invoices land in the correct annual sequence. The walk-forward loop is
  bounded at 1000 attempts and throws `InvalidOperationException` if it
  cannot find a free slot — not expected under realistic load but flagged
  here for completeness.
- **`IssuedAt` is write-once**: `UpdateStatusAsync` only sets it when the
  current value is null. This preserves the original audit timestamp if a
  status machine ever re-enters Issued in a future task.
- **No new authorization layer added**: the controller continues to rely on
  the existing tenant-scoped repository methods + `X-Tenant-Id` middleware.
  Cross-tenant access surfaces as 404 (not 400/403) for invoices, matching
  pre-B03 behavior.
