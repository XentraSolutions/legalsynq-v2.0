# Security Review — Identity Service
**Date:** 2026-04-11  
**Reviewer:** GitHub Copilot (code-review-excellence skill)  
**Scope:** `apps/services/identity/Identity.Api`, `Identity.Infrastructure`, `apps/gateway/Gateway.Api`  
**Methodology:** OWASP Top 10 · Security Review Guide  

---

## Summary Table

| Severity | # | Issue |
|---|---|---|
| 🔴 Critical | 2 | No service-level auth on admin endpoints; raw reset token in response |
| 🟡 High | 3 | Reset token logged in production; HS256 symmetric key; unbounded pageSize |
| 🟠 Medium | 2 | X-Forwarded-For IP spoofing; RDS hostname in source control |
| 🟢 Low | 3 | Password policy; no rate limiting on auth endpoints; modulo bias |

---

## 🔴 CRITICAL

### 1. Admin endpoints have zero service-level authorization

**File:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`  
**Lines:** Route registration block (~L27–L157)

The class comment explicitly states:
> "The Identity service **trusts all forwarded requests unconditionally**."

None of the 60+ `/api/admin/...` routes have `.RequireAuthorization()`. The YARP gateway enforces JWT auth for external traffic, but if the Identity service is reachable directly — cloud misconfiguration, cluster-internal access, a compromised sidecar, an open debug port — **every admin operation is fully accessible with no credentials required**: create tenants, force-logout any user, set arbitrary passwords, assign roles.

Defense in depth requires service-level authorization regardless of what the gateway enforces.

**Fix:**
```csharp
// In MapAdminEndpoints(), apply to the route group:
var adminGroup = routes.MapGroup("/api/admin")
    .RequireAuthorization();

// Then register all routes on adminGroup instead of routes:
adminGroup.MapGet("/tenants", ListTenants);
adminGroup.MapPost("/tenants", CreateTenant);
// ...
```

Or, add `.RequireAuthorization()` to each individual `.MapGet`/`.MapPost`/etc. call.

---

### 2. Raw password-reset token returned in API response

**File:** `apps/services/identity/Identity.Api/Endpoints/AuthEndpoints.cs`  
**Line:** ~574 (`POST /api/auth/forgot-password` response)

```csharp
// ❌ Current — raw token in response body
return Results.Ok(new
{
    message    = "If an account exists...",
    resetToken = rawToken,   // plaintext token visible to anyone with access to the response
});
```

The comment acknowledges this is temporary ("Future: the raw token will be emailed"). This is a **production blocker**: any observer with access to response bodies (browser DevTools, intercepting proxy, centralized request/response logs, CDN access logs) can hijack any account on demand.

**Fix:** Remove `resetToken` from the response. Deliver the token exclusively via email using a transactional mail service, and return only the opaque success message.

```csharp
// ✅ Safe
return Results.Ok(new
{
    message = "If an account exists with that email, a password reset link has been sent.",
});
```

---

## 🟡 HIGH

### 3. Raw reset token logged unconditionally (fires in production)

**File:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`  
**Line:** ~1426 (`AdminResetPassword`)

```csharp
// ❌ Current — LogInformation has no environment guard
logger.LogInformation(
    "[UIX-003-03] ... Reset token (dev only — never log in production): {RawToken}.",
    user.Id, user.Email, user.TenantId, rawToken, resetToken.ExpiresAtUtc);
```

Despite the "dev only" comment, this is `LogInformation` with no `IsDevelopment()` guard. It fires in every environment. Any aggregated log system (CloudWatch, Seq, Datadog) or anyone with log read access can harvest valid reset tokens and take over accounts.

**Fix:**
```csharp
// ✅ Gate on development environment
if (app.Environment.IsDevelopment())
{
    logger.LogDebug(
        "[UIX-003-03] DEV reset token for {UserId}: {RawToken}",
        user.Id, rawToken);
}
else
{
    logger.LogInformation(
        "[UIX-003-03] Password reset triggered for user {UserId} in tenant {TenantId}.",
        user.Id, user.TenantId);
}
```

Since `AdminResetPassword` is a `static` handler (no `IWebHostEnvironment` injected), the environment check needs to be passed in or the logger call simply removed for production — logging only the token hash or ID, never the raw value.

---

### 4. Symmetric JWT (HS256) — signing key shared across all services

**Files:**  
- `apps/services/identity/Identity.Infrastructure/Services/JwtTokenService.cs` (signer)  
- `apps/gateway/Gateway.Api/Program.cs` (verifier)  
- All downstream services that verify JWTs

Both token issuance (Identity) and token validation (gateway + every microservice) share the same `Jwt:SigningKey`. With a symmetric algorithm, **any service that can verify a JWT can also forge one**. A breach of the fund service, audit service, or documents service yields the ability to mint arbitrary admin JWTs for any user.

**Fix:** Migrate to RS256:
- Identity service holds the **RSA private key** (signs tokens).
- All verifying services receive only the **RSA public key** (verify only, cannot sign).
- Rotate the key pair periodically; expose JWKS at a discovery endpoint.

```csharp
// ✅ Identity service — RS256 signing
var rsa = RSA.Create();
rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

// ✅ Gateway / downstream — public key only
options.TokenValidationParameters = new TokenValidationParameters
{
    IssuerSigningKeyResolver = (token, secToken, kid, params) =>
        FetchPublicKeyFromJwks(issuer + "/.well-known/jwks.json"),
    ValidateIssuerSigningKey = true,
    // ... other params
};
```

---

### 5. Unbounded `pageSize` on admin list endpoints — full table dump risk

**File:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`  
**Affected handlers:** `ListTenants` (~L168), `ListUsers` (~L959), `ListRoles` (~L1834), `ListAudit` (~L1957), `ListGroups` (~L3441)

These endpoints accept an unconstrained `pageSize` query parameter. An authenticated admin can pass `?pageSize=1000000` to dump the entire table in one HTTP response and one unbounded DB query. Only `GetUserActivity` correctly clamps the value.

**Fix:** Add the same clamp used in `GetUserActivity` to every paginated handler:

```csharp
// ✅ Add at the top of each list handler
pageSize = Math.Clamp(pageSize, 1, 100);
page     = Math.Max(page, 1);
```

---

## 🟠 MEDIUM

### 6. X-Forwarded-For IP spoofing in audit records

**Files:** `AuthEndpoints.cs` (~L30), `AdminEndpoints.cs` (multiple audit blocks)

IP extraction reads the **leftmost** value from `X-Forwarded-For`:

```csharp
// ❌ Current — leftmost value is client-controlled
httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
```

The `X-Forwarded-For` header is appended by each proxy in the chain. The leftmost value is set by the **client** and can be trivially spoofed (`curl -H "X-Forwarded-For: 1.2.3.4" ...`). This means every audit record's IP address is attacker-controlled.

**Fix:**  
- If the YARP gateway strips/rewrites this header before forwarding to Identity, document and enforce that at the gateway level.
- Otherwise read the rightmost value (the last hop added by a trusted proxy), or configure ASP.NET Core's `ForwardedHeaders` middleware with explicit `KnownProxies` to let `httpContext.Connection.RemoteIpAddress` contain the correct value automatically.

```csharp
// ✅ In Program.cs — configure trusted proxy chain
builder.Services.Configure<ForwardedHeadersOptions>(opts =>
{
    opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    opts.KnownProxies.Add(IPAddress.Parse("10.0.0.1")); // your gateway IP(s)
});
// Then simply use:
var ip = httpContext.Connection.RemoteIpAddress?.ToString();
```

---

### 7. Live RDS hostname committed to source control

**File:** `apps/services/identity/Identity.Api/appsettings.json` (L4)

```json
"IdentityDb": "server=legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com;port=3306;database=identity_db;user=admin;password=REPLACE_VIA_SECRET"
```

Even with `password=REPLACE_VIA_SECRET`, the live RDS instance DNS name is in version control. This aids reconnaissance (region, account hint, service name) and violates the principle of keeping infrastructure details out of code repos.

**Fix:** Replace the connection string value with an environment variable reference and ensure no real hostnames are in committed config:

```json
"ConnectionStrings": {
  "IdentityDb": ""
}
```

Inject the full connection string via environment variable (`ConnectionStrings__IdentityDb`) or AWS Secrets Manager / Parameter Store at deploy time.

---

## 🟢 LOW

### 8. Password policy: length-only validation (8-character minimum)

**Files:** `AuthEndpoints.cs` (~L121), `AdminEndpoints.cs` (~L1471)

```csharp
// Current: only length is checked
if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 8)
```

For an identity service handling legal/medical data, an 8-character minimum with no complexity requirement is weak. NIST SP 800-63B and common compliance frameworks (SOC 2, HIPAA) recommend a minimum of 12 characters and checking against known-breached password lists.

**Recommendation:** Raise the minimum to 12 characters. Consider integrating a HIBP (Have I Been Pwned) check for user-set passwords.

---

### 9. No rate limiting on authentication endpoints

**File:** `apps/services/identity/Identity.Api/Endpoints/AuthEndpoints.cs`

`POST /api/auth/login`, `POST /api/auth/forgot-password`, `POST /api/auth/accept-invite`, and `POST /api/auth/password-reset/confirm` have no HTTP-layer rate limiting. Without it, these endpoints are open to credential stuffing, token enumeration, and denial-of-service via bcrypt cost (login).

**Fix:** Add ASP.NET Core's built-in rate limiter (available since .NET 7):

```csharp
// In Program.cs
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("auth", policy =>
    {
        policy.PermitLimit         = 10;
        policy.Window              = TimeSpan.FromMinutes(1);
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit          = 0;
    });
});

// On each auth route:
app.MapPost("/api/auth/login", ...).AllowAnonymous().RequireRateLimiting("auth");
app.MapPost("/api/auth/forgot-password", ...).AllowAnonymous().RequireRateLimiting("auth");
```

---

### 10. Modulo bias in `GenerateTemporaryPassword`

**File:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`  
**Line:** ~717

```csharp
// ❌ Current — modulo bias when 256 % charSet.Length != 0
chars[0] = upper[bytes[0] % upper.Length]; // upper.Length = 24 → 256 % 24 = 16
```

When 256 is not divisible by the character set length, the first `256 % N` characters appear slightly more often (~0.4% bias for the 24-char upper set). The bias is small and the practical impact on a temporary 12-character password is negligible, but it is avoidable.

**Fix:** Use rejection sampling:

```csharp
// ✅ Unbiased character selection
static char PickChar(ReadOnlySpan<char> set, byte[] pool, ref int pos)
{
    int limit = 256 - (256 % set.Length);
    byte b;
    do { b = pool[pos++ % pool.Length]; } while (b >= limit);
    return set[b % set.Length];
}
```

---

## Checklist Recap

### Authentication & Authorization
- [x] Passwords hashed with bcrypt (workFactor: 12) ✅
- [x] SHA-256 hash stored for reset/invite tokens ✅
- [x] Session version embedded in JWT for invalidation ✅
- [x] JWT lifetime validation + zero clock skew ✅
- [ ] **Admin endpoints require no authorization at service level** 🔴
- [ ] **No rate limiting on auth endpoints** 🟢

### Sensitive Data
- [ ] **Raw reset token returned in API response** 🔴
- [ ] **Raw reset token logged unconditionally** 🟡
- [ ] **Live RDS hostname in source control** 🟠
- [x] Passwords never logged ✅
- [x] Invite/reset tokens stored as hashes only ✅

### Cryptography
- [x] bcrypt with cost 12 ✅
- [x] CSPRNG for token/password generation ✅
- [ ] **HS256 symmetric key shared across all services** 🟡
- [ ] **Minor modulo bias in password generator** 🟢

### Input Validation
- [x] GUID route parameters type-constrained (`:guid`) ✅
- [x] Tenant code format validated (alphanumeric, 2–12 chars) ✅
- [x] Subdomain slug validated via `SlugGenerator` ✅
- [ ] **Unbounded pageSize on list endpoints** 🟡

### Tenant Isolation
- [x] `IsCrossTenantAccess` check present on user-scoped admin handlers ✅
- [x] PlatformAdmin bypass explicitly handled ✅
- [ ] `ListTenants`, `CreateTenant`, org-level endpoints have no tenant isolation (expected for PlatformAdmin-only routes, but see finding #1) ⚠️
