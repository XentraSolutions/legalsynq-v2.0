# Step 4 — Platform Hardening & Legacy Cutover

**Date:** 2026-03-30  
**Status:** Complete (both passes)

---

## Scope

Step 4 consolidates two full passes of work:

1. **First pass** — observability, dual-write scaffolding, startup diagnostics, and four ORGANIZATION GRAPH admin pages.  
2. **Second pass** — measurable legacy fallback paths, API format fixes, a `/legacy-coverage` admin page, and this analysis document.

---

## Pass 1 — Observability & Dual-Write Scaffolding

### Changes

| Area | What changed |
|---|---|
| `IdentityServiceOptions` | `AuthHeader` config property added; read from `appsettings.json` via DI |
| `AuthService` | `ILogger<AuthService>` + `EligibilityPath` enum (`DbRule \| LegacyString \| Unrestricted`); path logged per login |
| `ProviderService` / `FacilityService` | `LinkOrganization` structured log added |
| `UserRepository` | `ScopedRoleAssignment.Create(...)` dual-write on every `AssignRole` call (GLOBAL scope) |
| `Identity.Api.Program.cs` | Startup diagnostic: counts ProductRoles per EligibleOrgType; logs warning if any uncovered |
| Control-center nav | `ORGANIZATION GRAPH` section added with four admin pages |
| Admin pages | `/org-types`, `/relationship-types`, `/org-relationships`, `/product-rules` |

---

## Pass 2 — API Format Fixes + Legacy Coverage Endpoint

### Bug fixes in `AdminEndpoints.cs`

Three production bugs found in the Step 4 first-pass API layer:

#### 1. `ListProductOrgTypeRules` — wrong response shape + wrong field name

**Problem:**  
The handler returned `new { items, totalCount }` but the TypeScript client called `Array.isArray(raw)` — so the entire response was silently discarded and the admin page showed "No product org-type rules found."  
Additionally the field was named `orgTypeCode` but the mapper expected `organizationTypeCode`.

**Fix:**  
- Return the list directly: `return Results.Ok(items)` (plain array).  
- Rename field to `organizationTypeCode`.  
- Also added `productRoleId`, `productRoleCode`, `productRoleName`, `organizationTypeName` to give the table richer context.

#### 2. `ListProductRelationshipTypeRules` — same shape bug + URL 404

**Problem:**  
Same `{ items, totalCount }` wrap bug. The control-center client called `/api/admin/product-rel-type-rules` but only the canonical `/api/admin/product-relationship-type-rules` was registered.

**Fix:**  
- Return plain array.  
- Register a second route alias: `routes.MapGet("/api/admin/product-rel-type-rules", ListProductRelationshipTypeRules)`.
- Added `relationshipTypeName` field.

#### 3. Legacy Coverage endpoint (new)

**Added:** `GET /api/admin/legacy-coverage`

Returns a point-in-time snapshot across two migration streams:

```jsonc
{
  "generatedAtUtc": "2026-03-30T00:00:00Z",
  "eligibilityRules": {
    "totalActiveProductRoles": 8,
    "withDbRuleOnly": 3,       // modern path — EligibleOrgType string removed
    "withBothPaths": 4,        // transitional — has both DB rule + legacy string
    "legacyStringOnly": 1,     // ← MUST reach 0 before Phase F
    "unrestricted": 0,
    "dbCoveragePct": 87.5,
    "uncoveredRoles": [
      { "code": "LEGACY_ADMIN", "eligibleOrgType": "INTERNAL" }
    ]
  },
  "roleAssignments": {
    "usersWithLegacyRoles": 42,
    "usersWithScopedRoles": 39,
    "dualWriteCoveragePct": 92.9
  }
}
```

**Query logic:**
- `withDbRuleOnly` = roles with ≥1 active `ProductOrganizationTypeRule` and `EligibleOrgType IS NULL`  
- `withBothPaths` = roles with ≥1 active rule AND `EligibleOrgType IS NOT NULL`  
- `legacyStringOnly` = roles where `EligibleOrgType IS NOT NULL` and no active rule exists  
- `dualWriteCoveragePct` = `DISTINCT UserId` in `ScopedRoleAssignments (ScopeType=GLOBAL, IsActive=true)` ÷ `DISTINCT UserId` in `UserRoles`

---

### TypeScript layer

| File | Change |
|---|---|
| `types/control-center.ts` | `ProductOrgTypeRule` — added `productRoleId`, `productRoleCode`, `productRoleName`, `organizationTypeName`; `ProductRelTypeRule` — added `relationshipTypeName`; new interfaces: `LegacyCoverageReport`, `EligibilityRulesCoverage`, `RoleAssignmentsCoverage`, `UncoveredRole` |
| `lib/api-client.ts` | `CACHE_TAGS.legacyCoverage = 'cc:legacy-coverage'` |
| `lib/api-mappers.ts` | `mapProductOrgTypeRule` — extended for new fields; `mapProductRelTypeRule` — added `relationshipTypeName`; new `mapLegacyCoverageReport` |
| `lib/control-center-api.ts` | Added `legacyCoverage.get()` namespace (TTL 10 s) |
| `lib/routes.ts` | `legacyCoverage: '/legacy-coverage'` |
| `lib/nav.ts` | "Legacy Coverage" entry under CONFIGURATION |

### UI changes

| File | Change |
|---|---|
| `components/platform/product-rules-panel.tsx` | Added "Product Role" column showing `productRoleCode` + `productRoleName` |
| `components/platform/legacy-coverage-card.tsx` | New: two-section card with progress bars, stat rows, uncovered roles detail |
| `app/legacy-coverage/page.tsx` | New: PlatformAdmin-only page; calls `legacyCoverage.get()`; renders card + contextual help banner |

---

## Migration Targets

| Metric | Target | Phase |
|---|---|---|
| `eligibilityRules.legacyStringOnly` | **0** | Before Phase F (remove EligibleOrgType column) |
| `roleAssignments.dualWriteCoveragePct` | **100%** | Before removing legacy UserRole write path |

---

## Build Status

| Project | Errors | Warnings |
|---|---|---|
| `Identity.Api` | 0 | 0 |
| `CareConnect.Api` | 0 | 1 (pre-existing CS0168) |
| `control-center` (tsc) | 0 | 0 |

---

## Next Steps (Step 5+)

1. **Phase F prep** — once `legacyStringOnly` reaches 0, generate a migration to drop the `EligibleOrgType` column from `ProductRoles`.
2. **UserRole deprecation** — once dual-write coverage is 100%, remove the `UserRole` write from `UserRepository.AssignRole` and rely solely on `ScopedRoleAssignment`.
3. **Direct ScopedRoleAssignment writes** — admin "Assign Role" mutation in the control center should write directly to `ScopedRoleAssignments`, no longer going through the legacy path.
4. **Revalidation action** — add a server action to call `revalidateTag('cc:legacy-coverage')` from the admin page so an operator can force a fresh read without waiting 10 s.
