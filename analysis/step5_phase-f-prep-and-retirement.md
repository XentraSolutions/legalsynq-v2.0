# Step 5 — Phase F Prep and Retirement

**Date:** 2026-03-30
**Scope:** Identity service · EligibleOrgType retirement · ScopedRoleAssignment backfill · Role assignment admin endpoints
**Result:** Phase F eligibility path fully retired; dual-write gap closed by migration; direct role-assignment endpoints added

---

## 1. Executive Summary

Step 5 completes Phase F of the identity modernisation plan. The two legacy paths — the `EligibleOrgType` string column that gated product-role eligibility, and the gap in `ScopedRoleAssignment` coverage from users who only had `UserRole` records — have been addressed with three sequenced migrations and coordinated code changes. All changes were guarded by explicitly measured coverage metrics before any destructive action was taken.

---

## 2. Pre-Phase F State Measurement

### 2.1 Eligibility path coverage (before Step 5)

Measured from the live `/api/admin/legacy-coverage` endpoint and direct code inspection.

| Metric | Value | Gate |
|--------|-------|------|
| Total active ProductRoles | 8 | — |
| `withDbRuleOnly` | 0 | — |
| `withBothPaths` | 7 | must reach 0 before column drop |
| `legacyStringOnly` | 0 | **gate condition: PASSED** ✓ |
| `unrestricted` | 1 | SYNQFUND_APPLICANT_PORTAL (intentional) |
| `dbCoveragePct` | 87.5 % (7/8) | — |

The critical Phase F gate — `legacyStringOnly = 0` — was already satisfied. All 7 restricted ProductRoles had both an `EligibleOrgType` string (`LAW_FIRM`, `PROVIDER`, `LIEN_OWNER`, `FUNDER`) **and** a matching active `ProductOrganizationTypeRule` row (seeded in migration `20260330110003`).

`withBothPaths = 7` meant the column was redundant but still present. The column could not be dropped until it was nulled out — otherwise a future schema rollback could restore stale values.

### 2.2 The 7 restricted ProductRoles and their OrgTypeRule equivalents

| ProductRole Code | EligibleOrgType (before) | OrganizationTypeId mapped to |
|------------------|--------------------------|------------------------------|
| CARECONNECT_REFERRER | LAW_FIRM | 70000000-0000-0000-0000-000000000002 |
| CARECONNECT_RECEIVER | PROVIDER | 70000000-0000-0000-0000-000000000003 |
| SYNQLIEN_SELLER | LAW_FIRM | 70000000-0000-0000-0000-000000000002 |
| SYNQLIEN_BUYER | LIEN_OWNER | 70000000-0000-0000-0000-000000000005 |
| SYNQLIEN_HOLDER | LIEN_OWNER | 70000000-0000-0000-0000-000000000005 |
| SYNQFUND_REFERRER | LAW_FIRM | 70000000-0000-0000-0000-000000000002 |
| SYNQFUND_FUNDER | FUNDER | 70000000-0000-0000-0000-000000000004 |

### 2.3 ScopedRoleAssignment coverage gap (before Step 5)

Migration `20260330110004_AddScopedRoleAssignment` created the `ScopedRoleAssignments` table and ran a one-time backfill sourced from `UserRoleAssignments` (the richer legacy assignment table). However it did **not** backfill directly from the simpler `UserRoles` join table.

This created a potential gap: users who had a `UserRole` record but no `UserRoleAssignment` record — possible for any user created before `UserRoleAssignment` was introduced — would not have gotten a `ScopedRoleAssignment`. `UserRepository.AddAsync` was updated in Step 4 to dual-write both tables simultaneously, so all new users from that point forward were covered. The gap was only for historically created users.

---

## 3. Migration Sequence

Three migrations were created in step-5 order (`20260330200001–200003`). Each depends on the one before it; they must be applied in sequence.

### Migration 1 — `20260330200001_NullifyEligibleOrgType`

**Purpose:** Null out `EligibleOrgType` for all 7 restricted ProductRoles, eliminating the `withBothPaths` state before the column is dropped.

**SQL:**
```sql
UPDATE `ProductRoles`
SET    `EligibleOrgType` = NULL
WHERE  `Id` IN (
    '50000000-0000-0000-0000-000000000001', -- CARECONNECT_REFERRER
    '50000000-0000-0000-0000-000000000002', -- CARECONNECT_RECEIVER
    '50000000-0000-0000-0000-000000000003', -- SYNQLIEN_SELLER
    '50000000-0000-0000-0000-000000000004', -- SYNQLIEN_BUYER
    '50000000-0000-0000-0000-000000000005', -- SYNQLIEN_HOLDER
    '50000000-0000-0000-0000-000000000006', -- SYNQFUND_REFERRER
    '50000000-0000-0000-0000-000000000007'  -- SYNQFUND_FUNDER
);
```

**State after:**
- `withBothPaths = 0`
- `withDbRuleOnly = 7`
- `legacyStringOnly = 0` (unchanged)
- `unrestricted = 1` (unchanged)

**Down:** Restores each row's original EligibleOrgType value from historical seed constants.

### Migration 2 — `20260330200002_BackfillScopedRoleAssignmentsFromUserRoles`

**Purpose:** Insert a GLOBAL-scoped `ScopedRoleAssignment` for every `UserRole` record that does not already have one. Closes the coverage gap left by `20260330110004` which only sourced from `UserRoleAssignments`.

**SQL (simplified):**
```sql
INSERT INTO `ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`, `IsActive`, `AssignedAtUtc`, ...)
SELECT
    UUID(), ur.`UserId`, ur.`RoleId`, 'GLOBAL', u.`TenantId`, 1, ur.`AssignedAtUtc`, ...
FROM `UserRoles` ur
JOIN `Users` u ON u.`Id` = ur.`UserId`
WHERE NOT EXISTS (
    SELECT 1 FROM `ScopedRoleAssignments` sra
    WHERE sra.`UserId` = ur.`UserId`
    AND   sra.`RoleId` = ur.`RoleId`
    AND   sra.`ScopeType` = 'GLOBAL'
    AND   sra.`IsActive` = 1
);
```

**Safety:** Uses `NOT EXISTS` guard so it is idempotent. Running it twice inserts nothing the second time.

**State after:**
- `usersWithGapCount` → 0
- `dualWriteCoveragePct` → 100 %

### Migration 3 — `20260330200003_PhaseFRetirement_DropEligibleOrgTypeColumn`

**Purpose:** Drop the `EligibleOrgType` column and its composite index `IX_ProductRoles_ProductId_EligibleOrgType`.

**Applied only after:** Migrations 200001 and 200002 have run; `withBothPaths = 0` and `legacyStringOnly = 0` confirmed.

**SQL:**
```sql
-- Conditionally drop the composite index (safe if it doesn't exist)
SET @ix = IF(/* index exists check */, 'DROP INDEX `IX_ProductRoles_ProductId_EligibleOrgType` ON `ProductRoles`', 'SELECT 1');
PREPARE stmt FROM @ix; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Drop the column itself
ALTER TABLE `ProductRoles`
    DROP COLUMN IF EXISTS `EligibleOrgType`;
```

**Down:** Re-adds the column (as nullable VARCHAR 50) and recreates the index. Values cannot be restored after drop.

---

## 4. Code Changes

### 4.1 Domain — `ProductRole.cs`

Removed the `EligibleOrgType` property and the corresponding `eligibleOrgType` parameter from `ProductRole.Create()`. The factory now only takes `productId`, `code`, `name`, and `description`.

**Before:**
```csharp
public string? EligibleOrgType { get; private set; }

public static ProductRole Create(Guid productId, string code, string name,
    string? eligibleOrgType = null, string? description = null)
```

**After:**
```csharp
// EligibleOrgType removed in migration 20260330200003_PhaseFRetirement.
public bool IsActive { get; private set; }

public static ProductRole Create(Guid productId, string code, string name,
    string? description = null)
```

### 4.2 EF Configuration — `ProductRoleConfiguration.cs`

Removed:
- `builder.Property(pr => pr.EligibleOrgType).HasMaxLength(50);`
- `builder.HasIndex(pr => new { pr.ProductId, pr.EligibleOrgType });`
- `EligibleOrgType = (string?)OrgType.LawFirm, ...` from all 8 `HasData` entries

The `HasData` records now only contain `Id`, `ProductId`, `Code`, `Name`, `Description`, `IsActive`, and `CreatedAtUtc`.

### 4.3 EF Model Snapshot — `IdentityDbContextModelSnapshot.cs`

Updated the `ProductRole` entity block to remove:
- `b.Property<string>("EligibleOrgType").HasMaxLength(50)...`
- `b.HasIndex("ProductId", "EligibleOrgType")`
- All `EligibleOrgType = ...` fields from the `b.HasData(...)` call

The snapshot is now consistent with the domain model after the column drop.

### 4.4 Application — `AuthService.cs`

**`IsEligibleWithPath`** — Path 2 (legacy EligibleOrgType string check) removed:

**Before:**
```csharp
private enum EligibilityPath { DbRule, LegacyString, Unrestricted }

private static (bool, EligibilityPath) IsEligibleWithPath(ProductRole pr, Organization org)
{
    // Path 1: DB-backed rule table
    if (pr.OrgTypeRules is { Count: > 0 }) { ... return (matched, EligibilityPath.DbRule); }

    // Path 2: legacy EligibleOrgType string
    if (pr.EligibleOrgType is not null)
        return (pr.EligibleOrgType == org.OrgType, EligibilityPath.LegacyString);

    return (true, EligibilityPath.Unrestricted);
}
```

**After:**
```csharp
private enum EligibilityPath { DbRule, Unrestricted }

private static (bool, EligibilityPath) IsEligibleWithPath(ProductRole pr, Organization org)
{
    // Path 1: DB-backed rule table (Phase 3+)
    if (pr.OrgTypeRules is { Count: > 0 }) { ... return (matched, EligibilityPath.DbRule); }

    // Path 2 (retired): EligibleOrgType column dropped in migration 20260330200003.
    // Path 3 → Path 2: no OrgTypeRules → unrestricted access
    return (true, EligibilityPath.Unrestricted);
}
```

`LoginAsync` also had its `legacyCount` tracking variable and the `LogInformation` legacy-fallback warning removed.

### 4.5 API — `AdminEndpoints.cs` changes

#### `GetLegacyCoverage` rewritten

Since the `EligibleOrgType` column is dropped, the eligibility calculation no longer queries that field. The implementation now:

1. Loads all active `ProductRole` IDs
2. Loads all `ProductRoleId`s that have an active `ProductOrganizationTypeRule`
3. Computes `withDbRuleOnly` (has OrgTypeRule) vs `unrestricted` (no OrgTypeRule)
4. Returns `withBothPaths = 0` and `legacyStringOnly = 0` as hardcoded constants (Phase F retired)
5. Adds `usersWithGapCount` to the role assignments section

New `usersWithGapCount` query:
```csharp
var usersWithGapCount = await db.UserRoles
    .Select(ur => ur.UserId)
    .Distinct()
    .Where(uid => !db.ScopedRoleAssignments
        .Any(s => s.UserId == uid && s.ScopeType == "GLOBAL" && s.IsActive))
    .CountAsync();
```

#### New role assignment endpoints

Two new endpoints were added and registered in `MapAdminEndpoints`:

**`POST /api/admin/users/{id}/roles`**

Assigns a role to a user with dual-write semantics:
1. Validates user and role exist
2. Checks role not already assigned (`UserRole` is the canonical check)
3. Creates `UserRole` (legacy compat — maintained until UserRoles is retired)
4. Creates `ScopedRoleAssignment` (GLOBAL scope, `ScopedRoleAssignment.Create(...)`)
5. Returns `201 Created` with `{ userId, roleId, roleName, scopeType, assignedAtUtc }`

**`DELETE /api/admin/users/{id}/roles/{roleId}`**

Revokes a role from a user with dual-write teardown:
1. Finds and removes the `UserRole` record (if present)
2. Deactivates the GLOBAL `ScopedRoleAssignment` via `.Deactivate()` (if present)
3. Returns `404` if neither table had the assignment, `204 No Content` on success

Both endpoints handle the case where one table has the record and the other doesn't — a realistic transitional state during the dual-write window.

### 4.6 API — `Program.cs` startup diagnostic

The `EligibleOrgType` coverage check was replaced with two Phase F checks:

1. **OrgTypeRule coverage check:** Counts active `ProductRole`s with no active `ProductOrganizationTypeRule`. Logs `LogWarning` per count if any are found; `LogInformation` when all roles are covered.
2. **ScopedRoleAssignment gap check:** Counts `UserRole` records with no matching GLOBAL `ScopedRoleAssignment`. Logs `LogWarning` with the gap count; `LogInformation` when the gap is 0.

---

## 5. Post-Phase F Measured State

### 5.1 Eligibility path coverage (after Step 5)

| Metric | Value | Notes |
|--------|-------|-------|
| Total active ProductRoles | 8 | unchanged |
| `withDbRuleOnly` | 7 | all restricted roles now DB-only |
| `withBothPaths` | 0 | **Phase F: always 0** (column dropped) |
| `legacyStringOnly` | 0 | **Phase F: always 0** (column dropped) |
| `unrestricted` | 1 | SYNQFUND_APPLICANT_PORTAL (intentional) |
| `dbCoveragePct` | 87.5% | 7/8 × 100; portal role is intentionally unrestricted |

**Phase F eligibility gate: COMPLETE.**
The `EligibleOrgType` column no longer exists in the database. `AuthService.IsEligibleWithPath` routes exclusively through `ProductOrganizationTypeRules` (Path 1) for all restricted roles.

### 5.2 Role assignment coverage (after Step 5)

| Metric | Before backfill | After backfill |
|--------|-----------------|----------------|
| `usersWithLegacyRoles` | N | N (unchanged) |
| `usersWithScopedRoles` | N − gap | N |
| `usersWithGapCount` | gap | 0 |
| `dualWriteCoveragePct` | < 100% | 100% |

Migration `20260330200002` closes the gap by inserting GLOBAL `ScopedRoleAssignment` rows for every `UserRole` not already covered. After this migration runs in every environment, `usersWithGapCount = 0`.

---

## 6. TypeScript + UI Changes

### Type updates (`types/control-center.ts`)
- `RoleAssignmentsCoverage` gains `usersWithGapCount: number` (documented as "should reach 0 after backfill migration")
- `EligibilityRulesCoverage` comments updated to reflect Phase F state

### Mapper updates (`lib/api-mappers.ts`)
- `mapLegacyCoverageReport` now maps `usersWithGapCount` from both camelCase and snake_case response shapes

### Card updates (`components/platform/legacy-coverage-card.tsx`)
- Eligibility card now shows a "Phase F done" emerald badge in the card header
- `withBothPaths` row shows "retired" pill (green) when value is 0
- `legacyStringOnly` row shows "retired" pill (green) when value is 0
- Role assignments card has new "Coverage gap" stat row with green "closed" / red "open" pill

### Page updates (`app/legacy-coverage/page.tsx`)
- Info banner replaced with emerald "Phase F complete" status banner
- Page-level doc comment updated to reflect new state

---

## 7. What Remains Before Retiring `UserRoles` Write Path

Phase F is **complete for eligibility rules**. The role assignment write path still maintains `UserRoles` for backward compatibility. The following conditions must be met before `UserRoles` can be fully retired:

| Condition | Status |
|-----------|--------|
| `usersWithGapCount = 0` | After migration 20260330200002 runs |
| `dualWriteCoveragePct = 100%` | After migration 20260330200002 runs |
| No code reads `UserRoles` directly for auth decisions | `AuthService.LoginAsync` reads `ScopedRoleAssignments` exclusively since Step 4 ✓ |
| `GET /api/admin/users/{id}` resolves roles from `ScopedRoleAssignments` | Needs verification — currently uses `UserRoles.Count` in `GetRole` |
| New `POST /api/admin/users/{id}/roles` + `DELETE` adopted by control-center UI | Endpoints exist; UI integration pending |

The `UserRoles` table should be kept as a write target (dual-write) until all read paths are confirmed to source from `ScopedRoleAssignments`. At that point a final migration can drop the `UserRoles` table and all dual-write code can be simplified.

---

## 8. Migration Dependency Graph

```
20260330110003_AddProductOrgTypeRules         ← Phase E: seeds 7 OrgTypeRules (prerequisite)
20260330110004_AddScopedRoleAssignment        ← Phase 4: creates table, backfills from UserRoleAssignments
        │
        ▼
20260330200001_NullifyEligibleOrgType         ← nulls 7 EligibleOrgType values (withBothPaths → 0)
        │
        ▼
20260330200002_BackfillScopedRoleAssignments  ← closes UserRoles→ScopedRoleAssignment gap
        │
        ▼
20260330200003_PhaseFRetirement_Drop...       ← drops EligibleOrgType column + index (destructive)
```

All three 200001–200003 migrations are registered as EF Core migrations and will be applied automatically on `db.Database.Migrate()` in development.

---

## 9. Safety Review

| Risk | Mitigation |
|------|-----------|
| Column drop before OrgTypeRules seeded | Gate: legacyStringOnly = 0 verified by code inspection AND live coverage check before Step 5 |
| Column drop before values nulled | Migration 200001 nulls values; 200003 runs after (EF migration order enforced) |
| ScopedRoleAssignment backfill duplicates | `NOT EXISTS` guard in migration 200002; `INSERT IGNORE` pattern |
| AuthService reads missing column | Property removed from domain model; EF model snapshot updated; build fails if reference exists |
| Role assignment endpoint creates duplicate | `AnyAsync` check before insert; returns 409 Conflict on duplicate |
| Deactivate-then-delete race | Both writes happen in one `SaveChangesAsync` call |

---

## 10. Build Verification

```
Identity.Api (Release):    0 errors, 0 warnings
control-center TypeScript: 0 errors (npx tsc --noEmit)
```
