# LSCC-01-005-01 — PlatformAdmin Cross-Tenant Access Corrections
**Date:** 2026-04-02  
**Service:** CareConnect (port 5003)  
**Status:** COMPLETE — both defects resolved and E2E validated

---

## Background

During the E2E validation pass for LSCC-01-005 (full report: `analysis/careconnect/CC-E2E-VALIDATION-REPORT.md`),
31 endpoints were tested and two open defects were discovered affecting PlatformAdmin cross-tenant access:

| ID | Endpoint | Symptom |
|----|----------|---------|
| DEF-001 | `POST /api/admin/activations/{id}/approve` | 404 when provider TenantId ≠ activation request TenantId |
| DEF-002 | `GET /api/referrals/{id}` and sub-resources | 404 when PlatformAdmin accesses referrals owned by another tenant |

---

## DEF-001 — Cross-tenant activation approval 404

### Root Cause

`ActivationRequestService.ApproveAsync` delegated the provider org-link to
`IProviderService.LinkOrganizationAsync(request.TenantId, request.ProviderId, organizationId)`.

`LinkOrganizationAsync` used `IProviderRepository.GetByIdAsync(tenantId, providerId)` — a
tenant-scoped query that filters by `WHERE TenantId = @tenantId AND Id = @providerId`.

The specific cross-tenant case: a HARTWELL law firm submitted a referral to Dr. Elena Ramirez
(provider seeded under `TenantId = MERIDIAN` in the CareConnect DB). The activation request
carried `TenantId = HARTWELL`. The scoped lookup returned null → NotFoundException → 404.

### Fix

**Files changed:**

| File | Change |
|------|--------|
| `CareConnect.Application/Interfaces/IProviderService.cs` | Added `LinkOrganizationGlobalAsync(Guid providerId, Guid organizationId, CancellationToken)` |
| `CareConnect.Application/Services/ProviderService.cs` | Implemented using existing `IProviderRepository.GetByIdCrossAsync` |
| `CareConnect.Application/Services/ActivationRequestService.cs` | `ApproveAsync` now calls `LinkOrganizationGlobalAsync` instead of `LinkOrganizationAsync` |

**Design decision:** The activation approval path is exclusively admin-executed (PlatformAdmin only,
enforced at the endpoint). No `isPlatformAdmin` branching is needed — the global provider lookup
is always correct for this flow. Using the scoped lookup here was the original defect.

**New method:**
```csharp
// IProviderService
Task<ProviderResponse> LinkOrganizationGlobalAsync(
    Guid providerId,
    Guid organizationId,
    CancellationToken ct = default);

// ProviderService implementation
public async Task<ProviderResponse> LinkOrganizationGlobalAsync(
    Guid providerId, Guid organizationId, CancellationToken ct = default)
{
    var provider = await _providers.GetByIdCrossAsync(providerId, ct)
        ?? throw new NotFoundException($"Provider '{providerId}' was not found.");
    provider.LinkOrganization(organizationId);
    await _providers.UpdateAsync(provider, ct);
    var loaded = await _providers.GetByIdCrossAsync(providerId, ct);
    return ToResponse(loaded!);
}
```

### E2E Validation

```
POST /api/admin/activations/a79e7921-7d8b-4100-ba25-b9aa806fa5bc/approve
Authorization: PlatformAdmin (admin@legalsynq.com)
Body: { "organizationId": "42000000-0000-0000-0000-000000000001" }

HTTP 200
{
  "wasAlreadyApproved": false,
  "providerAlreadyLinked": false,
  "activationRequestId": "a79e7921-7d8b-4100-ba25-b9aa806fa5bc",
  "status": "Approved",
  "linkedOrganizationId": "42000000-0000-0000-0000-000000000001"
}
```

Result: **PASS** ✓

---

## DEF-002 — PlatformAdmin 404 on per-record referral endpoints

### Root Cause

Five `IReferralService` methods used tenant-scoped record lookup as the first operation:

```csharp
var referral = await _referrals.GetByIdAsync(tenantId, referralId, ct)
    ?? throw new NotFoundException(...);
```

`tenantId` came from the PlatformAdmin's JWT claim (`tenant_id = LEGALSYNQ`,
`20000000-0000-0000-0000-000000000001`). Referrals owned by HARTWELL or MERIDIAN tenants
returned null under this filter → 404.

Affected methods:
- `GetByIdAsync` — referral detail
- `GetHistoryAsync` — status history
- `ResendEmailAsync` — resend provider notification email
- `GetNotificationsAsync` — email delivery history
- `GetAuditTimelineAsync` — combined status + notification timeline

### Fix

**Strategy:** Add `bool isPlatformAdmin = false` to each affected interface method and service
implementation. When `true`, the initial lookup uses the already-existing
`IReferralRepository.GetByIdGlobalAsync(id)` (no tenant filter). After the global load,
`effectiveTenantId = referral.TenantId` is used for all sub-queries (notifications, history),
keeping downstream queries precisely scoped to the record's actual owner tenant.

No new repository methods were required — `GetByIdGlobalAsync` already existed on
`IReferralRepository`.

**Files changed:**

| File | Change |
|------|--------|
| `CareConnect.Application/Interfaces/IReferralService.cs` | Added `bool isPlatformAdmin = false` to 5 method signatures |
| `CareConnect.Application/Services/ReferralService.cs` | Added global lookup branching + `effectiveTenantId` to 5 methods |
| `CareConnect.Api/Endpoints/ReferralEndpoints.cs` | 5 endpoint handlers now pass `isPlatformAdmin: ctx.IsPlatformAdmin` |

**Pattern applied (example — GetByIdAsync):**
```csharp
// Before
var referral = await _referrals.GetByIdAsync(tenantId, id, ct)
    ?? throw new NotFoundException($"Referral '{id}' was not found.");
var latestNotif = await _notificationRepo.GetLatestByReferralAsync(tenantId, id, ct: ct);

// After
var referral = isPlatformAdmin
    ? await _referrals.GetByIdGlobalAsync(id, ct)
    : await _referrals.GetByIdAsync(tenantId, id, ct);
if (referral is null)
    throw new NotFoundException($"Referral '{id}' was not found.");
var effectiveTenantId = referral.TenantId;
var latestNotif = await _notificationRepo.GetLatestByReferralAsync(effectiveTenantId, id, ct: ct);
```

**Safety note:** `isPlatformAdmin` defaults to `false`. It is only set to `true` by endpoint
handlers that have already confirmed `ctx.IsPlatformAdmin`. No silent tenant-widening occurs
for standard user paths.

### E2E Validation

All tests used PlatformAdmin JWT (`admin@legalsynq.com / LEGALSYNQ`) accessing HARTWELL-owned
referral `8443e26b-3a80-49b6-a280-71a714c48495`.

| Endpoint | Before | After |
|----------|--------|-------|
| `GET /api/referrals/{id}` | 404 | **200** ✓ |
| `GET /api/referrals/{id}/history` | 404 | **200** ✓ |
| `GET /api/referrals/{id}/notifications` | 404 | **200** ✓ |
| `GET /api/referrals/{id}/audit` | 404 | **200** ✓ |
| `POST /api/referrals/{id}/resend-email` | 404 | **200** ✓ |

Audit record for `resend-email` confirmed `TenantId = 20000000-0000-0000-0000-000000000002`
(HARTWELL), not `20000000-0000-0000-0000-000000000001` (LEGALSYNQ), proving `effectiveTenantId`
is correctly propagated to sub-queries.

---

## Pre-existing issues (not in scope, not changed)

- 5 `ProviderAvailabilityServiceTests` unit test failures (pre-existing, unrelated to this work)
- Documents service CORS issue (separate bounded context)

---

## Build verification

```
dotnet build CareConnect.Api/CareConnect.Api.csproj
Build succeeded. 0 Warning(s). 0 Error(s).
```

---

## Commit

`81cdaea` — Fix issues with cross-tenant activation and platform admin referral access
