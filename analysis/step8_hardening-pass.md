# Step 8 — Hardening Pass

**Status:** COMPLETE  
**Date:** 2026-03-30  
**Phase:** Phase H (hardening pass on Phase G foundations)

---

## Objective

Harden the LegalSynq platform after Phase G completion:
1. Auto-resolve `OrganizationTypeId` FK from OrgType string in `Organization.Create()`
2. Derive `org_type` JWT claim from `OrgTypeMapper` (ID-first, string fallback)
3. Derive OrgType for `UserResponse` from `OrgTypeMapper` (ID-first, string fallback)
4. Add OrgType consistency startup diagnostic to Identity `Program.cs`
5. Add CareConnect provider/facility linkage health diagnostic to CareConnect `Program.cs`
6. Add `GET /api/admin/platform-readiness` endpoint to Identity `AdminEndpoints.cs`
7. Update `RoleAssignmentsCoverage` TypeScript type to Phase G shape
8. Update `mapLegacyCoverageReport` mapper to Phase G `roleAssignments` shape
9. Add `mapPlatformReadiness` mapper + `platformReadiness.get()` API client method
10. Add `PlatformReadinessSummary` and related sub-types to `control-center.ts`
11. Update `legacy-coverage-card.tsx` to render Phase G `RoleAssignmentsCoverage` fields

---

## Files Changed

### Identity backend

| File | Change |
|------|--------|
| `Identity.Domain/Organization.cs` | Auto-resolve `OrganizationTypeId` via `OrgTypeMapper.TryResolve(orgType)` when null |
| `Identity.Infrastructure/Services/JwtTokenService.cs` | Derive `org_type` claim from `OrgTypeMapper.TryResolveCode(org.OrganizationTypeId) ?? org.OrgType` |
| `Identity.Application/Services/AuthService.cs` | Derive `orgTypeForResponse` from `OrgTypeMapper` (ID-first, string fallback) |
| `Identity.Api/Program.cs` | Added check 3: OrgType consistency diagnostic (orgs with missing TypeId, code mismatch) |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Registered `GET /api/admin/platform-readiness`; added `GetPlatformReadiness` handler |

### CareConnect backend

| File | Change |
|------|--------|
| `CareConnect.Application/Services/ProviderService.cs` | Added `LogInformation` when provider created without OrganizationId |
| `CareConnect.Application/Services/FacilityService.cs` | Added `LogInformation` when facility created without OrganizationId |
| `CareConnect.Application/Services/ReferralService.cs` | Added `ILogger<ReferralService>` + `LogWarning` when both org IDs supplied but relationship resolution returned null |
| `CareConnect.Api/Program.cs` | Added Phase H startup diagnostic for provider/facility Identity linkage health |

### Control Center (TypeScript)

| File | Change |
|------|--------|
| `src/types/control-center.ts` | `RoleAssignmentsCoverage` updated to Phase G shape; added `PhaseGCompletion`, `OrgTypeCoverage`, `ProductRoleEligibilityCoverage`, `OrgRelationshipCoverage`, `PlatformReadinessSummary` |
| `src/lib/api-mappers.ts` | `mapLegacyCoverageReport` roleAssignments updated to Phase G shape; added `mapPlatformReadiness` |
| `src/lib/api-client.ts` | Added `platformReadiness: 'cc:platform-readiness'` to `CACHE_TAGS` |
| `src/lib/control-center-api.ts` | Added `mapPlatformReadiness` import, `PlatformReadinessSummary` type import, `platformReadiness.get()` method |
| `src/components/platform/legacy-coverage-card.tsx` | Replaced dual-write stats with Phase G SRA-only stats |

---

## Platform Readiness Endpoint

`GET /identity/api/admin/platform-readiness` returns:

```json
{
  "generatedAtUtc": "...",
  "phaseGCompletion": {
    "userRolesRetired": true,
    "soleRoleSourceIsSra": true,
    "totalActiveScopedAssignments": 0,
    "globalScopedAssignments": 0,
    "usersWithScopedRole": 0
  },
  "orgTypeCoverage": {
    "totalActiveOrgs": 0,
    "orgsWithOrganizationTypeId": 0,
    "orgsWithMissingTypeId": 0,
    "orgsWithCodeMismatch": 0,
    "consistent": true,
    "coveragePct": 100.0
  },
  "productRoleEligibility": {
    "totalActiveProductRoles": 0,
    "withOrgTypeRule": 0,
    "unrestricted": 0,
    "coveragePct": 100.0
  },
  "orgRelationships": {
    "total": 0,
    "active": 0
  }
}
```

---

## Build Status

| Service | Errors | Warnings |
|---------|--------|----------|
| Identity.Api | 0 | 0 |
| CareConnect.Api | 0 | 1 (pre-existing CS0168 in ExceptionHandlingMiddleware) |
| control-center (tsc) | 0 | — |

---

## TODO markers (Phase H → Phase I)

All changes include `// TODO [Phase H — remove OrgType string]` comments on the OrgType string fallback paths. Once a backfill migration populates `OrganizationTypeId` for all orgs and the `OrgType` string column is dropped, these fallback paths can be removed.
