# TBS-B02 Report — Customer Management

## 1. Codebase Analysis

The Tenant Billing service from TBS-B01 (`services/tenant-billing-api/`) had a
working but minimal customer slice:

- `Customer` entity carried `Id`, `TenantId`, `Name`, `Email`, `Phone`,
  `BillingAddress`, `CreatedAt`, `UpdatedAt`. No `ExternalReference`, `Notes`,
  or `IsDeleted` field.
- `ICustomerRepository` exposed `AddAsync`, `GetByIdAsync(id)`, `GetAllAsync()`.
  No tenant-scoped lookups, no update, no soft-delete, no search/pagination,
  no email-uniqueness check.
- `CustomerService.CreateAsync` validated required fields and trimmed strings
  but did not normalize email casing and did not enforce per-tenant email
  uniqueness.
- `CustomersController` exposed only `POST /api/customers`, `GET /api/customers`,
  and `GET /api/customers/{id}`. No `PUT`, no `DELETE`, no query parameters,
  no pagination envelope.
- `CreateCustomerRequest` and `CustomerResponse` mirrored the entity 1:1, so
  they also lacked `ExternalReference`, `Notes`, and `IsDeleted`. There was no
  `UpdateCustomerRequest` and no `CustomerListResponse` envelope.
- `InvoiceService.CreateAsync` looked up the parent customer via
  `_customers.GetByIdAsync(customerId)` (id-only) and then verified
  `customer.TenantId == tenantId`. With the new tenant-scoped active lookup
  this collapses to a single call.
- The DbContext mapped Customer to `customers` table with `(TenantId)` and
  `(TenantId, Email)` indexes. The `(TenantId, Email)` index was non-unique,
  matching the old "no duplicate enforcement" behavior.

Gaps identified vs. the TBS-B02 spec: missing fields on the entity/DTOs, no
soft-delete, no per-tenant email uniqueness, no list pagination/search, no
update/delete endpoints, and no tenant-scoped read paths.

## 2. Implementation Steps

1. Hardened the `Customer` entity with `ExternalReference`, `Notes`,
   and `IsDeleted` (default false).
2. Updated the EF Core mapping for the new fields (with column lengths) and
   added an `IsDeleted` index alongside the existing `TenantId` indexes.
3. Generated migration `CustomerManagementEnhancements` to add the three new
   columns + `IX_customers_IsDeleted` index against MySQL/Pomelo.
4. Rewrote `ICustomerRepository` to the spec surface
   (`AddAsync`, `UpdateAsync`, `GetByIdAsync(tenantId, id)`,
   `GetActiveByIdAsync(tenantId, id)`, `ListAsync(tenantId, search, page,
   pageSize)`, `CountAsync(tenantId, search)`,
   `ExistsByTenantAndEmailAsync(tenantId, email, excludingCustomerId)`).
5. Implemented the new repository against EF Core with case-insensitive
   `Contains` search across Name/Email/Phone/ExternalReference, newest-first
   ordering, and `IsDeleted=false` filtering for list/`GetActive`.
6. Expanded `CreateCustomerRequest`, added `UpdateCustomerRequest`, expanded
   `CustomerResponse`, and added `CustomerListResponse` envelope (Items,
   Page, PageSize, TotalCount, TotalPages).
7. Rewrote `CustomerService` with create/update/get/list/delete plus a typed
   `DuplicateEmailException` for 409 mapping. Email is normalized to
   lowercase + trimmed; Name and other strings are trimmed; PageSize is
   clamped to `[1, 100]` with default 25; Page defaults to 1.
8. Replaced `CustomersController` with a full CRUD surface that maps
   `ArgumentException` → 400, `DuplicateEmailException` → 409, missing →
   404, and successful soft-delete → 204.
9. Switched `InvoiceService.CreateAsync` to use the tenant-scoped
   `GetActiveByIdAsync(tenantId, customerId)` so invoices cannot be created
   against deleted or cross-tenant customers.
10. Updated the in-memory test fake (`InMemoryCustomerRepository`) to the new
    interface and added `CustomerServiceTests` covering all rules.
11. Regenerated `dotnet ef migrations script --idempotent` against the
    Pomelo MySQL provider to confirm the migration produces a clean,
    repeatable script.
12. Validated end-to-end against the running `Tenant Billing API` workflow on
    port 5001 (in-memory provider on Replit).

## 3. Files Created / Modified

**Created**

- `services/tenant-billing-api/src/TenantBilling.Domain/Services/DuplicateCustomerEmailException.cs`
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Data/Migrations/20260424164205_CustomerManagementEnhancements.cs`
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Data/Migrations/20260424164205_CustomerManagementEnhancements.Designer.cs`
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/CustomerServiceTests.cs`

**Modified**

- `services/tenant-billing-api/src/TenantBilling.Domain/Entities/Customer.cs`
  — added `ExternalReference`, `Notes`, `IsDeleted`.
- `services/tenant-billing-api/src/TenantBilling.Domain/Repositories/ICustomerRepository.cs`
  — replaced surface with the seven tenant-scoped methods from the spec.
- `services/tenant-billing-api/src/TenantBilling.Domain/Services/ICustomerService.cs`
  — added `UpdateAsync`, `DeleteAsync`, paged `ListAsync`, tenant-scoped `GetAsync`,
  introduced `CustomerPage`, exposed `DefaultPageSize` / `MaxPageSize` constants.
- `services/tenant-billing-api/src/TenantBilling.Domain/Services/CustomerService.cs`
  — implemented validation, email normalization, duplicate-email check, soft-delete,
  pagination clamping.
- `services/tenant-billing-api/src/TenantBilling.Domain/Services/InvoiceService.cs`
  — switched parent-customer lookup to `GetActiveByIdAsync(tenantId, customerId)`,
  collapsing tenant + soft-delete checks into one call.
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Repositories/CustomerRepository.cs`
  — implemented the new repository surface against EF Core.
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Data/TenantBillingDbContext.cs`
  — mapped the three new columns and added `IX_customers_IsDeleted`.
- `services/tenant-billing-api/src/TenantBilling.Infrastructure/Data/Migrations/TenantBillingDbContextModelSnapshot.cs`
  — auto-updated by EF tooling to reflect the new model.
- `services/tenant-billing-api/src/TenantBilling.Api/Contracts/CustomerDtos.cs`
  — expanded `CreateCustomerRequest` and `CustomerResponse`; added
  `UpdateCustomerRequest` and `CustomerListResponse`.
- `services/tenant-billing-api/src/TenantBilling.Api/Controllers/CustomersController.cs`
  — full CRUD: `POST`, paginated `GET` list, tenant-scoped `GET`-by-id, `PUT`,
  soft-deleting `DELETE`. `DuplicateCustomerEmailException` → 409,
  `ArgumentException` → 400.
- `services/tenant-billing-api/tests/TenantBilling.Domain.Tests/Fakes/InMemoryCustomerRepository.cs`
  — re-implemented to satisfy the new `ICustomerRepository` surface (tenant
  scoping, soft-delete filter, search, pagination, email-existence).

No files outside the Tenant Billing service tree were touched.

## 4. API Changes

All endpoints live under the existing `/api/customers` route. `tenantId`
remains in the request body for `POST` and as a query parameter elsewhere
(standalone-mode requirement of the spec).

| Method | Route                                                              | Success | Failure modes                                |
| ------ | ------------------------------------------------------------------ | ------- | -------------------------------------------- |
| POST   | `/api/customers`                                                   | 201     | 400 (validation) / 409 (duplicate email)     |
| GET    | `/api/customers?tenantId=&search=&page=&pageSize=`                 | 200     | 400 (missing/empty `tenantId`)               |
| GET    | `/api/customers/{id}?tenantId=`                                    | 200     | 400 (missing `tenantId`) / 404               |
| PUT    | `/api/customers/{id}?tenantId=`                                    | 200     | 400 (validation) / 404 / 409 (duplicate)     |
| DELETE | `/api/customers/{id}?tenantId=`                                    | 204     | 400 (missing `tenantId`) / 404               |

Request / response schema highlights:

- `CreateCustomerRequest` now accepts `externalReference` and `notes`.
- `UpdateCustomerRequest` is new and intentionally omits `tenantId` (it is a
  query parameter on `PUT`) and `isDeleted` (only `DELETE` toggles it).
- `CustomerResponse` now exposes `externalReference`, `notes`, and `isDeleted`.
- `CustomerListResponse` envelope returns `items`, `page`, `pageSize`,
  `totalCount`, `totalPages`. `pageSize` is clamped server-side to `[1, 100]`
  and defaults to 25; `page` defaults to 1.

Behavior changes for existing endpoints:

- `GET /api/customers/{id}` now requires `?tenantId=` and returns 404 (not
  the prior cross-tenant 200) when the id belongs to another tenant or has
  been soft-deleted.
- `GET /api/customers` now returns the paginated envelope and is
  tenant-filtered. The pre-B02 plain-array response is no longer emitted.
- `POST /api/invoices` now also rejects (400) attempts to invoice a
  soft-deleted customer, in addition to the existing cross-tenant rejection.

## 5. Database Changes

Migration `20260424164205_CustomerManagementEnhancements`:

- `ALTER TABLE customers ADD ExternalReference VARCHAR(200) NULL`
- `ALTER TABLE customers ADD IsDeleted TINYINT(1) NOT NULL DEFAULT 0`
- `ALTER TABLE customers ADD Notes VARCHAR(2000) NULL`
- `CREATE INDEX IX_customers_IsDeleted ON customers (IsDeleted)`

Both `Up` and `Down` are wired correctly. Existing indexes on `(TenantId)`
and `(TenantId, Email)` are preserved unchanged. The `(TenantId, Email)`
index is intentionally still non-unique because (a) it must allow the same
email across tenants, and (b) re-using an email after soft-delete must work
without a database-level uniqueness conflict — uniqueness is enforced in
the service layer with `ExistsByTenantAndEmailAsync` (which filters out
`IsDeleted = true` rows).

`dotnet ef migrations script --idempotent` produced a clean 332-line MySQL
script with both migrations wrapped in `IF NOT EXISTS` guards — safe to run
against an existing or fresh database.

## 6. Validation Results

**Build**

- `dotnet build services/tenant-billing-api/TenantBilling.sln` — 0 warnings,
  0 errors.

**Unit tests**

- `dotnet test services/tenant-billing-api/TenantBilling.sln` — **65 passed,
  0 failed** (40 pre-existing TBS-B01/B02-prior tests still green; 25 new
  `CustomerServiceTests` covering create normalize/dup, update / dup /
  wrong-tenant / deleted, get tenant scope, list tenant scope / search /
  pagination clamp / total pages / soft-delete exclusion, delete soft-delete
  + idempotency, re-using email after soft-delete, and the empty-id guards
  on `GetAsync` / `DeleteAsync`).

**End-to-end smoke tests against the running workflow on port 5001** (using
EF InMemory provider on Replit; behavior is identical under MySQL because
both the entity-defined indexes and the service-level checks are
provider-agnostic):

| Scenario                                              | Expected | Actual |
| ----------------------------------------------------- | -------- | ------ |
| `POST` valid customer (T1)                            | 201      | 201    |
| `POST` blank name                                     | 400      | 400    |
| `POST` malformed email                                | 400      | 400    |
| `POST` duplicate email same tenant (case-insensitive) | 409      | 409    |
| `POST` same email different tenant                    | 201      | 201    |
| `GET` list T1 default                                 | 200, 5 items | 200, 5 items |
| `GET` list T1 `search=globex`                         | 4 items  | 4 items |
| `GET` list T1 `search=GLB-2` (external reference)     | 1 item   | 1 item  |
| `GET` list T1 `pageSize=10000`                        | clamped to 100 | 100 |
| `GET` list T1 `page=2&pageSize=2`                     | 2 of 5, totalPages=3 | 2 of 5, totalPages=3 |
| `GET /{id}` valid                                     | 200      | 200    |
| `GET /{id}` wrong tenant                              | 404      | 404    |
| `GET /{id}` nonexistent                               | 404      | 404    |
| `PUT /{id}` valid                                     | 200      | 200    |
| `PUT /{id}` duplicate email                           | 409      | 409    |
| `PUT /{id}` wrong tenant                              | 404      | 404    |
| `DELETE /{id}` valid                                  | 204      | 204    |
| `DELETE /{id}` already deleted                        | 404      | 404    |
| `GET /{id}` after delete                              | 404      | 404    |
| `PUT /{id}` after delete                              | 404      | 404    |
| `GET` list after delete excludes the row              | excluded | excluded |
| `POST /api/invoices` for soft-deleted customer        | 400      | 400    |
| `POST /api/invoices` cross-tenant                     | 400      | 400    |
| `POST /api/invoices` for active customer              | 201      | 201    |
| `GET /{id}` with empty GUID id                        | 400      | 400    |
| `PUT /{id}` with empty GUID id                        | 400      | 400    |
| `DELETE /{id}` with empty GUID id                     | 400      | 400    |

**Architect-review fix applied**

The architect noted that `CustomerService.GetAsync` / `DeleteAsync` throw
`ArgumentException` for `Guid.Empty` ids, but `CustomersController.GetById` /
`Delete` did not catch them — surfacing the spec-mandated 400 path as a
500 instead. Fixed by mirroring the explicit `tenantId` empty-guid check
on the route id at the top of `GetById`, `Update`, and `Delete`. Two new
unit tests (`Get_rejects_empty_customer_id`, `Delete_rejects_empty_customer_id`)
lock the service-side invariant; the live API now returns 400 for all
three endpoints.

## 7. Known Gaps / Notes

- **Tenant identity is still trusted from the request.** `tenantId` is
  accepted from the request body (POST) or query string (other verbs) per
  the standalone-mode requirement. Authenticating the caller and deriving
  `tenantId` from a verified identity is explicitly scoped out of B02 and
  remains pending as project task #10.
- **Email uniqueness is service-enforced, not database-enforced.** A
  composite unique index would conflict with the “re-use email after
  soft-delete” requirement unless filtered on `IsDeleted=false`, which MySQL
  does not support natively as a partial index. The pragmatic alternative
  (enforce in `CustomerService.ExistsByTenantAndEmailAsync` and accept the
  rare race window) is documented here so a follow-up can choose between
  generated columns + functional unique index, application-level locking,
  or `INSERT ... ON DUPLICATE KEY` patterns when MySQL is brought online.
- **Invoice / Payment lookups still fetch by id without a tenant filter.**
  Customers are now strictly tenant-scoped on every read, but `GET
  /api/invoices/{id}` and `GET /api/payments/...` still return any record
  by id. This is the same row-level read filtering work tracked under
  project task #3 and was deliberately left untouched here.
- **No JWT / no UI / no audit / no notifications / no platform integration**
  per the spec's "DO NOT DO" list.

