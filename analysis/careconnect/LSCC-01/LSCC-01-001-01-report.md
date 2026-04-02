# LSCC-01-001-01 — Referral State Machine Correction

**Status:** Complete  
**Date:** 2026-04-02  
**Epic:** LSCC-01 (CareConnect Referral Workflow)  
**Parent:** LSCC-01-001 (Referral Foundation)

---

## 1. Summary

### What Was Implemented

- `InProgress` added as the canonical active referral state, replacing `Scheduled` in the active lifecycle
- `Scheduled` demoted to a legacy compat alias (no longer produced, only normalised on ingest)
- Transition matrix corrected: `Accepted → InProgress` (not `Accepted → Completed`)
- `Accepted → Completed` path explicitly blocked — receiver must move through `InProgress` first
- `InProgress → Cancelled` allowed (per spec)
- Legacy migration added: all `Scheduled` rows migrated to `InProgress`
- `Legacy.Normalize` updated to map `Scheduled → InProgress`
- Legacy Scheduled entry retained in transition matrix for pre-migration in-flight data
- Frontend updated: toolbar filters, status badge, row highlights, Mark In Progress action, booking prompt decoupled
- `ActivationFunnelAnalyticsService` updated to count `InProgress` (not `Scheduled`) as accepted
- Audit timeline label mapping updated to surface `InProgress` label correctly
- `ReferralWorkflowRulesTests` updated with 38 passing tests covering new transitions

### What Remains Incomplete

- No cancellation reason field implemented (deferred per spec note)
- Admin override path unchanged (not in scope)
- Org linkage corrections deferred (own task)
- Designer migration file not added — EF snapshot is not modified since Status is a string column; the migration is raw SQL only

---

## 2. Corrected Canonical Referral State Model

| Status     | Type      | Notes                                                              |
|------------|-----------|--------------------------------------------------------------------|
| New        | Canonical | Initial state on creation                                         |
| Accepted   | Canonical | Receiver has acknowledged the referral                            |
| InProgress | Canonical | **New** — Receiver is actively working the referral               |
| Completed  | Canonical | Referral fully serviced (terminal)                                |
| Declined   | Canonical | Receiver declined (terminal)                                      |
| Cancelled  | Canonical | Referral cancelled by referrer or receiver (terminal)             |
| Scheduled  | **Legacy**| Demoted — normalised to `InProgress` on ingest; never produced    |
| Received   | Legacy    | Pre-existing alias → normalised to `Accepted`                     |
| Contacted  | Legacy    | Pre-existing alias → normalised to `Accepted`                     |

**Compatibility mapping (`Legacy.Normalize`):**
- `Received` → `Accepted`
- `Contacted` → `Accepted`
- `Scheduled` → `InProgress` _(new — LSCC-01-001-01)_

---

## 3. Corrected Transition Matrix

| From        | Allowed To                                | Owner     |
|-------------|-------------------------------------------|-----------|
| New         | Accepted · Declined · Cancelled           | Receiver/Either |
| Accepted    | **InProgress** · Declined · Cancelled     | Receiver  |
| InProgress  | Completed · Cancelled                     | Receiver/Either |
| Completed   | _(terminal — none)_                       | —         |
| Declined    | _(terminal — none)_                       | —         |
| Cancelled   | _(terminal — none)_                       | —         |
| **Legacy: Received**  | Accepted · InProgress · Declined · Cancelled | Receiver |
| **Legacy: Contacted** | Accepted · InProgress · Declined · Cancelled | Receiver |
| **Legacy: Scheduled** | InProgress · Cancelled                   | Either   |

**Key blocked path:** `Accepted → Completed` is explicitly rejected by `ReferralWorkflowRules.ValidateTransition`.

---

## 4. Backend Enforcement

### Centralized Validation
All status transitions are gated through `ReferralWorkflowRules`:
- `ValidateTransition(fromStatus, toStatus)` — throws `ValidationException` for invalid moves
- `IsValidTransition(fromStatus, toStatus)` — boolean check for programmatic use
- `RequiredCapabilityFor(toStatus)` — returns the capability code required for each transition

### Capability Gating
The PUT `/api/referrals/{id}` endpoint calls `RequiredCapabilityFor(request.Status)` before delegating to the service. `InProgress` requires `ReferralUpdateStatus` capability.

### Arbitrary Mutation Prevention
`ReferralService.UpdateAsync` calls `ReferralWorkflowRules.ValidateTransition(referral.Status, request.Status)` before applying any change. The domain entity's `Update()` method only accepts a status string — no bypass exists in the service layer.

---

## 5. Legacy Migration

**Migration file:** `20260402000000_ReferralInProgressState.cs`

```sql
-- Up
UPDATE `Referrals` SET `Status` = 'InProgress' WHERE `Status` = 'Scheduled';

-- Down
UPDATE `Referrals` SET `Status` = 'Scheduled' WHERE `Status` = 'InProgress';
```

**Additional compatibility paths retained:**
- `AllowedTransitions` retains a `Legacy.Scheduled` entry allowing `Scheduled → InProgress | Cancelled` for any rows that arrive after the migration has run (e.g., in-flight API requests from un-refreshed clients)
- `Legacy.Normalize` maps `"Scheduled"` → `"InProgress"` so any normalization call on persisted legacy strings produces the correct canonical state

---

## 6. Frontend Alignment

### Changes Made

| File | Change |
|------|--------|
| `status-badge.tsx` | Added `InProgress` (amber) style; `Scheduled` kept as legacy fallback |
| `referral-queue-toolbar.tsx` | Replaced `Scheduled / Scheduled` filter pill with `InProgress / In Progress` |
| `referral-list-table.tsx` | Added amber row highlight for `InProgress` rows |
| `referral-status-actions.tsx` | Added **Mark In Progress** action for receiver when status is `Accepted`; updated `STATUS_LABELS` |
| `referrals/[id]/page.tsx` | Removed the "Book Appointment" prompt tied to `Accepted` status — decouples referral from appointment scheduling |

### Legacy Scheduled Handling During Rollout
Any row still carrying `"Scheduled"` status before the migration runs will:
- Display with the yellow `Scheduled` badge (retained in `STATUS_STYLES`)
- Be allowed to transition only to `InProgress` or `Cancelled` (transition matrix entry)
- Be normalized to `InProgress` by `Legacy.Normalize` if processed by the service layer

---

## 7. History / Audit

### Transition Data Recorded
`ReferralStatusHistory` records every status transition with: `FromStatus`, `ToStatus`, `ChangedByUserId`, `ChangedAtUtc`, `Notes`. This is unchanged by this task — `InProgress` transitions will produce a history record with `ToStatus = "InProgress"`.

### Audit Timeline Labels
`ReferralService.GetAuditTimelineAsync` label mapping updated:
- `"InProgress"` → `"Referral In Progress"` (success category)
- `"Scheduled"` kept as `"Referral Scheduled"` (info category) for historical entries in the timeline that pre-date the migration

### Migration Effects
Rows migrated from `Scheduled → InProgress` in the database do **not** generate a new `ReferralStatusHistory` record. The migration is a data correction, not a domain event. Historical timeline entries showing `"Scheduled"` remain in `ReferralStatusHistory` and are displayed using the legacy label.

---

## 8. Tests

### Coverage — `ReferralWorkflowRulesTests` (38 tests, all pass)

| Suite | Cases |
|-------|-------|
| Canonical transitions | `New → *`, `Accepted → *`, `InProgress → *`, terminal state blocks |
| Legacy transitions | `Received/Contacted → InProgress/Accepted/etc.`; `Scheduled → InProgress/Cancelled`; `Scheduled → Completed` blocked |
| Terminal states | InProgress is NOT terminal; Completed/Declined/Cancelled are |
| RequiredCapabilityFor | InProgress, Accepted, Declined, Cancelled, Completed |
| ValidStatuses.All | Contains InProgress; does NOT contain Scheduled/Received/Contacted |
| Legacy.Normalize | Scheduled → InProgress; Received → Accepted; Contacted → Accepted |
| ValidateTransition | Throws for Completed→New; throws for **Accepted→Completed**; allows InProgress→Cancelled; no-op for same-status |

### Gaps
- No integration tests for the full PUT /api/referrals/{id} → InProgress path against a real DB
- No end-to-end frontend tests for the "Mark In Progress" button flow

---

## 9. Known Limitations / Deferred Items

| Item | Notes |
|------|-------|
| Cancellation reason | No reason field on `InProgress → Cancelled` transitions; deferred |
| Admin override | Platform admin can still PUT any status directly; unchanged from LSCC-01-001 |
| Org linkage enforcement | Receiver role restriction to org-participant is enforced by org-scoped queries but org-linkage validation is deferred |
| `ReferralStatusHistory` migration records | The SQL migration does not backfill history for Scheduled→InProgress rows. Pre-migration timelines may show "Scheduled" |
| Appointment booking discoverability | The "Book Appointment" prompt was removed. Referrers must navigate to the provider availability page manually. A follow-up task should add a persistent appointment booking entrypoint that is status-independent |

---

## 10. Recommended Next Step

**LSCC-01-001-02 — Persistent Appointment Booking Entrypoint**

The removal of the `Accepted`-gated "Book Appointment" prompt decouples the referral and appointment workflows correctly, but leaves referrers without a clear path to schedule once the referral is `InProgress`. The next smallest step is to add a persistent "Schedule Appointment" link on the referral detail page that is always visible to the referrer (regardless of referral status) when the referral is non-terminal, linking to the provider availability page pre-seeded with the referral ID.

---

*Report generated by LSCC-01-001-01 implementation pass — 2026-04-02*
