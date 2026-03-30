# LegalSynq — Activate Platform Architecture

**Report generated:** 2026-03-30  
**Scope:** Continuation Phases A–F — wiring the Phase 1–6 infrastructure into live runtime behaviour  
**Predecessor:** `analysis/step1_platform-foundation-upgrade.md` (Phases 1–6)  
**Build status (after all changes):** ✅ Identity.Api — 0 errors / 0 warnings | ✅ CareConnect.Api — 0 errors / 1 pre-existing warning (CS0168) | ✅ control-center TypeScript — 0 errors

---

## 1. Context

Phases 1–6 built the structural foundation for a catalog-driven, multi-org platform:

- **Phase 1** — `OrganizationType` catalog table replacing the hard-coded `OrgType` static class  
- **Phase 2** — `RelationshipType` + `OrganizationRelationship` + `ProductRelationshipTypeRule` tables  
- **Phase 3** — `ProductOrganizationTypeRule` table + `AuthService.IsEligible` wired to read from it  
- **Phase 4** — `ScopedRoleAssignment` table with scope discriminator (GLOBAL / ORG / PRODUCT)  
- **Phase 5** — CareConnect schema alignment (`Provider.OrganizationId`, `Facility.OrganizationId`, `Referral.OrganizationRelationshipId`, `Appointment.OrganizationRelationshipId`)  
- **Phase 6** — 10 admin endpoints in Identity.Api for managing all new catalog tables  

The tables and endpoints existed, but the application code had not yet been updated to write into them or read from them at runtime. This report documents Phases A–F, which complete that activation.

---

## 2. State Before Phases A–F

| Area | Gap |
|------|-----|
| `Organization.Create` | No overload accepting `OrganizationTypeId` — new orgs were created without a type FK |
| `User.ScopedRoleAssignments` | Nav property not configured in EF; eager-load chains omitted it |
| `AuthService.LoginAsync` | Only read from `UserRoleAssignment` (legacy table); ignored `ScopedRoleAssignment` |
| `AuthService.IsEligible` | Fell back entirely to `ProductRole.EligibleOrgType` string comparison; DB rule table unused at login |
| `JwtTokenService` | Emitted `org_id` + `org_type` (string) only; no `org_type_id` claim |
| `Referral.Create` | Accepted `ReferringOrganizationId` / `ReceivingOrganizationId` but always passed `null` from the service layer; `OrganizationRelationshipId` column was never written to |
| `Appointment.Create` | `OrganizationRelationshipId` column existed but was never populated |
| `Provider` / `Facility` | `OrganizationId` column existed but no method to set it; DTOs had no field for it |
| `CreateReferralRequest` | No org context fields — callers could not pass referring/receiving org IDs |
| control-center TypeScript | No types, mappers, or API methods for the new catalog endpoints |
| Legacy entities | `ProductRole.EligibleOrgType` and `UserRoleAssignment` had no deprecation signals for developers |

---

## 3. Changes Delivered

### Phase A — Organization.Create Overload

**File:** `Identity.Domain/Organization.cs`

Added a second `Create` factory overload that accepts `organizationTypeId`:

```csharp
public static Organization Create(
    Guid tenantId, string name, string orgType, Guid? organizationTypeId = null)
```

Added a post-create / backfill instance method:

```csharp
public void AssignOrganizationType(Guid organizationTypeId, string code)
{
    OrganizationTypeId = organizationTypeId;
    OrgType = code;   // keep legacy string in sync
}
```

**Rationale:** Callers that know the catalog ID at creation time can pass it directly. Callers that don't (import tools, legacy migrations) can use `AssignOrganizationType` without recreating the record.

---

### Phase B — DB-Backed Eligibility Activation

**Files:** `Identity.Domain/User.cs`, `Identity.Infrastructure/Repositories/UserRepository.cs`, `Identity.Application/Services/AuthService.cs`, `Identity.Infrastructure/Services/JwtTokenService.cs`

#### User.ScopedRoleAssignments nav collection

```csharp
public ICollection<ScopedRoleAssignment> ScopedRoleAssignments { get; private set; } = new List<ScopedRoleAssignment>();
```

EF config updated from `WithMany()` → `WithMany(u => u.ScopedRoleAssignments)` so the collection is populated on eager-load.

#### UserRepository eager-load chains

`GetByIdWithRolesAsync` now includes:

```
User → ScopedRoleAssignments → Role
```

`GetPrimaryOrgMembershipAsync` now includes a second chain:

```
UserOrganizationMembership → Organization → OrganizationTypeRef (OrganizationType)
```

This gives `AuthService` access to the typed org reference without a second query.

#### AuthService.LoginAsync — GLOBAL role merge

```csharp
var globalRoles = user.ScopedRoleAssignments
    .Where(sra => sra.IsActive && sra.ScopeType == "GLOBAL")
    .Select(sra => sra.Role?.Name)
    .Where(n => n is not null)
    .Cast<string>();

roleNames = roleNames.Union(globalRoles).ToList();
```

GLOBAL-scoped assignments in `ScopedRoleAssignment` are merged into the role list returned to the token service. Org-scoped and product-scoped assignments are intentionally excluded here — they belong in future claim extensions.

#### AuthService.IsEligible — ID-first comparison

```csharp
// Prefer DB rule table (new path)
if (org.OrganizationTypeId.HasValue && productRole.OrgTypeRules.Count > 0)
{
    return productRole.OrgTypeRules.Any(r =>
        r.IsActive && r.OrganizationTypeId == org.OrganizationTypeId.Value);
}

// Fall back to string comparison (legacy compat)
if (!string.IsNullOrWhiteSpace(productRole.EligibleOrgType))
    return string.Equals(productRole.EligibleOrgType, org.OrgType, StringComparison.OrdinalIgnoreCase);

return true; // no restriction configured
```

This three-tier logic ensures backward compatibility while preferring the DB rule table.

#### JwtTokenService — org_type_id claim

```csharp
if (organization.OrganizationTypeId.HasValue)
    claims.Add(new Claim("org_type_id", organization.OrganizationTypeId.Value.ToString()));
```

The token now carries both `org_type` (legacy string) and `org_type_id` (catalog UUID). Downstream consumers choose which to use. The `org_type` claim is preserved so existing validation middleware is unaffected.

---

### Phase C — CareConnect Relationship Persistence

**Files:** `CareConnect.Application/Interfaces/IOrganizationRelationshipResolver.cs`, `CareConnect.Infrastructure/Services/OrganizationRelationshipNullResolver.cs`, `CareConnect.Domain/Referral.cs`, `CareConnect.Domain/Appointment.cs`, `CareConnect.Application/DTOs/CreateReferralRequest.cs`, `CareConnect.Application/Services/ReferralService.cs`, `CareConnect.Application/Services/AppointmentService.cs`, `CareConnect.Infrastructure/DependencyInjection.cs`

#### IOrganizationRelationshipResolver interface

```csharp
public interface IOrganizationRelationshipResolver
{
    Task<Guid?> FindActiveRelationshipAsync(
        Guid referringOrganizationId,
        Guid receivingOrganizationId,
        CancellationToken ct = default);
}
```

CareConnect does not have direct access to the Identity database. This interface cleanly abstracts the lookup, decoupling the domain from how the relationship is resolved. Three implementations are possible:

| Implementation | Status | Notes |
|----------------|--------|-------|
| `OrganizationRelationshipNullResolver` | ✅ Shipped | Safe default — always returns null |
| `HttpOrganizationRelationshipResolver` | 🔲 Planned | Calls `GET /identity/api/admin/organization-relationships` |
| `CachedOrganizationRelationshipResolver` | 🔲 Planned | Event-sourced local cache of relationship data |

#### OrganizationRelationshipNullResolver

```csharp
public sealed class OrganizationRelationshipNullResolver : IOrganizationRelationshipResolver
{
    public Task<Guid?> FindActiveRelationshipAsync(
        Guid referringOrganizationId,
        Guid receivingOrganizationId,
        CancellationToken ct = default)
        => Task.FromResult<Guid?>(null);
}
```

Returns null safely. When replaced with the HTTP resolver, the service layer and domain code require no changes — only the DI registration changes.

#### Referral.Create extended

```csharp
public static Referral Create(
    ...,
    Guid? organizationRelationshipId = null)
```

Added optional parameter — all existing call sites are source-compatible (no breakage).

#### Referral.SetOrganizationRelationshipId

```csharp
public void SetOrganizationRelationshipId(Guid organizationRelationshipId)
{
    OrganizationRelationshipId = organizationRelationshipId;
    UpdatedAtUtc = DateTime.UtcNow;
}
```

Used for post-create linking (async resolution, import backfill, admin correction).

#### Appointment — OrganizationRelationshipId denormalization

`Appointment.Create` now accepts an optional `organizationRelationshipId`. `AppointmentService.CreateAppointmentAsync` loads the referral (previously discarding the result with `_ =`) and passes `referral.OrganizationRelationshipId` into `Appointment.Create`. This means appointment-level reporting can filter by org relationship without joining back to Referral.

#### CreateReferralRequest extension

```csharp
public Guid? ReferringOrganizationId { get; set; }
public Guid? ReceivingOrganizationId { get; set; }
```

These are optional. Existing callers that omit them continue to work; the service falls through to `null` for the relationship ID.

#### ReferralService.CreateAsync resolution flow

```
1. Both org IDs present?
   → call _relationshipResolver.FindActiveRelationshipAsync(...)
   → orgRelationshipId = result (may be null if resolver returns null)
2. Pass referringOrganizationId, receivingOrganizationId, orgRelationshipId into Referral.Create
```

---

### Phase D — Provider / Facility Identity Alignment

**Files:** `CareConnect.Domain/Provider.cs`, `CareConnect.Domain/Facility.cs`, `CareConnect.Application/DTOs/CreateProviderRequest.cs`, `CareConnect.Application/DTOs/UpdateProviderRequest.cs`, `CareConnect.Application/Services/ProviderService.cs`

#### Domain methods

```csharp
// Provider.cs
public void LinkOrganization(Guid organizationId)
{
    OrganizationId = organizationId;
    UpdatedAtUtc = DateTime.UtcNow;
}

// Facility.cs — identical pattern
public void LinkOrganization(Guid organizationId)
{
    OrganizationId = organizationId;
    UpdatedAtUtc = DateTime.UtcNow;
}
```

These methods are the only write path to `OrganizationId`. The column was added in Phase 5 but remained null in all existing records. `LinkOrganization` makes the assignment explicit and auditable.

#### DTO extension

```csharp
// Added to both CreateProviderRequest and UpdateProviderRequest
public Guid? OrganizationId { get; set; }
```

Optional — existing API callers that omit the field are unaffected.

#### ProviderService wiring

```csharp
// CreateAsync
if (request.OrganizationId.HasValue)
{
    provider.LinkOrganization(request.OrganizationId.Value);
    await _providers.UpdateAsync(provider, ct);
}

// UpdateAsync
if (request.OrganizationId.HasValue)
    provider.LinkOrganization(request.OrganizationId.Value);
// UpdateAsync is called regardless — LinkOrganization runs before it
```

The create path needs a second `UpdateAsync` call because `Provider.Create` factory does not accept `OrganizationId` — the pattern follows the same two-step approach used for CategoryIds.

---

### Phase E — Control-Center Frontend Compatibility

**Files:** `apps/control-center/src/types/control-center.ts`, `apps/control-center/src/lib/api-mappers.ts`, `apps/control-center/src/lib/control-center-api.ts`, `apps/control-center/src/lib/api-client.ts`

#### New TypeScript types

```typescript
// Catalog reference types
interface OrganizationTypeItem { id, code, name, description, isActive, createdAtUtc }
interface RelationshipTypeItem { id, code, name, description, isActive, createdAtUtc }

// Relationship graph
type OrgRelationshipStatus = 'Active' | 'Inactive' | 'Pending';
interface OrgRelationship {
  id, sourceOrganizationId, targetOrganizationId,
  relationshipTypeId, relationshipTypeCode,
  status, effectiveFromUtc?, effectiveToUtc?, createdAtUtc, updatedAtUtc
}

// Product access rules
interface ProductOrgTypeRule  { id, productId, productCode, organizationTypeId, organizationTypeCode, isActive, createdAtUtc }
interface ProductRelTypeRule  { id, productId, productCode, relationshipTypeId, relationshipTypeCode, isActive, createdAtUtc }
```

All types follow the same conventions as existing types in the file: string IDs, optional fields as `?`, timestamps as ISO 8601 strings.

#### New mappers

Five mappers added to `api-mappers.ts`:

| Mapper | Input source |
|--------|-------------|
| `mapOrganizationTypeItem` | `GET /identity/api/admin/organization-types[/{id}]` |
| `mapRelationshipTypeItem` | `GET /identity/api/admin/relationship-types[/{id}]` |
| `mapOrgRelationship` | `GET /identity/api/admin/organization-relationships[/{id}]` |
| `mapProductOrgTypeRule` | `GET /identity/api/admin/product-org-type-rules` |
| `mapProductRelTypeRule` | `GET /identity/api/admin/product-rel-type-rules` |

All mappers follow the established pattern: snake_case-first field reads with camelCase fallback, `oneOf` validation for enum fields, safe defaults, dev-mode `console.warn` on unexpected values.

#### New API namespaces

Five namespaces added to the `controlCenterApi` object in `control-center-api.ts`:

```
organizationTypes
  .list()                          → OrganizationTypeItem[]          TTL 300 s  tag cc:org-types
  .getById(id)                     → OrganizationTypeItem | null

relationshipTypes
  .list()                          → RelationshipTypeItem[]          TTL 300 s  tag cc:rel-types
  .getById(id)                     → RelationshipTypeItem | null

organizationRelationships
  .list(params?)                   → PagedResponse<OrgRelationship>  TTL 60 s   tag cc:org-relationships
  .getById(id)                     → OrgRelationship | null

productOrgTypeRules
  .list(params?)                   → ProductOrgTypeRule[]            TTL 300 s  tag cc:product-org-type-rules

productRelTypeRules
  .list(params?)                   → ProductRelTypeRule[]            TTL 300 s  tag cc:product-rel-type-rules
```

All methods use `apiClient.get(url, ttl, [tag])` — the same signature as every existing namespace. Cache TTLs are calibrated to data volatility: 300 s for near-static catalog data, 60 s for live relationship records.

#### New CACHE_TAGS entries

```typescript
orgTypes:             'cc:org-types',
relTypes:             'cc:rel-types',
orgRelationships:     'cc:org-relationships',
productOrgTypeRules:  'cc:product-org-type-rules',
productRelTypeRules:  'cc:product-rel-type-rules',
```

Tags are `as const` so they participate in the `CacheTag` union type and can be passed to `revalidateTag()` with full type safety.

---

### Phase F — Legacy Architecture Deprecation Notices

**Files:** `Identity.Domain/ProductRole.cs`, `Identity.Domain/UserRoleAssignment.cs`

#### ProductRole.EligibleOrgType

```csharp
/// <summary>
/// TODO [LEGACY — Phase F]: retire this field once all ProductRoles have OrgTypeRules seeded
/// and AuthService IsEligible fully uses the ProductOrganizationTypeRule table.
/// Keep populated for backward compatibility; do not use in new code.
/// </summary>
public string? EligibleOrgType { get; private set; }
```

The field remains in the schema and is still read by `IsEligible` as a fallback. The notice signals to developers that new `ProductRole` rows should be assigned rules via `ProductOrganizationTypeRule` instead.

#### UserRoleAssignment class

```csharp
/// <summary>
/// TODO [LEGACY — Phase F]: this table predates ScopedRoleAssignment (Phase 4).
/// ScopedRoleAssignment is the forward-looking model (with scope discriminator).
/// UserRoleAssignment is kept for backward compatibility; do not create new records here.
/// All rows have been back-populated into ScopedRoleAssignments via migration 20260330110004.
/// </summary>
public class UserRoleAssignment { ... }
```

The table is not dropped — doing so before all consumers are migrated would break existing queries. The notice makes the intent clear for any developer who encounters the class in an IDE or code review.

---

## 4. Architecture After Phases A–F

### Identity — JWT payload evolution

| Claim | Before | After |
|-------|--------|-------|
| `org_id` | ✅ Always emitted | ✅ Unchanged |
| `org_type` | ✅ Always emitted (string) | ✅ Unchanged (backward compat) |
| `org_type_id` | ❌ Not emitted | ✅ Emitted when `OrganizationTypeId` is set |
| `product_roles` | ✅ Always emitted | ✅ Unchanged |
| `role` | ✅ From `UserRoleAssignment` | ✅ + merged from `ScopedRoleAssignment` (GLOBAL scope) |

### Identity — eligibility resolution order

```
1. org.OrganizationTypeId present AND productRole.OrgTypeRules not empty?
      → evaluate ProductOrganizationTypeRule table (DB-backed)
      → return true/false

2. productRole.EligibleOrgType not null?
      → evaluate string equality with org.OrgType (legacy string)
      → return true/false

3. Neither configured?
      → return true (no restriction)
```

### CareConnect — referral creation flow

```
CreateReferralRequest received
  ↓
ReferralService.CreateAsync
  ├─ Validate
  ├─ Load provider
  ├─ [NEW] If both org IDs present → IOrganizationRelationshipResolver.FindActiveRelationshipAsync(...)
  │         → returns Guid? (null from NullResolver; Guid from HttpResolver when deployed)
  ├─ Referral.Create(..., organizationRelationshipId: orgRelationshipId)
  └─ Save + return
```

### CareConnect — appointment creation flow

```
CreateAppointmentRequest received
  ↓
AppointmentService.CreateAppointmentAsync
  ├─ Validate
  ├─ [CHANGED] Load referral (previously _ = discarded)
  ├─ Load + validate slot
  ├─ Appointment.Create(..., organizationRelationshipId: referral.OrganizationRelationshipId)
  └─ Save + return
```

### Control-Center — new API surface

```
controlCenterApi
  ├─ tenants           (existing)
  ├─ users             (existing)
  ├─ roles             (existing)
  ├─ audit             (existing)
  ├─ settings          (existing)
  ├─ monitoring        (existing)
  ├─ support           (existing)
  ├─ organizationTypes         [NEW — Phase E]
  ├─ relationshipTypes         [NEW — Phase E]
  ├─ organizationRelationships [NEW — Phase E]
  ├─ productOrgTypeRules       [NEW — Phase E]
  └─ productRelTypeRules       [NEW — Phase E]
```

---

## 5. What Was NOT Changed

| Item | Reason |
|------|--------|
| `UserRoleAssignment` table | Kept for backward compat; deprecated via doc comment only |
| `ProductRole.EligibleOrgType` column | Kept as legacy fallback in `IsEligible`; deprecated via doc comment only |
| `OrgType` static class | Not removed — `org_type` claim is still the primary string for legacy consumers |
| `Facility` DTOs (`CreateFacilityRequest`) | `OrganizationId` not yet added — Facility creation API follows same pattern as Provider when ready |
| `HttpOrganizationRelationshipResolver` | Not implemented — depends on Identity admin endpoint stability; null resolver is a safe placeholder |
| CareConnect migrations | No new migrations needed — `OrganizationRelationshipId` columns were added in Phase 5; only domain/service code was updated |

---

## 6. Remaining Work (Next Steps)

| Item | Priority | Effort |
|------|----------|--------|
| Implement `HttpOrganizationRelationshipResolver` | High | Medium — HTTP client + retry/timeout |
| Replace `OrganizationRelationshipNullResolver` in DI once HTTP resolver is stable | High | Low — single DI line |
| Add `OrganizationId` to `CreateFacilityRequest` and wire `FacilityService` | Medium | Low — mirrors Phase D exactly |
| Seed `ProductOrganizationTypeRule` rows for all existing `ProductRole` records | Medium | Low — SQL seed script |
| Drop or archive `UserRoleAssignment` table (after verifying all consumers migrated) | Low | Low — single migration |
| Retire `ProductRole.EligibleOrgType` column (after rule table fully seeded) | Low | Low — migration + `IsEligible` simplification |
| Add `org_type_id` claim reader to `BuildingBlocks.Authorization.CurrentRequestContext` | Medium | Low — one claim read |
| Add org-scoped and product-scoped `ScopedRoleAssignment` support to login token | Medium | Medium — multi-claim structure |
| control-center pages/components for the 5 new API namespaces | High (UX) | High — full page build |

---

## 7. Build Verification

```
dotnet build apps/services/identity/Identity.Api/Identity.Api.csproj --nologo
→ Build succeeded. 0 Error(s). 0 Warning(s).

dotnet build apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj --nologo
→ Build succeeded. 0 Error(s). 1 Warning(s). [pre-existing CS0168 in ExceptionHandlingMiddleware — unrelated]

cd apps/control-center && npx tsc --noEmit
→ (no output) — 0 errors
```

---

## 8. File Change Summary

| File | Phase | Type |
|------|-------|------|
| `Identity.Domain/Organization.cs` | A | Modified — Create overload + AssignOrganizationType |
| `Identity.Domain/User.cs` | B | Modified — ScopedRoleAssignments nav collection |
| `Identity.Infrastructure/Repositories/UserRepository.cs` | B | Modified — eager-load chains |
| `Identity.Application/Services/AuthService.cs` | B | Modified — GLOBAL role merge + IsEligible ID-first |
| `Identity.Infrastructure/Services/JwtTokenService.cs` | B | Modified — org_type_id claim |
| `Identity.Domain/ProductRole.cs` | F | Modified — legacy deprecation comment |
| `Identity.Domain/UserRoleAssignment.cs` | F | Modified — legacy deprecation comment |
| `CareConnect.Application/Interfaces/IOrganizationRelationshipResolver.cs` | C | **New** |
| `CareConnect.Infrastructure/Services/OrganizationRelationshipNullResolver.cs` | C | **New** |
| `CareConnect.Domain/Referral.cs` | C | Modified — Create overload + SetOrganizationRelationshipId |
| `CareConnect.Domain/Appointment.cs` | C | Modified — Create overload + SetOrganizationRelationshipId |
| `CareConnect.Application/DTOs/CreateReferralRequest.cs` | C | Modified — ReferringOrganizationId + ReceivingOrganizationId |
| `CareConnect.Application/Services/ReferralService.cs` | C | Modified — resolver injection + org context wiring |
| `CareConnect.Application/Services/AppointmentService.cs` | C | Modified — referral load + denormalized org relationship |
| `CareConnect.Infrastructure/DependencyInjection.cs` | C | Modified — NullResolver registration |
| `CareConnect.Domain/Provider.cs` | D | Modified — LinkOrganization method |
| `CareConnect.Domain/Facility.cs` | D | Modified — LinkOrganization method |
| `CareConnect.Application/DTOs/CreateProviderRequest.cs` | D | Modified — OrganizationId field |
| `CareConnect.Application/DTOs/UpdateProviderRequest.cs` | D | Modified — OrganizationId field |
| `CareConnect.Application/Services/ProviderService.cs` | D | Modified — LinkOrganization call in Create + Update |
| `control-center/src/types/control-center.ts` | E | Modified — 5 new types |
| `control-center/src/lib/api-mappers.ts` | E | Modified — 5 new mappers |
| `control-center/src/lib/control-center-api.ts` | E | Modified — 5 new API namespaces |
| `control-center/src/lib/api-client.ts` | E | Modified — 5 new CACHE_TAGS entries |

**Total:** 2 new files, 22 modified files across Identity, CareConnect, and control-center.
