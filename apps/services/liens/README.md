# Liens Service (SynqLien)

Medical lien lifecycle management — creation, marketplace listing, offer/purchase workflow, and servicing.

**Port:** 5009 (API) + Flow service at 5015 (workflow engine)

## Responsibilities

- Lien CRUD (Draft → Offered → Accepted / Declined → Sold / Withdrawn)
- Marketplace browse and search
- Offer submission and negotiation
- Direct purchase at asking price
- Portfolio management for buyers and holders
- Case management (liens grouped under cases)
- Bill of Sale generation and execution
- Servicing items and task tracking
- Document attachment per lien/case

## Layer Structure

```
Liens.Api/            Endpoints, middleware, Program.cs (port 5009)
Liens.Application/    Interfaces, DTOs, services
Liens.Domain/         Lien, LienOffer, Case, ServicingItem, BillOfSale
Liens.Infrastructure/ DbContext (LiensDb), repositories, EF migrations
```

## Key Endpoints

| Method          | Path                                                                                                | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| --------------- | --------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET`           | `/api/liens`                                                                                        | List liens (my-liens / marketplace)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `POST`          | `/api/liens`                                                                                        | Create lien                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| `GET`           | `/api/liens/{id}`                                                                                   | Lien detail                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| `POST`          | `/api/liens/{id}/offer`                                                                             | Submit offer (buyer)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| `POST`          | `/api/liens/{id}/accept-offer`                                                                      | Accept offer (seller)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `POST`          | `/api/liens/{id}/purchase`                                                                          | Direct purchase                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `GET`           | `/api/liens/portfolio`                                                                              | Buyer/holder portfolio                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `GET`           | `/api/liens/cases`                                                                                  | Case list                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| `GET`           | `/api/liens/cases/{id}`                                                                             | Case detail                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| `GET`           | `/api/assistant-tools/liens/{id}`, `/api/assistant-tools/liens/by-number/{lienNumber}`              | Tenant- and visibility-scoped lien details for Xenia. `reductionAmount` is the latest persisted lien reduction by reduction date and creation time, matching the tenant portal; it is not derived from billing and purchase amounts. Date-only values such as `purchaseDate` are returned as ISO `yyyy-MM-dd` calendar dates and must not be timezone-shifted.                                                                                                                                                                                                            |
| `POST`          | `/service/case/v3`                                                                                  | Returns the paginated legacy servicing case list with settlement status/date, total settled amount, and case-level billing/purchase totals. Search accepts `keyword` or the servicing UI's compatibility alias `search`; a nonblank `keyword` takes precedence. Plaintiff names support exact, reversed exact, fuzzy/reversed fuzzy, and partial matching in that ranking order. Existing tenant and request filters are applied before ranking, and filtered candidates remain bounded to 5,000 before result pagination. The settled total prefers imported `totalSettledAmount` metadata, falls back to the current settlement-row amount, and then uses recorded payment amounts when no settlement total exists. Billing and purchase amounts aggregate linked legacy medical-code records and fall back to lien-level values; settlement dates use the latest recorded settlement date with a payment-record fallback for historical rows. |
| `PATCH`         | `/service/update-details`                                                                           | Updates servicing status and case metadata without replacing the case's user-authored notes. Law-firm, attorney, and case-manager values are contact IDs; law-firm updates do not change the case organization/tenant scope. Omitted relationship fields remain unchanged, while explicit empty values clear their metadata.                                                                                                                                                                                                                                              |
| `GET`           | `/api/liens/cases/dashboard/task-summary`                                                           | Legacy-compatible, assignee-scoped task dashboard. Returns the `isSuccess`/`message`/`data` envelope with total, upcoming, in-progress, in-review, and completed counts plus the task list. Counts recognize legacy numeric IDs, task-service codes, UI codes, and display names. Each task's `status` is normalized to `UPCOMING`, `INPROGRESS`, `INREVIEW`, `COMPLETED`, or `CANCELLED`; `statusId` preserves the stored value.                                                                                                                                         |
| `POST`          | `/api/liens/cases/task/create`, `/api/liens/cases/tasks/create`                                     | Creates a legacy case task. Priority accepts `High`, `Medium`, and `Low` case-insensitively; `Medium` maps to the servicing-domain `Normal` value while remaining `Medium` in legacy responses.                                                                                                                                                                                                                                                                                                                                                                           |
| `POST`, `PATCH` | `/api/liens/cases/task/update`                                                                      | Updates a legacy case task. Both methods are supported for compatibility with deployed clients. Status accepts legacy IDs/names and current UI codes such as `UPCOMING`, `INPROGRESS`, `INREVIEW`, `COMPLETED`, and `CANCELLED`; the compatibility value is preserved while the backing servicing item receives a valid canonical status.                                                                                                                                                                                                                                 |
| `DELETE`        | `/api/liens/cases/delete/{id}`                                                                      | Legacy case deletion; blocks when a linked lien is active, and detaches terminal/rejected liens before removing the case                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `POST`          | `/api/liens/cases/generate-csv`                                                                     | Exports cases as Base64-encoded CSV using canonical case fields plus raw migrated metadata and contact/audit enrichment for legacy-only columns; its accident-type and case-manager filters use canonical IDs with legacy metadata fallback                                                                                                                                                                                                                                                                                                                               |
| `GET`           | `/service/liens/settlement/payment-details/{caseId}`                                                | Returns complete legacy payment details, including check/reference number, payment method/payor, note, settlement type/status IDs and display names, and net profit from current or migrated payment metadata. Canonical settled liens are displayed as `Closed`, and legacy snake-case settlement type codes are returned as human-readable names while their original IDs are preserved.                                                                                                                                                                                |
| `PUT`           | `/api/liens/settlement/payments/{paymentId}`                                                        | Tenant-scoped settlement-payment update. The strict body requires amount, payment date/method/reference, notes, settlement type/status, and lien status; unknown fields return `400`. Case/lien linkage remains immutable, missing or cross-tenant payments return `404`, and payment plus linked-lien status changes commit atomically.                                                                                                                                                                                                                                     |
| `POST`          | `/service/settlement/history/v3`                                                                    | Returns paged payment, reduction, settlement, and legacy-compatible law-firm change history. Servicing-detail updates plus direct case-update, single-case, and batch law-firm reassignment routes atomically write a `law-firm-change` item only when the law firm actually changes, including unresolved legacy identifiers, the immediate or scheduled-switch description, actor, and timestamp. Future-dated switches retain the current firm and expose the requested firm as `pendingLawFirmId`; a startup-and-minute scheduled processor promotes due assignments. |
| `POST`          | `/api/liens/reports/diy/export`                                                                     | Export a DIY report as Base64-encoded CSV in the legacy `data` export envelope                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `POST`          | `/api/liens/reports/case-notes-history`, `/report/case-notes-history`                               | Returns the tenant-wide paged Case Notes History report. `noteType=TRACKING` maps to general/follow-up notes; `noteType=FEED` maps to feed notes. Results are tenant-safe, sortable, `no-store`, and include complete note content. Native and reconciled legacy notes remain available when stale legacy note crosswalks exist; only their unreconciled target rows are excluded and reported through the additive completeness fields.                                                                                                                                                    |
| `POST`          | `/api/liens/reports/case-notes-history/export`, `/report/case-notes-history/export`                 | Returns the matching full CSV in the legacy Base64 export envelope with the same unreconciled-row exclusion and completeness warning as preview. CSV cells are formula-safe and generation stops at the existing 10 MiB limit.                                                                                                                                                                                                                                                                                                                                          |
| `POST`          | `/report/weekly-bcc`, `/api/liens/reports/weekly-bcc`                                               | Returns the complete tenant-scoped Weekly BCC lien report for `{ "asOfDate": "YYYY-MM-DD" }`, including additive `summaryTotals` for total/open/closed cases, total/open liens, and purchase, returned, and billing amounts. The date is an inclusive purchase cutoff and the reference for days-since-purchase/annualized ROI; other fields reflect current stored state because historical snapshots are not available. Responses are `no-store` and require SynqLien product access plus case-read permission.                                                                                                                                                                                        |
| `GET`           | `/report/auto-generated`, `/api/liens/reports/auto-generated`                                       | Returns the authenticated tenant's auto-generated report metadata ordered newest first. Supports `page` and `pageSize` (maximum 100), returns `totalCount`, and uses `Cache-Control: no-store`.                                                                                                                                                                                                                                                                                                                                                                           |
| `POST`          | `/report/auto-generated/{reportId}/execute`, `/api/liens/reports/auto-generated/{reportId}/execute` | Executes an auto-generated report using its tenant-scoped stored date. Query parameters `page` (default 1) and `pageSize` (default 50, maximum 100) apply database paging before row enrichment. The response includes `page`, `pageSize`, `totalPages`, full-result `totalCount`, and the versioned 57-column Weekly BCC schema even when the requested page is empty. The stored API path is an exact in-process dispatch key; it is never requested as a URL.                                                                                                          |
| `POST`          | `/report/auto-generated/{reportId}/export`, `/api/liens/reports/auto-generated/{reportId}/export`   | Exports every eligible row from the tenant-scoped stored Weekly BCC report in the established Base64 CSV envelope. Rows are processed in bounded pages, headers follow the versioned 57-column schema (including the `noted` field labeled `Notes`), cells are formula-safe, and generation stops at 10 MiB. No request body or pagination parameters are required.                                                                                                                                                                                                                                                     |
| `POST`          | `/api/liens/cases/dashboard/total-lien-report-export/v3`                                            | Returns legacy-eligible liens with full-result status and billing/purchase summaries; a valid inclusive `startDate`/`endDate` or `purchaseDateFrom`/`purchaseDateTo` range filters lien purchase dates before summaries and paging, while an omitted range defaults through the previous Pacific calendar day and excludes future or undated liens; paged JSON requests calculate summaries from compact database projections and enrich only the requested page, while CSV still loads every matching export row                                                         |
| `POST`          | `/api/liens/cases/dashboard/total-case-report-export/v3`                                            | Returns all tenant/org-visible cases, including newly created cases with no liens, with full-result status counts when no date range is supplied. A valid inclusive `startDate`/`endDate` or `purchaseDateFrom`/`purchaseDateTo` range restricts results to cases having at least one linked lien purchased in range before status aggregation and paging. Paged, unfiltered JSON requests aggregate statuses in the database and enrich only the requested page.                                                                                                                |
| `POST`          | `/api/liens/cases/dashboard/lawfirm-case-report-export/v3`                                          | Returns all tenant/org-visible cases assigned to a law firm when no date range is supplied. An explicit date range filters cases by the purchase date of any linked lien; unfiltered paged JSON requests stream the full allocation summary from compact metadata and enrich only the requested page.                                                                                                                                                                                                                                                                       |
| `POST`          | `/api/liens/cases/dashboard/medical-provider-report-export/v3`                                      | Returns lien/facility allocation filtered by lien purchase date; paged JSON requests stream full-result medical-code and facility summaries from compact projections and enrich only the requested page, without selecting selling-party compatibility columns that are unrelated to the report                                                                                                                                                                                                                                                                           |
| `POST`          | `/api/liens/cases/dashboard/deployed`                                                               | Sums active imported legacy medical-code purchase amounts for liens with a persisted `PurchaseDate`; it does not fall back to lien-level `PurchasePrice`. An omitted range defaults through the previous Pacific calendar day.                                                                                                                                                                                                                                                                                                                                            |
| `POST`          | `/api/liens/cases/dashboard/cash-received`                                                          | Sums non-deleted settlement-row amounts by persisted `SettlementDate`. An explicit range is inclusive; an omitted range defaults through the previous Pacific calendar day and excludes undated or future settlements.                                                                                                                                                                                                                                                                                                                                                    |

All four V3 report endpoints return paginated rows plus full-result summaries. When paging is
missing or invalid, JSON requests default to `page: 1` and return all matching rows; positive
`page` and `limit` values are honored for the returned `items` array.
Set `isCsv: true` or `isCsv: "yes"` for an uncapped Base64-encoded CSV export.
Dashboard pie-chart and deployed/cash-received metrics use database grouping, aggregation, and streaming note
processing instead of retaining every matching case, lien, servicing note, or settlement in memory. Report contact
enrichment is restricted to referenced contacts, organization law firms, providers, and facilities rather than
loading the tenant-wide contact table.
The tenant dashboard requests one report row alongside the full-result summaries, then loads a selected
report's detailed breakdown in server-paginated pages of 10 rows. This keeps the initial four V3 JSON responses
bounded while preserving uncapped CSV exports and access to every report page.

DIY reports treat the legacy UI sentinel `isBulk: "N"` as no bulk filter, matching the legacy report SQL. Explicit `Y`/`Yes` selects bulk liens, while canonical `No`/`False`/`0` selects non-bulk and unset liens. Legacy relationship filters for law firm, attorney, funding company, medical facility, case manager, and medical provider are applied before pagination and summary calculation. Lien-status filter values may be either status codes or IDs from the lien-status lookup category.
CASES previews and exports include matching cases that do not yet have a linked lien unless a lien-dependent filter is selected. Server-generated case numbers use `YY-00000` and advance from the highest numeric suffix for the current year; existing six-digit legacy suffixes remain valid inputs when calculating the next sequence.
The legacy DIY `filter-options` endpoint returns both standalone case-manager contacts and case managers stored as law-firm subcontacts.

Case Notes History is an on-demand report and is not stored or scheduled through `AutoGeneratedReports`. Native v2 notes and reconciled historical notes are immediately eligible. An unreconciled `SL-CORE` case-note crosswalk excludes only its tenant-matching target note from preview and export; successful responses set `isComplete=false`, return `excludedUnreconciledLegacyNoteCount`, and include a `legacy_history_incomplete` warning until source-backed category reconciliation in `scripts/LegacyLiensImport` versions that note hash. No organization ID or tenant ID is accepted from the report request body.

Weekly BCC metadata reconciliation is controlled by `AutoGeneratedReports`. The scheduler is disabled by default,
uses DST-aware Pacific time, reconciles the latest due Friday at 4:00 AM, and retries on the configured reconciliation
interval. Eligible tenants come from Tenant Service's service-token-protected SynqLiens entitlement endpoint. Set
`AutoGeneratedReports:TenantServiceBaseUrl`, configure the shared `FLOW_SERVICE_TOKEN_SECRET`, verify the EF migration,
and only then set `AutoGeneratedReports:SchedulerEnabled=true`.
The auto-generated-report migration uses guarded MySQL DDL. Liens retries pending migrations after startup schema
recovery and independently replays the guarded table/index operations, covering both unapplied migrations and migration
history that was recorded after only partial DDL.

The DIY `ALL` status view includes every non-deleted lifecycle state, including rejected and cancelled liens; `CLOSED` includes settled liens and `REJECTED` includes declined, withdrawn, and cancelled liens. Report previews honor `page` and `limit`, while `/api/liens/reports/diy/export` exports every row that matches the filters.

DIY report billing and purchase columns aggregate `billingAmount` and `purchaseAmount` from linked legacy medical-code records, falling back to lien-level amounts when none exist. For LIENS compatibility responses, `summaryTotals.totalBillingAmt` retains the legacy card behavior and contains outstanding billing (`gross billing - returned`); `summaryTotals.grossBillingAmt` exposes gross billing, and `summaryTotals.totalAmtToSettle` contains the same outstanding value. Settlement, reduction, returned-amount, gross-profit, and ROI fields use imported legacy settlement metadata when it is available, matching the legacy DIY report formulas.

The optional DIY `notes` column returns the latest active, nonblank Feed note for the case, and `notes_date` returns that same note's creation date in `MM/dd/yyyy` format. Both are non-default `procedureInfo` columns populated through one tenant-scoped batch query for CASES, LIENS, and COMBINED reports, including cases without liens. Deleted, blank, non-Feed, and cross-tenant notes are excluded; equal timestamps use descending note ID. Missing notes produce an empty `notes` value and a null preview/blank CSV date. Saved report preview and export share the same mapping.

The DIY column key `last_case_note` is labeled **Tracking Notes** and returns every active, nonblank General or Follow-up note for the case, newest first and separated by line breaks. `last_case_note_date` is labeled **Last Tracking Note Date** and contains the newest included note date. Feed, internal, system/history, deleted, blank, and cross-tenant notes are excluded. Preview and CSV export use the same aggregation, and the report UI preserves the line breaks as stacked entries.

The separate Case Update compatibility keys use the newest active Case Activity row: `last_case_tracking_note` is labeled **Last Activity** and contains its normalized Description, while `last_case_tracking_date` is labeled **Last Activity Date** and contains its Pacific-time Timestamp (`MM/dd/yyyy hh:mm tt`). DIY previews and CSV exports share this tenant-scoped mapping for Case Created and internal Case Details Update activities; equal timestamps use descending activity ID, matching the Case Activity table.

The DIY `ucc_filed` column returns `Yes` when the linked case UCC flag is true and `No` otherwise, including when legacy case metadata does not contain the flag. Preview generation and CSV export use the same normalized value.

`PATCH /api/liens/cases/details-update` records every explicit Notes change in case-update history. Notes-only changes appear in `POST /api/liens/cases/case-updates/v3` as action **Case Details Update** with description **Case Tracking Note Update**; unchanged normalized values do not create duplicates, and clearing an existing Notes value is logged. A changed nonblank value continues to be appended separately to Tracking Notes. Case creation history prefers the authenticated user's email over the token's organization-oriented display name for its **Updated By** value.

`POST /api/liens/settlement/create` preserves its settlement-detail status.
`POST /api/liens/settlement/payments` stores settlement type (for example, `By Attorney`),
settlement status (for example, `Full Payment`), and lien status independently. An `Open`
or `Closed` lien status updates the linked lien to `Active` or `Settled`, respectively;
legacy callers that supplied `Open` or `Closed` through `settlementStatus` remain supported.
For their historical rows, a payment outcome formerly saved as the type is displayed as settlement status,
while an unavailable settlement type is displayed as `Other` rather than left blank or inferred incorrectly.
Payment creation accepts both `settlementType`/`settlementStatus` and the legacy `type`/`status` field names;
the payment-details response displays supported types as `By Attorney`, `By Medical Provider`,
`By Funding Company`, or `Other`.
Payment edits use `PUT /api/liens/settlement/payments/{paymentId}` with the payment ID returned by the
payment-details response. The update preserves unrelated payment metadata, replaces the submitted user note,
updates amount/date/reference/method/classification and audit fields, and keeps case/lien linkage immutable.
Payment method and reference are required and nonblank, reference is limited to 100 characters, and notes must
be supplied but may be empty. Metadata delimiters and the reserved `[legacy-meta]` marker are rejected from
classification fields; the marker is also rejected from notes while ordinary note punctuation remains valid.
`POST /service/liens/update/settlement` remains create-only and does not edit settlement payments.
Other settlement values do not change the lien. In the legacy payment-details response, a zero saved payment
amount uses the linked amount-to-settle as `checkAmount` instead of displaying
`0.00`. New payments that omit `paymentNumber` receive the next positive case payment number.
Historical zero-number rows receive deterministic non-zero display numbers. The payment-details
`amountToSettle` uses the recorded payment allocation before falling back to a linked settlement or
the lien's current balance, so closing a lien does not replace its payment-time amount with zero.
The legacy payment-details response returns the grouped legacy value (`Open` or `Closed`) in both
`lienStatus` and `lienStatusId` rather than exposing the canonical persisted lien status.

List filters accept both canonical persisted statuses and the legacy/UI lifecycle groups: `Open` expands to all active lien states, `Closed` expands to `Settled`, and `Rejected` expands to `Declined`, `Withdrawn`, and `Cancelled`. Historical rows literally persisted as `Rejected` remain hidden by default. Status/date-only lien-list filters are counted and paged in the database before per-lien detail and servicing enrichment, so broad status selections do not enrich the entire matching result set. The V3 case filter accepts comma-separated status, law-firm, case-manager, and accident-type selections. Case status filtering uses the saved legacy status label to distinguish `New`, `Processing`, and `Pre-Demand` cases that share the canonical `PreDemand` state, and to distinguish `Litigation` from `Negotiations` cases that share `InNegotiation`. The complete SL-CORE import preserves those labels, and the guarded relationship backfill repairs them for already-imported cases that have not since changed status. Law-firm values match the contact ID saved in case metadata and continue to accept legacy organization IDs.

## Selling Workflow

Seller-mode endpoints live under `/api/liens/selling` and require SynqLien product access plus sell mode. The Selling V2
lien-first lifecycle is `Pending`/`Internal` → `SubmittedForSale` → `Sold`; seller draft is not exposed. `PreparedForSale`
remains accepted only as a legacy transition state for records created by earlier deployments.
When a buyer declines a submitted lien offer, the buyer access-link response remains `Declined`, but the seller-facing lien
automatically returns to `Pending` so it stays searchable in the Pending list and can be submitted for sale again.
Intake writes are permitted only while the lien is `Pending` or `Internal`. State-changing V2 routes require an
`Idempotency-Key`; a retry with the same payload replays its stored response, while reusing the key with a different payload
returns `409 Conflict`.

Import [`LegalSynq Selling V2 API.postman_collection.json`](LegalSynq%20Selling%20V2%20API.postman_collection.json) into
Postman, set the collection variables for the appropriate seller or buyer token, and use a fresh `idempotencyKey` for each
new mutation (reuse it only to retry that exact request).

| Method | Path                                                            | Description                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| ------ | --------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET`  | `/api/liens/selling/dashboard?tab=pending\|internal\|sold\|archived\|all` | Returns seller-scoped portfolio totals, tab counts, and a paginated lien table. Summary amounts aggregate the displayed billing amounts for the filtered Pending, Internal, and Sold lists; Total Portfolio Value is their sum and excludes statuses outside those lists. Approval-stage rows (`Approval`, `PreparedForSale`, and `SubmittedForSale`) remain in the Pending tab while preserving their current status label. The Archived tab returns only soft-archived liens. Supports search, funding company, law firm, case manager, facility, initial-service-date, and sort filters. Accepted liens are categorized and displayed as Sold. |
| `GET`  | `/api/liens/selling/liens?tab=pending\|internal\|sold\|archived\|all`     | Returns the same seller-scoped, filtered, paginated lien rows without dashboard totals. Approval-stage rows remain searchable in the Pending tab, Archived rows remain searchable in the Archived tab, and Accepted liens remain searchable in the Sold tab.                                                                                                                                                                                                                                                                                                   |
| `GET`  | `/api/liens/selling/analytics/dashboard`                        | Returns the composite operations-dashboard read model for the authenticated seller organization. Optional inclusive `startDate`/`endDate` values must be supplied together and cannot exceed 366 days; the default is the current UTC calendar month. `compare=previousPeriod` (default) returns an adjacent equal-length comparison period when it is representable, while `compare=none` omits it. Requires `SYNQ_LIENS`, sell mode, and `SYNQ_LIENS.lien_sale:view_analytics`. |

The operations dashboard uses `InitialServiceDate` as the lien cohort date and reports USD because lien records do not
persist a currency code. Total Lien Revenue is the sum of `OriginalAmount`; Total Outstanding is the sum of
`CurrentBalance`, falling back to `OriginalAmount` only when the balance is null. Payments sum non-deleted settlement
payment details by their persisted `PaymentDate` and seller-owned lien. `lienStatuses` is the operational distribution of
persisted `Lien.Status` values. `sellerStatuses` is the separate seller lifecycle distribution: a completed Sold status
requires seller/lien lifecycle evidence plus both `SoldAtUtc` and `PurchasePrice`; Accepted remains separate, and
inconsistent legacy Sold rows appear as `SaleIncomplete`. Status and monthly time-series queries aggregate database
projections rather than loading full lien/offer sets.

All selling analytics query filters and analytics export request bodies use `startDate` and `endDate`. The operations
dashboard returns the same names in its `period` and `comparisonPeriod` objects.

Past Amount Due, A/R aging, and buyer aging intentionally return `isAvailable: false`, null/empty values, and an
`unavailableReason`; unavailable A/R returns a null total. The Liens schema has no authoritative receivable due date, so
deriving those values from service, submission, sale, or update dates would misstate A/R. Top buyers are grouped by the
persisted `BuyingOrgId` for non-terminal liens with a positive balance. Completed purchase amount is calculated separately
for the displayed buyers, so completed settled purchases remain included. Buyer Company Directory identity uses the most
recent accepted, period-relevant offer, ordered by response time, offer time, then offer ID; when no such scoped company
exists, the buyer organization ID is returned as the display fallback.

Seller ownership is canonical-first across the composite and existing dashboard/analytics reads: `SellingOrgId` must
match the authenticated organization when populated; legacy `OrgId` is consulted only when `SellingOrgId` is null.

Seller dashboard highest bids are aggregated in the database and, unless sorting by highest bid, are loaded only
for the requested page. Buyer dashboard lien, facility, and seller-contact lookups are batched; seller display
resolution uses bounded parallelism so multiple seller organizations do not serialize identity lookups.

### Selling receivables dashboard

`GET /api/liens/selling/analytics/receivables-dashboard` returns the composite mobile-dashboard dataset for the
authenticated tenant and seller organization. It requires SynqLien product access, sell mode, and
`SYNQ_LIENS.lien_sale:view_analytics`; callers cannot supply a tenant or seller organization identifier. Responses include
`Cache-Control: no-store`.

Optional query parameters:

- `asOfDate`: ISO `yyyy-MM-dd`; defaults to the current UTC date.
- `months`: number of monthly chart positions, defaults to `6`, allowed range `1` through `12`.
- `topBuyerLimit`: maximum buyers returned in both buyer lists, defaults to `5`, allowed range `1` through `20`.

The response contains summary amounts, five due-date aging buckets, mutually exclusive operational statuses, monthly
chart positions, top buyers, per-buyer aging, and data-quality counts. Outstanding balances are clamped at zero.
Payments received use only non-deleted `SettlementPaymentDetail` rows dated from the first day of the selected month
through `asOfDate`; settlement headers are not added to that metric. Buyer resolution prefers
`FundingCompanyCompanyId` and uses legacy `FundingCompanyId` only when no canonical reference exists. Missing or
out-of-scope buyer references are counted in `dataQuality.unassignedBuyerCount`.

`ReceivableDueDate` is nullable and is never inferred from purchase, service, settlement, or update dates. Positive
balances without a due date are excluded from the five aged buckets and reported through `unagedLienCount`,
`unagedBalance`, and `dataQuality.missingDueDateCount`. This release does not create historical balance snapshots, so
all summary metrics return `trendAvailable: false` with `trendPercent: null`; historical chart positions return
`dataAvailable: false`. Only the current month position is populated, and only when `asOfDate` is the current UTC date.

| `POST` | `/api/liens/selling/liens` | Creates a lien directly in `Pending` or `Internal`; it does not create a seller draft. |
| `GET` | `/api/liens/selling/liens/{lienId}` | Returns seller-scoped lien detail for the intake wizard, including funding-company contact person/email and case-manager/law-firm details when available. |
| `PUT` | `/api/liens/selling/liens/{lienId}/lien-information`, `/case-information`, `/medical-pricing`, `/documents` | Saves the seller wizard sections. `/case-information` accepts either legacy Contact IDs or Company Directory references: `fundingCompanyId` and `handlingLawFirmId` may identify active companies of the required type, `medicalProviderId` may identify an active Medical Provider company, `fundingCompanyContactId` must belong to the selected funding company, and `caseManagerId` must belong to the selected law firm with the Case Manager contact-person type. Company Directory references must belong to the authenticated tenant and seller organization. Canonical references are persisted in the dedicated company/contact-person columns and are returned by lien detail/readiness flows; legacy payloads remain supported. Medical-pricing rows and document references use collision-resistant task identifiers, including when required and supporting documents are saved together. Existing document IDs are verified against the Documents service and must reference the seller-owned lien or case. |
| `POST` | `/api/liens/selling/liens/{lienId}/prepare-sale` | Validates readiness and saves buyer, ask, visibility, and message selections without changing the lien from `Pending` or `Internal`. `buyerFundingCompanyId` and `buyerContactId` may be either a legacy funding-company/contact pair or an active Company Directory Funding Company/contact-person pair owned by the authenticated seller organization. The buyer organization is derived from the selected contact. |
| `POST` | `/api/liens/selling/liens/{lienId}/confirm-sale` | Confirms a prepared request, moves the lien from `Pending`/`Internal` (or legacy `PreparedForSale`) to `Offered` / `SubmittedForSale`, and sends buyer and seller `New Lien Offer` emails. Buyer access links preserve canonical Company Directory references when those IDs were selected while remaining compatible with legacy Contacts. |
| `GET` | `/api/liens/selling/liens/{lienId}/archived-status` | Returns `{ lienId, lienNumber, isArchived, sellerStatus, archivedAtUtc, archivedReason }` for the seller-scoped lien. |
| `POST` | `/api/liens/selling/liens/{lienId}/withdraw-sale`, `/archive`, `/restore`, `/buyer-access-links` | Withdraws a submitted lien, soft-archives an unsold lien, restores a soft-archived lien to Pending, or creates a time-limited buyer capability link. Archived liens retain their record, activity, archive timestamp, and archive reason until restored. Raw link tokens are returned only on first creation and are never persisted. |
| `GET` | `/api/liens/selling/bulk-import-template` | Downloads the canonical CSV template for a staged selling-lien bulk import. Its headers use the same names shown in Lien Details: `Lien Status`, `Listing Visibility`, `Purchase Date`, `Lien Notes`, `Medical Provider`, and `Target Ask Amount`. Required columns are marked with `*`. Attachments are uploaded separately and are not represented as CSV fields. |
| `POST` | `/api/liens/selling/bulk-imports` | Uploads a CSV, XLS, or XLSX selling-lien import using `multipart/form-data`. The import is staged tenant-scoped for subsequent validation and confirmation; it does not create liens directly. Confirmation rejects an import with `INVALID` rows until it is corrected and validated again; otherwise it creates rows independently and reports `PARTIAL` with failed-row reasons when an individual row cannot be persisted. Each valid row creates one lien with collision-resistant lien and servicing identifiers; rows with the same Case Code link to one existing seller case or create one shared case. Funding Company, Facility Name, and Medical Provider are matched case-insensitively to active records when there is exactly one match; otherwise their imported text is retained for display without a linked record. Medical Code & Description creates both Selling pricing and legacy medical-code records. Existing files using `Lien Status*`, `Seller Status`, `Notes`, `Medical Provider Name`, `Purchase Date*`, or `Purchase Amount*` remain accepted as legacy header aliases. |

### Selling company directory

Import [`LegalSynq Company and Contact Person API.postman_collection.json`](LegalSynq%20Company%20and%20Contact%20Person%20API.postman_collection.json) for the complete Company Directory request set, including CSV exports, custom contact-person types, company/contact reassignment, and saving the new references through Selling case information.

The Selling API includes a seller-organization-scoped company directory. Companies are always owned by the authenticated
tenant and seller organization; callers cannot assign that ownership through the request body. `linkedTenantId` is an
optional logical association with another LegalSynq tenant and is not a cross-service database foreign key. Company types
and their built-in contact-person types are deterministic reference data for Law Firm, Funding Company, Medical Provider,
and Medical Facility. Seller organizations may add custom contact-person types; custom types remain visible and assignable
only within the creating tenant and seller organization. Each company contact has one contact-person type from the
company's type-specific role list.

| Method                 | Path                                                                       | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| ---------------------- | -------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET`                  | `/api/liens/selling/lookups/company-types`                                 | Lists the four active company types.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `GET`                  | `/api/liens/selling/lookups/contact-person-types?companyTypeId={id}`       | Lists active built-in contact roles plus custom roles owned by the authenticated tenant and seller organization. Responses identify built-in rows with `isSystem: true`.                                                                                                                                                                                                                                                                                                                                                                                                |
| `POST`                 | `/api/liens/selling/lookups/contact-person-types`                          | Adds a seller-organization-scoped custom role for an active company type. Requires `companyTypeId`, machine-stable `code`, and display `name`; positive `sortOrder` is optional and otherwise follows the visible roles.                                                                                                                                                                                                                                                                                                                                                |
| `GET`, `POST`          | `/api/liens/selling/companies`                                             | Searches or creates companies for the authenticated tenant and seller organization. Search supports `search`, `companyTypeId`, `isActive`, `page`, and `pageSize` (maximum 200).                                                                                                                                                                                                                                                                                                                                                                                        |
| `GET`                  | `/api/liens/selling/companies/export`                                      | Downloads an uncapped CSV of scoped companies. Supports `search`, `companyTypeId`, and `isActive`; `isActive` defaults to `true`.                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `GET`, `PUT`, `DELETE` | `/api/liens/selling/companies/{companyId}`                                 | Reads, updates, or soft-deactivates a scoped company. Company type is immutable after creation.                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `GET`                  | `/api/liens/selling/company-details/{companyId}`                           | Returns the scoped company overview shown on the Company Details screen: company information, total cases, active cases (excluding `CaseSettled` and `Closed`), total original billing for active cases, and recent cases. Recent cases support `page` (default `1`) and `pageSize` (default `4`, maximum `100`). Canonical law-firm, funding-company, medical-provider, and medical-facility references determine case association.                                                                                                                                    |
| `POST`                 | `/api/liens/selling/companies/{companyId}/reassign`                        | Reassigns the source company's contacts and mutable canonical workflow references to an active scoped target company of the same company type. Body: `{ "targetCompanyId": "..." }`.                                                                                                                                                                                                                                                                                                                                                                                    |
| `PUT`                  | `/api/liens/selling/companies/{companyId}/reactivate`                      | Reactivates a company without changing its contact activation states.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `GET`, `POST`          | `/api/liens/selling/companies/{companyId}/contacts`                        | Lists or creates company contacts. New contacts require an active company and a role belonging to its company type.                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `GET`                  | `/api/liens/selling/contact-person`                                        | Lists contact persons across companies in the authenticated tenant and seller organization. All filters are optional: `search`, `companyTypeId`, `contactPersonTypeId`, `isActive` (default `true`), `page` (default `1`), and `pageSize` (default `20`, maximum `200`). Omit `contactPersonTypeId` or send it empty or as literal `null` to include all contact-person types. The legacy `filter` and `limit` aliases remain supported. Returns `items`, `page`, `limit`, `totalCount`, and `totalPages`, including company and company-type details for each contact. |
| `GET`                  | `/api/liens/selling/contacts/export`                                       | Downloads an uncapped CSV of contact persons across scoped companies. Supports `search`, `companyTypeId`, `contactPersonTypeId`, and `isActive`; omit `contactPersonTypeId` or send it empty or as literal `null` to include all contact-person types. Other nonempty values must be valid GUIDs or the request returns `400`. `isActive` defaults to `true`.                                                                                                                                                                                                           |
| `GET`                  | `/api/liens/selling/companies/{companyId}/contacts/export`                 | Downloads an uncapped CSV of contact persons for one scoped company. Supports `search`, `contactPersonTypeId`, and `isActive`; omit `contactPersonTypeId` or send it empty or as literal `null` to include all contact-person types. Other nonempty values must be valid GUIDs or the request returns `400`. `isActive` defaults to `true`.                                                                                                                                                                                                                             |
| `GET`, `PUT`, `DELETE` | `/api/liens/selling/companies/{companyId}/contacts/{contactId}`            | Reads, updates, or soft-deactivates a company contact.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `POST`                 | `/api/liens/selling/companies/{companyId}/contacts/{contactId}/reassign`   | Reassigns mutable canonical usage to an active scoped target contact whose company type and contact-person type (role) exactly match the source. Body: `{ "targetContactPersonId": "..." }`.                                                                                                                                                                                                                                                                                                                                                                            |
| `PUT`                  | `/api/liens/selling/companies/{companyId}/contacts/{contactId}/reactivate` | Reactivates a contact when its parent company is active.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |

All company-directory mutations require `Idempotency-Key`. Reads require `lien_sale:read`, creates require
`lien_sale:create`, and updates/deactivation/reactivation/reassignment require `lien_sale:update`. Reassignment retains
the source record for audit/history and returns transfer counts by referenced entity type; use the existing deactivation endpoint
separately when the source should no longer be selectable. Contact reassignment also synchronizes the paired canonical
company reference when the source contact was assigned with its source company. Immutable compatibility aliases remain
historical and are not rewritten. The directory intentionally remains
independent from the existing funding-company, law-firm, case-manager, and facility Selling lookups, whose identifiers are
still consumed by lien intake, imports, reports, notifications, and sale confirmation.

The additive compatibility schema stores namespace-, scope-, and workflow-specific aliases plus nullable canonical
sidecar references while legacy identifiers and `Case.Notes` remain authoritative. Rollout controls are independent and
default off: `SellingPartyCompatibility:BackfillEnabled`, `DualWriteEnabled`, `ShadowReadEnabled`, and
`CanonicalReadEnabled`; `BackfillBatchSize` defaults to 100. Enabling backfill creates deterministic, resumable
checkpoints and immutable preferred aliases for existing directory companies. Transient batches use fresh service scopes
and are bounded by `BackfillMaxRetries` (default 3) and `BackfillRetryDelayMilliseconds` (default 250). Do not enable canonical reads until each
Selling workflow has zero unexplained contract diffs and its dual-write/shadow-read coverage has been completed.

The company-directory, selling-party compatibility, and scoped contact-person-type migrations are restart-safe on MySQL. If a deployment stops after
MySQL auto-commits only part of a migration, the next Liens startup detects existing tables, columns, indexes,
constraints, and lookup seeds, creates only the missing objects, and lets EF record the migration normally. The same
guarded recovery also repairs environments where migration history was recorded before the schema completed, using a
database advisory lock so concurrent Liens instances do not race the DDL. Runtime and design-time Liens DbContexts enable
MySqlConnector user variables for this guarded DDL; callers do not need to append that option to
`ConnectionStrings:LiensDb` themselves.

Confirm-sale uses the persisted `AskAmount` as the offer price and leaves `SoldAtUtc` empty. The request only confirms
seller acceptance; notification delivery is mandatory and cannot be opted out through request payload. On every
confirmation, the service validates real buyer/seller contact data, creates a 30-day buyer response link and a separate
30-day seller-view link, then requests both buyer and seller emails through Notifications with idempotency keys. The
seller email uses matching branded copy with buyer/funding-company information and a `View Lien Details` link. The
buyer-facing seller name is resolved from the Identity user who confirmed/submitted the offer
(`SellingBuyerAccessLinks.CreatedByUserId` / confirm-sale acting user -> `idt_Users.FirstName` + `LastName`), scoped to
the seller organization when Identity validates membership. Seller
company is resolved from the selling organization (`sellerOrgId`) through Identity, with fallback only to non-law-firm
and non-case-manager contacts in that seller organization. Handling law firm and case manager remain case/asset details.
The public-link JSON and authenticated funding-company dashboard, offered-liens list, and detail views use the same
seller-user and seller-organization resolver, so the email CTA and logged-in views do not select different seller
information for the same offer. In buyer and seller notification Asset Overview sections, Contact Person, Email Address,
and Handling Law Firm all come from the selected standalone handling law-firm contact: `liens_Contacts.FirstName` +
`LastName`, `liens_Contacts.Email`, and `liens_Contacts.Organization` with `DisplayName` as the fallback for legacy
or incomplete firm records. Creating a standalone law firm without a separate organization value persists its display
name as the organization. The seller notification's Buyer Information section omits buyer phone number.
Supporting document names are pulled from existing legacy
lien/case document servicing metadata; both emails omit the document section when no real document names exist. The
email header uses the existing LegalSynq mark as an inline CID image attachment with HTML-rendered white/orange wordmark
text, and the section icons are also delivered as inline CID image attachments. No remote placeholder assets are
required.
Configure the buyer portal URL with `Liens:Selling:BuyerPortalBaseUrl` or the environment variable
`Liens__Selling__BuyerPortalBaseUrl`. If that value is absent, the API derives it from
`SYNQLIEN_COMMON_PORTAL_HOSTNAME`; `synqlien-demo.localhost` resolves to
`http://synqlien-demo.localhost:5000/selling/public` for the full `scripts/run-dev.sh` proxy. The value must be an
absolute portal URL and must match the active tenant-web browser origin. Use
`http://synqlien-demo.localhost:3000/selling/public` when running only `pnpm --dir apps/web dev`. Literal loopback hosts
such as `localhost` and `127.0.0.1` are rejected because outbound email recipients cannot open those links. Named
`.localhost` aliases such as `synqlien-demo.localhost` are allowed for local demo runs. If it contains `{token}` the
token is substituted, otherwise the token is appended as the final path segment.

The authenticated funding-company portal reads KPI, pending-offer, acquisition pipeline, and provider performance data
from `GET /api/liens/selling/buyer/dashboard`. Summary cards are range-scoped buyer totals: pending access links with
no buyer response, pending offered amount, accepted buyer responses as purchased liens, and accepted response amount as
capital deployed. Dashboard KPI trends compare current calendar month activity with the previous full calendar month and
return the previous-month range for the portal detail line on preset ranges. Dashboard date ranges filter by the offer
received timestamp for pending, accepted, and declined rows; custom ranges require both start and end dates and otherwise
return empty dashboard data. Dashboard preview lists are capped to five rows; provider performance is ordered by highest
offered-lien count. The same buyer scoping also drives
`GET /api/liens/selling/buyer/liens`. That endpoint projects buyer response access links into table rows scoped to the
authenticated buyer contact matched by email. Matching contacts must be active, belong to the tenant, and use the
`FundingCompany` or `LienHolder` contact type; access links are filtered by `BuyerContactId`. It supports
`status=Pending|Accepted|Declined`, free-text `search`, `page`, `pageSize`, `sort`, and
`direction` query parameters for the `/funding/offered-liens` page. Pending rows return `view`, `accept`, and `decline`
actions only while the underlying lien remains actionable by the public buyer-response rules. When a sibling access
link has already completed the lien workflow, the terminal lien state is projected instead of leaving an unanswered
link labeled as pending; accepted, declined, or otherwise non-actionable rows return `view` only. Row `detailHref`
values point to the authenticated tenant portal route
`/funding/offered-liens/{accessLinkId}`. The portal backs that route with
`GET /api/liens/selling/buyer/liens/{accessLinkId}`, which returns persisted seller/lien fields plus real servicing
documents, portal messages, and response activity for the funding company. Missing documents, messages, or activity are
returned as empty arrays for the frontend empty states. Detail documents include same-origin tenant-portal `viewUrl` and
`downloadUrl` BFF paths when a Documents-service id can be resolved; those paths call
`GET /api/liens/selling/buyer/liens/{accessLinkId}/documents/{documentId}/view` or `/download`, enforce the same buyer
scope, and redirect to a short-lived Documents access URL. The authenticated detail page posts messages through
`POST /api/liens/selling/buyer/liens/{accessLinkId}/messages` and records responses through
`POST /api/liens/selling/buyer/liens/{accessLinkId}/accept` or
`POST /api/liens/selling/buyer/liens/{accessLinkId}/decline`; these endpoints enforce the same buyer scoping and then
reuse the public-link workflows so the email link and logged-in funding portal share one message thread, response
status, activity, and notification behavior.

The temporary public portal endpoints are anonymous and token-scoped. `GET /api/liens/selling/public/{token}` returns
JSON from persisted lien, case, contact, access-link, response, and servicing document metadata only, including
`audience=buyer|seller`, handling law-firm organization, handling law-firm contact name, handling law-firm email, and
case manager when available, and sets `Referrer-Policy: no-referrer`. It does not render HTML; the tenant portal route
`/selling/public/{token}` in `apps/web`
fetches this JSON through the gateway and renders either the funding-company response page or the seller details page.
Both buyer-purpose and seller-purpose links can view and post public messages on the offer thread with
`POST /api/liens/selling/public/{token}/messages`; Liens derives the sender from the token purpose, stores the message,
and emails the other party with an idempotent message notification. Buyer-to-seller messages use the seller account
email resolved from Identity; seller-to-buyer replies use the activated or authenticated buyer account email, not
law-firm/contact email. Accept/decline outcome emails use the same account-recipient rule for the seller, and the
authenticated/activated buyer account email for the buyer when available. Because access-link raw tokens are only
returned on first creation and are not persisted, message notification emails omit a reply URL when the recipient's raw token is no longer available. Buyer-purpose links record
buyer responses with
`POST /api/liens/selling/public/{token}/accept` and `POST /api/liens/selling/public/{token}/decline`; accepting records
the current ask amount and moves the lien to `Status=Accepted` / `SellerStatus=Accepted`; declining records an optional
reason and moves the lien to `Status=Declined` / `SellerStatus=Declined`. `POST
/api/liens/selling/public/{token}/offers` is a compatibility alias for public accept. These public responses do not
finalize the sale, create a Bill of Sale, or mark the lien sold. The first accepted or declined response submits
outcome emails to both the buyer and seller through Notifications using idempotent recipient-specific keys; repeated
same-response posts return the recorded response and retry those idempotent notification submissions, so transient
notification failures can recover without duplicate emails. These outcome emails use HTML rendering and have status-only
subjects: `Lien Offer Accepted` or `Lien Offer Declined`. Liens submits these outcome emails with pre-rendered HTML and
without a notification template key, so template rendering cannot override the fixed subject or branded HTML. Seller-purpose
links are read-only and reject accept/decline/account-activation posts.
Liens sends these emails through `NotificationsService:BaseUrl` (also accepted through the legacy
`Services:NotificationsUrl` key). Because Notifications protects `POST /v1/notifications` with service JWT auth, Liens
must share the platform service-token signing key through `FLOW_SERVICE_TOKEN_SECRET` or `ServiceTokens:SigningKey`.
The local development appsettings and startup scripts provide the development value; deployed environments must provide
the production secret.
The public page's `Activate Free Account` CTA opens `/selling/public/{token}/activate`, which submits account
activation through the tenant-portal BFF to `POST /api/liens/selling/public/{token}/activate-account`. That endpoint
uses the token-scoped buyer organization/contact data to ask Identity to create or resolve a tenant-scoped
`LIEN_OWNER` organization for the source Liens buyer org, then create an active user with
`SYNQ_LIENS:SYNQLIEN_BUYER`. If the email already belongs to an Identity account, activation returns a `409`
error so the buyer can log in with the existing account instead. It does not accept or decline the lien and does
not finalize sale. Successful activation records the activated Identity user/email on the access link, so later public
page loads continue to show the login CTA even when the original buyer contact email was missing. The public
`GET /api/liens/selling/public/{token}` response also includes an `account` block for buyer-purpose links; when the
link has already activated an account or Identity reports `hasExistingAccount=true`, the tenant portal replaces
`Activate Free Account` with `Log In` and sends the buyer to
`/login?returnTo=%2Ffunding%2Fdashboard&reason=synqlien-buyer-activation&tenantId=<offer-tenant-id>`.
The `tenantId` query parameter keeps common-portal sign-in scoped to the tenant that issued the offer when the buyer email
belongs to multiple funding organizations.
When sending links through the tenant portal host, configure
`Liens__Selling__BuyerPortalBaseUrl=http://<portal-host>:<web-port>/selling/public` for local demo runs, or
`https://<portal-host>/selling/public` behind a real portal domain, so the public web route can render without a
`platform_session` cookie while fetching Liens data through the gateway. The confirm-sale email disables SendGrid click tracking for this
CTA so recipients see and open the LegalSynq portal URL directly.

Public buyer activation and buyer-facing seller display require the Liens service to reach Identity. Configure
`IdentityService:BaseUrl` (or the existing fallback `ExternalServices:Identity:BaseUrl`) and, outside local development,
set `IdentityService:ProvisioningToken` to match Identity's `TenantService:ProvisioningSecret`. If
`IdentityService:ProvisioningToken` is empty, Liens falls back to `TenantService:ProvisioningToken`, which is the token
used by the existing development and production startup scripts.

Local SynqLien demo portal example:

```bash
SYNQLIEN_COMMON_PORTAL_HOSTNAME=synqlien-demo.localhost
PORTAL_SYNQLIEN_SUBDOMAIN=synqlien-demo
Liens__Selling__BuyerPortalBaseUrl=http://synqlien-demo.localhost:5000/selling/public
# or, when only apps/web dev is running:
Liens__Selling__BuyerPortalBaseUrl=http://synqlien-demo.localhost:3000/selling/public
```

## Assistant Tool Endpoints

SynqLien exposes read-only assistant tool endpoints for Xenia under `/api/assistant-tools`. These endpoints require a
bearer token, SynqLien product access, and the matching read permission; Xenia forwards the caller's token so Liens
remains the authorization boundary. Lien lookups/searches accept broad lien read or scoped seller/buyer/holder read
permissions and apply visibility filters before returning results.
The tenant portal's `GET /api/liens/cases/dashboard/piechart` lien totals use the same visibility policy as the
assistant queue summary, so organization-scoped users see consistent Total Liens values on both surfaces. Broad lien
read, tenant-admin, and platform-admin access continue to produce tenant-wide totals.

| Method | Path                                                         | Permission                                  | Description                                                                                                                                                                                                                                                                           |
| ------ | ------------------------------------------------------------ | ------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET`  | `/api/assistant-tools/liens/search`                          | Lien read, read-own, browse, or read-held   | Search visible liens by subject, case number, status/status group, type, and created date window                                                                                                                                                                                      |
| `GET`  | `/api/assistant-tools/liens/queue-summary`                   | Lien read, read-own, browse, or read-held   | Return visible lien queue totals, status counts, KPI windows, and recent liens                                                                                                                                                                                                        |
| `GET`  | `/api/assistant-tools/liens/{id}`                            | Lien read, read-own, browse, or read-held   | Lookup one visible lien by id                                                                                                                                                                                                                                                         |
| `GET`  | `/api/assistant-tools/liens/by-number/{lienNumber}`          | Lien read, read-own, browse, or read-held   | Lookup one visible lien by lien number                                                                                                                                                                                                                                                |
| `GET`  | `/api/assistant-tools/cases/search`                          | `SYNQ_LIENS.case:read`                      | Search cases by client, case number, law firm, case manager, type, accident type, state, status, and opened date window. Law-firm names are resolved tenant-locally and filtered before pagination so `totalCount` reflects every matching case rather than only the returned sample. |
| `GET`  | `/api/assistant-tools/cases/{id}`                            | `SYNQ_LIENS.case:read`                      | Lookup one case by id with linked liens and client/case metadata                                                                                                                                                                                                                      |
| `GET`  | `/api/assistant-tools/cases/by-number/{caseNumber}`          | `SYNQ_LIENS.case:read`                      | Lookup one case by case number with linked liens and client/case metadata                                                                                                                                                                                                             |
| `GET`  | `/api/assistant-tools/cases/{id}/insights`                   | `SYNQ_LIENS.case:read`                      | Return a case snapshot with linked liens, financial totals, documents, notes, servicing, tasks, activity, capability flags, and optional Excel-ready sheets                                                                                                                           |
| `GET`  | `/api/assistant-tools/cases/by-number/{caseNumber}/insights` | `SYNQ_LIENS.case:read`                      | Return the same case snapshot by case number                                                                                                                                                                                                                                          |
| `GET`  | `/api/assistant-tools/tasks/search`                          | `SYNQ_LIENS.task:read`                      | Search post-cutover SynqLien tasks by assignment, case, lien, status/status group, priority, due date window, overdue, and due today                                                                                                                                                  |
| `GET`  | `/api/assistant-tools/servicing/search`                      | `SYNQ_LIENS.lien:service`                   | Search servicing items by case, lien, assignee, status/status group, priority, due date window, and overdue                                                                                                                                                                           |
| `GET`  | `/api/assistant-tools/reports/summary`                       | `SYNQ_LIENS.case:read` plus lien visibility | Return read-only case/lien report summaries including opened cases, active cases by manager/law firm, closed liens, and recent records                                                                                                                                                |

Assistant search, summary, insight, task, servicing, and report endpoints support `datePreset` values:
`today`, `yesterday`, `this_week`, `last_week`, `this_month`, `last_month`, `last_30_days`, `last_60_days`,
`last_90_days`, and `life_to_date`. Explicit `*From`/`*To` query parameters override presets. Document endpoints
currently expose uploaded document metadata from servicing items; they do not expose file bytes or OCR text for
document-content summarization. Case insights can include workbook-style sheet data with `includeExport=true`, but the
endpoint does not write an `.xlsx` file itself.

## Product Roles

| Role              | Access                                      |
| ----------------- | ------------------------------------------- |
| `SYNQLIEN_SELLER` | Create, offer, withdraw liens               |
| `SYNQLIEN_BUYER`  | Browse marketplace, submit offers, purchase |
| `SYNQLIEN_HOLDER` | View held portfolio                         |

## Database

`LiensDb` (MySQL).

## Legacy SL-CORE core import

The tenant-scoped, dry-run-first legacy importer lives in
[`scripts/LegacyLiensImport`](../../scripts/LegacyLiensImport/). It imports only
approved core cases, medical-lien headers, and case notes from an isolated
`SL-CORE` staging database. It requires explicit tenant, organization,
migration-user, and legacy-program parameters plus an Identity-signed mapping
manifest for `--apply` that binds the approved dump fingerprint; it does not
run the dump or infer tenant ownership. A controlled staging restore receipt
must bind the queried database to that same fingerprint. See its README for
preflight, apply, collision-policy, certificate-store trust, mapping-evidence,
source-fingerprint, and restore-provenance requirements.

For the currently approved tenant/org pair, the importer folder also contains
a guarded MySQL-only one-time runner. It must be executed only against a
controlled staging restore on the same MySQL server as LiensDb; see the
importer README for its trusted approval, source-receipt, dry-run, and
single-use apply requirements.

The complete SQL procedure requires migration
`20260731000001_AddLienPurchaseAndSettlementDates`. It preserves
`LM_PURCHASE_DATE` on the lien, preserves settlement amount/date rows for the
date-filtered Cash Received metric, and excludes source rows marked deleted (`CASE_IS_DELETED
= 'Y'`, `LM_IS_DELETED = 'Y'`, or `SLSPD_IS_DELETED = 'Y'`). Medical-code
amounts and servicing rows are imported only when `LMC_STATUS = 'A'`, matching
the legacy dashboard calculations. Cash Deployed includes only liens with a
persisted `PurchaseDate` and sums their imported active legacy medical-code
purchase amounts without a lien-level fallback. Cash Received sums only
non-deleted settlement rows with a persisted `SettlementDate`. When the
dashboard omits a range, the lien, medical-facility, Cash Deployed, and Cash
Received reports default through the previous Pacific calendar day, matching
the legacy portal's completed-day reporting window. Total Case and Law Firm
Case reports include all visible cases when no range is supplied; explicit
date ranges remain inclusive and use linked lien purchase dates.

## Workflow Engine (Flow)

Task management, workflow stages, task templates, and auto-generation rules are handled by the Flow service (`apps/services/flow/`) at port 5015. The Liens service calls Flow for task lifecycle operations.

## External Integrations

- **Documents service** — lien/case document storage and access tokens
- **Audit service** — lien lifecycle events published
- **Notifications service** — offer and purchase notifications
