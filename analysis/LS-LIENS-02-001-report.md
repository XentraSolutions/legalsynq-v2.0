# LS-LIENS-02-001 — Liens Service v2 Identity / Auth Integration Report

**Date:** 2026-04-13
**Epic:** Liens Microservice Foundation
**Feature:** Identity Context Integration (Auth/JWT)
**Status:** ✅ Complete — All validations passed

---

## 1. Summary

The Liens microservice has been integrated with the v2 JWT authentication and identity pattern used across the LegalSynq platform. The service now:

- Validates JWT Bearer tokens issued by the v2 Identity service
- Resolves the full authenticated request context via the shared `ICurrentRequestContext` abstraction
- Exposes a protected `/context` diagnostic endpoint proving end-to-end identity flow
- Preserves anonymous access to `/health` and `/info`
- Is protected at the gateway level (all non-health/info routes require authentication)

No business logic, permissions, or lien-specific workflows were introduced. This feature is purely foundational.

---

## 2. Existing v2 Auth/Identity Pattern Identified

### Reference Services
- **Fund** (`apps/services/fund/`) — primary reference (most mature v2 service)
- **CareConnect** (`apps/services/careconnect/`) — secondary reference

### Pattern Summary

All v2 services follow a consistent auth integration model:

| Concern | Implementation |
|---|---|
| Token format | JWT Bearer (HS256 symmetric) |
| Issuer | `legalsynq-identity` |
| Audience | `legalsynq-platform` |
| Claim mapping | `MapInboundClaims = false` (preserves original claim names) |
| Role claim type | `System.Security.Claims.ClaimTypes.Role` |
| Clock skew | `TimeSpan.Zero` (strict lifetime validation) |
| Signing key | Per-environment config (`Jwt:SigningKey`) |
| Request context | `ICurrentRequestContext` / `CurrentRequestContext` (shared BuildingBlocks) |
| DI registration | `AddHttpContextAccessor()` + `AddScoped<ICurrentRequestContext, CurrentRequestContext>()` in Infrastructure layer |
| Authorization policies | `AuthenticatedUser`, `AdminOnly`, `PlatformOrTenantAdmin` via `BuildingBlocks.Authorization` |
| Gateway protection | Non-health/info routes inherit `RequireAuthorization()` from gateway's `app.MapReverseProxy().RequireAuthorization()` |

### Shared BuildingBlocks Used

| Namespace | Purpose |
|---|---|
| `BuildingBlocks.Authorization` | `Policies`, `Roles` constants |
| `BuildingBlocks.Context` | `ICurrentRequestContext`, `CurrentRequestContext` |

---

## 3. Files Changed

| File | Change |
|---|---|
| `apps/services/liens/Liens.Api/Program.cs` | JWT Bearer auth setup, authorization policies, `/context` endpoint |
| `apps/services/liens/Liens.Infrastructure/DependencyInjection.cs` | `IHttpContextAccessor` + `ICurrentRequestContext` registration |
| `apps/services/liens/Liens.Api/Liens.Api.csproj` | Added `Microsoft.AspNetCore.Authentication.JwtBearer` package reference |
| `apps/services/liens/Liens.Infrastructure/Liens.Infrastructure.csproj` | Added `BuildingBlocks` project reference |
| `apps/services/liens/Liens.Api/appsettings.json` | Added `Jwt` section (issuer, audience, placeholder signing key) |
| `apps/services/liens/Liens.Api/appsettings.Development.json` | Dev JWT config with shared dev signing key |
| `apps/services/liens/Liens.Api/Properties/launchSettings.json` | Created — sets `ASPNETCORE_ENVIRONMENT=Development`, port 5009 |
| `apps/gateway/Gateway.Api/appsettings.json` | `liens-protected` route: removed `AuthorizationPolicy: "Anonymous"` |

---

## 4. Auth Setup Added to Liens

### Program.cs — JWT Bearer Configuration

```csharp
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidAudience            = jwtSection["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RoleClaimType            = ClaimTypes.Role,
            ClockSkew                = TimeSpan.Zero
        };
    });
```

### Authorization Policies

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());
    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));
    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));
});
```

### Middleware Pipeline

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

This is identical to the Fund service setup. Product-specific role policies (e.g., `CanReferFund`) are intentionally omitted — they will be added when Liens business features are built.

---

## 5. Current Request Context Abstraction

### Shared Abstraction: `ICurrentRequestContext`

Located in `shared/building-blocks/BuildingBlocks/Context/`. This is the same abstraction used by Fund, CareConnect, and all v2 services. No Liens-specific adapter was needed.

### Registration (Liens.Infrastructure)

```csharp
services.AddHttpContextAccessor();
services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();
```

### How It Works

`CurrentRequestContext` reads claims from `IHttpContextAccessor.HttpContext.User` (the `ClaimsPrincipal` populated by the JWT Bearer middleware). It provides type-safe access to all platform identity fields without requiring services to parse raw claims.

---

## 6. Identity/Context Fields Now Resolvable in Liens

| Property | Claim Source | Type | Description |
|---|---|---|---|
| `IsAuthenticated` | `Identity.IsAuthenticated` | `bool` | Whether the request carries a valid JWT |
| `UserId` | `sub` | `Guid?` | Unique user identifier |
| `TenantId` | `tenant_id` | `Guid?` | Tenant identifier |
| `TenantCode` | `tenant_code` | `string?` | Tenant short code (e.g., `LEGALSYNQ`) |
| `Email` | `email` | `string?` | User email |
| `OrgId` | `org_id` | `Guid?` | Organization identifier |
| `OrgType` | `org_type` | `string?` | Organization type name (e.g., `INTERNAL`, `LawFirm`) |
| `OrgTypeId` | `org_type_id` | `Guid?` | Organization type catalog GUID |
| `Roles` | `ClaimTypes.Role` | `IReadOnlyCollection<string>` | System/tenant roles (e.g., `PlatformAdmin`) |
| `ProductRoles` | `product_roles` | `IReadOnlyCollection<string>` | Product-scoped roles in `PRODUCT:ROLE` format |
| `IsPlatformAdmin` | derived from Roles | `bool` | Convenience flag |

### Usage in Future Liens Code

Any service, handler, or endpoint can inject `ICurrentRequestContext` via constructor injection or minimal API parameter binding:

```csharp
app.MapGet("/example", (ICurrentRequestContext ctx) =>
{
    var tenantId = ctx.TenantId;
    var userId = ctx.UserId;
    // ...
}).RequireAuthorization(Policies.AuthenticatedUser);
```

---

## 7. Validation Endpoint Details

### Route: `GET /context`

- **Authorization:** `RequireAuthorization(Policies.AuthenticatedUser)`
- **Handler:** Injects `ICurrentRequestContext`, returns 401 if not authenticated, otherwise returns resolved context as JSON
- **Purpose:** Diagnostic proof that identity context flows correctly — not intended for production client use

### Response Shape (authenticated)

```json
{
  "authenticated": true,
  "userId": "7b657820-708a-4863-b18f-22ba7b15c6c3",
  "tenantId": "20000000-0000-0000-0000-000000000001",
  "tenantCode": "LEGALSYNQ",
  "email": "admin@legalsynq.com",
  "orgId": "40000000-0000-0000-0000-000000000001",
  "orgType": "INTERNAL",
  "orgTypeId": "70000000-0000-0000-0000-000000000001",
  "roles": ["PlatformAdmin"],
  "productRoles": [],
  "isPlatformAdmin": true
}
```

### Security Notes

- No secrets, tokens, or signing keys are exposed
- No internal service URLs or infrastructure details are returned
- The endpoint only reveals the claims the caller already possesses (their own identity)

---

## 8. Build Results

### Liens Service

```
dotnet build apps/services/liens/Liens.Api/Liens.Api.csproj --configuration Debug
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Gateway

```
dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --configuration Debug
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 9. Run Results

Both services start successfully. The Liens service reports:

```
Starting liens v1 in Development
```

The Development environment is correctly activated via `Properties/launchSettings.json`, which ensures `appsettings.Development.json` (containing the dev signing key) is loaded.

### Port Assignments (unchanged)

| Service | Port |
|---|---|
| Liens | 5009 |
| Gateway | 5010 |
| Identity | 5001 |

---

## 10. Test Results

### Direct Service Tests

| # | Test | Method | Expected | Actual | Status |
|---|---|---|---|---|---|
| 1 | Anonymous `/health` | `GET http://localhost:5009/health` | 200 + `{"status":"ok","service":"liens"}` | 200 + `{"status":"ok","service":"liens"}` | ✅ Pass |
| 2 | Anonymous `/info` | `GET http://localhost:5009/info` | 200 + environment/version | 200 + `{"service":"liens","environment":"Development","version":"v1"}` | ✅ Pass |
| 3 | Unauthenticated `/context` | `GET http://localhost:5009/context` (no token) | 401 | 401 | ✅ Pass |
| 4 | Authenticated `/context` | `GET http://localhost:5009/context` (valid JWT) | 200 + identity claims | 200 + full identity payload (see §7) | ✅ Pass |

### Gateway Tests

| # | Test | Method | Expected | Actual | Status |
|---|---|---|---|---|---|
| 5 | Gateway anonymous `/liens/health` | `GET http://localhost:5010/liens/health` | 200 | 200 + `{"status":"ok","service":"liens"}` | ✅ Pass |
| 6 | Gateway unauthenticated `/liens/context` | `GET http://localhost:5010/liens/context` (no token) | 401 | 401 | ✅ Pass |
| 7 | Gateway authenticated `/liens/context` | `GET http://localhost:5010/liens/context` (valid JWT) | 200 + identity claims | 200 + full identity payload | ✅ Pass |

---

## 11. Dev Token / Testing Setup

### How a Valid JWT Was Obtained

1. Login via the platform BFF: `POST http://localhost:5000/api/auth/login` with valid credentials and `X-Tenant-Code` header
2. The BFF returns a `Set-Cookie: platform_session=<JWT>` cookie
3. The JWT value was extracted from the cookie and sent as `Authorization: Bearer <JWT>` to the Liens service
4. The token is issued by the v2 Identity service with issuer `legalsynq-identity` and audience `legalsynq-platform`

### Dev Signing Key

All v2 services (Identity, Fund, CareConnect, Liens) share the same development signing key in their `appsettings.Development.json`:

```
dev-only-signing-key-minimum-32-chars-long!
```

This key is only used when `ASPNETCORE_ENVIRONMENT=Development`. Production environments must override `Jwt:SigningKey` via secrets/environment variables.

---

## 12. v1 Auth/Session Confirmation

**No v1 auth or session logic was introduced.**

- No v1 session tokens, session stores, or session middleware
- No v1 API endpoints or compatibility shims
- No code copied from v1 services
- The integration uses exclusively v2 patterns: JWT Bearer + `ICurrentRequestContext` from `BuildingBlocks`

---

## 13. Deviations, Assumptions, and Residual Risks

### Deviations from Fund

1. **No product-specific authorization policies** — Fund defines `CanReferFund` and `CanFundApplications`. Liens does not yet define product-specific policies because no Liens business features exist yet. These will be added as Liens features are built.

### Assumptions

1. The shared dev signing key is acceptable for local development (consistent with all other v2 services)
2. The `Jwt:SigningKey` placeholder in `appsettings.json` will be overridden by deployment secrets in production
3. Gateway route ordering (60/61 for anonymous health/info, 160 for catch-all protected) is sufficient — matches the pattern used by other services

### Residual Risks

1. **Placeholder signing key in production config** — `appsettings.json` contains `REPLACE_VIA_SECRET_minimum_32_characters_long`. If not overridden in production, JWT tokens become forgeable. This is a platform-wide pattern (Fund, CareConnect have the same), not Liens-specific. Recommendation: add startup validation to fail fast if the placeholder key is detected in non-Development environments.

2. **`/context` endpoint in production** — The diagnostic endpoint exposes identity claims. While it only reveals information the caller already possesses (their own JWT), it could be restricted or removed before production deployment if desired.

---

## 14. Final Readiness Statement

### Is Liens now identity-context aware?

**Yes.** The Liens service correctly validates JWT tokens issued by the v2 Identity service and resolves the full platform request context (user, tenant, organization, roles, product roles) via the shared `ICurrentRequestContext` abstraction.

### Is the service ready for the next feature?

**Yes.** Future Liens features can:

1. Inject `ICurrentRequestContext` to access the authenticated user's identity
2. Use `ctx.TenantId`, `ctx.OrgId`, `ctx.UserId`, etc. for data scoping and authorization
3. Apply `RequireAuthorization(Policies.AuthenticatedUser)` or custom product-role policies to any new endpoint
4. Add Liens-specific authorization policies (e.g., `CanManageLiens`) following the Fund pattern

The identity integration is complete, clean, and aligned with the v2 platform standard.
