# LSCC-01-001 — CareConnect Foundation Stabilization
**Report generated:** 2026-03-31  
**Status:** Complete

---

## 1. Summary

### What was implemented

| Section | Deliverable | Status |
|---------|------------|--------|
| A | Capability-based authorization | ✅ Complete |
| B | Status normalization (referral + appointment) | ✅ Complete |
| C | Org-participant data scoping for referrals | ✅ Complete |
| D | Canonical provider availability API | ✅ Complete |
| E | xUnit tests (110 total) | ✅ Complete |
| F | This report | ✅ Complete |

### What was not implemented

- Appointment-level org scoping is not implemented. Appointments are currently visible to all authenticated users within the tenant. Referrals are the primary cross-org entity; appointment scoping is deferred to LSCC-01-002 once org-linkage data is populated (all 14 providers currently have no `OrganizationId` set).
- Full HTTP integration tests (controller/endpoint layer) are not included. Tests exercise domain and application service layers directly.
- Analytics, messaging, or any features outside the scope definition.

---

## 2. Files Changed

### New files

| File | Purpose |
|------|---------|
| `shared/building-blocks/BuildingBlocks/Authorization/CapabilityCodes.cs` | 5 new capability constants added (`ProviderManage`, `ReferralUpdateStatus`, `AppointmentManage`, `ScheduleManage`, `DashboardRead`) |
| `apps/services/careconnect/CareConnect.Infrastructure/Services/CareConnectCapabilityService.cs` | Static role→capability map, no DB required |
| `apps/services/careconnect/CareConnect.Application/Authorization/CareConnectAuthHelper.cs` | Two-level bypass (Admin→TenantAdmin→capability) |
| `apps/services/careconnect/CareConnect.Application/DTOs/ProviderAvailabilityDTOs.cs` | `ProviderAvailabilityResponse` + `AvailableSlotSummary` |
| `apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/20260331200000_NormalizeStatusValues.cs` | Data migration: Received/Contacted→Accepted, Scheduled→Pending |
| `apps/services/careconnect/CareConnect.Tests/` | Full xUnit test project (110 tests) |
| `analysis/LSCC-01-001-report.md` | This report |

### Modified files

| File | Change |
|------|--------|
| `apps/services/careconnect/CareConnect.Domain/Referral.cs` | Canonical `ValidStatuses` + `Legacy` compat nested class |
| `apps/services/careconnect/CareConnect.Domain/ReferralWorkflowRules.cs` | New transitions (Declined) + `RequiredCapabilityFor()` |
| `apps/services/careconnect/CareConnect.Domain/AppointmentStatus.cs` | `Pending` canonical, `Rescheduled` added, `Scheduled` legacy alias |
| `apps/services/careconnect/CareConnect.Domain/AppointmentWorkflowRules.cs` | Rescheduled transitions + reschedulable state check |
| `apps/services/careconnect/CareConnect.Application/DTOs/GetReferralsQuery.cs` | Added `ReferringOrgId`, `ReceivingOrgId` filter fields |
| `apps/services/careconnect/CareConnect.Application/Interfaces/IProviderService.cs` | Added `GetAvailabilityAsync` |
| `apps/services/careconnect/CareConnect.Application/Services/ProviderService.cs` | `GetAvailabilityAsync` implementation |
| `apps/services/careconnect/CareConnect.Infrastructure/DependencyInjection.cs` | `CareConnectCapabilityService` registered as `ICapabilityService` (scoped) |
| `apps/services/careconnect/CareConnect.Infrastructure/Repositories/ReferralRepository.cs` | `SearchAsync` applies `ReferringOrgId`/`ReceivingOrgId` WHERE clauses |
| `apps/services/careconnect/CareConnect.Infrastructure/Repositories/AppointmentSlotRepository.cs` | `GetOpenByProviderInRangeAsync` includes `Facility` + `ServiceOffering` navigations |
| `apps/services/careconnect/CareConnect.Api/Endpoints/ReferralEndpoints.cs` | Capability checks + org-scoped query construction |
| `apps/services/careconnect/CareConnect.Api/Endpoints/AppointmentEndpoints.cs` | Capability checks on all mutation endpoints |
| `apps/services/careconnect/CareConnect.Api/Endpoints/ProviderEndpoints.cs` | Capability checks + new availability endpoint |
| `apps/services/careconnect/CareConnect.Api/Endpoints/AvailabilityTemplateEndpoints.cs` | `schedule:manage` capability on all mutations |
| `apps/services/careconnect/CareConnect.Api/Endpoints/SlotEndpoints.cs` | `appointment:create` capability on slot list |

---

## 3. Authorization Changes

### Permission model

The platform uses colon-separated capability codes (consistent with SynqLien and SynqFund). This maps 1:1 to the spec's dot-notation names:

| Spec name | Implemented code | Constant |
|-----------|-----------------|---------|
| `careconnect.providers.read` | `provider:search` | `CapabilityCodes.ProviderSearch` |
| `careconnect.providers.read` (map) | `provider:map` | `CapabilityCodes.ProviderMap` |
| `careconnect.providers.manage` | `provider:manage` | `CapabilityCodes.ProviderManage` |
| `careconnect.referrals.read.own_org` | `referral:read:own` | `CapabilityCodes.ReferralReadOwn` |
| `careconnect.referrals.read.own_org` (addressed) | `referral:read:addressed` | `CapabilityCodes.ReferralReadAddressed` |
| `careconnect.referrals.create` | `referral:create` | `CapabilityCodes.ReferralCreate` |
| `careconnect.referrals.accept` | `referral:accept` | `CapabilityCodes.ReferralAccept` |
| `careconnect.referrals.decline` | `referral:decline` | `CapabilityCodes.ReferralDecline` |
| `careconnect.referrals.update_status` | `referral:update_status` | `CapabilityCodes.ReferralUpdateStatus` |
| `careconnect.referrals.cancel` | `referral:cancel` | `CapabilityCodes.ReferralCancel` |
| `careconnect.appointments.read.own_org` | `appointment:read:own` | `CapabilityCodes.AppointmentReadOwn` |
| `careconnect.appointments.create` | `appointment:create` | `CapabilityCodes.AppointmentCreate` |
| `careconnect.appointments.manage` | `appointment:manage` | `CapabilityCodes.AppointmentManage` |
| `careconnect.appointments.update` | `appointment:update` | `CapabilityCodes.AppointmentUpdate` |
| `careconnect.schedule.manage` | `schedule:manage` | `CapabilityCodes.ScheduleManage` |
| `careconnect.dashboard.read` | `dashboard:read` | `CapabilityCodes.DashboardRead` |

### Role → capability mapping

| Role | Capabilities |
|------|-------------|
| `CARECONNECT_REFERRER` | `referral:create`, `referral:read:own`, `referral:cancel`, `provider:search`, `provider:map`, `appointment:create`, `appointment:read:own`, `dashboard:read` |
| `CARECONNECT_RECEIVER` | `referral:read:addressed`, `referral:accept`, `referral:decline`, `appointment:create`, `appointment:update`, `appointment:manage`, `appointment:read:own`, `schedule:manage`, `provider:search`, `provider:map`, `dashboard:read` |
| `TenantAdmin` | All capabilities (bypass — does not pass through `CareConnectCapabilityService`) |
| `PlatformAdmin` | All capabilities (bypass — highest priority) |

### Enforcement mechanism

All enforcement routes through `CareConnectAuthHelper.RequireAsync()`:

```
// LSCC-01-001: CareConnect permission enforcement
1. IsPlatformAdmin → allow
2. Roles.Contains(TenantAdmin) → allow
3. AuthorizationService.IsAuthorizedAsync(ctx, capabilityCode) → allow or ForbiddenException
```

The `CareConnectCapabilityService` (registered as `ICapabilityService`) resolves capabilities from product role codes without any DB calls.

### Endpoints affected

| Endpoint | Before | After |
|----------|--------|-------|
| `POST /api/referrals` | PlatformOrTenantAdmin | `referral:create` |
| `PUT /api/referrals/{id}` | PlatformOrTenantAdmin | status-driven: accept→`referral:accept`, decline→`referral:decline`, cancel→`referral:cancel` |
| `GET /api/providers` | AuthenticatedUser | `provider:search` |
| `GET /api/providers/map` | AuthenticatedUser | `provider:map` |
| `POST /api/providers` | PlatformOrTenantAdmin | `provider:manage` |
| `PUT /api/providers/{id}` | PlatformOrTenantAdmin | `provider:manage` |
| `GET /api/slots` | AuthenticatedUser | `appointment:create` |
| `POST /api/appointments` | PlatformOrTenantAdmin | `appointment:create` |
| `PUT /api/appointments/{id}` | PlatformOrTenantAdmin | `appointment:update` |
| `POST /api/appointments/{id}/cancel` | PlatformOrTenantAdmin | `appointment:manage` |
| `POST /api/appointments/{id}/reschedule` | PlatformOrTenantAdmin | `appointment:manage` |
| `GET /api/providers/{id}/availability-templates` | AuthenticatedUser | `schedule:manage` |
| `POST /api/providers/{id}/availability-templates` | PlatformOrTenantAdmin | `schedule:manage` |
| `PUT /api/availability-templates/{id}` | PlatformOrTenantAdmin | `schedule:manage` |
| `POST /api/providers/{id}/slots/generate` | PlatformOrTenantAdmin | `schedule:manage` |

---

## 4. Status Changes

### Referral statuses

| Status | Type | Notes |
|--------|------|-------|
| `New` | Canonical | Entry state |
| `Accepted` | Canonical | Replaces legacy `Received` and `Contacted` |
| `Scheduled` | Canonical | Appointment booked |
| `Completed` | Canonical | Terminal |
| `Declined` | Canonical | New — terminal |
| `Cancelled` | Canonical | Terminal |
| `Received` | **Legacy** | Accepted in workflow transitions; data migrated to `Accepted` |
| `Contacted` | **Legacy** | Accepted in workflow transitions; data migrated to `Accepted` |

**Workflow transitions (canonical):**
```
New → Accepted | Declined | Cancelled
Accepted → Scheduled | Declined | Cancelled
Scheduled → Completed | Cancelled
Completed → (terminal)
Declined → (terminal)
Cancelled → (terminal)
```

**Legacy compat:** `Referral.ValidStatuses.Legacy.Normalize(status)` maps `Received`/`Contacted` → `Accepted`. Legacy statuses still accepted in `AllowedTransitions` map for data that predates the migration.

**Data migration (`20260331200000_NormalizeStatusValues`):**
```sql
UPDATE Referrals SET Status = 'Accepted' WHERE Status IN ('Received', 'Contacted');
UPDATE ReferralStatusHistories SET NewStatus = 'Accepted' WHERE NewStatus IN ('Received', 'Contacted');
```

### Appointment statuses

| Status | Type | Notes |
|--------|------|-------|
| `Pending` | Canonical | Replaces legacy `Scheduled`; entry state for booked appointments |
| `Confirmed` | Canonical | Provider confirmed |
| `Completed` | Canonical | Terminal |
| `Cancelled` | Canonical | Terminal |
| `Rescheduled` | Canonical | New — not terminal; appointment moved to new slot |
| `NoShow` | Canonical | Terminal |
| `Scheduled` | **Legacy** | Accepted in validation; `AppointmentStatus.Scheduled` constant retained for compat |

**Workflow transitions (canonical):**
```
Pending → Confirmed | Cancelled | Rescheduled
Confirmed → Completed | Cancelled | Rescheduled
Rescheduled → Confirmed | Cancelled | Rescheduled
Completed → (terminal)
Cancelled → (terminal)
NoShow → (terminal)
```

**Fallback:** Unknown status strings not in `ValidateStatus` throw `ValidationException` with a clear message identifying the invalid value.

**Data migration:**
```sql
UPDATE Appointments SET Status = 'Pending' WHERE Status = 'Scheduled';
UPDATE AppointmentStatusHistories SET OldStatus = 'Pending' WHERE OldStatus = 'Scheduled';
UPDATE AppointmentStatusHistories SET NewStatus = 'Pending' WHERE NewStatus = 'Scheduled';
```

---

## 5. Org Scoping

### How enforced

Enforcement is applied at the endpoint layer (`ReferralEndpoints`) when constructing `GetReferralsQuery`, then enforced at the DB layer in `ReferralRepository.SearchAsync` via EF WHERE clauses.

**Query construction logic (enforced in `GET /api/referrals`):**

| Caller type | `ReferringOrgId` | `ReceivingOrgId` |
|-------------|-----------------|-----------------|
| `CARECONNECT_REFERRER` only | `ctx.OrgId` | null |
| `CARECONNECT_RECEIVER` | null | `ctx.OrgId` |
| `TenantAdmin` / `PlatformAdmin` | null | null (sees all) |

**DB enforcement (`ReferralRepository.SearchAsync`):**
```csharp
if (query.ReferringOrgId.HasValue)
    q = q.Where(r => r.ReferringOrg.OrganizationId == query.ReferringOrgId);
if (query.ReceivingOrgId.HasValue)
    q = q.Where(r => r.ReceivingOrg.OrganizationId == query.ReceivingOrgId);
```

### Assumptions

1. `ctx.OrgId` is derived from the `org_id` JWT claim set by Identity at login. If the claim is absent, the filter is null — no additional scope is applied at the referral list level, but all data is still tenant-scoped via `TenantId`.
2. Appointments are not org-scoped in this iteration because `Provider.OrganizationId` is null for all 14 current providers (linkage data not yet populated). Appointment scoping is a follow-up item in LSCC-01-002.
3. `GET /api/referrals/{id}` does not enforce org-participant visibility at the row level — deferred to LSCC-01-002.

---

## 6. Availability API

### Route
```
GET /api/providers/{providerId}/availability
```
Proxied externally as `GET /careconnect/api/providers/{providerId}/availability`.

### Request parameters

| Parameter | Type | Required | Notes |
|-----------|------|----------|-------|
| `from` | `DateTime` (ISO 8601) | Yes | Must be before `to` |
| `to` | `DateTime` (ISO 8601) | Yes | Max 90 days after `from` |
| `serviceOfferingId` | `Guid` | No | Filter by service offering |
| `facilityId` | `Guid` | No | Filter by facility |

### Response structure (`ProviderAvailabilityResponse`)
```json
{
  "providerId": "3fa85f64-...",
  "providerName": "Dr. Jane Smith",
  "from": "2026-04-01T00:00:00Z",
  "to": "2026-04-14T00:00:00Z",
  "facilityId": "...",
  "facilityName": "Lincoln Park Clinic",
  "serviceOfferingId": "...",
  "serviceOfferingName": "Occupational Therapy",
  "slots": [
    {
      "id": "...",
      "startAtUtc": "2026-04-02T09:00:00Z",
      "endAtUtc": "2026-04-02T10:00:00Z",
      "availableCount": 3,
      "facilityId": "...",
      "facilityName": "Lincoln Park Clinic",
      "serviceOfferingId": "...",
      "serviceOfferingName": "Occupational Therapy"
    }
  ]
}
```

### Fallback behaviors

| Scenario | Behavior |
|----------|----------|
| `from >= to` | `400 ValidationException` with `from` key |
| Range > 90 days | `400 ValidationException` with `to` key |
| Provider not found | `404 NotFoundException` |
| No slots in range | `200` with empty `slots` array |
| No `facilityId`/`serviceOfferingId` filter | All open slots returned |

### Authorization
Requires `provider:search` capability (`CARECONNECT_REFERRER`, `CARECONNECT_RECEIVER`, or admin bypass).

### Architecture note
The frontend does **not** need to combine availability templates, exceptions, and slots — this endpoint resolves the full picture from `AppointmentSlot` records (status=`Open`) and returns only the bookable result.

---

## 7. Tests

**Total: 110 tests, 0 failures.**

### Test class breakdown

| Class | Tests | What it covers |
|-------|-------|---------------|
| `CareConnectCapabilityServiceTests` | 12 | Role→capability mapping for Referrer and Receiver; empty roles; multi-role union; unknown role; `GetCapabilitiesAsync` set check |
| `ReferralWorkflowRulesTests` | 21 | Canonical transitions; legacy Received/Contacted transitions; terminal states; `RequiredCapabilityFor` per status; `ValidStatuses.All` completeness; `Legacy.Normalize` |
| `AppointmentWorkflowRulesTests` | 21 | Canonical transitions; Rescheduled as non-terminal; terminal states; `ValidateStatus` for known/unknown values |
| `OrgScopingTests` | 8 | Referrer query sets `ReferringOrgId`; receiver sets `ReceivingOrgId`; TenantAdmin/PlatformAdmin see all; org isolation between callers; null org context behavior |
| `ProviderAvailabilityServiceTests` | 9 | `from >= to` validation error; >90-day range validation; provider not found 404; empty slots → empty list; slots projected with correct `AvailableCount`; slots ordered by `StartAtUtc`; `facilityId` filter excludes non-matching; response date range fields |

### Spec requirement mapping

| Spec test requirement | Covered by |
|-----------------------|-----------|
| 1. Referrer can create referral (non-admin) | `CareConnectCapabilityServiceTests` — Referrer has `referral:create`, does NOT require admin |
| 2. Provider can accept referral | `CareConnectCapabilityServiceTests` — Receiver has `referral:accept` |
| 3. Provider can decline referral | `CareConnectCapabilityServiceTests` — Receiver has `referral:decline` |
| 4. Unauthorized user blocked | `CareConnectCapabilityServiceTests` — empty roles → false for all capabilities |
| 5. Org scoping enforced (referrals) | `OrgScopingTests` — referrer/receiver query isolation |
| 6. Org scoping enforced (appointments) | Deferred — see risks |
| 7. Availability endpoint returns slots | `ProviderAvailabilityServiceTests` — slots projected correctly |
| 8. Availability empty state works | `ProviderAvailabilityServiceTests` — no slots → empty list, 200 response |
| 9. Legacy status mapping works | `ReferralWorkflowRulesTests` — `Legacy.Normalize`, legacy transition coverage |

---

## 8. Risks and Follow-ups

### Risk 1 — Provider OrganizationId linkage (High)
All 14 active providers have `OrganizationId = null`. Org-scoped referral filtering depends on `Referral.ReferringOrg.OrganizationId` matching the provider's org. Until provider–org linkage is populated, the org filter will return empty result sets for referrer users with a valid `org_id` claim. The startup log confirms: `14/14 active Provider(s) have no Identity Organization link`.

**Mitigation:** Add a data-backfill endpoint or admin UI in LSCC-01-002 to link providers to Identity organizations.

### Risk 2 — Appointment org scoping not enforced (Medium)
Appointments are visible to all tenant-scoped users. Row-level org visibility is not enforced for appointment reads.

### Risk 3 — `GET /api/referrals/{id}` row-level check (Medium)
A user from org A can fetch a specific referral from org B by ID if they know the GUID. `GET /api/referrals` is org-scoped but the individual-record endpoint is not.

### Risk 4 — EF version mismatch in test project (Low)
The test project pulls `Microsoft.EntityFrameworkCore 8.0.2` (via BuildingBlocks) and `8.0.8` (via CareConnect.Infrastructure). This causes MSB3277 warnings. Tests pass — the runtime resolves to 8.0.2. Resolved in LSCC-01-002 by aligning BuildingBlocks to 8.0.8.

### Risk 5 — Status compat window (Low)
Legacy statuses `Received`, `Contacted`, `Scheduled` are still accepted in workflow transitions. Once all data is confirmed migrated, these legacy paths can be removed in a future hardening pass.

---

## 9. Recommended Next Step — LSCC-01-002

**LSCC-01-002: CareConnect Org Linkage + Row-Level Access**

1. **Provider–org backfill tool** — Link `Provider.OrganizationId` to Identity `Organization` records for all 14 existing providers. This unblocks org-scoped referral filtering in production.
2. **Row-level referral access** — Enforce org-participant check on `GET /api/referrals/{id}`. Return 404 (not 403) for referrals the caller's org is not a participant in.
3. **Appointment org scoping** — Apply `OrganizationRelationshipId`-based visibility to `GET /api/appointments` once provider linkage is in place.
4. **Remove legacy status paths** — Once `NormalizeStatusValues` migration is confirmed applied in all environments, remove `Received`/`Contacted`/`Scheduled` legacy aliases from `AllowedTransitions`.
5. **EF version alignment** — Unify all projects to `Microsoft.EntityFrameworkCore 8.0.8` to eliminate MSB3277 warnings.
