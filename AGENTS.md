# AGENTS.md

Guidance for coding agents working in this repository. Follow these instructions unless a more specific `AGENTS.md` exists in a subdirectory.

## Project Overview

LegalSynq is a multi-tenant SaaS platform for legal, healthcare, funding, workflow, billing, and support operations. It is a mixed .NET/Node monorepo with:

- ASP.NET Core Minimal API microservices targeting `net10.0`.
- A YARP gateway at `apps/gateway` that fronts backend API traffic.
- Two main Next.js frontends: tenant portal in `apps/web` and operator control center in `apps/control-center`.
- A separate Flow workflow backend and a separate Flow frontend under `apps/services/flow`.
- Shared C# contracts/building blocks under `shared`.

The root `README.md` and service READMEs are useful orientation docs. If code and README disagree, trust current code and package/project files.

## Repository Map

- `LegalSynq.sln`: main solution, but not every service is included.
- `apps/gateway`: YARP reverse proxy, port `5010`.
- `apps/web`: tenant portal, browser-facing port `5000` through `scripts/dev-proxy.js`; internal Next port `3050` in full dev startup.
- `apps/control-center`: platform admin portal, port `5004`.
- `apps/services/identity`: auth, tenants/users/orgs/products/RBAC, port `5001`.
- `apps/services/tenant`: canonical tenant registry and branding, port `5005`.
- `apps/services/careconnect`: referrals/providers/appointments, port `5003`.
- `apps/services/liens`: SynqLien lifecycle and marketplace, port `5002`.
- `apps/services/fund`: SynqFund funding applications, port `5008`.
- `apps/services/documents`: document storage/scanning/access, port `5006`.
- `apps/services/notifications`: notification delivery, port `5025`.
- `apps/services/audit`: audit event log, port `5007`.
- `apps/services/monitoring`: service health and alerts, port `5020`.
- `apps/services/reports`: report templates/execution/export, port `5029`.
- `apps/services/flow`: workflow engine/task management; backend is a separate boundary.
- `apps/services/task`: platform task service.
- `apps/services/support`: support case management, separate service boundary.
- `apps/services/commerce`: commerce/billing integration, separate solution boundary.
- `apps/services/tenant-billing` and `apps/services/tenant-billing-api`: billing service boundaries.
- `shared/contracts`: dependency-light DTOs/constants.
- `shared/building-blocks`: shared auth/context/middleware/commerce helpers.
- `shared/audit-client`: typed audit event HTTP client.
- `scripts`: development, deployment, startup, DB, and smoke-test scripts.
- `analysis`, `exports`, `publish`, `dist`, `artifacts`: generated or support material. Avoid changing these unless the task explicitly targets them.

## Scoped Instructions

There is a scoped instruction file at `apps/services/flow/frontend/AGENTS.md`. Read it before changing anything under that directory. It warns that the installed Next.js version has breaking changes and that relevant docs in `node_modules/next/dist/docs/` should be checked before editing Next.js code there.

## Package and Runtime Rules

- The root package manager is declared as `pnpm@10.26.1`.
- Prefer `pnpm` for Node dependency installation and scripts. Do not introduce a new package manager or regenerate lockfiles casually.
- The repo currently contains both `pnpm-lock.yaml` and `package-lock.json`; preserve existing lockfiles unless dependency changes require deliberate updates.
- Root `node_modules` is used by the main frontends. `apps/control-center/README.md` specifically warns not to create a duplicate local `node_modules` there because duplicate React can break hooks.
- Main frontends use React `18.3.x` with Next `16.2.6` according to current `package.json` files, even where older README text says Next 15.
- `apps/services/flow/frontend` uses React 19 and Next 16 and has its own scoped rules.
- Backend projects target `net10.0`, but many package references are ASP.NET/EF Core 8.x. Do not "normalize" framework/package versions unless the task is specifically about dependency upgrades.

## Common Commands

Install Node dependencies:

```bash
pnpm install
```

Run the full local development stack:

```bash
bash scripts/run-dev.sh
```

Stop the local development stack:

```bash
bash scripts/stop-dev.sh
```

Run only the tenant portal:

```bash
pnpm --dir apps/web dev
```

Run only the control center:

```bash
pnpm --dir apps/control-center dev
```

Build/type-check/test the tenant portal:

```bash
pnpm --dir apps/web type-check
pnpm --dir apps/web build
pnpm --dir apps/web test
pnpm --dir apps/web test:e2e
```

Build/type-check/test the control center:

```bash
pnpm --dir apps/control-center type-check
pnpm --dir apps/control-center build
pnpm --dir apps/control-center test
```

Build the main .NET solution:

```bash
dotnet restore LegalSynq.sln
dotnet build LegalSynq.sln --no-restore
```

Run targeted .NET tests:

```bash
dotnet test apps/services/identity/Identity.Tests/Identity.Tests.csproj
dotnet test apps/services/identity/Identity.Api.Tests/Identity.Api.Tests.csproj
dotnet test apps/services/liens/Liens.Api.Tests/Liens.Api.Tests.csproj
dotnet test apps/services/flow/backend/tests/Flow.UnitTests/Flow.UnitTests.csproj
dotnet test apps/services/flow/backend/tests/Flow.IntegrationTests/Flow.IntegrationTests.csproj
dotnet test apps/services/reports/tests/Reports.Api.Tests/Reports.Api.Tests.csproj
dotnet test shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/BuildingBlocks.Tests.csproj
```

Build service boundaries not fully covered by `LegalSynq.sln` when touching them:

```bash
dotnet build apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj
dotnet build apps/services/reports/src/Reports.Api/Reports.Api.csproj
dotnet build apps/services/support/Support.Api/Support.Api.csproj
dotnet build apps/services/commerce/src/Commerce.Api/Commerce.Api.csproj
dotnet build apps/services/tenant-billing/src/Billing.Api/Billing.Api.csproj
dotnet build apps/services/tenant-billing-api/src/TenantBilling.Api/TenantBilling.Api.csproj
```

## Local Dev Startup Notes

`scripts/run-dev.sh` contains important operational behavior:

- Tenant portal is served to browsers at `http://localhost:5000` through `scripts/dev-proxy.js`.
- The tenant portal's internal Next.js process runs on port `3050`.
- Control center runs on `http://localhost:5004`.
- Gateway runs on `http://localhost:5010`.
- Some services are restored/built outside `LegalSynq.sln` because they are separate boundaries.
- The script uses low-memory .NET settings and staggered service startup to avoid OOM and database connection storms.
- It deliberately pins the main frontends to the Next 16 binary in the pnpm store.

If changing startup, ports, service URLs, or frontend dev behavior, read `scripts/run-dev.sh`, `scripts/stop-dev.sh`, and the relevant app README first.

## Backend Architecture Rules

- Services are intended to remain independently startable. Do not add direct database or EF context dependencies across services.
- Shared libraries must stay additive. A service should not be forced to adopt a shared feature unless explicitly wired in.
- Browser-to-backend calls should flow through frontend BFF route handlers and then the gateway. Do not expose raw JWT handling to browser client code.
- Gateway validates JWTs and routes requests, but downstream services must continue validating JWTs independently.
- Tenant/user/correlation context is propagated through headers and JWT-derived context helpers. Preserve this when adding HTTP clients or endpoint handlers.
- Internal service-to-service calls generally bypass the gateway and use direct HTTP with service tokens/provisioning secrets.
- For EF changes, update the correct service DbContext and create migrations in that service's infrastructure/migrations location. Do not put one service's schema change in another service.
- Table prefix conventions matter. For example, Identity uses `idt_` tables; check each service's existing migrations/configuration before adding tables.

## Frontend Architecture Rules

- `apps/web` and `apps/control-center` use the Next.js App Router, TypeScript, React 18, and Tailwind.
- Use server route handlers as BFF endpoints for backend access. Client code should prefer relative `/api/...` paths.
- Session/auth uses the `platform_session` HttpOnly cookie. Do not add client-side JWT decoding/storage.
- For protected server components/routes, use existing auth/session helpers such as `requirePlatformAdmin()` in control center and existing session providers/guards in the tenant portal.
- Respect existing product route groups in `apps/web/src/app/(platform)`.
- Keep frontend API types and service mappers aligned with backend DTOs. Avoid hand-waving response shapes.
- When editing Next.js code, check the installed Next version and local docs if behavior is version-sensitive.

## Product and Auth Model

Important product codes:

- `SYNQLIEN`: medical lien lifecycle, marketplace, servicing.
- `SYNQFUND`: funding application workflow.
- `CARECONNECT`: healthcare referral network and appointments.

Common roles include:

- `PlatformAdmin`: platform-wide operator/admin access.
- `TenantAdmin`: tenant-level user/group/access management.
- `SYNQLIEN_SELLER`, `SYNQLIEN_BUYER`, `SYNQLIEN_HOLDER`.
- `SYNQFUND_REFERRER`, `SYNQFUND_FUNDER`.
- `CARECONNECT_REFERRER`, `CARECONNECT_RECEIVER`.

Before adding permissions, product checks, or role gates, inspect existing policy/role helpers in `shared/building-blocks`, Identity, and the relevant frontend `lib` folder.

## Data, Secrets, and Config

- Do not commit `.env.local`, secrets, connection strings, JWT signing keys, service tokens, AWS credentials, SendGrid keys, Twilio keys, or production URLs unless they are already committed placeholders.
- Use `appsettings.Development.json` only for safe development defaults.
- Keep service-token config names and environment variable names consistent with existing code and startup scripts.
- For systemd/deployment env var changes, note gateway README guidance: YARP cluster/destination override keys should be underscore-only when used with `EnvironmentFile=`.

## Verification Expectations

Always run the narrowest meaningful validation for the files you changed:

- C# application/service logic: `dotnet test` for the closest test project, plus `dotnet build` for the affected API/project.
- Shared C# libraries: build the library and run corresponding shared tests.
- EF/model changes: build the affected API/infrastructure project and verify migrations/snapshot changes are intentional.
- Tenant portal UI or BFF changes: `pnpm --dir apps/web type-check` and relevant `test`/`build`/`test:e2e` as appropriate.
- Control center UI or BFF changes: `pnpm --dir apps/control-center type-check` and relevant `test`/`build`.
- Flow frontend changes: follow `apps/services/flow/frontend/AGENTS.md`, then run its local lint/build commands.
- Startup/deployment script changes: run the relevant script test under `scripts/tests` when possible.

If a full solution build or full stack startup is too slow, memory-heavy, or requires external services, say exactly what targeted checks were run and what was not run.

## Coding Conventions

- Prefer small, targeted changes. Avoid sweeping rewrites or dependency upgrades unless asked.
- Preserve service boundaries and existing layering: `Api` endpoints/middleware, `Application` DTOs/services/interfaces, `Domain` entities/value concepts, `Infrastructure` EF/repositories/external adapters.
- Keep nullable annotations meaningful in C#; do not silence nullability warnings without understanding the data flow.
- Prefer constructor injection and existing DI registration patterns.
- Use existing exception/response patterns in each service rather than introducing new ad hoc envelopes.
- Keep DTOs explicit and stable; avoid leaking EF entities through API responses.
- In TypeScript, keep BFF/server-only logic out of client components. Use existing `lib` service modules and mappers.
- Avoid generated artifact churn. Do not edit `bin`, `obj`, `.next`, `node_modules`, Playwright reports, screenshots, or build outputs.

## Git and Worktree Hygiene

- The worktree may contain user changes. Do not revert, reset, or overwrite files you did not change.
- Check `git status --short` before and after substantial edits.
- Do not run destructive git commands unless explicitly requested.
- When adding files, place instructions at the narrowest useful scope. Root `AGENTS.md` covers the whole repo; subdirectory `AGENTS.md` files override it for their subtree.
