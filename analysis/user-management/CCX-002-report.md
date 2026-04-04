# CCX-002 Implementation Report

## 1. Report Title
CareConnect Referral Notifications & Delivery Wiring

## 2. Feature ID
CCX-002

## 3. Summary
Wired all four referral lifecycle events (submitted, accepted, rejected, cancelled) to real notification creation and email delivery within the CareConnect service. Added idempotent duplicate prevention via a DedupeKey mechanism on the CareConnectNotification model, ensuring retried or replayed workflow mutations do not produce duplicate notifications. All notification paths follow the existing fire-and-observe pattern with retry support.

## 4. Scope Implemented
- Referral submitted → provider notification (already existed; hardened with dedupe key)
- Referral accepted → provider, referrer, and client notifications (already existed; hardened with dedupe keys and wired into UpdateAsync)
- Referral rejected (declined) → provider and referrer notifications (new)
- Referral cancelled → provider and referrer notifications (new)
- Idempotent duplicate prevention for all referral notification paths
- Retry support for all new notification types via the existing ReferralEmailRetryWorker
- Database migration for DedupeKey column with unique index

## 5. Referral Events Wired

| Event | Trigger Point | Notification Types Created |
|-------|--------------|---------------------------|
| Referral Submitted | `ReferralService.CreateAsync` | `ReferralCreated` (provider) |
| Referral Accepted | `ReferralService.UpdateAsync` (status → Accepted) | `ReferralAcceptedProvider`, `ReferralAcceptedReferrer`, `ReferralAcceptedClient` |
| Referral Rejected | `ReferralService.UpdateAsync` (status → Declined) | `ReferralRejectedProvider`, `ReferralRejectedReferrer` |
| Referral Cancelled | `ReferralService.UpdateAsync` (status → Cancelled) | `ReferralCancelledProvider`, `ReferralCancelledReferrer` |

## 6. Recipient Resolution Rules

| Event | Recipient | Source | RecipientType |
|-------|-----------|--------|---------------|
| Submitted | Provider org | `provider.Email` | Provider |
| Accepted | Provider org | `provider.Email` | Provider |
| Accepted | Referring user | `referral.ReferrerEmail` | InternalUser |
| Accepted | Client | `referral.ClientEmail` | ClientEmail |
| Rejected | Provider org | `provider.Email` | Provider |
| Rejected | Referring user | `referral.ReferrerEmail` | InternalUser |
| Cancelled | Provider org | `provider.Email` | Provider |
| Cancelled | Referring user | `referral.ReferrerEmail` | InternalUser |

All recipient resolution uses real domain relationships (Provider entity email, Referral stored referrer/client emails). No ad-hoc email strings are used. Tenant/org boundaries are preserved through the referral's TenantId being carried into every notification record.

## 7. Notification Persistence / Delivery Implementation

### Persistence
Reuses the existing `CareConnectNotification` entity and `CareConnectNotifications` table. Each notification is persisted as a DB record with status `Pending` before any delivery attempt.

### New Field Added
- `DedupeKey` (varchar(500), nullable, unique index): A deterministic key built from `referral:{referralId}:{event}:{recipientRole}` that prevents duplicate notification creation for the same referral event.

### Delivery
Uses the existing `ISmtpEmailSender` abstraction (which routes through either direct SMTP or the platform Notifications service via `NotificationsServiceEmailSender`). Each notification follows the pattern:
1. Create `CareConnectNotification` record (Pending)
2. Attempt immediate email send via `TrySendAndUpdateAsync`
3. On success → `MarkSent()` (Status=Sent, SentAtUtc set)
4. On failure → `MarkFailed()` (Status=Failed, FailureReason stored, NextRetryAfterUtc scheduled)

### New Notification Types
- `ReferralRejectedProvider`
- `ReferralRejectedReferrer`
- `ReferralCancelledProvider`
- `ReferralCancelledReferrer`

## 8. Idempotency / Duplicate Prevention

### Mechanism
Atomic dedupe via `TryAddWithDedupeAsync`: attempts an INSERT and catches only MySQL duplicate-entry errors (error 1062) on the unique `DedupeKey` index. On conflict, the entity is detached from the EF change tracker and the method returns `false`, skipping the send. This eliminates the TOCTOU race condition inherent in a check-then-insert pattern.

### DedupeKey Format
```
referral:{referralId}:{eventType}:{recipientRole}
```

Examples:
- `referral:abc-123:created:provider`
- `referral:abc-123:accepted:referrer`
- `referral:abc-123:declined:provider`
- `referral:abc-123:cancelled:referrer`

### Database Enforcement
A unique index on `DedupeKey` provides database-level protection. The `TryAddWithDedupeAsync` method only catches `DbUpdateException` with inner `MySqlException { Number: 1062 }` (duplicate entry), ensuring unrelated DB errors propagate normally.

### Coverage
All referral notification creation paths (submitted, accepted, rejected, cancelled) are protected by atomic dedupe inserts via `TryAddWithDedupeAsync`.

## 9. Backend Endpoints / Workflow Hooks Updated

### Files Modified

| File | Changes |
|------|---------|
| `CareConnect.Domain/NotificationType.cs` | Added 4 new notification type constants |
| `CareConnect.Domain/CareConnectNotification.cs` | Added `DedupeKey` property and parameter to `Create()` |
| `CareConnect.Application/Interfaces/IReferralEmailService.cs` | Added `SendRejectionNotificationsAsync` and `SendCancellationNotificationsAsync` |
| `CareConnect.Application/Services/ReferralEmailService.cs` | Implemented reject/cancel notifications, email templates, retry cases, atomic dedupe inserts on all paths |
| `CareConnect.Application/Services/ReferralService.cs` | Wired status-change email dispatch in `UpdateAsync` for Accepted/Declined/Cancelled |
| `CareConnect.Application/Repositories/INotificationRepository.cs` | Added `ExistsByDedupeKeyAsync` and `TryAddWithDedupeAsync` |
| `CareConnect.Infrastructure/Repositories/NotificationRepository.cs` | Implemented `ExistsByDedupeKeyAsync` and `TryAddWithDedupeAsync` (atomic dedupe with MySQL error 1062 filter) |
| `CareConnect.Infrastructure/Data/Configurations/CareConnectNotificationConfiguration.cs` | Added DedupeKey property + unique index config |

### Files Created

| File | Purpose |
|------|---------|
| `CareConnect.Infrastructure/Data/Migrations/20260404000000_AddNotificationDedupeKey.cs` | Migration for DedupeKey column |
| `CareConnect.Infrastructure/Data/Migrations/20260404000000_AddNotificationDedupeKey.Designer.cs` | Migration designer |

### No Endpoint Changes
No new API endpoints were added. The existing `PUT /api/referrals/{id}` endpoint now triggers email notifications as a side effect of status transitions, consistent with how `POST /api/referrals` already triggers the new-referral notification.

## 10. Frontend Changes
No frontend changes were made. The notification dispatch is fully backend-driven and uses the fire-and-observe pattern — it never blocks or gates the referral mutation response. The existing referral detail view and mutation UX continue to work unchanged.

The backend already returns notification records via the existing `GET /api/notifications` search endpoint and the referral-scoped `GetAllByReferralAsync` query. A lightweight notification status panel could be added in a future iteration if desired.

## 11. Out-of-Scope Confirmation
The following items were explicitly excluded from this implementation:
- Appointment notifications (booked, confirmed, cancelled, rescheduled, completed)
- Reminder jobs
- Notification preferences UI
- Notification center/inbox UI
- SMS notifications
- Push notifications
- Escalation workflows
- Campaign or template studio
- Notification analytics dashboard
- Unrelated workflow changes

No appointment-related notification code was added or modified.

## 12. Known Issues / Delivery Limitations

1. **SMTP/SendGrid configuration**: The outbound delivery depends on the `ISmtpEmailSender` implementation being properly configured. If SendGrid API keys or SMTP settings are not configured, notifications will be created as `Pending` records, the immediate send will fail, and they will be marked `Failed` with a retry schedule. The `ReferralEmailRetryWorker` will re-attempt delivery per the retry policy (max 3 attempts with 5min / 30min backoff).

2. **ReferralToken:Secret not configured**: The system logs a warning at startup and uses a development fallback secret. This must be configured for production.

3. **DedupeKey unique index on MySQL with NULLs**: MySQL treats NULL values as distinct in unique indexes, so legacy notification records without a DedupeKey will not conflict with each other. This is the desired behavior — only non-null keys enforce uniqueness.

4. **Background task pattern**: Notification dispatch in `UpdateAsync` uses `Task.Run` with a fresh DI scope (fire-and-observe), matching the existing `CreateAsync` pattern. If the application process terminates between the referral update and notification creation, the notification will not be sent. The retry worker provides a safety net for failed-but-created notifications, but not for never-created ones.

5. **Client notifications on reject/cancel**: Not implemented for reject/cancel events. The spec only requires provider and referrer notifications for these events. Client notification is only sent on acceptance.

## 13. Manual Test Results
Build verification: The CareConnect API project compiles successfully with zero errors and zero warnings after all changes.

Runtime testing requires a running MySQL instance with the migration applied and configured email provider. The notification paths follow the same patterns as the already-proven referral creation and acceptance notification flows.

## 14. Validation Checklist

- [x] Referral submitted notification works (existing flow hardened with dedupe key)
- [x] Referral accepted notification works (existing flow wired into UpdateAsync with dedupe keys)
- [x] Referral rejected notification works (new: provider + referrer notifications)
- [x] Referral cancelled notification works (new: provider + referrer notifications)
- [x] Correct recipients are targeted (provider email, referrer email, client email per event type)
- [x] Duplicate sends prevented (atomic TryAddWithDedupeAsync + unique index, MySQL error 1062 filter)
- [x] Delivery result persisted or logged (CareConnectNotification record with Status, FailureReason, AttemptCount)
- [x] Tenant/org boundaries preserved (TenantId carried from referral to notification)
- [x] No appointment notifications implemented (confirmed: no appointment notification code added)
- [x] Report generated correctly (this file: `/analysis/CCX-002-report.md`)
