# LSCC-010 — Auto Provisioning (Provider Instant Activation)

**Status:** Complete  
**Date:** 2026-03-31  
**Scope:** CareConnect + Identity services + frontend activation form

---

## Objective

Eliminate the manual admin step from the happy-path provider activation flow.
When a provider submits the LSCC-008 form, the `auto-provision` endpoint fires:

1. Validates the HMAC token and loads the referral
2. Creates or resolves an Identity Organization (idempotent by deterministic name)
3. Links the CareConnect provider record to that organization
4. Marks the LSCC-009 activation request approved
5. Returns a `loginUrl` so the frontend can redirect to the portal immediately

Any failure at any step triggers a clean fallback: upsert the LSCC-009 activation request
for admin review and return `fallbackRequired = true` to the frontend.

---

## Architecture

### Identity Service — `POST /api/admin/organizations`

**File:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` (`AdminEndpointsLscc010`)

- Accepts `{ tenantId, providerCcId, providerName }`
- Idempotency key: deterministic name `"{providerName} [cc:{providerCcId:D}]"`, looked up by `(TenantId, OrgType=PROVIDER, Name)`
- Returns `{ id, name, isNew }` — `isNew = false` on repeat calls (safe to retry)
- Also exposes `GET /api/admin/organizations/{id}` for verification

### CareConnect — `IIdentityOrganizationService`

**Files:**
- `CareConnect.Application/Interfaces/IIdentityOrganizationService.cs`
- `CareConnect.Infrastructure/Services/HttpIdentityOrganizationService.cs`

HTTP client wrapping the Identity endpoint. All failures (network, 4xx, 5xx, timeout, parse error)
return `null` — the caller interprets null as "fall back to LSCC-009". Disabled gracefully when
`IdentityService:BaseUrl` is not configured.

### CareConnect — `AutoProvisionService`

**File:** `CareConnect.Application/Services/AutoProvisionService.cs`

Orchestration service implementing `IAutoProvisionService`. Flow:

```
ValidateViewToken()
  → null / mismatch → Fallback
  → ok
    GetByIdGlobalAsync(referralId)
      → null → Fallback
    TokenVersion check
      → mismatch → Fallback
    GetByIdCrossAsync(providerId)
      → null → Fallback + upsert attempted
    Provider.OrganizationId.HasValue?
      → yes → AlreadyActive(loginUrl)   [no identity call]
    EnsureProviderOrganizationAsync()
      → null → Fallback + upsert
    LinkOrganizationAsync()
      → throws → Fallback + upsert
    UpsertAsync() + ApproveAsync()   [approval is best-effort, non-fatal]
    → Provisioned(orgId, loginUrl)
```

### CareConnect — `POST /api/referrals/{id}/auto-provision`

**File:** `CareConnect.Api/Endpoints/ReferralEndpoints.cs`

- Public endpoint (no `.RequireAuthorization`) — token-gated like `track-funnel`
- Body: `{ token, requesterName?, requesterEmail? }`
- Returns `AutoProvisionResult` serialized as JSON to the frontend

### Frontend — `activation-form.tsx`

**File:** `apps/web/src/app/referrals/activate/activation-form.tsx`

Calls `POST /api/careconnect/api/referrals/{id}/auto-provision`.
Renders three distinct post-submit states:

| State | Outcome | UI |
|---|---|---|
| `provisioned` | `success=true, alreadyActive=false` | Green card + "Log In to Your Account" CTA |
| `alreadyActive` | `success=true, alreadyActive=true` | Blue card + "Log In" CTA |
| `fallback` | `success=false, fallbackRequired=true` | Amber card — "Activation Request Received, team will follow up" |

---

## DTOs

**File:** `CareConnect.Application/DTOs/AutoProvisionDtos.cs`

```csharp
AutoProvisionResult {
  Success, OrganizationId?, AlreadyActive, FallbackRequired, FailureReason?, LoginUrl?
}
// Factories: Provisioned(orgId, loginUrl), AlreadyActiveResult(loginUrl), Fallback(reason)

AutoProvisionRequest { Token, RequesterName?, RequesterEmail? }
```

---

## Audit Events

All events are fire-and-forget via `IAuditEventClient`. Failures are logged but never block the flow.

| Event code | Trigger |
|---|---|
| `careconnect.autoprovision.autoprovisionstarted` | Entering happy path (provider found, not yet linked) |
| `careconnect.autoprovision.autoprovisionSucceeded` | Provider linked + request approved |
| `careconnect.autoprovision.autoprovisionFailed` | Any fallback path |
| `careconnect.autoprovision.autoprovisionSucceeded` | AlreadyActive idempotent path |

---

## Dependency Injection

**File:** `CareConnect.Infrastructure/DependencyInjection.cs`

```csharp
services.AddScoped<IIdentityOrganizationService, HttpIdentityOrganizationService>();
services.AddScoped<IAutoProvisionService, AutoProvisionService>();
```

---

## Tests

**File:** `CareConnect.Tests/Application/AutoProvisionTests.cs` — 10 tests, all pass

| Test | Asserts |
|---|---|
| `InvalidToken_ReturnsFallback` | Null token result → `FallbackRequired=true` |
| `TokenReferralIdMismatch_ReturnsFallback` | Token points to different referral |
| `ReferralNotFound_ReturnsFallback` | Referral not in DB |
| `RevokedToken_ReturnsFallback` | TokenVersion mismatch (stale token) |
| `ProviderNotFound_ReturnsFallback` | Provider record missing |
| `ProviderAlreadyLinked_ReturnsAlreadyActive` | OrganizationId already set; identity service NOT called |
| `IdentityOrgFails_ReturnsFallback_AndUpserts` | Identity returns null → upsert verified |
| `ProviderLinkThrows_ReturnsFallback` | LinkOrganizationAsync throws → fallback |
| `HappyPath_ReturnsProvisioned_WithLoginUrl` | Full flow, orgId returned, login URL present |
| `HappyPath_LoginUrl_ContainsReferralId` | Login URL encodes referral ID correctly |

**Full suite:** 341 pass, 5 pre-existing ProviderAvailability failures (unrelated to this work)

---

## Failure Safety

- **Token validation failure** → Fallback (no side effects)
- **Identity service down/timeout** → Fallback; LSCC-009 queue ensures no activation is lost
- **Provider link failure** → Fallback; org was created but link not written — retry via admin queue
- **Activation request approval failure** → Non-fatal; provider is already linked; logged as warning
- **`IdentityService:BaseUrl` not configured** → All auto-provision calls return null immediately → Fallback

---

## Login URL Format

```
{AppBaseUrl}/login?returnTo=%2Fcareconnect%2Freferrals%2F{referralId}&reason=activation-complete
```

`AppBaseUrl` is read from `configuration["AppBaseUrl"]` (defaults to `http://localhost:3000`).

---

## Files Created / Modified

### New
| File | Description |
|---|---|
| `Identity.Api/Endpoints/AdminEndpoints.cs` (`AdminEndpointsLscc010`) | Identity org creation + GET endpoint |
| `CareConnect.Application/Interfaces/IIdentityOrganizationService.cs` | Cross-service interface |
| `CareConnect.Application/Interfaces/IAutoProvisionService.cs` | Orchestration interface |
| `CareConnect.Application/DTOs/AutoProvisionDtos.cs` | Result + request DTOs |
| `CareConnect.Application/Services/AutoProvisionService.cs` | Full orchestration service |
| `CareConnect.Infrastructure/Services/HttpIdentityOrganizationService.cs` | HTTP client for Identity |
| `CareConnect.Tests/Application/AutoProvisionTests.cs` | 10 unit tests |
| `analysis/careconnect/LSCC-010-report.md` | This report |

### Modified
| File | Change |
|---|---|
| `CareConnect.Api/Endpoints/ReferralEndpoints.cs` | Added `POST /{id}/auto-provision` endpoint |
| `CareConnect.Infrastructure/DependencyInjection.cs` | DI registrations for new services |
| `apps/web/src/app/referrals/activate/activation-form.tsx` | Calls auto-provision; 3-state UI |
| `CareConnect.Tests/Application/ProviderActivationFunnelTests.cs` | Fixed URL assertion bug (checked encoded URL for plain path) |
