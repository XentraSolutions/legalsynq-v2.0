# LSCC-003 — CareConnect Workflow UI Completion

**Date:** 2026-03-31
**Status:** Complete
**Tests:** 177 passed, 0 failed (up from 158 before this task — 19 new tests added)

---

## 1. Summary

### What Was Implemented

LSCC-003 completed the full CareConnect end-to-end user workflow UI on top of the stable backend (LSCC-001 / LSCC-002 / LSCC-002-01). All eight sections of the spec were addressed.

The major work was:

| Item | Status | Notes |
|------|--------|-------|
| Dashboard (role-aware) | ✅ Implemented | Was `<BlankPage />` stub |
| Provider discovery (map + list) | ✅ Already complete | No changes needed |
| Provider detail + availability preview | ✅ Enhanced | Added `ProviderAvailabilityPreview` component |
| Referral creation flow | ✅ Already complete | Full modal with validation |
| Referral detail + timeline | ✅ Enhanced | Added real Accept/Decline actions |
| Provider referral inbox (accept/decline) | ✅ Implemented | `ReferralStatusActions` component |
| Appointment booking flow | ✅ Already complete | Slot picker → BookingPanel → redirect |
| Appointment detail + cancel | ✅ Enhanced | Added `AppointmentCancelButton` component |
| Navigation | ✅ Fixed | Appointments link added to CareConnect sidebar |
| Status filters | ✅ Fixed | Referral chips now use backend enum values |
| Status badge colors | ✅ Fixed | Added Accepted, Declined, NoShow colors |
| API layer additions | ✅ Complete | `referrals.update`, `appointments.cancel` |
| Tests | ✅ 19 new tests | `WorkflowIntegrationTests.cs` |

### What Remains Incomplete

- **Reschedule appointment:** `POST /api/appointments/{id}/reschedule` endpoint exists on the backend but no UI is wired up. Scheduled for LSCC-003-01.
- **Confirm appointment:** `PUT /api/appointments/{id}` with `status=Confirmed` is not surfaced in the UI. Scheduled for LSCC-003-01.
- **Referral timeline:** The `GET /api/referrals/{id}/history` endpoint exists but is not displayed in `ReferralDetailPanel`. History is read-only notes. Scheduled for LSCC-003-01.
- **Dashboard stat counts:** The stat bar shows live counts for referrals and appointments, but "New This Week" and "Completed" stats show `—` (would require date-filtered API calls). Scheduled for LSCC-003-01.

---

## 2. Pages Implemented

| Route | Component | Type | Status |
|-------|-----------|------|--------|
| `/careconnect/dashboard` | `DashboardPage` | Server | ✅ New |
| `/careconnect/providers` | `ProvidersPage` + `ProviderMapShell` | Server + Client | ✅ Pre-existing |
| `/careconnect/providers/[id]` | `ProviderDetailPage` | Client | ✅ Enhanced |
| `/careconnect/providers/[id]/availability` | `AvailabilityPage` | Client | ✅ Pre-existing |
| `/careconnect/referrals` | `ReferralsPage` | Server | ✅ Pre-existing + fixed |
| `/careconnect/referrals/[id]` | `ReferralDetailPage` | Server | ✅ Enhanced |
| `/careconnect/appointments` | `AppointmentsPage` | Server | ✅ Pre-existing |
| `/careconnect/appointments/[id]` | `AppointmentDetailPage` | Server | ✅ Enhanced |

---

## 3. API Integrations

### Endpoints Used Per Flow

**Dashboard**
- `GET /api/referrals?pageSize=5` — active referral list (referrer view)
- `GET /api/appointments?status=Scheduled&pageSize=5` — upcoming appointments (referrer view)
- `GET /api/referrals?status=New&pageSize=5` — pending referrals (receiver view)
- `GET /api/appointments?pageSize=20` — filtered client-side for today (receiver view)

**Provider Discovery**
- `GET /api/providers?name=…&city=…&categoryCode=…&page=…` — server-rendered list
- `GET /api/providers/map?…` — client-side markers for map view

**Provider Detail**
- `GET /api/providers/{id}` — provider details
- `GET /api/providers/{id}/availability?from=…&to=…` — availability preview (next 5 slots, 2-week window)

**Referral Creation**
- `POST /api/referrals` — create referral, redirect to `/referrals/{id}`

**Referral Detail + Accept/Decline**
- `GET /api/referrals/{id}` — server-rendered referral
- `PUT /api/referrals/{id}` — Accept, Decline, Cancel (client-side, status-driven)

**Appointment Booking**
- `GET /api/providers/{id}/availability?from=…&to=…` — fetch slots
- `POST /api/appointments` — create appointment with `referralId + appointmentSlotId`
- Redirect to `/careconnect/appointments/{id}`

**Appointment Detail + Cancel**
- `GET /api/appointments/{id}` — server-rendered detail with status history
- `POST /api/appointments/{id}/cancel` — cancel with optional notes

---

## 4. Role-Based Behavior

### CARECONNECT_REFERRER (Law Firm — HARTWELL users)

| Feature | Behavior |
|---------|----------|
| Dashboard | Active referrals + upcoming appointments + "Find Providers" CTA |
| Provider search | Full access — `/careconnect/providers` |
| Provider detail | Availability preview + "Create Referral" CTA |
| Referral list | Heading: "Sent Referrals", shows own outbound referrals |
| Referral detail | "Book Appointment" button when status = Accepted |
| Referral detail actions | Can cancel referral |
| Appointment list | Heading: "Sent Appointments" |
| Appointment detail | Can cancel appointment |

### CARECONNECT_RECEIVER (Provider Group — MERIDIAN users)

| Feature | Behavior |
|---------|----------|
| Dashboard | Pending referrals (status=New) + today's appointments + "Referral Inbox" CTA |
| Provider search | Redirected to `/dashboard` (receivers don't search for other providers) |
| Referral list | Heading: "Received Referrals", shows inbound referrals |
| Referral detail | Accept and Decline buttons with optional decline notes |
| Referral detail actions | Can also cancel referral |
| Appointment list | Heading: "Incoming Appointments" |
| Appointment detail | Can cancel appointment |

### TenantAdmin Bypass

TenantAdmins (margaret@hartwell.law, dr.ramirez@meridiancare.com) bypass all capability checks and see the referrer view by default on the dashboard, since the `isReferrer || !isReceiver` guard defaults to the referrer view.

---

## 5. Booking Flow Details

**Entry point:** Provider detail page → "Create Referral" → referral created → "Book Appointment" (visible when status=Accepted) → availability page.

**Steps:**
1. `/careconnect/providers/{id}` — user clicks "Create Referral"
2. `CreateReferralForm` modal → `POST /api/referrals` → redirect to `/careconnect/referrals/{id}`
3. Receiver accepts via `ReferralStatusActions` → `PUT /api/referrals/{id}` with `status=Accepted`
4. Referrer sees "Book Appointment" button on referral detail (only shown when status=Accepted)
5. `/careconnect/providers/{id}/availability?referralId={id}` — slot picker
6. `AvailabilityList` + `SlotPicker` → user selects slot
7. `BookingPanel` modal pre-populates client fields from referral → `POST /api/appointments`
8. Redirect to `/careconnect/appointments/{id}`

**Slot conflict handling:** If `POST /api/appointments` returns 409 Conflict, `BookingPanel` shows "This slot has just been booked by someone else. Please select a different time."

**Backend appointment DTO:** The backend `CreateAppointmentRequest` only needs `ReferralId`, `AppointmentSlotId`, and optional `Notes`. Client info is sourced from the referral by the backend service.

---

## 6. Known Limitations

1. **Referral status displayed as filter "New" not "Pending":** The backend uses `New` as the initial status (corrected in this task — previously the UI showed "Pending" which did not match).

2. **Availability slots are mock data from the backend:** The CareConnect backend generates time slots from its own availability service. Real provider schedules are not connected.

3. **Appointment booking requires an existing referral ID + slot ID:** The booking form does not support ad-hoc appointments without a referral in the current backend DTO (`CreateAppointmentRequest` requires `ReferralId`). The frontend type supports optional `referralId` but the backend requires it.

4. **Map markers use static coordinates (Las Vegas, NV):** All 14 test providers are seeded with coordinates in Las Vegas. The map will center on Las Vegas.

5. **Dashboard stat counts for "New This Week" and "Completed" show `—`:** Live counts would require additional API params not currently exposed (date range filters on the count endpoint).

---

## 7. UI Gaps and Follow-ups

| Gap | Impact | Notes |
|-----|--------|-------|
| Referral status timeline (history) | Medium | `GET /referrals/{id}/history` exists but not displayed |
| Appointment Confirm action | Low | Backend supports it via `PUT /appointments/{id}` |
| Appointment Reschedule action | Low | Backend endpoint exists, no UI |
| Provider map cluster mode | Low | Many markers in same area collapse; no clustering logic |
| Dashboard stat counts (date-filtered) | Low | Requires date-range query params |
| Referral pagination on dashboard | Low | Dashboard shows top 5; no "load more" |
| Referrer can see all referrals even Declined | Low | Dashboard could add a `status!=Declined` filter |

---

## 8. Recommended LSCC-003-01 Improvements

### Priority 1 — Complete the workflow
1. **Appointment Confirm/NoShow** — Wire `PUT /api/appointments/{id}` for status=Confirmed and status=NoShow. Receiver-only action.
2. **Appointment Reschedule** — Wire `POST /api/appointments/{id}/reschedule` with a new slot picker.
3. **Referral status history timeline** — Add a `ReferralTimeline` component that calls `GET /api/referrals/{id}/history` and renders it like the `AppointmentTimeline`.

### Priority 2 — Polish
4. **Dashboard live stat counts** — Add date-filtered referral/appointment counts using `createdFrom`/`createdTo` params.
5. **Provider map clustering** — Implement marker clustering for the Las Vegas cluster (Leaflet.markercluster or Mapbox clustering).
6. **Booking without referral** — Backend DTO update to make `ReferralId` optional, enabling standalone appointment booking.

### Priority 3 — UX
7. **Referral accept confirmation dialog** — Currently accept is one-click. Add a lightweight confirmation (or optimistic + undo) for better UX.
8. **Toast notifications** — Add a global toast system so Accept/Decline/Cancel actions show a success message without requiring a page reload.
9. **Referral search/filter on list page** — Add client name, case number, and provider filters to the referrals list.

---

## Files Changed

### New Files
- `apps/web/src/components/careconnect/referral-status-actions.tsx` — Accept/Decline/Cancel client component
- `apps/web/src/components/careconnect/appointment-cancel-button.tsx` — Cancel client component
- `apps/web/src/components/careconnect/provider-availability-preview.tsx` — 5-slot preview on provider detail
- `apps/services/careconnect/CareConnect.Tests/Application/WorkflowIntegrationTests.cs` — 19 new tests

### Modified Files
- `apps/web/src/app/(platform)/careconnect/dashboard/page.tsx` — Replaced BlankPage with full dashboard
- `apps/web/src/app/(platform)/careconnect/referrals/[id]/page.tsx` — Added ReferralStatusActions; Book Appt only on Accepted
- `apps/web/src/app/(platform)/careconnect/appointments/[id]/page.tsx` — Added AppointmentCancelButton
- `apps/web/src/app/(platform)/careconnect/providers/[id]/page.tsx` — Added availability preview
- `apps/web/src/app/(platform)/careconnect/referrals/page.tsx` — Fixed status filter chips (Pending → New)
- `apps/web/src/lib/careconnect-api.ts` — Added `referrals.update` and `appointments.cancel`
- `apps/web/src/lib/nav.ts` — Added Appointments link to CareConnect sidebar
- `apps/web/src/components/careconnect/status-badge.tsx` — Added Accepted, Declined, NoShow, Confirmed colors
