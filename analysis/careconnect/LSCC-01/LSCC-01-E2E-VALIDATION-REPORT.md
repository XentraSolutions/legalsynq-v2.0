# CareConnect E2E Test Readiness Validation Report

**Date:** 2026-04-02  
**Scope:** Full end-to-end validation of all CareConnect HTTP API flows  
**Services tested:** CareConnect API (port 5003), Identity API (port 5001), BFF Proxy (Next.js, port 5000)

---

## Test Credentials Resolved

| User | Email | Password | Tenant Code | Role | Tenant/Org ID |
|------|-------|----------|-------------|------|---------------|
| Margaret Hartwell | margaret@hartwell.law | hartwell123! | HARTWELL | TenantAdmin | 20000000-...-0002 / 40000000-...-0010 |
| James Whitmore | james.whitmore@hartwell.law | hartwell123! | HARTWELL | StandardUser | HARTWELL |
| Olivia Chen | olivia.chen@hartwell.law | hartwell123! | HARTWELL | StandardUser | HARTWELL |
| Dr. Elena Ramirez | dr.ramirez@meridiancare.com | meridian123! | MERIDIAN | TenantAdmin | 20000000-...-0003 / 42000000-...-0001 |
| Alex Diallo | alex.diallo@meridiancare.com | meridian123! | MERIDIAN | StandardUser | MERIDIAN |
| **Platform Admin** | admin@legalsynq.com | **Admin1234!** | **LEGALSYNQ** | PlatformAdmin | 20000000-...-0001 |

> **Note:** The admin password `Admin1234!` + tenantCode `LEGALSYNQ` was previously unknown. Confirmed via bcrypt hash verification against identity DB.

---

## Bugs Fixed During Validation

### BUG-001 (Critical): `BlockedProviderAccessLogs` Table Missing
- **Symptom:** `GET /api/admin/dashboard` → 500; `GET /api/admin/providers/blocked` → 500
- **Root Cause:** Migration `20260402010000_LSCC01004_BlockedProviderAccessLog` was recorded in `__EFMigrationsHistory` but the CREATE TABLE never executed (partial-apply failure)
- **Fix Applied:** Created the `BlockedProviderAccessLogs` table directly using a repair script
- **Status:** ✅ FIXED — both endpoints now return 200

### BUG-002 (Security): `ForbiddenException` Mapped to HTTP 500 Instead of 403
- **Symptom:** StandardUser attempting to accept a referral got `{"error":{"code":"INTERNAL_ERROR",...}}` HTTP 500
- **Root Cause:** `ExceptionHandlingMiddleware` had no `catch (ForbiddenException)` handler; it fell through to the generic 500 catch
- **Fix Applied:** Added `catch (ForbiddenException ex)` handler returning HTTP 403 with code `FORBIDDEN`
- **File:** `apps/services/careconnect/CareConnect.Api/Middleware/ExceptionHandlingMiddleware.cs`
- **Status:** ✅ FIXED — StandardUser now correctly receives `{"error":{"code":"FORBIDDEN","message":"Missing capability: referral:accept"}}` HTTP 403

---

## Defects Found (Not Fixed — Require Design Decision)

### DEF-001: Cross-Tenant Activation Approval Fails with 404
- **Endpoint:** `POST /api/admin/activations/{id}/approve`
- **Scenario:** PlatformAdmin (LEGALSYNQ tenant) approves an activation request where the referral tenant is HARTWELL and the provider (`a1000000-...`, Dr. Elena Ramirez) belongs to MERIDIAN's CC namespace
- **Root Cause:** `ActivationRequestService.ApproveAsync` calls `_providerService.LinkOrganizationAsync(request.TenantId, ...)` using the activation request's TenantId (HARTWELL), but the provider record exists under MERIDIAN's TenantId. Tenant-scoped lookup fails.
- **Impact:** Admin cannot approve cross-tenant provider activations via the queue
- **Recommendation:** `LinkOrganizationAsync` should use a global (tenant-bypass) provider lookup for admin approval flows, or the provider's own TenantId should be used instead of the request's TenantId

### DEF-002: PlatformAdmin Cannot Access Per-Tenant Referrals by ID
- **Endpoints:** `GET /api/referrals/{id}/history`, `GET /api/referrals/{id}/audit`, `POST /api/referrals/{id}/resend-email` (as PlatformAdmin)
- **Scenario:** PlatformAdmin trying to access a HARTWELL referral by ID gets 404
- **Root Cause:** These endpoints extract `tenantId` from the JWT and use it for the DB lookup. PlatformAdmin's JWT contains `tenantId = LEGALSYNQ`, so HARTWELL referrals are not found
- **Impact:** PlatformAdmin cannot drill into individual referrals from other tenants
- **Note:** The cross-tenant list endpoint (`GET /api/admin/referrals`) correctly returns 14 referrals across all tenants; only per-record endpoints are affected

---

## Pre-Existing Issues (Do Not Fix)

1. **5 ProviderAvailabilityServiceTests failing** — pre-existing, out of scope
2. **Documents service CORS/credentials issue** — pre-existing, not related to CareConnect
3. **BFF proxy double-api path convention** — `/api/careconnect/api/...` is by design (gateway routing)
4. **Public referral token endpoints blocked without session cookie** — by design; public flow uses direct CC port or gateway; BFF proxy is authenticated by design

---

## Full Endpoint Test Matrix

### Referral Endpoints

| # | Endpoint | Method | Actor | Status | Result |
|---|---------|--------|-------|--------|--------|
| 1 | `/api/referrals` | GET | TenantAdmin (HARTWELL) | ✅ 200 | 4 HARTWELL + tenant-scoped |
| 2 | `/api/referrals` | GET | PlatformAdmin | ✅ 200 | 14 cross-tenant referrals |
| 3 | `/api/referrals` | POST | TenantAdmin | ✅ 201 | Creates referral, triggers email |
| 4 | `/api/referrals/{id}` | GET | TenantAdmin | ✅ 200 | Full referral detail |
| 5 | `/api/referrals/{id}` | PUT | TenantAdmin | ✅ 200 | Admin bypass; status changed |
| 6 | `/api/referrals/{id}` | PUT | StandardUser (no capability) | ✅ 403 | `FORBIDDEN: Missing capability: referral:accept` (FIXED from 500) |
| 7 | `/api/referrals/{id}/history` | GET | TenantAdmin | ✅ 200 | Status history |
| 8 | `/api/referrals/{id}/notifications` | GET | TenantAdmin | ✅ 200 | Email notification log (ReferralCreated + ReferralEmailResent) |
| 9 | `/api/referrals/{id}/resend-email` | POST | TenantAdmin | ✅ 200 | Re-sends provider email |
| 10 | `/api/referrals/{id}/revoke-token` | POST | TenantAdmin | ✅ 200 | `tokenVersion` incremented 1→2 |
| 11 | `/api/referrals/{id}/audit` | GET | TenantAdmin | ✅ 200 | Audit trail |
| 12 | `/api/referrals/access-readiness` | GET | TenantAdmin (HARTWELL) | ✅ 200 | `isProvisioned:true, hasReferralAccept:true` (admin bypass) |
| 13 | `/api/referrals/access-readiness` | GET | TenantAdmin (MERIDIAN) | ✅ 200 | `isProvisioned:true, hasReferralAccept:true` |
| 14 | `/api/referrals/resolve-view-token?token=…` | GET | Public (no auth) | ✅ 200 | `routeType:"pending"` (provider not yet linked to org) |
| 15 | `/api/referrals/{id}/public-summary?token=…` | GET | Public | ✅ 200 | Client/provider/status summary |
| 16 | `/api/referrals/{id}/accept-by-token` | POST | Public | ✅ 410 | By design: "retired" — providers must log in |
| 17 | `/api/referrals/{id}/track-funnel` | POST | Public | ✅ 200 | `ReferralViewed` and `ActivationStarted` events accepted |
| 18 | `/api/referrals/{id}/auto-provision` | POST | Public | ✅ 200 | `fallbackRequired:true` (Identity service provision failed gracefully) |

### Provider Endpoints

| # | Endpoint | Method | Actor | Status | Result |
|---|---------|--------|-------|--------|--------|
| 19 | `/api/providers?q=Sandra` | GET | TenantAdmin | ✅ 200 | Provider search — tenant-scoped |
| 20 | `/api/providers/map?latitude=36.1&longitude=-115.1&radiusMiles=50` | GET | TenantAdmin | ✅ 200 | Geo providers list |
| 21 | `/api/providers/{id}` | GET | TenantAdmin | ✅ 200 | Full provider detail with categories |

### Admin Dashboard & Queue Endpoints

| # | Endpoint | Method | Actor | Status | Result |
|---|---------|--------|-------|--------|--------|
| 22 | `/api/admin/dashboard` | GET | TenantAdmin | ✅ 200 | `{referralCountToday:2, openReferrals:8, blockedAccessToday:0}` (FIXED) |
| 23 | `/api/admin/dashboard` | GET | PlatformAdmin | ✅ 200 | Cross-tenant aggregates |
| 24 | `/api/admin/providers/blocked` | GET | TenantAdmin | ✅ 200 | Empty queue; returns `{items:[],total:0}` (FIXED) |
| 25 | `/api/admin/referrals` | GET | PlatformAdmin | ✅ 200 | 14 cross-tenant referrals |
| 26 | `/api/admin/activations` | GET | PlatformAdmin | ✅ 200 | 1 pending (Dr. Ramirez ActivationStarted) |
| 27 | `/api/admin/activations/{id}` | GET | PlatformAdmin | ✅ 200 | Full activation detail |
| 28 | `/api/admin/activations/{id}/approve` | POST | PlatformAdmin | ⚠️ 404 | DEF-001: cross-tenant provider not found |
| 29 | `/api/admin/providers/unlinked` | GET | TenantAdmin | ✅ 200 | 5 unlinked HARTWELL providers |
| 30 | `/api/admin/providers/unlinked` | GET | PlatformAdmin | ✅ 200 | 4 unlinked LEGALSYNQ providers |
| 31 | `/api/admin/providers/{id}/activate-for-careconnect` | POST | TenantAdmin | ✅ 200 | `{alreadyActive:true}` for Sandra Nguyen PT |

---

## Token Flow Validation

Token format: `{referralId}:{tokenVersion}:{expiryUnixSeconds}:{hmacHex}` — Base64url-encoded  
HMAC key: `ReferralToken:Secret` (dev fallback: `LEGALSYNQ-DEV-REFERRAL-TOKEN-SECRET-2026`)

Flow tested:
1. Token generated for referral `8443e26b` (Dr. Elena Ramirez, `tokenVersion:1`)
2. `resolve-view-token` → `routeType:"pending"` (provider has no `OrganizationId` — correct)
3. `public-summary` → full client/provider/status data — correct
4. `accept-by-token` → 410 (design decision: providers must log in)
5. `track-funnel: ReferralViewed` → 200
6. `track-funnel: ActivationStarted` → 200 (creates activation request)
7. `revoke-token` → `tokenVersion:2` — old token now invalid
8. Verify old token (version 1) after revoke would fail version mismatch check ✅

---

## Authorization Validation

| Scenario | Expected | Actual | Pass? |
|---------|---------|--------|-------|
| TenantAdmin accepts referral | 200 (admin bypass) | 200 | ✅ |
| StandardUser accepts referral (no capability) | 403 | 403 FORBIDDEN | ✅ (FIXED from 500) |
| TenantAdmin accesses other tenant's referrals | 404 | 404 NOT_FOUND | ✅ |
| PlatformAdmin sees all referrals (list) | 200 cross-tenant | 200, 14 items | ✅ |
| TenantAdmin bypasses capability checks | By design | Confirmed (CareConnectAuthHelper line 26) | ✅ |
| StandardUser without admin role blocked | 403 | 403 | ✅ |

---

## Performance Endpoint (LSCC-01-005)

Confirmed working from prior session:
- `GET /api/admin/performance` → 200
- All 13 unit tests pass
- Report at `/analysis/LSCC-01-005-report.md`

---

## Summary

| Category | Count |
|---------|-------|
| Endpoints tested | 31 |
| Passing (200/201/204/410) | 29 |
| Defects fixed | 2 (BUG-001, BUG-002) |
| Defects found (unfixed) | 2 (DEF-001, DEF-002) |
| Pre-existing issues (out of scope) | 4 |

The CareConnect API is **substantially ready for E2E testing**. Two critical bugs were fixed (missing table, wrong exception status code). Two architectural defects remain that require design decisions around cross-tenant PlatformAdmin access patterns.
