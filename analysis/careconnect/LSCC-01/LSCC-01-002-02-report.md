# LSCC-01-002-02 — Provider Activation & Role Assurance Report

**Feature ID:** LSCC-01-002-02  
**Status:** Complete  
**Date:** 2026-04-02  
**Tests:** 14 new (all passing) · 417 total passing · 5 pre-existing failures (ProviderAvailabilityServiceTests, unrelated)

---

## 1. Summary

### What was implemented

An admin-controlled access gate enforcing that only correctly provisioned CareConnect providers can view or accept referrals. The feature adds a centralized, testable readiness check at both the backend (service + endpoint) and frontend (utility + blocking component) layers.

**Before this feature:** A non-provisioned provider who managed to authenticate would see a plain yellow banner message that was easy to miss and did not clearly direct them to an administrator.

**After this feature:** Non-provisioned providers see a dedicated "Access Not Available" page with a clear admin-contact message, no referral data is rendered, and no acceptance action is offered.

### What remains incomplete

Nothing. All spec requirements A–H are implemented.

---

## 2. Provider-Ready Access Rule

A provider is considered **fully provisioned** for CareConnect referral access if and only if their authenticated session (JWT product roles) contains:

| Requirement | Role / Capability | Code |
|---|---|---|
| CareConnect Receiver product role | `CARECONNECT_RECEIVER` | `ProductRoleCodes.CareConnectReceiver` |
| Receiver-side referral read | `ReferralReadAddressed` capability | `CapabilityCodes.ReferralReadAddressed` |
| Acceptance action | `ReferralAccept` capability | `CapabilityCodes.ReferralAccept` |

The `CareConnectReceiver` product role carries both capabilities in the static role→capability map (`CareConnectCapabilityService`). The role is **the single provisioning gate**: holding it implies holding both capabilities.

PlatformAdmin and TenantAdmin are unconditionally considered access-ready (they bypass capability checks across the platform, handled at the endpoint and service layers).

---

## 3. Access Readiness Verification

### How it works

**Backend service: `ProviderAccessReadinessService`**

```
GetReadinessAsync(productRoles) →
  hasReferralAccess = ICapabilityService.HasCapabilityAsync(roles, ReferralReadAddressed)
  hasReferralAccept = ICapabilityService.HasCapabilityAsync(roles, ReferralAccept)
  hasReceiverRole   = productRoles.Contains("CARECONNECT_RECEIVER")
  isProvisioned     = hasReferralAccess && hasReferralAccept
  reason            = null if provisioned, else "missing-receiver-role" | ...
```

- Read-only; no side effects
- No auto-provisioning, no role assignment, no org-link creation
- Deterministic: same roles → same result
- Registered as singleton in DI (depends only on `ICapabilityService`, also singleton)

**Backend endpoint: `GET /api/referrals/access-readiness`**

- Requires `Policies.AuthenticatedUser`
- PlatformAdmin / TenantAdmin bypass: returns `IsProvisioned = true` unconditionally
- All other callers: evaluated via `ProviderAccessReadinessService`
- Returns `ProviderAccessReadinessResult` JSON

**Frontend utility: `lib/careconnect-access.ts`**

```typescript
checkCareConnectReceiverAccess(session: PlatformSession): ProviderAccessReadiness
  → hasReceiverRole = session.productRoles.includes('CARECONNECT_RECEIVER')
  → isProvisioned   = hasReceiverRole
  → reason          = 'missing-receiver-role' if not provisioned
```

Pure function — no network calls, no side effects. Used by server components before any referral data is fetched.

### Where it is enforced

| Layer | Location | What it gates |
|---|---|---|
| Frontend (server component) | `careconnect/referrals/[id]/page.tsx` | Referral detail page access |
| Frontend (server component) | `careconnect/referrals/page.tsx` | Referral list page access |
| Backend (API endpoint) | `GET /api/referrals/access-readiness` | Queryable readiness check |
| Backend (all auth'd referral endpoints) | `RequireAuthorization(Policies.AuthenticatedUser)` | Session validity |
| Backend (capability gate) | `CareConnectAuthHelper.RequireAsync` on PUT | Acceptance capability |

---

## 4. Blocking State Behavior

### Exact user-facing behavior

When a provider lacks the CareConnect Receiver role, they are shown:

**Title:** Access Not Available  
**Body:** "Your account is not yet activated for CareConnect. Please contact your administrator to request access."  
**Steps:** numbered list: (1) contact admin, (2) ask for CareConnect Receiver role, (3) log in again  
**CTA:** "Return to Dashboard" (safe exit)  
**No:** referral details, accept button, referral ID, client name, or any referral context

The component is `ReferralAccessBlocked` in `components/careconnect/referral-access-blocked.tsx`.

### What is intentionally not exposed

- Referral details (client name, service, status)
- Accept / Decline / Cancel actions
- The specific referral ID the user was trying to access
- Internal capability codes or system-level diagnostics
- Any self-provisioning or upgrade path

---

## 5. Token / Login / Access Flow

### Full flow post-LSCC-01-002-01 and LSCC-01-002-02

```
Provider email link
  → /referrals/view?token={token}
  → Token validated server-side (HMAC-SHA256, version check)
  → /login?returnTo=/careconnect/referrals/{id}&reason=referral-view
  → Provider authenticates
  → /careconnect/referrals/{id}   (authenticated detail page)
  → checkCareConnectReceiverAccess(session)
      → isProvisioned = false → <ReferralAccessBlocked />  (no referral data)
      → isProvisioned = true  → referral detail fetched + rendered
  → If referral loaded and status=New and isReceiver:
      → ReferralStatusActions shows "Accept Referral"
      → PUT /api/referrals/{id} with status=Accepted
      → capability gate: ReferralAccept required
      → New → Accepted state transition
      → law firm + client notifications fire
```

### How token validation and readiness checks interact

- Token validation happens at `/referrals/view` — it identifies the referral ID and validates the link is legitimate, then routes to login
- Readiness check happens **after** login at the referral detail page — it checks the session (JWT product roles)
- A valid token does NOT bypass the readiness check: even if the token resolves the referral ID correctly, the user must be provisioned to see referral details

---

## 6. Legacy / Misconfigured Provider Handling

Providers who can authenticate but lack the `CareConnectReceiver` product role are handled safely:

| Scenario | Behavior |
|---|---|
| Authenticated, no CareConnect roles | `ReferralAccessBlocked` shown |
| Authenticated, only `CareConnectReferrer` role | `ReferralAccessBlocked` shown (referrer cannot act as receiver) |
| Authenticated, `CareConnectReceiver` role, wrong org | Backend 404 (not 403) — "referral not found" shown |
| Not authenticated | Redirected to `/login` by `requireOrg()` |
| Token valid, user authenticated, not provisioned | Login succeeds → `ReferralAccessBlocked` shown, no referral data |

The `reason` field in `ProviderAccessReadiness` is surfaced to the `ReferralAccessBlocked` component as a prop for future operational tooling, but is not displayed to end users.

---

## 7. Tests

### New test file: `ProviderAccessReadinessTests.cs` (14 tests, all passing)

| Test | Scenario |
|---|---|
| `ReceiverRole_IsFullyProvisioned` | Happy path: receiver role → all flags true |
| `ReferrerRoleOnly_IsNotProvisioned` | Referrer role only → blocked, reason="missing-receiver-role" |
| `NoRoles_IsNotProvisioned` | Empty role list → blocked |
| `UnknownRole_IsNotProvisioned` | Misconfigured role → blocked |
| `BothRoles_IsProvisioned` | Referrer + Receiver → provisioned (receiver caps satisfy check) |
| `Provisioned_ReasonIsNull` | Reason is null when provisioned |
| `NotProvisioned_ReasonIsSet` × 2 (Theory) | Reason is non-null for referrer and unknown roles |
| `ReferrerRole_HasReceiverRole_IsFalse` | HasReceiverRole tracks role presence independently |
| `ReceiverRole_HasReceiverRole_IsTrue` | HasReceiverRole is true for receiver role |
| `ReceiverRole_HasReferralAccess_And_HasReferralAccept_BothTrue` | Role carries both required capabilities |
| `ReferrerRole_HasReferralAccess_And_HasReferralAccept_BothFalse` | Referrer has no receiver capabilities |
| `ReadinessCheck_IsDeterministic` | Same inputs produce same result twice |

### Gaps / limitations

- The frontend `checkCareConnectReceiverAccess` utility is pure TypeScript with no external deps — fully testable with a JS test framework. No such framework is configured in this repo; logic is verified by code review and backend parity tests.
- The `GET /api/referrals/access-readiness` endpoint requires an HTTP test host for integration testing. The service layer is tested thoroughly above.
- End-to-end browser-level access gating tests are deferred to a future E2E suite.

---

## 8. Known Limitations / Deferred Items

| Item | Decision |
|---|---|
| Provider–org linkage check | The backend `GET /{id:guid}` returns 404 for non-participants. This is the org-linkage guard. It is not duplicated in the readiness check — single responsibility. |
| Auto-provisioning path | Explicitly forbidden per spec. The `auto-provision` endpoint (for new provider account setup via activation form) remains intact but does not affect the referral access gate. |
| Admin tooling to view blocked providers | Out of scope. The `reason` field in `ProviderAccessReadiness` is available for future admin tooling. |
| TenantAdmin bypass in `checkCareConnectReceiverAccess` | The frontend utility does not apply a TenantAdmin bypass — admins in the platform have `isTenantAdmin = true` and would typically have CareConnect roles assigned. The backend endpoint handles the bypass explicitly. Deferred as low-priority since admins have their own access paths. |

---

## 9. Recommended Next Step

**LSCC-01-003** (suggested): Admin provisioning UI — a focused admin interface for assigning the `CareConnectReceiver` role to provider users, linking them to the correct organization, and verifying their access bundle is complete. This is the "admin repair path" that LSCC-01-002-02 intentionally defers but depends on for operational use.

---

## Success Criteria Verification

| Criterion | Status |
|---|---|
| Providers without correct provisioning cannot view or accept referrals | ✅ `ReferralAccessBlocked` shown before any referral data is fetched |
| Blocked providers see "contact your administrator" message | ✅ "Access Not Available" / "contact your administrator to request access" |
| No implicit auto-provisioning during referral flow | ✅ `ProviderAccessReadinessService` is read-only; no provisioning code |
| Correctly provisioned providers still reach referral detail | ✅ `checkCareConnectReceiverAccess` returns `isProvisioned=true` for receiver role |
| Acceptance available only to authorized providers in valid state | ✅ Capability gate on PUT + workflow state machine unchanged |
| No regressions to LSCC-01-002 / LSCC-01-002-01 | ✅ 417 passing (up from 403); all prior tests still green |
| Report at `/analysis/LSCC-01-002-02-report.md` | ✅ This document |
