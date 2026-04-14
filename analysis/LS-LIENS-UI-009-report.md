# LS-LIENS-UI-009: Notifications Integration

## Feature ID
LS-LIENS-UI-009

## Objective
Integrate real notifications into SynqLiens using the established 5-file service layer pattern, replacing mock activity data with real API data.

## Backend Notification Service
- **Service**: `apps/services/notifications/` (.NET 8 microservice on port 5008)
- **Gateway routes**: `/notifications/{**catch-all}` → strips `/notifications` prefix → notifications-cluster
- **Tenant isolation**: Requires `X-Tenant-Id` header on all non-health endpoints
- **Endpoints used**:
  - `GET /v1/notifications` — paginated list (params: status, channel, limit, offset)
  - `GET /v1/notifications/stats` — delivery stats (total, byStatus, byChannel, last24h, last7d)
- **Not supported by backend**: Mark-as-read, unread count, dismiss (service is delivery-tracking, not user inbox)

## Architecture Decision: BFF Proxy Routes
The notification service requires `X-Tenant-Id` header which the client-side `apiClient` doesn't send. Created BFF proxy routes that:
1. Authenticate via `platform_session` cookie
2. Resolve `tenantId` from Identity service `/auth/me`
3. Inject `X-Tenant-Id` header
4. Proxy to notification service via gateway
5. Handle upstream failures with 503 fallback

### BFF Routes Created
- `apps/web/src/app/api/notifications/list/route.ts` — proxies `GET /v1/notifications`
- `apps/web/src/app/api/notifications/stats/route.ts` — proxies `GET /v1/notifications/stats`

## 5-File Service Layer
Located at `apps/web/src/lib/notifications/`:

| File | Purpose |
|------|---------|
| `notifications.types.ts` | DTOs (`NotifSummaryDto`, `NotifStatsDto`, etc.) and domain models (`NotificationItem`, `NotificationStats`, `NotificationListResult`, `NotificationQuery`) |
| `notifications.api.ts` | HTTP client calling BFF routes via `/api/notifications/*` with credentials |
| `notifications.mapper.ts` | Maps DTOs → domain models (parses JSON fields, formats timestamps, extracts metadata) |
| `notifications.service.ts` | Business-level functions: `getNotifications()`, `getRecentNotifications()`, `getStats()`, `getFailedCount()` |
| `index.ts` | Barrel export of service and types |

## UI Integration

### Notification Bell (TopBar)
- **Component**: `apps/web/src/components/shell/notification-bell.tsx`
- **Location**: TopBar between spacer and UserMenu
- **Features**:
  - Bell icon with red badge showing failed notification count
  - Dropdown panel with delivery stats summary (sent/failed/blocked/delivery rate)
  - Recent notification list with channel icons, status dots, recipient, time ago
  - Loading/error/empty states with retry action
  - Click-outside and Escape-key close
  - Links to notification detail pages and "View all" to `/notifications/activity`
  - ARIA labels and haspopup/expanded attributes

### Dashboard Recent Activity
- **File**: `apps/web/src/app/(platform)/lien/dashboard/page.tsx`
- **Change**: Replaced mock `MOCK_RECENT_ACTIVITY` from Zustand store with real notification API data
- **Features**:
  - Loading spinner, error state with retry, empty state
  - Each notification shows channel icon, subject/template, recipient, status, time ago
  - Links to notification detail pages
  - "View All" link to `/notifications/activity`

## Code Review Findings & Fixes
1. **Fixed**: BFF routes now wrap upstream `fetch` in try/catch, returning 503 on network failures
2. **Fixed**: NotificationBell has explicit `aria-label` with failed count context
3. **Noted**: BFF uses direct fetch rather than shared `apiClient` — intentional since `apiClient` doesn't support `X-Tenant-Id` injection and BFF routes are server-side Next.js route handlers (not client-side)

## Files Changed
- `apps/web/src/app/api/notifications/list/route.ts` (new)
- `apps/web/src/app/api/notifications/stats/route.ts` (new)
- `apps/web/src/lib/notifications/notifications.types.ts` (new)
- `apps/web/src/lib/notifications/notifications.api.ts` (new)
- `apps/web/src/lib/notifications/notifications.mapper.ts` (new)
- `apps/web/src/lib/notifications/notifications.service.ts` (new)
- `apps/web/src/lib/notifications/index.ts` (new)
- `apps/web/src/components/shell/notification-bell.tsx` (new)
- `apps/web/src/components/shell/top-bar.tsx` (modified — added NotificationBell import and usage)
- `apps/web/src/app/(platform)/lien/dashboard/page.tsx` (modified — replaced mock activity with real notification data)

## Status: COMPLETE
