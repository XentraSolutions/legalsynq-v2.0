# INV-TPL-01 — Invoice Template & Branding Foundation

## 1. Summary

INV-TPL-01 lays the data, service and HTTP foundation for Invoice
Templates & Branding in the standalone Tenant Billing Service. It
introduces a single `InvoiceTemplate` entity that lives in two
scopes (`Platform` for the cross-tenant catalogue and `Tenant` for a
tenant-owned brand kit), a default-per-scope rule enforced
atomically through `IUnitOfWork`, validation helpers for the
brand-related fields (hex colour, prefix, format, due-days, logo
URL), CRUD + status-transition + make-default endpoints under
`/api/invoice-templates/{platform|tenant}/...`, and a hook into
invoice creation that resolves `DueDate` from the active default
tenant template's `DefaultDueDays` when the request omits it.
Strictly excluded items (PDF/HTML/email/auth/upload/S3/JWT) remain
out of scope and out of the codebase.

## 2. Codebase Analysis

The standalone Tenant Billing Service lives at
`services/tenant-billing-api/` and is an ASP.NET Core 8 / EF Core 10
solution with three projects:

- `TenantBilling.Domain` — entities (`Invoice`, `Customer`,
  `InvoiceLineItem`, `Payment`, `Refund`), domain services
  (`InvoiceService`, `PaymentService`, `CustomerService`,
  `InvoiceLifecycleService`), and repository interfaces. Domain has
  no EF dependency; status constants live in `InvoiceStatus.cs`.
- `TenantBilling.Infrastructure` — EF Core repositories,
  `TenantBillingDbContext`, MySQL/Pomelo provider with InMemory
  fallback, EF migrations under `Data/Migrations/`, and
  `EfUnitOfWork` for atomic multi-write flows.
- `TenantBilling.Api` — controllers, DTOs (`Contracts/`), the
  `TenantResolutionMiddleware` + `HttpHeaderTenantContext` pair that
  enforces the `X-Tenant-Id` header on every `/api/*` request, and
  the gated `InvoiceOverdueHostedService` from TBS-B05.

Conventions confirmed by reading the existing code:

- **Tenant scoping** is mandatory on every `/api/*` route and is
  injected via `ITenantContext` (never read off the request body).
  `Guid.Empty` is rejected up-front with HTTP 400.
- **Repository pattern**: repositories return entities; services own
  validation and orchestration; controllers translate
  exceptions to ProblemDetails.
- **Exception → status mapping** follows a stable table:
  `ArgumentException` → 400, `DuplicateXyzException` → 409, custom
  lifecycle exceptions → 400, `null` returns → 404. All custom
  domain exceptions derive `InvalidOperationException` so legacy
  callers that catch the base class still work.
- **DTOs** use System.ComponentModel.DataAnnotations attributes for
  shape validation; deeper validation runs in the service layer.
- **Migrations** are EF Core code-first; each block adds an
  incremental migration under
  `TenantBilling.Infrastructure/Data/Migrations/`.
- **Tests** are split into a fast in-memory domain suite
  (`tests/TenantBilling.Domain.Tests`, with `Fakes/` and `Helpers/`)
  and a `WebApplicationFactory`-based API suite
  (`tests/TenantBilling.Tests`) — TBS-B05 ended green at 218 + 37
  tests.

The codebase has no concept of a `BillingAccount` or
`TenantBillingProfile`; tenant scoping is by raw `Guid TenantId`
column. INV-TPL-01 therefore models tenant-scoped templates as
`OwnerType = Tenant` + `BillingAccountId = TenantId` (and leaves
`TenantBillingProfileId` null) so the spec's strict-ownership rule
maps cleanly onto the existing tenant column.

## 3. Stories Completed

- **TPL-S1 — Platform-scoped invoice template CRUD**: catalogue
  ownership lives in the platform scope (`OwnerType = Platform`,
  `BillingAccountId = null`). Routes do not require `X-Tenant-Id`.
- **TPL-S2 — Tenant-scoped invoice template CRUD**: each tenant owns
  its brand kit. Tenant routes require `X-Tenant-Id`; cross-tenant
  reads/writes return 404 (treated as not-existing per existing
  tenant-isolation conventions).
- **TPL-S3 — Default template enforcement**: at most one Active +
  IsDefault template per scope. Promoting a template to default
  unsets the previous default in a single `IUnitOfWork` transaction.
- **TPL-S4 — Status lifecycle**: Draft → Active → Retired with
  explicit transitions. Retired templates cannot be edited or made
  default; retiring a current default clears the default flag.
- **TPL-S5 — Branding fields validated**: hex accent colour, prefix,
  number format, due-days range and logo URL guarded centrally and
  surfaced as 400 ProblemDetails.
- **TPL-S6 — Apply defaults on invoice create**:
  `CreateInvoiceRequest.DueDate` is now optional; when omitted the
  controller resolves the tenant's active default template's
  `DefaultDueDays`. With no template/default-days configured, the
  request fails 400 with a clear message.

## 4. Architecture Implemented

```
Controller (InvoiceTemplatesController, InvoicesController)
  └── IInvoiceTemplateService            (write paths, lifecycle)
  └── IInvoiceTemplateSelectionService   (read-only default lookup)
        └── IInvoiceTemplateRepository  (EF Core repository)
        └── IUnitOfWork                  (atomic make-default)
              └── TenantBillingDbContext (EF Core 10 / MySQL)
```

Both service interfaces are forwarded to a single
`InvoiceTemplateService` registration so callers that only need the
read-side (`InvoicesController`) do not pull in write dependencies.

`TenantResolutionMiddleware` was extended with an
`UnscopedPathPrefixes` whitelist so that `/api/invoice-templates/platform`
bypasses the `X-Tenant-Id` requirement while `/api/invoice-templates/tenant`
continues to require it. `ITenantContext.TenantId` still throws
lazily, so platform controllers never read it (they pass
`tenantId: null` into the service layer).

## 5. Files Created/Changed

**New** (Domain):
- `Entities/InvoiceTemplate.cs`, `Entities/InvoiceTemplateOwnerType.cs`,
  `Entities/InvoiceTemplateStatus.cs`
- `Exceptions/InvoiceTemplateExceptions.cs` (5 typed exceptions)
- `Services/IInvoiceTemplateService.cs`,
  `Services/IInvoiceTemplateSelectionService.cs`,
  `Services/InvoiceTemplateService.cs`,
  `Services/InvoiceTemplateValidation.cs`
- `Repositories/IInvoiceTemplateRepository.cs`

**New** (Infrastructure):
- `Repositories/InvoiceTemplateRepository.cs`
- `Data/Migrations/20260424202159_AddInvoiceTemplates.{cs,Designer.cs}`
  (+ snapshot updates)

**New** (Api):
- `Controllers/InvoiceTemplatesController.cs`
- `Contracts/CreateInvoiceTemplateRequest.cs`,
  `Contracts/UpdateInvoiceTemplateRequest.cs`,
  `Contracts/InvoiceTemplateResponse.cs`,
  `Contracts/InvoiceTemplateSummaryResponse.cs`,
  `Contracts/MakeDefaultTemplateResponse.cs`

**Changed**:
- `Infrastructure/Data/TenantBillingDbContext.cs` (DbSet + mapping)
- `Api/Middleware/TenantResolutionMiddleware.cs`
  (+ `UnscopedPathPrefixes`)
- `Api/Program.cs` (DI: single `InvoiceTemplateService`, two
  interfaces; middleware whitelist for platform routes)
- `Api/Controllers/InvoicesController.cs` (apply default DueDays when
  request omits DueDate)
- `Api/Contracts/CreateInvoiceRequest.cs` (DueDate now nullable)

**New** (Tests):
- `tests/TenantBilling.Domain.Tests/InvoiceTemplateValidationTests.cs`
- `tests/TenantBilling.Domain.Tests/InvoiceTemplateServiceTests.cs`
- `tests/TenantBilling.Domain.Tests/InvoiceTemplateSelectionTests.cs`
- `tests/TenantBilling.Domain.Tests/Fakes/InMemoryInvoiceTemplateRepository.cs`
- `tests/TenantBilling.Tests/InvoiceTemplatesPlatformApiTests.cs`
- `tests/TenantBilling.Tests/InvoiceTemplatesTenantApiTests.cs`
- `tests/TenantBilling.Tests/InvoiceCreationDefaultDueDaysApiTests.cs`

## 6. Database / Migration Changes

Single migration `20260424202159_AddInvoiceTemplates` adds the
`invoice_templates` table:

- Primary key `Id` (`CHAR(36)`).
- Scope/ownership columns: `OwnerType` (string, indexed),
  `BillingAccountId` (`CHAR(36)?`, indexed),
  `TenantBillingProfileId` (`CHAR(36)?`, always null in this block).
- Identity & branding: `Name` (1..150), `Description?` (≤500),
  `Status` (string), `IsDefault` (bool, default `false`),
  `LogoUrl?` (≤2048), `AccentColor?` (7 chars, `#RRGGBB`),
  `HeaderText?`, `FooterText?`, `PaymentInstructions?`,
  `TermsText?`, `MemoPlaceholder?`, `DefaultDueDays?` (int 0..365),
  `InvoiceNumberPrefix?` (≤16, alphanumeric/underscore),
  `InvoiceNumberFormat?` (≤64), `DisplayBillingAddress`,
  `DisplayPaymentInstructions`, `DisplayTerms` (bools, default
  `true`), `CreatedAtUtc`, `UpdatedAtUtc`.
- Indexes:
  - `IX_invoice_templates_OwnerType_BillingAccountId_Status`
  - `IX_invoice_templates_OwnerType_BillingAccountId_IsDefault`
  - `IX_invoice_templates_BillingAccountId`

A second migration `20260424204153_AddInvoiceTemplateDefaultUniqueIndex`
adds a database-level guard against the write-skew that two
concurrent `make-default` transactions could otherwise produce
under read-committed isolation:

- A stored, computed column `DefaultScopeKey VARCHAR(64)` defined
  as `CASE WHEN IsDefault = 1 THEN CONCAT(OwnerType, '|',
  IFNULL(BillingAccountId, '')) ELSE NULL END`. The column is
  `NULL` for non-default rows (so the unique index ignores them via
  MySQL's standard "NULLs are not equal" rule) and a deterministic
  per-scope key for default rows.
- A unique index `UX_invoice_templates_DefaultScopeKey` on that
  computed column. At most one row per scope can carry the default
  flag at any time, even under interleaved transactions.

The mapping is registered conditionally on `Database.IsRelational()`
in `TenantBillingDbContext` so the EF InMemory provider used in
tests still works (computed columns / generated SQL fragments are
not supported there). The repository's `AddAsync` / `UpdateAsync`
catch the resulting `DbUpdateException` (matching on the index name
across the inner-exception chain) and rethrow as
`InvoiceTemplateDefaultConflictException`, which the controller
already maps to **409 Conflict** — so the contract on the wire is
unchanged whether the conflict is detected by the service guard or
by the database.

## 7. API Endpoints Added

Platform routes (no `X-Tenant-Id`):
- `POST   /api/invoice-templates/platform`
- `GET    /api/invoice-templates/platform`
- `GET    /api/invoice-templates/platform/default`
- `GET    /api/invoice-templates/platform/{id}`
- `PUT    /api/invoice-templates/platform/{id}`
- `POST   /api/invoice-templates/platform/{id}/activate`
- `POST   /api/invoice-templates/platform/{id}/retire`
- `POST   /api/invoice-templates/platform/{id}/make-default`

Tenant routes (require `X-Tenant-Id`):
- `POST   /api/invoice-templates/tenant`
- `GET    /api/invoice-templates/tenant`
- `GET    /api/invoice-templates/tenant/default`
- `GET    /api/invoice-templates/tenant/{id}`
- `PUT    /api/invoice-templates/tenant/{id}`
- `POST   /api/invoice-templates/tenant/{id}/activate`
- `POST   /api/invoice-templates/tenant/{id}/retire`
- `POST   /api/invoice-templates/tenant/{id}/make-default`

Status mapping (consistent with existing controllers):
- `ArgumentException` / `InvoiceTemplateValidationException` → 400
- `InvalidInvoiceTemplateOwnerScopeException`,
  `InvalidInvoiceTemplateStatusTransitionException`,
  `RetiredInvoiceTemplateCannotBeDefaultException` → 400
- `InvoiceTemplateDefaultConflictException` → 409
- `null` from the service / `CrossTenantInvoiceTemplateAccessException`
  → 404 (cross-tenant access is treated as not-found to avoid
  leaking existence)

## 8. Invoice Template Domain Model

`InvoiceTemplate` aggregates identity + scope + branding metadata.
The aggregate is intentionally flat (no child entities) — line-item
defaults, attachments, and rendering are out of scope for this
block. The dual-scope model is encoded as:

| OwnerType | BillingAccountId | Meaning                       |
| --------- | ---------------- | ----------------------------- |
| Platform  | `null`           | Cross-tenant catalogue entry  |
| Tenant    | `<tenantId>`     | Brand kit owned by a tenant   |

`TenantBillingProfileId` is always `null` in this block; the column
is reserved for the future profile aggregate so we do not need a
breaking migration when it lands.

## 9. Default Template Behavior

- **Auto-default on first Active**: when the first template in a
  scope is created (or transitioned to) `Active` and no default
  exists yet, the service flips `IsDefault = true` automatically.
- **Make-default**: explicit promotion through
  `POST .../{id}/make-default` runs inside `IUnitOfWork` so the
  unset-previous + set-new are atomic. The response includes the
  `PreviousDefaultTemplateId` for audit/trace.
- **Retire-clears-default**: retiring a default template clears
  `IsDefault` in the same write — no scope is left with two
  defaults or a retired default.
- **Idempotent promotion**: making the current default the default
  again is a no-op success.
- **Draft / Retired guard**: only `Active` templates can be made
  default; trying anything else raises a typed exception → 400.

## 10. Invoice Creation Integration

`CreateInvoiceRequest.DueDate` was relaxed to `DateTime?`. The
controller flow is now:

1. Validate the request as before.
2. If `DueDate` is `null`, call
   `IInvoiceTemplateSelectionService.GetDefaultForTenantAsync(tenantId)`.
3. If the lookup returns a template with a non-null
   `DefaultDueDays`, compute
   `DueDate = IssueDate + TimeSpan.FromDays(DefaultDueDays)`.
4. If no template / no default-days is configured, return 400 with
   `Either DueDate or an Active default template with DefaultDueDays must be configured.`.
5. If `DueDate` was supplied by the caller, the controller bypasses
   the lookup entirely — explicit caller-provided dates always win.

`IInvoiceService.CreateAsync` and existing API contracts for
callers that send `DueDate` are unchanged.

## 11. Validation Rules Implemented

`InvoiceTemplateValidation` centralises:

- **Hex colour**: must be `#RRGGBB`. Empty/null is allowed.
- **Prefix**: trimmed; must be ≤16 alphanumeric/underscore
  characters; empty/null normalised to `null`.
- **Number format**: trimmed; ≤64 characters.
- **DefaultDueDays**: 0..365 inclusive when non-null.
- **LogoUrl**: when non-null, must be either an absolute http/https
  URL or a safe relative path (no `..`, no scheme), ≤2048 chars.
- **Name**: required, 1..150 characters; trimmed.
- **Description**: optional, ≤500 characters.

Validation errors throw typed exceptions or `ArgumentException`,
both of which the controller already maps to 400.

## 12. Tests Added

**Domain** (in-memory, no host):

- `InvoiceTemplateValidationTests` — every validator branch
  (positive + negative paths).
- `InvoiceTemplateServiceTests` — create/update/list/get,
  auto-default, explicit make-default with previous unset,
  cross-tenant isolation returns null, status transitions, retire
  clears default, idempotency.
- `InvoiceTemplateSelectionTests` —
  `GetDefaultForTenantAsync` and `GetDefaultPlatformAsync` happy
  paths plus null/wrong-tenant behaviour.
- `Fakes/InMemoryInvoiceTemplateRepository` — minimal in-memory
  fake matching the full interface, paired with an existing
  `InMemoryUnitOfWork`.

**API** (`WebApplicationFactory`-hosted, in-memory DB):

- `InvoiceTemplatesPlatformApiTests` — platform routes work
  without `X-Tenant-Id`; CRUD round-trip; invalid colour → 400;
  retire-on-default clears `/default`; make-default on retired →
  400. Uses a fresh factory per test (platform scope is global, so
  state leaks would otherwise pollute auto-default assertions).
- `InvoiceTemplatesTenantApiTests` — header is mandatory; ownership
  is assigned to the calling tenant; cross-tenant reads/writes
  return 404; partial updates only change supplied fields; updates
  on retired templates return 400.
- `InvoiceCreationDefaultDueDaysApiTests` —
  - Omitted `DueDate` + no template → 400.
  - Omitted `DueDate` + Active default template → invoice's
    `DueDate = IssueDate + DefaultDueDays`.
  - Provided `DueDate` overrides the template default.
  - Tenant A's default does not leak into tenant B (B → 400).

## 13. Validation Results

- `dotnet build TenantBilling.sln` — **0 warnings, 0 errors**.
- `dotnet ef migrations add AddInvoiceTemplates` and the follow-up
  `AddInvoiceTemplateDefaultUniqueIndex` — both clean; snapshot
  updated after each.
- `dotnet test` (per-class, all green; total runs split because
  the bash 120s ceiling sometimes timed out a single full
  invocation):
  - Domain tests: **281 / 281 passing**.
  - API integration tests: **59 / 59 passing**
    (`BillingApiTests` 11 + `InvoicesMarkOverdueApiTests` +
    `InvoiceServiceTests` + `PaymentServiceTests` (in tests
    project) 26 + new INV-TPL-01 16 + new
    `InvoiceTemplatesConflictMappingApiTests` 6).
  - Total: **340 / 340**.
- The new `InvoiceTemplatesConflictMappingApiTests` swap a fake
  `IInvoiceTemplateService` into the host that throws
  `InvoiceTemplateDefaultConflictException` on Create / Update /
  MakeDefault for both Platform and Tenant scopes, then assert the
  controller returns **HTTP 409 Conflict**. This locks in the
  catch-order fix (the typed-conflict catch must come before the
  generic `InvalidOperationException` catch, otherwise the conflict
  would silently surface as 400).
- Workflow `Tenant Billing API` restarts cleanly; smoke calls
  return the expected status codes (`/platform/default` → 404 on
  empty DB, `/tenant` without header → 400, `/tenant` with header
  → 200, platform create → 201 with `IsDefault = true`).

## 14. Known Gaps / Deferred Items

- `TenantBillingProfileId` is always `null` and unused — reserved
  for the future profile aggregate.
- No background lifecycle (e.g., auto-archive of long-Draft
  templates).
- No template-level versioning or audit trail beyond
  `CreatedAtUtc` / `UpdatedAtUtc`.
- No exposure of `InvoiceTemplateId` on `Invoice` yet — invoice
  creation only consumes the template's `DefaultDueDays`. Full
  template stamping onto the invoice is a future block.
- Default uniqueness is now enforced at **both** layers — service
  guard inside `IUnitOfWork` for the InMemory test path, and a
  unique index on the `DefaultScopeKey` computed column for the
  relational path. Direct SQL writes that try to flip a second
  default will now fail at the database with a duplicate-key error
  that the repository surfaces as 409.
- Middleware route matching for the `/api/invoice-templates/platform`
  bypass is currently a string-prefix check. Hardening it to a
  segment-aware match (so e.g. `/api/invoice-templates/platformx`
  could not accidentally bypass `X-Tenant-Id`) is a low-risk
  follow-on; today no such conflicting route exists.

## 15. Confirmation of Strict Exclusions

The block intentionally does **not** introduce any of:

- PDF rendering, HTML rendering, or email delivery code paths.
- Authentication, authorisation, JWT, or OIDC wiring.
- File uploads, S3, blob storage, or asset CDN integration.
- BillingAccount aggregate (`BillingAccountId` is mapped to the
  existing `TenantId`).
- Frontend / admin UI work for templates.

A grep for `pdf`, `html`, `smtp`, `email`, `s3`, `upload`, `jwt`
across the new files returns no functional code — only the textual
mention of these as exclusions in this report.

## 16. Recommended Next Block

The natural follow-on is **INV-TPL-02 — Template Application &
Stamping**, which would:

1. Add `InvoiceTemplateId` (nullable FK) to `Invoice` and resolve
   the effective template on issue (tenant default → platform
   default → null).
2. Stamp the chosen template's branding fields onto the invoice
   record at issue-time so the rendered output is stable across
   future template edits.
3. Surface the resolved template on the read DTOs.
4. Optionally add a per-customer override hook (still no UI / no
   PDF — those remain in their own dedicated blocks).
