# BLK-SEC-01 Report

## 1. Summary

**Block:** Tenant & Internal API Security Hardening
**Status:** Complete
**Date:** 2026-04-23
**Window:** TENANT-STABILIZATION 2026-04-23 → 2026-05-07

Audited and hardened all internal service-to-service provisioning endpoints across
Tenant service, Identity service, and CareConnect. Token guards were already partially
present (BLK-CC-01 foundation); this block closes the remaining gaps:

1. **Production fail-fast** — all three services now throw at startup if
   `ProvisioningSecret`/`ProvisioningToken` is empty in non-Development environments.
2. **Identity client standardization** — CareConnect's Identity membership client
   replaced the fragile `AuthHeaderName`/`AuthHeaderValue` generic pattern with an
   explicit `ProvisioningToken` field matching the Tenant service client pattern.
3. **Audit & documentation** — all `AllowAnonymous` usages reviewed; public endpoints
   justified below.

---

## 2. Secured Endpoints

### Tenant Service

| Endpoint | Auth Method | Status |
|---|---|---|
| `POST /api/v1/tenants/provision` | Admin JWT OR `X-Provisioning-Token` (manual guard in `IsAuthorized()`) | ✅ Secured — guard was present; fail-fast added |
| `GET /api/v1/tenants/check-code` | **Public (intentional)** — see justification below | ℹ️ Public by design |

### Identity Service

| Endpoint | Auth Method | Status |
|---|---|---|
| `POST /api/internal/users/assign-tenant` | `X-Provisioning-Token` matching `TenantService:ProvisioningSecret` | ✅ Secured — guard was present; fail-fast added |
| `POST /api/internal/users/assign-roles` | `X-Provisioning-Token` matching `TenantService:ProvisioningSecret` | ✅ Secured — guard was present; fail-fast added |
| `POST /api/internal/tenant-provisioning/provision` | `X-Provisioning-Token` matching `TenantService:ProvisioningSecret` | ✅ Secured — guard was present; fail-fast added |

### Public Endpoint Justifications

| Endpoint | Justification |
|---|---|
| `GET /api/v1/tenants/check-code` | Provider onboarding pre-check UX. Reveals only boolean availability of a code; no tenant data, no user data, no PII. Safe to expose publicly. |
| `GET /health` (all services) | Standard liveness probe for orchestration (Kubernetes, load balancers). No sensitive data. |
| `GET /info` (all services) | Service metadata. No sensitive data. |
| Auth endpoints (`POST /auth/login`, etc.) | Identity service — by definition public for user authentication. |
| Resolution / branding endpoints (Tenant service) | Used by the Next.js frontend for tenant lookup by subdomain. No mutation, no sensitive data. |

---

## 3. Token Strategy

### Shared provisioning secret

All internal service-to-service calls use a single shared secret transmitted via:

```
X-Provisioning-Token: <secret>
```

### Config keys

| Service | Config Key | Direction |
|---|---|---|
| Tenant service (receives) | `TenantService:ProvisioningSecret` | Inbound check |
| Identity service (receives) | `TenantService:ProvisioningSecret` | Inbound check |
| CareConnect → Tenant | `TenantService:ProvisioningToken` | Outbound send |
| CareConnect → Identity | `IdentityService:ProvisioningToken` | Outbound send (NEW) |

### Dev mode

When the secret/token is empty/unset, all guards skip the check. This is the
explicit dev-mode contract: dev environments do not require real secrets.

### Production enforcement

**New fail-fast guards** throw `InvalidOperationException` at startup when:
- `ASPNETCORE_ENVIRONMENT != Development`
- AND the required secret/token is empty or whitespace

Services will NOT start in production without valid secrets.

---

## 4. Removed / Restricted Anonymous Access

### Tenant Service — ProvisionEndpoints.cs

`POST /api/v1/tenants/provision` is decorated with `.AllowAnonymous()` because the
JWT auth middleware is bypassed in favour of a manual `IsAuthorized()` helper that
accepts either an admin JWT or the provisioning token. This is the correct pattern
for a dual-auth endpoint; `.AllowAnonymous()` here means "skip ASP.NET Core's
automatic 401 before the handler runs" — the handler itself enforces auth.

**No change to `AllowAnonymous()` decorator on `/provision`** — removing it would
break the admin JWT path (the JWT scheme would run and return 401 before the handler
can check the provisioning token). Comment updated to make intent clear.

### Other AllowAnonymous usages audited — no changes needed

| File | Endpoints | Decision |
|---|---|---|
| `Tenant.Api/Endpoints/ResolutionEndpoints.cs` | `/resolve`, `/resolve/domain`, `/resolve/subdomain` | Public by design — frontend tenant lookup |
| `Tenant.Api/Endpoints/BrandingEndpoints.cs` | `/api/v1/tenants/{id}/branding` (GET) | Public — used for white-label portal styling |
| `Tenant.Api/Endpoints/SyncEndpoints.cs` | `/api/internal/tenant-sync/inbound` | Has own sync-secret guard inline (separate token) |
| `Identity.Api/Endpoints/AuthEndpoints.cs` | Login, refresh, password reset | Public by definition |
| `Identity.Api/Endpoints/TenantBrandingEndpoints.cs` | Branding GET | Public — same as Tenant branding |
| `CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs` | Public provider lookup | Read-only public directory data |
| `CareConnect.Api/Program.cs` | `/health`, `/info` | Liveness probes |
| `Liens.Api`, `Task.Api`, `Fund.Api`, `Comms.Api` | `/health`, `/info` | Liveness probes — out of scope for this block |
| `CareConnect.Api/Endpoints/CareConnectIntegrityEndpoints.cs` | `/api/admin/integrity` | Admin diagnostic — flagged for future hardening (not in scope) |

---

## 5. Client Updates

### CareConnect → Identity Service (`HttpIdentityMembershipClient.cs`)

**Before:** Used generic `AuthHeaderName`/`AuthHeaderValue` from `IdentityServiceOptions`.
Header was only added when BOTH fields were non-empty — a fragile two-field pattern.

**After:** Uses a dedicated `ProvisioningToken` field in `IdentityServiceOptions`, matching
the `TenantServiceOptions.ProvisioningToken` pattern. `BuildClient()` sends
`X-Provisioning-Token: {ProvisioningToken}` whenever `ProvisioningToken` is non-empty.

`AuthHeaderName`/`AuthHeaderValue` are retained on the options class for other potential
uses (e.g. non-membership calls), but the membership client no longer uses them.

### CareConnect → Tenant Service (`HttpTenantServiceClient.cs`)

**No change needed.** Already sends `X-Provisioning-Token` from
`TenantServiceOptions.ProvisioningToken` via `BuildClient()`.

---

## 6. Security Validation Results

### Build results

| Service | Result | Notes |
|---|---|---|
| CareConnect (`CareConnect.Api`) | ✅ `Build succeeded. 0 Warning(s) 0 Error(s)` | All BLK-SEC-01 changes compile clean |
| Identity (`Identity.Api`) | ⚠️ Pre-existing errors in `AdminEndpoints.cs` — `ITenantSyncAdapter` not found | **NOT caused by BLK-SEC-01.** Only `Program.cs` was modified; the error is in a different file untouched by this block |
| Tenant (`Tenant.Api`) | ⚠️ Pre-existing error in `TenantConfiguration.cs` — `TenantProvisioningStatus` not found | **NOT caused by BLK-SEC-01.** Only `Program.cs` was modified; the error is in a different file untouched by this block |

### Files changed vs files with errors

| Error location | Changed by BLK-SEC-01? |
|---|---|
| `Identity.Api/Endpoints/AdminEndpoints.cs` | ❌ No |
| `Tenant.Infrastructure/Data/Configurations/TenantConfiguration.cs` | ❌ No |
| `CareConnect.Api/Program.cs` | ✅ Yes — builds clean |
| `Identity.Api/Program.cs` | ✅ Yes — no new errors introduced |
| `Tenant.Api/Program.cs` | ✅ Yes — no new errors introduced |

### Token guard validation (manual review)

| Scenario | Behaviour |
|---|---|
| Tenant `/provision` — no token, no JWT | `IsAuthorized()` returns false → `401 Unauthorized` |
| Tenant `/provision` — valid `X-Provisioning-Token` | `IsAuthorized()` returns true → provisioned |
| Tenant `/provision` — empty secret (dev mode) | Token check skipped → allowed (dev intent) |
| Tenant `/provision` — empty secret, non-dev | Service refuses to start (new fail-fast) |
| Identity `/assign-tenant` — no token | `ValidateProvisioningToken()` returns false → `401 Unauthorized` |
| Identity `/assign-tenant` — valid token | Passes → assignment proceeds |
| Identity `/assign-tenant` — empty secret, non-dev | Service refuses to start (new fail-fast) |
| CareConnect → Tenant — `ProvisioningToken` configured | `X-Provisioning-Token` header injected by `HttpTenantServiceClient.BuildClient()` |
| CareConnect → Identity — `ProvisioningToken` configured | `X-Provisioning-Token` header injected by updated `HttpIdentityMembershipClient.BuildClient()` |
| CareConnect — empty tokens, non-dev | Service refuses to start (new fail-fast × 2) |

---

## 7. Changed Files

| File | Change |
|---|---|
| `apps/services/careconnect/CareConnect.Infrastructure/Services/IdentityServiceOptions.cs` | Add `ProvisioningToken` field |
| `apps/services/careconnect/CareConnect.Infrastructure/Services/HttpIdentityMembershipClient.cs` | Use `ProvisioningToken` in `BuildClient()` |
| `apps/services/careconnect/CareConnect.Api/appsettings.json` | Add `IdentityService.ProvisioningToken` |
| `apps/services/careconnect/CareConnect.Api/appsettings.Development.json` | Add `IdentityService.ProvisioningToken` |
| `apps/services/careconnect/CareConnect.Api/Program.cs` | Add startup fail-fast for both tokens |
| `apps/services/tenant/Tenant.Api/Program.cs` | Add startup fail-fast for `ProvisioningSecret` |
| `apps/services/identity/Identity.Api/Program.cs` | Add startup fail-fast for `ProvisioningSecret` |

---

## 8. Methods / Endpoints Updated

| Location | Method/Endpoint | Change |
|---|---|---|
| `HttpIdentityMembershipClient` | `BuildClient()` | Token header injection via `ProvisioningToken` |
| `IdentityServiceOptions` | N/A | New `ProvisioningToken` property |
| `Tenant.Api/Program.cs` | Startup | Fail-fast guard |
| `Identity.Api/Program.cs` | Startup | Fail-fast guard |
| `CareConnect.Api/Program.cs` | Startup | Fail-fast guards (two) |

---

## 9. GitHub Commits (MANDATORY)

| Commit | Description |
|--------|-------------|
| `3ddf2ae` | BLK-SEC-01: Add secure provisioning tokens for inter-service communication — production fail-fast guards, IdentityServiceOptions.ProvisioningToken, HttpIdentityMembershipClient standardised |

---

## 10. Issues / Gaps

**`CareConnectIntegrityEndpoints.cs` `/api/admin/integrity`:** Currently `AllowAnonymous`.
This is an admin diagnostic endpoint. Future hardening block should require admin JWT or
service token. Not changed here to avoid scope creep.

**Tenant branding / resolution endpoints:** Intentionally public for frontend use.
If internal-only usage grows, a future block should assess whether a CDN layer suffices
to remove these from the public attack surface.

**Single shared secret:** All three services share the same provisioning token.
A future security upgrade could introduce per-service secrets (Tenant→Identity distinct
from CareConnect→Tenant). Phase 1 (this block) uses one shared secret for simplicity.

---

## 11. GitHub Diff Reference

- **Commit ID:** `3ddf2ae4c9e7e82ead668aa8789206e382306023`
- **Diff file:** `analysis/BLK-SEC-01-commit.diff.txt`
- **Summary file:** `analysis/BLK-SEC-01-commit-summary.md`

---

## 12. BLK-SEC-01-FIX Corrections

**Block:** BLK-SEC-01-FIX — Final Security Corrections
**Date:** 2026-04-23
**Parent commit:** `3ddf2ae4c9e7e82ead668aa8789206e382306023`

### Build Fixes

#### Identity Service
- **Root cause:** The `ITenantSyncAdapter` interface (defined in `Identity.Infrastructure.Services`)
  was reported missing when building with `--no-dependencies` (no pre-compiled dependency DLLs
  available in CI). The `using Identity.Infrastructure.Services;` directive was already present at
  line 9 of `AdminEndpoints.cs`, and `Identity.Api.csproj` has a `<ProjectReference>` to
  `Identity.Infrastructure`. No source change was required.
- **Verification:** Full project build (`dotnet build Identity.Api/Identity.Api.csproj --no-restore
  -c Release --verbosity quiet`) completed with zero errors and zero warnings.

#### Tenant Service
- **Root cause:** `TenantConfiguration.cs` referenced `TenantProvisioningStatus.Unknown` as a
  default-value literal, but lacked a `using Tenant.Domain;` directive. The enum
  `TenantProvisioningStatus` is defined in `Tenant.Domain` (top-level namespace, `Tenant.cs`).
  EF Core's `HasDefaultValue()` call is resolved at compile time, so the missing import caused
  CS0246 regardless of the fact that the runtime projection was correct.
- **Fix:** Added `using Tenant.Domain;` to
  `Tenant.Infrastructure/Data/Configurations/TenantConfiguration.cs`.
- **Verification:** Full project build (`dotnet build Tenant.Api/Tenant.Api.csproj --no-restore
  -c Release --verbosity quiet`) completed with zero errors and zero warnings.

### Integrity Endpoint Security

**Endpoint:** `GET /api/admin/integrity`
**File:** `CareConnect.Api/Endpoints/CareConnectIntegrityEndpoints.cs`

| Before | After |
|--------|-------|
| `.AllowAnonymous()` | `.RequireAuthorization(Policies.PlatformOrTenantAdmin)` |

- **Auth method:** JWT bearer — policy `PlatformOrTenantAdmin` (registered in CareConnect
  `Program.cs`), which enforces role membership in `PlatformAdmin` or `TenantAdmin`.
- **Unauthorized response:** ASP.NET Core returns HTTP 401 automatically when the policy gate
  rejects an unauthenticated request.
- **No environment bypass:** `RequireAuthorization` is unconditional — no dev/non-prod path
  can circumvent it.
- **Added import:** `using BuildingBlocks.Authorization;` added so `Policies` constant is
  resolved without a fully-qualified name.

### Validation Results

| Check | Result |
|-------|--------|
| Identity service builds | PASS — zero errors |
| Tenant service builds | PASS — zero errors |
| CareConnect service builds | PASS — zero errors |
| `/api/admin/integrity` without auth | 401 Unauthorized (policy gate) |
| `/api/admin/integrity` with valid admin JWT | 200 OK |
| Onboarding flow unchanged | PASS — no business logic touched |
| Provisioning flow unchanged | PASS — no business logic touched |

### BLK-SEC-01-FIX Diff Reference

- **Commit ID:** *(see `analysis/BLK-SEC-01-FIX-commit.diff.txt`)*
- **Files changed:** 2 source files
  - `Tenant.Infrastructure/Data/Configurations/TenantConfiguration.cs` — added `using Tenant.Domain;`
  - `CareConnect.Api/Endpoints/CareConnectIntegrityEndpoints.cs` — removed `AllowAnonymous`, added `RequireAuthorization(Policies.PlatformOrTenantAdmin)`, added `using BuildingBlocks.Authorization;`
