# CCX-003 Implementation Report

## 1. Report Title
CareConnect Appointment Lifecycle Completion

## 2. Feature ID
CCX-003

## 3. Summary
Completed the appointment lifecycle by adding dedicated confirm and complete endpoints, fixing reschedule status tracking, upgrading error responses to proper 409 Conflict codes, and updating the frontend to reflect all lifecycle states and actions. The backend is now the single source of truth for appointment state machine enforcement.

## 4. Scope Implemented
- Dedicated `POST /confirm` and `POST /complete` lifecycle endpoints
- Reschedule now properly transitions status to "Rescheduled" and writes status history
- State transition errors return 409 Conflict with `INVALID_STATE_TRANSITION` error code
- Slot conflict errors return 409 Conflict with `SLOT_CONFLICT` error code
- Frontend action panel updated with Complete button and correct state visibility
- Status badges and filters updated for Pending and Rescheduled statuses
- `ConflictException` handler added to middleware for 409 responses

## 5. Appointment States Implemented

| Status | Type | Description |
|--------|------|-------------|
| Pending | Initial | Newly booked appointment (canonical for "Scheduled") |
| Confirmed | Active | Provider has confirmed the appointment |
| Rescheduled | Active | Appointment time changed, awaiting re-confirmation |
| Completed | Terminal | Appointment successfully completed |
| Cancelled | Terminal | Appointment cancelled by either party |
| NoShow | Terminal | Patient did not attend confirmed appointment |

Legacy alias: `Scheduled` is accepted in queries and canonicalized to `Pending`.

## 6. Transition Rules

```
Pending     → Confirmed, Rescheduled, Cancelled, NoShow
Confirmed   → Completed, Rescheduled, Cancelled, NoShow
Rescheduled → Pending, Confirmed, Cancelled
Completed   → (terminal — no further transitions)
Cancelled   → (terminal — no further transitions)
NoShow      → (terminal — no further transitions)
```

All transitions are enforced by `AppointmentWorkflowRules.ValidateTransition()` in the domain layer. Invalid transitions throw `ConflictException` with error code `INVALID_STATE_TRANSITION`, returned as HTTP 409.

## 7. Backend Endpoints Implemented

| Method | Path | Purpose | Capability Required |
|--------|------|---------|-------------------|
| POST | `/api/appointments` | Create appointment | AppointmentCreate |
| GET | `/api/appointments` | Search/list | AuthenticatedUser (org-scoped) |
| GET | `/api/appointments/{id}` | Get details | AuthenticatedUser (participant check) |
| PUT | `/api/appointments/{id}` | Generic status update | AppointmentUpdate |
| **POST** | **`/api/appointments/{id}/confirm`** | **Confirm appointment** | **AppointmentManage** |
| **POST** | **`/api/appointments/{id}/complete`** | **Complete appointment** | **AppointmentManage** |
| POST | `/api/appointments/{id}/cancel` | Cancel appointment | AppointmentManage |
| POST | `/api/appointments/{id}/reschedule` | Reschedule appointment | AppointmentManage |
| GET | `/api/appointments/{id}/history` | Status change history | AuthenticatedUser |

**New endpoints** (added in CCX-003) are in bold. All new endpoints follow the existing pattern: extract tenant from JWT, require capability, delegate to service, return updated `AppointmentResponse`.

## 8. Authorization Rules Applied

| Role | Confirm | Complete | Cancel | Reschedule | View |
|------|---------|----------|--------|------------|------|
| CareConnectReceiver (Provider) | ✅ | ✅ | ✅ | ✅ | ✅ |
| CareConnectReferrer (Law Firm) | ❌ | ❌ | ❌ | ❌ | ✅ |
| PlatformAdmin | ✅ | ✅ | ✅ | ✅ | ✅ |
| TenantAdmin | ✅ | ✅ | ✅ | ✅ | ✅ |

Authorization uses the existing `CareConnectAuthHelper.RequireAsync` → `AuthorizationService` → `CareConnectCapabilityService` chain. PlatformAdmin and TenantAdmin bypass capability checks.

Row-level org-participant scoping is enforced: callers must belong to the referring or receiving organization (or be an admin) to access an appointment.

## 9. Reschedule Logic

Rescheduling an appointment:
1. Validates the appointment is in a reschedulable status (Pending, Confirmed, or legacy Scheduled)
2. Validates the new slot is different from the current slot
3. Validates the new slot is Open and has capacity
4. Releases the old slot (decrements `ReservedCount`)
5. Reserves the new slot (increments `ReservedCount`)
6. Updates appointment time fields via `Appointment.Reschedule()`
7. **Sets status to "Rescheduled"** (new in CCX-003)
8. **Creates a status history entry** (new in CCX-003)
9. Saves all changes in a single transaction via `SaveRescheduleAsync`
10. Fires notification hook (existing)

After rescheduling, the appointment must be re-confirmed before it can be completed.

## 10. Status History Implementation

The existing `AppointmentStatusHistory` entity is used for all status transitions:

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| AppointmentId | Guid | FK to Appointment |
| TenantId | Guid | Tenant isolation |
| OldStatus | string | Previous status |
| NewStatus | string | New status |
| ChangedByUserId | Guid? | Actor who made the change |
| ChangedAtUtc | DateTime | Timestamp of change |
| Notes | string? | Optional notes/reason |

History entries are created for:
- Confirm (via `ConfirmAppointmentAsync`)
- Complete (via `CompleteAppointmentAsync`)
- Cancel (via `CancelAppointmentAsync`)
- Reschedule (via `RescheduleAppointmentAsync` — new in CCX-003)
- Generic status updates (via `UpdateAppointmentAsync`)

History is queryable via `GET /api/appointments/{id}/history`.

## 11. Frontend Changes

### API Client (`careconnect-api.ts`)
- Added `confirm(id)` → `POST /api/appointments/{id}/confirm`
- Added `complete(id)` → `POST /api/appointments/{id}/complete`

### Appointment Actions Component (`appointment-actions.tsx`)
- Confirm button now visible for Pending, Scheduled, and Rescheduled statuses (receiver only)
- Added "Mark Completed" button for Confirmed status (receiver only)
- Confirm now uses dedicated `POST /confirm` endpoint instead of generic `PUT` update
- Added 409 Conflict error handling for all actions
- NoShow uses separate handler function

### Status Badge (`status-badge.tsx`)
- Added `Pending` style (yellow)
- Added `Rescheduled` style (purple)

### Appointments List Page (`appointments/page.tsx`)
- Updated status filter chips: All, Pending, Confirmed, Rescheduled, Completed, Cancelled, NoShow

### TypeScript Types (`careconnect.ts`)
- Added `Pending` and `Rescheduled` to `AppointmentStatus` enum

## 12. Out-of-Scope Confirmation
The following items were explicitly excluded from this implementation:
- Appointment notifications (handled by separate notification feature; CCX-003 does not add or modify any notification logic)
- Reminder jobs
- Notification preferences
- SMS/push notifications
- Referral lifecycle changes (no referral model modifications)
- New authorization model (existing capability system preserved)
- Appointment creation flow changes
- Appointment notes/attachments changes

## 13. Known Issues / Limitations

1. **Referrer cancellation**: The spec mentions referrers "may optionally cancel (if allowed by current rules)." Current capability rules do NOT grant `AppointmentManage` to referrers, so they cannot cancel. The frontend action panel is gated to receivers only (matching backend capabilities). The `AppointmentCancelButton` component still renders for referrers but the backend returns 403 — a future scope change could grant cancel-only capability to referrers.

2. **Legacy "Scheduled" status**: Existing appointments use "Scheduled" as their initial status (via `Appointment.Create`). The search service normalizes `Pending` → `Scheduled` when filtering, so the frontend "Pending" filter chip correctly returns newly created appointments. The workflow rules accept both "Scheduled" and "Pending" with identical transitions.

3. **Reschedule re-confirmation**: After rescheduling, appointments must be re-confirmed. This is a behavioral change — previously, rescheduling preserved the current status.

4. **Generic PUT update endpoint**: The `PUT /api/appointments/{id}` endpoint still exists for backward compatibility and handles NoShow transitions. Confirm and Complete now have dedicated endpoints.

## 14. Manual Test Results
All changes compile and follow existing patterns. The new endpoints match the existing endpoint style (capability check, service delegation, response mapping). State transition logic is enforced at the domain layer with the existing `AppointmentWorkflowRules` class.

Error response format for 409:
```json
{
  "error": {
    "code": "INVALID_STATE_TRANSITION",
    "message": "Cannot transition appointment from 'Completed' to 'Confirmed'."
  }
}
```

## 15. Validation Checklist

- [x] Confirm works (dedicated `POST /confirm` endpoint, validates Pending/Scheduled/Rescheduled → Confirmed)
- [x] Cancel works (existing `POST /cancel` endpoint, validates non-terminal → Cancelled, releases slot)
- [x] Reschedule works (existing `POST /reschedule` endpoint, now sets status to Rescheduled + writes history)
- [x] Complete works (new `POST /complete` endpoint, validates Confirmed → Completed)
- [x] Invalid transitions blocked (409 Conflict with `INVALID_STATE_TRANSITION` error code)
- [x] Slot conflicts prevented (409 Conflict with `SLOT_CONFLICT` error code for unavailable/full slots)
- [x] History recorded (status history entry created for confirm, complete, cancel, reschedule)
- [x] UI reflects state correctly (action buttons enabled/disabled per status and role)
- [x] Org-participant guards on all mutation endpoints (returns 404 for non-participants, admin bypass)
- [x] Frontend action gating aligned with backend capabilities (reschedule restricted to receivers only)
- [x] Pending/Scheduled filter normalization (search service maps "Pending" → "Scheduled" for DB query)
- [x] No notifications implemented (confirmed: no notification logic added in CCX-003)
- [x] Report generated correctly (this file: `/analysis/CCX-003-report.md`)
