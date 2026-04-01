# NOTIF-UI-001 — Notifications Control Center UI

**Status:** Planned  
**Date:** 2026-04-01  
**Scope:** Control Center (`apps/control-center`) — new Notifications section wired to the Notifications service at `:5008`

---

## Objective

Surface the Notifications service in the Control Center as a first-class admin section. Platform admins, scoped to an active tenant context, will be able to:

1. Monitor notification delivery (log, status, failure categories)
2. Manage message templates and their version lifecycle
3. Configure and health-check provider integrations (SendGrid, Twilio, etc.)
4. View usage/billing and rate-limit policies per tenant
5. Manage contact suppressions and review contact-channel health

---

## Tenant Context Requirement

Every Notifications service endpoint (except `GET /v1/health`) requires an `x-tenant-id` request header — enforced by the strict tenant middleware introduced in the merge fix.

**Design decision:** Notifications pages will not function without an active tenant context. When `getTenantContext()` returns `null`, every page renders a full-width info banner:

> **Select a tenant context to use Notifications.**  
> Use the tenant switcher at the top of the Tenants page.

This avoids building a cross-tenant aggregation layer that the backend doesn't yet support.

---

## New API Client — `lib/notifications-api.ts`

`apiFetch` in `lib/api-client.ts` does not forward `x-tenant-id`. A dedicated wrapper is needed.

```
lib/notifications-api.ts
```

Behaviour:
- Reads `tenantCtx = getTenantContext()` — throws/redirects if absent (caller should guard before reaching this point)
- Forwards `x-tenant-id: tenantCtx.tenantId` on every request alongside `Authorization: Bearer <token>`
- Follows the same cache, error-handling, and logging contract as `apiFetch`
- Exports `notifClient` with `.get / .post / .patch / .del` convenience methods
- Adds `NOTIF_CACHE_TAGS` constant map for on-demand revalidation

Cache tag map:

| Constant | Tag | TTL |
|---|---|---|
| `notifications` | `notif:notifications` | 10 s |
| `templates` | `notif:templates` | 60 s |
| `providers` | `notif:providers` | 30 s |
| `billing` | `notif:billing` | 60 s |
| `contacts` | `notif:contacts` | 30 s |

---

## Gateway Routes

Already in place from the merge (`appsettings.json`):

| Pattern | Auth | Destination |
|---|---|---|
| `/notifications/v1/health` | Anonymous | `:5008/v1/health` |
| `/notifications/**` | JWT | `:5008` |

No gateway changes required for NOTIF-UI-001.

---

## Pages

### 1. `/notifications` — Overview

**Component:** `app/notifications/page.tsx`  
**Data:**
- `GET /notifications/v1/notifications?page=1&pageSize=5` — recent 5 notifications
- `GET /notifications/v1/providers/configs` — provider config count
- `GET /notifications/v1/billing/usage/summary` — channel usage totals

**UI:**
- Stat row: Total sent / Failed / Blocked / Templates count
- Provider health snapshot (healthy / degraded / down badges from `GET /v1/providers/catalog`)
- Recent activity table (last 5 notifications — id, channel, status, created)
- Quick-nav cards to sub-sections

---

### 2. `/notifications/log` — Notification Log

**Component:** `app/notifications/log/page.tsx`  
**Data:** `GET /notifications/v1/notifications` with optional `?status=&channel=&page=&pageSize=`

**UI:**
- Filter bar: status dropdown (accepted / processing / sent / failed / blocked), channel dropdown (email / sms / push / in-app), date range
- Table: ID (truncated), Channel, Status badge, Recipient (from `recipientJson`), Provider used, Failure category, Created
- Row expand: shows `renderedSubject`, `renderedBody` preview, events (`GET /:id/events`), issues (`GET /:id/issues`)

**Status badge colours:**
| Status | Colour |
|---|---|
| sent | green |
| accepted | blue |
| processing | indigo |
| failed | red |
| blocked | amber |

---

### 3. `/notifications/templates` — Templates

**Component:** `app/notifications/templates/page.tsx`  
**Data:** `GET /notifications/v1/templates`

**UI:**
- Table: Template key, Channel, Published version number, Last updated
- Row action → `app/notifications/templates/[id]/page.tsx`:
  - Template detail card (key, channel, status)
  - Version list table (`GET /:id/versions`)
  - Published/draft badge per version
  - "Preview" button → calls `POST /:id/versions/:versionId/preview` with sample variables; renders result in a read-only modal

---

### 4. `/notifications/providers` — Provider Configuration

**Component:** `app/notifications/providers/page.tsx`  
**Data:**
- `GET /notifications/v1/providers/catalog` — available providers
- `GET /notifications/v1/providers/configs` — tenant-specific configs
- `GET /notifications/v1/providers/channel-settings` — per-channel routing settings

**UI (three tabs):**

**Configs tab:**
- Table: Provider, Channel, Status (active/inactive), Mode (platform/tenant)
- Actions: Validate / Test / Activate / Deactivate (Server Actions → `POST /configs/:id/validate` etc.)

**Channel Settings tab:**
- Per-channel card: email / sms / push / in-app
- Shows selected provider, fallback provider, override flags
- Edit inline → `PUT /channel-settings/:channel`

**Catalog tab:**
- Read-only list of available platform providers with their supported channels

---

### 5. `/notifications/billing` — Usage & Billing

**Component:** `app/notifications/billing/page.tsx`  
**Data:**
- `GET /notifications/v1/billing/usage/summary` — channel totals
- `GET /notifications/v1/billing/usage` — paginated usage event log
- `GET /notifications/v1/billing/plans` — tenant billing plan
- `GET /notifications/v1/billing/rate-limits` — rate-limit policies

**UI (two sections):**

**Usage section:**
- Summary stat cards: emails sent / SMS sent / push sent / in-app sent (current period)
- Paginated usage event table: timestamp, channel, event type, quantity, metadata

**Rate Limits section:**
- Table: Policy name, Channel, Limit, Window, Current count
- Create / edit rate-limit (Server Action → POST/PATCH)

---

### 6. `/notifications/contacts` — Contact Management

**Component:** `app/notifications/contacts/page.tsx`  
**Data:**
- `GET /notifications/v1/contacts/suppressions` — suppression list
- `GET /notifications/v1/contacts/health` — contact-channel health records
- `GET /notifications/v1/contacts/policies` — contact policies

**UI (two tabs):**

**Suppressions tab:**
- Table: Contact value, Channel, Reason code, Suppressed at
- Add suppression (Server Action → `POST /suppressions`)
- Edit/lift suppression (`PATCH /suppressions/:id`)

**Health tab:**
- Table: Channel, Contact value, Status (healthy/bounced/complained/invalid), Last event, Last event at
- Read-only — populated by webhook ingestion

---

## Sidebar Navigation

New `NOTIFICATIONS` section added to `CC_NAV` in `lib/nav.ts`:

```ts
{
  heading: 'NOTIFICATIONS',
  items: [
    { href: '/notifications',           label: 'Overview',     icon: 'ri-notification-3-line',  badge: 'MOCKUP' },
    { href: '/notifications/log',       label: 'Delivery Log', icon: 'ri-mail-send-line',        badge: 'MOCKUP' },
    { href: '/notifications/templates', label: 'Templates',    icon: 'ri-file-text-line',        badge: 'MOCKUP' },
    { href: '/notifications/providers', label: 'Providers',    icon: 'ri-plug-line',             badge: 'MOCKUP' },
    { href: '/notifications/billing',   label: 'Usage & Billing', icon: 'ri-bar-chart-2-line',  badge: 'MOCKUP' },
    { href: '/notifications/contacts',  label: 'Contacts',     icon: 'ri-contacts-line',         badge: 'MOCKUP' },
  ],
},
```

Badges start as `MOCKUP`. Promote to `IN PROGRESS` once the API client is wired; `LIVE` once tenant context is seeded and end-to-end data flows.

---

## Open Items Before Implementation

| # | Item | Impact |
|---|---|---|
| 1 | **No DB seed** — Notifications MySQL DB has no data in dev. All pages will render empty tables. | Low — empty states are valid UI; can add fixture seeding later |
| 2 | **Webhook routes JWT-protected** — External providers (SendGrid, Twilio) cannot hit `/v1/webhooks/**` through the Gateway without a bypass route. Contact health data will not populate. | Medium — contacts/health tab will always be empty until resolved |
| 3 | **No `x-tenant-id` in `apiFetch`** — Requires the new `notifications-api.ts` wrapper; cannot reuse existing `apiClient` directly. | Required — blocking |
| 4 | **Dispatch worker is a one-shot stub** — Notifications enqueued via `POST /v1/notifications` are accepted but not dispatched. Delivery log will show `accepted` status, not `sent`. | Low for UI — status badge accurately reflects reality |

---

## Files to Create / Modify

### New
| File | Description |
|---|---|
| `src/lib/notifications-api.ts` | Tenant-scoped fetch wrapper with `x-tenant-id` header |
| `src/app/notifications/page.tsx` | Overview dashboard |
| `src/app/notifications/log/page.tsx` | Delivery log table + expand |
| `src/app/notifications/templates/page.tsx` | Template list |
| `src/app/notifications/templates/[id]/page.tsx` | Template detail + version manager |
| `src/app/notifications/providers/page.tsx` | Configs / channel settings / catalog tabs |
| `src/app/notifications/billing/page.tsx` | Usage summary + rate-limit policies |
| `src/app/notifications/contacts/page.tsx` | Suppressions + contact health |
| `src/components/notifications/status-badge.tsx` | Reusable notification status badge |
| `src/components/notifications/channel-badge.tsx` | Reusable channel badge |
| `src/components/notifications/no-tenant-context.tsx` | Shared empty state for missing tenant context |
| `analysis/notifications/NOTIF-UI-001-report.md` | This report |

### Modified
| File | Change |
|---|---|
| `src/lib/nav.ts` | Add NOTIFICATIONS section to `CC_NAV` |
| `src/lib/api-client.ts` | Add `notif:*` tags to `CACHE_TAGS` constant |
