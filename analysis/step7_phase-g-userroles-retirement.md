# Step 7 — Phase G: UserRoles & UserRoleAssignment Table Retirement

**Date:** 2026-03-30  
**Status:** ✅ COMPLETE (build: 0 errors, 0 warnings)

---

## Objective

Fully retire the `UserRoles` and `UserRoleAssignments` tables from the Identity service.
`ScopedRoleAssignments` (backfilled in migration `20260330200002`) becomes the sole
authoritative role store. All dual-write paths, legacy navigations, EF entity
configurations, and domain classes are removed.

---

## Files Changed

| File | Change |
|------|--------|
| `Identity.Domain/UserRole.cs` | **Deleted** — domain entity retired |
| `Identity.Domain/UserRoleAssignment.cs` | **Deleted** — domain entity retired |
| `Identity.Infrastructure/…/UserRoleConfiguration.cs` | **Deleted** — EF config retired |
| `Identity.Infrastructure/…/UserRoleAssignmentConfiguration.cs` | **Deleted** — EF config retired |
| `Identity.Domain/User.cs` | Removed `UserRoles` and `RoleAssignments` navigation collections |
| `Identity.Domain/Role.cs` | Removed `UserRoles` and `RoleAssignments` navigation collections |
| `Identity.Domain/Organization.cs` | Removed `RoleAssignments` navigation collection |
| `Identity.Infrastructure/Persistence/IdentityDbContext.cs` | Removed `UserRoles` and `UserRoleAssignments` DbSets + `OnModelCreating` registrations |
| `Identity.Infrastructure/Repositories/UserRepository.cs` | Single `ScopedRoleAssignment` write (no dual-write) |
| `Identity.Application/Services/AuthService.cs` | Removed legacy `UserRoles` fallback; sole role source is `ScopedRoleAssignments` |
| `Identity.Application/Services/UserService.cs` | `ToResponse` uses `ScopedRoleAssignments` (was `UserRoles`) |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | `AssignRole`: single SRA write; `RevokeRole`: SRA deactivate only; `GetLegacyCoverage`: returns Phase G stats, no `UserRoles` queries |
| `Identity.Api/Program.cs` | Startup diagnostic updated to Phase G (SRA counts, no UserRoles gap check) |
| `Identity.Infrastructure/Persistence/Migrations/IdentityDbContextModelSnapshot.cs` | Removed `UserRole` + `UserRoleAssignment` entity blocks, relationship blocks, and navigation entries |
| `Identity.Infrastructure/Persistence/Migrations/20260330200004_PhaseG_DropUserRolesAndUserRoleAssignments.cs` | **New** — `DROP TABLE UserRoleAssignments; DROP TABLE UserRoles;` |

---

## Migration Chain

```
20260330200001_NullifyEligibleOrgType
20260330200002_BackfillScopedRoleAssignmentsFromUserRoles
20260330200003_PhaseFRetirement_DropEligibleOrgTypeColumn
20260330200004_PhaseG_DropUserRolesAndUserRoleAssignments  ← NEW
```

Migration `200004` drops `UserRoleAssignments` first (FK dependency), then `UserRoles`.
The `Down()` method recreates both tables in reverse order for rollback safety.

---

## Authorization Flow (Post Phase G)

```
Login / GetUser
    └─ UserRepository.GetByIdWithRolesAsync()
           └─ Eager-loads ScopedRoleAssignments (GLOBAL, IsActive)
                  └─ .Select(s => s.Role.Name)  →  JWT claims / UserResponse.Roles
```

No fallback. No `UserRoles` table. Single write path in `AssignRole`. Single
deactivate path in `RevokeRole`.

---

## AdminEndpoints.GetLegacyCoverage (updated response shape)

```json
{
  "generatedAtUtc": "...",
  "eligibilityRules": {
    "totalActiveProductRoles": 12,
    "withDbRuleOnly": 12,
    "withBothPaths": 0,
    "legacyStringOnly": 0,
    "unrestricted": 0,
    "dbCoveragePct": 100.0
  },
  "roleAssignments": {
    "usersWithScopedRoles": 42,
    "totalActiveScopedAssignments": 47,
    "userRolesRetired": true,
    "dualWriteCoveragePct": 100.0
  }
}
```

---

## Build Verification

```
dotnet build Identity.Api/Identity.Api.csproj
→ Build succeeded. (0 errors)
```

---

## Phase H Candidates (future work)

- **OrgType string field** on `Organization`: still present alongside `OrganizationTypeId`.
  A future Phase H can drop the `OrgType` string column once all consumers use the FK-based
  `OrganizationTypeId` path exclusively.
- **JWT `org_type` claim**: currently sourced from `org.OrgType` string; should be
  re-derived from `OrgTypeMapper` using `OrganizationTypeId`.
- **Control Center UI**: `dualWriteCoveragePct` and `usersWithLegacyRoles` display
  components can be updated to the Phase G response shape.
