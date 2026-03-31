# CareConnect Foundation Rebuild — Step 1 Analysis

**Date:** 2026-03-31  
**Scope:** Capability-based authorization, status normalization, org-participant scoping, provider availability endpoint, xUnit test suite.

---

## 1. Authorization Model

### Before
All CareConnect mutation endpoints used `Policies.PlatformOrTenantAdmin` (role-based). Any authenticated user with `TenantAdmin` or `PlatformAdmin` could call any write endpoint — no product-role differentiation.

### After
Each endpoint performs a two-level authorization check:

1. **Bypass layer** (`CareConnectAuthHelper`): `PlatformAdmin` and `TenantAdmin` always pass.
2. **Capability layer** (`AuthorizationService` + `CareConnectCapabilityService`): evaluates the user's product roles against a static role→capability map.

#### Role → Capability Mapping

| Product Role              | Capabilities Granted |
|---------------------------|----------------------|
| `CARECONNECT_REFERRER`    | `referral:create`, `referral:read:own`, `referral:cancel`, `provider:search`, `provider:map`, `appointment:create`, `appointment:read:own`, `dashboard:read` |
| `CARECONNECT_RECEIVER`    | `referral:read:addressed`, `referral:accept`, `referral:decline`, `appointment:create`, `appointment:update`, `appointment:manage`, `appointment:read:own`, `schedule:manage`, `provider:search`, `provider:map`, `dashboard:read` |
| `TenantAdmin` / `PlatformAdmin` | All capabilities (bypass) |

#### New Capability Codes Added

| Code | Constant |
|------|----------|
| `referral:update_status` | `CapabilityCodes.ReferralUpdateStatus` |
| `provider:manage` | `CapabilityCodes.ProviderManage` |
| `appointment:manage` | `CapabilityCodes.AppointmentManage` |
| `schedule:manage` | `CapabilityCodes.ScheduleManage` |
| `dashboard:read` | `CapabilityCodes.DashboardRead` |

#### Endpoint Authorization Matrix

| Endpoint | Before | After |
|----------|--------|-------|
| `GET /api/referrals` | AuthenticatedUser | AuthenticatedUser (org-scoped) |
| `GET /api/referrals/{id}` | AuthenticatedUser | AuthenticatedUser |
| `GET /api/referrals/{id}/history` | AuthenticatedUser | AuthenticatedUser |
| `POST /api/referrals` | **PlatformOrTenantAdmin** | **`referral:create`** |
| `PUT /api/referrals/{id}` | **PlatformOrTenantAdmin** | **status-driven**: accept→`referral:accept`, decline→`referral:decline`, cancel→`referral:cancel`, other→`referral:update_status` |
| `GET /api/providers` | AuthenticatedUser | **`provider:search`** |
| `GET /api/providers/map` | AuthenticatedUser | **`provider:map`** |
| `GET /api/providers/{id}` | AuthenticatedUser | **`provider:search`** |
| `POST /api/providers` | **PlatformOrTenantAdmin** | **`provider:manage`** |
| `PUT /api/providers/{id}` | **PlatformOrTenantAdmin** | **`provider:manage`** |
| `GET /api/providers/{id}/availability` | *new* | **`provider:search`** |
| `POST /api/appointments` | **PlatformOrTenantAdmin** | **`appointment:create`** |
| `GET /api/appointments` | AuthenticatedUser | AuthenticatedUser |
| `GET /api/appointments/{id}` | AuthenticatedUser | AuthenticatedUser |
| `PUT /api/appointments/{id}` | **PlatformOrTenantAdmin** | **`appointment:update`** |
| `POST /api/appointments/{id}/cancel` | **PlatformOrTenantAdmin** | **`appointment:manage`** |
| `POST /api/appointments/{id}/reschedule` | **PlatformOrTenantAdmin** | **`appointment:manage`** |
| `GET /api/appointments/{id}/history` | AuthenticatedUser | AuthenticatedUser |
| `GET /api/providers/{id}/availability-templates` | AuthenticatedUser | **`schedule:manage`** |
| `POST /api/providers/{id}/availability-templates` | **PlatformOrTenantAdmin** | **`schedule:manage`** |
| `PUT /api/availability-templates/{id}` | **PlatformOrTenantAdmin** | **`schedule:manage`** |
| `POST /api/providers/{id}/slots/generate` | **PlatformOrTenantAdmin** | **`schedule:manage`** |
| `GET /api/slots` | AuthenticatedUser | **`appointment:create`** |

---

## 2. Status Normalization

### Referral Statuses

| Legacy | Canonical | Migration Action |
|--------|-----------|-----------------|
| `Received` | `Accepted` | `UPDATE Referrals SET Status='Accepted' WHERE Status='Received'` |
| `Contacted` | `Accepted` | `UPDATE Referrals SET Status='Accepted' WHERE Status='Contacted'` |
| `New` | `New` | No change |
| `Scheduled` | `Scheduled` | No change |
| `Completed` | `Completed` | No change |
| `Cancelled` | `Cancelled` | No change |
| *(new)* | `Declined` | New canonical status |

Workflow transitions updated:
- `New` → `Accepted`, `Declined`, `Cancelled`
- `Accepted` → `Scheduled`, `Declined`, `Cancelled`
- `Scheduled` → `Completed`, `Cancelled`
- `Completed`, `Declined`, `Cancelled` = terminal

Legacy compat: `Referral.ValidStatuses.Legacy.Normalize(status)` maps `Received`/`Contacted` → `Accepted`.

### Appointment Statuses

| Legacy | Canonical | Migration Action |
|--------|-----------|-----------------|
| `Scheduled` | `Pending` | `UPDATE Appointments SET Status='Pending' WHERE Status='Scheduled'` |
| `Confirmed` | `Confirmed` | No change |
| `Completed` | `Completed` | No change |
| `Cancelled` | `Cancelled` | No change |
| `NoShow` | `NoShow` | No change |
| *(new)* | `Rescheduled` | New canonical status |

`AppointmentStatus.Scheduled` retained as a backward-compatibility constant.

---

## 3. Org-Participant Scoping

`GetReferralsQuery` now has `ReferringOrgId` and `ReceivingOrgId` filters.  
`ReferralRepository.SearchAsync` applies EF WHERE clauses for these when set.

`ReferralEndpoints.GET /api/referrals` derives the org filter from context:
- **Referrer-only** users → `ReferringOrgId = ctx.OrgId`
- **Receiver-only** users → `ReceivingOrgId = ctx.OrgId`
- **Admin / PlatformAdmin** → no org filter (sees all)

---

## 4. Provider Availability Endpoint

**New:** `GET /api/providers/{providerId}/availability?from=...&to=...`

Optional query params:
- `serviceOfferingId` — filter slots by service offering
- `facilityId` — filter slots by facility

Validation:
- `from` must be before `to`
- Range must not exceed 90 days

Response (`ProviderAvailabilityResponse`):
```json
{
  "providerId": "...",
  "providerName": "Dr. Jane Smith",
  "from": "...",
  "to": "...",
  "facilityId": "...",
  "facilityName": "...",
  "serviceOfferingId": "...",
  "serviceOfferingName": "...",
  "slots": [
    {
      "id": "...",
      "startAtUtc": "...",
      "endAtUtc": "...",
      "availableCount": 3,
      "facilityId": "...",
      "facilityName": "...",
      "serviceOfferingId": "...",
      "serviceOfferingName": "..."
    }
  ]
}
```

Implementation uses `IAppointmentSlotRepository.GetOpenByProviderInRangeAsync` (updated to include `Facility` and `ServiceOffering` navigations). `IAppointmentSlotRepository` is now injected into `ProviderService`.

---

## 5. Database Migration

**Migration:** `20260331200000_NormalizeStatusValues`

Applies idempotent SQL updates for referral and appointment status normalization.  
Also normalizes `ReferralStatusHistories` and `AppointmentStatusHistories` tables.

---

## 6. Test Suite

**Project:** `CareConnect.Tests` (xUnit, .NET 8)

| Test Class | Tests | Coverage |
|------------|-------|----------|
| `CareConnectCapabilityServiceTests` | 12 | Role→capability mapping, empty roles, multi-role union, unknown role |
| `ReferralWorkflowRulesTests` | 21 | Canonical transitions, legacy transitions, terminal states, capability mapping, ValidStatuses.All, Legacy.Normalize |
| `AppointmentWorkflowRulesTests` | 21 | Canonical transitions, reschedulable states, terminal states, ValidateStatus |

**Total: 94 tests passing.**

---

## 7. Files Changed

### New Files
- `shared/building-blocks/BuildingBlocks/Authorization/CapabilityCodes.cs` — 5 new constants
- `apps/services/careconnect/CareConnect.Infrastructure/Services/CareConnectCapabilityService.cs`
- `apps/services/careconnect/CareConnect.Application/Authorization/CareConnectAuthHelper.cs`
- `apps/services/careconnect/CareConnect.Application/DTOs/ProviderAvailabilityDTOs.cs`
- `apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/20260330110001_AlignCareConnectToPlatformIdentity.Designer.cs`
- `apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/20260331200000_NormalizeStatusValues.cs`
- `apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/20260331200000_NormalizeStatusValues.Designer.cs`
- `apps/services/careconnect/CareConnect.Tests/` (full project)

### Modified Files
- `apps/services/careconnect/CareConnect.Domain/Referral.cs` — status model
- `apps/services/careconnect/CareConnect.Domain/ReferralWorkflowRules.cs` — transitions + `RequiredCapabilityFor`
- `apps/services/careconnect/CareConnect.Domain/AppointmentStatus.cs` — canonical + legacy
- `apps/services/careconnect/CareConnect.Domain/AppointmentWorkflowRules.cs` — transitions + Rescheduled
- `apps/services/careconnect/CareConnect.Application/DTOs/GetReferralsQuery.cs` — org filters
- `apps/services/careconnect/CareConnect.Application/Interfaces/IProviderService.cs` — `GetAvailabilityAsync`
- `apps/services/careconnect/CareConnect.Application/Services/ProviderService.cs` — `GetAvailabilityAsync` impl
- `apps/services/careconnect/CareConnect.Infrastructure/DependencyInjection.cs` — capability service registration
- `apps/services/careconnect/CareConnect.Infrastructure/Repositories/ReferralRepository.cs` — org scoping
- `apps/services/careconnect/CareConnect.Infrastructure/Repositories/AppointmentSlotRepository.cs` — Include navigations
- `apps/services/careconnect/CareConnect.Api/Endpoints/ReferralEndpoints.cs` — capability auth
- `apps/services/careconnect/CareConnect.Api/Endpoints/AppointmentEndpoints.cs` — capability auth
- `apps/services/careconnect/CareConnect.Api/Endpoints/ProviderEndpoints.cs` — capability auth + availability endpoint
- `apps/services/careconnect/CareConnect.Api/Endpoints/AvailabilityTemplateEndpoints.cs` — capability auth
- `apps/services/careconnect/CareConnect.Api/Endpoints/SlotEndpoints.cs` — capability auth
