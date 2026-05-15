# INV-TPL-03 — Invoice Rendering Foundation

**Service:** `services/tenant-billing-api/` (standalone .NET 8)
**Block:** INV-TPL-03
**Depends on:** INV-TPL-01 (template foundation), INV-TPL-02 (template
stamping & snapshot columns), TBS-B05 (invoice lifecycle).

---

## 1. Summary

Adds the backend rendering foundation that converts an issued or
draft invoice plus its branding snapshot into a stable, deterministic
**`InvoiceRenderDocument`** model and a server-side **HTML render**
of that document. Two new tenant-scoped read endpoints are exposed:

- `GET /api/invoices/{id}/render` → JSON document
- `GET /api/invoices/{id}/render/html` → `text/html` document

Rendering reads the branding snapshot fields stamped onto `Invoice`
by INV-TPL-02 (`TemplateName`, `TemplateLogoUrl`, `TemplateAccentColor`,
…). It never queries `InvoiceTemplate` for historical appearance —
so a later template edit, retire, or hard-delete cannot change how an
existing invoice renders.

PDF generation is deliberately **deferred** to a future block: no safe
managed-code PDF library is currently in the project, and pulling in
a headless-browser dependency would violate the strict-exclusion rules
(no external services, no large new dependencies). The HTML endpoint
gives downstream consumers (UI preview, future PDF, future email body)
a single deterministic source of truth they can convert as needed.

All user- and admin-supplied text is HTML-escaped on output. No script
tags, raw HTML, or external JavaScript is ever emitted from input.

---

## 2. Codebase Analysis

The Tenant Billing service is a standalone .NET 8 ASP.NET Core API
at `services/tenant-billing-api/` with three layers:

- **`TenantBilling.Domain`** — entities, repository interfaces, pure
  domain services (`InvoiceService`, `PaymentService`,
  `InvoiceTemplateService`, `InvoiceTemplateStampingService`).
- **`TenantBilling.Infrastructure`** — EF Core 8 (SQL Server in prod,
  InMemory in tests), repositories, `EfUnitOfWork`, the DI
  composition root in `DependencyInjection.cs`.
- **`TenantBilling.Api`** — controllers, request/response DTOs,
  tenant resolution middleware (`X-Tenant-Id` header).

Directly relevant entry points already present before this block:

- `Invoice` carries the INV-TPL-02 snapshot columns
  (`InvoiceTemplateId`, `TemplateOwnerType`, `TemplateName`,
  `TemplateLogoUrl`, `TemplateAccentColor`, `TemplateHeaderText`,
  `TemplateFooterText`, `TemplatePaymentInstructions`,
  `TemplateTermsText`, `TemplateMemoPlaceholder`,
  `TemplateDisplayBillingAddress`, `TemplateDisplayPaymentInstructions`,
  `TemplateDisplayTerms`, `TemplateStampedAtUtc`).
- `IInvoiceRepository.GetByIdForTenantAsync(tenantId, invoiceId)`
  returns the invoice with `LineItems`, `Payments`, `Refunds`
  eagerly loaded; **does not** load `Customer`.
- `ICustomerRepository.GetByIdAsync(tenantId, customerId)` returns
  the customer (including soft-deleted, which we treat as missing).
- `IPaymentService.GetInvoicePaymentSummaryAsync(...)` returns
  `(InvoiceId, InvoiceNumber, InvoiceStatus, InvoiceTotal,
  TotalPaid, BalanceDue, Currency)` — already the canonical
  paid/due number used elsewhere in the API surface.
- `InvoicesController` is wired with `IInvoiceService`,
  `IPaymentService`, `IInvoiceTemplateSelectionService`,
  `ITenantContext`. `_tenant.TenantId` is the tenant for every
  request reaching the controller.

This block adds a new sub-namespace `TenantBilling.Domain.Rendering`
and a new pair of controller endpoints that compose those existing
read paths — no entity, repository, or migration was changed.

## 3. Stories Completed

- **INV-TPL-03-S1** — Define a stable, snapshot-only render document
  model that captures everything an invoice rendering needs without
  re-reading the live template.
- **INV-TPL-03-S2** — Provide a domain service that builds the
  render document from existing tenant-scoped repository reads.
- **INV-TPL-03-S3** — Provide a pure HTML renderer that converts
  the render document into a self-contained `text/html` page,
  escaping every user/admin string and gating optional sections on
  the snapshot's display flags.
- **INV-TPL-03-S4** — Expose the render document and the HTML
  rendering through tenant-scoped read endpoints on the existing
  invoices controller.
- **INV-TPL-03-S5** — Cover the new code paths with domain and HTTP
  tests including the snapshot-survives-edit guarantee, cross-tenant
  isolation, escaping, and the missing-tenant guard.

## 4. Architecture Implemented

```
InvoicesController
  ├─ GET /api/invoices/{id}/render
  │     └─ IInvoiceRenderService.BuildRenderDocumentAsync
  └─ GET /api/invoices/{id}/render/html
        └─ IInvoiceRenderService.RenderHtmlAsync
              └─ IInvoiceHtmlRenderer.Render

IInvoiceRenderService (scoped)
  ├─ IInvoiceRepository.GetByIdForTenantAsync   (existing)
  ├─ ICustomerRepository.GetByIdAsync           (existing)
  ├─ IPaymentService.GetInvoicePaymentSummaryAsync (existing)
  ├─ TimeProvider.GetUtcNow                     (System default)
  └─ Builds InvoiceRenderDocument purely from invoice columns

IInvoiceHtmlRenderer (singleton, pure)
  └─ InvoiceRenderDocument → string (text/html)
```

Key invariants:

- **Snapshot-only**: the renderer never resolves a live
  `InvoiceTemplate`. Branding is sourced exclusively from the
  invoice's `Template*` columns.
- **Tenant-scoped reads**: both endpoints scope the lookup to
  `_tenant.TenantId`; cross-tenant lookups return null (→ 404).
- **Pure rendering**: `InvoiceHtmlRenderer` is stateless and
  registered as singleton; the service that composes data is
  scoped because it depends on scoped EF repositories.

## 5. Files Created/Changed

**Created (5):**

- `src/TenantBilling.Domain/Rendering/InvoiceRenderModels.cs` —
  `InvoiceRenderDocument`, `InvoiceRenderLine`,
  `InvoiceRenderTemplateSnapshot` records.
- `src/TenantBilling.Domain/Rendering/IInvoiceRenderService.cs` —
  contract for `BuildRenderDocumentAsync` + `RenderHtmlAsync`.
- `src/TenantBilling.Domain/Rendering/InvoiceRenderService.cs` —
  composes the render document from existing repos/services.
- `src/TenantBilling.Domain/Rendering/IInvoiceHtmlRenderer.cs` —
  contract for the pure HTML renderer.
- `src/TenantBilling.Domain/Rendering/InvoiceHtmlRenderer.cs` —
  default HTML renderer (escaped, self-contained, no JS).

**Changed (2):**

- `src/TenantBilling.Infrastructure/DependencyInjection.cs` —
  added `TimeProvider.System` (TryAdd), singleton
  `IInvoiceHtmlRenderer`, scoped `IInvoiceRenderService`.
- `src/TenantBilling.Api/Controllers/InvoicesController.cs` —
  injected `IInvoiceRenderService`, added two endpoints.

**Tests added (3):**

- `tests/TenantBilling.Tests/Domain/InvoiceRenderServiceTests.cs`
  (10 tests, real EF InMemory repos via `DomainTestHost`).
- `tests/TenantBilling.Domain.Tests/InvoiceHtmlRendererTests.cs`
  (10 tests, pure unit tests of the renderer).
- `tests/TenantBilling.Tests/InvoiceRenderApiTests.cs` (7 tests,
  HTTP surface via `WebApplicationFactory`).

## 6. Database / Migration Changes

**None.** All snapshot fields needed for rendering already exist on
`Invoice` from INV-TPL-02. No new entities, no new columns, no new
indexes, no migration to ship. The InMemory DB used in tests stays
schema-compatible.

## 7. API Endpoints Added

Both endpoints are tenant-scoped (require `X-Tenant-Id`) and live on
the existing `/api/invoices` controller.

| Method | Path                                  | Auth header   | Returns                              | 404 condition                     |
| ------ | ------------------------------------- | ------------- | ------------------------------------ | --------------------------------- |
| GET    | `/api/invoices/{id}/render`           | `X-Tenant-Id` | `200 application/json` `InvoiceRenderDocument` | invoice missing or in another tenant |
| GET    | `/api/invoices/{id}/render/html`      | `X-Tenant-Id` | `200 text/html` (full HTML page)     | same as above                     |

Status codes:

- `200 OK` — invoice exists in the calling tenant.
- `400 Bad Request` — empty/zero `id` (controller guard) or missing
  tenant header (existing tenant-resolution middleware).
- `404 Not Found` — invoice does not exist for the calling tenant
  (no existence leak across tenants).

PDF endpoint: **intentionally not exposed** — see §10.

## 8. Render Document Model

`InvoiceRenderDocument` (record) is the DTO every downstream consumer
receives. Its shape is:

```csharp
record InvoiceRenderDocument(
    Guid   InvoiceId, string InvoiceNumber, Guid TenantId,
    Guid   CustomerId, string CustomerName, string? CustomerEmail,
    DateTime IssueDate, DateTime DueDate, string Status,
    string Currency,
    decimal Subtotal, decimal TaxAmount, decimal DiscountAmount,
    decimal TotalAmount, decimal AmountPaid, decimal AmountDue,
    string? Notes,
    IReadOnlyList<InvoiceRenderLine> Lines,
    InvoiceRenderTemplateSnapshot? TemplateSnapshot,
    DateTime GeneratedAtUtc);

record InvoiceRenderLine(
    string Description, int Quantity,
    decimal UnitAmount, decimal LineTotal);

record InvoiceRenderTemplateSnapshot(
    Guid? TemplateId, string? OwnerType, string? Name,
    string? LogoUrl, string? AccentColor,
    string? HeaderText, string? FooterText,
    string? PaymentInstructions, string? TermsText,
    string? MemoPlaceholder,
    bool DisplayBillingAddress,
    bool DisplayPaymentInstructions,
    bool DisplayTerms,
    DateTime? StampedAtUtc);
```

Money fields:

- `Subtotal` / `TaxAmount` / `DiscountAmount` / `TotalAmount` come
  directly off `Invoice` (already INV-TPL-02-rounded).
- `AmountPaid` / `AmountDue` are pulled via
  `IPaymentService.GetInvoicePaymentSummaryAsync` so the rendered
  numbers stay consistent with the existing `/payment-summary`
  endpoint. If that summary returns null (defensive), we fall back
  to `(0, TotalAmount)`.

`TemplateSnapshot` is `null` when the invoice was never stamped
(no explicit template id at create AND no tenant default existed at
create-time). In that case the renderer falls back to a minimal
neutral layout (default accent, no logo, no header/footer/terms).

## 9. HTML Rendering Behavior

`InvoiceHtmlRenderer.Render(InvoiceRenderDocument)` produces a
single self-contained HTML document with the following structure:

```
<!doctype html><html lang="en">
  <head>
    <meta charset="utf-8">
    <title>Invoice {InvoiceNumber}</title>
    <style>… inline-only, accent colour interpolated …</style>
  </head>
  <body>
    <div class="invoice-header">
      [optional <img class="logo" src="{LogoUrl}">]
      <h1 class="accent">Invoice {InvoiceNumber}</h1>
      [optional <p>{HeaderText}</p>]
    </div>
    <div class="invoice-meta">
      <div>Bill to … {CustomerName} … {CustomerEmail}</div>
      <div>Issue date / Due date / Status</div>
    </div>
    <table>… one <tr> per line item, or "No line items." …</table>
    <table class="totals">
      Subtotal / Tax / Discount / Total / Paid / Amount due
    </table>
    [optional <h3>Notes</h3><p>…</p>]
    [optional <h3>Payment instructions</h3>…  if snap.DisplayPaymentInstructions]
    [optional <h3>Terms</h3>…                if snap.DisplayTerms]
    [optional <div class="footer">{FooterText}</div>]
    <div class="footer">Generated {GeneratedAtUtc:u}</div>
  </body>
</html>
```

Key rules:

- **No external CSS, no JavaScript, no iframes.** The single
  `<style>` block is hard-coded; the only network reference is an
  optional `<img src="{LogoUrl}">` which is escaped as an HTML
  attribute value but not URL-validated (the template-creation
  surface already does that vetting).
- **Display-flag gating** — `Payment instructions` and `Terms`
  sections are only emitted when both the snapshot display flag is
  true AND the corresponding text is non-blank.
- **Money formatting** — invariant culture, two decimal places,
  followed by the (escaped) ISO currency code, e.g. `100.00 USD`.
- **Date formatting** — invariant `yyyy-MM-dd`.
- **GeneratedAtUtc** — `TimeProvider.GetUtcNow()`-driven, formatted
  with the round-trippable `u` specifier in invariant culture.

## 10. PDF Rendering Decision

**PDF is deferred** for INV-TPL-03. Rationale:

1. There is no safe, license-compatible, sandboxable managed-code
   PDF renderer already in the project. Adding one would mean a
   sizeable new dependency with its own attack surface.
2. The headless-browser route (Puppeteer/Playwright) would require
   pulling Chromium into the runtime image and would violate the
   "no large new dependencies / no external services" exclusion.
3. PDF generation is not required by any current consumer (no
   email/upload/download surface is part of this block).

Mitigation: the JSON render document and the HTML rendering give a
deterministic, self-contained source of truth. Any future block can
add `RenderPdfAsync` to `IInvoiceRenderService` and wire whichever
PDF strategy the team picks (managed library, headless browser,
external service) without changing the snapshot model or the HTML
output.

## 11. Template Snapshot Rendering Behavior

The snapshot block in the document is sourced 1-for-1 from the
`Invoice.Template*` columns:

| Document field                              | Invoice column                          |
| ------------------------------------------- | --------------------------------------- |
| `TemplateId`                                | `InvoiceTemplateId`                     |
| `OwnerType`                                 | `TemplateOwnerType`                     |
| `Name`                                      | `TemplateName`                          |
| `LogoUrl`                                   | `TemplateLogoUrl`                       |
| `AccentColor`                               | `TemplateAccentColor`                   |
| `HeaderText` / `FooterText`                 | `TemplateHeaderText` / `Footer…`        |
| `PaymentInstructions` / `TermsText`         | `Template…`                             |
| `MemoPlaceholder`                           | `TemplateMemoPlaceholder`               |
| `DisplayBillingAddress` / `DisplayPaymentInstructions` / `DisplayTerms` | `Template…` |
| `StampedAtUtc`                              | `TemplateStampedAtUtc`                  |

The renderer **never** queries the live `InvoiceTemplate` row —
verified by a domain test that:

1. Stamps an invoice with `Name="Original"`, `AccentColor="#10B981"`.
2. Updates the live template to `Name="Renamed"`,
   `AccentColor="#FF0000"`.
3. Re-renders the invoice and asserts the document still reports
   `Original` / `#10B981`.

Unstamped invoices (`InvoiceTemplateId == null`) yield
`TemplateSnapshot == null` and the HTML falls back to the neutral
default accent + no header/footer/terms blocks.

## 12. Security / Escaping Behavior

Every user/admin-supplied string in the HTML output is run through
`System.Net.WebUtility.HtmlEncode` before being written:

- `CustomerName`, `CustomerEmail`, `Notes`, `Status`, `Currency`,
  `InvoiceNumber`.
- Each `InvoiceRenderLine.Description`.
- Each snapshot text field (`Name`, `LogoUrl`, `HeaderText`,
  `FooterText`, `PaymentInstructions`, `TermsText`,
  `MemoPlaceholder`).
- `AccentColor` — additionally protected as a CSS-context value:
  the colour is encoded the same way as HTML text before being
  interpolated into the inline `<style>` block, so a payload of
  `red;}</style><script>alert(1)</script><style>` becomes invalid
  CSS rather than a `</style>` break-out.

Negative-test coverage in `InvoiceHtmlRendererTests`:

- A `<script>alert('xss')</script>` payload supplied as customer
  name, line description, notes, or any snapshot text field appears
  in the output as `&lt;script&gt;…` and **never** as a literal
  `<script>` opening tag (`Assert.DoesNotContain("<script", html)`).
- The accent-colour CSS break-out attempt does not produce
  `</style><script>` in the output.

The renderer never:

- Loads remote scripts or stylesheets.
- Accepts pre-formatted HTML from input — every text field is
  treated as plain text and HTML-encoded.
- Emits inline event handlers (`onerror=`, `onload=`, …).

## 13. Tests Added

Three new test files; **29 new tests** in total, all green.

**`tests/TenantBilling.Domain.Tests/InvoiceHtmlRendererTests.cs`
(10 tests)** — pure-function tests:

- Renders core invoice fields (number, customer, dates, lines,
  totals).
- Null snapshot → default accent and no logo / header / payment
  instructions / terms blocks.
- With a full snapshot → emits logo, accent, header, footer,
  payment instructions, terms.
- Display flags `false` → omits payment instructions / terms even
  when the text is set.
- `<script>` in user text → entity-encoded, no executable tag.
- `<script>` in snapshot text → entity-encoded.
- Accent CSS break-out attempt → `</style><script>` not emitted
  unencoded.
- Empty lines → "No line items." placeholder row.
- `null` document → `ArgumentNullException`.
- Maximally hostile snapshot+invoice → no `<script` substring
  anywhere in the output.

**`tests/TenantBilling.Tests/Domain/InvoiceRenderServiceTests.cs`
(10 tests)** — exercises the render service against the real
`DomainTestHost` (EF InMemory + real repositories + real
`PaymentService`):

- Unstamped invoice → null snapshot, populated core/lines/customer.
- Stamped invoice → snapshot mirrors the stamped columns 1-for-1.
- Snapshot-wins-after-edit: live template mutated post-stamp,
  re-rendered document keeps the original snapshot values.
- Cross-tenant id → returns `null`.
- Missing invoice id → returns `null`.
- Empty `tenantId` / `invoiceId` → `ArgumentException`.
- Payments applied → `AmountPaid` / `AmountDue` reflect the
  payment summary.
- `RenderHtml` happy path → `<!doctype html>` + invoice number.
- `RenderHtml` missing invoice → returns `null`.

**`tests/TenantBilling.Tests/InvoiceRenderApiTests.cs` (9 tests)**
— HTTP surface:

- `GET /render` → `200 application/json`, body matches expected
  document.
- `GET /render/html` → `200 text/html`, body starts with
  `<!doctype html>`, contains invoice number, customer, line
  description.
- `GET /render` missing invoice → `404`.
- `GET /render/html` missing invoice → `404`.
- `GET /render` cross-tenant → `404` (no existence leak).
- `GET /render` no tenant header → `400`.
- `GET /render/html` no tenant header → `400`.
- `GET /render` with `Guid.Empty` id → `400` (controller guard).
- `GET /render/html` with `Guid.Empty` id → `400` (controller guard).

## 14. Validation Results

**Build (full solution, `dotnet build TenantBilling.sln`):**

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**Unit/integration tests (relevant runs, all green):**

| Filter                                                               | Pass | Fail |
| -------------------------------------------------------------------- | ---: | ---: |
| `TenantBilling.Domain.Tests` (full project)                          | 304  | 0    |
| `TenantBilling.Tests`, `~InvoiceRenderApiTests`                      |   9  | 0    |
| `TenantBilling.Tests`, `~InvoiceTemplates|~InvoiceCreation|~InvoicesMarkOverdue` |  28  | 0 |
| `TenantBilling.Tests`, `~Domain.InvoiceRender|~Domain.InvoiceServiceStamping|~Domain.PaymentService|~Domain.InvoiceService` | 35 | 0 |
| `TenantBilling.Tests`, `~InvoicesTemplateStampingApiTests` (regression) | 6 | 0 |

All 29 new tests pass; pre-existing tests in the touched
projects remain green. No tests were modified.

**Workflow restart:** `Tenant Billing API` workflow restarted
cleanly on `:5001`.

**Smoke (live API):**

- `POST /api/customers` → 200, customer id captured.
- `POST /api/invoices` (no template) → 201, invoice
  `INV-2026-000001` created with `templateSnapshot: null`.
- `GET /api/invoices/{id}/render` →
  `200 application/json`,
  `{ invoiceNumber: "INV-2026-000001", customerName: "Acme Co",
     totalAmount: 100, amountDue: 100, lines: 1,
     templateSnapshot: null }`.
- `GET /api/invoices/{id}/render/html` →
  `200 text/html`, body begins
  `<!doctype html><html lang="en"><head><meta charset="utf-8">
   <title>Invoice INV-2026-000001</title>…`.
- `GET /api/invoices/00000000-0000-0000-0000-000000000999/render`
  → `404`.
- `GET /api/invoices/{id}/render` without `X-Tenant-Id` → `400`.

## 15. Known Gaps / Deferred Items

- **PDF rendering** — deferred (see §10). Future block can add
  `RenderPdfAsync` without changing the document model.
- **Customer billing address rendering** — the snapshot's
  `DisplayBillingAddress` flag is captured and surfaced in the
  document, but the current HTML layout does not yet render the
  customer's billing address. The address is on `Customer` but the
  render service intentionally only exposes name/email today; a
  follow-up can extend `InvoiceRenderDocument` with structured
  address fields and the renderer can gate them on the flag. This
  is documented for the next block rather than implemented here to
  keep the diff focused on the scoped story set.
- **Caching** — neither endpoint sets `Cache-Control`. Rendering is
  cheap (single in-memory string assembly over already-loaded data);
  if a future UI needs aggressive caching, an `ETag` derived from
  `Invoice.UpdatedAt` + `TemplateStampedAtUtc` would be the natural
  next step.
- **Localization** — labels in the HTML (`Bill to`, `Issue date`,
  `Subtotal`, …) are currently English-only. No i18n surface
  existed for the API to inherit from; adding one is out of scope.
- **Memo placeholder** — captured in the snapshot but not rendered
  visibly today (it's intended as a *placeholder string for the
  edit UI*, not a body field). Documented here so it isn't
  perceived as a bug.

## 16. Confirmation of Strict Exclusions

This block introduces **no** functionality from any of the
exclusions:

- ❌ No email / SendGrid / SMTP integration.
- ❌ No UI / SPA / preview surface (HTML response is server-only).
- ❌ No upload / S3 / object-storage integration.
- ❌ No auth / JWT / sign-in surface (relies entirely on the
  pre-existing `X-Tenant-Id` middleware).
- ❌ No payment-provider / webhook / Stripe / PayPal integration.
- ❌ No tax-engine integration.
- ❌ No refund-edit workflow.
- ❌ No PDF library / headless browser dependency added.
- ❌ No new database table, column, index, or migration.
- ❌ No mutation endpoints; both endpoints are pure GETs.

## 17. Recommended Next Block

**INV-TPL-04 — Invoice Customer & Address Rendering Enrichment**

Most-natural follow-up because it (a) closes the
`DisplayBillingAddress`-flag gap noted in §15 and (b) keeps the
work inside the same render-only surface:

1. Extend `InvoiceRenderDocument` with a structured
   `CustomerAddress` block (street, city, region, postal code,
   country) sourced from `Customer.BillingAddress` (and any future
   structured fields).
2. Render the address in the HTML "Bill to" block when
   `TemplateSnapshot.DisplayBillingAddress` is true.
3. Introduce snapshot fields for the *issuer's* legal entity
   (company name, address, VAT id) so an invoice can render its
   "From:" block from the snapshot — completing the snapshot-only
   contract for everything visible on a printed invoice.

Subsequent blocks can then layer:

- **INV-TPL-05** — PDF rendering (separate decision on managed
  library vs. headless browser; the model and HTML stay unchanged).
- **INV-TPL-06** — Tenant-side render preview UI in
  `artifacts/commerce-admin` consuming the existing JSON endpoint.
