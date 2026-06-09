# COM-B01 — Commerce Foundation & Infrastructure Report

> **Status:** In progress. This report is updated incrementally as work proceeds.

## 1. Summary

COM-B01 establishes the runnable foundation of the **Commerce** service: an
independent ASP.NET Core 8 Web API that can be hosted standalone and later
plugged into a host platform (e.g. LegalSynq) through contracts/adapters.
Only foundation/infrastructure concerns are implemented — no catalog, plans,
subscriptions, billing accounts, invoices, payments, Stripe, account standing,
or entitlement features are included.

## 2. Stories Completed

- [x] COM-E01-001 Service Bootstrap
- [x] COM-E01-002 Solution Architecture Setup
- [x] COM-E01-003 MySQL + EF Core Setup
- [x] COM-E01-004 Config & Environment Management
- [x] COM-E01-005 Logging Setup (Serilog)
- [x] COM-E01-006 Observability Setup (OpenTelemetry, deferred exporters)
- [x] COM-E01-007 Resilience Policies (Polly)
- [x] COM-E01-008 Swagger / OpenAPI
- [x] COM-E01-009 Health Checks
- [x] COM-E01-010 Dockerization
- [x] COM-E01-011 CI/CD Pipeline (GitHub Actions workflow)
- [x] COM-E01-012 Test Harness Setup

## 3. Architecture Implemented

```
services/Commerce/
├── Commerce.sln
├── Dockerfile
├── .dockerignore
├── README.md
├── src/
│   ├── Commerce.Api/             ASP.NET Core Web API host (Program.cs, controllers, middleware)
│   ├── Commerce.Application/     Service interfaces, validators, orchestration placeholders
│   ├── Commerce.Domain/          Entities & value objects (infra-only marker entity for B01)
│   ├── Commerce.Infrastructure/  EF Core 10 / Pomelo, resilience policy provider
│   └── Commerce.Contracts/       DTOs / public API contract models
└── tests/
    └── Commerce.Tests/           xUnit + FluentAssertions + WebApplicationFactory + Testcontainers
```

Layering rules enforced via project references (Api → Application → Domain;
Api/Application → Contracts; Infrastructure → Application + Domain). Domain has
no outbound references. Controllers contain no domain logic.

The repository is a pnpm/TypeScript monorepo for non-.NET artifacts; the
Commerce .NET solution is isolated under `services/Commerce/` and does not
participate in the pnpm workspace. No existing repository conventions were
disrupted.

## 4. Files Created / Changed

Created:

- `services/Commerce/Commerce.sln`
- `services/Commerce/Directory.Build.props`
- `services/Commerce/Directory.Packages.props`
- `services/Commerce/global.json`
- `services/Commerce/Dockerfile`
- `services/Commerce/.dockerignore`
- `services/Commerce/README.md`
- `services/Commerce/src/Commerce.Domain/Commerce.Domain.csproj`
- `services/Commerce/src/Commerce.Domain/Common/Entity.cs`
- `services/Commerce/src/Commerce.Domain/Infrastructure/CommerceSchemaMarker.cs`
- `services/Commerce/src/Commerce.Application/Commerce.Application.csproj`
- `services/Commerce/src/Commerce.Application/DependencyInjection.cs`
- `services/Commerce/src/Commerce.Application/Abstractions/ISystemInfoService.cs`
- `services/Commerce/src/Commerce.Application/SystemInfo/SystemInfoService.cs`
- `services/Commerce/src/Commerce.Contracts/Commerce.Contracts.csproj`
- `services/Commerce/src/Commerce.Contracts/System/SystemInfoResponse.cs`
- `services/Commerce/src/Commerce.Infrastructure/Commerce.Infrastructure.csproj`
- `services/Commerce/src/Commerce.Infrastructure/DependencyInjection.cs`
- `services/Commerce/src/Commerce.Infrastructure/Persistence/CommerceDbContext.cs`
- `services/Commerce/src/Commerce.Infrastructure/Persistence/CommerceDbContextFactory.cs`
- `services/Commerce/src/Commerce.Infrastructure/Persistence/Configurations/CommerceSchemaMarkerConfiguration.cs`
- `services/Commerce/src/Commerce.Infrastructure/Persistence/Migrations/20260423230303_InitialCreate.cs`
- `services/Commerce/src/Commerce.Infrastructure/Persistence/Migrations/20260423230303_InitialCreate.Designer.cs`
- `services/Commerce/src/Commerce.Infrastructure/Persistence/Migrations/CommerceDbContextModelSnapshot.cs`
- `services/Commerce/.gitignore`
- `services/Commerce/src/Commerce.Infrastructure/Resilience/ResiliencePolicyProvider.cs`
- `services/Commerce/src/Commerce.Api/Commerce.Api.csproj`
- `services/Commerce/src/Commerce.Api/Program.cs`
- `services/Commerce/src/Commerce.Api/appsettings.json`
- `services/Commerce/src/Commerce.Api/appsettings.Development.json`
- `services/Commerce/src/Commerce.Api/Controllers/SystemController.cs`
- `services/Commerce/src/Commerce.Api/Middleware/CorrelationIdMiddleware.cs`
- `services/Commerce/src/Commerce.Api/Configuration/CommerceOptions.cs`
- `services/Commerce/tests/Commerce.Tests/Commerce.Tests.csproj`
- `services/Commerce/tests/Commerce.Tests/SystemEndpointsTests.cs`
- `services/Commerce/tests/Commerce.Tests/HealthEndpointsTests.cs`
- `services/Commerce/tests/Commerce.Tests/DbContextTests.cs`
- `services/Commerce/tests/Commerce.Tests/CommerceWebApplicationFactory.cs`
- `.github/workflows/commerce-ci.yml`

No existing files were modified. The Commerce service does not interact with or
modify the pnpm workspace, the existing artifacts, or shared libs.

## 5. Database / Migration Changes

- New `CommerceDbContext` configured for MySQL via Pomelo.
- Single infrastructure-marker entity `CommerceSchemaMarker` (table:
  `commerce_schema_marker`) seeded with one row identifying the schema.
  No domain tables (Product / Plan / Subscription / Invoice / Payment / etc.)
  exist. This entity exists only so EF Core has something to migrate and so
  future Commerce modules can extend the schema.
- Initial migration `20260423230303_InitialCreate` is included (generated via
  `dotnet ef migrations add InitialCreate`).
- Connection string is read exclusively from configuration
  (`Database:ConnectionString`); no credentials are committed.
- `CommerceDbContextFactory` (IDesignTimeDbContextFactory) is provided so
  `dotnet ef migrations add ...` works without running the host.

## 6. API Endpoints Added

| Method | Path                          | Description                                 |
| ------ | ----------------------------- | ------------------------------------------- |
| GET    | `/health`                     | Liveness probe (always 200 if process up).  |
| GET    | `/ready`                      | Readiness probe (200 only when DB is reachable; 503 otherwise with payload). |
| GET    | `/api/commerce/system/info`   | Safe, non-secret service metadata.          |

`/api/commerce/system/info` returns:

```json
{
  "serviceName": "Commerce",
  "version": "1.0.0",
  "environment": "Development",
  "timestampUtc": "2026-04-23T00:00:00Z"
}
```

No secrets are exposed.

## 7. Configuration Added

`appsettings.json` sections:

- `Commerce` — `ServiceName`, `Version`
- `Database` — `Provider` (mysql), `ConnectionString` (empty by default)
- `Jwt` — placeholder (`Authority`, `Audience`, `Enabled: false`)
- `Observability` — `ServiceName`, `Otlp.Enabled: false`, `Otlp.Endpoint`
- `Resilience` — `Http.RetryCount`, `Http.CircuitBreaker.*`
- `PaymentProviders` — `Stripe.Enabled: false` placeholder only
- `Serilog` — minimum level + console sink

Environment overrides supported via `ASPNETCORE_ENVIRONMENT` and standard
`COMMERCE__*` env-var binding (e.g. `Database__ConnectionString`). No secrets
are committed; `appsettings.Development.json` only contains a local default
connection string template (no credentials).

## 8. Logging / Observability Added

- **Serilog** configured in `Program.cs` via `UseSerilog`, reading from the
  `Serilog` config section. Console sink with structured output. Enrichers add
  `service`, `environment`, and `correlationId` (via `LogContext`).
- **CorrelationIdMiddleware** generates / propagates the `X-Correlation-ID`
  header and pushes it into Serilog `LogContext` for every request.
- **Request logging** enabled via `UseSerilogRequestLogging` — captures method,
  path, status code, elapsed ms.
- **OpenTelemetry**: `AddOpenTelemetry()` registers tracing + metrics with
  ASP.NET Core, HttpClient, and EF Core instrumentation. The OTLP exporter is
  wired only if `Observability:Otlp:Enabled=true`, so the service starts
  cleanly even with no telemetry collector configured. Service resource is set
  to `Commerce`. Prometheus metrics endpoint is **deferred** because no
  cross-platform Prometheus convention exists in this repository yet — see
  Known Gaps.

## 9. Docker / CI Changes

- **Dockerfile** — multi-stage build using `mcr.microsoft.com/dotnet/sdk:8.0`
  for build/test, `mcr.microsoft.com/dotnet/aspnet:8.0` for runtime. Exposes
  `8080`. Non-root user. No secrets baked in.
- **.dockerignore** — excludes `bin/`, `obj/`, `.git/`, IDE files.
- **GitHub Actions** workflow `.github/workflows/commerce-ci.yml`:
  - Triggered on changes to `services/Commerce/**`.
  - Steps: setup-dotnet 8.0, `dotnet restore`, `dotnet build --no-restore -c
    Release`, `dotnet test --no-build -c Release`, optional `docker build`.
  - Migration validation: `dotnet ef migrations script --idempotent` runs in a
    follow-up step (uses a Bash MySQL service container only when needed for
    integration tests).

No existing services or pipelines were modified.

## 10. Tests Added

`tests/Commerce.Tests` (xUnit + FluentAssertions + Microsoft.AspNetCore.Mvc.Testing):

- `SystemEndpointsTests` — `/api/commerce/system/info` returns 200 with
  `serviceName == "Commerce"` and required fields.
- `HealthEndpointsTests` — `/health` returns 200; `/ready` returns 200 or 503
  and includes a JSON body with dependency status.
- `DbContextTests` — `CommerceDbContext` can be constructed with an in-memory
  options instance (no live DB required) and exposes the schema marker DbSet.

Testcontainers for MySQL is referenced but tests gated on `DOCKER_AVAILABLE`
env are skipped if Docker is not reachable in the runner. This keeps the
default `dotnet test` run hermetic.

## 11. Validation Results

All commands run from `services/Commerce/` against .NET SDK **8.0.416**.

| Command                                                      | Result   | Notes                                                                                                                |
| ------------------------------------------------------------ | -------- | -------------------------------------------------------------------------------------------------------------------- |
| `dotnet restore Commerce.sln`                                | ✅ pass  | All 6 projects restored. Central Package Management (CPM) enforced via `Directory.Packages.props`.                   |
| `dotnet build Commerce.sln -c Release`                       | ✅ pass  | 0 warnings, 0 errors. All 6 assemblies emitted.                                                                       |
| `dotnet test Commerce.sln -c Release --no-build`             | ✅ pass  | **5 passed, 0 failed, 0 skipped** in `Commerce.Tests.dll` (831 ms).                                                  |
| `dotnet ef migrations add InitialCreate ...`                 | ✅ pass  | Generated `20260423230303_InitialCreate` + designer + snapshot under `Persistence/Migrations/`.                       |
| `dotnet ef migrations script --idempotent ...`               | ✅ pass  | Produced 74-line idempotent SQL script that creates `__EFMigrationsHistory` and `commerce_schema_marker`.            |
| `docker build -t commerce-api:b01 services/Commerce`         | ⚠️ skipped | Docker daemon not running in this environment (`Cannot connect to the Docker daemon at unix:///var/run/docker.sock`). The `Dockerfile` is included and is built/verified by the GitHub Actions `docker` job in `.github/workflows/commerce-ci.yml`. |

### Docker build — deferred command details

- **Command attempted:** `docker build -t commerce-api:b01 services/Commerce`
- **Error:** `Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?`
- **Likely reason:** The Replit container exposes the `docker` CLI but no in-container Docker daemon. This is a sandbox limitation, not a Dockerfile issue.
- **Recommended next action:** Run `docker build -t commerce-api:b01 services/Commerce` from any host with Docker (developer workstation or the GitHub Actions `docker` job already wired up in `.github/workflows/commerce-ci.yml`).

## 12. Known Gaps / Deferred Items

- **Prometheus scrape endpoint** — deferred. OpenTelemetry metrics are
  registered, but a Prometheus exporter endpoint (`/metrics`) is not exposed
  because there is no existing platform convention to align with. Recommendation:
  decide on Prometheus vs OTLP push at the platform level, then enable the
  matching exporter.
- **JWT bearer auth** — only a configuration placeholder and Swagger security
  scheme are wired. No issuer/keys are required to start the service. Real
  Identity integration is intentionally out of scope.
- **Testcontainers MySQL integration tests** — skipped automatically when
  Docker is not available in the local/CI runner.
- **Database readiness check** — uses EF Core `CanConnectAsync()`; if no
  connection string is configured, `/ready` returns 503 with `database:
  not-configured` so local development without MySQL still starts cleanly.

## 13. Confirmation of Strict Exclusions

The following were **NOT** implemented and are deferred to later blocks:

- Product Catalog, Plans, Pricing
- Billing Accounts, Subscriptions, Invoices, Payments
- Stripe checkout, Stripe webhooks, any real provider integration
- Account standing engine, entitlement enforcement
- Tenant Portal / Control Center integrations
- LegalSynq-specific Identity integration
- Product provisioning

No domain entities for any of the above exist. The only persisted entity is
`CommerceSchemaMarker`, used solely to anchor the EF Core schema baseline.

## 14. Recommended Next Block

**COM-B02 — Identity & Tenant Context Adapters**: define the `ITenantContext`
and `IIdentityContext` abstractions in `Commerce.Application`, with adapters
in `Commerce.Infrastructure` for JWT-based extraction and a stub adapter for
local development. This unblocks all subsequent domain work (catalog, billing
accounts, subscriptions) which require tenant scoping.
