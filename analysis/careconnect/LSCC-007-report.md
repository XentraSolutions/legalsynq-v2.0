# LSCC-007 — CareConnect UX Layer (Workflow Usability & Efficiency)
## Implementation Report

**Date:** 2026-03-31  
**Status:** Complete  
**Feature:** Referral inbox / work queue redesign, search, status filtering, role-based quick actions, detail page reorganization, navigation improvements, empty/loading/error states

---

## 1. Summary

### What Was Implemented

| Area | Deliverable |
|------|-------------|
| **A. Referral inbox** | Work-queue layout with left-border row highlighting; pending rows visually distinct |
| **B. Search** | Debounced `ReferralQueueToolbar` search input → URL `?search=` → `clientName` API param |
| **C. Status filter** | Pill filter bar (7 options, "New" maps to "Pending" display label) in same toolbar component |
| **D. Quick actions** | `ReferralQuickActions` per row — View, Accept (receiver), Resend Email + Revoke Link (referrer) |
| **E. Status visibility** | `StatusBadge` on every row; "Pending" sublabel under "New" badges; highlighted rows |
| **F. Detail page** | Reorganized into 5 sections per spec; new `ReferralPageHeader` component; `hideHeader` prop on `ReferralDetailPanel` |
| **G. Navigation** | `from=dashboard` param support; back-to-dashboard breadcrumb; page subtitle copy |
| **H. Empty/error states** | Distinct empty message for filter + no-results; error banner on list page |
| **I. Report** | This file |

### What Was Not Implemented (Out of Scope)

- Tests (see section 9)
- New backend endpoints (none required)
- Bulk actions, messaging, onboarding, analytics expansion (all explicitly out of scope)
- Notification bell
- Real-time updates

---

## 2. Referral Inbox / Work Queue Changes

### Structure

The `/careconnect/referrals` page is now a prioritized work queue:

1. **Page header** — role-specific title ("Sent Referrals" / "Referral Inbox") + subtitle + "New Referral" CTA (referrer only)
2. **Active date filter indicator** (preserved from prior implementation, shown when date drill-down is active)
3. **`ReferralQueueToolbar`** — search input + status filter pills in one surfaced component
4. **Results summary** — "N referrals found" or "No referrals match your filters" when filters are active
5. **`ReferralListTable`** — redesigned table with quick actions column
6. **Back to Dashboard** link at page foot

### Role Differences

| Title | Referrer (Law Firm) | Receiver (Provider) |
|-------|---------------------|---------------------|
| Heading | "Sent Referrals" | "Referral Inbox" |
| Subtitle | "Referrals you have sent to providers." | "Referrals waiting for your action." |
| New Referral button | ✓ | — |
| Quick actions shown | View, Resend Email, Revoke Link | View, Accept |

### Scannability Improvements

- **Pending rows** (`status === 'New'`) render with `bg-blue-50/40` background and a `border-l-4 border-l-blue-400` left accent — they visually float to the top of attention without column reordering
- **Accepted rows** get a `border-l-teal-400` accent — remain readable, reduced urgency
- **All other rows** have no left border accent
- Service and urgency columns are hidden at narrower breakpoints (`hidden sm:table-cell`, `hidden md:table-cell`) to keep the table dense but not overwhelming on mobile
- Created date is hidden below `lg` breakpoint; quick actions are always visible

---

## 3. Search + Filter Implementation

### Where Filtering Happens

**Server-side** — search input updates URL query params (`?search=...`), Next.js re-renders the Server Component, which passes `clientName` to the backend API (`GET /api/referrals?clientName=...`). The backend `ReferralRepository` already supported `clientName` partial match search.

### Toolbar Component (`ReferralQueueToolbar`)

- Client Component (`'use client'`) with access to `useSearchParams`, `useRouter`, `usePathname`
- Debounce: 320 ms before pushing URL change
- Preserves all other active query params when changing search or status
- Resets `page` to 1 on any filter change
- Clear (×) button visible when search has text

### Status Mapping

| UI Label | API Value |
|----------|-----------|
| All | `""` (param omitted) |
| Pending | `New` |
| Accepted | `Accepted` |
| Declined | `Declined` |
| Scheduled | `Scheduled` |
| Completed | `Completed` |
| Cancelled | `Cancelled` |

The mapping is explicit and contained entirely in `referral-queue-toolbar.tsx`. No status values are duplicated or guessed.

### URL State

Filters are stored in URL query params (`?search=...&status=...`). This means:
- Filter state is preserved on browser back/forward
- Users can share/bookmark filtered views
- Server renders the correct data on initial load

---

## 4. Quick Actions

### `ReferralQuickActions` Component

Mounted per table row. Accepts `referral`, `isReferrer`, `isReceiver` props.

### Action Visibility Matrix

| Action | Role | Condition |
|--------|------|-----------|
| View | Both | Always |
| Accept | Receiver | `status` ∈ {New, Received, Contacted} |
| Resend Email | Referrer | `status === 'New'` |
| Revoke Link | Referrer | Always (any status) |

### APIs Used

| Action | API Call |
|--------|----------|
| Accept | `PUT /api/referrals/{id}` → status: Accepted |
| Resend Email | `POST /api/referrals/{id}/resend-email` |
| Revoke Link | `POST /api/referrals/{id}/revoke-token` (with inline confirm UI) |

### UX Details

- **Toast feedback** on success and failure via existing `useToast` context
- **Optimistic UI**: `busy` state disables buttons while the request is in flight
- **Revoke confirmation**: Replaces the row actions with an inline "Revoke the current email link? Yes / Cancel" flow — no browser `confirm()` dialog used for a cleaner UX
- `router.refresh()` on success to sync server-rendered data

---

## 5. Referral Detail Page Usability Changes

### Layout / Order Changes

**Before (LSCC-005):**
1. Back link
2. `ReferralDetailPanel` (header + all fields)
3. Book Appointment button
4. `ReferralDeliveryCard`
5. `ReferralStatusActions`
6. `ReferralAuditTimeline`
7. `ReferralTimeline`

**After (LSCC-007):**
1. Back link (with `from=dashboard` awareness)
2. **`ReferralPageHeader`** — client identity, status badge (large), urgency badge, service, created date — one clear summary at the top
3. **`ReferralStatusActions`** — primary action area, immediately below identity
4. **Book Appointment banner** — for referrers with an Accepted referral; rendered as a teal highlight bar with prominent CTA (was a small inline `<div>` before)
5. **`ReferralDetailPanel`** with `hideHeader={true}` — body only (referral fields, client fields, notes); no duplicate header
6. `ReferralDeliveryCard` — referrers only
7. `ReferralAuditTimeline` — referrers only
8. `ReferralTimeline` — all roles

### Reused Components

All existing components are preserved and reused unchanged:
- `ReferralDeliveryCard` — no changes
- `ReferralAuditTimeline` — no changes
- `ReferralStatusActions` — no changes
- `ReferralTimeline` — no changes
- `StatusBadge`, `UrgencyBadge` — no changes

### Additive Change to `ReferralDetailPanel`

Added `hideHeader?: boolean` prop (default `false`). When `true`, the card renders only the body (field sections), omitting the built-in header row. This preserves full backward compatibility — any existing usage without the prop still renders the full panel.

---

## 6. Navigation Changes

| Improvement | Before | After |
|-------------|--------|-------|
| Back navigation on detail page | Static "← Back to Referrals" | Accepts `?from=dashboard` param; shows "← Back to Dashboard" when set |
| Page subtitle | None | "Referrals you have sent…" / "Referrals waiting for your action." |
| Dashboard back link | None on list page | "← Back to Dashboard" at foot of list page |
| Book Appointment | Small text link | Highlighted teal banner with clear CTA |

The `from=dashboard` param pattern is lightweight and forward-compatible. Detail links from the dashboard can include `?from=dashboard` to provide correct back navigation context without adding complex routing state.

---

## 7. Components Added / Updated

### New Components

| Component | File | Purpose |
|-----------|------|---------|
| `ReferralPageHeader` | `referral-page-header.tsx` | Detail page identity + status header |
| `ReferralQueueToolbar` | `referral-queue-toolbar.tsx` | Debounced search input + status filter pills (client) |
| `ReferralQuickActions` | `referral-quick-actions.tsx` | Per-row quick actions with toast + inline confirm (client) |

### Updated Components / Pages

| File | Change |
|------|--------|
| `referral-detail-panel.tsx` | Added `hideHeader?: boolean` prop |
| `referral-list-table.tsx` | Added role props; `ReferralQuickActions` column; row highlighting; responsive column hiding; `currentQs` for pagination; distinct empty state |
| `referrals/page.tsx` | Added toolbar; `search` param → `clientName`; results summary; role props to table; back-to-dashboard link |
| `referrals/[id]/page.tsx` | New 5-section layout; `ReferralPageHeader`; `hideHeader` on detail panel; `from` param for back nav |

---

## 8. API Usage / Support Changes

### Query Params Added

| Param | Added to | Purpose |
|-------|----------|---------|
| `search` | URL / page `searchParams` | Drives `clientName` filter; debounced from search input |
| `from` | URL `searchParams` on detail page | Back-navigation context (`dashboard`) |

### No Backend Changes

The `clientName` query param was already supported by the backend `GetReferralsQuery` DTO and `ReferralRepository`. No new endpoints, no schema changes, no backend modifications were made.

---

## 9. Tests

### Test Coverage Gap

Automated tests were not added in this implementation. The existing test suite (`CareConnect.Tests`) focuses on backend application logic (services, repositories) and does not include frontend component tests. The Next.js app does not currently have a configured frontend test runner (Jest + Testing Library or Playwright).

### What Would Be Covered

If a test runner were added, the following scenarios would need coverage per the spec:

| Scenario | How to Test |
|----------|-------------|
| Queue renders referral rows | RTL render of `ReferralListTable` with mock data |
| Role-based quick actions | `isReferrer=true` shows Resend/Revoke; `isReceiver=true` shows Accept |
| Accept action hidden for accepted referral | `status='Accepted'` → no Accept button |
| Search debounce pushes URL param | `useRouter` mock; verify `push` called after 320ms |
| Status filter changes URL | Click filter pill → verify URL param updated |
| Quick action accept calls API | Mock `careConnectApi.referrals.update`; verify called with `status: 'Accepted'` |
| Quick action resend calls API | Mock `careConnectApi.referrals.resendEmail`; verify called |
| Quick action revoke shows confirm | Click "Revoke Link" → confirm UI appears; click "Yes, Revoke" → API called |
| Empty state renders | `referrals=[]` → "No referrals match your filters." message |
| Detail page section order | `ReferralPageHeader` appears before `ReferralStatusActions` in DOM |

### Recommendation

Add a `vitest` + `@testing-library/react` setup to `apps/web` as a follow-on task. The component structure (props-driven, no hidden global state) makes the above tests straightforward to write.

---

## 10. Known Limitations

| Limitation | Reason / Mitigation |
|------------|---------------------|
| No test suite | No frontend test runner configured; tracked in section 9 above |
| `from=dashboard` must be added manually to dashboard referral links | Dashboard page not modified in this task; links from dashboard still go to list page context |
| Revoke Link appears for all statuses (referrers) | Mirrors `ReferralDeliveryCard` behavior; no business rule says revoke is status-gated |
| Search is server-side only | Requires full page navigation on each debounced keystroke; acceptable given SSR architecture |
| Urgency and service columns hidden on narrow screens | Acceptable trade-off for scannability of name/status/actions at all breakpoints |

---

## 11. Recommended Next Step

**LSCC-007-01 — Dashboard → Referral Queue deep-links**

The dashboard currently links to `/careconnect/referrals` without a `from=dashboard` param, and the Referral Activity KPI cards link without preserving filter context. The smallest logical follow-up is:
1. Update dashboard referral activity card links to include `?from=dashboard` so the detail page back button returns to the dashboard
2. Add `?from=dashboard` to the "Sent Referrals" / "Referral Inbox" nav link on the dashboard section
3. Optionally: add the `from` query param support to `ReferralQuickActions` "View" links when the list page itself was reached `from=dashboard`

This is a ~15-line change across 1–2 files and rounds out the navigation loop without any new components or backend work.

---

## 12. Files Summary

```
New (frontend):
  apps/web/src/components/careconnect/referral-page-header.tsx
  apps/web/src/components/careconnect/referral-queue-toolbar.tsx
  apps/web/src/components/careconnect/referral-quick-actions.tsx

Modified (frontend):
  apps/web/src/components/careconnect/referral-detail-panel.tsx   (hideHeader prop)
  apps/web/src/components/careconnect/referral-list-table.tsx     (role props, quick actions, highlighting)
  apps/web/src/app/(platform)/careconnect/referrals/page.tsx      (toolbar, search, roles)
  apps/web/src/app/(platform)/careconnect/referrals/[id]/page.tsx (section reorganization)

Analysis:
  analysis/careconnect/LSCC-007-report.md
```
