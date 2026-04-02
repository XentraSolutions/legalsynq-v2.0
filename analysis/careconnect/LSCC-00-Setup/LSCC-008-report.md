# LSCC-008 — Provider Activation Funnel

## Status: Complete

**Date:** 2026-03-31  
**Ticket:** LSCC-008 Provider Activation Funnel  
**Scope:** End-to-end funnel that guides a provider from the referral notification email through account activation or direct acceptance, without requiring an authenticated session.

---

## Summary

When a referral is sent to a provider whose organisation is not yet linked to the platform (pending provider), the existing email link now routes them through a structured funnel:

```
Email Link
   └── /referrals/accept/[referralId]?token=...
         ├── Token invalid / expired → /referrals/accept/invalid?reason=...
         ├── Referral already accepted → AlreadyAcceptedScreen
         └── Pending referral
               ├── [Primary] /referrals/activate?referralId=...&token=...  (activation intent capture)
               ├── [Secondary] /login?returnTo=...&reason=referral-view     (existing account)
               └── [Tertiary] Direct accept-by-token (no account — collapsible)
```

The activate page collects the provider's name and email, records an `ActivationStarted` funnel event in the audit system, and shows a confirmation screen. Manual admin provisioning completes the account creation (automated provisioning is a future task).

For an active provider (OrganizationId set), the link still routes to `/login?returnTo=...` as before.

---

## Changes

### Backend — CareConnect.Application

**`DTOs/ReferralPublicSummaryResponse.cs`** (new)
- Properties: `ReferralId`, `ClientFirstName`, `ClientLastName`, `ReferrerName`, `ProviderName`, `RequestedService`, `Status`
- Computed: `IsAlreadyAccepted` → `true` when `Status` is not `"New"` (Accepted, Declined, Cancelled, Completed, Scheduled)
- Only exposes fields already present in the provider notification email — no additional PHI exposure

**`DTOs/TrackFunnelEventRequest.cs`** (new)
- `Token: string` — the HMAC view token from the email link
- `EventType: string` — allowlist-validated event identifier

**`Interfaces/IReferralService.cs`** — two new method signatures:
- `Task<ReferralPublicSummaryResponse?> GetPublicSummaryAsync(Guid referralId, string token, CancellationToken ct)`
- `Task<bool> TrackFunnelEventAsync(Guid referralId, string token, string eventType, CancellationToken ct)`

**`Services/ReferralService.cs`** — both methods implemented:
- `GetPublicSummaryAsync`: validates HMAC token, checks version against `referral.TokenVersion`, maps to DTO. Returns null on any validation failure (no data leak).
- `TrackFunnelEventAsync`: allowlist check (`ReferralViewed`, `ActivationStarted`) → token validation → `IngestAuditEventRequest` fire-and-forget via `_auditClient`. Returns `false` for unknown event types or invalid tokens.

### Backend — CareConnect.Api

**`Endpoints/ReferralEndpoints.cs`** — two new endpoints (no `.RequireAuthorization`, token-gated):

| Method | Path | Auth |
|--------|------|------|
| GET    | `/careconnect/api/referrals/{id}/public-summary?token=...` | HMAC token |
| POST   | `/careconnect/api/referrals/{id}/track-funnel` | HMAC token in body |

Both endpoints return 400 for missing parameters and 401 for invalid tokens.

### Frontend — Next.js Web App

**`middleware.ts`** — `/referrals/activate` added to `PUBLIC_PATHS` (no session cookie required)

**`app/referrals/accept/[referralId]/page.tsx`** (rebuilt as server component)
- Fetches `/public-summary` server-side on every request (`no-store`)
- `referralId === 'invalid'` → renders `<InvalidScreen reason="...">` (handles `missing-token`, `revoked`, `expired-or-invalid`)
- Null summary → redirects to `/referrals/accept/invalid?reason=expired-or-invalid`
- `summary.isAlreadyAccepted` → renders `<AlreadyAcceptedScreen>`
- Otherwise → renders `<ActivationLanding>` (client component)

**`app/referrals/accept/[referralId]/activation-landing.tsx`** (client component)
- Displays referral summary card (client name, referred by, requested service)
- Benefits panel (4 bullet points for platform value)
- Primary CTA: **"Activate & Accept Referral"** → `/referrals/activate?referralId=...&token=...`
- Secondary CTA: **"Already have an account? Log in"** → `/login?returnTo=...&reason=referral-view`
- Tertiary (collapsible): **"Accept without creating an account"** → calls `accept-by-token` directly, shows inline result

**`app/referrals/activate/page.tsx`** (new — server component)
- Reads `referralId` + `token` from query string
- Fetches public summary, redirects to invalid screen if validation fails
- If `isAlreadyAccepted` → redirects back to the accept page (which shows `AlreadyAcceptedScreen`)
- Renders context banner (client name, referred by, service) + `<ActivationForm>`

**`app/referrals/activate/activation-form.tsx`** (new — client component)
- Name + email form fields
- On submit: calls `POST /api/careconnect/api/referrals/{id}/track-funnel` with `ActivationStarted`
- Success: confirmation screen with activation details and login shortcut
- Error: inline error message with fallback suggestion

### Tests — CareConnect.Tests

**`Application/ProviderActivationFunnelTests.cs`** (new — 22 test cases)

| Test Group | Scenarios |
|------------|-----------|
| A. Provider state detection | Pending (null OrganizationId), Active (set OrganizationId), Invalid token → invalid, Revoked token → invalid |
| B. GetPublicSummaryAsync | Valid token → correct DTO, Invalid token → null, Revoked token → null, ReferralId mismatch → null |
| C. Accepted-referral edge case | `IsAlreadyAccepted` computed property: false for "New", true for all other statuses (Theory) |
| D. TrackFunnelEventAsync | Allowed event types (Theory x4), Unknown event types (Theory x4), Invalid token → false, Revoked token → false |
| E. Return URL logic | Active provider login URL, Pending provider activation URL format |

---

## Provider State Detection Rule

```
provider.OrganizationId.HasValue
    true  → "active"   → /login?returnTo=/careconnect/referrals/{id}&reason=referral-view
    false → "pending"  → /referrals/accept/{id}?token=... → ActivationLanding
    null  → "invalid"  → /referrals/accept/invalid?reason=...
```

---

## Security Considerations

1. **Token validated before every data access** — `ValidateViewToken` (HMAC-SHA256) is called before any database read in both `GetPublicSummaryAsync` and `TrackFunnelEventAsync`.
2. **Token version check** — stale tokens are rejected when `TokenVersion` does not match the current referral's version, preventing reuse of revoked email links.
3. **Allowlist for event types** — `TrackFunnelEventAsync` rejects any `eventType` not in `{"ReferralViewed", "ActivationStarted"}`.
4. **Minimal PHI exposure** — `ReferralPublicSummaryResponse` contains only: client name, referrer name, provider name, requested service, and status. No DOB, case numbers, or notes are exposed.
5. **No unauthenticated write to referral state** — the `ActivationStarted` event emits to the audit log only; it does not modify the referral record.

---

## Deferred Items (future tickets)

| Item | Notes |
|------|-------|
| Automated tenant provisioning | Currently manual — admin creates tenant and links provider |
| Email notification after activation request | Should email the admin + referrer |
| Activation request queue / admin UI | Dashboard view of pending activation requests |
| Rate limiting on public-summary + track-funnel | Protect against enumeration |
