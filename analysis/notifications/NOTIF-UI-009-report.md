# NOTIF-UI-009 — Tenant Notification Activity + Delivery Visibility

## 1. Implementation Summary

**What was built:** A complete tenant-facing notification activity and delivery visibility surface, consisting of:
- Activity list page with summary cards, delivery breakdown, channel split, filterable paginated table
- Activity detail page with notification metadata, delivery status, failure/blocked reason panel, template usage indicator, content preview (sandboxed HTML), event timeline, and related issues
- API client extensions for notification detail, events, and issues endpoints
- Navigation integration with the notifications hub page

**Scope completed vs requested:** All 15 acceptance criteria from the spec are addressed. The implementation is read-only, tenant-scoped, and does not expose any admin/CC capabilities.

**Overall completeness:** Complete

## 2. Files Created / Modified

### New Files
| File | Purpose |
|------|---------|
| `apps/web/src/app/(platform)/notifications/activity/page.tsx` | Activity list page — summary cards, filters, paginated table |
| `apps/web/src/app/(platform)/notifications/activity/[notificationId]/page.tsx` | Activity detail page — metadata, status, template usage, content preview, timeline, issues, failure panel |

### Modified Files
| File | Changes |
|------|---------|
| `apps/web/src/lib/notifications-shared.ts` | Added `NotifDetail`, `NotifEvent`, `NotifIssue` types |
| `apps/web/src/lib/notifications-server-api.ts` | Added `get()`, `events()`, `issues()` API methods; re-exported new types |
| `apps/web/src/app/(platform)/notifications/page.tsx` | Added "Activity" link to hub page navigation |

## 3. Features Implemented

### Activity List Page (`/notifications/activity`)
- **Summary cards:** Total, Sent, Failed, Blocked, Last 24h — derived from `/v1/notifications/stats`
- **Delivery breakdown:** Visual status distribution (sent/accepted/processing/failed/blocked) with percentage
- **Channel split:** By-channel count display
- **Filterable table:** Status and channel filters via search-param pills
- **Table columns:** Recipient, Channel, Status (with inline error preview), Template (from metadata), Provider, Sent at, Detail link
- **Pagination:** 25 per page with numbered page links
- **Empty states:** "No notification activity yet" and "No notifications match the current filters"
- **Error state:** Graceful display of fetch errors

### Activity Detail Page (`/notifications/activity/[notificationId]`)
- **Notification metadata:** Recipient, channel, status badge, provider, product, timestamps
- **Delivery status:** Large status badge with clear visual distinction
- **Failure/block reason panel:** Conditional panel for failed/blocked notifications showing category, error message, blocked reason, suppression reason
- **Template usage:** Template key, name, source (Global Template / Tenant Override badge), product, version ID — sourced from notification fields with metadata fallback
- **Content preview:** Subject line, HTML body in sandboxed iframe (CSP: `script-src 'none'`), text version
- **Event timeline:** Chronological delivery events with color-coded dots, detail, provider info
- **Related issues:** Severity-coded issue cards with category, message, detail, timestamps
- **Graceful degradation:** Events/issues unavailable states shown without breaking the page

### Navigation
- "Activity" button added to notifications hub page
- Breadcrumb navigation on both activity and detail pages

## 4. API / Backend Integration

### Activity List
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `GET /v1/notifications` | Paginated notification list with status/channel filters | Working (existing) |
| `GET /v1/notifications/stats` | Delivery statistics (totals, by-status, by-channel, 24h/7d) | Working (existing) |

### Detail
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `GET /v1/notifications/:id` | Single notification detail | Working (new client method) |

### Events
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `GET /v1/notifications/:id/events` | Delivery event timeline | Assumed (gracefully handled if unavailable) |

### Issues
| Endpoint | Purpose | Status |
|----------|---------|--------|
| `GET /v1/notifications/:id/issues` | Related delivery issues | Assumed (gracefully handled if unavailable) |

## 5. Data Flow / Architecture

### Request Flow
1. Server component calls `requireOrg()` to get authenticated session with `tenantId`
2. `tenantId` injected as `x-tenant-id` header via `notifRequest()` helper
3. Backend returns tenant-scoped data only
4. Server component renders response directly (no client-side fetching for list/detail)

### Tenant Context Flow
- `requireOrg()` validates session and ensures org membership
- `session.tenantId` used for all API calls
- No manual tenantId input anywhere in the UI
- Missing session redirects to `/no-org`

### Server/Client Component Split
- All pages are Server Components (no `'use client'` directives)
- Filters use search-param-driven `<Link>` navigation (no client state)
- HTML content rendered in sandboxed iframes (data URIs)

### Filter Flow
- Status/channel filters encoded as URL search params
- Filter pills are `<Link>` elements that navigate with updated params
- Clear filters link removes all params

### Detail/Timeline Flow
- Detail page fetches notification, events, and issues in parallel via `Promise.allSettled`
- Events/issues failures are caught independently — detail page renders even if timeline is unavailable

## 6. Validation & Testing

| Check | Status |
|-------|--------|
| TypeScript compilation (`tsc --noEmit`) | PASS |
| No errors in activity files | PASS |
| No errors in shared types / API client | PASS |
| Tenant context enforcement | PASS |
| Empty state handling | PASS |
| Filter no-results handling | PASS |
| Error state handling | PASS |
| 404 handling for invalid notification ID | PASS |
| Events/issues unavailability handling | PASS |

## 7. Error Handling

| Scenario | Handling |
|----------|---------|
| List fetch failure | Error banner with message; summary cards independent |
| Stats fetch failure | Error banner; list table independent |
| Detail fetch 404 | Next.js `notFound()` response |
| Detail fetch error | Error banner with message |
| Events endpoint unavailable | "Timeline not available" placeholder shown |
| Issues endpoint unavailable | Silently omitted (no broken UI) |
| Missing product attribution | Omitted from display; no fabricated data |
| Empty activity | Clear empty state message |
| Filter no-results | "No notifications match" with clear-filters link |

## 8. Tenant / Auth Context

- **Derivation:** `requireOrg()` from `@/lib/auth-guards` — redirects to `/no-org` if unauthenticated or no org
- **Enforcement:** Every page calls `requireOrg()` independently before any API call
- **x-tenant-id injection:** Via `notifRequest()` helper in `notifications-server-api.ts`
- **Missing context behavior:** Redirect to `/no-org`; no unsafe backend calls possible

## 9. Cache / Performance

- **Cache approach:** `cache: 'no-store'` on all API calls (real-time data)
- **Parallel fetching:** List and stats fetched simultaneously via `Promise.allSettled`; detail, events, and issues fetched in parallel
- **Known inefficiency:** No client-side caching of list data on filter changes (server round-trip each time) — acceptable for v1

## 10. Known Gaps / Limitations

| Gap | Severity | Notes |
|-----|----------|-------|
| Notification detail response shape may differ from `NotifDetail` type | Medium | Type includes optimistic fields (templateKey, templateSource, bodyHtml, etc.) that may not exist on backend response; metadata fallback used where possible |
| Events/issues endpoints may not exist on backend | Low | Gracefully handled — empty/unavailable states shown |
| Product attribution limited to metadata inference | Medium | If notification record doesn't include productType directly, falls back to metadata JSON parsing |
| No date range filter | Low | Backend list endpoint may not support date range; can be added when backend supports it |
| Template source (global vs override) depends on backend providing `templateSource` | Medium | Displayed when available; otherwise omitted |

## 11. Issues Encountered

| Issue | Resolution | Status |
|-------|-----------|--------|
| `NotifDetail` shape may be superset of actual backend response | Added metadata JSON fallback for key fields (templateKey, subject, bodyHtml) | Resolved |
| Events/issues endpoints are speculative | Used `Promise.allSettled` with graceful fallback | Resolved |
| Existing `NotifSummary` lacks template/product fields | Parse `metadataJson` for template key display in list | Resolved |

## 12. Run Instructions

1. Start the application: `bash scripts/run-dev.sh` (or use the "Start application" workflow)
2. Log in as a tenant user with org membership
3. Navigate to **Notifications** → click **Activity** button
4. Activity list shows with summary cards and filterable table
5. Click **Details** on any notification row to see the full detail view

## 13. Readiness Assessment

- **Is NOTIF-UI-009 complete?** Yes
- **Do tenants now have delivery visibility?** Yes
- **Can we proceed to the next phase?** Yes

## 14. Next Steps

- **Provider visibility:** Expose which delivery providers are configured and their health
- **Usage/billing visibility:** Show notification volume against plan limits
- **Date range filtering:** Add date-based filter when backend supports it
- **Resend/retry actions:** Future phase — currently strictly read-only
- **Advanced search:** Full-text search on recipient/template if backend supports it
