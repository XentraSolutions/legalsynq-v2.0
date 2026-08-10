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

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/liens` | List liens (my-liens / marketplace) |
| `POST` | `/api/liens` | Create lien |
| `GET` | `/api/liens/{id}` | Lien detail |
| `POST` | `/api/liens/{id}/offer` | Submit offer (buyer) |
| `POST` | `/api/liens/{id}/accept-offer` | Accept offer (seller) |
| `POST` | `/api/liens/{id}/purchase` | Direct purchase |
| `GET` | `/api/liens/portfolio` | Buyer/holder portfolio |
| `GET` | `/api/liens/cases` | Case list |
| `GET` | `/api/liens/cases/{id}` | Case detail |
| `GET` | `/api/liens/cases/dashboard/task-summary` | Legacy-compatible, assignee-scoped task dashboard. Returns the `isSuccess`/`message`/`data` envelope with total, upcoming, in-progress, in-review, and completed counts plus the task list. |
| `POST` | `/api/liens/cases/task/create`, `/api/liens/cases/tasks/create` | Creates a legacy case task. Priority accepts `High`, `Medium`, and `Low` case-insensitively; `Medium` maps to the servicing-domain `Normal` value while remaining `Medium` in legacy responses. |
| `POST`, `PATCH` | `/api/liens/cases/task/update` | Updates a legacy case task. Both methods are supported for compatibility with deployed clients. Status accepts legacy IDs/names and current UI codes such as `UPCOMING`, `INPROGRESS`, `INREVIEW`, `COMPLETED`, and `CANCELLED`; the compatibility value is preserved while the backing servicing item receives a valid canonical status. |
| `DELETE` | `/api/liens/cases/delete/{id}` | Legacy case deletion; blocks when a linked lien is active, and detaches terminal/rejected liens before removing the case |
| `POST` | `/api/liens/cases/generate-csv` | Exports cases as Base64-encoded CSV using canonical case fields plus raw migrated metadata and contact/audit enrichment for legacy-only columns; its accident-type and case-manager filters use canonical IDs with legacy metadata fallback |
| `GET` | `/service/liens/settlement/payment-details/{caseId}` | Returns complete legacy payment details, including check/reference number, payment method/payor, note, settlement type/status IDs and display names, and net profit from current or migrated payment metadata. Canonical settled liens are displayed as `Closed`, and legacy snake-case settlement type codes are returned as human-readable names while their original IDs are preserved. |
| `POST` | `/api/liens/reports/diy/export` | Export a DIY report as Base64-encoded CSV in the legacy `data` export envelope |
| `POST` | `/api/liens/cases/dashboard/total-lien-report-export/v3` | Returns all legacy-eligible liens with full-result status and billing/purchase summaries; paged JSON requests calculate summaries from compact database projections and enrich only the requested page, while CSV still loads every matching export row; the legacy V3 request has no date filter |
| `POST` | `/api/liens/cases/dashboard/total-case-report-export/v3` | Returns all legacy-eligible cases with full-result status counts; paged, unfiltered JSON requests aggregate statuses in the database and enrich only the requested page; the legacy V3 request has no date filter |
| `POST` | `/api/liens/cases/dashboard/lawfirm-case-report-export/v3` | Returns case/law-firm allocation and filters cases by the purchase date of any linked lien |
| `POST` | `/api/liens/cases/dashboard/medical-provider-report-export/v3` | Returns lien/facility allocation filtered by lien purchase date |
| `POST` | `/api/liens/cases/dashboard/deployed` | Sums lien purchase amounts with a persisted `PurchaseDate`; undated liens are excluded |
| `POST` | `/api/liens/cases/dashboard/cash-received` | Sums non-deleted lien settlement amounts with a persisted `SettlementDate`; undated settlements are excluded |

All four V3 report endpoints return paginated rows plus full-result summaries. When paging is
missing or invalid, JSON requests default to `page: 1` and return all matching rows; positive
`page` and `limit` values are honored for the returned `items` array.
Set `isCsv: true` or `isCsv: "yes"` for an uncapped Base64-encoded CSV export.
Dashboard pie-chart and deployed/cash-received metrics use database grouping and aggregation instead of
materializing every matching case, lien, or settlement. Report contact enrichment is restricted to referenced
contacts, organization law firms, providers, and facilities rather than loading the tenant-wide contact table.
The tenant dashboard requests one report row alongside the full-result summaries, then loads a selected
report's detailed breakdown in server-paginated pages of 10 rows. This keeps the initial four V3 JSON responses
bounded while preserving uncapped CSV exports and access to every report page.

DIY reports treat the legacy UI sentinel `isBulk: "N"` as no bulk filter, matching the legacy report SQL. Explicit `Y`/`Yes` selects bulk liens, while canonical `No`/`False`/`0` selects non-bulk and unset liens. Legacy relationship filters for law firm, attorney, funding company, medical facility, case manager, and medical provider are applied before pagination and summary calculation. Lien-status filter values may be either status codes or IDs from the lien-status lookup category.

The DIY `ALL` status view includes every non-deleted lifecycle state, including rejected and cancelled liens; `CLOSED` includes settled liens and `REJECTED` includes declined, withdrawn, and cancelled liens. Report previews honor `page` and `limit`, while `/api/liens/reports/diy/export` exports every row that matches the filters.

DIY report billing and purchase columns aggregate `billingAmount` and `purchaseAmount` from linked legacy medical-code records, falling back to lien-level amounts when none exist. For LIENS compatibility responses, `summaryTotals.totalBillingAmt` retains the legacy card behavior and contains outstanding billing (`gross billing - returned`); `summaryTotals.grossBillingAmt` exposes gross billing, and `summaryTotals.totalAmtToSettle` contains the same outstanding value. Settlement, reduction, returned-amount, gross-profit, and ROI fields use imported legacy settlement metadata when it is available, matching the legacy DIY report formulas.

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
Other settlement values do not change the lien. In the legacy payment-details response, a zero saved payment
amount uses the linked amount-to-settle as `checkAmount` instead of displaying
`0.00`. New payments that omit `paymentNumber` receive the next positive case payment number.
Historical zero-number rows receive deterministic non-zero display numbers. The payment-details
`amountToSettle` uses the recorded payment allocation before falling back to a linked settlement or
the lien's current balance, so closing a lien does not replace its payment-time amount with zero.

List filters accept both canonical persisted statuses and the legacy/UI lifecycle groups: `Open` expands to all active lien states, `Closed` expands to `Settled`, and `Rejected` expands to `Declined`, `Withdrawn`, and `Cancelled`. Historical rows literally persisted as `Rejected` remain hidden by default. Status/date-only lien-list filters are counted and paged in the database before per-lien detail and servicing enrichment, so broad status selections do not enrich the entire matching result set. The V3 case filter accepts comma-separated status, law-firm, case-manager, and accident-type selections. Case status filtering uses the saved legacy status label to distinguish `New`, `Processing`, and `Pre-Demand` cases that share the canonical `PreDemand` state, and to distinguish `Litigation` from `Negotiations` cases that share `InNegotiation`. The complete SL-CORE import preserves those labels, and the guarded relationship backfill repairs them for already-imported cases that have not since changed status. Law-firm values match the contact ID saved in case metadata and continue to accept legacy organization IDs.

## Selling Workflow

Seller-mode endpoints live under `/api/liens/selling` and require SynqLien product access plus sell mode. The Selling V2
lien-first lifecycle is `Pending`/`Internal` → `SubmittedForSale` → `Sold`; seller draft is not exposed. `PreparedForSale`
remains accepted only as a legacy transition state for records created by earlier deployments.
Intake writes are permitted only while the lien is `Pending` or `Internal`. State-changing V2 routes require an
`Idempotency-Key`; a retry with the same payload replays its stored response, while reusing the key with a different payload
returns `409 Conflict`.

Import [`LegalSynq Selling V2 API.postman_collection.json`](LegalSynq%20Selling%20V2%20API.postman_collection.json) into
Postman, set the collection variables for the appropriate seller or buyer token, and use a fresh `idempotencyKey` for each
new mutation (reuse it only to retry that exact request).

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/liens/selling/dashboard?tab=pending\|internal\|sold\|all` | Returns seller-scoped portfolio totals, tab counts, and a paginated lien table. Summary amounts aggregate the displayed billing amounts for the filtered Pending, Internal, and Sold lists; Total Portfolio Value is their sum and excludes statuses outside those lists. Supports search, funding company, law firm, case manager, facility, initial-service-date, and sort filters. Accepted liens are categorized and displayed as Sold. |
| `GET` | `/api/liens/selling/liens?tab=pending\|internal\|sold\|all` | Returns the same seller-scoped, filtered, paginated lien rows without dashboard totals. Accepted liens remain searchable in the Sold tab. |

Seller dashboard highest bids are aggregated in the database and, unless sorting by highest bid, are loaded only
for the requested page. Buyer dashboard lien, facility, and seller-contact lookups are batched; seller display
resolution uses bounded parallelism so multiple seller organizations do not serialize identity lookups.
| `POST` | `/api/liens/selling/liens` | Creates a lien directly in `Pending` or `Internal`; it does not create a seller draft. |
| `GET` | `/api/liens/selling/liens/{lienId}` | Returns seller-scoped lien detail for the intake wizard, including funding-company contact person/email and case-manager/law-firm details when available. |
| `PUT` | `/api/liens/selling/liens/{lienId}/lien-information`, `/case-information`, `/medical-pricing`, `/documents` | Saves the seller wizard sections. Medical-pricing rows and document references use collision-resistant task identifiers, including when required and supporting documents are saved together. Existing document IDs are verified against the Documents service and must reference the seller-owned lien or case. |
| `POST` | `/api/liens/selling/liens/{lienId}/prepare-sale` | Validates readiness and saves buyer, ask, visibility, and message selections without changing the lien from `Pending` or `Internal`. When a buyer contact is supplied, its buyer organization is derived from that contact rather than a separate funding-company selection. `confirm-sale` still requires a valid buyer contact to issue the buyer access link and notification. |
| `POST` | `/api/liens/selling/liens/{lienId}/confirm-sale` | Confirms a prepared request, moves the lien from `Pending`/`Internal` (or legacy `PreparedForSale`) to `Offered` / `SubmittedForSale`, and sends buyer and seller `New Lien Offer` emails. |
| `POST` | `/api/liens/selling/liens/{lienId}/withdraw-sale`, `/archive`, `/buyer-access-links` | Withdraws a submitted lien, archives an unsold lien, or creates a time-limited buyer capability link. Raw link tokens are returned only on first creation and are never persisted. |
| `GET` | `/api/liens/selling/bulk-import-template` | Downloads the current CSV template for a staged selling-lien bulk import. |
| `POST` | `/api/liens/selling/bulk-imports` | Uploads a CSV, XLS, or XLSX selling-lien import using `multipart/form-data`. The import is staged tenant-scoped for subsequent validation and confirmation; it does not create liens directly. Confirmation rejects an import with `INVALID` rows until it is corrected and validated again; otherwise it creates rows independently and reports `PARTIAL` with failed-row reasons when an individual row cannot be persisted. Each valid row creates one lien with collision-resistant lien and servicing identifiers; rows with the same Case Code link to one existing seller case or create one shared case. Funding Company, Facility Name, and Medical Provider Name are matched case-insensitively to active records when there is exactly one match; otherwise their imported text is retained without a linked record. Medical Code & Description creates both Selling pricing and legacy medical-code records. |

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
actions only while the underlying lien remains actionable by the public buyer-response rules; accepted, declined, or
otherwise non-actionable rows return `view` only. Row `detailHref` values point to the authenticated tenant portal route
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

| Method | Path | Permission | Description |
|---|---|---|---|
| `GET` | `/api/assistant-tools/liens/search` | Lien read, read-own, browse, or read-held | Search visible liens by subject, case number, status/status group, type, and created date window |
| `GET` | `/api/assistant-tools/liens/queue-summary` | Lien read, read-own, browse, or read-held | Return visible lien queue totals, status counts, KPI windows, and recent liens |
| `GET` | `/api/assistant-tools/liens/{id}` | Lien read, read-own, browse, or read-held | Lookup one visible lien by id |
| `GET` | `/api/assistant-tools/liens/by-number/{lienNumber}` | Lien read, read-own, browse, or read-held | Lookup one visible lien by lien number |
| `GET` | `/api/assistant-tools/cases/search` | `SYNQ_LIENS.case:read` | Search cases by client, case number, law firm, case manager, type, accident type, state, status, and opened date window |
| `GET` | `/api/assistant-tools/cases/{id}` | `SYNQ_LIENS.case:read` | Lookup one case by id with linked liens and client/case metadata |
| `GET` | `/api/assistant-tools/cases/by-number/{caseNumber}` | `SYNQ_LIENS.case:read` | Lookup one case by case number with linked liens and client/case metadata |
| `GET` | `/api/assistant-tools/cases/{id}/insights` | `SYNQ_LIENS.case:read` | Return a case snapshot with linked liens, financial totals, documents, notes, servicing, tasks, activity, capability flags, and optional Excel-ready sheets |
| `GET` | `/api/assistant-tools/cases/by-number/{caseNumber}/insights` | `SYNQ_LIENS.case:read` | Return the same case snapshot by case number |
| `GET` | `/api/assistant-tools/tasks/search` | `SYNQ_LIENS.task:read` | Search post-cutover SynqLien tasks by assignment, case, lien, status/status group, priority, due date window, overdue, and due today |
| `GET` | `/api/assistant-tools/servicing/search` | `SYNQ_LIENS.lien:service` | Search servicing items by case, lien, assignee, status/status group, priority, due date window, and overdue |
| `GET` | `/api/assistant-tools/reports/summary` | `SYNQ_LIENS.case:read` plus lien visibility | Return read-only case/lien report summaries including opened cases, active cases by manager/law firm, closed liens, and recent records |

Assistant search, summary, insight, task, servicing, and report endpoints support `datePreset` values:
`today`, `yesterday`, `this_week`, `last_week`, `this_month`, `last_month`, `last_30_days`, `last_60_days`,
`last_90_days`, and `life_to_date`. Explicit `*From`/`*To` query parameters override presets. Document endpoints
currently expose uploaded document metadata from servicing items; they do not expose file bytes or OCR text for
document-content summarization. Case insights can include workbook-style sheet data with `includeExport=true`, but the
endpoint does not write an `.xlsx` file itself.

## Product Roles

| Role | Access |
|---|---|
| `SYNQLIEN_SELLER` | Create, offer, withdraw liens |
| `SYNQLIEN_BUYER` | Browse marketplace, submit offers, purchase |
| `SYNQLIEN_HOLDER` | View held portfolio |

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
Cash Received metric, and excludes source rows marked deleted (`CASE_IS_DELETED
= 'Y'`, `LM_IS_DELETED = 'Y'`, or `SLSPD_IS_DELETED = 'Y'`). Medical-code
amounts and servicing rows are imported only when `LMC_STATUS = 'A'`, matching
the legacy dashboard calculations. Cash Received excludes settlement headers
whose persisted `SettlementDate` is empty, whether or not a date range is
supplied.

## Workflow Engine (Flow)

Task management, workflow stages, task templates, and auto-generation rules are handled by the Flow service (`apps/services/flow/`) at port 5015. The Liens service calls Flow for task lifecycle operations.

## External Integrations

- **Documents service** — lien/case document storage and access tokens
- **Audit service** — lien lifecycle events published
- **Notifications service** — offer and purchase notifications
