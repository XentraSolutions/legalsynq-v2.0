# LS-LIENS-01-001-01 — Hardening Iteration Report

**Date:** 2026-04-13
**Service:** Liens Microservice (`apps/services/liens/`)
**Scope:** Architectural hardening — remove premature auth, validate domain boundaries

---

## 1. Summary of Corrections

Two corrections were applied to the Liens microservice scaffold:

1. **Removed premature JWT authentication configuration** from `Liens.Api/Program.cs` and removed the `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package from `Liens.Api.csproj`.
2. **Validated Domain → BuildingBlocks dependency** — determined it is acceptable (details in section 4).

No new business functionality was added. No gateway integration, identity integration, entities, repositories, or business routes were introduced.

---

## 2. Auth-Related Changes in Program.cs

### Removed

| Item | Details |
|------|---------|
| `using System.Security.Claims` | JWT claim type reference |
| `using System.Text` | Encoding for signing key |
| `using Microsoft.AspNetCore.Authentication.JwtBearer` | JWT Bearer namespace |
| `using Microsoft.IdentityModel.Tokens` | Token validation namespace |
| JWT signing key configuration | `builder.Configuration.GetSection("Jwt")` + `SigningKey` extraction with `throw` guard |
| `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` | Full JWT Bearer service registration with `TokenValidationParameters` |
| `AddAuthorization` with `"AuthenticatedUser"` policy | Authorization policy requiring authenticated user |
| `app.UseAuthentication()` | Authentication middleware |
| `app.UseAuthorization()` | Authorization middleware |
| `.AllowAnonymous()` on `/health` and `/info` | No longer needed since no auth policy exists |

### Also Removed from Liens.Api.csproj

| Package | Version |
|---------|---------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.8 |

### Also Removed from Configuration

| File | Section Removed |
|------|----------------|
| `appsettings.json` | `Jwt` block (Issuer, Audience, SigningKey placeholder) |
| `appsettings.Development.json` | `Jwt` block (Issuer, Audience, dev signing key) |

### Retained

- `Microsoft.EntityFrameworkCore.Design` (8.0.2) — needed for future migration tooling
- Project references to `Liens.Application`, `Liens.Infrastructure`, `Contracts`

---

## 3. Current Middleware/Pipeline State

After removing auth, the pipeline is:

```
WebApplication.CreateBuilder(args)
  → Logging (Console)
  → AddLiensServices (Infrastructure DI — currently registers HttpContextAccessor only)
  → Build

Middleware pipeline:
  1. ExceptionHandlingMiddleware (global error handling)
  2. /health endpoint (anonymous)
  3. /info endpoint (anonymous)
  4. MapLienEndpoints() (currently empty)
```

No authentication or authorization middleware is in the pipeline. The service starts without requiring any `Jwt:*` configuration values. Future identity integration can add auth back cleanly — the pipeline structure supports inserting `UseAuthentication()` and `UseAuthorization()` before endpoint mapping.

---

## 4. Domain → BuildingBlocks Dependency Validation

### Reference

`Liens.Domain.csproj` contains:
```xml
<ProjectReference Include="..\..\..\..\shared\building-blocks\BuildingBlocks\BuildingBlocks.csproj" />
```

### Current Usage

**None.** The `Liens.Domain` project contains zero `.cs` source files. The `Entities/` folder contains only a `.gitkeep` placeholder. No `using BuildingBlocks.*` statements exist anywhere in `Liens.Domain`.

### BuildingBlocks Contents Analysis

| Namespace | Contents | Domain-Safe? |
|-----------|----------|:---:|
| `BuildingBlocks.Domain` | `AuditableEntity` (base entity class) | Yes |
| `BuildingBlocks.Exceptions` | `ValidationException`, `NotFoundException`, `ConflictException`, `ForbiddenException` | Yes |
| `BuildingBlocks.Authorization` (constants) | `ProductCodes`, `Roles`, `PermissionCodes`, `OrgType` | Yes (string constants) |
| `BuildingBlocks.Context` | `ICurrentRequestContext`, `CurrentRequestContext` | Borderline — application layer |
| `BuildingBlocks.Authorization` (services) | `IPolicyEvaluationService`, `IPermissionService`, `AuthorizationService` | No — infrastructure |
| `BuildingBlocks.Authorization.Filters` | `RequireProductAccessFilter`, `RequirePermissionFilter`, `RequireOrgProductAccessFilter` | No — ASP.NET filters |

### BuildingBlocks Framework Reference

`BuildingBlocks.csproj` includes `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, which means any project referencing BuildingBlocks transitively has access to ASP.NET Core APIs. This is an architectural concern for the Domain layer.

### Assessment

**Acceptable with documented risk.**

- The reference exists as a forward-looking placeholder. When Liens entities are created, they will inherit from `BuildingBlocks.Domain.AuditableEntity` and use `BuildingBlocks.Exceptions` for domain-level error signaling.
- These are safe, domain-appropriate abstractions.
- The Domain layer does NOT currently import or use any HTTP, auth, persistence, or infrastructure concerns from BuildingBlocks.
- The transitive `Microsoft.AspNetCore.App` framework reference is a build-time concern but does not create runtime coupling unless Domain code explicitly uses ASP.NET types.
- A future architectural improvement would be to split BuildingBlocks into `BuildingBlocks.Domain` (no ASP.NET dependency) and `BuildingBlocks.Infrastructure` (with ASP.NET). This is out of scope for this iteration.

### Decision: KEEP the reference. Document the boundary constraint.

**Constraint for future development:** `Liens.Domain` must only consume from `BuildingBlocks.Domain` and `BuildingBlocks.Exceptions` namespaces. Any usage of `BuildingBlocks.Authorization.Filters`, `BuildingBlocks.Context`, or ASP.NET types in the Domain layer would be a boundary violation.

---

## 5. Startup Script Validation

### File: `scripts/run-dev.sh`

Liens was added to `run-dev.sh` in LS-LIENS-01-001 at line 33:
```bash
dotnet run --no-build --project "$ROOT/apps/services/liens/Liens.Api/Liens.Api.csproj" &
```

### Validation

- The full dev stack starts cleanly with all services including Liens.
- Liens does not interfere with any other service startup.
- No port conflicts (Liens uses port 5009, which is reserved for it).
- No configuration dependencies were broken by removing JWT config (the service no longer reads `Jwt:*` settings).

---

## 6. Build Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:22.68
```

All six projects in the dependency chain built cleanly:
- `Contracts`
- `BuildingBlocks`
- `Liens.Domain`
- `Liens.Application`
- `Liens.Infrastructure`
- `Liens.Api`

---

## 7. Run Result

Service starts successfully on port 5009. Log output:
```
Starting liens v1 in Production
```

No errors or warnings during startup. No JWT configuration required.

---

## 8. /health Test Result

```
GET http://localhost:5009/health

Response: {"status":"ok","service":"liens"}
HTTP Status: 200
```

---

## 9. /info Test Result

```
GET http://localhost:5009/info

Response: {"service":"liens","environment":"Production","version":"v1"}
HTTP Status: 200
```

---

## 10. Deviations, Assumptions, and Residual Risks

### Deviations from Spec: None

### Assumptions

1. The `HttpContextAccessor` registration in `DependencyInjection.cs` is retained. While it's infrastructure-adjacent, it's a standard .NET service registration that will be needed when identity/context integration is added. Removing it now and re-adding later adds unnecessary churn.
2. The `ExceptionHandlingMiddleware` in `Liens.Api` uses `BuildingBlocks.Exceptions` (specifically `ValidationException`, `NotFoundException`, `ConflictException`). This is the API layer using shared exceptions — not the Domain layer — so it does not violate the domain boundary constraint.

### Residual Risks

1. **BuildingBlocks transitive ASP.NET dependency**: As documented in section 4, `BuildingBlocks.csproj` pulls in the full ASP.NET framework reference. Future developers could inadvertently use ASP.NET types in `Liens.Domain` without a compile-time guard. A future `BuildingBlocks.Domain` package split would mitigate this.
2. **No integration tests**: The scaffold has no automated tests. /health and /info were validated manually. Automated endpoint tests should be added in a future iteration.

---

## 11. Final Readiness Statement

### Is LS-LIENS-01-001 now cleanly complete?

**Yes.** The two issues identified in the iteration review have been resolved:

1. JWT authentication has been fully removed from the service configuration, middleware pipeline, and package references. The service starts without any identity coupling.
2. The Domain → BuildingBlocks dependency has been validated as acceptable. The Domain layer contains no source code that uses BuildingBlocks yet, and the intended future usage (AuditableEntity, shared exceptions) is limited to safe domain abstractions.

### Is the service ready for LS-LIENS-01-002?

**Yes.** The Liens microservice scaffold is architecturally clean and minimal:

- Clean four-layer project structure (Api, Application, Domain, Infrastructure)
- No premature integrations (no auth, no gateway, no business entities)
- Builds with 0 warnings, 0 errors
- Runs successfully with /health and /info endpoints
- Integrated into dev startup script without conflicts
- Domain boundary is clean and documented

The service is properly staged for the next phase of development, which can include identity integration, gateway registration, or domain entity implementation as scoped by LS-LIENS-01-002.
