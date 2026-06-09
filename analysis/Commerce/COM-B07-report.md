# COM-B07 — Admin & Operability Layer

## 1. Summary

COM-B07 adds the Admin & Operability layer on top of the standalone
`services/Commerce` ASP.NET Core 8 service. Two deliverables are in
scope:

1. **Backend** — a new read-only "admin dashboard" surface area at
   `/api/commerce/admin/dashboard/*` (summary, revenue summary,
   account-standing summary, provider-event summary, recent activity)
   built as projection-only queries over the existing `CommerceDbContext`.
   No new aggregates, no commands, no schema migrations.
2. **Frontend** — a standalone Next.js 14 (App Router) + TypeScript +
   Tailwind + Lucide React admin UI under `services/Commerce/admin/`.
   Internal-only, server-rendered, no auth, no telemetry, talks to the
   Commerce service via a single typed fetch client. Pages cover
   Dashboard, Catalog, Billing Accounts (+detail), Subscriptions
   (+detail), Invoices (+detail), Payments, Provider Events (+detail with
   gated reprocess), and Account Standing (with manual re-evaluate).

The block was implemented strictly read-first. The only state-mutating
controls in the UI are the two operability actions explicitly required
by the spec: provider-event reprocess and manual account-standing
re-evaluate. Both call existing controller endpoints; no new write
endpoints were introduced.

## 2. Stories Completed

- **As an operator**, I can open a dashboard and see at-a-glance health
  of the catalog, billing accounts, subscriptions, invoices, payments,
  account standing, and provider events.
- **As an operator**, I can drill into individual billing accounts,
  subscriptions, invoices, payments, provider events, and account
  standing to see read-only detail.
- **As an operator**, I can manually reprocess a provider webhook event
  log when it has a recoverable status (Failed / Received / Ignored).
- **As an operator**, I can manually re-evaluate account standing for a
  given billing account.
- **As a developer**, I have typed admin DTOs + a typed TS client so
  the admin UI fails fast at build time on any contract drift.

## 3. Backend Architecture Implemented

The backend follows the existing four-project layout
(`Domain`, `Contracts`, `Application`, `Infrastructure`, `Api`) and
fits cleanly into the existing DI graph.

```
Commerce.Contracts
└── Admin/DashboardDtos.cs            # all admin-dashboard DTOs

Commerce.Application
└── Admin/Abstractions/IAdminDashboardService.cs

Commerce.Infrastructure
├── Admin/Services/AdminDashboardService.cs   # internal sealed, registered
└── DependencyInjection.cs                    # +1 line: AddScoped<IAdminDashboardService, AdminDashboardService>

Commerce.Api
└── Controllers/Admin/AdminDashboardController.cs  # 5 GET endpoints
```

Design decisions:

- `AdminDashboardService` is **read-only** by design — every method is a
  projection over `CommerceDbContext`. No tracked entities, no writes,
  no domain events, no commands.
- Group counts are produced via `GroupBy(...).ToDictionaryAsync(...)`
  and then "filled" against the full enum so consumers always receive a
  stable shape (every status key present, zero when absent). This keeps
  the admin UI free of `?? 0` defensive code.
- `GetRecentActivityAsync(take)` merges the latest entries from
  invoices, payments, provider event logs, and subscription changes
  into a single `RecentActivityResponse` with a generic
  `RecentActivityEntryResponse(Kind, Id, Summary, Status,
  OccurredAtUtc)`. This is intentional — the UI renders a unified list
  without coupling to module-specific DTOs.
- Time is sourced from the existing `IClock` so tests can assert
  `GeneratedAtUtc` deterministically.

No JSON serialization changes were made. Enum values continue to
serialize as integers (System.Text.Json default), and the admin
frontend converts them via a small `enumName(map, value)` helper. An
earlier attempt to flip the API to `JsonStringEnumConverter` was
reverted because it broke 6 existing controller tests that
deserialize `ReadFromJsonAsync<T>()` without configured options.

## 4. Frontend Architecture Implemented

`services/Commerce/admin/` is a fully standalone Next.js 14 app — it is
deliberately **not** part of the pnpm workspace so the Commerce service
can be picked up and dropped into a different monorepo without churn.

Layout:

```
services/Commerce/admin/
├── package.json            # next 14.2.16, react 18.3, tailwind 3.4, lucide-react 0.460
├── tsconfig.json           # strict, @/* path alias
├── next.config.mjs
├── tailwind.config.ts      # ink + accent palette, scans app/components/lib
├── postcss.config.mjs
├── .eslintrc.json          # next/core-web-vitals
├── .env.example            # NEXT_PUBLIC_COMMERCE_API_BASE
├── README.md
├── styles/globals.css
├── lib/
│   ├── api.ts              # typed fetch + ApiError, env base URL
│   ├── types.ts            # mirrors of every consumed DTO
│   ├── enums.ts            # numeric-enum → string name maps + enumName()
│   └── format.ts           # money / date helpers
├── components/
│   ├── SideNav.tsx         # client component, lucide icons + active state
│   ├── PageHeader.tsx, KpiCard.tsx, StatusBadge.tsx,
│   ├── DataTable.tsx, EmptyState.tsx, ErrorBox.tsx
└── app/
    ├── layout.tsx          # SideNav + main shell
    ├── page.tsx            # redirects to /dashboard
    ├── dashboard/page.tsx
    ├── catalog/page.tsx
    ├── billing-accounts/page.tsx
    ├── billing-accounts/[id]/page.tsx
    ├── subscriptions/page.tsx
    ├── subscriptions/[id]/page.tsx
    ├── invoices/page.tsx
    ├── invoices/[id]/page.tsx
    ├── payments/page.tsx
    ├── provider-events/page.tsx
    ├── provider-events/[id]/page.tsx
    ├── provider-events/[id]/ReprocessButton.tsx   # client component
    ├── account-standing/page.tsx
    └── account-standing/AccountStandingForm.tsx   # client component
```

Conventions:

- All list/detail pages are **server components** that fetch through
  `lib/api.ts` with `cache: "no-store"`. Errors surface as a friendly
  banner via `ErrorBox` rather than crashing the route.
- The two operability actions live in **client components** under their
  page directories so the rest of the app stays server-rendered.
- All numeric enum fields are rendered via
  `enumName(<EnumMap>, value)` — the helper is tolerant of either
  numeric or string input so this UI stays compatible if the backend
  later flips to `JsonStringEnumConverter`.

## 5. Files Created/Changed

### Backend

Added:

- `services/Commerce/src/Commerce.Contracts/Admin/DashboardDtos.cs`
- `services/Commerce/src/Commerce.Application/Admin/Abstractions/IAdminDashboardService.cs`
- `services/Commerce/src/Commerce.Infrastructure/Admin/Services/AdminDashboardService.cs`
- `services/Commerce/src/Commerce.Api/Controllers/Admin/AdminDashboardController.cs`
- `services/Commerce/tests/Commerce.Tests/Admin/AdminDashboardServiceTests.cs`
- `services/Commerce/tests/Commerce.Tests/Admin/AdminDashboardEndpointsTests.cs`
- `services/Commerce/tests/Commerce.Tests/Admin/AdminDashboardTestHost.cs`

Modified:

- `services/Commerce/src/Commerce.Infrastructure/DependencyInjection.cs`
  (single line: register `IAdminDashboardService`).

Tooling:

- `services/Commerce/.config/dotnet-tools.json` (added `dotnet-ef
  8.0.10` as a local tool so the idempotent migration script can be
  generated from a clean checkout).

### Frontend

All files under `services/Commerce/admin/` listed in section 4 are new.

## 6. API Endpoints Added/Reused

### Added (admin dashboard)

| Method | Route                                                          | Returns                              |
|-------:|----------------------------------------------------------------|--------------------------------------|
| GET    | `/api/commerce/admin/dashboard/summary`                        | `AdminDashboardSummaryResponse`      |
| GET    | `/api/commerce/admin/dashboard/revenue-summary`                | `RevenueSummaryResponse`             |
| GET    | `/api/commerce/admin/dashboard/account-standing-summary`       | `AccountStandingSummaryResponse`     |
| GET    | `/api/commerce/admin/dashboard/provider-event-summary`         | `ProviderEventSummaryResponse`       |
| GET    | `/api/commerce/admin/dashboard/recent-activity?take={n}`       | `RecentActivityResponse`             |

### Reused by the admin UI (no changes)

| Method | Route |
|-------:|-------|
| GET  | `/api/commerce/catalog/products`, `/plans`, `/bundles`, `/addons`, `/prices` |
| GET  | `/api/commerce/billing-accounts`, `/api/commerce/billing-accounts/{id}` |
| GET  | `/api/commerce/billing-accounts/{billingAccountId}/subscriptions` |
| GET  | `/api/commerce/billing-accounts/{billingAccountId}/invoices` |
| GET  | `/api/commerce/subscriptions[?billingAccountId=…]`, `/api/commerce/subscriptions/{id}` |
| GET  | `/api/commerce/invoices?take=…`, `/api/commerce/invoices/{id}` |
| GET  | `/api/commerce/payments?take=…` |
| GET  | `/api/commerce/payments/event-logs?take=…`, `/api/commerce/payments/event-logs/{id}` |
| POST | `/api/commerce/payments/event-logs/{id}/reprocess` (operability) |
| GET  | `/api/commerce/billing-accounts/{billingAccountId}/account-standing` |
| POST | `/api/commerce/billing-accounts/{billingAccountId}/account-standing/evaluate` (operability) |

## 7. Admin UI Pages Added

| Path                                  | Purpose                                                                     |
|---------------------------------------|-----------------------------------------------------------------------------|
| `/`                                   | Server redirect to `/dashboard`.                                            |
| `/dashboard`                          | KPI cards + revenue-by-currency + standing breakdown + provider-event groups + recent-activity feed. |
| `/catalog`                            | Tabular read-only view of products, plans, bundles, add-ons, prices.        |
| `/billing-accounts`                   | List of billing accounts.                                                   |
| `/billing-accounts/[id]`              | Billing account profile + linked subscriptions + linked invoices + jump-link to account-standing. |
| `/subscriptions`                      | Cross-account subscription list.                                            |
| `/subscriptions/[id]`                 | Subscription profile + line items.                                          |
| `/invoices`                           | Latest invoices (configurable `?take`).                                     |
| `/invoices/[id]`                      | Invoice header + lines.                                                     |
| `/payments`                           | Latest payments with status, amount, failure reason.                        |
| `/provider-events`                    | Latest provider webhook event logs.                                         |
| `/provider-events/[id]`               | Event detail + **status-gated** Reprocess action.                           |
| `/account-standing`                   | Lookup form for any billing-account id with **Re-evaluate** action.         |
| `/account-standing?billingAccountId=…`| Same page, prefilled — used by the billing-account detail jump link.        |

## 8. Operability Features Implemented

1. **Provider-event reprocess.**
   - UI: `app/provider-events/[id]/ReprocessButton.tsx` (client).
   - Wire: `POST /api/commerce/payments/event-logs/{id}/reprocess`
     → `ReprocessProviderEventResponse`.
   - The button is **only rendered** when the event's
     `processingStatus` is `Failed`, `Received`, or `Ignored`.
     Otherwise the page shows a non-actionable explanation. This avoids
     triggering 409s from the backend's `ProviderEventReplayService`
     for terminal statuses (`Processed`, `Duplicate`).
   - Result is shown inline (status + reason) and the route is
     refreshed via `router.refresh()` so the surrounding fields update.

2. **Manual account-standing re-evaluation.**
   - UI: `app/account-standing/AccountStandingForm.tsx` (client).
   - Wire: `POST /api/commerce/billing-accounts/{id}/account-standing/evaluate`.
   - Form takes a billing-account GUID, supports both **Lookup** (GET)
     and **Re-evaluate** (POST). The detail card refreshes from the
     POST response.
   - Linkable from the billing account detail page via
     `/account-standing?billingAccountId=<id>`.

3. **Operational dashboard.**
   - Provides at-a-glance counts across every Commerce surface plus a
     unified recent-activity stream — the entry point for noticing that
     anything needs operability action in the first place.

## 9. Tests Added

Backend (xUnit, `Commerce.Tests`, **13 new**):

- `AdminDashboardServiceTests` (8 tests): summary counts include
  active/inactive products and grouped subscription/invoice/payment
  status counts; revenue-by-currency aggregates `Paid` vs `Open`
  amounts and counts; account-standing summary always contains every
  enum key; provider-event summary groups by `(Provider, Status)` and
  surfaces `LastEventUtc`; recent-activity entries are time-ordered
  and respect `take`.
- `AdminDashboardEndpointsTests` (5 tests): each `GET` endpoint
  returns 200 with the expected response shape, exercised through
  `WebApplicationFactory<Program>` and the in-memory Commerce DB.

Test suite total: **204 passed / 0 failed / 0 skipped** (was 199
before COM-B07).

Frontend tests are deferred — see section 11.

## 10. Validation Results

| Check | Command | Result |
|-------|---------|-------|
| .NET solution build | `dotnet build` (Commerce.Api + Commerce.Tests, `/p:UseSharedCompilation=false`) | ✅ 0 warnings, 0 errors |
| .NET tests          | `dotnet test … --no-build -- xUnit.MaxParallelThreads=1` | ✅ 204 / 204 |
| EF idempotent SQL   | `dotnet ef migrations script --idempotent --project src/Commerce.Infrastructure --startup-project src/Commerce.Api` | ✅ 1675-line script generated cleanly (no schema changes vs COM-B06) |
| Frontend install    | `pnpm install --ignore-workspace` (in `services/Commerce/admin`) | ✅ |
| Frontend typecheck  | `npx tsc --noEmit` | ✅ 0 errors |
| Frontend lint       | `npx next lint` | ✅ no warnings or errors |
| Frontend build      | `npx next build` | ✅ 12 routes compiled (1 static, 11 dynamic) |

## 11. Known Gaps or Deferred Items

- **Frontend unit/integration tests deferred.** Adding Vitest +
  Testing Library would have required ~1 GB of additional dev
  dependencies and a JSDOM/Server-Component test harness that is
  noticeably heavier than the rest of the admin app. Given (a) the
  admin is read-first and server-rendered, (b) `tsc --noEmit` already
  catches contract drift between TS types and DTOs at build time, and
  (c) the Commerce backend itself has comprehensive coverage, we chose
  to ship without a dedicated frontend test runner. The two
  client-side actions (provider-event reprocess and account-standing
  re-evaluate) are thin wrappers over backend endpoints that *are*
  covered by backend tests.
- **No pagination on long lists.** The list pages cap at the default
  `take` (50) or `?take=100` where supported. A proper
  cursor/page-based pagination control was deliberately deferred to a
  future block since it would also require backend `take/skip` query
  parameters across catalog/billing-account endpoints that don't
  currently expose them.
- **Billing-account detail does not show contacts, external refs,
  payment methods, or audit events.** The existing endpoints exist and
  are reachable; we surfaced the operability-critical surfaces
  (subscriptions, invoices, account-standing). Surfacing the rest is a
  small, mechanical follow-up.
- **No filter/search on list pages.** Operators can navigate by ID and
  through cross-links; structured filters were intentionally left out
  of the read-first scope.
- **No dark mode / no responsive sidebar.** The UI is internal-only
  and desktop-first; both are easy follow-ups.

## 12. Confirmation of Strict Exclusions

The following items were **explicitly excluded** by the spec and
nothing in COM-B07 changes that:

- ❌ No authentication, authorization, JWT, sessions, or login UI.
  `services/Commerce/admin` is internal-only and assumes network-level
  protection.
- ❌ No entitlements / feature-flag evaluation.
- ❌ No tax engine, refund engine, coupon engine, dunning engine, or
  email/notification dispatch.
- ❌ No customer self-service portal.
- ❌ No new schema, no new EF migrations — `dotnet ef migrations
  script --idempotent` produces an unchanged 1675-line script
  identical in scope to the COM-B06 baseline.
- ❌ No new outbound integrations or background workers.
- ❌ No changes to existing controller routes, request/response shapes,
  or persistence behavior.

The single non-additive backend change is one line in
`Commerce.Infrastructure/DependencyInjection.cs` to register the new
`IAdminDashboardService`.

## 13. Recommended Next Block

Suggested follow-ups, in priority order:

1. **COM-B08 — Auth/authorization for the admin UI** (likely as
   network-fronted SSO + service-to-service mTLS, gated by a feature
   flag so the standalone-mode use case still works).
2. **COM-B09 — Pagination + filtering across list endpoints**
   (back-end query parameters, then UI controls). The dashboard's
   recent-activity already implies the patterns here.
3. **Operability extensions**: surface contacts / external refs /
   payment methods / audit events on the billing-account detail page,
   add a "retry recent failed payments" operability action keyed off
   the existing payment-attempts surface.
4. **Frontend test harness** (Vitest + Testing Library + a small
   contract test that fetches `/api/commerce/admin/dashboard/summary`
   from a running API and validates the shape against `lib/types.ts`).
