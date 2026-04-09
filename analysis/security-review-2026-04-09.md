# LegalSynq v2.0 — Full Code Security Review

**Date:** April 9, 2026  
**Reviewer:** GitHub Copilot (Claude Sonnet 4.6)  
**Scope:** Full codebase — backend services, API gateway, frontend apps, shared libraries  
**Standard:** OWASP Top 10, NIST SP 800-63B

---

## Summary

| Severity | Count |
|----------|-------|
| 🔴 Critical | 3 |
| 🟠 High | 5 |
| 🟡 Medium | 5 |
| 🟢 Low / Informational | 10 |

---

## Critical Findings

---

### [CRIT-01] Admin endpoints have no authorization at the service layer

**OWASP:** A01 — Broken Access Control  
**File:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`

The class-level comment explicitly states:
> *"Auth is enforced at the gateway layer — the Identity service trusts all forwarded requests **unconditionally**."*

All 80+ `/api/admin/...` routes (tenant creation/deletion, user activation/locking, password reset, DNS provisioning, role assignment) are registered with **no `.RequireAuthorization()` call**. The service relies entirely on YARP as its only enforcement boundary.

**Risk:** If the Identity service port (5001) is directly reachable inside the network — via misconfigured network policies, container escape, SSRF from another service, or lateral movement — every admin operation is executable by any unauthenticated caller.

**Mitigation:**
```csharp
// Add a policy in Program.cs
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("PlatformAdmin", p => p.RequireRole("PlatformAdmin"));

// Apply to all admin routes
routes.MapGet("/api/admin/tenants", ListTenants)
      .RequireAuthorization("PlatformAdmin");
```
At minimum, require a valid JWT on all admin endpoints as defence-in-depth. The gateway should not be the sole gate.

---

### [CRIT-02] `AllowAnyOrigin()` CORS fallback on Audit and Documents services

**OWASP:** A05 — Security Misconfiguration  
**Files:**
- `apps/services/audit/Program.cs` (line 542)
- `apps/services/documents-dotnet/Documents.Api/Program.cs` (line 151)

When `AllowedCorsOrigins` is empty or contains `"*"`, both services fall through to `policy.AllowAnyOrigin()`. Combined with `AllowAnyHeader().AllowAnyMethod()`, this is a blanket CORS bypass.

**Risk:** Any website can make cross-origin requests to these services.

**Mitigation:** Remove the wildcard fallback. Fail closed — if no origins are configured, deny all cross-origin requests:
```csharp
if (allowedOrigins.Count == 0)
    policy.WithOrigins(); // effectively denies all
else
    policy.WithOrigins([.. allowedOrigins]).AllowAnyHeader().AllowAnyMethod();
```

---

### [CRIT-03] Real AWS RDS hostname committed to source control

**OWASP:** A02 — Cryptographic / Secrets Failures  
**Files:**
- `apps/services/identity/Identity.Api/appsettings.json`
- `apps/services/fund/Fund.Api/appsettings.json`
- `apps/services/careconnect/CareConnect.Api/appsettings.json`

The full AWS RDS endpoint `legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com` with username `admin` is in plaintext in committed configuration files.

**Risk:** Even with the password redacted, the hostname reveals the cloud provider, region, account fingerprint, and database identifiers — reconnaissance data for attackers.

**Mitigation:** Replace the hostname with a placeholder (`server=REPLACE_VIA_SECRET`) or move all connection string templates out of committed files. Use AWS Secrets Manager or environment variables injected at runtime.

---

## High Findings

---

### [HIGH-01] No brute-force / rate limiting on the login endpoint

**OWASP:** A07 — Identification & Authentication Failures  
**File:** `apps/services/identity/Identity.Api/Endpoints/AuthEndpoints.cs`

`POST /api/auth/login` has no rate limiting, no failed-attempt counter, and no per-IP throttling. The `control-center/next.config.mjs` contains a `TODO` comment acknowledging this gap.

**Risk:** Credential stuffing and brute-force attacks can run unconstrained. Account locking only applies after a manual admin action.

**Mitigation:** Add ASP.NET Core rate limiting (already used in the Documents service as a reference):
```csharp
builder.Services.AddRateLimiter(opts =>
    opts.AddFixedWindowLimiter("login", o => {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 5;
        o.QueueLimit = 0;
    })
);
app.MapPost("/api/auth/login", ...).RequireRateLimiting("login");
```

---

### [HIGH-02] `X-Forwarded-For` consumed without trusted-proxy validation

**OWASP:** A03 — Injection / Spoofing  
**File:** `apps/services/identity/Identity.Api/Endpoints/AuthEndpoints.cs` (lines 30, 103, 270, 321, 367)

The Identity service reads `X-Forwarded-For` directly from request headers in multiple places for audit logging — but does not use `UseForwardedHeaders()` with `KnownProxies`/`KnownNetworks` configured.

**Risk:** Any caller can inject a fake IP into `X-Forwarded-For`, bypassing IP-based audit logs and future IP-based controls. Audit events will record an attacker-controlled IP address.

**Mitigation:**
```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options => {
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.1")); // your gateway IP
});
app.UseForwardedHeaders();
// Then use httpContext.Connection.RemoteIpAddress only
```

---

### [HIGH-03] Development JWT signing key is weak and committed

**OWASP:** A02 — Cryptographic / Secrets Failures  
**File:** `apps/gateway/Gateway.Api/appsettings.Development.json`

```json
"SigningKey": "dev-only-signing-key-minimum-32-chars-long!"
```

This key is committed to source control and used identically by both Gateway and Identity services. If it leaks into a production environment, forged JWTs with arbitrary claims could be created.

**Mitigation:** Add `.Development.json` files to `.gitignore`. Use `dotnet user-secrets` locally. Ensure production uses secrets injection exclusively. Rotate dev keys regularly.

---

### [HIGH-04] `/api/admin/integrity` endpoint is unauthenticated

**OWASP:** A01 — Broken Access Control  
**File:** `apps/services/careconnect/CareConnect.Api/Endpoints/CareConnectIntegrityEndpoints.cs` (line 25)

```csharp
routes.MapGet("/api/admin/integrity", GetIntegrityReport).AllowAnonymous();
```

This returns internal operational counters about the database to any unauthenticated caller.

**Risk:** Information disclosure — exposes internal data structure and operational health data.

**Mitigation:** Change `.AllowAnonymous()` to `.RequireAuthorization(Policies.PlatformOrTenantAdmin)`. Every other admin endpoint in CareConnect already requires this policy.

---

### [HIGH-05] Server Actions `allowedOrigins: ['*']` in web portal

**OWASP:** A01 — Broken Access Control / CSRF  
**File:** `apps/web/next.config.mjs` (line 7)

```js
serverActions: {
  allowedOrigins: ['*'],
},
```

This disables Next.js 14's built-in CSRF origin check for Server Actions across the entire tenant web portal.

**Risk:** Cross-site requests can trigger Server Actions on behalf of authenticated users — a blanket CSRF bypass for all state-changing server actions.

**Mitigation:** Enumerate allowed origins explicitly, mirroring the pattern already used in `apps/control-center/next.config.mjs`. The `TODO` comment acknowledges this — it must be resolved before production.

---

## Medium Findings

---

### [MED-01] Password validation is length-only (minimum 8 characters)

**OWASP:** A07 — Identification & Authentication Failures  
**File:** `apps/services/identity/Identity.Api/Endpoints/AuthEndpoints.cs` (lines 137, 233, 402)

All password acceptance points only check `body.NewPassword.Length < 8`. No complexity, entropy, or common-password checks exist.

**Risk:** Passwords like `password`, `12345678`, or `aaaaaaaa` are accepted.

**Mitigation:** Add complexity rules or integrate the HIBP API to reject known compromised passwords. NIST SP 800-63B recommends at minimum 8 chars + checking against known breach lists.

---

### [MED-02] Security response headers missing from most services

**OWASP:** A05 — Security Misconfiguration  
**Affected:** `apps/web`, `apps/gateway`, Identity, CareConnect, Fund APIs

Only the Control Center and the Audit service have security headers. The main web app, Gateway, Identity, CareConnect, and Fund APIs send no `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, or `Strict-Transport-Security` headers.

**Risk:** Clickjacking, MIME-sniffing, and information leakage through referrer headers.

**Mitigation:** Add a `headers()` block to `apps/web/next.config.mjs` mirroring the control-center config. Add security header middleware to all .NET services.

---

### [MED-03] No Content-Security-Policy on either frontend

**OWASP:** A03 — XSS  
**Files:** `apps/web/next.config.mjs`, `apps/control-center/next.config.mjs`

Both frontends acknowledge this with `TODO` comments. No CSP header is set on any response.

**Risk:** Without CSP, any XSS vulnerability executes without browser-level restriction.

**Mitigation:** Implement a strict CSP. Start in report-only mode (`Content-Security-Policy-Report-Only`) to detect violations before enforcing.

---

### [MED-04] `ExecuteSqlRawAsync` used with string-format pattern

**OWASP:** A03 — Injection  
**File:** `apps/services/identity/Identity.Infrastructure/Services/ProductProvisioningService.cs` (lines 86, 94)

```csharp
await _db.Database.ExecuteSqlRawAsync(
    "UPDATE TenantProducts SET IsEnabled = 0 WHERE TenantId = {0} AND ProductId = {1}",
    tenant.Id, product.Id);
```

EF Core's `{0}` parameterization is safe here. However, this pattern is a maintenance hazard — a future developer might concatenate a string instead, introducing SQL injection.

**Mitigation:** Replace with EF Core LINQ updates or use `ExecuteSqlInterpolatedAsync` which is explicitly injection-safe by design:
```csharp
await _db.Database.ExecuteSqlInterpolatedAsync(
    $"UPDATE TenantProducts SET IsEnabled = 0 WHERE TenantId = {tenant.Id} AND ProductId = {product.Id}");
```

---

### [MED-05] User email address written to server logs on every login

**OWASP:** A09 — Security Logging and Monitoring Failures  
**File:** `apps/web/src/app/api/auth/login/route.ts` (line 47)

```ts
console.log(`[login] host=${rawHost}, ...email=${email}`);
```

User email addresses are written to server logs on every login attempt.

**Risk:** Log aggregation systems (CloudWatch, Datadog) may store or expose PII. In a legal platform, email addresses are sensitive data.

**Mitigation:** Remove or hash PII from log output. Log a user ID or a masked form (`e****@domain.com`) instead.

---

## Low / Informational Findings

| ID | Finding | File | Action |
|----|---------|------|--------|
| LOW-01 | `/health` and `/info` endpoints return service name + environment name | `Identity.Api/Program.cs` | Restrict environment name to internal networks |
| LOW-02 | Real AWS region (`us-east-2`) and RDS subdomain in config files | Multiple `appsettings.json` | Replace with placeholders |
| LOW-03 | `AllowedHosts: "*"` — all hostnames accepted without restriction | `Identity.Api/appsettings.json` | Restrict to known hostnames |
| LOW-04 | `appsettings.Development.json` committed with dev signing keys | Gateway + Identity | Add to `.gitignore`, use `user-secrets` |
| LOW-05 | No CSRF double-submit cookie on login/logout BFF routes | `next.config.mjs` (TODO acknowledged) | Implement before production |
| LOW-06 | `DisableAntiforgery()` on document upload endpoints | `DocumentEndpoints.cs` | Acceptable for API-only endpoints; document rationale |
| LOW-07 | ✅ BCrypt `workFactor: 12` — correct and sufficient | `BcryptPasswordHasher.cs` | No action needed |
| LOW-08 | ✅ JWT `ClockSkew: TimeSpan.Zero` — strict and correct | Gateway + Identity `Program.cs` | No action needed |
| LOW-09 | ✅ Session version invalidation on force-logout is implemented correctly | `AuthService.cs` | No action needed |
| LOW-10 | ✅ HttpOnly + SameSite=Strict cookie for JWT storage — correctly implemented | `login/route.ts` | No action needed |

---

## Priority Remediation Order

| Priority | ID | Finding | Effort |
|----------|----|---------|--------|
| 🔴 P0 | CRIT-01 | Admin endpoints unprotected at service layer | Medium |
| 🔴 P0 | CRIT-02 | Wildcard CORS on Audit + Documents services | Low |
| 🔴 P0 | HIGH-05 | Server Actions `allowedOrigins: ['*']` in web portal | Low |
| 🟠 P1 | HIGH-04 | Integrity endpoint unauthenticated | Trivial |
| 🟠 P1 | HIGH-01 | No login rate limiting | Low |
| 🟠 P1 | HIGH-02 | `X-Forwarded-For` not proxy-validated | Low |
| 🟡 P2 | CRIT-03 | RDS hostname committed to source control | Low |
| 🟡 P2 | HIGH-03 | Dev JWT signing key committed | Low |
| 🟡 P2 | MED-02 | Missing security headers on most services | Medium |
| 🟡 P2 | MED-03 | No Content-Security-Policy on frontends | Medium |
| 🟢 P3 | MED-01 | Password complexity enforcement | Low |
| 🟢 P3 | MED-04 | Replace `ExecuteSqlRawAsync` pattern | Low |
| 🟢 P3 | MED-05 | Email address in server logs | Low |

---

## Positive Security Observations

The codebase demonstrates several well-implemented security controls worth noting:

- **JWT storage** — raw tokens are never sent to the browser; stored exclusively in HttpOnly, SameSite=Strict cookies via the BFF pattern.
- **Session invalidation** — `session_version` claim is validated against the database on every `/auth/me` call, enabling effective force-logout.
- **Password hashing** — BCrypt with `workFactor: 12` is appropriate and correctly applied.
- **Tenant isolation** — user-facing endpoints enforce `tenantId` claim checks at the handler level (not just gateway).
- **JWT validation** — `ClockSkew: TimeSpan.Zero`, all validation parameters enabled, token not decoded client-side.
- **Audit logging** — comprehensive audit trail for auth events, user lifecycle, and admin actions.
- **Invite token security** — invitation tokens are SHA-256 hashed before storage; raw tokens are never persisted.
- **Account locking** — locked accounts are rejected at both login and `/auth/me` (session check).
