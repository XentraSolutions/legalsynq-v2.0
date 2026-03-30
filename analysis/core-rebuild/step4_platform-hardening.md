# Step 4 — Platform Hardening

**Status:** Complete  
**Builds:** Identity 0 errors/0 warnings · CareConnect 0 errors · TypeScript `tsc --noEmit` clean

---

## 1  Resolver Auth Header Support

**Files changed:**
- `CareConnect.Infrastructure/Services/IdentityServiceOptions.cs`
- `CareConnect.Infrastructure/Services/HttpOrganizationRelationshipResolver.cs`
- `CareConnect.Api/appsettings.json` / `appsettings.Development.json`

### What changed

`IdentityServiceOptions` now carries two optional fields:

```csharp
public string? AuthHeaderName  { get; set; }   // e.g. "X-Service-Token"
public string? AuthHeaderValue { get; set; }   // injected via env/secret
```

`HttpOrganizationRelationshipResolver`:

- **Construction-time disabled check** — `_isEnabled` is evaluated once in the constructor, not on every `FindActiveRelationshipAsync` call.  A `LogWarning` is emitted **once at startup** when `BaseUrl` is missing (was Debug per-request before).
- **Auth header injection** — when both `AuthHeaderName` and `AuthHeaderValue` are non-empty, the header is applied via `TryAddWithoutValidation` before each request.
- Timeout log level elevated from Debug → Warning for observability parity.

### Config

```json
"IdentityService": {
  "BaseUrl":         "http://identity:5001",
  "TimeoutSeconds":  5,
  "AuthHeaderName":  "X-Service-Token",
  "AuthHeaderValue": ""   // set via IDENTITYSERVICE__AUTHHEADERVALUE env var
}
```

---

## 2  AuthService Eligibility Observability

**File changed:** `Identity.Application/Services/AuthService.cs`

### What changed

- `ILogger<AuthService>` injected via constructor.
- Static `IsEligible(pr, org) → bool` replaced by `IsEligibleWithPath(pr, org) → (bool, EligibilityPath)`.
- `EligibilityPath` enum: `DbRule | LegacyString | Unrestricted`.
- `LoginAsync` aggregates per-path counts and emits:
  - `LogDebug` — total resolved product roles + per-path breakdown.
  - `LogInformation` — **only when `LegacyString > 0`**, so the legacy fallback is surfaced without noise on fully-migrated tenants.

### Log output sample (dev)

```
[DBG] Product role eligibility resolved for user=... org=...: 2 role(s) — DB-rule=2, legacy-string=0, unrestricted=0.
```
```
[INF] Legacy EligibleOrgType fallback used for 1 product role(s) during login for user=... orgType=LAW_FIRM. Seed ProductOrganizationTypeRule rows to remove this fallback.
```

---

## 3  ProviderService / FacilityService — Org Linkage Logging

**Files changed:**
- `CareConnect.Application/Services/ProviderService.cs`
- `CareConnect.Application/Services/FacilityService.cs`

Both services now take `ILogger<T>` (injected via DI, no registration changes needed) and emit `LogDebug` when `LinkOrganization()` is called on create or update.

### Also: ProviderResponse.OrganizationId

`ProviderResponse` now exposes `Guid? OrganizationId` populated from `Provider.OrganizationId` in `ToResponse()`. This field was previously accepted on create/update requests but was silently dropped in the response — it is now surfaced to API callers.

---

## 4  UserRepository — Dual-Write ScopedRoleAssignment

**File changed:** `Identity.Infrastructure/Repositories/UserRepository.cs`

### What changed

`AddAsync` now writes a `ScopedRoleAssignment` record alongside each `UserRole` record:

```csharp
var scoped = ScopedRoleAssignment.Create(
    userId:    user.Id,
    roleId:    roleId,
    scopeType: ScopedRoleAssignment.ScopeTypes.Global);

await _db.ScopedRoleAssignments.AddAsync(scoped, ct);
```

**Legacy `UserRole` records are preserved** — no read-path changes. Both tables are kept in sync from the moment of user creation. A future Phase F migration will backfill historical users and then cut over read paths.

### Why GLOBAL scope

All roles assigned at user-creation time are platform-wide role grants (not scoped to a specific org, product, or relationship). `ScopeTypes.Global` is the correct scope discriminator. Org/Product/Relationship scopes will be created separately via dedicated APIs.

---

## 5  EligibleOrgType Coverage Startup Diagnostic

**File changed:** `Identity.Api/Program.cs`

A startup block (runs in all environments) queries `ProductRoles` for any active role that has `EligibleOrgType` set but no active `ProductOrganizationTypeRule` row:

```
[INF] EligibleOrgType coverage check passed — all active ProductRoles with EligibleOrgType have matching ProductOrganizationTypeRule rows.
```

If a gap is found:

```
[WRN] ProductRole 'MY_ROLE' has EligibleOrgType='LAW_FIRM' but no active ProductOrganizationTypeRule row.
```

**Current state:** All 7 seeded ProductRoles with `EligibleOrgType` set have matching `OrgTypeRules` rows — check passes green on every startup.

---

## 6  Control-Center Admin Pages — ORGANIZATION GRAPH

### Routes added (`lib/routes.ts`)

| Route key            | Path                    | Purpose                         |
|----------------------|-------------------------|---------------------------------|
| `orgTypes`           | `/org-types`            | Org type catalog                |
| `relationshipTypes`  | `/relationship-types`   | Relationship type catalog       |
| `orgRelationships`   | `/org-relationships`    | Live org relationship graph     |
| `productRules`       | `/product-rules`        | Product org-type + rel-type rules |

### Nav section added (`lib/nav.ts`)

```
ORGANIZATION GRAPH
  ├── Org Types          /org-types
  ├── Relationship Types /relationship-types
  ├── Org Relationships  /org-relationships
  └── Product Rules      /product-rules
```

### Pages

| Page                                   | API                                    | Cache |
|----------------------------------------|----------------------------------------|-------|
| `/org-types`                           | `organizationTypes.list()`             | 300 s |
| `/relationship-types`                  | `relationshipTypes.list()`             | 300 s |
| `/org-relationships`                   | `organizationRelationships.list(...)`  | 60 s  |
| `/product-rules`                       | `productOrgTypeRules.list()` + `productRelTypeRules.list()` (parallel) | 300 s |

### Components

| Component                                         | Exported                                          |
|---------------------------------------------------|---------------------------------------------------|
| `components/platform/org-type-table.tsx`          | `OrgTypeTable`                                    |
| `components/platform/relationship-type-table.tsx` | `RelationshipTypeTable`                           |
| `components/platform/org-relationship-table.tsx`  | `OrgRelationshipTable` (with pagination)          |
| `components/platform/product-rules-panel.tsx`     | `ProductOrgTypeRuleTable`, `ProductRelTypeRuleTable` |

All pages:
- Call `requirePlatformAdmin()` — PlatformAdmin gate enforced.
- Wrap in `CCShell` with the user's email.
- Show an error banner on fetch failure; never throw to the Next.js error boundary.
- Use `controlCenterServerApi.*` — auto-wired when Identity admin endpoints are live; graceful degradation while endpoints are stubbed.

---

## Remaining legacy paths (Phase F)

| Location | Legacy path | Retirement condition |
|---|---|---|
| `AuthService.IsEligibleWithPath` | `EligibilityPath.LegacyString` fallback | All ProductRoles seeded with OrgTypeRules (already done for 7/7 seeded roles) |
| `UserRepository.AddAsync` | `UserRoles` write | Backfill migration run + read paths switched to `ScopedRoleAssignment` |
| `User.UserRoles` nav prop | `GetByIdWithRolesAsync` LINQ | Read path migration |
| `OrgType` string claim in JWT | `org_type` claim | `OrganizationTypeId` backfill + JWT v2 claim set |
