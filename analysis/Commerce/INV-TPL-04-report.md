# INV-TPL-04 — Address & Issuer Enrichment Report

> Service: `services/tenant-billing-api/` (.NET 8, Pomelo MySQL)
> Builds on: INV-TPL-01 (template foundation), INV-TPL-02 (template
> stamping), INV-TPL-03 (rendering foundation).

## 1. Summary

INV-TPL-04 closes the rendering data model by adding the two parties
that every invoice needs but that earlier blocks omitted: the
**customer billing address** ("Bill to") and the **issuer / seller
identity** ("From"). Both are exposed through the existing
`/api/invoices/{id}/render` (JSON) and `/api/invoices/{id}/render/html`
endpoints. Issuer fields are snapshotted onto the invoice at create
time and at issue time exactly the way template branding was in
INV-TPL-02 — so a later edit, retire, or hard-delete of the source
template never alters how a historical invoice renders. No live
`InvoiceTemplate` lookup is performed during rendering.

## 2. Codebase Analysis

The block builds on three pieces already in the service:

- **`Customer`** held only a free-text `BillingAddress` column, which
  printed correctly on the legacy renderer but could not be shaped
  into a real postal block. It has been augmented with six structured
  nullable columns. The free-text column is preserved untouched so
  no historical rows lose data.
- **`InvoiceTemplate` / `InvoiceTemplateService` / `InvoiceTemplateValidation`**
  already had the branding block (logo, accent, header/footer,
  payment instructions, terms) plus three display flags. We layered
  a 12-field issuer block onto the same shape: positional
  `NewInvoiceTemplate` / `InvoiceTemplateUpdate` records, normalize-on-write
  in `InvoiceTemplateService`, and dedicated `InvoiceTemplateValidation`
  helpers for the two non-trivial fields (email shape, absolute
  http(s) website).
- **`InvoiceTemplateStampingService` / `Invoice`** already snapshotted
  template-branding columns onto the invoice at create + issue time,
  with an idempotency guard keyed on `InvoiceTemplateId`. We added the
  same 12 issuer columns plus `IssuerStampedAtUtc` to `Invoice` and
  extended `StampInvoice` so the issuer block always travels with the
  branding block; the existing `EnsureStampedInvoice` guard is the
  only entry point we needed to touch.
- **Render pipeline (`InvoiceRenderService` + `InvoiceHtmlRenderer`)**
  already produced a `InvoiceRenderDocument` that survived template
  deletion, by reading invoice snapshot columns rather than the live
  template row. The new `CustomerAddress` and `Issuer` blocks slot
  into the same record and the same renderer.

## 3. Stories Completed

- **S01:** Add structured customer billing address columns (Line1/2,
  City, State/Region, Postal, Country) on `Customer` while keeping
  legacy `BillingAddress` text intact for backwards compatibility.
- **S02:** Add 12 issuer / seller identity columns on `InvoiceTemplate`
  with normalization, length caps, email shape validation, and
  absolute http(s) URL validation for the website.
- **S03:** Snapshot all 12 issuer columns + a dedicated
  `IssuerStampedAtUtc` timestamp onto `Invoice` at create + issue time.
- **S04:** Surface the new data in `InvoiceRenderDocument` as two
  optional sub-records (`CustomerAddress`, `Issuer`).
- **S05:** Render a "From" block in the HTML output (gated on issuer
  presence) and append structured address lines to the existing "Bill
  to" block (gated on `TemplateSnapshot.DisplayBillingAddress`).
- **S06:** Generate EF migration `InvoiceIssuerAddressEnrichment`
  (Pomelo MySQL, utf8mb4) covering all schema changes.
- **S07:** Cover stamping, render service, HTML renderer, validation,
  and the two API endpoints with new tests.

## 4. Architecture Implemented

```
                ┌─────────────────────┐
                │  InvoiceTemplate    │  (live, mutable)
                │  + 12 issuer cols   │
                └──────────┬──────────┘
                           │ stamp at create+issue
                           ▼
┌──────────────┐    ┌─────────────────────┐
│   Customer   │    │      Invoice        │
│  + 6 addr    │    │  + 12 issuer cols   │
│  cols        │    │  + IssuerStampedAt  │
└──────┬───────┘    └──────────┬──────────┘
       │                        │
       └─────── render ─────────┘
                  │
                  ▼
        InvoiceRenderService
   (snapshot-only — never reads
    live InvoiceTemplate row)
                  │
                  ▼
         InvoiceRenderDocument
       { ..., CustomerAddress?, Issuer? }
                  │
        ┌─────────┴─────────┐
        ▼                   ▼
   /render JSON      InvoiceHtmlRenderer
                     (.parties → From + Bill-To)
```

The snapshot rule is the architectural keystone: the renderer
**never touches the `invoice_templates` table**. Every issuer field
on the rendered document comes from an `invoices.*` column populated
at stamp time. This is what guarantees that retiring or editing a
template does not retro-rewrite history.

## 5. Files Created / Changed

```
 22 files changed, 1657 insertions(+), 18 deletions(-)
```

**Domain layer**

- `src/TenantBilling.Domain/Entities/Customer.cs` (+24)
- `src/TenantBilling.Domain/Entities/Invoice.cs` (+28)
- `src/TenantBilling.Domain/Entities/InvoiceTemplate.cs` (+23)
- `src/TenantBilling.Domain/Services/ICustomerService.cs` (+32)
- `src/TenantBilling.Domain/Services/CustomerService.cs` (+46)
- `src/TenantBilling.Domain/Services/IInvoiceTemplateService.cs` (+34)
- `src/TenantBilling.Domain/Services/InvoiceTemplateService.cs` (+67)
- `src/TenantBilling.Domain/Services/InvoiceTemplateValidation.cs` (+73)
- `src/TenantBilling.Domain/Services/IInvoiceTemplateStampingService.cs` (+38)
- `src/TenantBilling.Domain/Rendering/InvoiceRenderModels.cs` (+51)
- `src/TenantBilling.Domain/Rendering/InvoiceRenderService.cs` (+103)
- `src/TenantBilling.Domain/Rendering/InvoiceHtmlRenderer.cs` (+130)

**API layer**

- `src/TenantBilling.Api/Contracts/CustomerDtos.cs` (+50)
- `src/TenantBilling.Api/Contracts/InvoiceTemplateDtos.cs` (+104)
- `src/TenantBilling.Api/Controllers/CustomersController.cs` (+2)

**Infrastructure / persistence**

- `src/TenantBilling.Infrastructure/Data/TenantBillingDbContext.cs` (+48)
- `src/TenantBilling.Infrastructure/Data/Migrations/20260424222018_InvoiceIssuerAddressEnrichment.cs` (new)
- `src/TenantBilling.Infrastructure/Data/Migrations/20260424222018_InvoiceIssuerAddressEnrichment.Designer.cs` (new)
- `src/TenantBilling.Infrastructure/Data/Migrations/TenantBillingDbContextModelSnapshot.cs` (+123)

**Tests**

- `tests/TenantBilling.Domain.Tests/InvoiceHtmlRendererTests.cs` (+170)
- `tests/TenantBilling.Domain.Tests/InvoiceTemplateValidationTests.cs` (+97)
- `tests/TenantBilling.Tests/Domain/InvoiceRenderServiceTests.cs` (+193)
- `tests/TenantBilling.Tests/Domain/InvoiceServiceStampingTests.cs` (+126)
- `tests/TenantBilling.Tests/InvoiceRenderApiTests.cs` (+113)

**Report**

- `analysis/INV-TPL-04-report.md` (this file)

## 6. Database / Migration Changes

Migration: `20260424222018_InvoiceIssuerAddressEnrichment`
(Pomelo MySQL provider, utf8mb4 charset on every text column).

**`customers` (6 new columns)**

| Column | Type | Notes |
|---|---|---|
| `BillingAddressLine1` | `varchar(250)` NULL | utf8mb4 |
| `BillingAddressLine2` | `varchar(250)` NULL | utf8mb4 |
| `BillingCity` | `varchar(100)` NULL | utf8mb4 |
| `BillingStateRegion` | `varchar(100)` NULL | utf8mb4 |
| `BillingPostalCode` | `varchar(100)` NULL | utf8mb4 |
| `BillingCountry` | `varchar(100)` NULL | utf8mb4 |

The legacy `BillingAddress varchar(1000)` column is **untouched** —
existing rows render through the renderer's legacy fallback path
(see Section 8) until a tenant adopts the structured fields.

**`invoice_templates` (12 new columns)**

| Column | Type |
|---|---|
| `IssuerDisplayName` | `varchar(200)` NULL |
| `IssuerLegalName` | `varchar(250)` NULL |
| `IssuerAddressLine1` | `varchar(250)` NULL |
| `IssuerAddressLine2` | `varchar(250)` NULL |
| `IssuerCity` | `varchar(100)` NULL |
| `IssuerStateRegion` | `varchar(100)` NULL |
| `IssuerPostalCode` | `varchar(100)` NULL |
| `IssuerCountry` | `varchar(100)` NULL |
| `IssuerEmail` | `varchar(320)` NULL |
| `IssuerPhone` | `varchar(50)` NULL |
| `IssuerTaxId` | `varchar(100)` NULL |
| `IssuerWebsite` | `varchar(500)` NULL |

**`invoices` (13 new columns — same 12 + timestamp)**

Identical 12-column block (matching template column types) plus
`IssuerStampedAtUtc datetime(6) NULL`. Together with the existing
`InvoiceTemplateId` / `TemplateStampedAtUtc` columns, this preserves
"who issued this, at what moment in time, with what address."

`Down()` drops every added column on all three tables in reverse
order. No data migration is performed — new columns are nullable, so
existing rows survive both directions of the migration without
write-amplification or backfill.

## 7. API Changes

No new endpoints. Existing endpoints accept and emit additional
fields:

**`POST /api/customers` and `PUT /api/customers/{id}`**

Now accept six optional flat properties on the request body:
`billingAddressLine1`, `billingAddressLine2`, `billingCity`,
`billingStateRegion`, `billingPostalCode`, `billingCountry`. The
legacy `billingAddress` text field is still accepted and persisted
to the same column as before.

`CustomerResponse` returns the same six new properties.

**`POST /api/invoice-templates/tenant`, `POST /api/invoice-templates/platform`,
and the matching `PUT` endpoints**

Now accept twelve optional issuer properties (`issuerDisplayName`,
`issuerLegalName`, `issuerAddressLine1`, `issuerAddressLine2`,
`issuerCity`, `issuerStateRegion`, `issuerPostalCode`,
`issuerCountry`, `issuerEmail`, `issuerPhone`, `issuerTaxId`,
`issuerWebsite`). `InvoiceTemplateResponse` returns all twelve.

**`GET /api/invoices/{id}/render` (JSON)**

Response document now contains two optional blocks:

```json
{
  "customerAddress": {
    "line1": "100 Main St", "line2": "Suite 4",
    "city": "Springfield", "stateRegion": "IL",
    "postalCode": "62704", "country": "USA"
  },
  "issuer": {
    "displayName": "Brand A Display",
    "legalName": "Brand A, Inc.",
    "addressLine1": "100 Market St", "addressLine2": "Suite 200",
    "city": "San Francisco", "stateRegion": "CA",
    "postalCode": "94105", "country": "USA",
    "email": "ar@brand.test", "phone": "+1-415-555-0100",
    "taxId": "EIN-12-3456789", "website": "https://brand.test",
    "stampedAtUtc": "2026-04-24T22:30:12.69Z"
  }
}
```

Either block is `null` when the underlying data is absent.

**`GET /api/invoices/{id}/render/html`**

The `.parties` flex container now contains a `From` column (when
`Issuer != null`) on the left of the existing `Bill to` column.
Bill-To gains structured address lines (gated on
`TemplateSnapshot.DisplayBillingAddress`).

## 8. Customer Address Behavior

The render service constructs `CustomerAddress` from
`InvoiceRenderService.BuildCustomerAddressBlock(Customer)`:

1. If **any** of the six structured columns are non-null, build a
   block that mirrors the columns 1:1 (nulls preserved).
2. Otherwise, if the legacy `BillingAddress` text is non-null, pack
   the entire string into `Line1` and leave the other five fields
   null. The renderer prints it as a single address line under the
   customer name.
3. Otherwise, return `null` (no `CustomerAddress` block on the
   document).

This guarantees:
- New customers using the structured form get a properly-shaped
  multi-line block in the rendered HTML.
- Legacy customers with only free-text addresses keep their existing
  rendering — no migration needed.
- Customers with no address at all neither break the renderer nor
  emit empty markup.

The HTML "Bill to" column itself is **always present** (it carries
the customer's display name and email even when no address is set).
Address lines inside the column are gated on whether the invoice's
`TemplateSnapshot.DisplayBillingAddress` flag is `true`. When no
template snapshot is stamped (e.g. an invoice created without a
template), the renderer defaults to showing the address — matching
the historical INV-TPL-03 behaviour.

## 9. Issuer Snapshot Behavior

`InvoiceTemplateStampingService.StampInvoice` always copies all 12
issuer columns from template to invoice and writes `IssuerStampedAtUtc`,
even when every template field is null. This is intentional:

- "Stamped at T with no issuer info" is semantically distinct from
  "never stamped" — the renderer can decide independently whether to
  emit a From block based on whether any text column is non-null.
- The single `IssuerStampedAtUtc` makes audit queries trivial:
  `WHERE IssuerStampedAtUtc IS NOT NULL` returns every invoice that
  has been through the stamping pipeline since this block shipped.

`EnsureStampedInvoice` is unchanged — it already gates idempotency on
`invoice.InvoiceTemplateId.HasValue`, so an invoice that was stamped
under INV-TPL-02 (when issuer columns did not exist yet) will not be
retro-stamped when this block ships. Such invoices simply render
without a From block, which is the correct behaviour: we do not
fabricate issuer info we never had.

`InvoiceRenderService` produces an `InvoiceRenderIssuer` record by
reading the 12 invoice columns directly. It returns `null` when
every text column is null — which is the same condition the renderer
uses to gate the From block. There is **no fallback to the live
template**: a template can be edited, retired, or hard-deleted with
no effect on a stamped invoice's rendered From block.

## 10. HTML Rendering Behavior

The "From" block, when emitted, prints the snapshot in this order:

```
From
  <DisplayName>
  <LegalName>
  <AddressLine1>
  <AddressLine2>
  <City>, <StateRegion> <PostalCode>     ← single line, smart-joined
  <Country>
  <Email>
  <Phone>
  <a href="<Website>" rel="noopener noreferrer"><Website></a>
  Tax ID: <TaxId>
```

Every field is omitted independently when null/empty. The
city/state/postal joiner uses `string.Join(", ", parts)` after
filtering nulls so absent middle fields don't produce stray commas.

Every text value is `WebUtility.HtmlEncode`-escaped. The website is
escaped both as the `href` attribute value (URL-encoded by
`HtmlEncode`'s attribute-context behaviour) and as the visible link
text. `rel="noopener noreferrer"` is hard-coded — the renderer never
trusts an issuer-provided URL to be safe.

The "Bill to" block always emits the customer's display name and
email. Structured address lines (or the legacy single-line fallback)
are emitted only when `snapshot.DisplayBillingAddress == true`, or
when no snapshot exists at all (legacy invoices). When the snapshot
exists and the flag is `false`, no address lines render — matching
INV-TPL-03's contract.

## 11. Validation Rules Implemented

`InvoiceTemplateValidation` exposes:

| Field | Rule |
|---|---|
| `IssuerDisplayName` | trim; max 200 chars (`NormalizeOptionalText`) |
| `IssuerLegalName` | trim; max 250 chars |
| `IssuerAddressLine1` / `Line2` | trim; max 250 chars each |
| `IssuerCity` | trim; max 100 chars |
| `IssuerStateRegion` | trim; max 100 chars |
| `IssuerPostalCode` | trim; max 100 chars |
| `IssuerCountry` | trim; max 100 chars |
| `IssuerEmail` | trim; **lowercase**; max 320; matches the same `EmailShape` regex `CustomerService` uses (single `@`, non-empty local + domain, at least one dot in the domain) |
| `IssuerPhone` | trim; max 50 chars |
| `IssuerTaxId` | trim; max 100 chars |
| `IssuerWebsite` | trim; max 500 chars; **must** match `^https?://[^\s]+$`. Relative paths, `ftp://`, `javascript:`, and bare hostnames are rejected — the From block is public-facing surface and a relative URL would break HTML email and downloaded artefacts |

Every rule applies symmetrically on `Create` and `Update`. Update
treats `null` as "no change" (typed-record convention used elsewhere
in the service); to clear a field the API consumer sends an empty
string, which the normalizer collapses back to `null`.

## 12. Tests Added

| Suite | New tests | Coverage |
|---|---|---|
| `InvoiceTemplateValidationTests` | 7 | Email trim+lowercase, blank → null, bad shape (4 patterns), over-long; website accepts http(s) absolute, blank → null, rejects relative / ftp / javascript / bare host, over-long; max-length-constants safety net for the 11 length-validated fields |
| `InvoiceServiceStampingTests` | 3 | Issuer snapshot fully populated; null issuer template still stamps timestamp + nulls; snapshot survives template issuer edit |
| `InvoiceRenderServiceTests` | 6 | Customer structured address populates block; legacy text falls back to `Line1`; no address ⇒ no block; issuer snapshot populates block; null issuer ⇒ no block; issuer snapshot survives template edit |
| `InvoiceHtmlRendererTests` | 5 | From block emitted when issuer present (display, legal, addr lines, city/state/postal joiner, country, email, phone, tax id, website link with `rel="noopener noreferrer"`); From omitted when null; Bill-To address present when no snapshot; Bill-To address present when snapshot enables flag; Bill-To address omitted when snapshot disables flag; HTML-encoding of every issuer field neutralizes a `<script>` payload |
| `InvoiceRenderApiTests` | 2 | `/render` JSON body contains both blocks; `/render/html` body contains both blocks (`From`, `Bill to`, address lines, safe website link) |

**Total new tests: 23.**

## 13. Validation Results

```
$ dotnet build TenantBilling.sln
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/TenantBilling.Domain.Tests
Passed!  Failed: 0, Passed: 328, Skipped: 0, Total: 328

$ dotnet test tests/TenantBilling.Tests
Passed!  Failed: 0, Passed: 100, Skipped: 0, Total: 100
```

Total: **428 tests passing**, 0 failures, 0 warnings.

**Workflow restart and end-to-end smoke** (`Tenant Billing API`,
:5001):

1. `POST /api/invoice-templates/tenant` with all 12 issuer fields →
   `201` with the issuer block in the response, email lowercased to
   `ar@brand.test`.
2. `POST /api/customers` with structured address → `201` with the
   structured address echoed back.
3. `POST /api/invoices` referencing the customer → invoice created
   and stamped with the issuer block.
4. `GET /api/invoices/{id}/render` → JSON contains a populated
   `customerAddress` block (six fields) and a populated `issuer`
   block (12 fields + `stampedAtUtc`).
5. `GET /api/invoices/{id}/render/html` → HTML contains
   `>From<`, `Brand A Display`, `100 Market St`,
   `San Francisco, CA 94105`, `<a href="https://brand.test"
   rel="noopener noreferrer">`, plus `>Bill to<`, `100 Main St`,
   `Springfield, IL 62704`.

## 14. Known Gaps / Deferred Items

- No UI surface — neither the platform Control Center nor the tenant
  portal renders the new fields yet. The next UI block can pick this
  up directly from the existing JSON contract.
- No PDF rendering — out of scope, deferred to the dedicated PDF
  block.
- No email-friendly variant of the From block — the same explicit
  exclusion holds.
- `IssuerWebsite` validation is a syntactic check only (regex match).
  We do not perform DNS resolution, HEAD requests, or scheme
  enforcement beyond `http(s)`. That is the right tradeoff for an
  inline write path; a background reachability checker can be a
  later concern.
- Customer billing address has no per-country format checks (postal
  code shape, state/region whitelist). The renderer is tolerant of
  any combination, so adding country-specific rules would be a pure
  validation extension.

## 15. Confirmation of Strict Exclusions

NOT implemented in this block (per spec):
- PDF generation
- Email sending / SendGrid
- Tenant portal UI
- Control center UI
- File upload / S3
- Authentication / authorization / JWT
- Payment provider changes
- Tax engine
- Refunds / credits
- Invoice editing workflow
- LegalSynq integration

Verified by `git diff --stat` — the diff touches only
`services/tenant-billing-api/src/TenantBilling.{Domain,Api,Infrastructure}/`
plus the matching test projects and this report.

## 16. Recommended Next Block

**INV-TPL-05 — PDF Rendering** is the natural follow-on. The render
document already has every field a PDF needs; an `InvoicePdfRenderer`
would consume the same `InvoiceRenderDocument` contract and emit
either via `QuestPDF` or by snapshotting the existing HTML through a
headless renderer. The snapshot-only contract guarantees PDFs match
HTML byte-for-byte regardless of when they're regenerated.

Other adjacent candidates:
- **INV-TPL-06 — Tenant portal template editor UI** (consumes the
  existing template DTOs).
- **INV-TPL-07 — Invoice email delivery** (consumes the rendered
  HTML and a tenant-configured SMTP/SendGrid destination).
