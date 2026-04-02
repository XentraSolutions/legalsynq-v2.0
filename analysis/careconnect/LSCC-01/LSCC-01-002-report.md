# LSCC-01-002 — Referral Acceptance Flow Completion: Implementation Report

**Date:** 2026-04-02  
**Status:** COMPLETE

---

## Objective

Ensure the full law firm → provider referral intake handshake works end-to-end, including
a **client acceptance email notification** (the primary gap identified during analysis).

---

## Pre-existing Components Verified Working

The following were confirmed implemented before this task and required no changes:

| Component | Location | Status |
|---|---|---|
| Provider search + referral creation | `CareConnect.Application/Services/ReferralService.cs` | ✅ |
| Initial referral state = `New` | `Referral.ValidStatuses.New` (domain constant) | ✅ |
| Provider email notification (HMAC token CTA) | `SendNewReferralNotificationAsync` | ✅ |
| Token-based `/referrals/view` routing | `apps/web/src/app/referrals/view/page.tsx` | ✅ |
| Login redirect with `returnTo` guard | `apps/web/src/app/login/login-form.tsx` | ✅ |
| Public provider acceptance (`AcceptByTokenAsync`) | `ReferralService.AcceptByTokenAsync` | ✅ |
| Provider acceptance email (`ReferralAcceptedProvider`) | `SendAcceptanceConfirmationsAsync` | ✅ |
| Referrer/law-firm acceptance email (`ReferralAcceptedReferrer`) | `SendAcceptanceConfirmationsAsync` | ✅ |
| Law firm UI showing Accepted status | Referral list + detail pages | ✅ |

---

## Changes Implemented

### 1. Domain — `NotificationType.cs`

Added `ReferralAcceptedClient` constant and registered it in the `All` set:

```csharp
// LSCC-01-002: client acceptance notification
public const string ReferralAcceptedClient = "ReferralAcceptedClient";
```

The constant is validated by `NotificationType.IsValid(value)`, which guards the DB `NotificationType`
column. Adding it to `All` means existing notification-type enforcement in the application layer
automatically accepts this new value without any further changes.

### 2. Email Service — `ReferralEmailService.cs`

#### 2a. Third recipient in `SendAcceptanceConfirmationsAsync`

Added a client email block (recipient #3) after the existing provider and referrer blocks:

```
1. Provider email (ReferralAcceptedProvider) — unchanged
2. Referrer/law-firm email (ReferralAcceptedReferrer) — unchanged
3. Client email (ReferralAcceptedClient) — NEW
```

**Graceful skip behaviour:** If `referral.ClientEmail` is empty or whitespace, no notification record
is created and no exception is thrown. A `LogWarning` is emitted:

```
Skipping client acceptance email for referral {ReferralId}: no ClientEmail stored.
Acceptance is not blocked — provider and referrer have been notified.
```

This satisfies the HIPAA-aligned design principle: acceptance is never gated on client email delivery.

#### 2b. New `BuildClientAcceptanceHtml()` template

Client-facing HTML email:
- Subject: `Your case has been accepted — {ProviderOrganizationName}`
- Greeting uses `ClientFirstName`
- Names the accepted `RequestedService` and provider
- States provider will reach out directly
- Footer advisory: contact the referring party with questions
- **No appointment scheduling language** (decoupled per LSCC-01-001-01)

#### 2c. Retry case for `ReferralAcceptedClient` in `RetryNotificationAsync`

Added a `case NotificationType.ReferralAcceptedClient:` block in the retry switch:
- Reads `RecipientAddress` from the stored notification record
- Clears retry schedule if address is empty (cannot recover)
- Rebuilds subject and body from current referral/provider state
- Same retry-count-based `nextRetryAfterUtc` calculation as other acceptance types

#### 2d. Updated stale appointment language in existing templates

Both `BuildProviderAcceptanceHtml` and `BuildReferrerAcceptanceHtml` contained language
that assumed an appointment would follow acceptance. This was decoupled by LSCC-01-001-01
(InProgress state, appointment booking separated from acceptance). Copy updated:

| Template | Old copy | New copy |
|---|---|---|
| Provider | "The next step is to schedule an appointment." | "You can now begin coordinating care for this client directly." |
| Referrer | "They will be in touch with your client to schedule an appointment." | "[Provider] will be in touch with your client to continue coordinating care." |

### 3. Interface — `IReferralEmailService.cs`

Updated the `SendAcceptanceConfirmationsAsync` summary comment to document the third recipient
and the graceful-skip contract for missing `ClientEmail`.

---

## Test Coverage Added

New file: `CareConnect.Tests/Application/ReferralClientEmailTests.cs`  
**10 new tests**, all passing.

| Test | Validates |
|---|---|
| `NotificationType_ReferralAcceptedClient_HasExpectedStringValue` | Constant value is `"ReferralAcceptedClient"` |
| `NotificationType_All_ContainsReferralAcceptedClient` | `All` set includes the new type |
| `NotificationType_IsValid_ReturnsTrueForReferralAcceptedClient` | `IsValid()` guard accepts it |
| `NotificationRecipientType_All_ContainsClientEmail` | `ClientEmail` recipient type is registered |
| `NotificationRecipientType_IsValid_ReturnsTrueForClientEmail` | `IsValid()` guard accepts it |
| `NotificationType_AllThreeAcceptanceTypes_ArePresentAndDistinct` | All three acceptance types are distinct and valid |
| `SendAcceptanceConfirmationsAsync_WithClientEmail_PersistsClientNotification` | Notification record created with correct type + recipient type + address |
| `SendAcceptanceConfirmationsAsync_WithoutClientEmail_DoesNotPersistClientNotification` | No client notification created; no exception thrown |
| `SendAcceptanceConfirmationsAsync_WithClientEmail_SendsToClientAddress` | SMTP called with client address exactly once |
| `SendAcceptanceConfirmationsAsync_WithAllEmails_SendsThreeEmails` | All three SMTP sends fire when all addresses present |

**Full test suite result:** 385 passed, 5 failed (pre-existing `ProviderAvailabilityServiceTests` failures, unrelated to this task), 390 total.

---

## Data Flow (Complete End-to-End)

```
Law firm user
  → Search providers (CC API)
  → Create referral (CC API, status: New)
  → Provider receives email: "New referral received" (token CTA)
  → Provider clicks link → /referrals/view?token=...
      → if pending provider: public accept form
      → if active tenant provider: /login?returnTo=/careconnect/referrals/{id}
  → Provider accepts
  → AcceptByTokenAsync:
      1. Validates token (HMAC + version check)
      2. Guards against replay (status must be New)
      3. Calls referral.Accept() → status: Accepted
      4. Persists + fires background:
         → Provider email: "Referral accepted" (ReferralAcceptedProvider)
         → Law firm email: "Your referral was accepted" (ReferralAcceptedReferrer)
         → Client email:   "Your case has been accepted" (ReferralAcceptedClient) ← NEW
  → Law firm sees Accepted status in referral list/detail
```

---

## Files Changed

| File | Change |
|---|---|
| `CareConnect.Domain/NotificationType.cs` | Added `ReferralAcceptedClient` constant + `All` registration |
| `CareConnect.Application/Services/ReferralEmailService.cs` | Client email block, `BuildClientAcceptanceHtml`, retry case, updated provider/referrer copy |
| `CareConnect.Application/Interfaces/IReferralEmailService.cs` | Updated docstring |
| `CareConnect.Tests/Application/ReferralClientEmailTests.cs` | New test file (10 tests) |

---

## Design Decisions

- **No migration required**: `ReferralAcceptedClient` is a new notification type string stored in the existing `CareConnectNotifications.NotificationType` column (`varchar`). No schema changes needed.
- **Graceful degradation**: Missing client email does not block acceptance. This is consistent with the existing pattern for `ReferrerEmail` (which is also optional).
- **Fire-and-observe**: Client email is included in the same `Task.WhenAll(tasks)` within `SendAcceptanceConfirmationsAsync`, which is itself called fire-and-observe from `AcceptByTokenAsync`. Acceptance is never gated on email delivery.
- **Retry coverage**: `RetryNotificationAsync` now handles `ReferralAcceptedClient` using the same pattern as `ReferralAcceptedReferrer` (address sourced from the stored notification record).
