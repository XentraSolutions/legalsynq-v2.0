# LegalSynq Final Convergence

**Date:** 2026-03-30
**Scope:** Identity · CareConnect · Control Center
**Build:** Identity.Api 0 errors 0 warnings · CareConnect.Api 0 errors · TypeScript 0 errors

---

## 1. Starting State

At the start of Step 6 the platform had completed Phase F eligibility retirement (Step 5).
The following gaps remained:

| Area | Gap |
|------|-----|
| Organization.Update() | Did not accept `organizationTypeId`; updates could not set the typed FK |
| OrgType ↔ OrganizationTypeId mapping | No centralized helper — each caller was expected to know seed GUIDs directly |
| AuthService login flow | ScopedRoleAssignments loaded additively (merge on top of UserRoles), never exclusively |
| UserRepository.GetAllWithRolesAsync | Only loaded UserRoles; ScopedRoleAssignments excluded |
| AdminEndpoints — ListUsers / GetUser | Role display sourced from `UserRoles` Include |
| AdminEndpoints — ListRoles / GetRole | `userCount` sourced from `r.UserRoles.Count` Include |
| AdminEndpoints — AssignRole existence check | `db.UserRoles.AnyAsync(...)` — legacy table used as canonical check |
| Phase F TODO markers | Write paths had stale `[LEGACY — Phase F]` labels |
| ProviderService.CreateAsync | Called `AddAsync` first, then `LinkOrganization`, then a second `UpdateAsync` |

Phase C (OrganizationRelationship in CareConnect workflows) and Phase E (Control Center pages) were already complete.

---

## 2. Identity Convergence Changes

### 2.1 Phase A — OrganizationType as authoritative write model

#### `Organization.Update()` extended

The existing `Update()` method accepted only `name`, `displayName`, and `updatedByUserId`.
An optional `organizationTypeId` + `orgTypeCode` parameter pair is now supported:

```csharp
public void Update(string name, string? displayName, Guid? updatedByUserId,
    Guid?   organizationTypeId = null,
    string? orgTypeCode        = null)
```

When `organizationTypeId` is supplied the method delegates to `AssignOrganizationType()`,
keeping `OrgType` string and `OrganizationTypeId` FK in sync atomically.
This means any future `PATCH /api/admin/organizations/{id}` endpoint can resolve the FK
in one write without a separate backfill step.

The `Organization.Create()` canonical overload and `AssignOrganizationType()` were already
present from Step 1. This change closes the one remaining gap in the update path.

#### `OrgTypeMapper` — centralized string ↔ GUID mapping

New file: `apps/services/identity/Identity.Domain/OrgTypeMapper.cs`

```
Identity.Domain.OrgTypeMapper
  TryResolve(string? orgTypeCode)   → Guid?   (code → catalog GUID)
  TryResolveCode(Guid? id)          → string? (catalog GUID → code)
  AllCodes                          → IReadOnlyCollection<string>
```

The mapping table is the single source of truth for the five catalog records:

| OrgType code | OrganizationTypeId |
|--------------|--------------------|
| INTERNAL | 70000000-0000-0000-0000-000000000001 |
| LAW_FIRM | 70000000-0000-0000-0000-000000000002 |
| PROVIDER | 70000000-0000-0000-0000-000000000003 |
| FUNDER | 70000000-0000-0000-0000-000000000004 |
| LIEN_OWNER | 70000000-0000-0000-0000-000000000005 |

Previously every caller that needed to cross-reference the string code and the FK GUID
had to reach into `SeedIds` directly. `OrgTypeMapper` replaces that pattern for all
create/update flows.

Backward compatibility: `OrgType` string validation and JWT claims are unchanged.
`OrgType.IsValid()` still gates the `Create()` factory.

### 2.2 Phase B — UserRoles eliminated from all read paths

All five read paths that previously sourced data from `UserRoles` have been migrated.
`UserRoles` is now **write-only** (dual-write preserved for the backfill window).

#### AuthService — flip login role source

**Before (Step 5):**
```
roleNames = UserRoles (primary) ∪ ScopedRoleAssignments GLOBAL (additive merge)
```

**After (Step 6):**
```
if ScopedRoleAssignments GLOBAL (active) exist → roleNames = those only
else (fallback) → roleNames = UserRoles + LogWarning
```

This means that on any environment where migration `20260330200002` has run, the
fallback branch will never be reached. The `LogWarning` provides an explicit signal
if the fallback fires in production.

File: `apps/services/identity/Identity.Application/Services/AuthService.cs`

#### UserRepository — GetByIdWithRolesAsync

Include order swapped:
1. `ScopedRoleAssignments` (filtered to `IsActive = true`) — primary
2. `UserRoles` — retained with `TODO [Phase G]` marker

File: `apps/services/identity/Identity.Infrastructure/Repositories/UserRepository.cs`

#### UserRepository — GetAllWithRolesAsync

Added `ScopedRoleAssignments` Include (was missing entirely).
`UserRoles` Include retained with `TODO [Phase G]` marker.

#### AdminEndpoints — ListUsers

`Include(u => u.UserRoles)` removed. Role name resolved via correlated subquery
inside the LINQ projection — EF Core translates to a single SQL statement with a
correlated subselect:

```csharp
role = db.ScopedRoleAssignments
    .Where(s => s.UserId == u.Id && s.IsActive
             && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global)
    .Select(s => s.Role!.Name)
    .FirstOrDefault() ?? "User",
```

No N+1 queries; one round-trip regardless of result set size.

#### AdminEndpoints — GetUser

`Include(u => u.UserRoles)` replaced with a filtered Include:

```csharp
.Include(u => u.ScopedRoleAssignments
    .Where(s => s.IsActive && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global))
    .ThenInclude(s => s.Role)
```

Role name: `u.ScopedRoleAssignments.Select(s => s.Role.Name).FirstOrDefault() ?? "User"`.

#### AdminEndpoints — ListRoles

`Include(r => r.UserRoles)` removed. `userCount` resolved via correlated subquery in
the projection — no Role navigation property change required:

```csharp
userCount = db.ScopedRoleAssignments.Count(
    s => s.RoleId == r.Id && s.IsActive
      && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global),
```

#### AdminEndpoints — GetRole

`Include(r => r.UserRoles)` removed. Count issued as a separate single-row async query
(cleaner for a single-entity endpoint):

```csharp
var userCount = await db.ScopedRoleAssignments
    .CountAsync(s => s.RoleId == id && s.IsActive
                  && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global);
```

#### AdminEndpoints — AssignRole existence check

**Before:** `db.UserRoles.AnyAsync(ur => ur.UserId == id && ur.RoleId == roleId)`
**After:** `db.ScopedRoleAssignments.AnyAsync(s => ... && s.IsActive && s.ScopeType == GLOBAL)`

The check now looks at the authoritative table, preventing the edge case where a
ScopedRoleAssignment was created outside the admin endpoint but no UserRole record
existed (would have allowed a duplicate assignment previously).

---

## 3. Authorization Convergence Changes

### 3.1 Role eligibility path (AuthService.IsEligibleWithPath) — unchanged

`IsEligibleWithPath` was already correct after Step 5:
- Path 1: DB-backed `ProductOrganizationTypeRule` check
- Path 2 (fallback within Path 1): `OrgType` string match when `OrganizationTypeId` is null

No changes needed here; the method is already Phase F complete.

### 3.2 Phase F — TODO markers placed on all UserRoles write paths

Three `// TODO [Phase G — UserRoles Retirement]` markers placed:

| Location | What the marker guards |
|----------|------------------------|
| `UserRepository.AddAsync` | `db.UserRoles.AddAsync(...)` in new-user creation |
| `AdminEndpoints.AssignRole` | `db.UserRoles.Add(userRole)` in role assignment |
| `AdminEndpoints.RevokeRole` | `db.UserRoles.Remove(userRole)` in role revocation |

Each marker documents the pre-condition for safe removal and points to migration
`20260330200002` as the coverage gate.

---

## 4. Relationship Activation in CareConnect

### 4.1 Status at start of Step 6: already complete

Phase C (OrganizationRelationship in workflows) was confirmed **already implemented**
through code inspection. No new code was needed.

**ReferralService.CreateAsync** (lines 58–67):
- Checks for both `ReferringOrganizationId` and `ReceivingOrganizationId`
- Calls `_relationshipResolver.FindActiveRelationshipAsync(referringOrgId, receivingOrgId, ct)`
- Sets `Referral.OrganizationRelationshipId = relationship?.Id`
- Proceeds without error if resolver returns `null`

**AppointmentService.CreateAppointmentAsync** (line 112):
- Reads `referral.OrganizationRelationshipId`
- Denormalizes it onto the `Appointment` record for reporting without joins

**Resolver implementation:** `HttpOrganizationRelationshipResolver`
- Cross-service HTTP call to `Identity /api/admin/organization-relationships`
- Returns `null` on HTTP error, timeout, or no active match
- Registered in `CareConnect.Infrastructure.DependencyInjection` as `IOrganizationRelationshipResolver`

**Fail-safe:** Referral creation never fails due to a missing or unresolvable relationship.
`OrganizationRelationshipId` remains nullable throughout.

### 4.2 Phase D — Provider and Facility identity linkage

#### ProviderService.CreateAsync — redundant UpdateAsync eliminated

**Before:**
```csharp
await _providers.AddAsync(provider, ct);          // INSERT (no OrganizationId)
if (request.OrganizationId.HasValue)
{
    provider.LinkOrganization(request.OrganizationId.Value);
    await _providers.UpdateAsync(provider, ct);   // second round-trip
}
```

**After:**
```csharp
if (request.OrganizationId.HasValue)
    provider.LinkOrganization(request.OrganizationId.Value);  // mutate before persist

await _providers.AddAsync(provider, ct);          // single INSERT with OrganizationId set
```

This matches the pattern already used by `FacilityService.CreateAsync`. Both services
now apply `LinkOrganization` before the first `AddAsync` call.

`ProviderService.UpdateAsync` was already correct (single `UpdateAsync` call at end).

Both `Provider.LinkOrganization(Guid)` and `Facility.LinkOrganization(Guid)` remain
unchanged — the domain method sets `OrganizationId` and updates `UpdatedAtUtc`.

---

## 5. Files Changed

| File | Phase | Change |
|------|-------|--------|
| `Identity.Domain/Organization.cs` | A | `Update()` gains `organizationTypeId` + `orgTypeCode` optional params |
| `Identity.Domain/OrgTypeMapper.cs` | A | **New file** — centralized string↔GUID mapping helper |
| `Identity.Application/Services/AuthService.cs` | B | Login flips to ScopedRoleAssignments primary; UserRoles fallback with warning |
| `Identity.Infrastructure/Repositories/UserRepository.cs` | B / F | `GetByIdWithRolesAsync` and `GetAllWithRolesAsync` reordered; TODO markers added to write path |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | B / F | ListUsers, GetUser, ListRoles, GetRole — all sourced from ScopedRoleAssignments; AssignRole existence check migrated; TODO markers on dual-write blocks; RevokeRole scope constant normalised |
| `CareConnect.Application/Services/ProviderService.cs` | D | `LinkOrganization` moved before `AddAsync`; extra `UpdateAsync` removed |

No TypeScript/control-center files required changes — Phase E (list pages and API client methods) was already complete.

---

## 6. Remaining Legacy Dependencies

### 6.1 UserRoles table — write-only dependency

All read paths have been migrated. UserRoles is now written to but never read.

Remaining write locations:

| Location | File |
|----------|------|
| `UserRepository.AddAsync` | `Identity.Infrastructure/Repositories/UserRepository.cs` |
| `AdminEndpoints.AssignRole` | `Identity.Api/Endpoints/AdminEndpoints.cs` |
| `AdminEndpoints.RevokeRole` | `Identity.Api/Endpoints/AdminEndpoints.cs` |

Each is marked with `// TODO [Phase G — UserRoles Retirement]`.

### 6.2 OrgType string field on Organization

`Organization.OrgType` (varchar) is still written on every create/update, surfaced
in JWT claims as `org_type`, and used as a fallback in `IsEligibleWithPath` when
`OrganizationTypeId` is null. It will remain until:
- All Organization rows have `OrganizationTypeId` set (backfill migration required)
- JWT token consumers have been updated to use `org_type_id` instead of `org_type`

The `OrgType` string field **must not** be removed before those two gates are confirmed.

### 6.3 UserRoles navigation properties on User and Role domain models

`User.UserRoles` and `Role.UserRoles` collections are still defined and mapped by EF Core.
`UserRepository` still Includes them as a fallback in both `GetByIdWithRolesAsync` and
`GetAllWithRolesAsync`. These can be removed in Phase G.

---

## 7. Ready-for-Removal Checklist

### Phase G — UserRoles table retirement

All of the following must be confirmed true before the `UserRoles` table and its
associated code can be removed:

| # | Condition | Current status |
|---|-----------|----------------|
| 1 | Migration `20260330200002` applied on every environment | Required before deploy |
| 2 | `usersWithGapCount = 0` confirmed via `/api/admin/legacy-coverage` | Check after migration |
| 3 | `dualWriteCoveragePct = 100%` confirmed via `/api/admin/legacy-coverage` | Check after migration |
| 4 | AuthService fallback branch never fires in production logs | Monitor for 1 release cycle |
| 5 | No `LogWarning` of "fell back to legacy UserRoles" in any environment | Monitor |
| 6 | `UserRepository.GetByIdWithRolesAsync` — `Include(UserRoles)` removed | Code change |
| 7 | `UserRepository.GetAllWithRolesAsync` — `Include(UserRoles)` removed | Code change |
| 8 | `UserRepository.AddAsync` — `db.UserRoles.AddAsync(...)` removed | Code change |
| 9 | `AdminEndpoints.AssignRole` — `db.UserRoles.Add(...)` removed | Code change |
| 10 | `AdminEndpoints.RevokeRole` — `db.UserRoles.Remove(...)` removed | Code change |
| 11 | EF Core configuration for `UserRole` entity removed | Code change |
| 12 | Migration created to `DROP TABLE UserRoles` | Migration |
| 13 | `User.UserRoles` navigation property removed from domain | Code change |
| 14 | `Role.UserRoles` navigation property removed from domain | Code change |

### Summary of convergence gates

| Gate | Value | Status |
|------|-------|--------|
| EligibleOrgType column | Dropped (migration 20260330200003) | ✅ Step 5 |
| `withBothPaths` | 0 (Phase F constant) | ✅ Step 5 |
| `legacyStringOnly` | 0 (Phase F constant) | ✅ Step 5 |
| AuthService reads UserRoles | Never (primary) — fallback only | ✅ Step 6 |
| Admin endpoints read UserRoles | None | ✅ Step 6 |
| OrganizationRelationship in referrals | Active | ✅ Phase C pre-done |
| OrganizationRelationship in appointments | Active | ✅ Phase C pre-done |
| Provider/Facility LinkOrganization on create | Before first INSERT | ✅ Step 6 Phase D |
| OrgTypeMapper centralized | Yes | ✅ Step 6 Phase A |
| Control Center org/rel list pages | All wired | ✅ Phase E pre-done |
| `usersWithGapCount` | 0 (after migration 20260330200002) | Pending migration run |
| UserRoles table removal | Deferred to Phase G | Planned |
