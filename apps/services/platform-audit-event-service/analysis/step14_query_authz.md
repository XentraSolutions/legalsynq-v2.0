# Step 14 — Query Authorization Foundations

**Service**: Platform Audit/Event Service  
**Date**: 2026-03-30  
**Build status**: Verified — 0 errors, 0 warnings (see verification section)

---

## Objective

Establish a portable, provider-neutral authorization layer for all query API endpoints (`/audit/*`). The layer must:

- Support six conceptual caller scopes aligned to real organizational security boundaries.
- Enforce tenant isolation: non-platform callers cannot access cross-tenant records.
- Enforce self-scope: user-self callers can only see their own records.
- Apply visibility filtering based on caller scope.
- Not hardcode any identity provider — claims, roles, and token format are all configurable.

---

## Scope Model

```
CallerScope:

  Unknown        (0) — unresolved, all access denied
  UserSelf       (1) — sees only own records (actorId = UserId claim)
  TenantUser     (2) — sees User-scope records within own tenant
  Restricted     (3) — compliance/read-only; sees Tenant-scope and below
  OrganizationAdmin (4) — sees Org-scope and below in own org
  TenantAdmin    (5) — sees all within own tenant (Tenant + Org + User)
  PlatformAdmin  (6) — cross-tenant; sees Platform + Tenant + Org + User
```

Scope is numeric so `>` comparisons can determine privilege ordering when needed. Higher value = more privileged.

---

## Component Architecture

```
HTTP Request
    │
    ▼
QueryAuthMiddleware
    ├── calls IQueryCallerResolver.ResolveAsync()
    │       ├── AnonymousCallerResolver (Mode=None)
    │       └── ClaimsCallerResolver   (Mode=Bearer)
    │                │
    │                ▼
    │           reads HttpContext.User.Claims
    │           maps claim names from QueryAuthOptions
    │           resolves CallerScope from role lists
    │           returns QueryCallerContext
    │
    ├── if Mode≠None and Scope=Unknown → 401
    └── stores IQueryCallerContext in HttpContext.Items
            │
            ▼
        Controller action
            ├── reads IQueryCallerContext from Items
            ├── calls IQueryAuthorizer.Authorize(caller, query)
            │       ├── Phase 1: access check (Unknown, cross-tenant, no TenantId)
            │       └── Phase 2: constraint application (TenantId, OrgId, ActorId, MaxVisibility)
            │
            ├── if denied → 401 or 403 with structured error body
            └── if allowed → query is already constrained; call IAuditEventQueryService
```

---

## Enforcement Behavior

### Phase 1 — Access gate

| Condition | HTTP Status |
|---|---|
| Scope = Unknown, unauthenticated | 401 |
| Scope = Unknown, authenticated | 403 |
| Scope = UserSelf, no UserId claim | 403 |
| Non-PlatformAdmin, query.TenantId ≠ caller.TenantId | 403 |
| EnforceTenantScope=true, non-PlatformAdmin, no TenantId | 403 |

### Phase 2 — Constraint application

All mutations happen in-place on the `AuditEventQueryRequest` before it is passed to the query service:

| Scope | TenantId | OrgId | ActorId | MaxVisibility floor |
|---|---|---|---|---|
| PlatformAdmin | unchanged | unchanged | unchanged | none |
| TenantAdmin | → caller.TenantId | unchanged | unchanged | Tenant(2) |
| Restricted | → caller.TenantId | unchanged | unchanged | Tenant(2) |
| OrganizationAdmin | → caller.TenantId | → caller.OrganizationId | unchanged | Organization(3) |
| TenantUser | → caller.TenantId | unchanged | unchanged | User(4) |
| UserSelf | → caller.TenantId | unchanged | → caller.UserId | User(4) |

`MaxVisibility` is set to `max(scope_floor, existing_value)` — the more restrictive of the scope's minimum and any value the caller already sent. This allows callers to request a more restrictive window (e.g. "I only want User-scope records") but prevents them from requesting a more permissive window than their scope allows.

---

## Key Design Decisions

### 1. Query mutation over separate filtering

The authorizer mutates the `AuditEventQueryRequest` in place rather than applying a separate filter predicate in the repository. This approach:

- Keeps the repository clean (one path, one query model).
- Makes constraint application auditable in one place (`QueryAuthorizer`).
- Means controller/service code doesn't need to know the caller's scope — constraints are invisible by the time the query executes.

### 2. Provider-neutral claim resolution

`ClaimsCallerResolver` reads claim names from `QueryAuthOptions` instead of hardcoding standard names like `"http://schemas.microsoft.com/ws/2008/06/identity/claims/role"`. This means:

- Auth0, Entra ID, Keycloak, custom OIDC — all work by changing config, not code.
- A future `KeycloakCallerResolver` (for `realm_access.roles` parsing) can be slotted in without touching the middleware or authorizer.

### 3. Middleware resolves, controller enforces

The middleware does only two things: resolve the caller context and check that Mode≠None callers are at least authenticated (401 gate). Fine-grained 403 logic stays in the controller where HTTP response semantics are clear and where logging has full request context.

### 4. Singleton resolvers and authorizer

Both resolvers and the authorizer are registered as singletons. They hold no request state — all per-request data flows through method parameters or `HttpContext`. This avoids allocation overhead on hot query paths.

### 5. Caller context fallback in controller

The `AuthorizeQuery` helper falls back to `QueryCallerContext.Anonymous()` if the Items entry is missing (e.g. in tests that bypass middleware). In production, the middleware always sets it before the controller runs.

---

## Visibility Scope Interaction

The `VisibilityScope` enum uses ordered values where **higher = more restricted**:

```
Platform(1) → Tenant(2) → Organization(3) → User(4) → Internal(5)
```

`MaxVisibility` in the query represents the **floor**: records with `VisibilityScope >= MaxVisibility` are returned. So `MaxVisibility=Tenant(2)` returns `{Tenant, Organization, User}` records but excludes `Platform`.

The scope → floor mapping:

```
PlatformAdmin    → null (no floor; Platform(1) and above)
TenantAdmin      → Tenant(2)
Restricted       → Tenant(2)
OrganizationAdmin → Organization(3)
TenantUser       → User(4)
UserSelf         → User(4)  + actorId constraint
```

`Internal(5)` records are excluded at the repository layer independent of `MaxVisibility`.

---

## Future Integration Guidance

### JWT Validation

This step provides the authorization layer on top of a resolved `ClaimsPrincipal`. JWT signature validation is intentionally out-of-scope here — add it by registering `AddJwtBearer()` in Program.cs with the appropriate authority URL and audience. The `ClaimsCallerResolver` will read the resulting claims without changes.

### API Key Mode

Implement `ApiKeyCallerResolver : IQueryCallerResolver` that:
1. Reads a header (e.g. `X-Api-Key`).
2. Looks up the key in a store (DB or config) to retrieve associated scope, tenantId, etc.
3. Returns a `QueryCallerContext`.

Register it in Program.cs under `Mode = "ApiKey"`.

### mTLS / Certificate Auth

Similar pattern — implement `MtlsCallerResolver` that reads `HttpContext.Connection.ClientCertificate`, maps the certificate subject or thumbprint to a caller context.

### Keycloak Nested Roles

Keycloak encodes roles as `{ realm_access: { roles: ["role-a"] } }` rather than flat claims. Implement `KeycloakCallerResolver` that deserializes this JSON claim and feeds the role list into the same scope resolution logic from `ClaimsCallerResolver` (or extract the resolution logic into a shared `ScopeResolver` helper).

### HIPAA Audit Trail on Authorization Decisions

For HIPAA compliance, every access denial (403) should be recorded as an audit event in the ingest pipeline. Add an `IAuthorizationEventEmitter` that calls the ingest service internally on denial. This keeps the authorization layer itself auditable.

### Attribute-level Redaction

`AuditEventRecordMapper` already supports a `redactNetworkIdentifiers` flag. Future steps can use the caller's scope to determine whether to redact network identifiers (IP, user agent) before returning records to lower-privilege callers.

---

## Files Delivered

| File | Type | Purpose |
|---|---|---|
| `Authorization/CallerScope.cs` | New | Scope enum (6 values, ordered by privilege) |
| `Authorization/IQueryCallerContext.cs` | New | Per-request caller identity interface |
| `Authorization/QueryCallerContext.cs` | New | Concrete implementation + static factory helpers |
| `Authorization/IQueryCallerResolver.cs` | New | Resolver contract |
| `Authorization/AnonymousCallerResolver.cs` | New | Dev-mode resolver (Mode=None → PlatformAdmin) |
| `Authorization/ClaimsCallerResolver.cs` | New | Bearer mode resolver (claims → CallerScope) |
| `Authorization/QueryAuthorizationResult.cs` | New | Authorization decision carrier (pass/deny + HTTP status) |
| `Authorization/IQueryAuthorizer.cs` | New | Authorizer contract |
| `Authorization/QueryAuthorizer.cs` | New | Phase 1 + Phase 2 enforcement |
| `Middleware/QueryAuthMiddleware.cs` | New | Per-request context resolution + 401 gate |
| `Configuration/QueryAuthOptions.cs` | Updated | New role lists + claim type fields |
| `Controllers/AuditEventQueryController.cs` | Updated | Authorizer injected; `AuthorizeQuery` helper on all 7 actions |
| `Program.cs` | Updated | Resolver factory + authorizer registration + middleware wiring |
| `appsettings.json` | Updated | Full QueryAuth section with new fields |
| `appsettings.Development.json` | Updated | Claim type names added; Mode=None |
| `Docs/query-authorization-model.md` | New | Operator reference |
| `analysis/step14_query_authz.md` | New | This file |

---

## Build Verification

```
dotnet build -c Debug 2>&1 | tail -20
```

Expected: 0 errors, 0 warnings. Run after all files are in place.
