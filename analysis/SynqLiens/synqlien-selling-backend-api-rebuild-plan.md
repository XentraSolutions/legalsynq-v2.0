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
- Draft save and final submit.

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
- Application service: `ISellingBulkImportService`.
- Application service: `ISellingBuyerPortalService`, only if public buyer access is required.
- DTO namespace or file group: `Liens.Application/DTOs/Selling`.
- Domain entities or owned models for selling metadata, medical pricing rows, access links, and activity.

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
Draft
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
| `Draft` | Lien wizard is started but not submitted. |
| `Pending` | Lien is submitted and visible in the seller portfolio pending tab. |
| `Internal` | Lien is retained internally and not actively submitted for sale. |
| `PreparedForSale` | Sale data is ready, buyer/funding company may be selected, but seller has not confirmed. |
| `SubmittedForSale` | Seller confirmed sale submission and buyer notification/access is active. |
| `Sold` | Lien sale is completed. |
| `Withdrawn` | Seller withdrew the lien from sale consideration. |
| `Archived` | Soft-hidden or closed from seller workflow. |

Allowed transitions:

| From | To | API |
| --- | --- | --- |
| `Draft` | `Pending` | `POST /liens/{lienId}/submit` |
| `Draft` | `Internal` | `POST /liens/{lienId}/submit` |
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

## Final API Surface

All authenticated seller APIs require:

- Valid JWT.
- Tenant context.
- `SYNQLIEN` product access.
- Seller role or permission.
- Tenant/org-level ownership check for every lien, contact, document, and buyer operation.

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

### Create Draft Lien

```http
POST /api/liens/selling/liens/drafts
```

Purpose:

- Start single-lien wizard.
- Return a `lienId` immediately so later wizard steps can save independently.

Request:

```json
{
  "sellerStatus": "Draft",
  "source": "Single"
}
```

Response:

```json
{
  "lienId": "guid",
  "sellerStatus": "Draft"
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

- `sellerStatus` can be `Draft`, `Pending`, or `Internal` during intake.
- `initialServiceDate` is required before submit.
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

### Submit Lien Intake

```http
POST /api/liens/selling/liens/{lienId}/submit
```

Headers:

```http
Idempotency-Key: required-guid-or-client-generated-key
```

Purpose:

- Finalize the add-lien wizard.
- Moves `Draft` to `Pending` or `Internal`.

Validation:

- Required lien information exists.
- Required case/funding information exists.
- Required pricing fields exist.
- Required documents exist if product rules require them.

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
- Sends buyer notification or generates buyer access depending on the configured buyer access model.

Response:

```json
{
  "lienId": "guid",
  "sellerStatus": "SubmittedForSale",
  "buyerFundingCompanyId": "guid",
  "submittedForSaleAtUtc": "2026-07-19T00:00:00Z"
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

Only implement this if the product confirms that funding companies can view offers through an email-token temporary portal.

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

### Public Buyer View

```http
GET /api/liens/selling/public/{token}
```

Purpose:

- Returns limited buyer-safe lien offer data.
- Must not expose seller internal notes, unrelated tenant data, or private document categories.

### Submit Buyer Offer

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

### Decline Offer

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

## Data Model Plan

Add or verify fields on the core lien/selling model:

```text
SellerStatus
ListingVisibility
FundingCompanyId
FundingCompanyContactId
AskAmount
BillingAmount
HighestBidAmount
SubmittedForSaleAtUtc
SoldAtUtc
WithdrawnAtUtc
ArchivedAtUtc
ArchivedReason
```

Add tables if not already represented cleanly:

```text
SellingLienMedicalPricingRows
SellingLienDocuments
SellingBuyerAccessLinks
SellingLienActivityEvents
SellingBulkImports
SellingBulkImportRows
IdempotencyRecords
```

Indexing plan:

```text
TenantId + SellerOrganizationId + SellerStatus
TenantId + SellerOrganizationId + InitialServiceDate
TenantId + FundingCompanyId
TenantId + SellerStatus + ListingVisibility
TenantId + LienNumber
TokenHash for buyer access links
ImportId + RowNumber for bulk import rows
```

Migration safety:

- New fields should be nullable or have safe defaults for existing liens.
- Backfill `SellerStatus` conservatively.
- Avoid destructive migrations until frontend and tests are migrated.

## Idempotency Plan

Require `Idempotency-Key` on these operations:

```http
POST /api/liens/selling/liens/{lienId}/submit
POST /api/liens/selling/liens/{lienId}/prepare-sale
POST /api/liens/selling/liens/{lienId}/confirm-sale
POST /api/liens/selling/liens/{lienId}/withdraw-sale
POST /api/liens/selling/liens/{lienId}/archive
POST /api/liens/selling/bulk-imports/{importId}/confirm
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
SellingLienDraftCreated
SellingLienInformationUpdated
SellingLienCaseInformationUpdated
SellingLienMedicalPricingUpdated
SellingLienDocumentsUpdated
SellingLienSubmitted
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
- Require `SYNQLIEN` product access.
- Require seller role or permission.
- Enforce tenant isolation in every query.
- Enforce seller organization ownership.
- Never trust IDs from the client without tenant ownership validation.

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
- Finalize buyer access model.
- Write DTOs and endpoint request/response contracts.

Phase 2: Data model

- Add selling-specific fields.
- Add medical pricing rows if existing models are not sufficient.
- Add document link table if existing relationships are not sufficient.
- Add bulk import tables or extend current batch upload model.
- Add buyer access link table only if public buyer portal is required.
- Add idempotency storage if not already available.

Phase 3: Read APIs

- `GET /dashboard`
- `GET /liens`
- `GET /liens/{lienId}`
- `GET /liens/{lienId}/activity`

Phase 4: Single-lien wizard

- `POST /liens/drafts`
- `PUT /lien-information`
- `PUT /case-information`
- `PUT /medical-pricing`
- `PUT /documents`
- `POST /submit`

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

- Generate access link.
- Public view.
- Submit offer.
- Decline.
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
- Create draft lien.
- Save each wizard step.
- Submit lien.
- Reject submit when required sections are missing.
- Prepare sale.
- Confirm sale with idempotency retry.
- Withdraw sale.
- Archive lien.
- Bulk upload validation.
- Bulk confirm partial success.
- Buyer access link generation.
- Public buyer view data minimization.
- Public buyer offer idempotency.
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

1. Should buyer access be public-token based, login-based, or both?
2. What exact fields are required for medical code and marketplace pricing?
3. What exact columns are required in the bulk import template?
4. Does `Sold` happen immediately after seller confirmation, or only after buyer acceptance and bill-of-sale completion?
5. Should `Internal` liens ever be visible to buyers?
6. Are funding companies tenant contacts, marketplace organizations, or both?
7. Which document types are required before a lien can be submitted or sold?

## Recommendation

Proceed with the new lien-first selling API contract, keep it inside the existing Liens service, and implement it beside the current selling endpoints first. Deprecate the old portfolio-first API only after the frontend is migrated and integration tests cover the new Figma-based flow.
