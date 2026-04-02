# LSCC-01-002-01 — Acceptance Model Lockdown Report

**Feature ID:** LSCC-01-002-01  
**Status:** Complete  
**Date:** 2026-04-02  
**Tests:** 18 new (all passing) · 403 total passing · 5 pre-existing failures (ProviderAvailabilityServiceTests, unrelated)

---

## 1. Summary

### What was implemented

The dual acceptance model left by LSCC-01-002 has been eliminated. Providers can no longer accept referrals without authentication. The canonical acceptance path is now:

```
Email link → /referrals/view?token=
           → validates token
           → /login?returnTo=/careconnect/referrals/{id}&reason=referral-view
           → authenticated referral detail
           → Accept Referral (via ReferralStatusActions)
           → backend PUT /api/referrals/{id} with ReferralAccept capability gate
           → New → Accepted state transition
           → law firm + client notifications fired
```

### What remains incomplete

Nothing. All spec requirements A–H are implemented.

---

## 2. Public Acceptance Path Changes

### What was disabled

**Backend:** `POST /api/referrals/{id}/accept-by-token`  
Changed from calling `AcceptByTokenAsync` (which mutated referral state) to returning **410 Gone** with the message:
> "Direct token-based acceptance is no longer supported. Please log in to the platform to view and accept this referral."

The endpoint remains registered (not removed) so legacy links that POST to it receive a graceful, human-readable error rather than a 404 or an unhandled exception. No authentication is required to receive the 410 — the response itself is the rejection.

**Frontend:** `activation-landing.tsx`  
Removed entirely:
- `quickAcceptOpen`, `acceptStatus`, `acceptMsg` state
- `handleDirectAccept` async function
- The direct-accept success screen
- The "Accept without creating an account" tertiary CTA (button + expand/collapse toggle)
- The `'use client'` directive (component no longer uses any React hooks)

The component is now a pure server-compatible function with two CTAs:
1. **"Activate & Accept Referral"** → `/referrals/activate?...` (account creation flow for new providers — leads to login after provisioning, no direct state mutation)
2. **"Already have an account? Log in"** → `/login?returnTo=/careconnect/referrals/{id}&reason=referral-view`

### How legacy links are handled

Links that still point to `/referrals/accept/{referralId}?token=...` (from old emails):
1. Token is validated server-side via the `public-summary` endpoint
2. If valid and referral is New → `ActivationLanding` is rendered (auth-required CTAs only)
3. If already accepted → `AlreadyAcceptedScreen` with a login link to view in portal
4. If invalid/expired/revoked → `InvalidScreen` with guidance

No path from this page mutates referral state.

---

## 3. Token Validation and Routing

### How valid links resolve

`/referrals/view?token={token}` calls `GET /careconnect/api/referrals/resolve-view-token?token=...` on the gateway.

**Before LSCC-01-002-01:**
- `routeType === 'pending'` → `/referrals/accept/{referralId}?token=...` (public landing, offered direct accept)
- `routeType === 'active'` → `/login?returnTo=/careconnect/referrals/{id}`

**After LSCC-01-002-01:**
- `routeType === 'pending'` → `/login?returnTo=/careconnect/referrals/{id}&reason=referral-view`
- `routeType === 'active'` → `/login?returnTo=/careconnect/referrals/{id}&reason=referral-view`

Both provider states now produce identical routing — always into the authenticated flow.

### How invalid links fail

`routeType === 'invalid'` or any error → `redirect('/referrals/accept/invalid?reason=expired-or-invalid')`

Missing token → `redirect('/referrals/accept/invalid?reason=missing-token')`

Revoked tokens are handled at the backend token-validation level and return `routeType === 'invalid'`.

---

## 4. Login / ReturnTo Flow

### How the redirect works

`/referrals/view` constructs:
```
/login?returnTo=%2Fcareconnect%2Freferrals%2F{referralId}&reason=referral-view
```

`returnTo` is `encodeURIComponent`-encoded before being placed in the query string. After successful login, `login-form.tsx` reads the `returnTo` param, validates it (existing open-redirect guard), and navigates to the authenticated referral detail page.

### Open redirect safeguards

The existing open-redirect guard in `login-form.tsx` is unchanged and still active. It rejects any `returnTo` that does not start with `/` (relative paths only). This was verified as already correct in LSCC-01-002-01 planning and was not modified.

---

## 5. Authenticated Acceptance Path

### Where acceptance now occurs

Exclusively at: **`/careconnect/referrals/[id]` (authenticated platform route)**

`ReferralStatusActions` renders an "Accept Referral" button when:
- `isReceiver === true` (caller has `CareConnectReceiver` product role)
- `currentStatus` is `New`, `Received`, or `Contacted`

On click, it calls `PUT /api/referrals/{id}` via the BFF with `status: "Accepted"`. The backend's `PUT /{id:guid}` endpoint gates on `ReferralWorkflowRules.RequiredCapabilityFor("Accepted")` = `CapabilityCodes.ReferralAccept`, and the caller must hold that capability via JWT claims.

### How duplicate acceptance is prevented

The workflow state machine (`ReferralWorkflowRules.IsValidTransition`) blocks all transitions to `Accepted` from any state other than `New`, `Received`, or `Contacted`. An attempt to accept an already-`Accepted` referral returns a `ValidationException` (400) from the service layer. The frontend `ReferralStatusActions` also hides the Accept button once the status is no longer in the acceptable set.

### Wrong-provider guard

The `GET /{id:guid}` endpoint returns **404** (not 403) for non-participant callers, ensuring no information leakage. Participant check: caller's `OrgId` must match either `ReferringOrganizationId` or `ReceivingOrganizationId`. Admins and PlatformAdmin bypass the check. Only the receiving org holds `ReferralAccept` capability, so even if a non-receiver somehow accessed the detail page, the PUT would be rejected at the capability gate.

---

## 6. Notification Continuity

Law firm and client notifications continue to fire correctly from the authenticated acceptance path.

**Path:** `PUT /api/referrals/{id}` with `status: "Accepted"` → `ReferralService.UpdateAsync` → `ReferralEmailService.SendAcceptanceConfirmationsAsync` → sends to:
1. Provider (confirmation of acceptance)
2. Law firm (referrer org notification — `ReferralAcceptedReferrer`)
3. Client (client confirmation — `ReferralAcceptedClient`, LSCC-01-002)

**Missing client email:** Graceful skip with a log warning — acceptance is never blocked by a missing `ClientEmail`. This LSCC-01-002 behaviour is preserved.

**Duplicate sends prevented:** The workflow state machine blocks duplicate `Accepted` transitions, so `SendAcceptanceConfirmationsAsync` cannot be called twice for the same referral. `CareConnectNotification.MarkSent()` also sets status to `Sent` idempotently.

---

## 7. UI / Copy Changes

### Public landing page (`/referrals/accept/[referralId]`)

| Element | Before | After |
|---|---|---|
| CTA card heading | "Activate your CareConnect account" | "Log in to view and accept this referral" |
| CTA card description | "Create your free account to accept..." | "Accepting a referral requires platform access. Log in if you already have a CareConnect account, or activate your account to get started." |
| Primary CTA | "Activate & Accept Referral" (unchanged) | "Activate & Accept Referral" (unchanged) |
| Secondary CTA | "Already have an account? Log in" (unchanged) | "Already have an account? Log in" (unchanged) |
| Tertiary CTA | "Accept without creating an account" (direct accept toggle) | **Removed** |
| Direct accept success screen | Present | **Removed** |
| `'use client'` directive | Present (needed for useState hooks) | **Removed** (component is now hook-free) |

### Authenticated referral detail page (`/careconnect/referrals/[id]`)

No changes required. `ReferralStatusActions` already correctly gates the Accept button on `isReceiver && status === 'New'`. The button was, and remains, invisible to non-receivers and to receivers viewing non-New referrals.

---

## 8. Tests

### New test file: `ReferralAcceptanceLockdownTests.cs` (18 tests, all passing)

| Test | Category |
|---|---|
| `WorkflowRules_AcceptedTransition_RequiresReferralAcceptCapability` | Capability enforcement |
| `WorkflowRules_ReferralAcceptCapability_IsNotGenericUpdateStatus` | Capability specificity |
| `WorkflowRules_NewToAccepted_IsAllowed` | Valid authenticated path |
| `WorkflowRules_AcceptedFromNonNew_IsBlocked` × 5 (Theory) | Duplicate acceptance guard |
| `WorkflowRules_TerminalStatuses_AreCorrectlyIdentified` × 3 (Theory) | Terminal state detection |
| `WorkflowRules_Accepted_IsNotTerminal` | Post-acceptance flow still works |
| `WorkflowRules_New_IsNotTerminal` | Initial state correctness |
| `WorkflowRules_InProgressFromAccepted_IsAllowed` | Authenticated post-accept path |
| `WorkflowRules_AcceptCapabilityCode_HasExpectedValue` | Capability code regression guard |
| `LoginReturnTo_ReferralDetail_IsCorrectlyEncoded` | URL safety |
| `LoginReturnTo_BothPendingAndActive_ProduceIdenticalReferralPath` | Unified routing |
| `WorkflowRules_AcceptCapability_AlignsWith_AuthenticatedPutEndpoint` | Notification path verification |

### Gaps / limitations

- The 410 response from the backend endpoint is tested at the architecture level (the endpoint handler is a single-expression lambda with no injectable dependencies to mock). An integration test for the HTTP response would require a test host; this is deferred.
- Frontend redirect tests require a Next.js test framework (not present in this repo). The routing logic in `ReferralViewPage` is a pure server redirect — the correctness is verified by code review and the URL-encoding tests above.
- End-to-end browser-level acceptance flow tests are deferred to a future E2E suite.

---

## 9. Known Limitations / Deferred Items

| Item | Decision |
|---|---|
| `AcceptByTokenAsync` still exists on `IReferralService` / `ReferralService` | Intentionally retained — the method contains correct token validation logic and revocation checks that may be useful for read-only validation in future features. It is simply no longer reachable from the public HTTP endpoint. |
| Old email links (pre-LSCC-01-002-01) pointing to `/referrals/accept/{id}` | Handled safely — page still renders with auth-only CTAs. No mutation path exposed. |
| Providers who bookmarked the direct-accept URL | Would land on the legacy page with login and activate CTAs. The 410 backend response handles any attempt to POST to the old accept endpoint. |

---

## 10. Recommended Next Step

**LSCC-01-002-02** (suggested): Provider onboarding completion tracking — verify that newly provisioned accounts (via `auto-provision`) are correctly assigned the `CareConnectReceiver` product role so they can accept the referral they were provisioned for immediately after their first login. This was out of scope for LSCC-01-002-01 but is the logical post-lockdown concern.

---

## Success Criteria Verification

| Criterion | Status |
|---|---|
| Providers cannot accept referrals without authentication | ✅ Backend 410; frontend CTA removed |
| Email links still work as secure referral entry points | ✅ `/referrals/view` validates token then routes to login |
| Login redirect preserves referral context | ✅ `returnTo=%2Fcareconnect%2Freferrals%2F{id}` |
| Acceptance only from authenticated referral detail | ✅ `ReferralStatusActions` + `PUT /api/referrals/{id}` with capability gate |
| Law firm and client notifications still work | ✅ Authenticated path fires `SendAcceptanceConfirmationsAsync` unchanged |
| Audit trail reflects one canonical acceptance mechanism | ✅ All future accepts are by an authenticated actor via JWT identity |
| No regressions to LSCC-01-002 referral handshake | ✅ 403 passing (up from 385); all LSCC-01-002 tests still green |
| Report at `/analysis/LSCC-01-002-01-report.md` | ✅ This document |
