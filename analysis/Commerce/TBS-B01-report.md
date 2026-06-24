# TBS-B01 — Tenant Billing Service: Billing Domain Foundation

> **Status:** Complete. Service builds, migration is generated, and all endpoints
> have been smoke-tested end-to-end against the running workflow.

## 1. Codebase Analysis

Findings from the pre-implementation scan:

- The repo is a pnpm monorepo with three artifacts (`api-server`,
  `commerce-admin`, `mockup-sandbox`) and one existing .NET service at
  `services/Commerce/`. **There was no `services/tenant-billing-api/` directory
  prior to this block** — TBS-B01 is greenfield.
- The Commerce service pins .NET SDK **8.0.416** in `services/Commerce/global.json`
  and uses Central Package Management via `Directory.Build.props` +
  `Directory.Packages.props`. We mirror that convention so the two services
  evolve in lockstep on the same SDK and EF Core 10.0.10.
- Commerce uses an **EF Core In-Memory fallback** when no MySQL connection
  string is configured (see `Commerce.Infrastructure/DependencyInjection.cs`),
  so the host still boots in the Replit sandbox where MySQL is not
  provisioned. We replicate this pattern in `TenantBilling.Infrastructure/DependencyInjection.cs`.
- Commerce listens on port **5000** (`Commerce API` workflow) and uses
  `SEED_DEMO_DATA=true` for the demo dataset. To avoid a port collision the new
  Tenant Billing API uses port **5001** with no seeder.
- Tenant Billing is a **separate bounded context** — it shares no DbContext,
  schema, or code with Commerce. The two services intentionally have zero
  compile-time coupling.

## 2. Implementation Steps

1. **Initialized report** — `analysis/TBS-B01-report.md` created with the seven
   required sections.
2. **Solution scaffolding** — created `services/tenant-billing-api/` with
   `global.json`, `Directory.Build.props`, `Directory.Packages.props`,
   `.gitignore`, and three projects under `src/`:
   `TenantBilling.Domain`, `TenantBilling.Infrastructure`, `TenantBilling.Api`.
   References: `Api → Domain`, `Api → Infrastructure`,
   `Infrastructure → Domain`. Domain has zero outbound references (no EF Core,
   no ASP.NET Core).
3. **Domain entities** — added `Customer`, `Invoice`, `InvoiceLineItem`,
   `Payment` with `Guid` ids, UTC timestamps, and `decimal` money fields.
4. **DbContext + entity configuration** — `TenantBillingDbContext` with the four
   `DbSet`s, snake_case table names, `decimal(18,2)` precision on every money
   column, indexes on `TenantId`/`CustomerId`/`InvoiceId`, a unique
   `(TenantId, InvoiceNumber)` index, and the three FK relationships.
5. **EF + connection wiring** — `appsettings.json` with empty
   `ConnectionStrings:DefaultConnection`; `AddTenantBillingInfrastructure`
   registers MySQL via Pomelo when configured and falls back to
   `UseInMemoryDatabase("tenant-billing-inmemory")` when not.
6. **Initial migration** — `dotnet ef migrations add InitialCreate` produced
   `Data/Migrations/20260424153104_InitialCreate.cs` with all four tables and
   all FK/index DDL. Applied at runtime via the in-memory provider; against
   MySQL the migration would be applied with
   `dotnet ef database update --project src/TenantBilling.Infrastructure --startup-project src/TenantBilling.Api`.
7. **Repositories** — `CustomerRepository`, `InvoiceRepository`,
   `PaymentRepository` each expose `Add`, `GetById`, `GetAll`. Reads use
   `AsNoTracking`; `InvoiceRepository` includes line items and payments on
   detail/list reads.
8. **Domain services** — `CustomerService`, `InvoiceService`, `PaymentService`
   own id and timestamp generation, validate inputs, and (for invoices) compute
   `LineTotal` / `Subtotal` / `TotalAmount` so controllers stay thin.
9. **Controllers** — `CustomersController`, `InvoicesController`,
   `PaymentsController` expose POST + GET + list with `ProducesResponseType`
   attributes and `CreatedAtAction` on POSTs. Validation errors map to
   `ProblemDetails`.
10. **Workflow registration** — registered the `Tenant Billing API` workflow
    running `dotnet run --project services/tenant-billing-api/src/TenantBilling.Api/TenantBilling.Api.csproj --urls http://0.0.0.0:5001`
    with `DOTNET_ROOT` set so the global tools find the SDK runtime.
11. **Validation** — `dotnet build` green; full curl flow
    (customer → invoice → payment → list) returned 201/200 with the expected
    JSON; `/health` returned `{"status":"ok"}`.

## 3. Files Created / Changed

Created (all under `services/tenant-billing-api/`):

- `global.json`
- `Directory.Build.props`
- `Directory.Packages.props`
- `TenantBilling.sln`
- `.gitignore`
- `src/TenantBilling.Domain/TenantBilling.Domain.csproj`
- `src/TenantBilling.Domain/Entities/Customer.cs`
- `src/TenantBilling.Domain/Entities/Invoice.cs`
- `src/TenantBilling.Domain/Entities/InvoiceLineItem.cs`
- `src/TenantBilling.Domain/Entities/Payment.cs`
- `src/TenantBilling.Domain/Repositories/ICustomerRepository.cs`
- `src/TenantBilling.Domain/Repositories/IInvoiceRepository.cs`
- `src/TenantBilling.Domain/Repositories/IPaymentRepository.cs`
- `src/TenantBilling.Domain/Services/ICustomerService.cs`
- `src/TenantBilling.Domain/Services/IInvoiceService.cs`
- `src/TenantBilling.Domain/Services/IPaymentService.cs`
- `src/TenantBilling.Domain/Services/CustomerService.cs`
- `src/TenantBilling.Domain/Services/InvoiceService.cs`
- `src/TenantBilling.Domain/Services/PaymentService.cs`
- `src/TenantBilling.Infrastructure/TenantBilling.Infrastructure.csproj`
- `src/TenantBilling.Infrastructure/DependencyInjection.cs`
- `src/TenantBilling.Infrastructure/Data/TenantBillingDbContext.cs`
- `src/TenantBilling.Infrastructure/Data/TenantBillingDbContextFactory.cs`
- `src/TenantBilling.Infrastructure/Data/Migrations/20260424153104_InitialCreate.cs`
- `src/TenantBilling.Infrastructure/Data/Migrations/20260424153104_InitialCreate.Designer.cs`
- `src/TenantBilling.Infrastructure/Data/Migrations/TenantBillingDbContextModelSnapshot.cs`
- `src/TenantBilling.Infrastructure/Repositories/CustomerRepository.cs`
- `src/TenantBilling.Infrastructure/Repositories/InvoiceRepository.cs`
- `src/TenantBilling.Infrastructure/Repositories/PaymentRepository.cs`
- `src/TenantBilling.Api/TenantBilling.Api.csproj`
- `src/TenantBilling.Api/Program.cs`
- `src/TenantBilling.Api/appsettings.json`
- `src/TenantBilling.Api/appsettings.Development.json`
- `src/TenantBilling.Api/Contracts/CustomerDtos.cs`
- `src/TenantBilling.Api/Contracts/InvoiceDtos.cs`
- `src/TenantBilling.Api/Contracts/PaymentDtos.cs`
- `src/TenantBilling.Api/Controllers/CustomersController.cs`
- `src/TenantBilling.Api/Controllers/InvoicesController.cs`
- `src/TenantBilling.Api/Controllers/PaymentsController.cs`

Also created: `analysis/TBS-B01-report.md` (this file).

Modified: `.replit` — workflow registration appended the `Tenant Billing API`
workflow entry (no other content changed). The Commerce service source tree,
all artifacts, and the pnpm workspace were left untouched.

## 4. Database Schema

Generated by `Data/Migrations/20260424153104_InitialCreate.cs` (189 lines,
4 tables, 8 indexes including PKs, 3 FK constraints).

### `customers`
| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `char(36)` | PK |
| `TenantId` | `char(36)` | indexed |
| `Name` | `varchar(200)` | required |
| `Email` | `varchar(320)` | required |
| `Phone` | `varchar(50)` | nullable |
| `BillingAddress` | `varchar(1000)` | nullable |
| `CreatedAt` | `datetime(6)` | UTC |
| `UpdatedAt` | `datetime(6)` | UTC |

Indexes: `IX_customers_TenantId`, `IX_customers_TenantId_Email`.

### `invoices`
| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `char(36)` | PK |
| `TenantId` | `char(36)` | indexed |
| `CustomerId` | `char(36)` | FK → `customers.Id` (Restrict) |
| `InvoiceNumber` | `varchar(64)` | unique with TenantId |
| `IssueDate` | `datetime(6)` | required |
| `DueDate` | `datetime(6)` | required |
| `Status` | `varchar(32)` | default `Draft` |
| `Subtotal` | `decimal(18,2)` | computed by service |
| `TaxAmount` | `decimal(18,2)` | client-supplied in B01 |
| `TotalAmount` | `decimal(18,2)` | `Subtotal + TaxAmount` |
| `Currency` | `varchar(3)` | ISO 4217, uppercased |
| `Notes` | `varchar(2000)` | nullable |
| `CreatedAt` / `UpdatedAt` | `datetime(6)` | UTC |

Indexes: `IX_invoices_TenantId`, `IX_invoices_CustomerId`,
`IX_invoices_TenantId_InvoiceNumber` (unique).

### `invoice_line_items`
| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `char(36)` | PK |
| `InvoiceId` | `char(36)` | FK → `invoices.Id` (Cascade) |
| `Description` | `varchar(500)` | required |
| `Quantity` | `int` | ≥ 1 |
| `UnitPrice` | `decimal(18,2)` | required |
| `LineTotal` | `decimal(18,2)` | `Quantity * UnitPrice` |
| `CreatedAt` | `datetime(6)` | UTC |

Index: `IX_invoice_line_items_InvoiceId`.

### `payments`
| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `char(36)` | PK |
| `TenantId` | `char(36)` | indexed |
| `InvoiceId` | `char(36)` | FK → `invoices.Id` (Restrict) |
| `Amount` | `decimal(18,2)` | > 0 |
| `Currency` | `varchar(3)` | uppercased |
| `Method` | `varchar(64)` | required |
| `Status` | `varchar(32)` | default `Pending` |
| `TransactionReference` | `varchar(200)` | nullable |
| `PaidAt` | `datetime(6)` | UTC, defaults to now |
| `CreatedAt` | `datetime(6)` | UTC |

Indexes: `IX_payments_TenantId`, `IX_payments_InvoiceId`.

To apply against a real MySQL instance:
```bash
dotnet ef database update \
  --project src/TenantBilling.Infrastructure \
  --startup-project src/TenantBilling.Api
```

## 5. API Endpoints

Base URL in this Replit dev container: `http://localhost:5001`.

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/health` | liveness; returns `{ "status": "ok", "service": "tenant-billing-api" }` |
| POST | `/api/customers` | creates a customer; 201 + body |
| GET | `/api/customers` | lists customers (newest first) |
| GET | `/api/customers/{id}` | single customer; 404 if missing |
| POST | `/api/invoices` | creates invoice + line items in one call |
| GET | `/api/invoices` | lists invoices with embedded line items |
| GET | `/api/invoices/{id}` | single invoice with lines + payments |
| POST | `/api/payments` | records a payment against an invoice |
| GET | `/api/payments` | lists payments |
| GET | `/api/payments/{id}` | single payment |
| GET | `/swagger` | Swagger UI (Development only) |

Sample requests/responses captured in section 6.

## 6. Validation Results

Run from repo root against .NET SDK **8.0.416**.

| Command | Result |
| --- | --- |
| `dotnet build services/tenant-billing-api/TenantBilling.sln` | **0 warnings, 0 errors** in ~7s. |
| `dotnet ef migrations add InitialCreate ...` | Created `20260424153104_InitialCreate.cs` (189 lines) with all 4 tables, 5 indexes (+3 PK indexes), and 3 FKs. |
| Workflow `Tenant Billing API` start | Boots on port 5001 using the in-memory EF fallback (no MySQL configured). |
| `curl /health` | `{"status":"ok","service":"tenant-billing-api"}` |
| `POST /api/customers` | 201, returned `Customer` with generated `Id` and timestamps. |
| `POST /api/invoices` (1 line, qty 2 @ 50.00, tax 10.00) | 201, `subtotal: 100.00`, `taxAmount: 10.00`, `totalAmount: 110.00`, `currency: "USD"`, `status: "Draft"`. |
| `POST /api/payments` | 201, payment linked to invoice with the provided `transactionReference`. |
| `GET /api/customers` / `/api/invoices` / `/api/payments` | All return arrays containing the seeded records (verified end-to-end). |

Sample response (truncated for brevity):

```json
{
  "id": "3f55259b-a419-4ec9-aafd-7e6f15be125f",
  "customerId": "01ca05a6-fdb5-4048-8d8c-4ccd43686045",
  "invoiceNumber": "INV-001",
  "subtotal": 100.00,
  "taxAmount": 10.00,
  "totalAmount": 110.00,
  "currency": "USD",
  "status": "Draft",
  "lines": [
    { "description": "Pro plan", "quantity": 2, "unitPrice": 50.00, "lineTotal": 100.00 }
  ]
}
```

### Architect-review fixes applied

After the initial scaffold passed smoke tests, an architect review flagged four
hardenings — three within scope, one explicitly deferred per the task spec.
Applied:

- **Cross-aggregate parent-tenant integrity (writes only).** `InvoiceService`
  now rejects creating an invoice for a customer that belongs to another
  tenant (400). `PaymentService` rejects payments against an invoice that
  belongs to another tenant (400). Row-level read filtering remains
  intentionally out of scope for B01.
- **Duplicate invoice number → 409 Conflict.** `InvoiceService` performs a
  pre-flight `ExistsByTenantAndNumberAsync` check (works under both Pomelo
  MySQL and EF InMemory) and throws a typed `DuplicateInvoiceNumberException`
  the controller maps to 409. The controller also catches the racy
  `DbUpdateException` path so concurrent inserts under MySQL still land on 409
  rather than 500.
- **Domain invariants.** `InvoiceService` rejects `taxAmount < 0`,
  `dueDate < issueDate`, line `Quantity < 1`, line `UnitPrice < 0`, and blank
  line `Description`. These guard against invalid state being persisted by
  any caller (HTTP, future jobs, tests).
- **Money precision.** Every monetary computation (`UnitPrice`, `LineTotal`,
  `Subtotal`, `TaxAmount`, `TotalAmount`, payment `Amount`) is explicitly
  rounded to 2 decimal places with `MidpointRounding.AwayFromZero` in the
  domain layer before being persisted. This keeps in-memory and MySQL
  behavior identical and avoids surprises when EF maps to `decimal(18,2)`.

Re-validation:

| Scenario | HTTP | Notes |
| --- | --- | --- |
| `POST /api/invoices` happy path with `unitPrice 33.333`, `taxAmount 10.005` | 201 | Persisted as `33.33`, `10.01`, `total 110.01`. |
| `POST /api/invoices` for a customer in another tenant | 400 | "Customer ... does not belong to tenant ..." |
| `POST /api/invoices` with duplicate `(TenantId, InvoiceNumber)` | 409 | "An invoice with InvoiceNumber 'X' already exists for tenant ..." |
| `POST /api/invoices` with `dueDate < issueDate` | 400 | "DueDate must be on or after IssueDate." |
| `POST /api/payments` for an invoice in another tenant | 400 | "Invoice ... does not belong to tenant ..." |
| `POST /api/payments` with `amount 1.005` | 201 | Persisted as `1.01`. |

## 7. Known Gaps / Deferred Items

- **MySQL not provisioned in Replit.** Only Postgres is available in this
  environment, so the workflow runs against EF Core In-Memory. The migration is
  generated and committed; applying it against MySQL is a one-liner
  (`dotnet ef database update`) for any host with MySQL reachable.
- **Authentication / authorization** — none. Endpoints accept the `tenantId` in
  the request body and persist it for future row-level enforcement, but no
  middleware enforces it in B01.
- **Invoice status transitions / engine** — invoices are created in `Draft`
  and stay there. A subsequent payment does not (yet) move an invoice to
  `Paid`/`PartiallyPaid`; that lives in a later block.
- **Totals validation against payments** — there is no cross-check that
  `sum(payments.amount) <= invoice.totalAmount`. Negative or zero amounts are
  rejected by the service, but multi-payment reconciliation is out of scope.
- **Tax computation** — `TaxAmount` is currently client-supplied. A real tax
  engine and per-line tax breakdown is deferred.
- **Tests** — TBS-B01 ships no unit/integration test project; the spec lists
  build + runtime smoke tests as the validation surface. A `tests/` project
  mirroring `services/Commerce/tests/Commerce.Tests/` is the obvious next add.
- **Observability** — no Serilog/OpenTelemetry pipeline yet (Commerce has
  these). Default ASP.NET Core console logging is sufficient for B01.
- **Containerization** — no Dockerfile in this block. `dotnet run` is the
  documented runner for now.
- **Brittle duplicate-key detection in the racy fallback path.** The primary
  duplicate-invoice path uses a typed `DuplicateInvoiceNumberException` thrown
  from the domain. The secondary `DbUpdateException`-based fallback in
  `InvoicesController` (only reachable under a true concurrent-insert race on
  MySQL) inspects the inner exception message for "Duplicate" / "UNIQUE" /
  "1062" — convenient but brittle across providers. A follow-up should switch
  to provider-specific error-code inspection (e.g. `MySqlException.Number`).
