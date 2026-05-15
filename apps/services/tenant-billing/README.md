# Billing Service

## Status
**Source imported and internally hardened** (MS-BILL-SVC-001 → MS-BILL-SVC-003). Not yet wired to the Monk BFF or the tenant portal. .NET build validation is run in CI (no .NET SDK is installed in the Replit environment).

## Imported source origin
Donor: `tenant-billing-api` from the Billing donor archive at `/tmp/billing-analysis/extracted/services/tenant-billing-api/`. Imports were limited to that single donor service. The donor's `Commerce` service, `commerce-admin` UI, `tenant-billing-admin` UI, and React/Next libraries were **not** imported.

## Current projects
```
services/billing/
  Billing.sln
  Dockerfile                     multi-stage build → ASP.NET 8 runtime
  Directory.Build.props          TargetFramework=net8.0, central package mgmt
  Directory.Packages.props       centralised package versions
  global.json                    .NET SDK 8.0.416
  .config/dotnet-tools.json      dotnet-ef 8.0.10
  src/
    Billing.Api/                 ASP.NET Core 8 host
      Security/                  RequireInternalTokenMiddleware,
                                 PlatformTemplatesGuardAttribute
      Tenancy/                   X-Tenant-Id middleware (BFF→Billing only)
      Hosting/                   InvoiceLifecycle background job
      Controllers/               Customers, Invoices, Payments,
                                 InvoiceTemplates, Statements, StatementTemplates
    Billing.Domain/              Entities, Services, Rendering, Statements
    Billing.Infrastructure/      EF Core DbContext, Migrations, Repositories
  tests/
    Billing.Domain.Tests/        Pure domain xUnit
    Billing.Tests/               WebApplicationFactory + integration xUnit
```

## Runtime contract

### Port
**5007** is the canonical private Billing port (5005 collides with `ncm`, 5006 is also taken in the existing port plan). Override with `ASPNETCORE_URLS` at deploy time if the fleet allocates a different one.

### Environment variables

| Variable | Purpose | Required? | Notes |
|---|---|---|---|
| `BILLING_INTERNAL_TOKEN` | BFF→Billing shared secret. Enforced on every `/api/*` request. | **Yes (production)** | Service fails closed (401) on `/api/*` if unset. Health endpoints work without it. |
| `BILLING_DB_CONNECTION` | MySQL connection string for the Billing schema. | **Yes (production)** | Equivalent: `ConnectionStrings__Billing`. Falls back to InMemory when unset (smoke / tests). |
| `BILLING_RUN_MIGRATIONS` | Set to `true` to run EF migrations on startup. | No | Auto-runs in `Development`. In any other environment migrations are skipped unless this flag is set. |
| `BILLING_ENABLE_PLATFORM_TEMPLATES` | Set to `true` to expose `/api/invoice-templates/platform/*`. | No | **Default: false** (returns 404). Monk Search tenant Billing scope excludes platform templates. |
| `ASPNETCORE_ENVIRONMENT` | Standard ASP.NET environment. | No | `Development` enables Swagger UI and auto-migrations. |
| `ASPNETCORE_URLS` | Listening interface and port. | No | Container default `http://+:5007`. |

### Configuration fallbacks (`appsettings.Development.json` only)
For local development without env vars, you may set:
```json
"Billing": {
  "InternalToken": "dev-only-change-me",
  "EnablePlatformTemplates": false
}
```
**Never use this token (or any literal token) outside of local development.** The repo never carries a real production token.

## Local build & run (when a .NET 8 SDK is available)

```bash
# Restore + build
dotnet restore services/billing/Billing.sln
dotnet build   services/billing/Billing.sln --no-restore

# Run tests
dotnet test    services/billing/Billing.sln --no-build

# Run the API locally with placeholder env vars
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://127.0.0.1:5007 \
BILLING_INTERNAL_TOKEN=dev-only-change-me \
dotnet run --project services/billing/src/Billing.Api/Billing.Api.csproj

# Smoke (separate shell)
curl -s http://127.0.0.1:5007/healthz
# 200 OK

curl -s -i http://127.0.0.1:5007/api/customers
# HTTP/1.1 401 Unauthorized   ← Billing rejects unauthenticated /api/*

curl -s -i http://127.0.0.1:5007/api/customers \
  -H "X-Internal-Token: dev-only-change-me"
# HTTP/1.1 400 Bad Request    ← reaches tenant middleware, missing X-Tenant-Id
```

## Security boundaries

> **Billing.Api MUST remain a private internal service.**
>
> - Do **not** expose `Billing.Api` to browsers, public networks, or any untrusted internal caller.
> - The `X-Tenant-Id` header is an **internal BFF→Billing contract only**. It carries no authentication weight by itself; it is trusted because the only callers are inside the trust boundary that supplies `X-Internal-Token`.
> - Tenant identity is derived in the BFF from the validated IDM session (MS-BILL-BFF-005). The browser never sends `X-Tenant-Id` directly.

## Disabled by default
- `/api/invoice-templates/platform/*` — returns **404 Not Found** unless `BILLING_ENABLE_PLATFORM_TEMPLATES=true`. Monk Search tenant Billing does not expose Platform-owned invoice templates. The donor `OwnerType` enum and migrations are preserved for future internal-admin scenarios.

## OpenAPI contract

| Item | Value |
|---|---|
| Canonical contract path | `services/billing/openapi/billing-openapi.json` |
| Generation script | `services/billing/scripts/generate-openapi.sh` |
| Tooling | `Swashbuckle.AspNetCore.Cli` 6.8.1 (local tool, see `.config/dotnet-tools.json`) |
| Filters | `OpenApi/InternalTokenOperationFilter`, `Tenancy/TenantHeaderOperationFilter`, `OpenApi/HideDisabledPlatformTemplateEndpointsDocumentFilter` |
| Status | Toolchain in place (MS-BILL-SVC-004). Snapshot is generated by CI / contributor with .NET 8 SDK. The committed JSON is intentionally absent until then — see `services/billing/openapi/README.md`. |

Regenerate locally:

```bash
services/billing/scripts/generate-openapi.sh
# → services/billing/openapi/billing-openapi.json
```

> The contract documents the **internal** Billing surface — `X-Internal-Token` and `X-Tenant-Id` are BFF-injected headers. The browser must NOT call Billing.Api directly; it goes through the BFF (MS-BILL-BFF-005). Angular client generation is deferred until the BFF proxy shape is stable.

## Wiring status

| Layer | Status |
|---|---|
| BFF route (`artifacts/api-server/src/routes/billing.ts`) | **Not added** — MS-BILL-BFF-005 |
| Tenant portal navigation / page | **Not added** — MS-BILL-UI-* |
| Root `MonkSearch.sln` aggregate | **Not added** — to be decided |
| Top-level `docker-compose.yml` entry | **Not added** — service-registration prompt |
| OpenAPI contract toolchain | **Added (MS-BILL-SVC-004).** Snapshot JSON generated by `services/billing/scripts/generate-openapi.sh` in any environment with the .NET 8 SDK. |
| Angular SDK from contract | **Deferred** — until after the BFF proxy shape is stable (MS-BILL-BFF-005). |

## Future implementation prompts
- **MS-BILL-BFF-005** — Add Billing proxy to the Node BFF with session→tenant resolution and internal-token injection.
- **MS-BILL-VAL-006** — Backend tenant-isolation and end-to-end validation.
- **MS-BILL-UI-001 … MS-BILL-UI-008** — Tenant portal Angular UI buildout.

## References
- Integration plan: [`/analysis/billing/INTEGRATION-PLAN.md`](../../analysis/billing/INTEGRATION-PLAN.md)
- Donor reports index: [`/analysis/billing/donor-reports/README.md`](../../analysis/billing/donor-reports/README.md)
- Per-prompt reports:
  [MS-BILL-SVC-001](../../analysis/MS-BILL-SVC-001-report.md),
  [MS-BILL-SVC-002](../../analysis/MS-BILL-SVC-002-report.md),
  [MS-BILL-SVC-003](../../analysis/MS-BILL-SVC-003-report.md),
  [MS-BILL-SVC-004](../../analysis/MS-BILL-SVC-004-report.md)
- OpenAPI contract:
  [`services/billing/openapi/README.md`](./openapi/README.md),
  [`analysis/billing/openapi/README.md`](../../analysis/billing/openapi/README.md)
