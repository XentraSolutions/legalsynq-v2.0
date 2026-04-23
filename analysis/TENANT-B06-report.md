# TENANT-B06 — Read Switch Planning + Gateway/Web Consumer Transition Preparation

**Status:** IN PROGRESS  
**Date:** 2026-04-23  

---

## 1. Objective

Prepare and partially execute the runtime transition so the platform can safely read tenant metadata from the Tenant service instead of Identity, while preserving full rollback/fallback safety.

This block adds:
- A configurable read-source abstraction (Identity / Tenant / HybridFallback)
- Anonymous Tenant service public routes in the gateway
- A read-source-aware BFF branding endpoint in the web app
- HybridFallback mode with explicit fallback policy and observability
- Feature flags in the Tenant service for future consumer control
- A phased rollout mechanism so production cutover can be enabled safely

Identity ownership is NOT removed. Final decoupling is deferred to Block 7.

---

## 2. Codebase Analysis

### What Blocks 1–5 established
- **B1**: Standalone Tenant service, `tenant_db`, core Tenant entity, base APIs
- **B2**: `TenantBranding` entity, branding APIs, public branding endpoints (`/api/v1/public/branding/by-code/{code}`, `/api/v1/public/branding/by-subdomain/{subdomain}`)
- **B3**: `TenantDomain` entity, public resolution endpoints (`/api/v1/public/resolve/by-host`, `/api/v1/public/resolve/by-code/{code}`, `/api/v1/public/resolve/by-subdomain/{subdomain}`)
- **B4**: `TenantProductEntitlement`, `TenantCapability`, `TenantSetting`, migration dry-run
- **B5**: Migration execute, `MigrationRun` audit tables, `NoOpTenantSyncAdapter`

### Where runtime reads currently come from
- **Public branding**: `TenantBrandingProvider` (client) → `/api/identity/api/tenants/current/branding` → Identity BFF proxy → Gateway → Identity service
- **Tenant resolution**: Identity service `GET /api/tenants/current/branding` resolves tenant by `X-Tenant-Code` header or `X-Forwarded-Host`
- **Logo serving**: `/api/branding/logo/public` BFF route → Gateway → Identity branding → Documents service

### Why final Identity removal is still deferred
- Identity still contains the authoritative tenant fields
- Authentication flow in Identity depends on tenant record in Identity's DB
- Full migration execution (`tenant_MigrationRuns`) has run but production completeness is not verified
- Block 7 (final decoupling) requires confirmation that all tenants exist in Tenant service DB

---

## 3. Read-Source Design

### Abstraction / feature flags added

**Tenant service** — `TenantFeatures` config class:
- `Features:TenantReadSource` — overall default (Identity | Tenant | HybridFallback)
- `Features:TenantBrandingReadSource` — branding-specific override
- `Features:TenantResolutionReadSource` — resolution-specific override
- `Features:TenantDualWriteEnabled` — dual-write activation flag (default: false)

**Web app** — environment variable:
- `TENANT_BRANDING_READ_SOURCE` — controls the web BFF branding read path (default: `Identity`)

All defaults are `Identity` — the safest, non-breaking mode.

### Behavior per mode
| Mode | Behavior |
|---|---|
| `Identity` | Current legacy path — unchanged, calls Identity `/api/tenants/current/branding` |
| `Tenant` | Calls Tenant service `/api/v1/public/branding/by-code/{code}` directly |
| `HybridFallback` | Tries Tenant first; falls back to Identity on 404, timeout, or incomplete data |

### Fallback policy (HybridFallback)
Required fields for a "usable" Tenant branding result:
1. `tenantId` — must be non-empty
2. `code` — tenant code must be present
3. `displayName` — must be non-empty

If Tenant service returns a 404, missing fields, timeout, or transport error → fall back to Identity.
If Identity also fails → return 404 (existing behavior, no regression).

### Response shape mapping
Tenant service `PublicBrandingResponse` → web `TenantBranding`:
- `tenantId` → `tenantId`
- `code` → `tenantCode`
- `displayName` → `displayName`
- `primaryColor` → `primaryColor`
- `logoDocumentId` → `logoDocumentId`
- `logoWhiteDocumentId` → `logoWhiteDocumentId`
- (faviconDocumentId not in web type — silently ignored)

---

## 4. Consumer Transition

### Gateway — new anonymous Tenant public routes
Routes added before the catch-all `tenant-protected` route:
- `tenant-public-branding-by-code` — `GET /tenant/api/v1/public/branding/by-code/{code}` (Anonymous)
- `tenant-public-branding-by-subdomain` — `GET /tenant/api/v1/public/branding/by-subdomain/{subdomain}` (Anonymous)
- `tenant-public-resolve-by-host` — `GET /tenant/api/v1/public/resolve/by-host` (Anonymous, query param)
- `tenant-public-resolve-by-subdomain` — `GET /tenant/api/v1/public/resolve/by-subdomain/{subdomain}` (Anonymous)
- `tenant-public-resolve-by-code` — `GET /tenant/api/v1/public/resolve/by-code/{code}` (Anonymous)

### Web BFF — new `/api/tenant-branding` route
New server-side route `apps/web/src/app/api/tenant-branding/route.ts`:
- Reads `TENANT_BRANDING_READ_SOURCE` env var (default: `Identity`)
- Derives tenant code from `X-Tenant-Code` header → subdomain → `NEXT_PUBLIC_TENANT_CODE`
- Implements Identity / Tenant / HybridFallback logic
- Maps Tenant service response to `TenantBranding` shape
- Logs source used, fallback triggered, and reason

### Web `TenantBrandingProvider` — updated
Changed client-side fetch from `/api/identity/api/tenants/current/branding` → `/api/tenant-branding`.
The new endpoint is mode-aware server-side; the client remains fully source-agnostic.

### Web middleware — updated
Added `/api/tenant-branding` to `PUBLIC_PATHS` (required before session cookie).

### Tenant service — read-source diagnostics endpoint
New `GET /api/v1/admin/read-source` (AdminOnly) — returns current `TenantFeatures` config.
Allows operators to confirm active feature flag values without server restart.

---

## 5. Observability / Safety

### Logging
Every `GET /api/tenant-branding` call logs:
```json
{
  "mode": "HybridFallback",
  "source": "identity",
  "tenantCode": "acme",
  "fallbackTriggered": true,
  "fallbackReason": "not_found",
  "resolved": true
}
```

### Rollback strategy
- Set `TENANT_BRANDING_READ_SOURCE=Identity` (or remove the env var) → reverts to pre-B06 behavior instantly, no deploy required
- Tenant service `Features:TenantReadSource=Identity` in appsettings/env → preserves Identity-first behavior
- No code changes required to revert — all controlled by config

### Failure containment in HybridFallback
- Try/catch wraps every Tenant service call
- Timeout defaults to the fetch default (no hard-coded timeout added in this block — deferred)
- On any exception: log warn + trigger fallback
- On 404 from Tenant: trigger fallback (not an error — tenant may not be migrated yet)

---

## 6. Dual Write Preparation

### Current state
`ITenantSyncAdapter` / `NoOpTenantSyncAdapter` scaffolded in Block 5. All calls are no-ops.
`Features:TenantDualWriteEnabled = false` added to config — disable flag preserved.

### What remains deferred
- Actual `ITenantSyncAdapter` implementation calling Tenant service write endpoints
- Hook point in Identity `TenantService.CreateAsync` / `UpdateAsync` — requires Identity code change
- This is intentionally deferred to Block 7 after read-path confidence is established

### Identity code touched
None. Identity service is not modified in this block.

---

## 7. Implementation Summary

### Files added
| File | Description |
|---|---|
| `apps/services/tenant/Tenant.Api/Configuration/TenantFeatures.cs` | Feature flag config class + `TenantReadSource` enum |
| `apps/services/tenant/Tenant.Api/Endpoints/ReadSourceEndpoints.cs` | Admin diagnostics endpoint |
| `apps/web/src/app/api/tenant-branding/route.ts` | Read-source-aware branding BFF route |

### Files modified
| File | Change |
|---|---|
| `apps/services/tenant/Tenant.Api/appsettings.json` | Added `Features` section |
| `apps/services/tenant/Tenant.Infrastructure/DependencyInjection.cs` | Register `IOptions<TenantFeatures>` |
| `apps/services/tenant/Tenant.Api/Program.cs` | Map `ReadSourceEndpoints` |
| `apps/gateway/Gateway.Api/appsettings.json` | 5 new anonymous Tenant public routes |
| `apps/web/src/providers/tenant-branding-provider.tsx` | Changed fetch endpoint to `/api/tenant-branding` |
| `apps/web/src/middleware.ts` | Added `/api/tenant-branding` to PUBLIC_PATHS |

### Feature flags added
| Flag | Location | Default | Description |
|---|---|---|---|
| `Features:TenantReadSource` | Tenant `appsettings.json` | `Identity` | Overall default read source |
| `Features:TenantBrandingReadSource` | Tenant `appsettings.json` | `Identity` | Branding-specific override |
| `Features:TenantResolutionReadSource` | Tenant `appsettings.json` | `Identity` | Resolution-specific override |
| `Features:TenantDualWriteEnabled` | Tenant `appsettings.json` | `false` | Dual-write activation gate |
| `TENANT_BRANDING_READ_SOURCE` | Web env var | `Identity` | Web BFF branding mode |

---

## 8. Validation Results

_To be completed after implementation._

---

## 9. Known Gaps / Deferred Items

- Fetch timeout for Tenant service calls in HybridFallback mode — deferred (use platform default)
- In-memory counters / metrics for read-source telemetry — deferred (logs cover observability for now)
- Logo route (`/api/branding/logo/public`) read-source transition — deferred to B07 (low-risk, logo is a secondary fallback)
- Identity hook points for dual-write — deferred to B07
- Full cutover decision — deferred after production validation of HybridFallback behavior

---

## 10. Next Recommended Block

**BLOCK 7 — Identity Decoupling + Final Runtime Source Transition**
- Switch default `TENANT_BRANDING_READ_SOURCE` to `HybridFallback` after validating production
- Implement `ITenantSyncAdapter` with real writes
- Add Identity dual-write hook points (disabled by default, opt-in per env)
- Verify all tenants exist in Tenant DB before final cutover
- Remove Identity from branding bootstrap once confidence is established
