# XENIA-P1-T1-V1 — Core Foundation Validation and Remediation Report

**Ticket:** XENIA-P1-T1-V1  
**Parent:** XENIA-P1 — Xenia Platform Foundation & Email Automation  
**Related:** XENIA-P1-T1 — Core Service Foundation  
**Date:** 2026-07-10  
**Status:** ✅ COMPLETE — 7 defects found and fixed, 20/20 tests pass, builds clean

---

## 1. Environment

| Tool | Version | Status |
|---|---|---|
| .NET SDK | 10.0.101 | ✅ net10.0 targets compile |
| Node.js | 20.20.0 | ✅ |
| pnpm | 10.26.1 | ✅ |
| EF CLI (`dotnet-ef`) | 8.0.0 | ✅ Restored via `dotnet tool restore` |
| MySQL CLI | Not in PATH | ⚠️ DB operations via dotnet ef only |

---

## 2. File Structure

All expected files present and accounted for:

```
apps/services/xenia/
  Xenia.Domain/
    Entities/   PlatformModule, TenantModuleSetting, PlatformAdapter, XeniaConfigurationEntry
    Enums/      AdapterHealthStatus, AdapterConfigurationStatus, AdapterAvailabilityStatus, ScopeType
    Events/     XeniaEventEnvelope<T>, IXeniaEventEnvelope
  Xenia.Application/
    Adapters/Interfaces/   8 platform adapter interfaces + noop impls
    Configuration/         IXeniaConfigurationService, ConfigurationEntryDto
    Events/                IEventPublisher, IEventHandler<T>
    Modules/               IModuleRegistry, ModuleDto
    TenantContext/         ITenantContextResolver, XeniaTenantContextAccessor
    DependencyInjection.cs
  Xenia.Infrastructure/
    Configuration/  EfXeniaConfigurationService
    Events/         InMemoryEventPublisher
    Modules/        EfModuleRegistry
    Platform/       8 noop adapter implementations
    Persistence/    XeniaDbContext, Migrations/20260710000001_XeniaInitial.cs, XeniaDbContextModelSnapshot
    Registry/       EfAdapterRegistry
    TenantContext/  XeniaJwtTenantContextResolver
    DependencyInjection.cs
  Xenia.Api/
    Endpoints/    XeniaModuleEndpoints, XeniaAdapterEndpoints, XeniaConfigurationEndpoints, XeniaInfoEndpoints
    Middleware/   XeniaCorrelationMiddleware, XeniaExceptionMiddleware, XeniaTenantContextMiddleware [added]
    Program.cs
  Xenia.Tests/
    Modules/        ModuleRegistryTests.cs
    Registry/       AdapterRegistryTests.cs
    TenantContext/  TenantContextTests.cs

apps/control-center/src/
  app/xenia/           page.tsx, modules/page.tsx, adapters/page.tsx, settings/...
  app/api/xenia/       [...path]/route.ts  (CC BFF proxy)
  components/xenia/    xenia-dashboard.tsx, xenia-adapters-table.tsx, xenia-modules-table.tsx
  lib/                 xenia-api.ts
```

---

## 3. Build Results

### Backend (all 5 projects)

| Project | Errors | Warnings | Result |
|---|---|---|---|
| Xenia.Domain | 0 | 0 | ✅ |
| Xenia.Application | 0 | 0 | ✅ |
| Xenia.Infrastructure | 0 | 0 | ✅ |
| Xenia.Api | 0 | 0 | ✅ |
| Xenia.Tests | 0 | 0 | ✅ |

Pre-remediation: 43 build errors across Domain (missing FrameworkReference), all test files ([Fact] not found, internal types inaccessible).

### Frontend type-check

| Scope | Result |
|---|---|
| `app/xenia/page.tsx` | ✅ Pass |
| `app/xenia/modules/page.tsx` | ✅ Pass |
| `app/xenia/adapters/page.tsx` | ✅ Pass |
| `lib/xenia-api.ts` | ✅ Pass |
| `app/api/xenia/[...path]/route.ts` | ✅ Pass |
| `components/xenia/*.tsx` | ✅ Pass |
| Pre-existing unrelated error | ⚠️ P-01 (see §7) |

---

## 4. Test Results

```
Total:   20
Passed:  20
Failed:   0
Duration: ~3.3 s
```

| Suite | Tests | Status |
|---|---|---|
| ModuleRegistryTests | 7 | ✅ All pass |
| AdapterRegistryTests | 8 | ✅ All pass |
| TenantContextTests | 5 | ✅ All pass |

**ModuleRegistryTests** covers: register module, get all, enable/disable, duplicate key throws, get non-existent returns null.  
**AdapterRegistryTests** covers: all 8 adapters seeded initially unconfigured, unavailable status, IsConfigured returns false, RecordEvent doesn't throw, Validate returns NotAvailable.  
**TenantContextTests** covers: resolver with valid/invalid/missing/empty GUID claims, unauthenticated request, accessor set/get/null guard.

---

## 5. Migration Validation

- Migration file: `Xenia.Infrastructure/Persistence/Migrations/20260710000001_XeniaInitial.cs` ✅
- Designer file: `20260710000001_XeniaInitial.Designer.cs` ✅
- Snapshot: `XeniaDbContextModelSnapshot.cs` ✅
- Hosted service: `XeniaMigrationsHostedService.cs` — calls `MigrateAsync()` on startup ✅

Migration creates 5 tables matching the EF entity configurations:

| Table | Entity |
|---|---|
| `xn_modules` | `PlatformModule` |
| `xn_tenant_modules` | `TenantModuleSetting` |
| `xn_platform_adapters` | `PlatformAdapter` |
| `xn_configuration` | `XeniaConfigurationEntry` |
| `xn_tenant_settings` | (tenant-level settings) |

Connection string: `ConnectionStrings__XeniaDb` (environment secret, not in source).  
`MigrateOnStartup` pattern consistent with all other services.

---

## 6. Architecture Review

### 6.1 Domain layer

- `Guid.CreateVersion7()` used for all primary keys (time-ordered, consistent with platform convention) ✅
- `XeniaEventEnvelope<T>` sealed record with `IXeniaEventEnvelope` marker interface — correct design for untyped dispatch ✅
- All enum discriminated states are explicit (no silent fallbacks) ✅

### 6.2 Application layer

- `IModuleRegistry`, `IAdapterRegistry`, `IXeniaConfigurationService`, `IEventPublisher` — clean interface contracts ✅
- `ITenantContextResolver` reads from `HttpContext.User` (JWT claims only, never caller headers) ✅
- `XeniaTenantContextAccessor` Scoped lifetime matches request scope ✅
- 8 platform adapter interfaces: Identity, Audit, Notification, Document, Lien, Tenant, Workflow, CareConnect ✅
- `FrameworkReference Include="Microsoft.AspNetCore.App"` present (needed for HttpContext) ✅

### 6.3 Infrastructure layer

- `EfModuleRegistry`, `EfAdapterRegistry`: `internal sealed` (correctly hidden from public surface) ✅
- `InMemoryEventPublisher`: per-handler `try/catch` — publisher never throws even if handlers fail ✅
- `XeniaJwtTenantContextResolver`: resolves `tenant_id` from verified JWT claims — no header trust ✅
- Configuration precedence chain `TenantModule → Tenant → Module → Global` correctly implemented ✅
- `InternalsVisibleTo` for `Xenia.Tests` — allows test direct access without exposing public API ✅

### 6.4 API layer

| Endpoint Group | Policy Applied | Auth Required |
|---|---|---|
| `GET /health`, `GET /ready`, `GET /info` | None | ❌ Anonymous |
| `GET /modules`, `GET /modules/{key}` | `XeniaPolicies.ModulesRead` | ✅ |
| `PUT /modules/{key}/enable`, `PUT /modules/{key}/disable` | `XeniaPolicies.ModulesManage` | ✅ |
| `GET /modules/tenant` | `XeniaPolicies.ModulesRead` | ✅ |
| `GET /adapters`, `GET /adapters/{key}` | `XeniaPolicies.AdaptersRead` | ✅ |
| `GET /configuration` | `XeniaPolicies.ConfigurationRead` | ✅ |
| `PUT /configuration` | `XeniaPolicies.ConfigurationRead` (group) + Admin escalation | ✅ |
| `GET /secure/ping` | `XeniaPolicies.Read` | ✅ |

All policies accept `PlatformAdmin` role as super-user.

- Correlation middleware: propagates `X-Correlation-Id`, generates UUID if absent ✅
- Exception middleware: returns RFC 7807 problem details without stack traces in production ✅
- Tenant context middleware: runs after `UseAuthorization()`, reads JWT claims only ✅

### 6.5 Frontend

- Server components read raw bearer token from `platform_session` cookie via `cookies()` — matches CC convention ✅
- `xenia-api.ts` DTOs match backend response shapes ✅
- CC BFF proxy forwards `Authorization` header from authenticated callers ✅
- `getXeniaAdapters`/`getXeniaModules` return empty arrays on null (safe fallback) ✅
- Dashboard shows service status, adapter health summary, and an honest "unconfigured adapters expected" note ✅

---

## 7. Pre-existing Findings (Outside Xenia Scope)

### P-01 — Missing `middleware.ts` referenced by existing test file

**File:** `apps/control-center/src/lib/__tests__/middleware-systemstatus-redirect.test.ts`  
**Error:** `TS2307: Cannot find module '../../middleware'`  
**Description:** This test covers a `/systemstatus → /status` permanent redirect. The `src/middleware.ts` file it references was never created. This is a pre-existing gap unrelated to Xenia — the file was introduced before this validation session.  
**Recommendation:** Implement `apps/control-center/src/middleware.ts` with the systemstatus redirect in a separate CC hardening ticket.

---

## 8. Defect Summary

| ID | Severity | Area | Description | Status |
|---|---|---|---|---|
| D-01 | Critical | Build | Application.csproj missing `FrameworkReference` → `HttpContext`/`IServiceCollection` build errors | ✅ Fixed |
| D-02 | Critical | Tests | All 3 test files missing `using Xunit;` → `[Fact]` unresolved | ✅ Fixed |
| D-03 | Critical | Tests | `EfModuleRegistry`/`EfAdapterRegistry` internal, invisible to test project | ✅ Fixed |
| D-04 | High | Runtime | Tenant context middleware never wired → `tenantAccessor.Current` always null | ✅ Fixed |
| D-05 | Medium | Auth | Granular permission policies defined in `XeniaPermissions` but never registered or applied | ✅ Fixed |
| D-06 | High | Frontend | Wrong session import; `session?.token` doesn't exist on `PlatformSession` | ✅ Fixed |
| D-07 | Low | Frontend | tsconfig includes `.next/dev/types` generated files causing spurious type errors | ✅ Fixed |
| P-01 | Low | Frontend | Pre-existing: missing `middleware.ts` for systemstatus redirect test (out of scope) | ⚠️ Pre-existing |

---

## 9. Remediation Details

### D-01 — Application.csproj FrameworkReference

```xml
<!-- Added -->
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>

<!-- Removed (redundant, was causing NU1510 warning) -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
```

### D-02 — Test file using directives

Added `using Xunit;` to `ModuleRegistryTests.cs`, `AdapterRegistryTests.cs`, `TenantContextTests.cs`.

### D-03 — InternalsVisibleTo

```xml
<!-- Xenia.Infrastructure.csproj -->
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
    <_Parameter1>Xenia.Tests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

### D-04 — Tenant context middleware

New file: `Xenia.Api/Middleware/XeniaTenantContextMiddleware.cs`

```csharp
public sealed class XeniaTenantContextMiddleware
{
    private readonly RequestDelegate _next;
    public XeniaTenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContextResolver resolver,
        XeniaTenantContextAccessor accessor)
    {
        var tenantContext = await resolver.ResolveAsync(context, context.RequestAborted);
        if (tenantContext is not null)
            accessor.Set(tenantContext);
        await _next(context);
    }
}
```

Wired in `Program.cs` after `UseAuthorization()`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<XeniaTenantContextMiddleware>();
```

### D-05 — Granular auth policies

Added 5 new policy constants and registrations to `Program.cs`. Endpoint groups updated:

- `/modules` group → `XeniaPolicies.ModulesRead`
- `/modules/{key}/enable|disable` → `XeniaPolicies.ModulesManage`
- `/adapters` group → `XeniaPolicies.AdaptersRead`
- `/configuration` group → `XeniaPolicies.ConfigurationRead`

### D-06 — Frontend session pattern

All three Xenia server components updated to read the raw JWT directly from the httpOnly session cookie:

```tsx
// Before (broken — getSession undefined in scope; PlatformSession has no .token)
import { getSession } from '@/lib/session';
const session = await getSession();
const token = session?.token ?? '';

// After (correct — matches CC convention used in all other components)
import { cookies } from 'next/headers';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
const jar = await cookies();
const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';
```

### D-07 — tsconfig fix

```json
// Before
"include": ["next-env.d.ts", "**/*.ts", "**/*.tsx", ".next/types/**/*.ts", ".next/dev/types/**/*.ts"]

// After
"include": ["next-env.d.ts", "**/*.ts", "**/*.tsx", ".next/types/**/*.ts"],
"exclude": ["node_modules", ".next/dev"]
```

---

## 10. Final Checklist

| Check | Result |
|---|---|
| All 5 backend projects build with 0 errors, 0 warnings | ✅ |
| 20/20 unit tests pass | ✅ |
| Tenant context middleware wired after UseAuthorization | ✅ |
| 7 auth policies registered and applied to endpoints | ✅ |
| InternalsVisibleTo configured for test project | ✅ |
| EF migration + snapshot present | ✅ |
| Xenia frontend pages type-check clean | ✅ |
| Session cookie pattern matches CC conventions | ✅ |
| `Guid.CreateVersion7()` for all new IDs | ✅ |
| No cross-service DB access | ✅ |
| No secrets in source or config | ✅ |
| Tenant resolved from JWT claims only (never caller headers) | ✅ |
| Noop adapter implementations never throw | ✅ |
| Event publisher catches per-handler failures (non-throwing) | ✅ |
| Port 5035, `ConnectionStrings__XeniaDb`, MigrateOnStartup | ✅ |

---

## 11. Recommendation

**XENIA-P1-T1 is validated and remediated.** Seven defects — three build-blocking, two high severity runtime/frontend issues, one medium auth gap, one low tsconfig issue — were found and fixed. One pre-existing out-of-scope finding (P-01) is documented.

The foundation is ready to proceed to XENIA-P1-T2 (first real module implementation). The CC middleware gap (P-01) should be tracked in a separate ticket.
