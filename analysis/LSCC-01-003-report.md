# LSCC-01-003: Admin Provider Provisioning Interface — Implementation Report

**Date:** 2026-04-02  
**Status:** Complete  
**Scope:** Backend + Frontend provisioning tooling for CareConnect RECEIVER access

---

## Problem

Provider organizations could not receive CareConnect referrals because four
preconditions were not consistently enforced during onboarding:

1. User must have an active **primary org membership** in a PROVIDER organization.
2. Tenant must have the **SYNQ_CARECONNECT TenantProduct** enabled.
3. The org must have the **SYNQ_CARECONNECT OrganizationProduct** enabled.
4. User must hold a **CARECONNECT_RECEIVER (or REFERRER) ScopedRoleAssignment**.

Additionally, on the CareConnect side, the provider record must have
`IsActive = true` and `AcceptingReferrals = true`.

There was no admin tooling to diagnose or correct these gaps atomically.

---

## What Was Built

### CareConnect Domain (`CareConnect.Domain`)

**`Provider.Activate()`**  
Added an idempotent domain method that sets `IsActive = true`,
`AcceptingReferrals = true`, and advances `UpdatedAtUtc`. No-op if already active.

### CareConnect Application (`CareConnect.Application`)

**`IProviderService.ActivateForCareConnectAsync(Guid providerId)`**  
New interface method returning `ProviderActivationResult`
(`ProviderId`, `AlreadyActive`, `IsActive`, `AcceptingReferrals`).

**`ProviderActivationResult` record**  
Added alongside the existing `BulkLinkReport` and `ProviderOrgLinkItem` records
in `IProviderService.cs`.

**`ProviderService.ActivateForCareConnectAsync`**  
Implementation: cross-tenant read via `GetByIdCrossAsync` → guards on not-found
→ checks `IsActive && AcceptingReferrals` → calls `provider.Activate()` +
`UpdateAsync` only when needed → returns result.

### CareConnect API (`CareConnect.Api`)

**`POST /api/admin/providers/{id}/activate-for-careconnect`**  
Registered in `ProviderAdminEndpoints.MapProviderAdminEndpoints`.  
Auth: `PlatformOrTenantAdmin`.  
Returns: `{ providerId, alreadyActive, isActive, acceptingReferrals }`.

### Identity API (`Identity.Api`)

**`GET /api/admin/users/{id}/careconnect-readiness`**  
Returns a diagnostic snapshot:
- `hasPrimaryOrg` — active primary org membership exists
- `primaryOrgId`, `primaryOrgType` — the linked org details
- `tenantHasCareConnect` — TenantProduct enabled for SYNQ_CARECONNECT
- `orgHasCareConnect` — OrganizationProduct enabled on primary org
- `hasCareConnectRole` — CARECONNECT_RECEIVER or REFERRER SRA exists
- `isFullyProvisioned` — all four conditions met

**`POST /api/admin/users/{id}/provision-careconnect`**  
Idempotent. Applies all missing preconditions in a single transaction:
1. Adds/enables `TenantProduct` for SYNQ_CARECONNECT.
2. Adds/enables `OrganizationProduct` on primary org.
3. Adds `CARECONNECT_RECEIVER` ScopedRoleAssignment (global scope).

Returns: `{ userId, organizationId, organizationName, tenantProductAdded, orgProductAdded, roleAdded, isFullyProvisioned }`.

Guard: returns `422 Unprocessable Entity` with code `NO_PRIMARY_ORG` if the user
has no primary org — provisioning cannot proceed without an org link.

### Frontend (`apps/web`)

**Types (`src/types/careconnect.ts`)**  
`ProviderReadinessDiagnostics`, `ProvisionCareConnectResult`, `ProviderActivationResult`.

**Server API client (`src/lib/careconnect-server-api.ts`)**  
`careConnectServerApi.adminProvisioning` — three methods:
- `getReadiness(userId)` → Identity GET
- `provision(userId)` → Identity POST
- `activateProvider(providerId)` → CareConnect POST

**Page (`/careconnect/admin/providers/provisioning`)**  
Server component. Accepts `?userId=<uuid>`. Pre-fetches readiness data and
passes it to the client panel.

**Panel (`ProviderProvisioningPanel`)**  
Client component — three-step interactive workflow:
1. User ID input → fetch readiness diagnostics with four status indicators.
2. "Provision User" button → runs Identity-side provisioning, refreshes diagnostics.
3. Provider ID input → "Activate" button → runs CC-side provider activation.

All three steps show inline success/error feedback with idempotency messaging.

---

## Tests Added

| File | Tests | Result |
|---|---|---|
| `CareConnect.Tests/Domain/ProviderActivationTests.cs` | 5 | All pass |
| `CareConnect.Tests/Application/ProviderProvisioningTests.cs` | 5 | All pass |

**Total new tests:** 10  
**Pre-existing failures (unrelated):** 5 in `ProviderAvailabilityServiceTests` — unchanged.

---

## Key IDs Used

| Item | ID |
|---|---|
| SYNQ_CARECONNECT Product | `10000000-0000-0000-0000-000000000003` |
| CARECONNECT_RECEIVER Role | `50000000-0000-0000-0000-000000000002` |
| REFERRER Role | `50000000-0000-0000-0000-000000000001` |

---

## Files Changed

```
apps/services/careconnect/
  CareConnect.Domain/Provider.cs                           + Activate()
  CareConnect.Application/Interfaces/IProviderService.cs   + ActivateForCareConnectAsync, ProviderActivationResult
  CareConnect.Application/Services/ProviderService.cs      + ActivateForCareConnectAsync impl
  CareConnect.Api/Endpoints/ProviderAdminEndpoints.cs      + activate-for-careconnect route + handler
  CareConnect.Tests/Domain/ProviderActivationTests.cs      NEW (5 tests)
  CareConnect.Tests/Application/ProviderProvisioningTests.cs NEW (5 tests)

apps/services/identity/
  Identity.Api/Endpoints/AdminEndpoints.cs                 + GetCareConnectReadiness, ProvisionForCareConnect

apps/web/src/
  types/careconnect.ts                                     + 3 new interfaces
  lib/careconnect-server-api.ts                            + adminProvisioning namespace (3 methods)
  app/(platform)/careconnect/admin/providers/provisioning/page.tsx  NEW
  components/careconnect/admin/provider-provisioning-panel.tsx       NEW
```
