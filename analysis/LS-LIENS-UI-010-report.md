# LS-LIENS-UI-010: Unified Activity Feed

## Feature ID
LS-LIENS-UI-010

## Objective
Combine real audit and notification events into one unified activity experience using the established 5-file service layer architecture pattern.

---

## T001 — Initial Analysis

### Audit Service (existing, LS-LIENS-UI-008)
- **Location**: `apps/web/src/lib/audit/`
- **API endpoint**: `GET /audit-service/audit/events` (all events, paginated) and `GET /audit-service/audit/entity/{type}/{id}` (entity-scoped)
- **Client**: Uses `apiClient` (goes through gateway rewrite)
- **Key types**: `AuditEventRecordDto` → `TimelineItem`
- **Features**: actor, entity, action, description, timestamp, severity, category

### Notification Service (existing, LS-LIENS-UI-009)
- **Location**: `apps/web/src/lib/notifications/`
- **API endpoint**: `GET /api/notifications/list` (BFF proxy → gateway → notification service)
- **Client**: Uses BFF routes (injects X-Tenant-Id from session)
- **Key types**: `NotifSummaryDto` → `NotificationItem`
- **Features**: channel, status, recipient, subject/template, error, timestamp

### Existing Activity UI Surfaces
- **Dashboard "Recent Activity"** — was showing notifications only (LS-LIENS-UI-009)
- **Entity Timeline** (`entity-timeline.tsx`) — shows audit events per entity
- **Notification bell** (TopBar) — notification deliveries dropdown
- **`/notifications/activity`** — full notification log page

### Analysis Decision
- Create a unified activity feed service layer that merges audit events + notification events
- Add a new `/lien/activity` page as the full unified activity feed
- Replace the dashboard "Recent Activity" to show combined audit + notification data
- Each item retains its source badge (Audit / Notification) for clarity

**Status**: COMPLETE

---

## T002 — Types Layer

**File**: `apps/web/src/lib/unified-activity/unified-activity.types.ts`

### Types Created
- `ActivitySource` — `'audit' | 'notification'`
- `ActivityEntityRef` — `{ type, id }` for navigable entity references
- `ActivityActorRef` — `{ name, type }` for actor display
- `UnifiedActivityItem` — unified model with `id`, `source`, `title`, `description`, `actor`, `entity`, `timestamp`, `icon`, `iconColor`, `severity`, `sourceDetail`
- `AuditSourceDetail` — audit-specific fields (eventType, category, action, entityType, entityId, severity)
- `NotificationSourceDetail` — notification-specific fields (channel, status, recipient, templateKey, subject, isFailed, isBlocked, errorMessage)
- `UnifiedActivityQuery` — `{ source?, limit?, page? }`
- `UnifiedActivityResult` — `{ items, hasMore }`

**Status**: COMPLETE

---

## T003 — API Layer

**File**: `apps/web/src/lib/unified-activity/unified-activity.api.ts`

### Implementation
- Imports and delegates to existing `auditApi.getEvents()` and `notificationsApi.list()`
- No direct HTTP calls — reuses established API clients
- `fetchAuditEvents(page, pageSize)` → returns raw `ApiResponseWrapper<AuditEventQueryResponseDto>`
- `fetchNotifications(limit, offset)` → returns raw `NotifListResponseDto`

**Status**: COMPLETE

---

## T004 — Mapper Layer

**File**: `apps/web/src/lib/unified-activity/unified-activity.mapper.ts`

### Mappings
- `mapAuditToUnified(dto)` — maps `AuditEventRecordDto` → `UnifiedActivityItem` with audit-specific icon/color based on `eventCategory`, extracts entity/actor references
- `mapNotificationToUnified(dto)` — maps `NotifSummaryDto` → `UnifiedActivityItem` with channel icon, status color, parsed recipient/metadata
- `getEntityHref(entity)` — resolves entity type to navigable route (Case → `/lien/cases/{id}`, Lien → `/lien/liens/{id}`, etc.)
- `getNotificationHref(notifId)` — resolves to `/notifications/activity/{id}`

### Entity Type Route Map
| Entity Type | Route |
|---|---|
| Case | `/lien/cases/{id}` |
| Lien | `/lien/liens/{id}` |
| ServicingItem | `/lien/servicing/{id}` |
| BillOfSale | `/lien/bill-of-sales/{id}` |
| Contact | `/lien/contacts/{id}` |
| Document | `/lien/document-handling/{id}` |

**Status**: COMPLETE

---

## T005 — Service Layer

**File**: `apps/web/src/lib/unified-activity/unified-activity.service.ts`

### Merge/Sort Logic
1. **Single-source filter**: When `source` is specified, fetch only that source using its native pagination. `hasMore` is derived from the source's own pagination metadata (`audit.hasNext` / `notif.meta.total`).
2. **All-sources (no filter)**: Fetch both sources in parallel via `Promise.allSettled()`. Merge results, sort by `timestampRaw` descending, trim to `limit`. `hasMore` is true if either source reports more pages.
3. Sort is always by timestamp descending (most recent first).

### Resilience Strategy
- Uses `Promise.allSettled()` — both sources are fetched in parallel, failures are isolated
- If one source fails and the other succeeds, returns partial results (graceful degradation)
- Only throws if BOTH sources fail
- Single-source filter: throws directly if that source fails (no false empty state)

### Code Review Fixes Applied
- **Pagination**: Single-source queries now use native pagination metadata (`hasNext` from audit, `total` from notifications) instead of incorrect `sorted.length > limit`
- **Error handling**: Single-source filter now propagates errors (throws) instead of silently returning empty results. All-sources uses `Promise.allSettled()` for parallel execution with proper failure isolation
- **hasMore accuracy**: For all-sources, `hasMore` is `auditResult.hasMore || notifResult.hasMore` (either source having more = more to load)

### Exposed Functions
- `getUnifiedActivity(query)` — full unified feed with source filtering + pagination
- `getRecentUnifiedActivity(limit)` — shortcut for dashboard summary
- `getUnifiedActivityBySource(source, limit, page)` — filtered by single source

**Status**: COMPLETE

---

## T006 — Unified Activity UI Surface

**File**: `apps/web/src/app/(platform)/lien/activity/page.tsx`

### Implementation
- Full-page activity feed at `/lien/activity`
- Source filter tabs: All Sources / Audit / Notification
- Each item shows: source-aware icon + color, title, description, source badge, actor, time ago, entity type tag
- Loading spinner, error with retry, empty state
- "Load more" pagination
- Clickable items navigate to entity detail pages (audit) or notification detail pages (notification)
- Items without navigable targets render as non-link divs

**Status**: COMPLETE

---

## T007 — Dashboard / Summary Integration

**File**: `apps/web/src/app/(platform)/lien/dashboard/page.tsx`

### Changes
- Replaced notifications-only "Recent Activity" with unified activity (audit + notifications merged)
- Uses `unifiedActivityService.getRecentUnifiedActivity(6)` for 6 most recent items
- Each item shows source badge (Audit / Notification), icon, description, actor, time ago
- "View All" link updated from `/notifications/activity` to `/lien/activity`
- Loading/error/empty states preserved
- Clickable items route to entity detail or notification detail based on source

**Status**: COMPLETE

---

## T008 — Entity Navigation

### Implementation
- Audit items: `getEntityHref()` resolves entity type + id → route (6 supported entity types)
- Notification items: `getNotificationHref()` → `/notifications/activity/{id}`
- Unsupported entity types or missing entity data → item renders as non-clickable div (no broken links)
- No 404 risk from null/undefined entity references

**Status**: COMPLETE

---

## T009 — Source-Aware Filtering

### Implementation
- Activity feed page has 3 filter buttons: All Sources, Audit, Notification
- Filter passed as `source` parameter to `getUnifiedActivity()` query
- When filtered by single source, only that source is fetched (avoids unnecessary API call)
- No unsupported global filters exposed (no fake date range, no fake search that can't work across both sources)

**Status**: COMPLETE

---

## T010 — Remove Mock Data

### Changes
- `MOCK_RECENT_ACTIVITY` import removed from `lien-store.ts`
- Store `activity` field initialized as empty array `[]` instead of `MOCK_RECENT_ACTIVITY.map(...)`
- `MOCK_RECENT_ACTIVITY` still exported from `lien-mock-data.ts` (dead code, harmless) but no longer imported anywhere
- `addActivity` still works as a local fire-and-forget logger (called by internal CRUD actions) but doesn't seed with mock data
- No component reads `store.activity` state — all UI surfaces now use unified activity service

**Status**: COMPLETE

---

## T011 — Unsupported Features

### Documented Gaps
1. **Global unread/read state**: Not supported. Neither audit nor notification backend has mark-as-read endpoints. No fake unread count is displayed.
2. **Global deduplication**: Not performed. Audit events and notifications are fundamentally different data (audit = system events, notifications = delivery tracking). No risk of duplicates since IDs are prefixed (`audit-`, `notif-`).
3. **True global pagination**: Not perfectly supported. Each source has its own pagination model (audit: page/pageSize, notification: limit/offset). The unified service fetches equal amounts from each source and merges client-side. "Load more" fetches the next page from both sources. For large datasets, items may interleave imperfectly — documented as known limitation.
4. **Cross-source search**: No global text search across both sources. Neither API supports full-text search consistently.
5. **Real-time updates**: No WebSocket/SSE push. Data refreshes on page load and manual retry.

**Status**: COMPLETE

---

## T012 — Final Validation

### Build Verification
- TypeScript compilation: **0 errors** (`npx tsc --noEmit`)
- Application restart: **successful** (workflow running)
- No new console errors introduced

### Architecture Validation

#### Data Flow (Before)
```
Dashboard "Recent Activity" → notificationsService.getRecentNotifications() → BFF → Gateway → Notification Service
Entity Timeline → auditService.getEntityTimeline() → apiClient → Gateway → Audit Service
```

#### Data Flow (After)
```
Dashboard "Recent Activity" → unifiedActivityService.getRecentUnifiedActivity()
  ├─ auditApi.getEvents() → apiClient → Gateway → Audit Service
  └─ notificationsApi.list() → BFF → Gateway → Notification Service
  → merge + sort by timestamp → UnifiedActivityItem[]

Activity Feed Page → unifiedActivityService.getUnifiedActivity(query)
  ├─ auditApi.getEvents() → apiClient → Gateway → Audit Service
  └─ notificationsApi.list() → BFF → Gateway → Notification Service
  → merge + sort + filter by source → UnifiedActivityItem[]

Entity Timeline → auditService.getEntityTimeline() (unchanged)
Notification Bell → notificationsService (unchanged)
```

### Endpoints Used
| Endpoint | Source | Access Method |
|---|---|---|
| `GET /audit-service/audit/events` | Audit | `apiClient` (gateway rewrite) |
| `GET /api/notifications/list` | Notification | BFF proxy (injects X-Tenant-Id) |

### Service Layer Pattern Compliance
| Layer | File | Pattern |
|---|---|---|
| Types | `unified-activity.types.ts` | Unified DTOs + domain models |
| API | `unified-activity.api.ts` | Delegates to existing audit + notification APIs |
| Mapper | `unified-activity.mapper.ts` | DTO → unified model, entity routing, icon/color |
| Service | `unified-activity.service.ts` | Merge/sort logic, source filtering, resilience |
| Index | `index.ts` | Barrel exports |

### Files Created
- `apps/web/src/lib/unified-activity/unified-activity.types.ts`
- `apps/web/src/lib/unified-activity/unified-activity.api.ts`
- `apps/web/src/lib/unified-activity/unified-activity.mapper.ts`
- `apps/web/src/lib/unified-activity/unified-activity.service.ts`
- `apps/web/src/lib/unified-activity/index.ts`
- `apps/web/src/app/(platform)/lien/activity/page.tsx`

### Files Modified
- `apps/web/src/app/(platform)/lien/dashboard/page.tsx` — replaced notification-only activity with unified activity
- `apps/web/src/stores/lien-store.ts` — removed `MOCK_RECENT_ACTIVITY` import, initialized `activity` as empty array

### Risks / Blockers
- **Pagination approximation**: Merged pagination fetches equal pages from both sources. With large volume skew (e.g. 10k audit events, 50 notifications), later pages may show only one source's data.
- **No real-time push**: Activity feed requires manual refresh/navigation to update.
- **No mark-as-read**: Backend doesn't support it; no fake state implemented.

## Status: COMPLETE
