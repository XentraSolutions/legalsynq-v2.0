# LegalSynq Live Relationship Integration

**Report generated:** 2026-03-30  
**Scope:** Phases 1–6 (step 3) — live cross-service relationship resolution and next integration layer  
**Predecessor:** `analysis/step2_activate-platform-architecture.md` (Phases A–F)  
**Build status:** ✅ Identity.Api — 0 errors / 0 warnings | ✅ CareConnect.Api — 0 errors / 1 pre-existing warning (CS0168)

---

## 1. Starting State

| Area | Condition entering this phase |
|------|-------------------------------|
| `IOrganizationRelationshipResolver` | Interface existed; `OrganizationRelationshipNullResolver` was the only registered implementation |
| Referral creation | Accepted `ReferringOrganizationId` / `ReceivingOrganizationId` but always resolved to `null` relationship ID |
| Appointment creation | Correctly read `OrganizationRelationshipId` from referral; that field was always null |
| `CurrentRequestContext` | Read `org_type` (string) and `org_id`; `org_type_id` claim was being emitted by JWT but never parsed |
| `ICurrentRequestContext` | No `OrgTypeId` property defined; callers had no typed access to the catalog UUID |
| Facility create/update | `OrganizationId` column and `LinkOrganization` method existed in domain; DTO/service not wired |
| `FacilityResponse` | Did not expose `OrganizationId` |
| `ReferralResponse` | Did not expose `ReferringOrganizationId`, `ReceivingOrganizationId`, or `OrganizationRelationshipId` |
| `AppointmentResponse` | Did not expose `OrganizationRelationshipId` |
| Legacy `UserRoles` write path | Used without a developer-visible deprecation signal |

---

## 2. Runtime Relationship Resolution Changes

### Phase 1 — IdentityServiceOptions

**New file:** `CareConnect.Infrastructure/Services/IdentityServiceOptions.cs`

```csharp
public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";
    public string? BaseUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 5;
}
```

Bound from `appsettings.json` under the `"IdentityService"` key. `BaseUrl` defaults to empty string — when unset the HTTP resolver skips the network call entirely and returns `null`.

Configuration added to both settings files:

```jsonc
// appsettings.json (production placeholder)
"IdentityService": {
  "BaseUrl": "",
  "TimeoutSeconds": 5
}

// appsettings.Development.json
"IdentityService": {
  "BaseUrl": "http://localhost:5001",
  "TimeoutSeconds": 5
}
```

---

### Phase 1 — HttpOrganizationRelationshipResolver

**New file:** `CareConnect.Infrastructure/Services/HttpOrganizationRelationshipResolver.cs`

Live implementation of `IOrganizationRelationshipResolver`. Calls the Identity admin endpoint:

```
GET {BaseUrl}/api/admin/organization-relationships
        ?sourceOrgId={referringOrganizationId:D}
        &activeOnly=true
        &pageSize=200
```

The Identity endpoint filters by `sourceOrganizationId` and `activeOnly=true` server-side. The resolver then scans the returned items for a match on `targetOrganizationId == receivingOrganizationId` and returns the matching relationship `id`, or `null`.

**Fail-safe layers:**

| Failure mode | Behaviour |
|---|---|
| `BaseUrl` not configured | Returns `null` immediately, no HTTP call made |
| HTTP timeout | `OperationCanceledException` caught → returns `null` + warning log |
| Network error | `Exception` caught → returns `null` + warning log |
| 4xx or 5xx from Identity | `!IsSuccessStatusCode` → returns `null` + warning log |
| JSON parse failure | `Exception` caught → returns `null` + warning log |
| No matching item | Returns `null` + debug log |

Referral creation is **never blocked** by relationship resolution. `null` is always a valid value for `OrganizationRelationshipId`.

**Private response models** (scoped to the class, not shared contracts):

```csharp
private sealed class OrgRelationshipPagedResponse
{
    [JsonPropertyName("items")]
    public List<OrgRelationshipItem>? Items { get; set; }
    public int TotalCount { get; set; }
}

private sealed class OrgRelationshipItem
{
    public Guid Id { get; set; }
    public Guid SourceOrganizationId { get; set; }
    public Guid TargetOrganizationId { get; set; }
    public bool IsActive { get; set; }
}
```

These are intentionally not shared contracts — they model a specific admin API response shape and should not be coupled to the domain.

---

### Phase 2 — DI Registration Switch

**File:** `CareConnect.Infrastructure/DependencyInjection.cs`

Changes:

```csharp
// Added before DbContext registration:
services.Configure<IdentityServiceOptions>(
    configuration.GetSection(IdentityServiceOptions.SectionName));
services.AddHttpClient("IdentityService");

// Changed from NullResolver to HTTP resolver:
// Before: services.AddScoped<IOrganizationRelationshipResolver, OrganizationRelationshipNullResolver>();
services.AddScoped<IOrganizationRelationshipResolver, HttpOrganizationRelationshipResolver>();
```

`OrganizationRelationshipNullResolver` is retained in the codebase. It remains available as an explicit override in integration test host setups that do not need real Identity connectivity:

```csharp
// In a test host:
services.AddScoped<IOrganizationRelationshipResolver, OrganizationRelationshipNullResolver>();
```

---

## 3. Identity / Auth Context Changes

### Phase 3 — OrgTypeId in ICurrentRequestContext

**File:** `shared/building-blocks/BuildingBlocks/Context/ICurrentRequestContext.cs`

New property added to the interface:

```csharp
/// <summary>
/// Phase B: canonical OrganizationType catalog ID from the org_type_id JWT claim.
/// Null when the token was issued before org_type_id was added, or when the
/// organization has not yet been assigned an OrganizationType.
/// Prefer this over OrgType (string) in new code.
/// </summary>
Guid? OrgTypeId { get; }
```

**File:** `shared/building-blocks/BuildingBlocks/Context/CurrentRequestContext.cs`

Implementation added:

```csharp
public Guid? OrgTypeId =>
    Guid.TryParse(User?.FindFirstValue("org_type_id"), out var otid) ? otid : null;
```

**Backward compatibility:** `OrgType` (string) is unchanged. All existing middleware, policies, and service code that reads `OrgType` continues to work. `OrgTypeId` is an additive property — nothing reads it by default. New authorization logic should prefer `OrgTypeId` when available and fall back to `OrgType`.

The `AuthorizationService.IsAuthorizedAsync` path is unchanged — it delegates to `ICapabilityService.HasCapabilityAsync` which uses product roles, not org type. No changes to policy registration or `Policies.cs` were needed.

---

## 4. CareConnect Identity Alignment Changes

### Phase 4 — Facility DTO and Service Wiring

**File:** `CareConnect.Application/DTOs/FacilityDTOs.cs`

Added `OrganizationId?` to all three Facility DTO types:

```csharp
public class CreateFacilityRequest
{
    // ... existing fields ...
    public Guid? OrganizationId { get; set; }   // new
}

public class UpdateFacilityRequest
{
    // ... existing fields ...
    public Guid? OrganizationId { get; set; }   // new
}

public class FacilityResponse
{
    // ... existing fields ...
    public Guid? OrganizationId { get; init; }  // new
}
```

All fields are optional. Callers that omit `OrganizationId` receive identical behaviour to before. The response field is null for legacy facilities that predate the org-alignment migration.

**File:** `CareConnect.Application/Services/FacilityService.cs`

`CreateAsync` — calls `LinkOrganization` before `AddAsync` when `OrganizationId` is provided:

```csharp
if (request.OrganizationId.HasValue)
    facility.LinkOrganization(request.OrganizationId.Value);
await _facilities.AddAsync(facility, ct);
```

`UpdateAsync` — calls `LinkOrganization` before `UpdateAsync` (supports backfill via update call):

```csharp
facility.Update(...);
if (request.OrganizationId.HasValue)
    facility.LinkOrganization(request.OrganizationId.Value);
await _facilities.UpdateAsync(facility, ct);
```

`ToResponse` — now includes `OrganizationId = f.OrganizationId`.

No new migrations required — the `OrganizationId` column was already added in Phase 5 (Phases 1–6 work).

---

### Phase 5 — Referral and Appointment Response Enrichment

**File:** `CareConnect.Application/DTOs/ReferralResponse.cs`

Three new nullable fields added:

```csharp
public Guid? ReferringOrganizationId { get; set; }
public Guid? ReceivingOrganizationId { get; set; }
public Guid? OrganizationRelationshipId { get; set; }
```

**File:** `CareConnect.Application/Services/ReferralService.cs` — `ToResponse` updated:

```csharp
ReferringOrganizationId    = r.ReferringOrganizationId,
ReceivingOrganizationId    = r.ReceivingOrganizationId,
OrganizationRelationshipId = r.OrganizationRelationshipId
```

**File:** `CareConnect.Application/DTOs/AppointmentDTOs.cs` — `AppointmentResponse` extended:

```csharp
public Guid? OrganizationRelationshipId { get; init; }
```

**File:** `CareConnect.Application/Services/AppointmentService.cs` — `ToAppointmentResponse` updated:

```csharp
OrganizationRelationshipId = a.OrganizationRelationshipId
```

Both fields are nullable. Pre-Phase C records and records created without org IDs will expose `null`. No existing API callers are broken — the fields are additive.

---

## 5. Legacy Dependency Reduction

### Phase 6 — UserRoles write path annotated

**File:** `Identity.Infrastructure/Repositories/UserRepository.cs`

A targeted comment was added to `AddAsync` at the point where `UserRole` records are written (the `user_roles` join table, which predates `ScopedRoleAssignment`):

```csharp
// TODO [LEGACY — Phase F]: UserRoles maps to user_roles (UserRole join entity),
// which is the simple user-to-role table predating ScopedRoleAssignment.
// New callers should create ScopedRoleAssignment (scope=GLOBAL) instead.
// This path is retained for backward compatibility — do not add new callers.
foreach (var roleId in roleIds)
    await _db.UserRoles.AddAsync(UserRole.Create(user.Id, roleId), ct);
```

### Summary of remaining legacy reads

| Legacy path | Status | Retirement condition |
|-------------|--------|---------------------|
| `ProductRole.EligibleOrgType` read in `AuthService.IsEligible` | Still active as 3rd-tier fallback | Retire when all `ProductRole` rows have `ProductOrganizationTypeRule` entries seeded |
| `UserRole` / `user_roles` write in `UserRepository.AddAsync` | Still active; annotated | Retire when new user registration is migrated to `ScopedRoleAssignment` creation |
| `UserRoleAssignment` table | Not read in any application path (only schema/migrations) | Retire when confirmed no consumer reads it directly |
| `OrgType` (string) claim on JWT | Retained for backward compat; `org_type_id` (UUID) emitted alongside it | Retire when all consumers prefer `OrgTypeId` |

None of these were removed in this phase — safe retirement of each requires end-to-end validation of the consuming paths, which is deferred to a targeted migration phase.

---

## 6. Files Changed

| File | Phase | Type |
|------|-------|------|
| `CareConnect.Infrastructure/Services/IdentityServiceOptions.cs` | 1 | **New** |
| `CareConnect.Infrastructure/Services/HttpOrganizationRelationshipResolver.cs` | 1 | **New** |
| `CareConnect.Infrastructure/DependencyInjection.cs` | 2 | Modified — options + HTTP client + resolver swap |
| `CareConnect.Api/appsettings.json` | 2 | Modified — IdentityService section added |
| `CareConnect.Api/appsettings.Development.json` | 2 | Modified — IdentityService section (dev values) |
| `BuildingBlocks/Context/ICurrentRequestContext.cs` | 3 | Modified — OrgTypeId property added |
| `BuildingBlocks/Context/CurrentRequestContext.cs` | 3 | Modified — OrgTypeId claim reader added |
| `CareConnect.Application/DTOs/FacilityDTOs.cs` | 4 | Modified — OrganizationId on all three types |
| `CareConnect.Application/Services/FacilityService.cs` | 4 | Modified — LinkOrganization in Create + Update + ToResponse |
| `CareConnect.Application/DTOs/ReferralResponse.cs` | 5 | Modified — 3 new nullable org fields |
| `CareConnect.Application/Services/ReferralService.cs` | 5 | Modified — ToResponse populates org fields |
| `CareConnect.Application/DTOs/AppointmentDTOs.cs` | 5 | Modified — OrganizationRelationshipId on AppointmentResponse |
| `CareConnect.Application/Services/AppointmentService.cs` | 5 | Modified — ToAppointmentResponse populates org relationship |
| `Identity.Infrastructure/Repositories/UserRepository.cs` | 6 | Modified — legacy comment on UserRoles write path |

**Total:** 2 new files, 12 modified files. No new DB migrations. No breaking changes to existing API surfaces.

---

## 7. Build / Verification Results

```
dotnet build apps/services/identity/Identity.Api/Identity.Api.csproj --nologo
→ Build succeeded. 0 Error(s). 0 Warning(s).

dotnet build apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj --nologo
→ Build succeeded. 0 Error(s). 1 Warning(s).
   [pre-existing CS0168 in ExceptionHandlingMiddleware.cs:62 — unrelated to this work]
```

No TypeScript files were changed in this phase — `tsc --noEmit` is not required.

---

## 8. Remaining Gaps

| Item | Priority | Effort | Notes |
|------|----------|--------|-------|
| Set `IdentityService:BaseUrl` in production via secret/env var | **Critical** | Low | Without it, HTTP resolver is no-op in prod |
| Seed `ProductOrganizationTypeRule` rows for all existing `ProductRole` records | High | Medium | Required before retiring `EligibleOrgType` |
| Retire `UserRoles` write path in `UserRepository.AddAsync` | Medium | Medium | Requires migrating user registration to `ScopedRoleAssignment` |
| Drop `UserRoleAssignment` table | Low | Low | Confirm no consumer reads it; single migration |
| Retire `ProductRole.EligibleOrgType` column | Low | Low | After full rule-table seeding; single migration |
| Add org-scoped `ScopedRoleAssignment` support to login token | Medium | High | Multi-claim JWT extension; future auth phase |
| Add `OrgTypeId`-aware authorization policies | Medium | Medium | New `RequireOrgTypeId` policy alongside existing `OrgType` string checks |
| control-center admin pages for the 5 new catalog endpoints | High (UX) | High | Frontend pages — Organization Types, Relationship Types, Relationships, Rules |
| `IdentityService:BaseUrl` service-to-service auth header | Medium | Medium | Add optional API key or mutual-TLS header to `HttpOrganizationRelationshipResolver` if the Identity endpoint is gated behind a service-level auth check in production |
