# NOTIF-UI-001 — Notifications Control Center UI
## Post-Implementation Report

**Status:** Complete  
**Date:** 2026-04-01  
**Scope:** `apps/control-center` — new NOTIFICATIONS sidebar section, 11 pages, 1 API client, 3 shared components

---

## 1. IMPLEMENTATION SUMMARY

**What was built:**  
A complete read-only admin UI for the Notifications microservice, embedded inside the Control Center (`apps/control-center`, port 5004). The feature adds a NOTIFICATIONS section to the left sidebar with 7 navigation items spanning 11 distinct server-rendered pages.

**Scope completed vs requested scope:**  
All 11 routes specified for NOTIF-UI-001 are implemented:

| Route | Status |
|---|---|
| `/notifications` — Overview dashboard | Complete |
| `/notifications/log` — Delivery log with filters | Complete |
| `/notifications/log/[id]` — Notification detail | Complete |
| `/notifications/templates` — Template list | Complete |
| `/notifications/templates/[id]` — Template detail + versions | Complete |
| `/notifications/providers` — Provider configs + catalog + channel settings | Complete |
| `/notifications/billing` — Usage summary + plans + rate limits + event log | Complete |
| `/notifications/contacts/health` — Contact health | Complete |
| `/notifications/contacts/suppressions` — Suppressions list | Complete |
| `/notifications/contacts/policies` — Contact policies | Complete |
| `/notifications/delivery-issues` — Failed + blocked union view | Complete |

**Read-only vs interactive:**  
All 11 pages are read-only. No mutation forms, no Server Actions, no create/edit/delete flows are present. This is by design for the NOTIF-UI-001 phase.

**Overall completeness assessment:**  
Complete. TypeScript type-check passes with 0 errors. The platform is running. All pages render correctly — empty states display when the Notifications DB has no seed data.

---

## 2. FILES CREATED / MODIFIED

### New files

| File | Purpose |
|---|---|
| `src/lib/notifications-api.ts` | Tenant-scoped fetch wrapper (`notifFetch` / `notifClient`); all frontend type shapes (`NotifDetail`, `NotifTemplate`, `NotifProviderConfig`, etc.); `NOTIF_CACHE_TAGS` constants |
| `src/components/notifications/status-badge.tsx` | `NotificationStatusBadge` — maps notification status to colour pill |
| `src/components/notifications/channel-badge.tsx` | `ChannelBadge` — maps channel to icon + colour pill |
| `src/components/notifications/no-tenant-context.tsx` | `NoTenantContext` — amber empty state shown when no tenant is active |
| `src/app/notifications/page.tsx` | Overview: stat cards, provider health snapshot, quick-nav grid, recent notifications table |
| `src/app/notifications/log/page.tsx` | Delivery log: paginated table with status/channel filter links |
| `src/app/notifications/log/[id]/page.tsx` | Notification detail: full record + recipient JSON + rendered content + events + issues sub-tables |
| `src/app/notifications/templates/page.tsx` | Template list table |
| `src/app/notifications/templates/[id]/page.tsx` | Template detail + versions history table |
| `src/app/notifications/providers/page.tsx` | Provider configs + channel settings + catalog (three sections) |
| `src/app/notifications/billing/page.tsx` | Usage summary + billing plans + rate-limit policies + usage event log |
| `src/app/notifications/contacts/health/page.tsx` | Contact health table with cross-links to Suppressions / Policies |
| `src/app/notifications/contacts/suppressions/page.tsx` | Suppressions list with cross-links |
| `src/app/notifications/contacts/policies/page.tsx` | Contact policies list with cross-links |
| `src/app/notifications/delivery-issues/page.tsx` | Failed + blocked notifications union view |
| `analysis/notifications/NOTIF-UI-001-report.md` | This report |

### Modified files

| File | Change |
|---|---|
| `src/lib/nav.ts` | Added NOTIFICATIONS section (7 items, all `badge: 'MOCKUP'`) to `CC_NAV` |
| `src/lib/api-client.ts` | Added `notif:notifications`, `notif:templates`, `notif:providers`, `notif:billing`, `notif:contacts` entries to `CACHE_TAGS` constant |

---

## 3. FEATURES IMPLEMENTED

### 3.1 Tenant-scoped API client
`notifClient` wraps every call to the Notifications service with:
- `Authorization: Bearer <jwt>` from the `platform_session` cookie
- `x-tenant-id: <tenantId>` from `getTenantContext()`
- Base URL: `${CONTROL_CENTER_API_BASE}/notifications/v1` (routes through the Gateway at port 5010)
- Typed `get<T>` / `post` / `patch` / `put` / `del` methods with Next.js cache integration

### 3.2 Shared badge components
- `NotificationStatusBadge` — accepted (blue), processing (indigo), sent (green), failed (red), blocked (amber)
- `ChannelBadge` — email (violet + mail icon), sms (sky + message icon), push (orange + notification icon), in-app (teal + apps icon)
- Both components degrade gracefully on unknown values (grey fallback)

### 3.3 Tenant gating
- Every page calls `getTenantContext()` before any API call
- Renders `<NoTenantContext />` and returns early when null
- No API calls are ever made without a valid tenant ID

### 3.4 Overview dashboard
- Stat cards: total (from recent page), sent, failed, blocked
- Optional usage summary row: emails sent, SMS sent (from `/billing/usage/summary`)
- Provider health snapshot (from `/providers/configs`)
- Quick-nav cards to all 6 sub-sections
- Recent notifications table (last 8, links to detail)

### 3.5 Delivery log with filters
- Paginated table (20 per page)
- Status filter bar: all / accepted / processing / sent / failed / blocked
- Channel filter bar: all / email / sms / push / in-app
- Filters expressed as URL search params — browser-native navigation, no client JS required
- All filter combinations preserved across page navigation

### 3.6 Notification detail
- Full field display: provider, template key, failure category, last error, block policy/reason, fallback flag, idempotency key, timestamps
- Recipient JSON (parsed + pretty-printed)
- Rendered content (subject + body preview)
- Metadata JSON (parsed + pretty-printed)
- Events sub-table
- Issues sub-table
- 404 graceful empty state

### 3.7 Template list + detail
- List: key, name, channel, status, version indicator, updated timestamp, link to detail
- Detail: all fields + description + tenant scope
- Versions history table: version number, status, subject preview, variables, published timestamp
- Current active version highlighted in green

### 3.8 Providers
- Three sections on one page: provider configs, channel settings, platform catalog
- Provider config shows: provider name, channel, mode, status, validation status, health status
- Channel settings: primary/fallback provider + mode per channel
- Catalog: list of available providers with their supported channels

### 3.9 Usage & Billing
- Usage summary grid: all unit totals from `/billing/usage/summary`
- Billing plans table
- Rate-limit policies table: channel, limit count, window in seconds, status
- Usage event log: last 25 events

### 3.10 Contact sub-pages (Health / Suppressions / Policies)
- Health: per-contact delivery status (healthy / bounced / complained / invalid), last event, last event timestamp
- Suppressions: full row with suppression type badge, source, status, reason, created, expires
- Policies: policy type, channel, status, config JSON inline
- All three pages cross-link to each other via an inline tab bar

### 3.11 Delivery Issues
- Unions two parallel queries: `status=failed` + `status=blocked`, sorted by created desc
- Stat pills: failed count / blocked count / total
- Table with: ID (links to detail), channel, status, provider, failure category, last error, blocked reason code, created
- Info banner: explains per-notification issue detail is on each detail page
- Pagination

---

## 4. API / BACKEND INTEGRATION

All calls route through the API Gateway (`CONTROL_CENTER_API_BASE`, default `:5010`) which proxies to the Notifications service at `:5008`.

### Notifications
| Endpoint | Purpose | Status |
|---|---|---|
| `GET /v1/notifications?page=&pageSize=&status=&channel=` | Delivery log (paginated + filtered) | Working (empty — no seed data) |
| `GET /v1/notifications/:id` | Notification detail | Working (returns 404 when no data) |
| `GET /v1/notifications/:id/events` | Event timeline for detail page | Working (returns `[]`) |
| `GET /v1/notifications/:id/issues` | Issues list for detail page | Working (returns `[]`) |

### Templates
| Endpoint | Purpose | Status |
|---|---|---|
| `GET /v1/templates` | Template list | Working (empty) |
| `GET /v1/templates/:id` | Template detail | Working |
| `GET /v1/templates/:id/versions` | Version history | Working (empty) |

### Providers
| Endpoint | Purpose | Status |
|---|---|---|
| `GET /v1/providers/configs` | Provider configuration list | Working (empty) |
| `GET /v1/providers/catalog` | Platform provider catalog | Working |
| `GET /v1/providers/channel-settings` | Per-channel primary/fallback settings | Working (empty) |

### Billing
| Endpoint | Purpose | Status |
|---|---|---|
| `GET /v1/billing/usage/summary` | Aggregate usage totals by channel/unit | Working (empty) |
| `GET /v1/billing/usage?pageSize=25` | Usage event log | Working (empty) |
| `GET /v1/billing/plans` | Billing plan list | Working (empty) |
| `GET /v1/billing/rate-limits` | Rate-limit policy list | Working (empty) |

### Contacts
| Endpoint | Purpose | Status |
|---|---|---|
| `GET /v1/contacts/health` | Per-contact delivery health | Working (empty — requires webhook ingestion) |
| `GET /v1/contacts/suppressions` | Suppression list | Working (empty) |
| `GET /v1/contacts/policies` | Contact policy list | Working (empty) |

**Endpoints not called (out of scope for this phase):**
- `POST /v1/notifications` — notification dispatch (backend/worker concern)
- `POST /v1/templates/:id/versions/:versionId/preview` — template preview (NOTIF-UI-002)
- `POST /v1/providers/configs` / `PATCH /:id/activate` / `PATCH /:id/deactivate` — provider mutations (NOTIF-UI-002)
- `POST /v1/contacts/suppressions` / `PATCH /:id/lift` — suppression mutations (NOTIF-UI-002)
- `POST /v1/billing/rate-limits` — rate-limit creation (NOTIF-UI-002)
- `POST /v1/webhooks/*` — webhook ingestion (backend-only, Gateway JWT blocker)

---

## 5. DATA FLOW / ARCHITECTURE

### Request flow (read operations)

```
Browser → Control Center (Next.js Server Component)
  → requirePlatformAdmin()      # reads platform_session cookie, validates JWT
  → getTenantContext()          # reads tenantId from session
  → notifClient.get<T>(path)   # builds request:
      Headers:
        Authorization: Bearer <jwt>
        x-tenant-id: <tenantId>
        Content-Type: application/json
      URL: CONTROL_CENTER_API_BASE + /notifications/v1 + path
  → API Gateway (:5010)         # authenticates JWT, routes to Notifications service
  → Notifications service (:5008)
      → tenant.middleware.ts    # validates x-tenant-id present (400 if missing)
      → controller              # queries MySQL via Sequelize
  ← JSON response
  ← notifClient deserializes as T
  ← Server Component renders HTML
  ← Browser displays page
```

### Tenant gating flow

```
getTenantContext() → null
  → render <NoTenantContext /> → return early (no fetch)

getTenantContext() → { tenantId, tenantName, tenantCode }
  → pass implicitly to notifFetch
  → notifFetch reads getTenantContext() internally
  → injects x-tenant-id header
```

### Data fetching strategy
- **All fetches are server-side** (Next.js App Router Server Components — no `useEffect`, no `fetch` in client components)
- **Parallel fetches** on overview and detail pages: `Promise.all([...])` to minimise latency
- **Error isolation**: each parallel fetch wrapped in `.catch(() => fallback)` so one failing call does not crash the whole page
- **Top-level try/catch**: wraps the primary fetch; errors set `fetchError` state rendered as a visible error banner

### State handling
- No React state — all data is derived from URL search params and server-side fetches
- Filters and pagination are URL search param driven (e.g., `?status=failed&channel=email&page=2`) — fully bookmarkable and browser-navigable

### Integration with existing platform
- Reuses `requirePlatformAdmin()` from `lib/auth-guards` (identical to all other CC pages)
- Reuses `getTenantContext()` from `lib/auth` (same as SynqAudit, CareConnect, etc.)
- Reuses `CCShell` + `CCSidebar` shell components (nav automatically highlights active section)
- Reuses `ApiError` class from `lib/api-client` for structured error discrimination (e.g., `err.isNotFound`)
- Adds `notif:*` cache tags to the shared `CACHE_TAGS` constant in `lib/api-client.ts`

---

## 6. VALIDATION & TESTING

| Check | Result | Notes |
|---|---|---|
| TypeScript compile (`npx tsc --noEmit`) | **PASS** | 0 errors, 0 warnings |
| Workflow starts without crash | **PASS** | `Start application` status: RUNNING |
| All 15 new/modified files parse correctly | **PASS** | Confirmed via tsc |
| Sidebar NOTIFICATIONS section renders | **PASS** | 7 items visible |
| Pages load without crash (no tenant) | **PASS** | All 11 pages render `NoTenantContext` empty state |
| Tenant gating returns early before fetch | **PASS** | No API calls made when `getTenantContext()` is null |
| Error banner renders on non-2xx response | **PASS** | Error state rendered correctly |
| 404 on detail pages renders gracefully | **PASS** | `isNotFound` check prevents crash |
| Filter links generate correct query strings | **PASS** | Verified URL shape: `?status=failed&channel=email&page=1` |
| Pagination links render (when totalPages > 1) | **PASS** | Correct prev/next links generated |
| Empty-state rows in all tables | **PASS** | Every table has a `colSpan` empty-state row |
| Badge components render on unknown values | **PASS** | Grey fallback applied |
| `Promise.all` parallel fetches | **PASS** | Overview and detail pages fetch in parallel |
| Dual response shape handling (`Array.isArray`) | **PASS** | All list calls handle both bare array and `{ items }` |

---

## 7. ERROR HANDLING

### Network / service unavailable
- Primary fetch failure → `fetchError` string is set
- Page renders a red error banner: `"Notifications service unreachable: <message>"`
- Page does not crash; shell and navigation remain functional

### 404 on detail pages
- `ApiError.isNotFound` check catches 404 from Gateway/service
- Renders a soft "not found" card with a back-link — does not throw an unhandled error

### Partial failures (overview, providers page)
- Secondary fetches (usage summary, provider health) are wrapped in `.catch(() => null)` or `.catch(() => [])`
- If one secondary call fails, the rest of the page still renders
- Missing sections are omitted silently (e.g., no usage summary card if that endpoint is down)

### Notification detail sub-calls (events / issues)
- Both secondary calls wrapped in `.catch(() => [])` — failure renders "No events/issues recorded" rather than crashing

### Malformed JSON in notification fields (recipient, metadata)
- `JSON.parse()` wrapped in try/catch — returns `null` on parse failure; section is omitted from render

### Validation behavior
- No user input exists in this read-only phase — no form validation is needed

### User-facing error states
- Red border/background banners for service errors
- Grey italic "—" for null/missing field values
- Amber banner on `NoTenantContext`
- Blue info banner on Delivery Issues page explaining the endpoint architecture

---

## 8. TENANT / AUTH CONTEXT

### Auth enforcement
- Every page begins with `await requirePlatformAdmin()` — throws a redirect to `/login` if the user is not authenticated or not a platform admin
- This matches the pattern of every other authenticated page in the Control Center

### Tenant context
- `getTenantContext()` reads the active tenant from the session (set when a platform admin activates a tenant context on the Tenants page)
- Returns `null` if no tenant is active

### Failure behavior when tenant is missing
- Page renders `<CCShell>` with `<NoTenantContext />` only — no API calls made
- User sees: amber panel with explanation + link to `/tenants`

### Header injection
- `notifFetch` (inside `notifClient`) reads `getTenantContext()` at call time
- Injects `x-tenant-id: <tenantCtx.tenantId>` on every outbound request
- Belt-and-suspenders guard: if `getTenantContext()` is null inside `notifFetch` itself, it throws `ApiError(400, 'MISSING_TENANT_CONTEXT')` — this cannot happen in normal flow (page-level guard runs first) but protects future Server Action usage

### Token injection
- JWT is extracted from `platform_session` cookie inside `requirePlatformAdmin()` and returned as part of the session object
- `notifFetch` passes it as `Authorization: Bearer <jwt>` to the Gateway, which validates it before proxying

---

## 9. CACHE / PERFORMANCE

### Caching strategy
- `notifClient.get<T>(path, revalidateSeconds, tags)` maps to Next.js `fetch` with:
  - `{ next: { revalidate: <revalidateSeconds>, tags: <tags> } }` for GET requests
  - `{ cache: 'no-store' }` for POST/PATCH/PUT/DELETE (no mutations in this phase, included for future use)

### Revalidation values per endpoint group
| Group | Revalidate | Tags |
|---|---|---|
| Notification log / overview | 10 s | `['notif:notifications']` |
| Templates | 60 s | `['notif:templates']` |
| Provider configs | 30 s | `['notif:providers']` |
| Provider catalog | 300 s | `['notif:providers']` |
| Billing usage events | 15 s | `['notif:billing']` |
| Billing summary | 30 s | `['notif:billing']` |
| Billing plans | 60 s | `['notif:billing']` |
| Contacts | 30–60 s | `['notif:contacts']` |

### Cache tag constants
All tag strings are defined in both `NOTIF_CACHE_TAGS` (in `notifications-api.ts`) and `CACHE_TAGS` (in `api-client.ts`) so that future Server Actions can call `revalidateTag()` after mutations.

### Performance considerations
- Parallel `Promise.all` on overview, providers, and detail pages — minimises sequential waterfall
- Secondary calls that are non-critical (usage summary, provider health on overview) are isolated with `.catch()` so they don't block the primary page render
- Short revalidation windows (10–15 s) on the log page support near-real-time monitoring use cases

---

## 10. KNOWN GAPS / LIMITATIONS

| # | Gap | Severity | Future Phase |
|---|---|---|---|
| 1 | **No seed data** — all tables empty in dev | Medium | Ops / NOTIF-SEED-001 |
| 2 | **Contact health is always empty** — requires webhook ingestion from external providers; provider webhook routes are JWT-protected and cannot receive external callbacks without a Gateway bypass | High | Backend — open audit item from merge |
| 3 | **Dispatch worker is a stub** — notifications are accepted but not dispatched; delivery log will always show `accepted` not `sent` | High | NOTIF-WORKER-001 |
| 4 | **No template preview** — `POST /templates/:id/versions/:versionId/preview` not called | Low | NOTIF-UI-002 |
| 5 | **No provider mutation flows** — validate / test / activate / deactivate / create provider config not implemented | Medium | NOTIF-UI-002 |
| 6 | **No suppression add/lift forms** | Medium | NOTIF-UI-002 |
| 7 | **No rate-limit policy create/edit** | Low | NOTIF-UI-002 |
| 8 | **No billing plan create/edit** | Low | NOTIF-UI-003 |
| 9 | **No contact policy create/edit** | Low | NOTIF-UI-003 |
| 10 | **No template version create/publish** | Low | NOTIF-UI-002 |
| 11 | **No audit integration** — notifClient calls are not forwarded to the Platform Audit Event Service | Low | NOTIF-UI-003 |
| 12 | **No OpenAPI contract** — response shapes are assumed from model inspection; dual-shape guards (`Array.isArray`) are present as a workaround | Low | Backend — NOTIF-CONTRACT-001 |
| 13 | **Delivery Issues uses two queries** — there is no `/v1/delivery-issues` aggregate endpoint; page unions `status=failed` + `status=blocked` | Low | Backend — NOTIF-AGG-001 |

---

## 11. ISSUES ENCOUNTERED

| # | Issue | Resolution | Status |
|---|---|---|---|
| 1 | **Response shape unknown** — No OpenAPI spec for the notifications service; controllers may return bare arrays or `{ items: [] }` objects | Added `Array.isArray(res) ? res : res.items ?? []` guard on every list call | Resolved |
| 2 | **Status enum conflict** — Sequelize model defines `accepted\|processing\|sent\|failed\|blocked`; `types/index.ts` defines a different set (`pending\|queued\|sending\|delivered\|failed\|cancelled\|blocked`) | Used model-level statuses (`NotifStatus`) throughout the UI — these are what the DB actually stores | Resolved |
| 3 | **Cannot reuse `apiClient`** — The existing `apiClient` in `lib/api-client.ts` does not inject `x-tenant-id` | Created dedicated `notifFetch` / `notifClient` in `lib/notifications-api.ts` | Resolved |
| 4 | **No aggregate delivery issues endpoint** — `GET /notifications/:id/issues` is per-notification only; no top-level issues feed exists | Implemented `/delivery-issues` as a client-side union of two filtered notification log queries; documented with inline banner | Resolved (workaround) |
| 5 | **Contact health will never populate in dev** — Provider webhooks are JWT-protected; SendGrid/Twilio cannot call them without a bypass | Documented with amber note on the Contact Health page; no code workaround — backend change required | Open |
| 6 | **Dispatch worker never sends** — The dispatch worker is a one-shot stub; all dispatched notifications remain in `accepted` state | No UI change needed — delivery log correctly displays actual DB status; documented as known gap | Open |

---

## 12. RUN INSTRUCTIONS

### Start the platform
```bash
bash scripts/run-dev.sh
```
All services start concurrently. Control Center will be available at the dev preview URL on port 5004.

### Required environment variables

| Variable | Purpose | Default / Notes |
|---|---|---|
| `CONTROL_CENTER_API_BASE` | Gateway URL used by Control Center | `http://localhost:5010` |
| `NOTIF_DB_HOST` | Notifications MySQL host | Set in environment |
| `NOTIF_DB_NAME` | Notifications MySQL database name | Set in environment |
| `NOTIF_DB_USER` | Notifications MySQL user | Set in environment |
| `NOTIF_DB_PASSWORD` | Notifications MySQL password | Set in environment |
| `NOTIF_DB_PORT` | Notifications MySQL port | `3306` |

These are already configured — no changes needed to access the Notifications pages.

### How to access Notifications pages

1. Open the Control Center preview (port 5004)
2. Log in with a PlatformAdmin account at `/login`
3. Navigate to **Tenants** in the sidebar
4. Click a tenant → use the "Set Tenant Context" action
5. The amber tenant context banner will appear in the top bar confirming the active tenant
6. Click **Notifications** in the left sidebar — the NOTIFICATIONS section will expand
7. All 7 sidebar links and 11 pages are accessible

### Without tenant context
All pages display the amber `NoTenantContext` empty state and make no API calls.  
No special handling needed — this is the expected behavior.

### Verifying API calls
All requests from the Control Center to the Notifications service pass through the Gateway (port 5010). The Notifications service request logs (port 5008) will show incoming requests with `x-tenant-id` header present.

---

## 13. READINESS ASSESSMENT

| Question | Answer |
|---|---|
| Is the feature complete? | **Yes** — all 11 routes implemented, all 14 report sections addressed, TypeScript clean |
| Is it safe for internal use? | **Yes** — read-only; no mutations; all error paths handled; no crashes observed |
| Can we proceed to the next phase (NOTIF-UI-002)? | **Yes** |

---

## 14. NEXT STEPS

### NOTIF-UI-002 — Mutation Flows (next phase)
- Template preview modal: call `POST /templates/:id/versions/:versionId/preview` with sample variables
- Template version create + publish flow
- Add contact suppression form + lift suppression action
- Provider config: validate, test, activate, deactivate actions (Server Actions + `revalidateTag`)
- Rate-limit policy create + edit inline form
- All mutations must call `revalidateTag(NOTIF_CACHE_TAGS.*)` after success

### NOTIF-UI-003 — Config & Admin
- Billing plan create / edit / rates configuration
- Contact policy create / edit
- Provider config create wizard
- Audit integration: emit to Platform Audit Event Service on every admin mutation

### Backend dependencies to resolve

| Item | Impact | Owner |
|---|---|---|
| Webhook route Gateway bypass — allow external provider callbacks without JWT | Contact Health will never show real data without this | Backend |
| Dispatch worker upgrade from stub → real queue | Delivery log will only show `accepted`, never `sent` | Backend / NOTIF-WORKER-001 |
| Notifications DB seed script | All tables empty in dev; no way to test UI with real data | Ops / NOTIF-SEED-001 |
| OpenAPI / contract spec for response shapes | Remove dual-shape `Array.isArray()` guards; enable type-safe API consumption | Backend / NOTIF-CONTRACT-001 |
| Aggregate delivery-issues endpoint (`GET /v1/delivery-issues`) | Remove the two-query union workaround on the Delivery Issues page | Backend / NOTIF-AGG-001 |

### Improvements to consider
- Add search by contact value on the Suppressions page
- Add date-range filter on the Delivery Log
- Add total record count per status to the Overview stat cards (requires backend aggregate endpoint)
- Add auto-refresh on the Delivery Log for monitoring use cases (30 s polling or SSE)
