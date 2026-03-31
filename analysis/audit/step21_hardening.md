# Step 21 — Production Hardening Analysis

**Date:** 2026-03-30  
**Scope:** Platform Audit/Event Service  
**Status:** COMPLETE — all identified issues resolved

---

## Overview

A systematic production-readiness review of every runtime file was conducted.
The review covered all middleware, controllers, configuration classes, services,
validators, Program.cs, and appsettings files.  All significant findings were
immediately resolved in the same session.

---

## Issues Found and Resolved

### 1. `ExceptionMiddleware` — Internal Message Leakage (SECURITY)

**Severity:** High  
**File:** `Middleware/ExceptionMiddleware.cs`

**Finding:**  
`ArgumentException` and `InvalidOperationException` were forwarded to clients via
`ex.Message`. Exception messages often contain internal paths, method names, or
developer-facing diagnostic strings that are not safe to surface to API callers.

**Fix:**  
All exception-to-response message mappings now use static, caller-safe strings:
- `ArgumentException` → `"Invalid input."`
- `InvalidOperationException` → `"The request could not be processed in the current state."`
- `UnauthorizedAccessException` → `"Access denied."`
- All others → `"An unexpected error occurred."`

The full exception detail (including `ex.Message` and stack trace) remains in the
server-side structured log. Nothing is suppressed from operators.

---

### 2. `ExceptionMiddleware` — Wrong HTTP Status for Access Denial (BUG)

**Severity:** Medium  
**File:** `Middleware/ExceptionMiddleware.cs`

**Finding:**  
`UnauthorizedAccessException` was mapped to HTTP `401 Unauthorized`.  
In .NET semantics, `UnauthorizedAccessException` means the caller is known but
lacks permission — an **authorization** failure.  
HTTP semantics: 401 = "I don't know who you are" (authentication). 403 = "I know who you are, but you can't do this" (authorization).

**Fix:**  
`UnauthorizedAccessException` now maps to `403 Forbidden`.

---

### 3. `ExceptionMiddleware` — Missing `JsonStringEnumConverter` (INCONSISTENCY)

**Severity:** Low  
**File:** `Middleware/ExceptionMiddleware.cs`

**Finding:**  
The internal `_json` serializer options used `CamelCase` naming only, missing
`JsonStringEnumConverter`. This made exception-path responses serialize enums as
integers while success responses used string enum names (set in `Program.cs`
`AddControllers().AddJsonOptions`).

**Fix:**  
Added `new JsonStringEnumConverter()` to the middleware `_json` options.

---

### 4. `ExceptionMiddleware` — Incorrect Log Level for 4xx Errors (QUALITY)

**Severity:** Low  
**File:** `Middleware/ExceptionMiddleware.cs`

**Finding:**  
All caught exceptions were logged at `Error` level, including client-caused 4xx
conditions. This inflates error-rate dashboards with client-side noise.

**Fix:**  
4xx exceptions log at `Warning`; 5xx (unhandled) at `Error`.

---

### 5. `CorrelationIdMiddleware` — No Header Sanitization (SECURITY)

**Severity:** Medium  
**File:** `Middleware/CorrelationIdMiddleware.cs`

**Finding:**  
The raw incoming `X-Correlation-ID` header value was echoed back to the client
and stored in `HttpContext.Items` without any length or character validation.
An adversary could send:
- A multi-kilobyte value to pad all log lines and response headers.
- Special characters (CRLF, semicolons) to attempt header injection.

**Fix:**  
Added a `ResolveCorrelationId` helper that:
1. Rejects values longer than 100 characters.
2. Rejects values containing characters outside `[a-zA-Z0-9\-_]`.
3. Falls back to a fresh `Guid.NewGuid()` string on rejection.

---

### 6. `CorrelationIdMiddleware` — CorrelationId Not in Structured Logs (OBSERVABILITY)

**Severity:** Medium  
**File:** `Middleware/CorrelationIdMiddleware.cs`

**Finding:**  
The correlation ID was stored in `HttpContext.Items` but was never pushed into
Serilog's `LogContext`. As a result, `CorrelationId` did not appear as a
structured property in log entries unless a developer manually extracted it.
This made request-scoped log correlation impractical for most log queries.

**Fix:**  
The resolved correlation ID is now pushed via `LogContext.PushProperty("CorrelationId", …)`
wrapped around the `await _next(ctx)` call. Every log entry written during the
lifetime of an HTTP request automatically carries `CorrelationId` as a structured field.

---

### 7. `appsettings.json` — Serilog Output Template Missing CorrelationId (OBSERVABILITY)

**Severity:** Low  
**File:** `appsettings.json`

**Finding:**  
The console sink output template did not include `{CorrelationId}`, so even after
the LogContext fix the property would not appear in human-readable console output.

**Fix:**  
Updated template to:
```
[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {SourceContext}: {Message:lj}{NewLine}{Exception}
```

---

### 8. `AuditExportController` — Inconsistent Error Response Envelope (CONTRACT)

**Severity:** Medium  
**File:** `Controllers/AuditExportController.cs`

**Finding:**  
Five error paths returned anonymous objects `new { error = "..." }` or
`new { error, errors }` instead of the standard `ApiResponse<T>` envelope.  
Client code that expects `{ success, message, data, errors, traceId }` on every
response would receive an unexpected shape on these paths.

Affected paths:
- Validation failure (400)
- Unsupported format (400)
- Access denied (403)
- Unauthenticated (401)
- Export not configured (503)

**Fix:**  
All five paths now return `ApiResponse<object>.Fail(…, traceId: traceId)` or
`ApiResponse<object>.ValidationFail(…, traceId: traceId)`.  
Success paths (202 and 200) are also now wrapped in `ApiResponse<T>.Ok(…)` for
full contract consistency.

---

### 9. `HealthController` — Hardcoded Service Name and Version (CONFIGURATION)

**Severity:** Low  
**File:** `Controllers/HealthController.cs`

**Finding:**  
`Service = "Platform Audit/Event Service"` and `Version = "1.0.0"` were hardcoded
literals. `AuditServiceOptions` already carries `ServiceName` and `Version` properties
that are config-driven and are correctly used in Program.cs (Swagger) and startup logs.

**Fix:**  
`IOptions<AuditServiceOptions>` is now injected into `HealthController` and its values
are used for `Service` and `Version` in the response.

---

### 10. `HealthController` — Route Conflict with Built-In Health Check Endpoint (BUG)

**Severity:** Medium  
**File:** `Controllers/HealthController.cs` / `Program.cs`

**Finding:**  
`[Route("[controller]")]` on `HealthController` resolves to `/health`.
`app.MapHealthChecks("/health")` also targets `/health`.
Both were registered in the ASP.NET Core endpoint routing table for the same
path and HTTP verb, creating an ambiguous endpoint match.

**Fix:**  
`HealthController` route changed from `[Route("[controller]")]` to
`[Route("health/detail")]`.

Architecture after fix:
- `GET /health` — lightweight built-in health check (k8s liveness/readiness probe).
- `GET /health/detail` — rich diagnostic response: service name, version, live event count.

Swagger description in `Program.cs` updated to document both endpoints.

---

### 11. `AuditEventQueryController` — Query Parameters Not Validated (QUALITY)

**Severity:** Medium  
**File:** `Controllers/AuditEventQueryController.cs`

**Finding:**  
`AuditEventQueryRequestValidator` was registered in DI but never called from the
query controller. The validator enforces pagination bounds, enum validity, time-range
ordering, and string length limits on 19 filter parameters — none of these were
checked before the query reached the service layer.

**Fix:**  
`IValidator<AuditEventQueryRequest>` is now injected into `AuditEventQueryController`.
A private `ValidateQueryAsync` helper is called in every action that accepts a
`[FromQuery] AuditEventQueryRequest`:
- `ListEvents`
- `GetEntityEvents`
- `GetActorEvents`
- `GetUserEvents`
- `GetTenantEvents`
- `GetOrganizationEvents`

Validation runs after path parameters are merged into the query object (so their
lengths are also checked) and before the authorization call (fail fast on bad input).
Returns `400 ApiResponse.ValidationFail` on failure.

---

### 12. `Program.cs` — Auth-Mode "None" Not Escalated to Error in Production (SECURITY)

**Severity:** Medium  
**File:** `Program.cs`

**Finding:**  
`IngestAuth:Mode = "None"` and `QueryAuth:Mode = "None"` emitted `LogWarning` in all
environments. In Production, these conditions represent critical security misconfigurations
that should surface as alerts in log aggregation platforms (which typically alert on
`Error` or `Fatal` level, not `Warning`).

**Fix:**  
After `builder.Build()`, if `app.Environment.IsProduction()`:
- Auth mode "None" → `Log.Error(…)` with a directive to resolve immediately.
- `EnableSensitiveDataLogging = true` → `Log.Error(…)`.
- `EnableDetailedErrors = true` → `Log.Warning(…)`.

---

### 13. `Program.cs` — Missing HTTP Security Response Headers (SECURITY)

**Severity:** Medium  
**File:** `Program.cs`

**Finding:**  
No security response headers were added to HTTP responses.  
OWASP recommends at minimum: `X-Content-Type-Options`, `X-Frame-Options`,
`Referrer-Policy`, and disabling the legacy `X-XSS-Protection` in favor of CSP.

**Fix:**  
Added an inline `app.Use(…)` middleware immediately after `CorrelationIdMiddleware`
in the pipeline. Headers added to every response:
```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
X-XSS-Protection: 0
```

---

### 14. Missing `appsettings.Production.json` (CONFIGURATION)

**Severity:** Medium  
**File:** (new) `appsettings.Production.json`

**Finding:**  
No production-specific configuration file existed. Operators had no authoritative
baseline to reference when configuring a production deployment.

**Fix:**  
Created `appsettings.Production.json` with hardened production defaults:
- `Database:Provider = "MySQL"` (not InMemory)
- `Database:EnableSensitiveDataLogging = false`
- `Database:EnableDetailedErrors = false`
- `IngestAuth:Mode = "ServiceToken"`
- `IngestAuth:RequireSourceSystemHeader = true`
- `QueryAuth:Mode = "Bearer"`
- `QueryAuth:EnforceTenantScope = true`
- `Integrity:VerifyOnRead = true`
- `Retention:DryRun = false`, `LegalHoldEnabled = true`
- `AuditService:ExposeSwagger = false`
- Serilog with ISO-8601 timestamp format for log aggregation tooling

Connection strings, tokens, and secrets are explicitly documented as environment-variable-only and are **not** present in the file.

---

## Items Reviewed with No Issues Found

| Area | Status |
|------|--------|
| Pagination cap (`MaxPageSize = 500` via `Math.Clamp`) | Correct |
| Batch size cap (500 in `BatchIngestRequestValidator`) | Correct |
| FluentValidation integration for ingest endpoints | Correct |
| Swagger gated by `IsDevelopment() || ExposeSwagger` | Correct |
| `EnableSensitiveDataLogging` defaults false | Correct |
| `EnableDetailedErrors` defaults false | Correct |
| `AllowedCorsOrigins` defaults empty (deny all CORS) | Correct |
| `ExposeSwagger` defaults false | Correct |
| Append-only persistence with unique-constraint race handling | Correct |
| `ExceptionMiddleware` first in pipeline | Correct |
| Serilog structured request logging with level routing | Correct |
| `IngestAuth:Mode = "None"` startup warning | Correct (now Error in Prod) |
| `QueryAuth:Mode = "None"` startup warning | Correct (now Error in Prod) |
| IntegrityCheckpointController uses ApiResponse consistently | Correct |
| AuditEventQueryController uses ApiResponse consistently | Correct |
| IngestController uses ApiResponse consistently | Correct |

---

## Build Verification

After all changes were applied:
```
dotnet clean && dotnet build -c Debug 2>&1 | grep -E "(error|warning|Build succeeded|FAILED)"
```
Result:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Files Changed

| File | Change |
|------|--------|
| `Middleware/ExceptionMiddleware.cs` | Fix 401→403; safe messages; enum converter; log levels |
| `Middleware/CorrelationIdMiddleware.cs` | Header sanitization; Serilog LogContext push |
| `Controllers/AuditExportController.cs` | ApiResponse envelope on all error and success paths |
| `Controllers/HealthController.cs` | Options injection; route `/health/detail`; rename DTO |
| `Controllers/AuditEventQueryController.cs` | Add FluentValidation to all query actions |
| `Program.cs` | Security headers middleware; production security assertions; Swagger doc update |
| `appsettings.json` | Serilog template includes `{CorrelationId}` |
| `appsettings.Production.json` | **New** — hardened production defaults |
| `Docs/production-readiness-checklist.md` | **New** — operator deployment checklist |
| `analysis/step21_hardening.md` | **New** — this document |
