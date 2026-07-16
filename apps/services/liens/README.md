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
| `GET` | `/api/assistant-tools/cases/search` | `SYNQ_LIENS.case:read` | Search cases by client, case number, and status |
| `GET` | `/api/assistant-tools/cases/{id}` | `SYNQ_LIENS.case:read` | Lookup one case by id with linked liens |
| `GET` | `/api/assistant-tools/cases/by-number/{caseNumber}` | `SYNQ_LIENS.case:read` | Lookup one case by case number with linked liens |

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
