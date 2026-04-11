# Performance & Maintainability Review — Identity Service + Web BFF
**Date:** 2026-04-11
**Reviewer:** GitHub Copilot (code-review-excellence skill)
**Scope:** `apps/services/identity/` · `apps/web/src/` (BFF + session layer)
**Methodology:** C# Review Guide · Performance Review Guide

---

## Summary Table

| Severity | # | Issue |
|---|---|---|
| 🔴 Blocking | 2 | PII in BFF logs; business logic in endpoint layer |
| 🟡 Important | 7 | Double DB loads on login; N+1 slug queries; over-fetching includes; `SigningCredentials` allocation; missing `AsNoTracking`; `Program.cs` god object; duplicated product-code map |
| 🟢 Nit | 4 | `VerificationRetryBackgroundService` misleading option name; unbounded list queries; service classes not `sealed`; untyped `res.json()` in session provider |

---

## 🔴 BLOCKING

### 1. PII Leaked to Logs in BFF Login Route

**File:** `apps/web/src/app/api/auth/login/route.ts` · L45

```ts
// ❌ Current — writes full email address to stdout
console.log(`[login] host=${rawHost}, subdomainTenant=..., email=${email}`);
```

User email addresses are written to application logs in plaintext on every login attempt. This is a HIPAA/GDPR compliance issue; log aggregation systems (CloudWatch, Datadog, etc.) will index and retain PII indefinitely.

**Fix — mask before logging:**
```ts
const maskedEmail = email.replace(/(?<=.{2}).+(?=@)/, '***');
console.log(`[login] host=${rawHost}, tenant=${tenantCode}, email=${maskedEmail}`);
```

---

### 2. Business Logic Bypasses the Application/Service Layer

**Files:**
- `apps/services/identity/Identity.Api/Endpoints/AuthEndpoints.cs` — `accept-invite` and `change-password` endpoints inject `IdentityDbContext` directly

The API layer directly queries EF Core, hashes passwords, mutates domain entities, and calls `db.SaveChangesAsync()`. This violates Clean Architecture (Presentation → Application → Domain ← Infrastructure). Consequences:

- The logic cannot be unit-tested without a real (or mocked) `DbContext`.
- Any future cross-cutting concerns (e.g., audit trail, caching, concurrency guards) must be copy-pasted into each endpoint.
- `IPasswordHasher`, `IAuthService`, and the audit client are partially duplicated between service and endpoint layers.

**Fix:** Move `accept-invite` and `change-password` logic into `IAuthService` / `AuthService`, injecting only the service interface in the endpoint.

---

## 🟡 IMPORTANT

### 3. Double User Load on Every Login

**File:** `apps/services/identity/Identity.Application/Services/AuthService.cs`

```csharp
// Round-trip 1 — lightweight load to check IsActive / IsLocked
var user = await _userRepository.GetByTenantAndEmailAsync(tenant.Id, normalizedEmail, ct);

// ... validation ...

// Round-trip 2 — full load with ScopedRoleAssignments
var userWithRoles = await _userRepository.GetByIdWithRolesAsync(user.Id, ct);
```

Two separate DB queries are issued for the same entity on every login path. The first load is redundant if the second query includes the same scalar columns.

**Fix:** Add `GetByTenantAndEmailWithRolesAsync` (or teach the existing method to return the full graph) so login costs one round-trip:

```csharp
var userWithRoles = await _userRepository.GetByTenantAndEmailWithRolesAsync(
    tenant.Id, normalizedEmail, ct);
if (userWithRoles is null || !userWithRoles.IsActive) { ... }
```

---

### 4. Sequential Slug-Uniqueness Queries (N+1 Pattern)

**File:** `apps/services/identity/Identity.Infrastructure/Services/TenantProvisioningService.cs`

```csharp
// Issues up to 98 sequential DB queries on slug collision
for (var i = 2; i <= 99; i++)
{
    var taken = await _db.Tenants.AnyAsync(t => t.Subdomain == candidate && t.Id != tenantId, ct);
    if (!taken) return candidate;
}
```

In the pathological case (98 existing suffixed slugs), this blocks a provisioning request for ~100 × RTT. In practice collisions beyond 2–3 are rare, but the code should not rely on that.

**Fix — single query, in-process selection:**
```csharp
var candidates = Enumerable.Range(2, 98)
    .Select(i => SlugGenerator.AppendSuffix(slug, i))
    .ToList();

var takenSet = await _db.Tenants
    .Where(t => candidates.Contains(t.Subdomain!) && t.Id != tenantId)
    .Select(t => t.Subdomain!)
    .ToHashSetAsync(ct);

return candidates.First(c => !takenSet.Contains(c));
```

---

### 5. Over-Fetching in `GetPrimaryOrgMembershipAsync`

**File:** `apps/services/identity/Identity.Infrastructure/Repositories/UserRepository.cs`

The primary-org membership query loads a 5-level navigation graph on every login:

```
Membership → Organization → OrganizationProducts → Product → ProductRoles → OrgTypeRules → OrganizationType
           → Organization → OrganizationTypeRef
```

Most of this data is never read by the login path — only `Organization.Id`, `Organization.OrgType`, and `Organization.OrganizationTypeId` are needed for JWT claim population.

**Fix:** Introduce a lightweight projection for the login path:

```csharp
.Select(m => new {
    m.Organization.Id,
    m.Organization.OrgType,
    m.Organization.OrganizationTypeId,
    m.Organization.DisplayName,
    m.Organization.Name
})
.FirstOrDefaultAsync(ct);
```

Keep the full include chain available for callers (e.g., `ProductRoleResolutionService`) that actually need it, or pass the already-loaded membership in from `LoginAsync`.

---

### 6. `SigningCredentials` Allocated on Every Token Generation

**File:** `apps/services/identity/Identity.Infrastructure/Services/JwtTokenService.cs`

```csharp
// Called on every GenerateToken() invocation:
var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
```

`_signingKey`, the algorithm, `issuer`, `audience`, and `expiryMinutes` are all constant for the lifetime of the service. Only `credentials` should be cached — it allocates and boxes the key unnecessarily on every call.

**Fix:** Cache in the constructor:

```csharp
private readonly SigningCredentials _credentials;
private readonly string _issuer;
private readonly string _audience;
private readonly int _expiryMinutes;

public JwtTokenService(IConfiguration configuration)
{
    // ... RSA key setup ...
    _issuer        = configuration["Jwt:Issuer"]   ?? "legalsynq-identity";
    _audience      = configuration["Jwt:Audience"] ?? "legalsynq-platform";
    _expiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var m) ? m : 60;
    _credentials   = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
}
```

---

### 7. Missing `AsNoTracking()` on All Read-Only Repository Queries

**File:** `apps/services/identity/Identity.Infrastructure/Repositories/UserRepository.cs`

Methods `GetAllWithRolesAsync`, `GetByTenantWithRolesAsync`, `GetPrimaryOrgMembershipAsync`, `GetActiveMembershipsWithProductsAsync`, and `GetByIdWithRolesAsync` all return entities that are only ever projected into DTOs — they are never mutated through the `DbContext`. Without `AsNoTracking()`, EF Core tracks every entity and its graph in the `ChangeTracker`, increasing heap allocation linearly with result set size.

**Fix:** Add `.AsNoTracking()` to all read-only queries:

```csharp
public Task<List<User>> GetByTenantWithRolesAsync(Guid tenantId, CancellationToken ct = default) =>
    _db.Users
        .AsNoTracking()
        .Where(u => u.TenantId == tenantId)
        .Include(u => u.ScopedRoleAssignments.Where(s => s.IsActive))
            .ThenInclude(s => s.Role)
        .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
        .ToListAsync(ct);
```

> **Note:** Do not add `AsNoTracking()` to `GetByIdAsync` when used in update paths (e.g., `UpdateAvatarAsync` loads then mutates — tracking is required there).

---

### 8. `DbToFrontendProductCode` Dictionary Duplicated

**File:** `apps/services/identity/Identity.Application/Services/AuthService.cs` — comment reads:
> "Keep in sync with `AdminEndpoints.DbToFrontendProductCode`"

A lookup dictionary that must be manually kept in sync across two files is a maintenance liability. When a new product is added it is easy to miss one of the two sites.

**Fix:** Extract to a single constant in `Identity.Domain` or `Identity.Application`:

```csharp
// Identity.Application/ProductCodeMap.cs
public static class ProductCodeMap
{
    public static readonly IReadOnlyDictionary<string, string> DbToFrontend
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SYNQ_FUND"]        = "SynqFund",
            ["SYNQ_LIENS"]       = "SynqLien",
            ["SYNQ_CARECONNECT"] = "CareConnect",
            // ...
        };
}
```

Then reference `ProductCodeMap.DbToFrontend` from both `AuthService` and `AdminEndpoints`.

---

### 9. `ProductRoleResolutionService`: Second User Load is Redundant

**File:** `apps/services/identity/Identity.Infrastructure/Services/ProductRoleResolutionService.cs`

```csharp
// Load 1
var memberships = await _userRepository.GetActiveMembershipsWithProductsAsync(userId, tenantId, ct);
// Load 2 — same user, separate query
var userWithRoles = await _userRepository.GetByIdWithRolesAsync(userId, ct);
```

Both loads happen unconditionally inside `ResolveAsync`, which is called from `LoginAsync` — meaning login incurs at minimum **four** DB round-trips for user data alone (including the initial email lookup). The scoped role assignments should be passed in from the call site or included in the memberships query.

---

## 🟢 NIT

### 10. `VerificationRetryBackgroundService` — Misleading Option Name

**File:** `apps/services/identity/Identity.Infrastructure/Services/VerificationRetryBackgroundService.cs`

```csharp
// Used as the perpetual polling interval, not just the initial delay
var delaySeconds = Math.Max(15, _opts.InitialDelaySeconds);
```

`InitialDelaySeconds` is used as the recurring poll interval throughout the background worker's lifetime. Rename to `PollingIntervalSeconds` to match actual behaviour, and update `VerificationRetryOptions` and `appsettings.json` accordingly.

---

### 11. Unbounded List Queries — No Pagination

**File:** `apps/services/identity/Identity.Infrastructure/Repositories/UserRepository.cs`

`GetAllWithRolesAsync` and `GetByTenantWithRolesAsync` load an unbounded list of users into memory. As tenants grow, these calls will cause increasing latency and memory pressure.

**Fix:** Add `(int page, int pageSize)` parameters (or a cursor) before usage scales:

```csharp
public Task<List<User>> GetByTenantWithRolesAsync(
    Guid tenantId, int page = 1, int pageSize = 50,
    CancellationToken ct = default) =>
    _db.Users
        .AsNoTracking()
        .Where(u => u.TenantId == tenantId)
        .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
        .Skip((page - 1) * pageSize).Take(pageSize)
        // ...
```

---

### 12. `AuthService` and `UserService` Not `sealed`

**Files:**
- `apps/services/identity/Identity.Application/Services/AuthService.cs`
- `apps/services/identity/Identity.Application/Services/UserService.cs`

Both are concrete implementations of interfaces, not designed for inheritance. Mark them `sealed` to communicate intent and allow the JIT to de-virtualize calls:

```csharp
public sealed class AuthService : IAuthService { ... }
public sealed class UserService : IUserService { ... }
```

---

### 13. Untyped `res.json()` in Session Provider

**File:** `apps/web/src/providers/session-provider.tsx`

```ts
const me = await res.json(); // returns `any`
const mapped: PlatformSession = {
    userId: me.userId,
    email:  me.email,
    // ...
};
```

If `AuthMeResponse` gains or renames fields, the mapping silently produces `undefined` values at runtime with no TypeScript compile-time warning. Type the response:

```ts
import type { AuthMeResponse } from '@/types';
const me = await res.json() as AuthMeResponse;
```

Or use `zod` for runtime validation if API response integrity cannot be guaranteed.

---

## What's Well Done

- **Rate limiting** is correctly scoped to anonymous auth routes only, with proper IP de-aliasing via `ForwardedHeaders`.
- **`JwtTokenService` implements `IDisposable`** and releases the `RSA` instance correctly.
- **`BcryptPasswordHasher` uses work factor 12** — appropriate for a production system.
- **`ScopedAuthorizationService` keeps authorization checks server-side** — predicates translate cleanly to SQL `WHERE` clauses.
- **The BFF never forwards the raw JWT to the browser** — the `platform_session` HttpOnly cookie pattern is correctly implemented.
- **`CancellationToken` is consistently accepted and propagated** through service, repository, and endpoint layers.
- **`HashSet<string>` is used for membership tests** (e.g., `existingNameSet`) where `O(1)` lookup matters.

---

## Recommended Action Order

| Priority | Item | Effort |
|---|---|---|
| 1 | Mask PII in BFF login log | Low |
| 2 | Move accept-invite / change-password to `AuthService` | Medium |
| 3 | Eliminate double user load on login | Low |
| 4 | Add `AsNoTracking()` to all read-only queries | Low |
| 5 | Cache `SigningCredentials` in `JwtTokenService` | Low |
| 6 | Fix N+1 slug query in `TenantProvisioningService` | Low |
| 7 | Extract `ProductCodeMap` to shared location | Low |
| 8 | Refactor `Program.cs` startup blocks to extension methods | Medium |
| 9 | Add pagination to user list queries | Medium |
