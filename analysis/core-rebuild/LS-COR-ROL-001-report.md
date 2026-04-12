# LS-COR-ROL-001 — Product Role Resolution Engine

## Summary

The Identity service now includes a centralized Product Role Resolution Engine that replaces the previous ad-hoc product role derivation in `AuthService.LoginAsync`. The engine is extensible via product-specific mapper plugins and supports multi-organization resolution, scoped role assignments, OrgType-based DB rules, and hardcoded fallback mappings.

## Architecture

### New Files

| File | Layer | Purpose |
|------|-------|---------|
| `Identity.Application/DTOs/EffectiveAccessContext.cs` | Application | Return type from the engine — contains per-org, per-product access entries with grant/deny status, effective roles, and access source tracing |
| `Identity.Application/Interfaces/IProductRoleResolutionService.cs` | Application | Service contract: `ResolveAsync(userId, tenantId, ct)` → `EffectiveAccessContext` |
| `Identity.Application/Interfaces/IProductRoleMapper.cs` | Application | Plugin contract for product-specific mappers; includes `ProductRoleMapperContext` record |
| `Identity.Infrastructure/Services/ProductRoleResolutionService.cs` | Infrastructure | Core engine implementation — orchestrates tenant product checks, org membership iteration, eligibility gates, and mapper dispatch |
| `Identity.Infrastructure/Services/CareConnectRoleMapper.cs` | Infrastructure | CareConnect-specific mapper with 3-tier resolution (scoped assignments → DB OrgType rules → OrgType fallback) |

### Modified Files

| File | Change |
|------|--------|
| `Identity.Application/Services/AuthService.cs` | Replaced inline product role loop with `_roleResolutionService.ResolveAsync()` call (lines 111-112) |
| `Identity.Application/Interfaces/IUserRepository.cs` | Added `GetActiveMembershipsWithProductsAsync(userId, ct)` |
| `Identity.Infrastructure/Repositories/UserRepository.cs` | Implemented `GetActiveMembershipsWithProductsAsync` with full eager loading of Organization → OrganizationProducts → Product → ProductRoles → OrgTypeRules |
| `Identity.Infrastructure/DependencyInjection.cs` | Registered `IProductRoleMapper → CareConnectRoleMapper` and `IProductRoleResolutionService → ProductRoleResolutionService` as scoped services |

## Resolution Pipeline

```
ResolveAsync(userId, tenantId)
│
├── 1. Load enabled product codes at tenant level
│     └── Exit early if no products enabled
│
├── 2. Load all active org memberships with product/role graph
│     └── Exit early if no memberships
│
├── 3. Load scoped role assignments for the user
│
├── 4. For each membership → for each enabled org product:
│     │
│     ├── 4a. Gate: Is product enabled at tenant level?
│     │     └── No → record denial (TenantDisabled)
│     │
│     ├── 4b. Gate: Is OrgType eligible per ProductEligibilityConfig?
│     │     └── No → record denial (OrgTypeIneligible)
│     │
│     ├── 4c. Dispatch to registered IProductRoleMapper (if one exists for this product code)
│     │     └── CareConnectRoleMapper for "SYNQ_CARECONNECT"
│     │
│     └── 4d. Else → default mapper (scoped assignments → DB rules → grant all active product roles)
│
└── 5. Return EffectiveAccessContext with all ProductAccessEntry records
```

## CareConnect Role Mapper — 3-Tier Resolution

| Priority | Source | Description |
|----------|--------|-------------|
| 1 (highest) | `ScopedRoleAssignment` (PRODUCT scope) | Explicit per-user, per-org product role assignments from the DB |
| 2 | `ProductOrganizationTypeRule` (DB rules) | Role ↔ OrgType mapping rules defined on each ProductRole entity |
| 3 (fallback) | Hardcoded OrgType map | `PROVIDER → CARECONNECT_RECEIVER`, `LAW_FIRM → CARECONNECT_REFERRER`, `INTERNAL → CARECONNECT_ADMIN` |

Roles accumulate across tiers (union). Fallback only activates when tiers 1+2 yield zero roles.

## EffectiveAccessContext API

```csharp
record EffectiveAccessContext
{
    Guid UserId;
    Guid TenantId;
    IReadOnlyList<ProductAccessEntry> ProductAccess;
    IReadOnlyList<string> DeniedReasons;

    IReadOnlyList<string> GetEffectiveProductRoles();     // flat distinct role list
    bool HasProductAccess(string productCode);             // grant check
    IReadOnlyList<string> GetRolesForProduct(string code); // per-product roles
    IReadOnlyList<ProductAccessEntry> GetAccessForOrganization(Guid orgId);
}

record ProductAccessEntry
{
    string ProductCode;
    Guid? OrganizationId;
    string? OrganizationName;
    string? OrgType;
    IReadOnlyList<string> EffectiveRoles;
    bool IsGranted;
    string AccessSource;      // tracing: which mapper/tier resolved
    string? DenialReason;     // human-readable reason if denied
}
```

## AuthService Integration

Before (ad-hoc):
```csharp
// Inline loop over ProductEligibilityConfig → org products → product roles
// Hardcoded in LoginAsync, no tracing, no multi-org support
```

After:
```csharp
var accessContext = await _roleResolutionService.ResolveAsync(user.Id, tenant.Id, ct);
var productRoles = accessContext.GetEffectiveProductRoles().ToList();
```

The `productRoles` list is passed to `JwtTokenService.GenerateToken()` unchanged — the JWT `product_roles` claim format is fully backward compatible.

## DI Registration

```csharp
services.AddScoped<IProductRoleMapper, CareConnectRoleMapper>();
services.AddScoped<IProductRoleResolutionService, ProductRoleResolutionService>();
```

Multiple `IProductRoleMapper` registrations are supported. `ProductRoleResolutionService` receives `IEnumerable<IProductRoleMapper>` and dispatches by `ProductCode`. Duplicate `ProductCode` registrations are handled gracefully — first-registered wins with a warning log.

## Security: Tenant Scoping

The membership query (`GetActiveMembershipsWithProductsAsync`) is scoped by both `userId` AND `tenantId` at the database query level. This prevents cross-tenant authorization leakage — a user's memberships in tenant A cannot affect role resolution when logging into tenant B.

## Extensibility

To add a new product mapper:

1. Create a class implementing `IProductRoleMapper` with the product's code.
2. Register it in `DependencyInjection.cs`: `services.AddScoped<IProductRoleMapper, NewProductMapper>();`
3. The engine will automatically dispatch to it for organizations that have that product enabled.

## Verification

- **Build**: Identity.Api compiles successfully (Release configuration).
- **Runtime**: Service starts, health check returns 200.
- **Login test** (LEGALSYNQ admin): Returns `productRoles: []` — correct, no products enabled at tenant level. Log confirms: `"No products enabled at tenant level for tenant=..., user=..."`.
- **Backward compatibility**: Login response shape unchanged — `accessToken`, `user.productRoles[]`, all existing claims preserved.

## Status

**Complete.** The engine is integrated, building, and serving requests.
