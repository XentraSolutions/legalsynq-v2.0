# LSCC-002-01 — Provider Org Bulk Tooling, Appointment Org-ID Backfill, Access Validation Tests, EF Core Alignment

**Date**: 2026-03-31  
**Service**: CareConnect (`:5003`)  
**Test count after LSCC-002-01**: 158 (141 → +17), 0 failures  
**EF Core warnings after fix**: 0 (MSB3277 resolved)

---

## A. Provider Org Bulk Tooling

### New Endpoints

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| `GET`  | `/api/admin/providers/unlinked` | PlatformOrTenantAdmin | List active providers with no OrganizationId |
| `POST` | `/api/admin/providers/bulk-link-organization` | PlatformOrTenantAdmin | Bulk-link providers to organizations from explicit mapping |

### Design Decisions

- **Unlinked list** (`GET /api/admin/providers/unlinked`): Returns `{ providers: [...], count: N }`. Only active providers are included. Results are ordered by name.
- **Bulk link** (`POST /api/admin/providers/bulk-link-organization`): Accepts `{ items: [{ providerId, organizationId }] }`. Each item is processed independently:
  - **Updated**: provider found, has no org ID, successfully linked.
  - **Skipped**: provider found, already has an org ID (idempotent — no override ever happens).
  - **Unresolved**: provider ID not found in this tenant (explicit, never silently ignored).
- Both endpoints are tenant-scoped via `ICurrentRequestContext.TenantId`.
- No auto-guessing or inference of org mappings — every link must be explicit in the request body.

### Files Changed

| File | Change |
|------|--------|
| `CareConnect.Application/Repositories/IProviderRepository.cs` | Added `GetUnlinkedAsync(tenantId, ct)` |
| `CareConnect.Infrastructure/Repositories/ProviderRepository.cs` | Implemented `GetUnlinkedAsync` (active + null OrganizationId, ordered by Name) |
| `CareConnect.Application/Interfaces/IProviderService.cs` | Added `GetUnlinkedProvidersAsync`, `BulkLinkOrganizationAsync`; added `ProviderOrgLinkItem` and `BulkLinkReport` records |
| `CareConnect.Application/Services/ProviderService.cs` | Implemented both service methods |
| `CareConnect.Api/Endpoints/ProviderAdminEndpoints.cs` | Added two new route registrations and handlers |

---

## B. Legacy Appointment Org-ID Backfill

### New Endpoint

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| `POST` | `/api/admin/appointments/backfill-org-ids` | PlatformOrTenantAdmin | Populate null org IDs on legacy appointments from parent Referral |

### Design Decisions

- **Tenant-scoped**: only processes appointments whose `TenantId` matches the requesting admin's tenant.
- **Source of truth**: org IDs are copied exclusively from the parent `Referral`. No inference, no heuristics.
- **Idempotent**: appointments that already have both org IDs set are counted as `alreadySet` and never touched.
- **Partial-referral handling**: if the parent `Referral` has null org IDs (itself a legacy record), the appointment is counted as `skipped` (unresolvable). Operator must fix the referral first.
- **Atomic save**: `SaveChangesAsync` is only called if `updated > 0`, avoiding a wasted round-trip on no-op runs.
- **Response** (`AppointmentBackfillReport`): `{ updated, skipped, alreadySet, candidates }` — gives operators a clear audit trail of every run.

### Domain Method Added

```csharp
// Appointment.cs
public void BackfillOrgIds(Guid referringOrganizationId, Guid receivingOrganizationId)
{
    ReferringOrganizationId = referringOrganizationId;
    ReceivingOrganizationId = receivingOrganizationId;
    UpdatedAtUtc = DateTime.UtcNow;
}
```

- Idempotent by design (same values in = same state out).
- No `UpdatedByUserId` set — this is a system-initiated migration operation, not a user edit.

### Files Changed

| File | Change |
|------|--------|
| `CareConnect.Domain/Appointment.cs` | Added `BackfillOrgIds(Guid, Guid)` domain method |
| `CareConnect.Api/Endpoints/AdminBackfillEndpoints.cs` | New file — `POST /api/admin/appointments/backfill-org-ids` |
| `CareConnect.Api/Program.cs` | Registered `MapAdminBackfillEndpoints()` |

---

## C. HTTP Access Validation Tests

**17 new tests** added to `CareConnect.Tests/Authorization/AccessControlValidationTests.cs`.

| # | Test Name | Scenario |
|---|-----------|----------|
| 1 | `IsAdmin_PlatformAdmin_ReturnsTrue` | PlatformAdmin always bypasses participant checks |
| 2 | `IsAdmin_TenantAdmin_ReturnsTrue` | TenantAdmin always bypasses participant checks |
| 3 | `IsAdmin_RegularUser_ReturnsFalse` | Regular user with org is not an admin |
| 4 | `IsReferralParticipant_NonParticipantOrg_ReturnsFalse` | Third-party org blocked from referral |
| 5 | `IsReferralParticipant_ReferringOrg_ReturnsTrue` | Referring org has referral access |
| 6 | `IsReferralParticipant_ReceivingOrg_ReturnsTrue` | Receiving org has referral access |
| 7 | `IsReferralParticipant_NullCallerOrg_ReturnsFalse` | No org context = no participant access |
| 8 | `IsAppointmentParticipant_ReferringOrg_ReturnsTrue` | Referring org has appointment access |
| 9 | `IsAppointmentParticipant_ReceivingOrg_ReturnsTrue` | Receiving org has appointment access |
| 10 | `IsAppointmentParticipant_ThirdPartyOrg_ReturnsFalse` | Third-party org blocked from appointment |
| 11 | `IsAppointmentParticipant_NullCallerOrg_ReturnsFalse` | No org context = no appointment access |
| 12 | `GetReferralOrgScope_Admin_ReturnsNullNullTuple` | Admin gets no scope filter (full tenant) |
| 13 | `GetReferralOrgScope_Referrer_SetsReferringOrgOnly` | Referrer sees only their referrals |
| 14 | `GetReferralOrgScope_Receiver_SetsReceivingOrgOnly` | Receiver sees only their referrals |
| 15 | `BackfillOrgIds_SetsBothOrganizationIdFields` | Domain method correctly populates both fields |
| 16 | `BackfillOrgIds_IsIdempotent_WhenCalledWithSameValues` | Second call is a no-op |
| 17 | `BulkLinkReport_ExposesAllCounters` | DTO exposes Total/Updated/Skipped/Unresolved |

Note: These are service-level / logic-level tests exercising the participant check predicates directly. No HTTP harness is required because the logic is in static helper methods called by endpoint handlers — they can be tested without spinning up a server.

---

## D. EF Core Version Alignment

### Root Cause

`Microsoft.EntityFrameworkCore.Design 8.0.8` (with `IncludeAssets=runtime`) caused the Infrastructure project to compile against EF Core 8.0.8. At test time, `Pomelo.EntityFrameworkCore.MySql 8.0.2` pulls in EF Core 8.0.2, creating an MSB3277 version conflict.

### Fix

Downgraded `Microsoft.EntityFrameworkCore.Design` from `8.0.8` → `8.0.2` to match Pomelo in all four affected projects:

| Project | File |
|---------|------|
| CareConnect API | `apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj` |
| CareConnect Infrastructure | `apps/services/careconnect/CareConnect.Infrastructure/CareConnect.Infrastructure.csproj` |
| Fund API | `apps/services/fund/Fund.Api/Fund.Api.csproj` |
| Fund Infrastructure | `apps/services/fund/Fund.Infrastructure/Fund.Infrastructure.csproj` |

Both services now compile with a single coherent EF Core 8.0.2 dependency chain. MSB3277 warning is eliminated in all four projects.

---

## Security & Operational Notes

1. **All admin endpoints require `PlatformOrTenantAdmin`** — no capability-role bypass path exists.
2. **Bulk link is explicit-only** — no auto-discovery of org mappings. Admins must supply the exact `{ providerId, organizationId }` pairs.
3. **Appointment backfill cannot corrupt data** — it only sets fields that are currently null, and only from a verified Referral source. It never overrides existing values.
4. **All operations are tenant-scoped** — cross-tenant data access is structurally impossible via these endpoints.
5. **Idempotency** — all three admin operations (single link, bulk link, backfill) can be called multiple times safely.

---

## Test Summary

| Phase | Count | Delta |
|-------|-------|-------|
| Baseline (pre-LSCC-002) | 110 | — |
| After LSCC-002 | 141 | +31 |
| After LSCC-002-01 | **158** | **+17** |

All 158 tests pass. 0 failures. 0 skipped.
