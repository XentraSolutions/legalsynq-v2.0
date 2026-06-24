# COM-B07 — Commerce Admin UI (Standalone) — Implementation Report

> **Status:** Complete. The standalone internal admin UI for the
> Commerce service has been built, builds clean, lints clean, and
> typechecks clean.
>
> **Companion backend report:** `analysis/COM-B07-report.md`.

## 1. Summary

This report covers the **frontend** half of COM-B07: a standalone,
internal-only Next.js 14 (App Router) admin UI for the Commerce
service. It lives at `services/Commerce/admin/` and is intentionally
**outside** the project's pnpm workspace so it can be lifted/dropped
into any internal-tools host without coupling to the broader Replit
artifact tooling.

Hard scope guarantees, all upheld:

* No authentication implemented.
* No LegalSynq integration.
* No entitlement enforcement (the UI is read + operate only).
* No business logic in the UI — every action is a thin call to a
  Commerce API endpoint.
* No embedding into other portals.

The UI is consumer of the Commerce APIs that COM-B07 (backend)
shipped: dashboard summaries, catalog reads, billing/subscription/
invoice/payment lists and details, provider-event reprocessing, and
account-standing evaluation.

## 2. Pages Implemented

App Router routes under `services/Commerce/admin/app/`:

| Route | Purpose | Backend used |
| --- | --- | --- |
| `/` | Redirect to `/dashboard`. | — |
| `/dashboard` | KPI cards (billing accounts, active/trialing subs, open/paid invoices, failed payments, provider event failures, account-standing breakdown) + revenue summary. | `/api/commerce/admin/dashboard/summary`, `/api/commerce/admin/dashboard/revenue-summary` |
| `/catalog` | Read-only browser of products / plans / prices / addons / bundles. | `/api/commerce/catalog/products`, `/plans`, `/prices`, `/addons`, `/bundles` |
| `/billing-accounts` | List with account number, name, status, currency. | `/api/commerce/billing-accounts` |
| `/billing-accounts/[id]` | Account detail with subs, invoices, payments, account standing. | per-account variants of the above |
| `/subscriptions` | List with subscription number, status, billing account, current period. | `/api/commerce/subscriptions` |
| `/subscriptions/[id]` | Detail with items and change history. | per-id sub endpoints |
| `/invoices` | List with invoice number, status, total, due date. | `/api/commerce/invoices?take=100` |
| `/invoices/[id]` | Detail with line items and payments. | per-id invoice endpoints |
| `/payments` | List of payments with amount/status/provider/timestamps. | `/api/commerce/payments?take=100` |
| `/provider-events` | List of provider events (id, type, status, created). | per provider-event endpoints |
| `/provider-events/[id]` | Payload (formatted JSON), error message, status, **Reprocess** button (visible only for Failed/Ignored/Received). | `POST /api/commerce/payments/event-logs/{id}/reprocess` |
| `/account-standing` | List of billing accounts with status + reason; **Evaluate** form. | `POST /api/commerce/billing-accounts/{id}/account-standing/evaluate` |

The build artefact summary (Next.js 14.2.16 production build):

```
Route (app)                              Size     First Load JS
┌ ○ /                                    143 B          87.3 kB
├ ○ /_not-found                          872 B          88.1 kB
├ ƒ /account-standing                    3.57 kB        90.8 kB
├ ƒ /billing-accounts                    191 B          94.1 kB
├ ƒ /billing-accounts/[id]               191 B          94.1 kB
├ ƒ /catalog                             143 B          87.3 kB
├ ƒ /dashboard                           143 B          87.3 kB
├ ƒ /invoices                            191 B          94.1 kB
├ ƒ /invoices/[id]                       191 B          94.1 kB
├ ƒ /payments                            191 B          94.1 kB
├ ƒ /provider-events                     191 B          94.1 kB
├ ƒ /provider-events/[id]                3.08 kB          97 kB
├ ƒ /subscriptions                       191 B          94.1 kB
└ ƒ /subscriptions/[id]                  191 B          94.1 kB
+ First Load JS shared by all            87.2 kB
```

## 3. API Endpoints Used

All endpoints belong to the existing Commerce service. No new backend
endpoints were introduced for the UI.

**Reads (GET):**

* `GET /api/commerce/admin/dashboard/summary`
* `GET /api/commerce/admin/dashboard/revenue-summary`
* `GET /api/commerce/catalog/products`
* `GET /api/commerce/catalog/plans`
* `GET /api/commerce/catalog/prices`
* `GET /api/commerce/catalog/addons`
* `GET /api/commerce/catalog/bundles`
* `GET /api/commerce/billing-accounts`
* `GET /api/commerce/billing-accounts/{id}` (and nested: subscriptions,
  invoices, payments, account-standing)
* `GET /api/commerce/subscriptions`
* `GET /api/commerce/subscriptions/{id}`
* `GET /api/commerce/invoices?take=100`
* `GET /api/commerce/invoices/{id}`
* `GET /api/commerce/payments?take=100`
* `GET /api/commerce/payments/event-logs` (provider events)
* `GET /api/commerce/payments/event-logs/{id}`

**Operate (POST):**

* `POST /api/commerce/payments/event-logs/{id}/reprocess` — Reprocess
  provider event (button shown only when status ∈ {Failed, Ignored,
  Received}).
* `POST /api/commerce/billing-accounts/{billingAccountId}/account-standing/evaluate`
  — Force re-evaluation of a billing account's standing.

The user spec referred to these two endpoints under their conceptual
names (`/payments/provider-events/{id}/reprocess` and
`/account-standing/billing-accounts/{id}/evaluate`); the UI uses the
**actual backend route shapes** above (`payments/event-logs/...` and
`billing-accounts/{id}/account-standing/evaluate`).

## 4. Components Created

Shared, framework-agnostic UI primitives under
`services/Commerce/admin/components/`:

| File | Purpose |
| --- | --- |
| `SideNav.tsx` | Left sidebar (8 nav items with Lucide icons, active-route highlighting). |
| `PageHeader.tsx` | Page title + optional subtitle/actions slot. |
| `KpiCard.tsx` | Compact metric card for the dashboard. |
| `DataTable.tsx` | Headless, typed table component used by every list page. |
| `StatusBadge.tsx` | Coloured pill for statuses across domains (subscription, invoice, payment, provider-event, account-standing). |
| `EmptyState.tsx` | Standard empty-result placeholder. |
| `ErrorBox.tsx` | Standard error surface with the API error message. |

Page-local interactive widgets:

* `app/provider-events/[id]/ReprocessButton.tsx` — client component
  guarding visibility on status and posting to the reprocess endpoint.
* `app/account-standing/AccountStandingForm.tsx` — client component
  posting an `evaluate` request for a chosen billing account.

Library files under `lib/`:

* `lib/api.ts` — typed fetch client (env base URL, error wrapping,
  `cache: "no-store"`). Reads `NEXT_PUBLIC_COMMERCE_API_BASE_URL` (with
  legacy `NEXT_PUBLIC_COMMERCE_API_BASE` fallback). Exports
  `api.get<T>` / `api.post<T>` and `ApiError`.
* `lib/types.ts` — TypeScript shapes mirroring the Commerce contract
  DTOs consumed by the UI.
* `lib/enums.ts` — string enums used for badge colour mapping.
* `lib/format.ts` — small helpers (currency, date, optional fallback).

## 5. Known Gaps

* **No authentication / authorisation.** The UI is intended for an
  internal trusted environment; future work must place an
  identity-aware reverse proxy in front of it.
* **No write surface beyond the two operational POSTs** (reprocess,
  evaluate). Catalog/billing/subscription mutations are out of scope.
* **No automated UI tests shipped.** No Vitest/Testing Library or
  Playwright suite is configured. The build, lint, and `tsc --noEmit`
  gates run clean. See §6 for manual test steps.
* **Pagination is fixed at the API defaults** (`?take=100` where
  applicable). No infinite scroll or page-size selector.
* **Provider event payload viewer** renders formatted JSON; it does
  not deep-link to related billing/subscription/payment records.
* **Audit / change history** for subscriptions reads from the existing
  endpoint; richer diff visualisation is deferred.
* The UI is **standalone** (its own `package.json`, lockfile, and
  `node_modules`) and is not part of the pnpm workspace by design.
  This is intentional for portability and was a stated requirement.

## 6. Validation Results

Performed on 24 Apr 2026 (UTC).

* **Install:** `pnpm install --prefer-offline --no-frozen-lockfile`
  succeeded in 3.3 s (pnpm v10.26.1).
* **Build:** `pnpm build` (Next.js 14.2.16) succeeded — 12 routes
  generated, 0 errors. See §2 for the size table.
* **Lint:** `pnpm lint` (`next lint`) — **No ESLint warnings or
  errors.**
* **Typecheck:** `pnpm typecheck` (`tsc --noEmit`) — clean (no output,
  exit 0).
* **Manual verification steps (developer machine):**
  1. Copy `.env.example` to `.env.local` and set
     `NEXT_PUBLIC_COMMERCE_API_BASE_URL` to the running Commerce
     service base URL.
  2. `pnpm dev` (serves on `http://localhost:3100`).
  3. Open `/dashboard` → KPI cards populate.
  4. Visit each list page (catalog, billing accounts, subscriptions,
     invoices, payments, provider events, account standing) → tables
     render or show a clear empty state.
  5. Open a provider event with status `Failed`/`Ignored`/`Received`
     → **Reprocess** button appears; click and observe the API
     response (success or error toast / inline message).
  6. On `/account-standing`, submit the **Evaluate** form for a known
     billing account id → response surfaces inline.

The Commerce backend test suite (235 / 235 passing as of COM-B08) is
unaffected by this UI work.
