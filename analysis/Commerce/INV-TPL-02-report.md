# INV-TPL-02 — Template Application & Stamping

**Service:** `services/tenant-billing-api/` (standalone .NET 8)
**Block:** INV-TPL-02
**Goal:** Add an `InvoiceTemplateId` reference and a branding **snapshot** to
`Invoice` so historical invoices preserve their template appearance even
after the template is later edited or retired. Stamping happens at create
time and is *ensured* (not re-applied) at issue time. Effective template
selection follows the chain:
`explicit id (validated) → tenant default → null`. Backward compatible
(template fully optional). **Strict exclusions:** no PDF/HTML/email/
SendGrid/upload/S3/UI/auth/JWT/tax/refund/payment-provider work.

---

## 1. Codebase Analysis

### 1.1 Where INV-TPL-01 left things

INV-TPL-01 introduced the platform- and tenant-scoped
`InvoiceTemplate` aggregate, the EF mapping, the
`IInvoiceTemplateService` (admin write surface) and
`IInvoiceTemplateSelectionService` (read surface), the typed
exception hierarchy under `InvoiceTemplateException`, the
default-uniqueness DB index, and the first hook into invoice
creation: when `CreateInvoiceRequest.DueDate` is omitted the
controller resolves it via
`IInvoiceTemplateSelectionService.GetDefaultForTenantAsync` and
adds the template's `DefaultDueDays`.

That hook is the only existing coupling between an invoice and a
template — the `Invoice` entity itself has zero columns referring to
a template today. INV-TPL-02 builds on that hook: the same selection
result that drives `DefaultDueDays` will now also drive the
**branding snapshot** stamped onto the invoice row.

### 1.2 Invoice aggregate shape

`Invoice` (`services/tenant-billing-api/src/TenantBilling.Domain/
Entities/Invoice.cs`) is a flat anaemic entity (no domain methods):

- IDs and tenancy: `Id`, `TenantId`, `CustomerId`, `InvoiceNumber`.
- Money: `Subtotal`, `TaxAmount`, `DiscountAmount`, `TotalAmount`,
  `Currency`.
- Dates / lifecycle: `IssueDate`, `DueDate`, `Status`, `IssuedAt`,
  `CreatedAt`, `UpdatedAt`.
- Free text: `Notes`.
- Nav collections: `LineItems`, `Payments`, `Refunds`.

There are no value-object properties (e.g. no `Money`, no
`Branding`) — the codebase pattern is "flat columns on the
aggregate root". INV-TPL-02 follows that pattern by adding
template-snapshot columns directly on `Invoice` rather than
introducing a new owned-entity sub-aggregate.

### 1.3 Invoice service / repository shape

`InvoiceService` (`services/tenant-billing-api/src/
TenantBilling.Domain/Services/InvoiceService.cs`):

- `CreateAsync` validates inputs, resolves the invoice number
  (auto-allocate or duplicate-check), composes the entity, and
  delegates to `IInvoiceRepository.AddAsync` for a single
  `SaveChanges` write.
- `IssueAsync` does a tenant-scoped read, validates the lifecycle
  transition (`Draft → Issued`) via `InvoiceLifecycleService`,
  then calls `IInvoiceRepository.UpdateStatusAsync(... Issued ...)`
  which loads a tracked instance, mutates `Status` /
  `UpdatedAt` / `IssuedAt`, and saves.
- Other transitions (Void / Reevaluate / MarkOverdue / Refund)
  follow the same shape: tenant-scoped read → lifecycle gate →
  selective `UpdateStatusAsync` write.

`InvoiceRepository` (`services/tenant-billing-api/src/
TenantBilling.Infrastructure/Repositories/InvoiceRepository.cs`):

- `AddAsync` does a single full-row insert.
- `UpdateStatusAsync` is a focused selective update — only the
  three lifecycle fields (`Status`, `UpdatedAt`, `IssuedAt`)
  are touched. Critically it **loads a tracked entity** before
  mutating, so if we mutate additional fields on that tracked
  instance they will also be persisted.

This shape gives INV-TPL-02 two clean integration points:

- **Create-time stamping**: stamp the snapshot fields onto the
  in-memory `Invoice` *before* `AddAsync` so a single insert
  carries everything.
- **Issue-time ensure-stamping**: a separate repo write that
  loads the tracked invoice, copies the selected template's
  branding fields onto it, and saves. We do this *before* the
  existing `UpdateStatusAsync` so each call is a focused write
  with a single responsibility.

### 1.4 Selection service shape

`IInvoiceTemplateSelectionService` (already in
`IInvoiceTemplateService.cs`) currently exposes two methods:

- `GetDefaultForTenantAsync(tenantId, ct)`
- `GetDefaultPlatformAsync(ct)`

Both return `Active`-status templates only; `Draft` and `Retired`
are filtered defensively even when `IsDefault=true` (see
`InMemoryInvoiceTemplateRepository.GetDefaultInScopeAsync`).
INV-TPL-02 layers two new methods on top:

- `SelectForTenantInvoiceAsync(tenantId, explicitTemplateId?, ct)`
- `SelectForPlatformInvoiceAsync(explicitTemplateId?, ct)`

both implementing the chain:
"explicit id (validated: in scope, Active) → tenant default
→ null". Validation for the explicit branch raises two new typed
exceptions:

- `InvoiceTemplateNotFoundInScopeException` — 400. The id does not
  exist in the caller's scope (or exists in another scope); we
  return 400 (not 404) because the failing resource is the *invoice
  create* request, which itself doesn't exist yet.
- `InvoiceTemplateNotSelectableException` — 400. The id exists in
  the right scope but its status is `Draft` or `Retired`.

Both derive `InvoiceTemplateException : InvalidOperationException`
so the existing `catch (InvalidOperationException)` mappings in
`InvoicesController` already turn them into 400s with no further
work. We mention them explicitly only to make intent obvious.

### 1.5 Controller shape

`InvoicesController.Create` already injects
`IInvoiceTemplateSelectionService` for the default-due-days path
and runs *before* the service call. INV-TPL-02 simply broadens
that selection from "give me the default" to "select for this
invoice (explicit override or default)" and reuses the **same
selected template** for both:

- `effectiveDueDate = IssueDate + template.DefaultDueDays` (when
  request omits `DueDate`) — already wired.
- `_service.CreateAsync(... template, ct)` — new pass-through.

This avoids hitting the selection service twice on the create
path, and keeps the service layer's input simple (it receives a
fully-resolved `InvoiceTemplate?` and never has to decide policy).

### 1.6 Persistence — DbContext + migrations

`TenantBillingDbContext` (`services/tenant-billing-api/src/
TenantBilling.Infrastructure/Data/TenantBillingDbContext.cs`) is
the EF Core mapping. New columns are added in
`OnModelCreating` under the existing `Entity<Invoice>` block, then
shipped via a migration in
`services/tenant-billing-api/src/TenantBilling.Infrastructure/
Data/Migrations/`. The dev environment already runs against the
EF InMemory provider when no MySQL connection string is present
(see `DependencyInjection.AddTenantBillingInfrastructure`), so the
existing test suite continues to work without touching the
migration.

### 1.7 Test scaffolding

Domain unit tests live under
`services/tenant-billing-api/tests/TenantBilling.Domain.Tests/`
and use:

- `InMemoryInvoiceRepository`, `InMemoryInvoiceTemplateRepository`,
  `InMemoryUnitOfWork` (under `Fakes/`).
- `TestData` helpers (under `Helpers/`) for seeding customers
  and invoices.

API tests live under
`services/tenant-billing-api/tests/TenantBilling.Tests/` and use
`TenantBillingWebApplicationFactory` which:

- Forces the InMemory provider per-factory-instance.
- Provides `CreateClientForTenant(tenantId)` so every test sets the
  required `X-Tenant-Id` header up front.

INV-TPL-02 adds:

- Three new domain test files
  (`InvoiceTemplateStampingTests.cs`,
  `InvoiceTemplateSelectionForInvoiceTests.cs`,
  `InvoiceServiceStampingTests.cs`).
- One new API test file
  (`InvoicesTemplateStampingApiTests.cs`).
- Mirror impl of the new repo method in
  `InMemoryInvoiceRepository`.

---

## 2. Architecture & Design Decisions

### 2.1 "Snapshot, not live reference"

The key invariant for this block is that an invoice's branding
*at the time it was issued* never silently changes when an
operator later edits the template. Two implementation shapes
satisfy this; we deliberately pick the simpler one:

- **(Chosen) Flat snapshot columns on `Invoice`.** Add
  nullable `InvoiceTemplate*` fields directly on the entity.
  Stamping = a copy of the relevant template fields at create or
  issue time. Reading a historical invoice is a single SELECT
  with no template join; templates can later be deleted with
  no foreign-key drama (we deliberately do NOT add a FK).
- **(Rejected) Owned `InvoiceTemplateSnapshot` value object.**
  Cleaner aggregate boundary on paper, but adds an EF owned-type
  configuration, a nested DTO record, and a deserialization path
  for every existing read. The codebase pattern is flat columns
  everywhere, so introducing one owned type just for this block
  would be inconsistent.

The flat approach also matches the INV-TPL-02 spec wording:
"add `InvoiceTemplateId` reference + branding snapshot fields to
invoices."

### 2.2 Field set on the snapshot

Per the spec, the new columns on `Invoice` are:

| Column                                   | Type      |
|------------------------------------------|-----------|
| `InvoiceTemplateId`                      | `Guid?`   |
| `TemplateOwnerType`                      | `string?` |
| `TemplateName`                           | `string?` |
| `TemplateLogoUrl`                        | `string?` |
| `TemplateAccentColor`                    | `string?` |
| `TemplateHeaderText`                     | `string?` |
| `TemplateFooterText`                     | `string?` |
| `TemplatePaymentInstructions`            | `string?` |
| `TemplateTermsText`                      | `string?` |
| `TemplateMemoPlaceholder`                | `string?` |
| `TemplateDisplayBillingAddress`          | `bool`    |
| `TemplateDisplayPaymentInstructions`     | `bool`    |
| `TemplateDisplayTerms`                   | `bool`    |
| `TemplateStampedAtUtc`                   | `DateTime?` |

Lengths/precision mirror the corresponding `InvoiceTemplate`
columns. `TemplateOwnerType` is a `VARCHAR(16)` — same as the
template's column. The three display flags default to `false`
(an invoice with no stamp shows no template-driven sections);
they are flipped to the template's setting at stamp time.

We deliberately exclude template fields that are about
*template administration*, not *invoice rendering*:
`Description`, `IsDefault`, `Status`, `DefaultDueDays`,
`InvoiceNumberPrefix`, `InvoiceNumberFormat`,
`TenantBillingProfileId`, `BillingAccountId`, `CreatedAtUtc`,
`UpdatedAtUtc`. These never appear on a rendered invoice and
adding them would just bloat the row.

### 2.3 Selection chain

`SelectForTenantInvoiceAsync(tenantId, explicitTemplateId, ct)`:

1. If `explicitTemplateId` is `null` → return
   `GetDefaultForTenantAsync(tenantId, ct)` (which already filters
   to `Active`).
2. Otherwise:
   - `var t = await GetByIdInScopeReadOnlyAsync(tenantId, id)`
   - If `t is null` → throw `InvoiceTemplateNotFoundInScopeException`.
   - If `t.Status != Active` → throw
     `InvoiceTemplateNotSelectableException`.
   - Return `t`.

`SelectForPlatformInvoiceAsync` mirrors the same with
`tenantId = null`. Both return `null` when the chain bottoms out
(no explicit, no default) — an invoice without a template is
fully supported.

### 2.4 Stamping service

`IInvoiceTemplateStampingService` is a tiny pure-ish service
(no DB I/O of its own) with a single method:

```csharp
void StampInvoice(Invoice invoice, InvoiceTemplate template,
                  DateTime nowUtc);
```

It copies template → invoice for every snapshot field listed
in §2.2 and sets `TemplateStampedAtUtc = nowUtc`. We chose a
service (rather than a static helper or an extension method) so
it can be mocked in tests and so DI keeps the dependency graph
explicit.

The "ensure" semantics live one layer up in `InvoiceService`
(see §2.5) — the stamping service itself never decides whether
to stamp. This keeps responsibilities clean: the service either
gets called or it doesn't, but it never silently no-ops.

### 2.5 Integration into `InvoiceService`

**Create path:**

`InvoiceService.CreateAsync` gains an optional last-positional
parameter `InvoiceTemplate? template = null` (immediately before
`ct`). When non-null, the in-memory `Invoice` is stamped *before*
`_repository.AddAsync` so a single insert carries the full row.
Existing callers (controllers, tests) keep compiling because the
new parameter is optional and added at the end of the
non-cancellation arguments.

**Issue path:**

`InvoiceService.IssueAsync` is extended with the "ensure stamp"
rule:

1. Read the invoice (existing).
2. Validate `Draft → Issued` transition (existing).
3. **NEW:** if `invoice.InvoiceTemplateId is null` AND a tenant
   default template exists, call
   `_repository.ApplyStampAsync(tenantId, invoiceId, template,
   nowUtc, ct)` — this is a new repo method that does a tracked
   read + selective save of the snapshot fields.
4. Call `_repository.UpdateStatusAsync(... Issued ...)` (existing).

We deliberately make these *two* writes rather than fold the
stamp into `UpdateStatusAsync`. Reasoning:

- `UpdateStatusAsync` is reused by Void / Reevaluate / Refund /
  MarkOverdue. Conditionally branching its body for a stamp
  would couple unrelated flows.
- The ensure-stamp window is rare (only invoices that were
  created without an effective template ever take that branch),
  so the extra round-trip is negligible.
- Each repo method retains a single responsibility, which is
  the convention in this codebase.

### 2.6 Why no FK on `Invoice.InvoiceTemplateId`

The reference is a snapshot anchor, not an enforced relation.
Once a template is retired or deleted, we *want* the invoice's
copy of its branding to keep working — the whole point of the
snapshot is decoupling. A FK would either:

- prevent template deletion (operationally annoying, no
  business value), or
- cascade-null the id (acceptable, but EF would surface a model
  warning every time a template was deleted, which we'd then
  have to suppress).

Skipping the FK while keeping a non-unique index on
`InvoiceTemplateId` gives us O(1) "list invoices using template
X" queries (a likely future admin UI feature) without the
deletion friction.

### 2.7 Error mapping

| Error                                                        | HTTP |
|--------------------------------------------------------------|------|
| `InvoiceTemplateNotFoundInScopeException` (explicit id)      | 400  |
| `InvoiceTemplateNotSelectableException` (Draft/Retired id)   | 400  |
| Default-resolution returns `null` (no template configured)   | 400 *(only when `DueDate` also omitted — pre-existing rule)* |
| Default-resolution returns `null` (template optional path)   | 200 / 201 *(invoice is created with no snapshot)* |

Both new exceptions derive `InvoiceTemplateException`, which
derives `InvalidOperationException`. The controller's existing
`catch (InvalidOperationException)` clause maps them to 400 with
no extra wiring — we keep the catch order unchanged so the
duplicate-number 409 still wins where applicable.

### 2.8 Backward compatibility checklist

- `CreateInvoiceRequest.InvoiceTemplateId` is nullable; existing
  callers that omit it land in the "tenant default → null" branch
  exactly as before.
- `InvoiceResponse.TemplateSnapshot` is nullable; absent when no
  stamp has occurred (existing clients ignoring unknown fields
  are unaffected).
- `InvoiceService.CreateAsync` adds an optional parameter; all
  existing positional callers compile.
- `IssueAsync` signature is unchanged; behavior on
  *already-stamped* invoices is unchanged (the ensure-stamp
  guard short-circuits).
- The new EF migration only ADDS columns + one index; no
  existing column types or indexes are altered.

---

## 3. Domain Model Changes

The `Invoice` entity (`TenantBilling.Domain/Entities/Invoice.cs`) gains
14 new properties — one FK-shaped reference plus a verbatim branding
snapshot. All are intentionally optional / default-safe so existing
rows and code paths stay valid:

| Property                                 | Type        | Notes                                                                  |
|------------------------------------------|-------------|------------------------------------------------------------------------|
| `InvoiceTemplateId`                      | `Guid?`     | The template that was stamped. NOT a navigation property — pure id.    |
| `TemplateOwnerType`                      | `string?`   | `"Tenant"` or `"Platform"` — copied from `InvoiceTemplate.OwnerType`.  |
| `TemplateName`                           | `string?`   | Snapshot of `Name` at stamp time.                                      |
| `TemplateLogoUrl`                        | `string?`   | Snapshot.                                                              |
| `TemplateAccentColor`                    | `string?`   | Snapshot.                                                              |
| `TemplateHeaderText`                     | `string?`   | Snapshot.                                                              |
| `TemplateFooterText`                     | `string?`   | Snapshot.                                                              |
| `TemplatePaymentInstructions`            | `string?`   | Snapshot.                                                              |
| `TemplateTermsText`                      | `string?`   | Snapshot.                                                              |
| `TemplateMemoPlaceholder`                | `string?`   | Snapshot.                                                              |
| `TemplateDisplayBillingAddress`          | `bool`      | Default `false`. Snapshot.                                             |
| `TemplateDisplayPaymentInstructions`     | `bool`      | Default `false`. Snapshot.                                             |
| `TemplateDisplayTerms`                   | `bool`      | Default `false`. Snapshot.                                             |
| `TemplateStampedAtUtc`                   | `DateTime?` | UTC instant of the stamp. `null` for un-stamped invoices.              |

The entity remains anaemic (no domain methods). All mutation goes
through `InvoiceTemplateStampingService`. The bool flags are
non-nullable for storage simplicity; the absence of a stamp is
indicated by `InvoiceTemplateId is null` (or equivalently
`TemplateStampedAtUtc is null`), not by the flags themselves.

## 4. Service Layer Changes

Three layers change in `TenantBilling.Domain/Services/`:

**`IInvoiceTemplateSelectionService`** (read-only) gains two new
methods alongside the existing `GetDefaultForTenantAsync` /
`GetDefaultForPlatformAsync`:

```csharp
Task<InvoiceTemplate?> SelectForTenantInvoiceAsync(
    Guid tenantId, Guid? explicitTemplateId, CancellationToken ct = default);

Task<InvoiceTemplate?> SelectForPlatformInvoiceAsync(
    Guid? explicitTemplateId, CancellationToken ct = default);
```

Each implements the chain `explicit → tenant default → null`. When an
explicit id is provided, the service:

1. Calls `_repo.GetByIdAsync(id, ct)`.
2. Throws `InvoiceTemplateNotFoundInScopeException` if the row is
   missing OR if its scope (`OwnerType` + `BillingAccountId`) doesn't
   match the caller's scope (cross-tenant or platform/tenant mix).
3. Throws `InvoiceTemplateNotSelectableException` if the row's
   `Status != Active` (i.e. `Draft`, `Retired`, etc.).

When no explicit id is supplied, falls back to
`GetDefaultForTenantAsync` / `GetDefaultForPlatformAsync` (which
already returns only Active defaults). When that yields nothing,
returns `null` (template-less invoice — fully supported).

**`IInvoiceTemplateStampingService`** is brand new:

```csharp
public interface IInvoiceTemplateStampingService
{
    void StampInvoice(Invoice invoice, InvoiceTemplate template, DateTime nowUtc);
    bool TryEnsureStamp(Invoice invoice, InvoiceTemplate? template, DateTime nowUtc);
}
```

`StampInvoice` is unconditional (used by `CreateAsync` once selection
has succeeded). `TryEnsureStamp` is the idempotent guard used by
`IssueAsync`: it no-ops if the invoice already has
`InvoiceTemplateId.HasValue` OR if `template is null`, so re-issuing
or issuing a deliberately template-less invoice never mutates the
snapshot. Both copy ALL 13 snapshot fields verbatim (the flags too)
and set `TemplateStampedAtUtc = nowUtc`. Both are `void` / `bool` —
no I/O, pure in-memory mutation. Registered in DI as singleton.

**`InvoiceService`** changes:

- Constructor now also takes `IInvoiceTemplateSelectionService` and
  `IInvoiceTemplateStampingService`.
- `CreateAsync` gains an optional positional parameter
  `InvoiceTemplate? template = null` placed immediately before `ct`.
  When non-null, the service stamps the in-memory `Invoice` *before*
  the `_repository.AddAsync` call so the snapshot lands in the same
  insert.
- `IssueAsync` calls `_templateSelection.GetDefaultForTenantAsync`
  for invoices where `InvoiceTemplateId is null`, and if a default
  exists, calls `_repository.ApplyStampAsync` BEFORE the existing
  `UpdateStatusAsync` flip so the snapshot persists alongside the
  status change. If no default exists, the invoice is issued without
  a stamp (still valid). The ensure path goes through the repository
  (not the in-memory stamping helper) because the entity at issue
  time is loaded by the repository for a status-only update — we keep
  the write surface centralized there.

## 5. Repository Layer Changes

`IInvoiceRepository` gains a single method:

```csharp
Task<Invoice?> ApplyStampAsync(
    Guid tenantId,
    Guid invoiceId,
    InvoiceTemplate template,
    DateTime stampedAtUtc,
    CancellationToken ct = default);
```

Two implementations:

- **EF (`InvoiceRepository.cs`)** — loads the tracked `Invoice` via
  `WithTenantScope(_db.Invoices, tenantId).FirstOrDefaultAsync(...)`,
  short-circuits to `null` if missing or already stamped
  (`InvoiceTemplateId.HasValue`), copies all snapshot fields directly
  onto the tracked entity, calls `SaveChangesAsync(ct)`, and returns
  the freshly read invoice via the existing `GetByIdAsync` to ensure
  the consumer sees the updated row from the standard read path.

- **InMemory (`InMemoryInvoiceRepository.cs`)** — mirrors the same
  shape against the in-memory `_byId` dictionary, idempotency guard
  included.

Both stay tenant-scoped: a cross-tenant invoice id returns `null`
(the caller decides whether that becomes a 404). No FK back to
`InvoiceTemplate` — keeping the snapshot self-sufficient means a
later template hard-delete cannot orphan or break invoice reads.

## 6. DbContext / EF Mapping Changes

`TenantBillingDbContext.OnModelCreating` adds, inside the existing
`modelBuilder.Entity<Invoice>(e => { ... })` block:

```csharp
e.Property(x => x.InvoiceTemplateId);
e.Property(x => x.TemplateOwnerType).HasMaxLength(32);
e.Property(x => x.TemplateName).HasMaxLength(200);
e.Property(x => x.TemplateLogoUrl).HasMaxLength(2048);
e.Property(x => x.TemplateAccentColor).HasMaxLength(16);
e.Property(x => x.TemplateHeaderText);
e.Property(x => x.TemplateFooterText);
e.Property(x => x.TemplatePaymentInstructions);
e.Property(x => x.TemplateTermsText);
e.Property(x => x.TemplateMemoPlaceholder).HasMaxLength(200);
e.Property(x => x.TemplateDisplayBillingAddress).IsRequired();
e.Property(x => x.TemplateDisplayPaymentInstructions).IsRequired();
e.Property(x => x.TemplateDisplayTerms).IsRequired();
e.Property(x => x.TemplateStampedAtUtc);
e.HasIndex(x => x.InvoiceTemplateId);
```

All snapshot text fields are nullable (default EF behaviour); the
three flags are `IsRequired()` so existing rows backfill to `false`.
`MaxLength` mirrors what `InvoiceTemplate` itself uses for the same
columns. The single index on `InvoiceTemplateId` supports the
"how many invoices reference this template" admin query without
introducing a hard FK — a template may eventually be retired and
hard-deleted, and we deliberately want stamped invoices to keep
their snapshot intact in that case.

## 7. Migration Plan

Generated via `dotnet-ef`:

```
20260424211739_AddInvoiceTemplateStampToInvoices.cs
20260424211739_AddInvoiceTemplateStampToInvoices.Designer.cs
TenantBillingDbContextModelSnapshot.cs (updated)
```

The `Up` step is purely additive: 14 `AddColumn` calls + one
`CreateIndex` on `InvoiceTemplateId`. The three bool columns get
`defaultValue: false` so existing rows backfill automatically — no
data backfill script is needed. The `Down` step drops the index then
the columns. No existing column types or constraints are altered.

Runtime behaviour: the workflow uses
`UseInMemoryDatabase("tenant-billing-inmemory")` (per
`TenantBilling.Infrastructure/DependencyInjection.cs`), so the
schema is built directly from the model — the migration is purely a
forward-compatibility artifact for any future relational deployment.

## 8. DTO / Controller Surface Changes

`TenantBilling.Api/Contracts/InvoiceDtos.cs`:

- `CreateInvoiceRequest` gains a single nullable property
  `Guid? InvoiceTemplateId` (alongside the existing `DueDate?`,
  `Notes`, `Lines`, …). Existing clients that omit it keep the old
  behaviour (selection falls back to tenant default).
- `InvoiceResponse` gains a nullable nested
  `TemplateSnapshot : InvoiceTemplateSnapshotResponse` projecting
  the 13 stamped fields plus `Id` and `StampedAtUtc`. The mapping
  helper produces the nested object only when `InvoiceTemplateId`
  is set — for un-stamped invoices `templateSnapshot` is `null`.

`TenantBilling.Api/Controllers/InvoicesController.cs`:

- `Create` now resolves the template once via
  `_templateSelection.SelectForTenantInvoiceAsync(tenantId,
  request.InvoiceTemplateId, ct)`. The same `InvoiceTemplate?`
  reference is reused for BOTH the existing default-due-days
  computation AND the new `template` parameter passed to
  `_invoiceService.CreateAsync(...)`. There is exactly one
  selection per request — no duplicate DB read.
- The two new exceptions
  (`InvoiceTemplateNotFoundInScopeException`,
  `InvoiceTemplateNotSelectableException`) inherit from
  `InvoiceTemplateException : InvalidOperationException`, so the
  existing controller-level `InvalidOperationException` → 400
  ProblemDetails handler maps them automatically with their
  message. No new central `ExceptionResponseFactory` was needed
  (none exists in the project today).

## 9. Selection Algorithm

```
SelectForTenantInvoiceAsync(tenantId, explicitId?):

  if explicitId is null:
      return GetDefaultForTenantAsync(tenantId)   // existing path,
                                                  // returns Active default or null
  else:
      tpl = _repo.GetByIdAsync(explicitId)
      if tpl is null:
          throw InvoiceTemplateNotFoundInScopeException
      if tpl.OwnerType != "Tenant"
         or tpl.BillingAccountId != tenantId:
          throw InvoiceTemplateNotFoundInScopeException
      if tpl.Status != Active:
          throw InvoiceTemplateNotSelectableException
      return tpl
```

`SelectForPlatformInvoiceAsync` is symmetrical: scope check is
`OwnerType == "Platform"` (no tenant id involved); the `Active`
gate is identical. Returning `null` is a first-class outcome — it
means "create a template-less invoice", which the rest of the
pipeline handles without any special-case branching.

## 10. Stamping Algorithm

```
StampInvoice(invoice, template, nowUtc):       # used at create time
  invoice.InvoiceTemplateId        = template.Id
  invoice.TemplateOwnerType        = template.OwnerType.ToString()
  invoice.TemplateName             = template.Name
  invoice.TemplateLogoUrl          = template.LogoUrl
  invoice.TemplateAccentColor      = template.AccentColor
  invoice.TemplateHeaderText       = template.HeaderText
  invoice.TemplateFooterText       = template.FooterText
  invoice.TemplatePaymentInstructions = template.PaymentInstructions
  invoice.TemplateTermsText        = template.TermsText
  invoice.TemplateMemoPlaceholder  = template.MemoPlaceholder
  invoice.TemplateDisplayBillingAddress      = template.DisplayBillingAddress
  invoice.TemplateDisplayPaymentInstructions = template.DisplayPaymentInstructions
  invoice.TemplateDisplayTerms     = template.DisplayTerms
  invoice.TemplateStampedAtUtc     = nowUtc

TryEnsureStamp(invoice, template?, nowUtc):    # used at issue time
  if invoice.InvoiceTemplateId.HasValue: return false
  if template is null:                  return false
  StampInvoice(invoice, template, nowUtc); return true
```

Two key invariants enforced by these signatures:

1. **No live reference.** The stamping copies primitive fields only —
   `Invoice` never holds an `InvoiceTemplate` navigation. A later
   `UpdateAsync` of the template (rename, recolour, retire, even
   hard-delete) cannot mutate any historical invoice's appearance.
2. **Idempotent ensure.** `IssueAsync` calls
   `_repository.ApplyStampAsync` only when
   `InvoiceTemplateId is null`, and the repository itself
   short-circuits to `null` on already-stamped rows — a defence in
   depth in case any other code path issues a previously-stamped
   draft.

## 11. Error Handling & HTTP Mapping

| Exception                                       | HTTP | Title                          | Detail (example)                                                                                          |
|-------------------------------------------------|------|--------------------------------|-----------------------------------------------------------------------------------------------------------|
| `InvoiceTemplateNotFoundInScopeException`       | 400  | "Invoice template not found"   | "Invoice template '<id>' was not found in the calling scope (Tenant '<tenantId>')."                       |
| `InvoiceTemplateNotSelectableException`         | 400  | "Bad Request"                  | "Invoice template '<id>' has status 'Draft' and cannot be selected. Only 'Active' templates may be stamped onto a new invoice." |

Both inherit from
`InvoiceTemplateException : InvalidOperationException`, so they hit
the existing `InvalidOperationException` filter in
`InvoicesController` that already produces a 400 ProblemDetails.
Smoke-tested live: see Section 15.

400 (rather than 404) is deliberate for the scope failure — the
invoice template id is request *input*, not a path resource. A
client passing a cross-tenant id is an input error, not "the
endpoint doesn't exist". This matches how the existing template
controller already maps similar cross-scope id mismatches.

## 12. Test Plan

Domain (`tests/TenantBilling.Domain.Tests/`):

- `InvoiceTemplateStampingTests` — 6 cases covering: stamps all 14
  fields verbatim; idempotent ensure on stamped invoice; ensure
  no-ops on null template; `nowUtc` is recorded; explicit stamp
  overwrites; flag defaults are honoured.
- `InvoiceTemplateSelectionForInvoiceTests` — 7 cases:
  explicit-active happy path, explicit-draft → throws
  `NotSelectable`, explicit-retired → throws `NotSelectable`,
  explicit-cross-tenant → throws `NotFoundInScope`,
  explicit-platform-id-on-tenant-call → throws `NotFoundInScope`,
  no-explicit + no-default → null, no-explicit + active default →
  the default.

Service (`tests/TenantBilling.Tests/Domain/`):

- `InvoiceServiceStampingTests` — 6 cases: create-with-explicit-
  template stamps before persistence; create-without-template
  leaves snapshot null; issue stamps tenant default when invoice
  unstamped; issue is no-op when invoice already stamped; historical
  snapshot survives later template edit (name, logo, colour);
  no-default + no-explicit → invoice issued template-less.

API (`tests/TenantBilling.Tests/`):

- `InvoicesTemplateStampingApiTests` — 6 cases: POST with explicit
  active id returns `templateSnapshot`; POST without id falls back
  to tenant default and returns snapshot; POST without id and no
  default returns `templateSnapshot: null`; snapshot survives a
  subsequent `PUT /api/invoice-templates/tenant/{id}`; cross-tenant
  explicit id → 400 ProblemDetails; retired explicit id → 400
  ProblemDetails.

## 13. Backward Compatibility & Migration Risks

- **Existing clients.** `CreateInvoiceRequest.InvoiceTemplateId` is
  optional; omitting it preserves the prior behaviour exactly
  (selection falls back to the tenant default if any, otherwise
  returns null). Existing API tests required zero edits.
- **Existing rows.** All 14 new columns are either nullable or have
  a `false` default, so a relational backfill is a no-op. The runtime
  store is in-memory today (model is rebuilt on startup), so no live
  migration is needed.
- **Existing call sites.** The 6 InvoiceService construction sites
  in `TenantBilling.Domain.Tests` were updated in one batch to inject
  a no-op selection service plus the real stamping service; the
  `RacingInvoiceRepository` test stub got the new
  `ApplyStampAsync` shim. All 294 domain tests pass.
- **Template lifecycle interaction.** The deliberate omission of an
  FK from `Invoice → InvoiceTemplate` means a future hard-delete or
  archive of a template never breaks historical invoice reads.
- **Idempotency at issue.** The dual-guard (service-level
  `InvoiceTemplateId is null` + repository-level early-return on
  already-stamped) means re-issuing a previously issued invoice can
  never overwrite an existing snapshot.

## 14. Out-of-Scope Confirmations

The following were explicitly excluded by the block scope and
remain untouched:

- **No PDF or HTML rendering** of the snapshot. The DTO surfaces it
  as JSON only; rendering is a downstream consumer concern.
- **No email / SendGrid integration.**
- **No file upload / S3 / object storage** changes — `LogoUrl` is a
  pre-existing string column on `InvoiceTemplate` and is copied
  verbatim into the snapshot.
- **No UI / admin frontend** changes — only API + domain.
- **No auth / JWT / tenant resolution middleware** changes — the
  existing `X-Tenant-Id` header path is reused unchanged.
- **No tax / discount / currency / refund / payment-provider work.**
- **No template *write* surface changes** (create / update / retire
  / activate of `InvoiceTemplate` itself). The selection service
  layered on top is the only addition.

## 15. Verification Run (build / test / migration / smoke)

**Build:** `dotnet build TenantBilling.sln` — clean (0 warnings,
0 errors).

**Migrations:** `dotnet ef migrations add
AddInvoiceTemplateStampToInvoices` — succeeded; produces
`20260424211739_AddInvoiceTemplateStampToInvoices.{cs,Designer.cs}`
and an updated `TenantBillingDbContextModelSnapshot.cs`. The `Up`
step is purely additive (14 `AddColumn` + 1 `CreateIndex`).

**Tests:** `dotnet test`

| Project                   | Tests | Passed | Failed | Notes                       |
|---------------------------|-------|--------|--------|-----------------------------|
| `TenantBilling.Domain.Tests` | 294   | 294    | 0      | +13 new (was 281)            |
| `TenantBilling.Tests`        |  71   |  71    | 0      | +12 new (was 59)             |
| **Total**                    | **365** | **365** | **0** | All previously-green tests still green. |

**Smoke (live workflow on `:5001`):**

1. `POST /api/customers` (with `X-Tenant-Id: <tid>`) → `200`,
   returns customer.
2. `POST /api/invoice-templates/tenant` → `200`, template created in
   `Draft` status.
3. `POST /api/invoices` with that template id (still `Draft`) → `400`
   ProblemDetails:
   `"Invoice template '<id>' has status 'Draft' and cannot be
   selected. Only 'Active' templates may be stamped onto a new
   invoice."` — confirms `InvoiceTemplateNotSelectableException`
   maps to 400.
4. `POST /api/invoice-templates/tenant/{id}/activate` → `200`,
   template now `Active`.
5. `POST /api/invoices` with that template id → `200`, response
   includes `"templateSnapshot": { "id": "...", "ownerType":
   "Tenant", "name": "Brand A", "logoUrl": "https://x/y.png",
   "accentColor": "#112233", "headerText": "Hello", "footerText":
   "Bye", "paymentInstructions": "Pay", "termsText": "Net30",
   "memoPlaceholder": "Memo", "displayBillingAddress": true,
   "displayPaymentInstructions": true, "displayTerms": true,
   "stampedAtUtc": "2026-04-24T21:25:40.302..." }` — confirms the
   end-to-end stamping path.

## 16. Architect Review Outcome

Architect run with `includeGitDiff: true` and the full set of
relevant files after T10.

**Verdict: PASS** — no must-fix defects identified across the nine
review criteria (snapshot completeness, ensure-stamp idempotency,
selection chain correctness, backward compatibility, repository
semantics, DI registration, single-selection per request, EF
mapping & migration shape, strict-exclusion adherence). Security:
none observed.

Architect-confirmed properties:

- `StampInvoice` copies the full branding snapshot onto `Invoice`
  with no FK or navigation coupling; `InvoiceResponse.From` reads
  from the snapshot only, never from a live template.
- `IssueAsync` only attempts the ensure-stamp when
  `InvoiceTemplateId is null`, and the repository
  `ApplyStampAsync` no-ops on already-stamped rows in both EF and
  in-memory stores.
- The selection chain `explicit → tenant default → null` correctly
  enforces in-scope existence and Active-only gating; cross-scope
  ids surface as `NotFoundInScope`, non-Active explicit ids as
  `NotSelectable`. Both map to HTTP 400 via the existing
  `InvalidOperationException` handler.
- Backward compatibility holds: existing `CreateInvoiceRequest`
  callers continue to work; the new `template` parameter on
  `IInvoiceService.CreateAsync` is optional; the full pre-existing
  test suite (281 domain + 59 API) plus the 25 new tests all pass.
- `InvoicesController.Create` performs exactly one selection per
  request and reuses it for both due-date derivation and stamping.
- The migration is purely additive (nullable snapshot columns +
  bool defaults + non-unique index on `InvoiceTemplateId`); no FK
  is introduced; the model snapshot is in sync.
- No out-of-scope changes detected (no PDF/HTML/email/upload/UI/
  auth/tax/refund/payment-provider work touched).

**Optional polish (not blocking):** the architect noted that
passing `Guid.Empty` as an explicit template id currently flows
through the standard not-found-in-scope path. It already maps
correctly to a 400, so this is purely a consistency suggestion and
is intentionally left out of the INV-TPL-02 scope.

**Outcome:** Merge as-is for INV-TPL-02. Existing test coverage
serves as the regression guard; no additional blocking tests
required.
