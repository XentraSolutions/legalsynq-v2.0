# Step 12 — Service-to-Service Ingest Authentication: Report

Platform Audit/Event Service  
Date: 2026-03-30

---

## Overview

This step adds a portable, config-driven service-to-service authentication layer protecting
all `/internal/audit/*` endpoints. The design decouples auth mechanism selection from the
middleware and controllers, making it straightforward to replace or extend the auth strategy
without touching the ingestion pipeline.

---

## Auth Flow

```
Inbound request
      │
      ▼
IngestAuthMiddleware.InvokeAsync(HttpContext)
      │
      ├─ Path does not start with /internal/audit → skip → next()
      │
      ├─ IngestAuth:Mode = "None"
      │     └─ SetAnonymousContext (ServiceName = "anonymous") → next()
      │
      └─ Delegate to IIngestAuthenticator.AuthenticateAsync(headers)
             │
             ServiceTokenAuthenticator:
             │
             ├─ x-service-token absent/empty
             │     └─ 401  { reason: "MissingToken" }
             │
             ├─ ServiceTokens registry empty (misconfiguration)
             │     └─ 401  { reason: "TokenNotConfigured" }
             │
             ├─ No registry entry matches (constant-time scan)
             │     └─ 401  { reason: "InvalidToken" }
             │
             └─ Match found (entry.Enabled = true)
                    │
                    ├─ AllowedSources configured AND x-source-system absent/not in list
                    │     └─ 403  Forbidden
                    │
                    └─ Success
                           └─ Store ServiceAuthContext in HttpContext.Items → next()
                                    │
                                    ▼
                           AuditEventIngestController (reads ServiceAuthContext if needed)
```

### Constant-time scan detail

`ServiceTokenAuthenticator` encodes all registry token values at startup (avoiding per-request allocation).
On each request:

1. The inbound token value is UTF-8 encoded once.
2. Every registry entry is compared using `CryptographicOperations.FixedTimeEquals`.
3. Unequal-length arrays are padded to the same length before comparison so token length cannot be inferred from response time.
4. The scan **always runs to completion** (no early exit on first failure) so the response time is independent of which position in the registry a match occurs.

---

## Headers Used

| Header | Role | Required |
|--------|------|----------|
| `x-service-token` | Shared secret credential | Required in ServiceToken mode |
| `x-source-system` | Source system identifier (logging + allowlist) | Optional by default; required when `AllowedSources` configured or `RequireSourceSystemHeader = true` |
| `x-source-service` | Sub-component within source system (logging only) | Optional |

All headers are case-insensitive (RFC 7230).

### Why `x-service-token` and not `Authorization: Bearer`?

`Authorization: Bearer` implies JWT semantics — validation, expiry, JWKS endpoint. This auth
layer is deliberately simpler (shared secrets, no identity provider dependency). Using a
distinct header avoids protocol confusion and makes it easy to later add a real Bearer mode
alongside the existing token mode without conflict.

---

## Config Model

### `IngestAuthOptions` (section: `IngestAuth`)

```json
{
  "Mode": "ServiceToken",
  "ServiceTokens": [
    {
      "Token": "{{ injected from environment }}",
      "ServiceName": "identity-service",
      "Description": "Identity microservice",
      "Enabled": true
    },
    {
      "Token": "{{ injected from environment }}",
      "ServiceName": "fund-service",
      "Description": "Fund microservice",
      "Enabled": true
    }
  ],
  "RequireSourceSystemHeader": false,
  "AllowedSources": []
}
```

### `ServiceTokenEntry`

| Field | Type | Description |
|-------|------|-------------|
| `Token` | `string` | The shared secret. Inject via env var. |
| `ServiceName` | `string` | Logical service name — appears in logs and `ServiceAuthContext`. |
| `Description` | `string?` | Human-readable description for operator clarity. |
| `Enabled` | `bool` | Revoke without removing config entry. Default `true`. |

### `IngestAuthOptions` fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Mode` | `string` | `"None"` | Auth strategy. `None`, `ServiceToken`, `Bearer` (planned). |
| `ServiceTokens` | `List<ServiceTokenEntry>` | `[]` | Named token registry. |
| `RequireSourceSystemHeader` | `bool` | `false` | Enforce `x-source-system` presence. |
| `AllowedSources` | `List<string>` | `[]` | Allowlist for `x-source-system`. Empty = any. |
| `ApiKey` | `string?` | `null` | [Legacy] Single key for ApiKey mode. |
| `RequiredClaims` | `List<string>` | `[]` | [Reserved] JWT claim names for Bearer mode. |
| `RequiredRole` | `string?` | `null` | [Reserved] JWT role for Bearer mode. |

### Environment variable injection

```bash
# Pattern: IngestAuth__ServiceTokens__{index}__{field}
IngestAuth__Mode=ServiceToken
IngestAuth__ServiceTokens__0__Token=<openssl rand -base64 32>
IngestAuth__ServiceTokens__0__ServiceName=identity-service
IngestAuth__ServiceTokens__0__Enabled=true
IngestAuth__ServiceTokens__1__Token=<openssl rand -base64 32>
IngestAuth__ServiceTokens__1__ServiceName=fund-service
IngestAuth__ServiceTokens__1__Enabled=true
```

---

## Status Code Reference

| Code | Trigger | Body |
|------|---------|------|
| **401 Unauthorized** | `x-service-token` absent | `"Missing required header: x-service-token."` |
| **401 Unauthorized** | Token invalid or registry empty | `"Authentication failed. Verify your service token..."` |
| **403 Forbidden** | Token valid, `x-source-system` not in allowlist | `"Source system '...' is not in the configured allowlist."` |
| **403 Forbidden** | Token valid, `x-source-system` absent when allowlist configured | `"Header x-source-system is required when source allowlist is configured."` |

All error bodies follow the `ApiResponse<T>` envelope:
```json
{ "success": false, "message": "...", "traceId": "...", "data": null, "errors": [] }
```

---

## Extension Points

### Adding a new auth mode (e.g. JWT Bearer)

The system is designed for zero-middleware-change extensibility:

**Step 1 — Implement `IIngestAuthenticator`:**

```csharp
// Services/JwtIngestAuthenticator.cs
public sealed class JwtIngestAuthenticator : IIngestAuthenticator
{
    public string Mode => "Bearer";

    public Task<AuthResult> AuthenticateAsync(IHeaderDictionary headers, CancellationToken ct = default)
    {
        // 1. Extract Authorization: Bearer <token>
        // 2. Validate JWT (signature, expiry, issuer, audience)
        // 3. Extract service identity from claims
        // 4. Return AuthResult(Succeeded: true, ServiceName: claimValue)
    }
}
```

**Step 2 — Register in Program.cs (one line added):**

```csharp
builder.Services.AddSingleton<JwtIngestAuthenticator>();

builder.Services.AddSingleton<IIngestAuthenticator>(sp => ingestAuthMode switch
{
    "ServiceToken" => sp.GetRequiredService<ServiceTokenAuthenticator>(),
    "Bearer"       => sp.GetRequiredService<JwtIngestAuthenticator>(),   // ← add this
    _              => sp.GetRequiredService<NullIngestAuthenticator>(),
});
```

**Step 3 — Configure:**

```json
"IngestAuth": { "Mode": "Bearer", "RequiredRole": "platform-audit-ingest" }
```

No changes to `IngestAuthMiddleware`, `AuditEventIngestController`, validators, or services.

---

### Planned future modes

| Mode | Description |
|------|-------------|
| `"Bearer"` | JWT from any OIDC provider. Validate via `Microsoft.IdentityModel`. |
| `"MtlsHeader"` | Client cert forwarded by Nginx/Envoy in an `X-Forwarded-Client-Cert` header. |
| `"MeshInternal"` | SPIFFE identity from Istio/Linkerd service mesh. Trust-on-network — no shared secrets. |

---

## `ServiceAuthContext`

After successful auth, `IngestAuthMiddleware` stores a `ServiceAuthContext` in `HttpContext.Items`:

```csharp
public sealed class ServiceAuthContext
{
    public const string ItemKey = "IngestAuth.ServiceAuthContext";

    public string  ServiceName   { get; init; }  // "identity-service"
    public string? SourceSystem  { get; init; }  // from x-source-system header
    public string? SourceService { get; init; }  // from x-source-service header
    public string  AuthMode      { get; init; }  // "ServiceToken"
}
```

Controllers access it via:
```csharp
var ctx = HttpContext.Items[ServiceAuthContext.ItemKey] as ServiceAuthContext;
```

Available in all request handlers within the `/internal/audit/*` path scope.

---

## Files Created / Changed

| File | Change |
|------|--------|
| `Configuration/ServiceTokenEntry.cs` | **New** — named token registry entry record |
| `Configuration/IngestAuthOptions.cs` | **Updated** — added `ServiceTokens`, `RequireSourceSystemHeader`; documented all modes |
| `Services/IIngestAuthenticator.cs` | **New** — auth interface + `AuthResult` record |
| `Services/AuthResult.cs` | Part of `IIngestAuthenticator.cs` — pre-built result constants |
| `Services/ServiceAuthContext.cs` | **New** — request-scoped auth identity carrier |
| `Services/IngestAuthHeaders.cs` | **New** — centralized header name constants |
| `Services/NullIngestAuthenticator.cs` | **New** — dev pass-through (Mode = "None") |
| `Services/ServiceTokenAuthenticator.cs` | **New** — production token authenticator with constant-time comparison |
| `Middleware/IngestAuthMiddleware.cs` | **New** — path-scoped auth middleware |
| `Program.cs` | **Updated** — singleton registrations + factory switch + middleware pipeline |
| `appsettings.json` | **Updated** — `ServiceTokens: []`, `RequireSourceSystemHeader`, `AllowedSources` |
| `appsettings.Development.json` | **Updated** — dev token entries (Mode remains "None") |
| `Docs/ingest-auth.md` | **New** — operator reference: flow, headers, modes, extension guide |
| `README.md` | **Updated** — new endpoint table, auth quick reference, production checklist |
| `analysis/step12_ingest_auth.md` | **New** — this report |

---

## Security Properties

| Property | Value |
|----------|-------|
| Constant-time comparison | ✅ `CryptographicOperations.FixedTimeEquals` |
| Full-registry scan (no early exit) | ✅ Response time independent of match position |
| Length normalization before comparison | ✅ Prevents token length inference from timing |
| Per-service revocation | ✅ `Enabled: false` on individual entries |
| Per-service rotation | ✅ Add new entry → deploy → remove old entry |
| No shared master secret | ✅ Each service has its own token |
| Dev mode clearly warned | ✅ `WARNING` logged at startup when `Mode = "None"` |
| Zero-token warning | ✅ `WARNING` logged when `ServiceTokens` is empty in ServiceToken mode |

---

## Build Status

- PlatformAuditEventService: ✅ 0 errors, 0 warnings
