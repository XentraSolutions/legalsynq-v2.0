# Performance & Maintainability Hardening — Identity Service

**Branch:** `performance-and-maintainability-`  
**Date:** 2026-04-11  
**Scope:** Identity service (Clean Architecture layers) + Next.js BFF login route

---

## Summary

This PR applies the findings from the internal [performance & maintainability review](analysis/performance-maintainability-review-2026-04-11.md)
conducted on 2026-04-11. It resolves 2 blocking issues and 7 important issues with no feature or API contract changes.

---

## Changes

### Security / Blocking

#### PII masked in BFF login log — `apps/web`
- `login/route.ts`: The raw email address was logged to the console on every login attempt.
  A regex mask (`ab***@example.com`) is now applied before logging.

#### Business logic removed from endpoint layer — `Identity.Api`
- `AuthEndpoints.cs`: Four fat endpoint handlers (`accept-invite`, `change-password`,
  `password-reset/confirm`, `forgot-password`) each injected `IdentityDbContext` directly
  and embedded full business flows inline (hashing, status validation, audit emission).
  All four are now thin delegates that call `IAuthService`.
- Removed `using Identity.Infrastructure.Data` and `using Microsoft.EntityFrameworkCore`
  from `AuthEndpoints.cs` — the endpoint layer no longer touches the DB.

---

### Performance

#### Eliminated double DB load on login — `Identity.Application` / `Identity.Infrastructure`
- `LoginAsync` previously called `GetByTenantAndEmailAsync` then `GetByIdWithRolesAsync`
  (two round-trips, second one redundant).
- New method `GetByTenantAndEmailWithRolesAsync` loads the user and their active
  `ScopedRoleAssignments` in one tracked query.

#### Eliminated redundant DB load in `ProductRoleResolutionService`
- `ResolveAsync` previously called `GetByIdWithRolesAsync` even when the login path
  had already loaded the same data.
- Added a second `ResolveAsync` overload that accepts
  `IReadOnlyList<ScopedRoleAssignment> preloadedScopedAssignments`. The login path
  now passes its pre-loaded assignments through, saving a full user+roles round-trip
  on every login.

#### Lighter org fetch on login path — `Identity.Infrastructure`
- `LoginAsync` previously called `GetPrimaryOrgMembershipAsync`, which eager-loads
  products, roles, and org-type eligibility rules (6+ JOINs) — data only needed by
  `AdminEndpoints`.
- New method `GetPrimaryOrganizationForLoginAsync` loads only the `Organization` and
  its `OrganizationTypeRef` (2 JOINs), which is all the login/JWT path needs.

#### N+1 slug-uniqueness queries eliminated — `TenantProvisioningService`
- `ResolveUniqueSlugAsync` looped up to 98 × `AnyAsync(...)` calls.
- Replaced with a single `WHERE Subdomain IN (candidates)` query + in-process
  `HashSet` lookup. Worst-case cost reduced from 98 round-trips to 1.

#### `SigningCredentials` cached in constructor — `JwtTokenService`
- `GenerateToken` previously re-read `IConfiguration` and allocated a new
  `SymmetricSecurityKey` + `SigningCredentials` on every call.
- All values now captured as `readonly` fields in the constructor.

---

### Maintainability

#### `ProductCodeMap` — single source of truth — `Identity.Application`
- `AdminEndpoints` and `AuthService` each maintained their own `Dictionary<string,string>`
  for DB↔frontend product code translation with inconsistent entry sets.
- New `ProductCodeMap.cs` (Application layer) holds both directions as
  `IReadOnlyDictionary<string, string>`. All consumers now reference it.

#### `Program.cs` — startup blocks extracted — `Identity.Api`
- `Program.cs` was ~420 lines with 4 large `try/catch` startup blocks inline.
- Extracted to `StartupExtensions.cs` as 4 extension methods:
  `RunDevMigrations`, `RunPhaseGDiagnosticsAsync`, `SeedProductRolesAsync`, `RunDevFixupsAsync`.
- `Program.cs` is now ~70 lines.

#### `AsNoTracking` applied to all read-only queries — `UserRepository`
- `GetByIdWithRolesAsync`, `GetAllWithRolesAsync`, `GetByTenantWithRolesAsync`,
  `GetPrimaryOrgMembershipAsync`, `GetActiveMembershipsWithProductsAsync` all
  return read-only result sets but were previously tracked by EF Core.
  `.AsNoTracking()` added to eliminate unnecessary change-tracker overhead.

---

## Files Changed

| File | Type | Change |
|---|---|---|
| `apps/web/src/app/api/auth/login/route.ts` | Modified | Mask email before logging |
| `Identity.Application/IUserRepository.cs` | Modified | +6 method signatures |
| `Identity.Application/Interfaces/IAuthService.cs` | Modified | +4 method signatures |
| `Identity.Application/Interfaces/IProductRoleResolutionService.cs` | Modified | +1 overload |
| `Identity.Application/Services/AuthService.cs` | Modified | Implement 4 new methods; optimised login path |
| `Identity.Application/ProductCodeMap.cs` | **New** | Single source of truth for product code maps |
| `Identity.Infrastructure/Repositories/UserRepository.cs` | Modified | AsNoTracking + 6 new methods |
| `Identity.Infrastructure/Services/JwtTokenService.cs` | Modified | Cache credentials in constructor |
| `Identity.Infrastructure/Services/TenantProvisioningService.cs` | Modified | Bulk slug query |
| `Identity.Infrastructure/Services/ProductRoleResolutionService.cs` | Modified | Overload + eliminate redundant load |
| `Identity.Api/Endpoints/AuthEndpoints.cs` | Modified | 4 thin delegates; remove DB usings |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Modified | Delegate to `ProductCodeMap` |
| `Identity.Api/Program.cs` | Modified | 420 → 70 lines |
| `Identity.Api/StartupExtensions.cs` | **New** | 4 startup extension methods |

---

## Testing

- `dotnet build LegalSynq.sln --configuration Release` — **0 errors, 0 warnings** ✓
- All 17 projects in the solution build clean
- No API contract changes; no migrations required

---

## Known Issues / Follow-ups

The code review identified the following items that are **not** addressed in this PR:

| # | Location | Issue |
|---|---|---|
| 1 | `AuthEndpoints.cs` | `X-Forwarded-For` header not forwarded to `ChangePasswordAsync` — audit log records proxy IP instead of real client IP in load-balanced environments |
| 2 | `AuthEndpoints.cs` | `ex.Message` passed directly to `Results.Problem(...)` on login, change-password, and password-reset — leaks internal error messages to API clients |
| 3 | `JwtTokenService.cs` | `new JwtSecurityTokenHandler()` allocated on every `GenerateToken` call — should be a `static readonly` field |
| 4 | `StartupExtensions.cs` | `RunPhaseGDiagnosticsAsync` and its full org table load runs on every production restart — should be dev-only or rewritten as aggregate queries |
