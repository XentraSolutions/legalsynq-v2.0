# Step 26 — Final Rollout, Producer Expansion, UI Activation & SynqAudit Productization

**Date:** 2026-03-30
**Status:** Complete
**Tests:** 72 / 72 passing (all groups: Ingest ×30, Query ×21, Remaining ×21)

---

## Summary

Step 26 completes the SynqAudit productization cycle by:

1. Enforcing canonical event naming across all producers (identity. / careconnect. prefix)
2. Expanding producer coverage to all HIPAA-critical lifecycle events
3. Activating the tenant-facing activity log (Phase 2)
4. Upgrading the Control Center audit dashboard with an interactive detail panel
5. Confirming the CorrelationId enforcement infrastructure is production-ready

---

## T001 — Event Naming Normalization (Complete)

All event type strings now follow the `<service>.<entity>.<action>` canonical format.

| File | Before | After |
|------|--------|-------|
| `AuthService.cs` | `user.login.succeeded` | `identity.user.login.succeeded` |
| `AuthService.cs` | `user.login.failed` | `identity.user.login.failed` |
| `AdminEndpoints.cs` | `user.role.assigned` | `identity.role.assigned` |
| `AdminEndpoints.cs` | `user.role.revoked` | `identity.role.removed` |
| `AuthService.cs` | `Outcome = "failure"` (invalid field) | Removed — field does not exist on `IngestAuditEventRequest` |

Idempotency keys updated to match new event type names in all cases.

---

## T002 — Missing Identity Producer Events (Complete)

### `identity.user.logout` → `AuthEndpoints.cs`

- Added to `POST /api/auth/logout` handler
- Reads identity claims from the HTTP context (JWT may be expired at logout time — claims are read without re-validation)
- Emits actor email / userId as available; gracefully handles anonymous/expired state
- Fire-and-observe pattern — logout response is never blocked by audit emission

### `identity.user.created` → `UserEndpoints.cs`

- Added to `POST /api/users` handler after `userService.CreateUserAsync` succeeds
- Includes Before: null, After: `{ userId, email, tenantId }` diff snapshot
- Actor type: `System` (user creation is an admin/system provisioning action)
- Uses deterministic idempotency key: `IdempotencyKey.For("identity-service", "identity.user.created", user.Id)`

### `identity.user.deactivated`
- Gap documented: no deactivation endpoint exists in Identity.Api at this time
- Recommended: add `DELETE /api/users/{id}` or `PATCH /api/users/{id}/status` endpoint in a future step and emit `identity.user.deactivated` on status change to Inactive

---

## T003 — CareConnect Producer Expansion (Complete)

### `careconnect.referral.updated` → `ReferralService.UpdateAsync`

- Emitted after `_referrals.UpdateAsync(...)` and the status-change notification hook
- Action discriminated: `"ReferralStatusChanged"` when status changed, `"ReferralUpdated"` otherwise
- After snapshot: `{ status, requestedService, urgency, statusChanged }`
- Visibility: `Tenant`, Category: `Business`, Severity: `Info`

### `careconnect.appointment.cancelled` → `AppointmentService.CancelAppointmentAsync`

- Emitted after `_appointments.SaveCancellationAsync(...)` and the cancellation notification hook
- Before snapshot: `{ status: oldStatus }`, After snapshot: `{ status: "Cancelled", notes, slotReleased }`
- Severity: `Warn` (cancellations are operationally significant)
- Visibility: `Tenant`, Category: `Business`

Both use `IdempotencyKey.ForWithTimestamp(...)` to prevent duplicate ingestion on retry.

---

## T004 — CorrelationId Enforcement Audit (Complete — pre-existing)

`CorrelationIdMiddleware` was found to be fully implemented in Step 21:

- Reads `X-Correlation-ID` from incoming request headers
- Validates: max 100 chars, alphanumeric + hyphen + underscore only
- Auto-generates a new UUID if header is absent or fails validation
- Pushes `CorrelationId` into Serilog `LogContext` for every structured log entry in the request scope
- Echoes the resolved value back in the `X-Correlation-ID` response header
- Pipeline position: before `IngestAuthMiddleware` and `QueryAuthMiddleware`, ensuring TraceId is available to all downstream handlers

No additional work required. Production readiness confirmed.

**HMAC Key note:** `Integrity__HmacKeyBase64` must be set as a production secret.
Generate with: `openssl rand -base64 32`

---

## T005 — Control Center Audit Dashboard Enhancement (Complete)

### New component: `CanonicalAuditTableInteractive`

File: `apps/control-center/src/components/audit-logs/canonical-audit-table-interactive.tsx`

- Client component (`'use client'`) wrapping the full audit table
- Row click toggles selection state; selected row highlighted with indigo ring
- Detail side-panel slides in to the right of the table on row selection
- Panel is 384px wide, scrollable (max 70vh), organized into named sections:
  - **Event**: type, source, category, severity, outcome
  - **Timing**: occurred UTC (full ms precision), ingested UTC
  - **Actor**: name, ID (or "System / anonymous" if both absent)
  - **Target Entity**: type + ID (only shown when present)
  - **Scope**: tenant ID
  - **Tracing**: correlation ID, IP address, event UUID
  - **Description**: human-readable event description
  - **Metadata**: formatted as pretty-printed JSON (raw string if non-JSON)
- Clicking the × button or clicking the same row again closes the panel

The `CanonicalAuditTable` server component is preserved unchanged. The audit-logs page now imports and renders `CanonicalAuditTableInteractive` for the canonical path.

---

## T006 — Tenant Portal Activity Page (Phase 2 — Complete)

File: `apps/web/src/app/(platform)/activity/page.tsx`

Upgrades the placeholder `BlankPage` shell to a fully functional, tenant-scoped activity log viewer:

### Access control
- `requireOrg()` guard — authenticated org member required, redirects to `/no-org` otherwise

### Data fetch
- `serverApi.get('/audit-service/audit/events?tenantId={session.tenantId}&...')`
- Reads `platform_session` cookie, forwards as `Authorization: Bearer` to gateway
- Gateway routes `/audit-service/...` → Platform Audit Event Service (port 5007)
- Graceful error state: shows amber warning banner when service is unreachable

### Filters (GET form, URL-based, no JS required)
- Event Type (free text)
- Severity (select: All / Info / Warn / Error / Critical)
- Date From / Date To
- Filter chips shown below filter bar when active
- Pagination (20 events per page) with page range builder

### Columns rendered (tenant-safe subset)
- Time (UTC) — formatted `YYYY-MM-DD HH:MM:SS`
- Event (eventType, monospace)
- Category — colour-coded badge
- Severity — colour-coded badge
- Actor (label + ID where both present)
- Target (type + ID)
- Description (truncated, max-w-xs)

**Intentionally excluded** (platform-internal):
- `source` / `sourceService`
- `ipAddress`
- `correlationId`
- `integrityHash` / `ingestedAtUtc`

---

## T007 — Build & Test Verification (Complete)

| Service | Status |
|---------|--------|
| Identity.Api | Build succeeded |
| CareConnect.Api | Build succeeded (1 pre-existing CS0168 warning, unrelated) |
| PlatformAuditEventService | Build succeeded |
| control-center TypeScript | No errors |
| web TypeScript | 1 pre-existing error in dashboard/page.tsx (unrelated) |

| Test group | Passed | Failed |
|-----------|--------|--------|
| Ingest (×30) | 30 | 0 |
| Query (×21) | 21 | 0 |
| Remaining (×21) | 21 | 0 |
| **Total** | **72** | **0** |

---

## Canonical Producer Coverage Summary

| Service | Event Type | Trigger |
|---------|-----------|---------|
| Identity | `identity.user.login.succeeded` | Successful authentication |
| Identity | `identity.user.login.failed` | Failed authentication |
| Identity | `identity.user.logout` | POST /api/auth/logout |
| Identity | `identity.user.created` | POST /api/users |
| Identity | `identity.role.assigned` | Role assignment |
| Identity | `identity.role.removed` | Role removal |
| CareConnect | `careconnect.referral.created` | Referral created |
| CareConnect | `careconnect.referral.updated` | Referral updated/status changed |
| CareConnect | `careconnect.appointment.scheduled` | Appointment booked |
| CareConnect | `careconnect.appointment.cancelled` | Appointment cancelled |

**Documented gap:** `identity.user.deactivated` — requires a future user deactivation endpoint.

---

## Production Deployment Checklist

| Config | Value | Where |
|--------|-------|-------|
| `Integrity__HmacKeyBase64` | `openssl rand -base64 32` | Audit service secret |
| `QueryAuth__Mode` | `Bearer` | Audit service env |
| `AUDIT_READ_MODE` | `canonical` | control-center + web env |
| `GATEWAY_URL` | `https://gateway.legalsynq.com` | All frontend services |
| `Jwt__*` | (existing) | Identity service |

---

## Files Changed in Step 26

```
apps/services/identity/Identity.Application/Services/AuthService.cs
apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs
apps/services/identity/Identity.Api/Endpoints/AuthEndpoints.cs
apps/services/identity/Identity.Api/Endpoints/UserEndpoints.cs
apps/services/careconnect/CareConnect.Application/Services/ReferralService.cs
apps/services/careconnect/CareConnect.Application/Services/AppointmentService.cs
apps/control-center/src/components/audit-logs/canonical-audit-table-interactive.tsx  [NEW]
apps/control-center/src/app/audit-logs/page.tsx
apps/web/src/app/(platform)/activity/page.tsx
analysis/step26_rollout_and_productization.md  [NEW]
```
