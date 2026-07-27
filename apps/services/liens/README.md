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
| `DELETE` | `/api/liens/cases/delete/{id}` | Legacy case deletion; blocks when a linked lien is active, and detaches terminal/rejected liens before removing the case |
| `POST` | `/api/liens/reports/diy/export` | Export a DIY report as Base64-encoded CSV in the legacy `data` export envelope |
| `POST` | `/api/liens/cases/dashboard/*-report-export/v3` | Returns the paginated JSON report by default; include `isCsv: true` or `isCsv: "yes"` for a Base64-encoded CSV export envelope with the report's four designated columns |

DIY reports treat `isBulk: "N"` as non-bulk for legacy `N`, canonical `No`, and unset lien values, so a newly created ordinary lien is included in its report.

DIY report billing and purchase columns aggregate `billingAmount` and `purchaseAmount` from linked legacy medical-code records, falling back to lien-level amounts when none exist.

## Selling Workflow

Seller-mode endpoints live under `/api/liens/selling` and require SynqLien product access plus sell mode. The lien-first
confirm-sale route is:

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/liens/selling/liens/{lienId}/confirm-sale` | Confirms a prepared selling lien, moves it to `Offered` / `SubmittedForSale`, and optionally sends buyer and seller `New Lien Offer` emails |

Confirm-sale uses the persisted `AskAmount` as the offer price and leaves `SoldAtUtc` empty. When
`sendBuyerNotification=true`, the service validates real buyer/seller contact data, creates a 30-day buyer response
link and a separate 30-day seller-view link, then sends the buyer email through Notifications with an idempotency
key. After the buyer email is submitted, the seller receives a matching branded email with buyer/funding-company
information and a `View Lien Details` link. Supporting document names are pulled from existing legacy
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

The temporary public portal endpoints are anonymous and token-scoped. `GET /api/liens/selling/public/{token}` returns
JSON from persisted lien, case, contact, access-link, response, and servicing document metadata only, including
`audience=buyer|seller`. It does not render HTML; the tenant portal route `/selling/public/{token}` in `apps/web`
fetches this JSON through the gateway and renders either the funding-company response page or the seller details page.
Both buyer-purpose and seller-purpose links can view and post public messages on the offer thread with
`POST /api/liens/selling/public/{token}/messages`; Liens derives the sender from the token purpose, stores the message,
and emails the other party's public link with an idempotent message notification. Buyer-purpose links record buyer responses with
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
not finalize sale. The public `GET /api/liens/selling/public/{token}` response also includes an `account` block for
buyer-purpose links; when Identity reports `hasExistingAccount=true`, the tenant portal replaces `Activate Free Account`
with `Log In` and sends the buyer to `/login?returnTo=%2Ffunding%2Foffered-liens&reason=synqlien-buyer-activation`.
When sending links through the tenant portal host, configure
`Liens__Selling__BuyerPortalBaseUrl=http://<portal-host>:<web-port>/selling/public` for local demo runs, or
`https://<portal-host>/selling/public` behind a real portal domain, so the public web route can render without a
`platform_session` cookie while fetching Liens data through the gateway. The confirm-sale email disables SendGrid click tracking for this
CTA so recipients see and open the LegalSynq portal URL directly.

Public buyer activation requires the Liens service to reach Identity. Configure `IdentityService:BaseUrl` (or the
existing fallback `ExternalServices:Identity:BaseUrl`) and, outside local development, set
`IdentityService:ProvisioningToken` to match Identity's `TenantService:ProvisioningSecret`.

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

## Workflow Engine (Flow)

Task management, workflow stages, task templates, and auto-generation rules are handled by the Flow service (`apps/services/flow/`) at port 5015. The Liens service calls Flow for task lifecycle operations.

## External Integrations

- **Documents service** — lien/case document storage and access tokens
- **Audit service** — lien lifecycle events published
- **Notifications service** — offer and purchase notifications
