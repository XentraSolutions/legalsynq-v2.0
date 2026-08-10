---
name: Xenia Platform Foundation
description: Architecture decisions and conventions for the Xenia standalone automation service (XENIA-P1-T1).
---

## What Xenia Is

Xenia is a standalone, tenant-aware automation platform at `apps/services/xenia/`. It is intentionally isolated from LegalSynq domain logic — it accesses platform capabilities through replaceable adapter interfaces.

## Port and Connection String

- Port: **5035**
- Connection string env var: `ConnectionStrings__XeniaDb`
- Table prefix: `xn_` (xn_modules, xn_tenant_modules, xn_platform_adapters, xn_configuration, xn_tenant_settings)

## Tenant Context

Tenant context is resolved from the cryptographically signed JWT `tenant_id` claim by `JwtTenantContextResolver`. The scoped `XeniaTenantContextAccessor` stores it for the request lifetime. Middleware rejects tenant-scoped endpoints when context is missing (returns 400).

**Why:** Xenia must not trust arbitrary caller-supplied tenant IDs. Only JWT-signed claims are accepted.

## Adapter Pattern

8 adapter interfaces in `Xenia.Application/Adapters/Interfaces/`. All noop `Unavailable*Adapter` implementations in `Xenia.Infrastructure/Platform/` return `IsConfigured = false` and honest unavailable results. **Exception: `UnavailableAuditAdapter` logs events at Warning with `[AUDIT-FALLBACK]` prefix — never silently discards.**

**How to apply:** When wiring a real adapter, register a concrete implementation in `DependencyInjection.cs` before (or replacing) the corresponding `Unavailable*` registration.

## Authorization

Policies: `XeniaRead` (requires `xenia.read` permission or `PlatformAdmin` role) and `XeniaAdmin` (requires `xenia.admin` or `PlatformAdmin`). Anonymous endpoints: `/health`, `/ready`, `/info`.

## Configuration Precedence

1. Application defaults
2. Environment variables / appsettings.json
3. Global (`xn_configuration` where scope_type='Global')
4. Tenant (`scope_type='Tenant'`)
5. Module (`scope_type='Module'`)
6. TenantModule — highest precedence

## Startup Script Integration

- `run-dev.sh`: Xenia starts after Comms, before Support (late in wave — no callers among existing services)
- `run-prod.sh`: Added to `BUILD_PROJECTS`, `Xenia.Api` case sets `ASPNETCORE_URLS=http://0.0.0.0:5035`
- `_startup-helpers.sh`: `Xenia.Api` → `"Xenia"` label

## Control Center Integration

- API proxy: `apps/control-center/src/app/api/xenia/[...path]/route.ts` → `XENIA_API_BASE` (default `http://127.0.0.1:5035`)
- Pages: `/xenia` (dashboard), `/xenia/modules`, `/xenia/adapters`, `/xenia/settings`
- All gated by `requirePlatformAdmin()`
- Navigation section: `AUTOMATION` with badge `LIVE`

## Migrations

Manual migration `20260710000001_XeniaInitial` — written by hand (dotnet ef unavailable in Replit due to NETSDK1045). Applied by `XeniaMigrationsHostedService` on startup.

## No Email in Core

Email automation is excluded from XENIA-P1-T1 (belongs to XENIA-P1-T2). No email tables, adapters, or endpoints exist in the core foundation.
