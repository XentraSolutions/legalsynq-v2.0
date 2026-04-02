# LSCC-005 — Minimal Referral Flow + Basic Dashboard Analytics
## Implementation Report

**Date:** 2026-03-31  
**Status:** Complete (including post-completion bug fix)  
**Feature:** Full referral notification flow + 30-day Referral Activity dashboard section

---

## 1. Executive Summary

LSCC-005 implements the end-to-end referral notification flow:

1. Referrer creates a referral via the provider list "Refer Patient" button.
2. The receiving provider receives an HTML email with a secure HMAC-SHA256 View Referral link.
3. **Pending providers** (no Identity Organization link, `OrganizationId IS NULL`) land on a public `/referrals/accept/[id]` page and click Accept.
4. **Active-tenant providers** are deep-linked through the platform login page with a `returnTo` param.
5. On acceptance, confirmation emails are sent to both the provider and the referrer.
6. The CareConnect dashboard gains a fixed 30-day **Referral Activity** section (4 KPI cards).

The "New" referral status is the canonical equivalent of "Pending" in the LSCC-005 spec. No new status values were introduced.

---

## 2. Deliverables

### 2.1 New Backend Files

| File | Purpose |
|------|---------|
| `CareConnect.Application/Interfaces/IReferralEmailService.cs` | Service interface: token generation/validation, email dispatch |
| `CareConnect.Application/Interfaces/ISmtpEmailSender.cs` | SMTP sender abstraction |
| `CareConnect.Application/DTOs/AcceptByTokenRequest.cs` | Request DTO for `POST /accept-by-token` |
| `CareConnect.Application/DTOs/ReferralViewTokenRouteResponse.cs` | Response DTO for `GET /resolve-view-token` |
| `CareConnect.Application/Services/ReferralEmailService.cs` | HMAC-SHA256 token logic, HTML email templates, notification record creation |
| `CareConnect.Infrastructure/Email/SmtpEmailSender.cs` | SMTP implementation; explicit failure logging |
| `CareConnect.Infrastructure/Data/Migrations/20260401100000_AddReferrerFieldsToReferral.cs` | DB migration adding `ReferrerEmail`/`ReferrerName` columns |
| `CareConnect.Infrastructure/Data/Migrations/20260401100000_AddReferrerFieldsToReferral.Designer.cs` | EF migration designer snapshot |
| `CareConnect.Tests/Application/ReferralEmailServiceTests.cs` | 14 new tests for token + email logic |

### 2.2 New Frontend Files

| File | Purpose |
|------|---------|
| `apps/web/src/app/referrals/view/page.tsx` | Server Component; resolves token → redirects to accept page or login+returnTo |
| `apps/web/src/app/referrals/accept/[referralId]/page.tsx` | Public Client Component; shows referral summary + Accept button; `/invalid` sub-path for bad tokens |

### 2.3 Modified Backend Files

| File | Change |
|------|--------|
| `CareConnect.Domain/Referral.cs` | Added `ReferrerEmail`, `ReferrerName` fields; `Accept(Guid?)` method |
| `CareConnect.Domain/NotificationType.cs` | Added `ReferralCreated`, `ReferralAcceptedProvider`, `ReferralAcceptedReferrer` |
| `CareConnect.Domain/CareConnectNotification.cs` | Added `MarkSent()`, `MarkFailed()` domain methods |
| `CareConnect.Application/DTOs/CreateReferralRequest.cs` | Added `ReferrerEmail?`, `ReferrerName?` |
| `CareConnect.Application/Interfaces/IReferralService.cs` | Added `ResolveViewTokenAsync`, `AcceptByTokenAsync` |
| `CareConnect.Application/Repositories/IReferralRepository.cs` | Added `GetByIdGlobalAsync` (cross-tenant lookup) |
| `CareConnect.Application/Repositories/INotificationRepository.cs` | Added `AddAsync` |
| `CareConnect.Application/Services/ReferralService.cs` | `CreateAsync`: stores referrer fields + fires email notification; added `ResolveViewTokenAsync`, `AcceptByTokenAsync` |
| `CareConnect.Api/Endpoints/ReferralEndpoints.cs` | Added `GET /resolve-view-token` and `POST /{id}/accept-by-token` public endpoints |
| `CareConnect.Infrastructure/DependencyInjection.cs` | Registered `IReferralEmailService`, `ISmtpEmailSender`, `INotificationRepository` |
| `CareConnect.Infrastructure/Repositories/ReferralRepository.cs` | Implemented `GetByIdGlobalAsync` |
| `CareConnect.Infrastructure/Repositories/NotificationRepository.cs` | New `AddAsync` implementation |

### 2.4 Modified Frontend Files

| File | Change |
|------|--------|
| `apps/web/src/middleware.ts` | Added `/referrals/view` and `/referrals/accept` to `PUBLIC_PATHS` |
| `apps/web/src/components/auth/login-form.tsx` | `returnTo` query param support with open-redirect guard (`/` prefix, not `//`) |
| `apps/web/src/components/careconnect/provider-card.tsx` | Converted to Client Component; `isReferrer` + referrer identity props; "Refer Patient" button with `CreateReferralForm` modal via `useState` |
| `apps/web/src/components/careconnect/provider-map-shell.tsx` | Pulls referrer identity from `useSession()`; passes `isReferrer`, `referrerEmail`, `referrerName` to `ProviderCard` |
| `apps/web/src/components/careconnect/create-referral-form.tsx` | `referrerEmail?`/`referrerName?` props forwarded in payload |
| `apps/web/src/types/careconnect.ts` | Added `referrerEmail?`, `referrerName?` to `CreateReferralRequest` |
| `apps/web/src/lib/careconnect-api.ts` | Added `referrals.acceptByToken(id, token)` |
| `apps/web/src/app/(platform)/careconnect/dashboard/page.tsx` | Fixed 30-day Referral Activity section (4 KPI cards); visible for referrer role only |

---

## 3. Architecture Decisions

### 3.1 HMAC-SHA256 Token Strategy

View tokens are HMAC-SHA256 signed and Base64url-encoded with the format:

```
{referralId}:{expiryUnixSeconds}:{hmacHex}
```

- **TTL:** 30 days from creation.
- **Config key:** `ReferralToken:Secret`. Falls back to a hard-coded development constant with a `LogWarning` on every request — intentionally loud to prevent accidental production use.
- **URL-safe encoding:** `+` → `-`, `/` → `_`, no `=` padding. Validated by roundtrip tests.
- **Tamper detection:** HMAC is verified via `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.
- Tokens are single-purpose (view only). Accept action requires presenting the token again at `POST /accept-by-token`, which re-validates HMAC + expiry.

### 3.2 Fire-and-Observe Email Dispatch

Email notifications never gate referral creation or acceptance:

```csharp
_ = Task.Run(async () => {
    try { await _emailService.SendNewReferralNotificationAsync(...); }
    catch (Exception ex) { _logger.LogWarning(ex, "Background referral notification failed..."); }
});
```

If SMTP is not configured (`Smtp:Host` absent), `SmtpEmailSender` throws `InvalidOperationException`, which is caught by the fire-and-observe wrapper. The referral is saved and the notification record is created with `Pending` status. This means a missing SMTP config is silent to the end user but visible in warning logs — intentional for the development environment.

### 3.3 Provider Routing at Token Resolution

`GET /api/referrals/resolve-view-token?token=X` returns one of four `routeType` values:

| routeType | Meaning | Frontend action |
|-----------|---------|-----------------|
| `pending` | Provider has `OrganizationId IS NULL` | Redirect to `/referrals/accept/[id]?token=X` |
| `active` | Provider has an Identity org link | Redirect to login with `returnTo=/careconnect/referrals/[id]` |
| `invalid` | Token is expired, malformed, or HMAC mismatch | Redirect to `/referrals/accept/invalid` |
| `notfound` | Referral was deleted or never existed | Same as invalid — no information leak |

The routing is resolved server-side in a Next.js Server Component (`/referrals/view/page.tsx`), which calls the gateway and issues the appropriate `redirect()`. The provider's `OrganizationId` is the single source of truth for "pending" vs "active" classification.

### 3.4 Cross-Tenant Provider Lookup (Bug Fix)

Providers are a **platform-wide marketplace** — `ProviderRepository.BuildBaseQuery` deliberately omits the TenantId filter (`var q = _db.Providers.AsQueryable()` with a comment explaining the intent). However, `ReferralService.CreateAsync` originally used `GetByIdAsync(tenantId, providerId)`, which includes a `WHERE TenantId = @tenantId` clause. Since all 14 providers in the current environment have a different TenantId than the referrer's JWT, every create attempt returned `NotFoundException` → 404.

**Fix:** `CreateAsync` now calls `GetByIdCrossAsync(providerId)`, which matches the marketplace design. This is consistent with `ProviderService.GetAvailabilityAsync`, which already used the cross-tenant lookup.

### 3.5 Dashboard Referral Activity — Fixed 30-Day Window

The Referral Activity section uses a **hardcoded 30-day window** (today minus 30 days), not the user-adjustable date range from LSCC-004's Performance Overview. This is by spec design: the 4 KPI cards are intended as a quick operational snapshot, not an analytics tool.

The 4 cards:
- **Total Referrals** — all referrals created in the window
- **Pending** — referrals currently in `New` status
- **Accepted** — referrals currently in `Accepted` status
- **Acceptance Rate** — `Accepted / Total × 100%`

Data is fetched via 4 parallel `Promise.allSettled` calls to the existing referral search API with `createdFrom`/`createdTo` and `status` filters.

---

## 4. Public Routes and Auth

### 4.1 Middleware

`/referrals/view` and `/referrals/accept` are added to `PUBLIC_PATHS` in `middleware.ts`. The matcher excludes these paths from the session check, allowing unauthenticated access.

### 4.2 Login Deep-Link (`returnTo`)

The login form reads `?returnTo=<path>` from the URL and, on successful login, redirects to that path instead of the default dashboard. Open-redirect guard: the value must start with `/` but not `//` (relative path only, no protocol-relative URLs).

Active-tenant providers receive an email with a link like:
```
https://app.legalsynq.com/login?returnTo=/careconnect/referrals/{id}
```

On login, they land directly on the referral detail page.

### 4.3 Public Accept Endpoint

`POST /api/referrals/{id}/accept-by-token` requires **no auth**. It:
1. Validates the HMAC token matches the referral ID.
2. Asserts the referral is in `New` status.
3. Calls `referral.Accept(updatedByUserId: null)` (null because no Identity user is acting).
4. Persists the status change + history record.
5. Fires confirmation emails (fire-and-observe) to both provider and referrer.

---

## 5. Test Coverage

### 5.1 New Tests (`ReferralEmailServiceTests.cs`)

**14 tests added**, all passing.

| Group | Tests |
|-------|-------|
| Token round-trip (generate → validate → referralId match) | 2 |
| URL-safe Base64 encoding (no `+`, `/`, or `=` chars) | 1 |
| Token expiry (valid within TTL, rejected after TTL) | 2 |
| HMAC tampering (modified payload, modified signature) | 2 |
| Wrong secret (token signed with different key rejected) | 1 |
| Malformed inputs (null, empty, too short, missing colons) | 4 |
| Dev fallback (no config → warning log, fallback key used) | 2 |

### 5.2 Total Test Suite

```
Failed:  5  (pre-existing ProviderAvailabilityServiceTests — unrelated)
Passed: 231
Total:  236
```

The pre-existing failures in `ProviderAvailabilityServiceTests` are caused by an in-memory mock that does not implement `GetByIdCrossAsync`, which was added in an earlier phase. They are unchanged and unrelated to LSCC-005.

---

## 6. Email Templates

Two HTML email templates are rendered inline in `ReferralEmailService`:

### New Referral Notification (to provider)
- Subject: `New Referral: {ClientFirstName} {ClientLastName}`
- Body: referral summary table (client name, DOB, phone, email, service, urgency, notes), referrer identity, View Referral button linking to `/referrals/view?token=X`.

### Acceptance Confirmation (2 emails fired on accept)
- **To provider:** `Referral Accepted — {ClientFirstName} {ClientLastName}` — confirmation that their acceptance was recorded.
- **To referrer:** `Your Referral Was Accepted — {ClientFirstName} {ClientLastName}` — notifies the referrer the provider has accepted.

If `ReferrerEmail` is null/empty, the referrer confirmation email is skipped silently (logged at Debug level).

---

## 7. HIPAA / Security Notes

- Tokens are HMAC-signed; they do not contain any PHI — only the referral UUID and expiry timestamp.
- Token links are single-use in practice (the accept page transitions the referral to `Accepted`; subsequent token validation will succeed but `AcceptByTokenAsync` will throw `InvalidOperationException` for a non-`New` status).
- The `GET /resolve-view-token` endpoint returns `routeType: "notfound"` for deleted referrals rather than a distinct error, to avoid confirming existence to unauthenticated callers.
- SMTP credentials are never logged; the `SmtpEmailSender` only logs host/port configuration, not credentials.
- No PHI appears in URL query parameters except on the authenticated referral detail page (which is behind the platform session).

---

## 8. Configuration Reference

| Key | Required | Description |
|-----|----------|-------------|
| `ReferralToken:Secret` | Prod only | HMAC signing key; dev falls back with LogWarning |
| `AppBaseUrl` | Recommended | Used to construct View Referral links (default: `http://localhost:3000`) |
| `Smtp:Host` | Required for email | SMTP server hostname |
| `Smtp:Port` | Optional | Default: 587 |
| `Smtp:EnableSsl` | Optional | Default: true |
| `Smtp:Username` | Optional | SMTP auth username |
| `Smtp:Password` | Optional | SMTP auth password |
| `Smtp:FromAddress` | Required for email | Sender address |
| `Smtp:FromName` | Optional | Sender display name (default: "LegalSynq") |

---

## 9. What Was Not Done (Out of Scope for LSCC-005)

- No real-time notification (WebSocket / SignalR) — email only.
- No in-app notification bell for referral acceptance — notification records are written to DB but not surfaced in the UI beyond the existing notification list.
- No provider-side referral inbox UI — providers use the public accept page or the existing platform referral list.
- No PDF/attachment delivery in emails.
- No token revocation / one-time-use enforcement at the token level (status check in `AcceptByTokenAsync` provides functional idempotency).
- Active-tenant provider deep-link flow (`routeType: "active"`) is implemented in the backend and frontend router, but the referral detail page for authenticated providers was pre-existing (LSCC-002). No new UI was built for that path.

---

## 10. Files Summary

```
New (backend):
  CareConnect.Application/Interfaces/IReferralEmailService.cs
  CareConnect.Application/Interfaces/ISmtpEmailSender.cs
  CareConnect.Application/DTOs/AcceptByTokenRequest.cs
  CareConnect.Application/DTOs/ReferralViewTokenRouteResponse.cs
  CareConnect.Application/Services/ReferralEmailService.cs
  CareConnect.Infrastructure/Email/SmtpEmailSender.cs
  CareConnect.Infrastructure/Data/Migrations/20260401100000_AddReferrerFieldsToReferral.cs
  CareConnect.Infrastructure/Data/Migrations/20260401100000_AddReferrerFieldsToReferral.Designer.cs
  CareConnect.Tests/Application/ReferralEmailServiceTests.cs

New (frontend):
  apps/web/src/app/referrals/view/page.tsx
  apps/web/src/app/referrals/accept/[referralId]/page.tsx

Modified (backend):
  CareConnect.Domain/Referral.cs
  CareConnect.Domain/NotificationType.cs
  CareConnect.Domain/CareConnectNotification.cs
  CareConnect.Application/DTOs/CreateReferralRequest.cs
  CareConnect.Application/Interfaces/IReferralService.cs
  CareConnect.Application/Repositories/IReferralRepository.cs
  CareConnect.Application/Repositories/INotificationRepository.cs
  CareConnect.Application/Services/ReferralService.cs
  CareConnect.Api/Endpoints/ReferralEndpoints.cs
  CareConnect.Infrastructure/DependencyInjection.cs
  CareConnect.Infrastructure/Repositories/ReferralRepository.cs
  CareConnect.Infrastructure/Repositories/NotificationRepository.cs

Modified (frontend):
  apps/web/src/middleware.ts
  apps/web/src/components/auth/login-form.tsx
  apps/web/src/components/careconnect/provider-card.tsx
  apps/web/src/components/careconnect/provider-map-shell.tsx
  apps/web/src/components/careconnect/create-referral-form.tsx
  apps/web/src/types/careconnect.ts
  apps/web/src/lib/careconnect-api.ts
  apps/web/src/app/(platform)/careconnect/dashboard/page.tsx

Analysis:
  analysis/LSCC-005-report.md
```
