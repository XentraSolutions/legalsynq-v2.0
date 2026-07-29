# SynqLien Selling Backend API Rebuild Plan

## Goal

Replace the current selling API contract with a clean, Figma-aligned, backend-only selling module for SynqLien.

The new contract should be lien-first, not portfolio-first. The user journey shown in Figma is:

1. Manage portfolio dashboard.
2. Add lien, single or bulk.
3. View lien detail.
4. Prepare lien for sale.
5. Confirm sell.
6. Notify or expose the offer to the funding company.
7. Track buyer response and final sale state.

## Scope

In scope:

- Backend API contract.
- Liens service application/domain/infrastructure changes.
- Database model and migrations.
- Authentication, authorization, tenant isolation, idempotency, and audit planning.
- Integration test plan.
- Deprecation plan for existing selling endpoints.

Out of scope:

- Frontend implementation.
- Figma UI implementation.
- Gateway route changes unless the existing gateway does not already forward Liens API traffic.
- A new microservice. This should stay inside the existing Liens service boundary.

## Existing Backend Context

Current useful backend areas:

- `apps/services/liens/Liens.Api/Endpoints/SellingEndpoints.cs`
- `apps/services/liens/Liens.Api/Endpoints/LienEndpoints.cs`
- `apps/services/liens/Liens.Api/Endpoints/ContactEndpoints.cs`
- `apps/services/liens/Liens.Api/Endpoints/BatchUploadEndpoints.cs`
- `apps/services/liens/Liens.Application/DTOs`
- `apps/services/liens/Liens.Application/Services`
- `apps/services/liens/Liens.Domain/Entities`
- `apps/services/liens/Liens.Infrastructure`
- `apps/services/liens/Liens.Api.Tests/Tests/SellingPortfolioEndpointTests.cs`

Important planning decision:

- "Scrap existing" means replace the public selling API contract.
- It should not mean deleting stable lien, case, contact, document, lookup, or batch infrastructure before the replacement is implemented and tested.
- The existing `portfolios` selling API can be deprecated after the new lien-first API is adopted.

## Planner Findings

These decisions are now locked for backend implementation:

- Buyer access must support both authenticated buyer APIs and public token APIs.
- Figma-specific selling workflow fields must be added as columns on `Lien` in `liens_Liens`, not as a separate selling metadata table.
- `POST /api/liens/selling/liens/{lienId}/confirm-sale` moves `PreparedForSale` to `SubmittedForSale`; it must not mark the lien as `Sold`.
- `Sold` happens only after buyer acceptance or bill-of-sale completion.
- New endpoints must reuse the existing selling route authorization pattern: authenticated user, `LiensPermissions.ProductCode` (`SYNQ_LIENS`) product access, sell mode, and selling permissions.

## Figma-Derived Backend Requirements

Portfolio dashboard needs:

- Summary cards: total portfolio value, total pending, total internal, total sold.
- Tab filters: pending, internal, sold.
- Search, filters, sorting, and pagination.
- Table rows with lien ID, funding company, initial service date, billing amount, ask amount, highest bid, status, and row actions.

Add lien needs:

- Single-lien wizard.
- Bulk upload flow.
- Lien information step.
- Funding company and case information step.
- Medical code and marketplace pricing step.
- Document upload step.
- Save each wizard step while the lien remains in its selected intake state (`Pending` or `Internal`).

Lien detail needs:

- Lien information.
- Funding company and case information.
- Medical pricing.
- Documents.
- Sale readiness.
- Activity timeline.
- Buyer/offers state.

Sell lien needs:

- Prepare sale package.
- Select funding company or buyer contact.
- Confirm sale submission.
- Send buyer email or generate buyer portal access.
- Track submitted, withdrawn, sold, declined, or offer states.

Contacts need:

- Contact list/search/export.
- Add/edit/deactivate contact.
- Contact detail aggregate.
- Reassign linked cases or liens where applicable.

## Architecture Recommendation

Create a new backend module inside the Liens service:

- API: `SellingV2Endpoints` or replacement `SellingEndpoints` after migration.
- Application service: `ISellingLienService`.
- Application service: `ISellingDashboardService`.
- Application service: `ISellingAnalyticsService`.
- Application service: `ISellingBulkImportService`.
- Application service: `ISellingBuyerPortalService` for both authenticated buyer access and public token access.
- DTO namespace or file group: `Liens.Application/DTOs/Selling`.
- Domain changes should add Figma selling workflow fields directly to `Lien`, with separate support entities only for buyer access links, activity, bulk imports, and idempotency.

Recommended public base path:

```http
/api/liens/selling
```

Avoid exposing the old portfolio-first model to the frontend. The frontend should work mostly with `lienId`.

## Canonical Selling State Machine

Keep selling state separate from the existing core lien lifecycle status.

Recommended field:

```text
SellerStatus
```

Allowed values:

```text
Pending
Internal
PreparedForSale
SubmittedForSale
Sold
Withdrawn
Archived
```

State meanings:

| State | Meaning |
| --- | --- |
| `Pending` | Lien is created through intake and visible in the seller portfolio pending tab. |
| `Internal` | Lien is retained internally and not actively submitted for sale. |
| `PreparedForSale` | Sale data is ready, buyer/funding company may be selected, but seller has not confirmed. |
| `SubmittedForSale` | Seller confirmed sale submission and buyer notification/access is active. |
| `Sold` | Lien sale is completed. |
| `Withdrawn` | Seller withdrew the lien from sale consideration. |
| `Archived` | Soft-hidden or closed from seller workflow. |

Allowed transitions:

| From | To | API |
| --- | --- | --- |
| `Pending` | `Internal` | `PUT /liens/{lienId}/lien-information` |
| `Internal` | `Pending` | `PUT /liens/{lienId}/lien-information` |
| `Pending` | `PreparedForSale` | `POST /liens/{lienId}/prepare-sale` |
| `Internal` | `PreparedForSale` | `POST /liens/{lienId}/prepare-sale` |
| `PreparedForSale` | `SubmittedForSale` | `POST /liens/{lienId}/confirm-sale` |
| `SubmittedForSale` | `Withdrawn` | `POST /liens/{lienId}/withdraw-sale` |
| `SubmittedForSale` | `Sold` | future sale completion or bill-of-sale flow |
| any non-`Sold` state | `Archived` | `POST /liens/{lienId}/archive` |

Rules:

- Do not hard-delete seller liens through the new selling API.
- Use archive/cancel/withdraw actions instead of `DELETE /liens/{lienId}`.
- Do not overload the existing `LienStatus` enum if it represents a different lifecycle.
- `SellerStatus` controls the Figma selling workflow; core `Lien.Status` must still be updated where existing offer and sale services require it.

## Core Lien Status Mapping

The implementation must maintain both `SellerStatus` and the existing core `Lien.Status`.

Required mapping:

| Selling action | SellerStatus result | Core `Lien.Status` result | Financial mapping |
| --- | --- | --- | --- |
| Create lien intake | `Pending` or `Internal` | `Draft` | `billingAmount` maps to `Lien.OriginalAmount`; core `Draft` is technical only and is not a seller workflow state |
| Update lien intake | unchanged | `Draft` | `billingAmount` maps to `Lien.OriginalAmount` |
| Prepare sale | `PreparedForSale` | `Draft` | `AskAmount` is staged on `Lien.AskAmount` |
| Confirm sale | `SubmittedForSale` | `Offered` | copy `AskAmount` to `Lien.OfferPrice` |
| Buyer offer submitted | unchanged | `Offered` or `UnderReview` | offer amount stored on `LienOffer` |
| Buyer accepted / bill of sale completed | `Sold` | `Sold` | `SoldAtUtc` set and `PurchasePrice` set |
| Withdraw sale | `Withdrawn` | `Withdrawn` | preserve previous sale pricing for audit |

This mapping is required because existing buyer offer creation only accepts core liens in `Offered` or `UnderReview`, and existing sale finalization marks the core lien as `Sold` after offer acceptance.

## Final API Surface

All authenticated seller APIs require:

- Valid JWT.
- Tenant context.
- `LiensPermissions.ProductCode` (`SYNQ_LIENS`) product access.
- Seller role or permission.
- Tenant/org-level ownership check for every lien, contact, document, and buyer operation.

Permission mapping:

| API group | Permission |
| --- | --- |
| Dashboard, list, detail, activity | `LiensPermissions.LienSaleRead` |
| Create lien, bulk import upload/confirm | `LiensPermissions.LienSaleCreate` |
| Save wizard steps, prepare sale, archive | `LiensPermissions.LienSaleUpdate` |
| Confirm sale, buyer access link generation | `LiensPermissions.LienSalePublish` |
| Withdraw sale | `LiensPermissions.LienSaleWithdraw` |
| Authenticated buyer view | `LiensPermissions.LienBrowse` or `LiensPermissions.LienReadHeld` |
| Authenticated buyer offer/decline | `LiensPermissions.LienOffer` |

### Dashboard

```http
GET /api/liens/selling/dashboard
```

Query parameters:

```text
tab=pending|internal|sold|all
search=string
fundingCompanyId=guid
lawFirmId=guid
caseManagerId=guid
facilityId=guid
initialServiceDateFrom=date
initialServiceDateTo=date
sortBy=lienId|fundingCompany|initialServiceDate|billingAmount|askAmount|highestBid|status
sortDirection=asc|desc
page=1
pageSize=25
```

Response shape:

```json
{
  "summary": {
    "totalPortfolioValue": 0,
    "totalPending": 0,
    "totalInternal": 0,
    "totalSold": 0,
    "pendingCount": 0,
    "internalCount": 0,
    "soldCount": 0
  },
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalCount": 0
}
```

### Lien List

```http
GET /api/liens/selling/liens
```

Purpose:

- Lien table list for the portfolio page.
- Can share filters with dashboard.
- Use when frontend wants list data without summary cards.

### Create Lien Intake

```http
POST /api/liens/selling/liens
```

Purpose:

- Start the single-lien wizard in `Pending` or `Internal`.
- Return a `lienId` immediately so later wizard steps can save independently.
- Do not expose a seller-facing `Draft` state.

Request:

```json
{
  "sellerStatus": "Pending",
  "source": "Single"
}
```

Response:

```json
{
  "lienId": "guid",
  "sellerStatus": "Pending"
}
```

### Lien Detail

```http
GET /api/liens/selling/liens/{lienId}
```

Purpose:

- Aggregate all detail page sections.
- Avoid making the frontend stitch together five backend calls.

Response sections:

```text
lienInformation
caseInformation
fundingCompany
medicalPricing
documents
saleReadiness
buyerOfferSummary
activity
availableActions
```

### Step 1: Lien Information

```http
PUT /api/liens/selling/liens/{lienId}/lien-information
```

Request:

```json
{
  "sellerStatus": "Pending",
  "initialServiceDate": "2026-07-19",
  "endServiceDate": null,
  "listingVisibility": "Private",
  "notes": "string"
}
```

Validation:

- `sellerStatus` can be `Pending` or `Internal` during intake.
- `initialServiceDate` is required before preparing the lien for sale.
- `listingVisibility` must be `Public` or `Private`.

### Step 2: Funding Company And Case Information

```http
PUT /api/liens/selling/liens/{lienId}/case-information
```

Request:

```json
{
  "fundingCompanyId": "guid",
  "fundingCompanyContactId": "guid",
  "handlingLawFirmId": "guid",
  "caseManagerId": "guid",
  "caseId": "guid",
  "createCaseIfMissing": false
}
```

Validation:

- Funding company must belong to the same tenant or be an approved marketplace buyer.
- Contact must belong to the selected funding company when provided.
- Law firm and case manager must be valid seller-accessible contacts.

### Step 3: Medical Code And Marketplace Pricing

```http
PUT /api/liens/selling/liens/{lienId}/medical-pricing
```

Request:

```json
{
  "askAmount": 12500,
  "billingAmount": 18000,
  "rows": [
    {
      "medicalCode": "99213",
      "description": "Office visit",
      "serviceDate": "2026-07-19",
      "billingAmount": 600,
      "medicareCost": 180,
      "targetSaleAmount": 350
    }
  ]
}
```

Validation:

- `askAmount` must be non-negative.
- Top-level `billingAmount` maps to `Lien.OriginalAmount`; do not add a separate `BillingAmount` column.
- Row amounts must be non-negative.
- Medical code rows are replaced atomically unless patch semantics are explicitly added later.

### Step 4: Documents

```http
PUT /api/liens/selling/liens/{lienId}/documents
```

Purpose:

- Save the document set attached to the wizard.
- Document binaries should still go through the existing document upload infrastructure.
- This endpoint should attach existing document IDs to the selling lien.

Request:

```json
{
  "documents": [
    {
      "documentId": "guid",
      "documentType": "MedicalBill",
      "displayName": "string"
    }
  ]
}
```

The wizard saves data while the lien remains `Pending` or `Internal`. The
`prepare-sale` validation is the readiness gate: it must reject a lien that
does not yet have its required lien, case/funding, pricing, or document data.

### Prepare Sale

```http
POST /api/liens/selling/liens/{lienId}/prepare-sale
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Request:

```json
{
  "buyerFundingCompanyId": "guid",
  "buyerContactId": "guid",
  "askAmount": 12500,
  "listingVisibility": "Private",
  "messageToBuyer": "string"
}
```

Purpose:

- Save buyer and sale preparation data.
- Moves eligible liens to `PreparedForSale`.
- This backs the "Prepare Your Lien for Sale" screen.

### Confirm Sale

```http
POST /api/liens/selling/liens/{lienId}/confirm-sale
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Request:

```json
{
  "confirmationAccepted": true,
  "sendBuyerNotification": true
}
```

Purpose:

- Called by the "Yes, Sell" confirmation modal.
- Moves `PreparedForSale` to `SubmittedForSale`.
- Sets core `Lien.Status` to `Offered` so existing buyer offer APIs can accept offers.
- Copies `AskAmount` to core `Lien.OfferPrice`.
- Must not move the lien to `Sold`.
- Must leave `SoldAtUtc` null.
- Sends buyer notification and enables buyer access through authenticated buyer APIs and, when requested by the seller flow, public token access.

Response:

```json
{
  "lienId": "guid",
  "sellerStatus": "SubmittedForSale",
  "lienStatus": "Offered",
  "buyerFundingCompanyId": "guid",
  "submittedForSaleAtUtc": "2026-07-19T00:00:00Z",
  "soldAtUtc": null
}
```

### Withdraw Sale

```http
POST /api/liens/selling/liens/{lienId}/withdraw-sale
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Request:

```json
{
  "reason": "string"
}
```

Purpose:

- Withdraw a lien from sale consideration.
- Moves `SubmittedForSale` to `Withdrawn`.

### Archive Lien

```http
POST /api/liens/selling/liens/{lienId}/archive
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Request:

```json
{
  "reason": "string"
}
```

Purpose:

- Soft-hide a lien from the seller workflow.
- Does not hard-delete underlying records.

### Activity

```http
GET /api/liens/selling/liens/{lienId}/activity
```

Purpose:

- Returns seller activity timeline.
- Include user, event type, timestamp, summary, and relevant metadata.

## Bulk Import API

### Download Template

```http
GET /api/liens/selling/bulk-import-template
```

Purpose:

- Returns the current bulk import template.
- Should include all required columns and examples.

### Create Bulk Import

```http
POST /api/liens/selling/bulk-imports
Content-Type: multipart/form-data
```

Request fields:

```text
file
templateType=SellingLienImport
defaultListingVisibility=Private
defaultSellerStatus=Pending
```

Response:

```json
{
  "importId": "guid",
  "status": "Uploaded",
  "totalRows": 0
}
```

### Get Bulk Import

```http
GET /api/liens/selling/bulk-imports/{importId}
```

Purpose:

- Import metadata, counts, status, creator, created date.

### Get Bulk Import Rows

```http
GET /api/liens/selling/bulk-imports/{importId}/rows
```

Query parameters:

```text
status=valid|invalid|created|failed|all
page=1
pageSize=100
```

### Validate Bulk Import

```http
POST /api/liens/selling/bulk-imports/{importId}/validate
```

Purpose:

- Parse and validate rows without creating liens.
- Returns row-level validation errors.

### Confirm Bulk Import

```http
POST /api/liens/selling/bulk-imports/{importId}/confirm
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Purpose:

- Creates liens from valid rows.
- Stores row-level success/failure.
- Should be resumable and safe if retried.

### Cancel Bulk Import

```http
DELETE /api/liens/selling/bulk-imports/{importId}
```

Purpose:

- Cancels an unconfirmed import.
- Does not delete liens already created by confirm.

## Buyer Portal API

Buyer access must support both authenticated buyer users and public email-token access.

Authenticated buyer APIs require JWT, `LiensPermissions.ProductCode` (`SYNQ_LIENS`) product access, buyer permissions, tenant isolation, and buyer organization access to the submitted lien.

### Authenticated Buyer View

```http
GET /api/liens/selling/buyer/liens/{lienId}
```

Purpose:

- Returns buyer-safe lien offer data for a logged-in funding company user.
- Must not expose seller internal notes, unrelated tenant data, or private document categories.

### Authenticated Buyer Offer

```http
POST /api/liens/selling/buyer/liens/{lienId}/offers
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Request:

```json
{
  "offerAmount": 10000,
  "message": "string"
}
```

### Authenticated Buyer Decline

```http
POST /api/liens/selling/buyer/liens/{lienId}/decline
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Request:

```json
{
  "reason": "string"
}
```

### Generate Buyer Access Link

```http
POST /api/liens/selling/liens/{lienId}/buyer-access-links
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Request:

```json
{
  "buyerFundingCompanyId": "guid",
  "buyerContactId": "guid",
  "expiresInHours": 168
}
```

Security:

- Store only a hashed token.
- Set expiry.
- Bind token to tenant, lien, buyer organization, and buyer contact.
- Audit generation and access.

### Public Token Buyer View

```http
GET /api/liens/selling/public/{token}
```

Purpose:

- Returns limited buyer-safe lien offer data.
- Must not expose seller internal notes, unrelated tenant data, or private document categories.

### Public Token Buyer Offer

```http
POST /api/liens/selling/public/{token}/offers
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Request:

```json
{
  "offerAmount": 10000,
  "message": "string"
}
```

### Public Token Buyer Decline

```http
POST /api/liens/selling/public/{token}/decline
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Request:

```json
{
  "reason": "string"
}
```

## Lookup APIs

Prefer reusing existing lookup/contact endpoints if they already return the required data. Add selling-specific facades only if the frontend needs consistent dropdown shapes.

Optional selling lookup facades:

```http
GET /api/liens/selling/lookups/funding-companies
GET /api/liens/selling/lookups/funding-company-contacts
GET /api/liens/selling/lookups/law-firms
GET /api/liens/selling/lookups/case-managers
GET /api/liens/selling/lookups/facilities
GET /api/liens/selling/lookups/medical-codes
GET /api/liens/selling/lookups/document-types
```

## Contacts API Plan

Contacts do not need to be owned by selling, but the Figma contact screens need backend support.

Use or modernize:

```http
GET  /api/liens/contacts
GET  /api/liens/contacts/{contactId}
POST /api/liens/contacts
PUT  /api/liens/contacts/{contactId}
PUT  /api/liens/contacts/{contactId}/deactivate
PUT  /api/liens/contacts/{contactId}/reactivate
POST /api/liens/contacts/export-csv
```

Add if missing:

```http
GET  /api/liens/contacts/{contactId}/detail
POST /api/liens/contacts/{contactId}/reassign-cases
```

Rules:

- Treat Figma "Delete Contact" as deactivate/soft delete unless product explicitly requires hard delete.
- Reassignment should return per-case success/failure results.
- Export should support current filters and selected IDs.

## Selling Analytics Backend Readiness Procedure

Add this procedure before any selling analytics backend implementation. It turns the analytics API from a route list into a build-ready contract with clear gates, ownership, and acceptance criteria.

### Contract Freeze Gate

Finalize these items before code begins:

- API routes.
- Query parameters.
- Enum values.
- Response DTOs for every endpoint.
- Export CSV columns and row grain.
- Error behavior.
- Metric formulas.

No backend engineer should invent response fields, null behavior, sort order, or metric semantics during implementation.

### Backend Contract Completion Procedure

Before implementation starts, create a contract matrix for every selling analytics endpoint. The matrix must contain:

- Route and HTTP method.
- Required permission.
- Query parameters and request body.
- Validation rules and `400` cases.
- Response DTO name and exact JSON fields.
- Numeric types and rounding rules.
- Default ordering, tie-breakers, pagination, and limits.
- Null/default behavior for missing analytics fields.
- Date anchor used by `dateFrom` and `dateTo`.
- Error status codes for auth, tenant, seller ownership, not found, and validation failures.

Freeze these enum values before code begins:

```text
sellerStatus=Pending|Internal|PreparedForSale|SubmittedForSale|Sold|Withdrawn|Archived
listingVisibility=Public|Private
dateDimension=submitted|sold|offer|service
grain=day|week|month
concentrationDimension=fundingCompany|facility|sellerStatus|listingVisibility
exportReport=overview|statusBreakdown|funnel|timeseries|offers|buyerPerformance|aging|concentration
```

If a frontend screen needs a value outside these enums, update the plan first; do not silently accept extra values in backend code.

### Backend Ownership Procedure

Use this ownership sequence for implementation:

1. Database engineer owns schema, EF configuration, migrations, snapshots, and index design.
2. Backend engineer owns DTOs, service interfaces, service implementation, endpoint registration, and export generation.
3. QA engineer owns analytics test matrix coverage and fixture data.
4. Reviewer validates auth, tenant isolation, metric correctness, and legacy portfolio compatibility before completion.

Do not implement analytics endpoint handlers before the database gate and contract matrix are complete. Do not implement export before the JSON read endpoints and shared query service are stable.

### Schema Readiness Gate

Analytics implementation is blocked until these fields exist on `Lien` and are mapped in EF:

```text
SellerStatus
ListingVisibility
FundingCompanyId
AskAmount
HighestBidAmount
SubmittedForSaleAtUtc
SoldAtUtc
WithdrawnAtUtc
ArchivedAtUtc
```

Schema, indexes, migrations, and snapshot updates require database-engineer coordination. Analytics v1 must use first-class stored fields only. Exclude `lawFirmId` and `caseManagerId` from v1 analytics filters unless they are denormalized into stored analytics-safe fields.

### Date And Filter Procedure

Use `dateFrom` and `dateTo` as ISO `YYYY-MM-DD` query parameters. Apply an inclusive lower bound and exclusive upper bound by converting `dateTo` to the next UTC day.

Treat these shared filters as multi-value filters:

```text
sellerStatus
listingVisibility
fundingCompanyId
facilityId
```

Validation must return `400` for invalid enum values, invalid dates, `dateFrom > dateTo`, and missing `dateDimension` on timeseries requests.

### Auth Procedure

Every analytics endpoint must require:

- Authenticated user JWT, not public-token flow.
- `LiensPermissions.ProductCode` (`SYNQ_LIENS`) product access.
- Sell mode.
- `LiensPermissions.LienSaleViewAnalytics`.
- Service-level `TenantId` and current seller organization ownership predicates in every query.

Buyer-only denial must be enforced by permission assignment and by seller organization ownership checks. Public-token endpoints must never route to analytics handlers.

### Endpoint Implementation Procedure

Create `SellingAnalyticsEndpoints.cs` instead of expanding the current portfolio-heavy `SellingEndpoints.cs`. Register it from `Program.cs` with `app.MapSellingAnalyticsEndpoints();` adjacent to `app.MapSellingEndpoints();`, using the same selling route authorization pattern.

### Service Implementation Procedure

Create:

```text
ISellingAnalyticsService
SellingAnalyticsService
SellingAnalyticsFilter
SellingAnalyticsDtos.cs
```

Use one shared filtered query builder for all analytics endpoints so dashboard cards, chart data, export, and filter options cannot drift.

### Metric Procedure

Rules:

- Read sold analytics from `SellerStatus = Sold` and `SoldAtUtc != null`.
- Use `Lien.PurchasePrice` as the live `soldAmount`.
- Use `LienOffer.OfferedAtUtc` for offer analytics.
- Use `InitialServiceDate` for service-date analytics.
- Use `SellingLienActivityEvents.CreatedAtUtc` for activity analytics once that support table exists.
- `POST /api/liens/selling/liens/{lienId}/confirm-sale` must remain `SubmittedForSale`; it must never count as `Sold`.
- Offer or bill-of-sale amount fallback is allowed only in controlled backfill or repair procedures, not live analytics.

### Export Procedure

Implement `POST /api/liens/selling/analytics/export` as synchronous CSV only for v1.

Rules:

- Maximum 10,000 rows.
- Return `400` if the filtered export would exceed 10,000 rows.
- Use the same filters and auth rules as JSON analytics endpoints.
- Return filename format `selling-analytics-{report}-{yyyyMMddHHmmss}.csv`.
- Freeze the report-specific column list during contract freeze.

### Required Backend Acceptance Gate

Implementation is not complete until:

- All endpoint DTOs are explicit and tested.
- Invalid enums, invalid dates, `dateFrom > dateTo`, and missing `dateDimension` on timeseries return `400`.
- Public-token and buyer-only access are denied.
- Legacy `/api/liens/selling/portfolios/{id}/analytics` still works.
- Query plans are checked for analytics-heavy endpoints on MySQL.

## Data Model Plan

Implementation must add these fields directly to `liens_Liens` through the `Lien` entity and EF configuration:

```text
SellerStatus
ListingVisibility
FundingCompanyId
FundingCompanyContactId
AskAmount
HighestBidAmount
SubmittedForSaleAtUtc
SoldAtUtc
WithdrawnAtUtc
ArchivedAtUtc
ArchivedReason
```

Do not create a separate selling metadata table for these Figma workflow fields.

Keep separate tables only for supporting workflow data:

```text
SellingBuyerAccessLinks
SellingLienActivityEvents
SellingBulkImports
SellingBulkImportRows
IdempotencyRecords
```

Indexing plan:

```text
TenantId + SellingOrgId + SellerStatus
TenantId + SellingOrgId + InitialServiceDate
TenantId + FundingCompanyId
TenantId + SellerStatus + ListingVisibility
TenantId + LienNumber
TenantId + SellingOrgId + SellerStatus + SubmittedForSaleAtUtc
TenantId + SellingOrgId + SellerStatus + SoldAtUtc
TenantId + SellingOrgId + InitialServiceDate
TenantId + SellingOrgId + FundingCompanyId + SellerStatus
TenantId + SellerOrgId + OfferedAtUtc for lien offers
TenantId + LienId + Status for lien offers
TokenHash for buyer access links
ImportId + RowNumber for bulk import rows
```

Migration safety:

- New fields should be nullable or have safe defaults for existing liens.
- Backfill `SellerStatus` conservatively.
- Avoid destructive migrations until frontend and tests are migrated.

### Buyer Access Token Security Migration Runbook

The buyer-access-token migration is intentionally forward-only. It adds
`TokenHash` and idempotency storage, retains the legacy nullable `Token` only
for a temporary compatibility window, and writes only hash values for newly
created access links. Its `Down` migration must fail rather than leave EF
migration history inconsistent with an irrecoverable bearer-token rollback.

Deployment procedure:

1. Before migration, run this duplicate-token preflight query. Resolve every
   returned token by revoking and reissuing the affected buyer links; the new
   hash lookup is global and its unique index cannot safely retain duplicate
   legacy tokens from different tenants.

   ```sql
   SELECT `Token`, COUNT(*) AS `TokenCount`
   FROM `liens_SellingBuyerAccessLinks`
   WHERE `Token` IS NOT NULL AND `Token` <> ''
   GROUP BY `Token`
   HAVING COUNT(*) > 1;
   ```

2. Back up the Liens database and apply the additive migration.
3. Deploy the new Liens API while the legacy-token compatibility trigger is
   active.
4. Drain all old public-buyer endpoint instances before permitting the new API
   to issue access links. Old binaries cannot resolve a new link because its
   legacy `Token` is intentionally null.
5. Verify public view, accept, decline, and offer behavior using a newly issued
   link, then remove the old application fleet.
6. Keep the compatibility trigger and legacy column only until the prior
   version is retired; remove them in a later, separately approved migration.

Rollback is an audited restore of the pre-migration database backup together
with the prior application version, or a corrective forward migration. Do not
run `dotnet ef database update <previous-migration>` for this migration.

## Idempotency Plan

Require `Idempotency-Key` on these operations:

```http
POST /api/liens/selling/liens
POST /api/liens/selling/liens/{lienId}/prepare-sale
POST /api/liens/selling/liens/{lienId}/confirm-sale
POST /api/liens/selling/liens/{lienId}/withdraw-sale
POST /api/liens/selling/liens/{lienId}/archive
POST /api/liens/selling/bulk-imports/{importId}/confirm
POST /api/liens/selling/buyer/liens/{lienId}/offers
POST /api/liens/selling/buyer/liens/{lienId}/decline
POST /api/liens/selling/liens/{lienId}/buyer-access-links
POST /api/liens/selling/public/{token}/offers
POST /api/liens/selling/public/{token}/decline
```

Rules:

- Scope idempotency by tenant, user or token subject, route, entity ID, and key.
- Return the original response when the same key is retried.
- Reject the same key with a different request body hash.

## Audit Plan

Record audit events for:

```text
SellingLienCreated
SellingLienInformationUpdated
SellingLienCaseInformationUpdated
SellingLienMedicalPricingUpdated
SellingLienDocumentsUpdated
SellingLienPreparedForSale
SellingLienConfirmedForSale
SellingLienWithdrawn
SellingLienArchived
SellingBulkImportUploaded
SellingBulkImportValidated
SellingBulkImportConfirmed
SellingBuyerAccessLinkCreated
SellingBuyerAccessLinkOpened
SellingBuyerOfferSubmitted
SellingBuyerOfferDeclined
ContactReassigned
ContactDeactivated
ContactExported
```

Each event should include:

```text
TenantId
UserId or buyer token subject
LienId when applicable
CaseId when applicable
ContactId when applicable
Before/after status when applicable
CorrelationId
TimestampUtc
```

## Security Plan

Authenticated seller APIs:

- Require JWT.
- Require `LiensPermissions.ProductCode` (`SYNQ_LIENS`) product access.
- Require seller role or permission.
- Enforce tenant isolation in every query.
- Enforce seller organization ownership.
- Never trust IDs from the client without tenant ownership validation.

Authenticated buyer APIs:

- Require JWT.
- Require `LiensPermissions.ProductCode` (`SYNQ_LIENS`) product access.
- Require buyer role or permission.
- Enforce tenant isolation.
- Enforce buyer organization access to the submitted lien.
- Return only buyer-safe fields.

Public buyer-token APIs:

- Do not accept raw tenant IDs from client.
- Resolve tenant, lien, buyer organization, and allowed documents from the hashed token.
- Expire tokens.
- Support revocation.
- Rate-limit by token/IP if infrastructure supports it.
- Return only buyer-safe fields.
- Audit all reads and writes.

## File Ownership For Implementation

Backend engineer:

- `apps/services/liens/Liens.Api/Endpoints/SellingEndpoints.cs`
- New selling endpoint files if split.
- `apps/services/liens/Liens.Application/DTOs/Selling`
- `apps/services/liens/Liens.Application/Services`
- `apps/services/liens/Liens.Application/Repositories`

Database engineer:

- `apps/services/liens/Liens.Domain/Entities`
- `apps/services/liens/Liens.Infrastructure`
- EF configurations and migrations.
- Indexes and backfill strategy.

Security engineer:

- Buyer token access model.
- Authorization checks.
- Tenant isolation checks.
- Idempotency and audit design review.

QA engineer:

- `apps/services/liens/Liens.Api.Tests`
- Integration coverage for all new selling flows.

Frontend engineer:

- Not needed for backend-only implementation.

## Safe Parallelization Plan

Can run in parallel:

- DTO contract drafting and test scenario drafting.
- Dashboard/detail read API design and database migration design.
- Contacts detail/reassignment work and selling dashboard work, if file ownership is separated.
- Bulk import parser planning and buyer portal security planning.

Must be sequential:

1. State machine decision before write endpoints.
2. Data model before repository implementation.
3. Repository implementation before service implementation.
4. Service implementation before endpoint wiring.
5. Endpoint wiring before integration tests.
6. Security review before public buyer-token APIs.
7. Frontend migration before old endpoint removal.

## Implementation Order

Phase 1: Contract freeze

- Finalize status values.
- Finalize required Figma fields.
- Finalize bulk import columns.
- Write DTOs and endpoint request/response contracts.

Phase 2: Data model

- Add selling-specific fields directly to `liens_Liens`.
- Map top-level API `billingAmount` to existing `Lien.OriginalAmount`.
- Persist medical pricing through the agreed `Lien` fields and existing medical-code lookup/service patterns.
- Attach documents through the existing document infrastructure; do not add a selling-specific document table in this rebuild.
- Add `SellingBulkImports` and `SellingBulkImportRows` support tables.
- Add buyer access link table.
- Add idempotency storage if not already available.

Phase 3: Read APIs

- `GET /dashboard`
- `GET /liens`
- `GET /liens/{lienId}`
- `GET /liens/{lienId}/activity`

Phase 3A: Selling analytics contract and read APIs

- Freeze analytics response DTOs and export CSV columns.
- Add `SellingAnalyticsFilter`, `ISellingAnalyticsService`, and `SellingAnalyticsService`.
- Add `SellingAnalyticsEndpoints.cs`.
- Register `app.MapSellingAnalyticsEndpoints();` adjacent to `app.MapSellingEndpoints();`.
- Implement JSON analytics endpoints before CSV export.

Phase 4: Single-lien wizard

- `POST /liens`
- `PUT /lien-information`
- `PUT /case-information`
- `PUT /medical-pricing`
- `PUT /documents`

Phase 5: Sale flow

- `POST /prepare-sale`
- `POST /confirm-sale`
- `POST /withdraw-sale`
- `POST /archive`

Phase 6: Bulk import

- Template.
- Upload.
- Validate.
- Preview rows.
- Confirm.
- Cancel unconfirmed import.

Phase 7: Buyer access

- Authenticated buyer view.
- Authenticated buyer offer.
- Authenticated buyer decline.
- Generate access link.
- Public view.
- Public offer.
- Public decline.
- Token revocation and expiry handling.

Phase 8: Deprecation

- Mark old portfolio-first selling APIs as deprecated.
- Migrate frontend to new lien-first APIs.
- Remove old endpoints only after tests pass and no callers remain.

## Validation Plan

Build:

```powershell
dotnet build apps/services/liens/Liens.Api/Liens.Api.csproj
```

Tests:

```powershell
dotnet test apps/services/liens/Liens.Api.Tests/Liens.Api.Tests.csproj
```

Add integration tests for:

- Dashboard cards and tab counts.
- Lien list search/filter/sort/pagination.
- Create a lien in `Pending` or `Internal`.
- Save each wizard step.
- Reject prepare-sale when required intake sections are missing.
- Prepare sale.
- Confirm sale with idempotency retry.
- Confirm sale sets `SellerStatus = SubmittedForSale`, not `Sold`.
- Confirm sale sets core `Lien.Status = Offered`.
- Confirm sale copies `AskAmount` to `Lien.OfferPrice`.
- Confirm sale leaves `SoldAtUtc` null.
- Withdraw sale.
- Archive lien.
- Bulk upload validation.
- Bulk confirm partial success.
- Authenticated buyer can view lien through `/api/liens/selling/buyer/liens/{lienId}`.
- Authenticated buyer can submit offer through `/api/liens/selling/buyer/liens/{lienId}/offers`.
- Buyer access link generation.
- Public-token buyer can view lien through `/api/liens/selling/public/{token}`.
- Public-token buyer can submit offer through `/api/liens/selling/public/{token}/offers`.
- Public buyer view data minimization.
- Public token response excludes seller internal notes and unrelated tenant data.
- Public buyer offer idempotency.
- Selling fields persist on `Lien`.
- Selling analytics unauthenticated request is denied.
- Selling analytics missing product access is denied.
- Selling analytics manage-mode request is denied.
- Selling analytics missing `LiensPermissions.LienSaleViewAnalytics` is denied.
- Buyer-only user cannot access selling analytics.
- Public-token flow cannot access selling analytics.
- Wrong seller organization cannot view lien analytics.
- Cross-tenant selling analytics access is denied.
- Selling analytics excludes confirm-sale liens from sold metrics.
- Selling analytics excludes `SoldAtUtc = null` liens from sold metrics.
- Selling analytics highest bid excludes rejected, withdrawn, and expired offers.
- Selling analytics date boundaries use inclusive `dateFrom` and exclusive next-day `dateTo`.
- Selling analytics filter options are seller-scoped.
- Selling analytics export returns `400` over 10,000 rows.
- Selling analytics export output matches the filtered read result.
- Contact detail.
- Contact reassignment.
- Contact export.

Manual verification:

- Confirm all queries are tenant-scoped.
- Confirm public token endpoints do not require JWT but do enforce token constraints.
- Confirm old endpoints are not removed until replacement is validated.

## Risks

- Figma showed some screens through metadata only because the Figma MCP seat limit was reached before every detailed screen could be inspected.
- `Pending`, `Internal`, and `Sold` may mean business display states, not core lien lifecycle states.
- Public buyer portal token access is security-sensitive.
- Bulk import can create partial data unless row transaction boundaries are designed carefully.
- Existing worktree has many modified backend and frontend files; implementation must avoid overwriting unrelated user changes.
- Existing legacy flows may store important data in generic fields or notes; migration needs careful compatibility checks.

## Open Questions

1. What exact fields are required for medical code and marketplace pricing?
2. What exact columns are required in the bulk import template?
3. Should `Internal` liens ever be visible to buyers?
4. Are funding companies tenant contacts, marketplace organizations, or both?
5. Which document types are required before a lien can be prepared for sale or sold?

## Recommendation

Proceed with the new lien-first selling API contract, keep it inside the existing Liens service, and implement it beside the current selling endpoints first. Deprecate the old portfolio-first API only after the frontend is migrated and integration tests cover the new Figma-based flow.
