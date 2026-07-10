---
name: Xenia P1-T1 validation defect patterns
description: Defect classes found during XENIA-P1-T1 validation — checklist to apply when implementing follow-on Xenia tickets.
---

Seven defects were found and fixed in XENIA-P1-T1. These represent recurring traps for new Xenia service layers:

## Build/compile defects

1. **Missing FrameworkReference** — Any Application-layer project that uses `HttpContext` or `IServiceCollection` directly needs `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. Without it the project builds standalone but fails when referenced. Do NOT add `Microsoft.Extensions.DependencyInjection.Abstractions` as a separate package alongside FrameworkReference — it causes NU1510.

2. **Test files missing `using Xunit;`** — xUnit types (`[Fact]`, `[Theory]`, `Assert`) are not in implicit usings for net10.0. Every new test file needs the directive explicitly.

3. **`internal sealed` types invisible to tests** — Infrastructure types are intentionally internal. Test projects need `InternalsVisibleTo` via an `AssemblyAttribute` in the Infrastructure csproj, not via a C# attribute in code.

## Runtime correctness defects

4. **Tenant context middleware must be wired** — `ITenantContextResolver` and `XeniaTenantContextAccessor` are registered in DI, but the middleware that calls `ResolveAsync()` must be added to the pipeline explicitly AFTER `UseAuthorization()`. Without it `tenantAccessor.Current` is always null.

5. **Granular auth policies** — `XeniaPermissions` defines granular constants; `XeniaPolicies` must register a named policy for each. Endpoint groups should use the narrowest policy (e.g. `ModulesRead` not `Read`). `PlatformAdmin` role satisfies all policies.

## Frontend defects

6. **CC server component session pattern** — See `cc-session-cookie-pattern.md`. Never use `session?.token`.

7. **tsconfig `.next/dev` in include** — Next.js dev server writes partial type files to `.next/dev/types/`. If tsconfig includes this directory, type-check breaks on stale generated artifacts. Keep `.next/types/**/*.ts` (build output) but exclude `.next/dev`.

## Pre-existing (for awareness)

- `apps/control-center/src/lib/__tests__/middleware-systemstatus-redirect.test.ts` references `../../middleware` which doesn't exist. This is a pre-existing gap for a CC hardening ticket, not a Xenia issue.

**Why:** All of these were introduced because XENIA-P1-T1 was written under SDK 8 (which couldn't compile net10.0). The implementer could not do a live compile check. Expect similar gaps in early Xenia tickets; always run a full build + test + type-check before marking a Xenia ticket complete.
