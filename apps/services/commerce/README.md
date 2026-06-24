# Commerce Service

Independent, portable Commerce platform service. This block (COM-B01) implements
only the foundation/infrastructure: bootstrap, layered architecture, EF Core +
MySQL baseline, configuration, logging, observability, resilience, Swagger,
health checks, Dockerization, CI/CD, and a test harness.

No domain features (catalog, plans, subscriptions, invoices, payments,
entitlements, account standing, Stripe) are implemented yet.

## Run locally

```bash
cd services/Commerce
dotnet restore
dotnet run --project src/Commerce.Api
```

The service listens on `ASPNETCORE_URLS` (defaults to `http://localhost:5080`
locally, `http://+:8080` in container).

## Endpoints

- `GET /health` — liveness
- `GET /ready` — readiness (DB ping)
- `GET /api/commerce/system/info` — service metadata
- `GET /swagger` — OpenAPI UI (Development environment)

## Migrations

```bash
dotnet ef migrations add <Name> \
  --project src/Commerce.Infrastructure \
  --startup-project src/Commerce.Api
```

A design-time `CommerceDbContextFactory` is provided so EF tools work without
running the host.

Startup migration behavior:

- `Development`: migrations run automatically on startup.
- Non-development: migrations are skipped unless `COMMERCE_RUN_MIGRATIONS=true`.
- Production-safe manual apply:

```bash
Database__ConnectionString='Server=<host>;Port=3306;Database=<db>;User=<user>;Password=<pass>;' \
dotnet ef database update \
  --project src/Commerce.Infrastructure/Commerce.Infrastructure.csproj \
  --startup-project src/Commerce.Api/Commerce.Api.csproj
```

## Tests

```bash
dotnet test
```
