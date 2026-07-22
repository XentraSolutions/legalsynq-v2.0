# Liens Service (SynqLien)

Medical lien lifecycle management — creation, marketplace listing, offer/purchase workflow, and servicing.

**Port:** 5009 (API) + Flow service at 5015 (workflow engine)

## Responsibilities

- Lien CRUD (Draft → Offered → Sold / Withdrawn)
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

## Selling Workflow

Seller-mode endpoints live under `/api/liens/selling` and require SynqLien product access plus sell mode. The lien-first
confirm-sale route is:

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/liens/selling/liens/{lienId}/confirm-sale` | Confirms a prepared selling lien, moves it to `Offered` / `SubmittedForSale`, and optionally sends the buyer `New Lien Offer` email |

Confirm-sale uses the persisted `AskAmount` as the offer price and leaves `SoldAtUtc` empty. When
`sendBuyerNotification=true`, the service validates real buyer/seller contact data, creates a 30-day buyer access link,
and sends the email through Notifications with an idempotency key. Supporting document names are pulled from existing
legacy lien/case document servicing metadata; the email omits the document section when no real document names exist.
Configure the buyer portal URL with `Liens:Selling:BuyerPortalBaseUrl` or the environment variable
`Liens__Selling__BuyerPortalBaseUrl`. The value must be an absolute portal URL; if it contains `{token}` the token is
substituted, otherwise the token is appended as the final path segment.

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
