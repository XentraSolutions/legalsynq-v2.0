# LSCC-002 — CareConnect Org Linkage + Access Hardening

**Status:** ✅ Complete  
**Date:** 2026-03-31  
**Tests:** 141 passing (31 added, 0 failures)

---

## Spec Coverage Matrix

| # | Requirement | Implementation | Status |
|---|-------------|----------------|--------|
| 1 | Admin provider org-link backfill endpoint | `PUT /api/admin/providers/{id}/link-organization` | ✅ |
| 2 | Referral detail row-level participant check | `GET /api/referrals/{id}` — 404 for non-participants | ✅ |
| 3 | Appointment list org scoping | `GET /api/appointments` — participant-scoped via `ReferringOrganizationId`/`ReceivingOrganizationId` | ✅ |
| 4 | Appointment detail row-level participant check | `GET /api/appointments/{id}` — 404 for non-participants | ✅ |
| 5 | Appointment org ID denormalization on create | `Appointment.Create` now accepts and sets `referringOrganizationId`/`receivingOrganizationId` | ✅ |
| 6 | Shared participant-scoping helper | `CareConnectParticipantHelper` — static helper covering both Referral and Appointment | ✅ |

---

## Architecture Decisions

### Participant-access pattern
- **Golden rule:** non-participant and null-org callers are DENIED — access is never silently widened.
- Detail endpoints return **404 (not 403)** for non-participants to avoid confirming record existence (anti-enumeration).
- Admins (PlatformAdmin or TenantAdmin) bypass all participant checks.

### Appointment org ID denormalization
- `Appointment.Create` factory extended with two optional parameters: `referringOrganizationId`, `receivingOrganizationId`.
- `AppointmentService.CreateAppointmentAsync` passes these from the source `Referral` entity at booking time.
- Legacy appointments (created before LSCC-002) will have `null` org IDs — they are treated as "no org" for access checks (participants cannot access legacy records through the participant check; admins still can).
- **No migration required** — the columns already existed in the domain; only the `Create` path was wiring them.

### Search scoping
- `IAppointmentRepository.SearchAsync` extended with `Guid? referringOrgId = null, Guid? receivingOrgId = null`.
- `IAppointmentService.SearchAppointmentsAsync` extended with the same two params (default null = no filter).
- Both filters are applied independently so admins passing `null` for both get no narrowing (full tenant view).
- Mirrors the exact pattern already established for `IAppointmentRepository.SearchAsync` (referral side).

### Admin provider org-link backfill
- `PUT /api/admin/providers/{id}/link-organization` — `PlatformOrTenantAdmin` policy.
- Calls `IProviderService.LinkOrganizationAsync` → `provider.LinkOrganization(orgId)` → `IProviderRepository.UpdateAsync`.
- Idempotent: re-calling with same `organizationId` is safe (domain method just sets the nullable FK).
- Responds with the full `ProviderResponse` so callers can confirm the updated state.

---

## Files Modified

### Domain
| File | Change |
|------|--------|
| `CareConnect.Domain/Appointment.cs` | `Create` factory: added `referringOrganizationId`, `receivingOrganizationId` optional params |

### Application
| File | Change |
|------|--------|
| `CareConnect.Application/Authorization/CareConnectParticipantHelper.cs` | **New** — shared participant helper (IsAdmin, IsReferralParticipant, IsAppointmentParticipant, GetReferralOrgScope, GetAppointmentOrgScope) |
| `CareConnect.Application/Interfaces/IAppointmentService.cs` | `SearchAppointmentsAsync` — added `referringOrgId`, `receivingOrgId` params |
| `CareConnect.Application/Interfaces/IProviderService.cs` | Added `LinkOrganizationAsync` method |
| `CareConnect.Application/Repositories/IAppointmentRepository.cs` | `SearchAsync` — added `referringOrgId`, `receivingOrgId` params |
| `CareConnect.Application/DTOs/AppointmentDTOs.cs` | `AppointmentResponse` — added `ReferringOrganizationId`, `ReceivingOrganizationId` |
| `CareConnect.Application/Services/AppointmentService.cs` | Create path: denormalize org IDs from referral; Search: forward org filter params; mapper: include new DTO fields |
| `CareConnect.Application/Services/ProviderService.cs` | `LinkOrganizationAsync` implementation |

### Infrastructure
| File | Change |
|------|--------|
| `CareConnect.Infrastructure/Repositories/AppointmentRepository.cs` | `SearchAsync`: org filter clauses applied when non-null |

### API
| File | Change |
|------|--------|
| `CareConnect.Api/Endpoints/ReferralEndpoints.cs` | `GET /{id}`: participant check → 404 for non-participants |
| `CareConnect.Api/Endpoints/AppointmentEndpoints.cs` | `GET /`: org-scoped via `isReceiver` + `GetAppointmentOrgScope`; `GET /{id}`: participant check → 404 |
| `CareConnect.Api/Endpoints/ProviderAdminEndpoints.cs` | **New** — `PUT /api/admin/providers/{id}/link-organization` |
| `CareConnect.Api/Program.cs` | `app.MapProviderAdminEndpoints()` registered |

---

## New Tests (31 added)

### `CareConnect.Tests/Authorization/CareConnectParticipantHelperTests.cs` (19 tests)
- `IsAdmin` — PlatformAdmin, TenantAdmin, regular user
- `IsReferralParticipant` — referring org, receiving org, third party, null org, null org IDs
- `IsAppointmentParticipant` — same matrix as above
- `GetReferralOrgScope` — admin bypass, TenantAdmin bypass, referrer scope, receiver scope
- `GetAppointmentOrgScope` — admin bypass, referrer scope, receiver scope

### `CareConnect.Tests/Application/AppointmentOrgScopingTests.cs` (12 tests)
- Denormalization: `Create` sets referring/receiving org IDs correctly; null propagation
- Independence: two appointments from different referrals have distinct org IDs
- Scoping: referrer scope, receiver scope, admin no-filter
- Row-level: referring org visible, receiving org visible, third party not visible

---

## Authorization Matrix (post-LSCC-002)

| Endpoint | Anonymous | AuthUser | Referrer (CARECONNECT_REFERRER) | Receiver (CARECONNECT_RECEIVER) | TenantAdmin | PlatformAdmin |
|----------|-----------|----------|----------------------------------|----------------------------------|-------------|---------------|
| `GET /api/referrals` | ❌ | ✅ (scoped to org) | ✅ (ReferringOrg filter) | ✅ (ReceivingOrg filter) | ✅ (unfiltered) | ✅ (unfiltered) |
| `GET /api/referrals/{id}` | ❌ | participant only | ✅ (if participant) | ✅ (if participant) | ✅ | ✅ |
| `GET /api/appointments` | ❌ | ✅ (scoped to org) | ✅ (ReferringOrg filter) | ✅ (ReceivingOrg filter) | ✅ (unfiltered) | ✅ (unfiltered) |
| `GET /api/appointments/{id}` | ❌ | participant only | ✅ (if participant) | ✅ (if participant) | ✅ | ✅ |
| `PUT /api/admin/providers/{id}/link-organization` | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |

---

## Remaining Known Items (non-blocking)

| Item | Priority | Notes |
|------|----------|-------|
| EF version mismatch (8.0.2 vs 8.0.8) | Low | Pre-existing MSB3277 warning; non-blocking; pin BuildingBlocks to 8.0.8 to resolve |
| Legacy appointment org IDs backfill | Low | Appointments created before LSCC-002 have `null` `ReferringOrganizationId`/`ReceivingOrganizationId`; admins can still access; non-admins would get 404 on detail for those records |
| Provider org ID backfill | Operational | Use new `PUT /api/admin/providers/{id}/link-organization` endpoint after Identity org IDs are known |
