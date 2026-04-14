# LS-LIENS-UI-011: Provider Mode (Sell vs Manage Internally)

## Feature ID
LS-LIENS-UI-011

## Objective
Enable dynamic product behavior based on provider mode sourced from a tenant/org configuration API. Two modes: `sell` (marketplace-enabled) and `manage` (internal lien management only). Mode is organization-level, not per-user.

## Final Mode Source Decision
**Tenant/Org Configuration API** via `GET /identity/api/organizations/my/config` endpoint.

## Expected Org Config Contract
```json
{
  "organizationId": "org-123",
  "productCode": "LIENS",
  "settings": {
    "providerMode": "sell"
  }
}
```

Fallback: If `providerMode` is missing, invalid, or API is unavailable → defaults to `sell`.

---

## Implementation Log

### T001 — Mode Source Analysis + Backend Endpoint
**Status:** COMPLETE
- **Analysis:** Investigated Identity, Liens, CareConnect, and Notifications services. No existing org-level config endpoint for provider mode found.
- **Backend addition:** Added `GET /api/organizations/my/config` to `Identity.Api/Endpoints/AuthEndpoints.cs`
- **Behavior:** Reads caller's `org_id` from JWT claims, looks up active org in DB. Returns `{ organizationId, productCode: "LIENS", settings: { providerMode: "sell" } }`. Currently defaults to `sell` — future DB column will back this.
- **Files modified:** `apps/services/identity/Identity.Api/Endpoints/AuthEndpoints.cs`

### T002 — Provider Mode Types
**Status:** COMPLETE
- Created `provider-mode.types.ts` with:
  - `ProviderMode = 'sell' | 'manage'`
  - `OrgConfigSettingsDto` (raw API settings shape)
  - `OrgConfigResponseDto` (full API response)
  - `ProviderModeInfo` (normalized frontend mode)
- **Files:** `apps/web/src/lib/provider-mode/provider-mode.types.ts`

### T003 — Provider Mode API Layer
**Status:** COMPLETE
- **BFF route:** `apps/web/src/app/api/org-config/route.ts`
  - Reads `platform_session` cookie, calls `${GATEWAY_URL}/identity/api/organizations/my/config` with Bearer auth
  - Returns fallback `{ providerMode: "sell" }` on any failure
- **Client API:** `apps/web/src/lib/provider-mode/provider-mode.api.ts`
  - `fetchOrgConfig()` — calls BFF `/api/org-config`, returns fallback on error
- **Files:** `apps/web/src/app/api/org-config/route.ts`, `apps/web/src/lib/provider-mode/provider-mode.api.ts`

### T004 — Provider Mode Service Layer
**Status:** COMPLETE
- `resolveProviderMode(config)` — normalizes raw API response to `ProviderModeInfo`
- `getDefaultModeInfo()` — returns default `sell` mode
- `isSellMode()` / `isManageMode()` — convenience functions
- Invalid values normalize to `sell`
- **Files:** `apps/web/src/lib/provider-mode/provider-mode.service.ts`

### T005 — App-level Integration (Context/Provider)
**Status:** COMPLETE
- Created `ProviderModeProvider` context at `apps/web/src/providers/provider-mode-provider.tsx`
- Wraps app in root layout (`apps/web/src/app/layout.tsx`)
- Fetches org config once when session is available, caches via React state
- `useProviderModeContext()` provides mode info to all descendants
- `useProviderMode()` hook at `apps/web/src/hooks/use-provider-mode.ts` wraps context access
- **Provider order:** TenantBranding → Session → ProviderMode → children
- **Files:** `apps/web/src/providers/provider-mode-provider.tsx`, `apps/web/src/hooks/use-provider-mode.ts`, `apps/web/src/app/layout.tsx`

### T006 — Navigation Updates
**Status:** COMPLETE
- Added `sellModeOnly` flag to `NavItem` and `NavSection` types
- Bill of Sales nav item: `sellModeOnly: true`
- MARKETPLACE section: `sellModeOnly: true` (entire section hidden in manage mode)
- Sidebar component filters sections and items by `sellModeOnly` using provider mode
- **Sell mode:** Full navigation (MY TASKS incl. BOS, MARKETPLACE, MY TOOLS, SETTINGS)
- **Manage mode:** Simplified navigation (no BOS, no MARKETPLACE section)
- **Files:** `apps/web/src/types/index.ts`, `apps/web/src/lib/nav.ts`, `apps/web/src/components/shell/sidebar.tsx`

### T007 — Lien Detail Behavior by Mode
**Status:** COMPLETE
- **Sell mode:** Full marketplace behavior — Submit Offer button, Offers panel, pending-offers banner, Offer Price + Purchase Price KPI cards, offer modal, lifecycle shows Negotiation/Sold steps
- **Manage mode:** Internal tracking only — no offer UI, single Original Amount KPI card, simplified lifecycle (Draft→Active→Closed)
- Offer data fetch skipped entirely in manage mode (no unnecessary API call)
- Data fetch waits for `modeReady` to prevent double-fetch
- **Files:** `apps/web/src/app/(platform)/lien/liens/[id]/page.tsx`

### T008 — Lien Creation Flow by Mode
**Status:** COMPLETE
- Lien creation modal remains unchanged — creation is a data entry operation common to both modes
- In manage mode, created liens are for internal management only (no marketplace exposure)
- No sell-related affordances in the creation form (there never were — offers come later in lifecycle)
- **Files:** No changes needed — `create-lien-modal.tsx` is mode-agnostic by design

### T009 — Bill of Sale / Settlement Gating
**Status:** COMPLETE
- BOS list page: redirects to dashboard in manage mode (defensive route protection)
- BOS nav item: hidden via `sellModeOnly: true`
- BOS quick action on dashboard: hidden via `sellOnly: true` filter
- Hooks called unconditionally before early return (React rules compliance)
- **Files:** `apps/web/src/app/(platform)/lien/bill-of-sales/page.tsx`

### T010 — Unified Activity Adaptation
**Status:** COMPLETE
- Added `filterActivityByMode()` and `isSellModeActivity()` utilities
- Sell-mode event patterns: `offer`, `bos`, `bill_of_sale`, `settlement`, `marketplace`, `sold`, `purchase`
- Manage mode: filters out sell-only activity from unified feed (dashboard + activity page)
- Does NOT alter audit source truth — only UI presentation filtering
- **Files:** `apps/web/src/lib/unified-activity/unified-activity.types.ts`, `apps/web/src/lib/unified-activity/index.ts`, `apps/web/src/app/(platform)/lien/activity/page.tsx`, `apps/web/src/app/(platform)/lien/dashboard/page.tsx`

### T011 — Notifications Adaptation
**Status:** COMPLETE
- Notifications data model does not contain sell-specific categories in current schema
- Notification channels (email, sms, push, in-app) and statuses (sent, failed, blocked) are operational, not sell-specific
- The unified activity filter already handles any audit events that might be sell-related
- No additional notification-specific changes needed — operational notifications (servicing, case, document, contact updates) pass through in both modes
- **Files:** No changes needed — notification service is mode-agnostic

### T012 — Mode Indicator in UI
**Status:** COMPLETE
- Dashboard header shows mode badge: "Sell Mode" (emerald) or "Internal Mode" (slate)
- Small, non-intrusive pill badge next to "Dashboard" heading
- Consistent with existing styling patterns
- **Files:** `apps/web/src/app/(platform)/lien/dashboard/page.tsx`

### T013 — Remove Unsupported Actions in Manage Mode
**Status:** COMPLETE
Actions removed/hidden in manage mode:
- Submit Offer button (lien detail)
- Accept Offer button (lien detail offers panel)
- Offers panel (entire section)
- Pending offers action-required banner
- Offer Price / Purchase Price KPI cards
- Offer column in liens list table and preview drawer
- Offered/Sold status filter options in liens list
- Bill of Sale quick action on dashboard
- Bill of Sales navigation item
- MARKETPLACE navigation section
- Negotiation/Sold lifecycle steps
All use clean omission (not disabled states).
- **Files:** Multiple (see T006, T007, T009 above)

### T014 — Validation and Regression Checks
**Status:** COMPLETE
- `tsc --noEmit` passes with zero errors (frontend)
- `dotnet build` passes for Identity.Api (backend)
- Sell mode: fully intact — all marketplace/offer/BOS features remain
- Manage mode: all sell-only UI omitted; no broken navigation; no stray offer actions
- No broken entity pages — lien detail, liens list, dashboard, activity all compile and render
- BOS page has defensive route guard redirecting to dashboard

---

## Executive Summary

### Mode Source
Org-level configuration from `GET /identity/api/organizations/my/config`. BFF proxy at `/api/org-config`. Currently defaults to `sell` (no DB column yet).

### Files Created
| File | Purpose |
|------|---------|
| `apps/web/src/lib/provider-mode/provider-mode.types.ts` | Type definitions |
| `apps/web/src/lib/provider-mode/provider-mode.api.ts` | Client API (BFF fetch) |
| `apps/web/src/lib/provider-mode/provider-mode.service.ts` | Mode resolution logic |
| `apps/web/src/lib/provider-mode/index.ts` | Barrel export |
| `apps/web/src/app/api/org-config/route.ts` | BFF proxy route |
| `apps/web/src/providers/provider-mode-provider.tsx` | React context provider |

### Files Modified
| File | Changes |
|------|---------|
| `Identity.Api/Endpoints/AuthEndpoints.cs` | Added `GET /api/organizations/my/config` |
| `apps/web/src/app/layout.tsx` | Added `ProviderModeProvider` to provider chain |
| `apps/web/src/hooks/use-provider-mode.ts` | Rewired to use context (not session roles) |
| `apps/web/src/types/index.ts` | Added `sellModeOnly` to NavItem/NavSection |
| `apps/web/src/lib/nav.ts` | BOS + MARKETPLACE gated by `sellModeOnly` |
| `apps/web/src/components/shell/sidebar.tsx` | Mode-aware nav filtering |
| `apps/web/src/app/(platform)/lien/liens/[id]/page.tsx` | Offer/sale UI gating |
| `apps/web/src/app/(platform)/lien/liens/page.tsx` | Offer column + status filter gating |
| `apps/web/src/app/(platform)/lien/dashboard/page.tsx` | Mode badge, quick action gating, activity filtering |
| `apps/web/src/app/(platform)/lien/bill-of-sales/page.tsx` | Defensive route guard |
| `apps/web/src/app/(platform)/lien/activity/page.tsx` | Activity mode filtering |
| `apps/web/src/lib/unified-activity/unified-activity.types.ts` | `filterActivityByMode()` utility |
| `apps/web/src/lib/unified-activity/index.ts` | Export filter utility |

### Sell Mode Behavior
Full feature set: marketplace nav, offers, BOS, settlement, offer/purchase KPI cards, full lifecycle steps, all status filters, sell-related activity visible.

### Manage Mode Behavior
Internal management only: no marketplace nav, no offers, no BOS, single amount KPI, simplified lifecycle (Draft→Active→Closed), limited status filters, sell-related activity hidden from unified feed.

### Risks/Issues
1. **Backend providerMode is static `sell`** — no DB column yet. Future task required to add `ProviderMode` column to Organization entity.
2. **Route protection is client-side** — BOS page redirects in manage mode but backend APIs still accept requests. Backend gating is a future concern.
3. **Activity filter is pattern-based** — uses string matching on event types; if new sell-related event types are introduced, patterns must be updated.
