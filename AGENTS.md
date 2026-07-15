# AGENTS.md

Guidance for Codex and other coding agents working in this repository. Follow these instructions unless a more specific `AGENTS.md` exists in a subdirectory.

## Project Overview

LegalSynq is a multi-tenant SaaS platform for legal, healthcare, funding, workflow, billing, communications, automation, and support operations. It is a mixed .NET/Node monorepo with:

- ASP.NET Core Minimal API services targeting `net10.0`.
- A YARP gateway at `apps/gateway` that fronts backend API traffic.
- Two main Next.js frontends: tenant portal in `apps/web` and operator control center in `apps/control-center`.
- A separate Flow workflow backend and a separate Flow frontend under `apps/services/flow`.
- A standalone Xenia automation/assistant service under `apps/services/xenia`.
- Shared C# contracts and building blocks under `shared`.

The root `README.md` and service READMEs are useful orientation docs. If documentation and code disagree, trust current code, package files, project files, appsettings, and startup scripts.

## Agent Operating Rules

- Check `git status --short` before substantial edits and assume existing changes belong to the user.
- Do not revert, reset, overwrite, or reformat user changes unless explicitly asked.
- Use `rg` or `rg --files` first when searching.
- Read the relevant service README, package file, project file, and local patterns before changing code.
- Keep changes scoped to the requested behavior and the owning service/frontend boundary.
- Prefer existing helpers, DTOs, mappers, auth utilities, DI patterns, response shapes, and test styles.
- Run the narrowest meaningful validation for the files changed. For docs-only changes, a diff/review is enough unless the docs include generated content.
- Do not edit generated outputs such as `bin`, `obj`, `.next`, `node_modules`, Playwright reports, build artifacts, screenshots, `dist`, `publish`, or exported files unless the task explicitly targets them.

## Documentation Sync Rule

When a change affects project shape, startup behavior, ports, service boundaries, product/auth constants, public commands, runtime configuration, dependency/runtime versions, or agent behavior, update the necessary docs in the same task. Check the narrowest relevant docs first:

- Root `README.md` for product, architecture, service map, tech stack, and common command changes.
- Root `AGENTS.md` for durable Codex/agent rules, validation expectations, repo conventions, and service-boundary guidance.
- Service `README.md` files for service-local APIs, ports, migrations, environment variables, and run/test commands.
- Scoped `AGENTS.md` files when the rule only applies inside a subtree.

This repository has a project-local Codex hook at `.codex/hooks.json` that runs `scripts/check-doc-sync.py` on `SessionStart` and `Stop`. It is intentionally project-only; do not mirror it into `~/.codex`. The hook records a per-session baseline in the git-ignored `.local/state/codex-doc-sync/` directory, checks doc-sensitive changes, and blocks until the final response ends with exactly one `Documentation impact:` line using one of these formats: `Documentation impact: None — ... .`, `Documentation impact: Updated — ... .`, or `Documentation impact: EDR created/updated — ... .`. The hook then advances the session baseline so the same change set does not loop.

## Repository Map

| Path | Purpose | Local port / notes |
|---|---|---|
| `LegalSynq.sln` | Main .NET solution; some boundaries still need separate builds | Not every service is guaranteed to be covered |
| `apps/gateway` | YARP reverse proxy | `5010` |
| `apps/web` | Tenant portal, Next.js App Router | Browser `5000` through `scripts/dev-proxy.js`; internal Next `3050` in full dev startup |
| `apps/control-center` | Platform admin/operator portal | `5004` |
| `apps/services/identity` | Auth, tenants/users/orgs/products/RBAC | `5001` |
| `apps/services/fund` | SynqFund funding applications | `5002` |
| `apps/services/careconnect` | Referrals/providers/appointments/networks | `5003` |
| `apps/services/tenant` | Canonical tenant registry and branding | `5005` |
| `apps/services/documents` | Document storage/scanning/access | `5006` |
| `apps/services/audit` | Audit event log | `5007` |
| `apps/services/notifications` | Notification delivery | `5008` |
| `apps/services/liens` | SynqLien lifecycle and marketplace | `5009` |
| `apps/services/comms` | Messaging and communications service | `5011` |
| `apps/services/flow` | Workflow engine/task management; backend and separate frontend | Backend `5012` |
| `apps/services/monitoring` | Service health and alerts | `5015` |
| `apps/services/task` | Platform task service | `5016` |
| `apps/services/support` | Support case management | `5017` |
| `apps/services/reports` | Report templates/execution/export | `5029` |
| `apps/services/commerce` | Commerce/billing integration boundary | `5030` in full dev startup |
| `apps/services/tenant-billing` | Billing service boundary | `5031` in full dev startup |
| `apps/services/tenant-billing-api` | Alternate/imported tenant billing boundary | Separate boundary |
| `apps/services/xenia` | Tenant-aware automation and assistant platform | `5035` |
| `shared/contracts` | Dependency-light DTOs/constants/events | Shared library |
| `shared/building-blocks` | Auth/context/middleware/commerce helpers | Shared library |
| `shared/audit-client` | Typed audit event HTTP client | Shared library |
| `scripts` | Development, deployment, startup, DB, and smoke-test scripts | Read before changing startup behavior |
| `artifacts/api-server` | Local artifact API started by `scripts/run-dev.sh` | `5020` |
| `analysis`, `exports`, `publish`, `dist`, `artifacts` | Generated or support material | Avoid unless explicitly targeted |

## Scoped Instructions

There is a scoped instruction file at `apps/services/flow/frontend/AGENTS.md`. Read it before changing anything under that directory. It warns that the installed Next.js version has breaking changes and that relevant docs in `node_modules/next/dist/docs/` should be checked before editing Next.js code there.

## Package and Runtime Rules

- The root package manager is declared as `pnpm@10.26.1`.
- Prefer `pnpm` for Node dependency installation and scripts. Do not introduce a new package manager.
- The repo currently contains both `pnpm-lock.yaml` and `package-lock.json`; preserve existing lockfiles unless dependency changes require deliberate updates.
- Root `node_modules` is used by the main frontends. `apps/control-center/README.md` specifically warns not to create a duplicate local `node_modules` there because duplicate React can break hooks.
- Main frontends use React `18.3.x` with Next `16.2.6` according to current `package.json` files.
- `apps/services/flow/frontend` uses React 19 and Next 16 and has its own scoped rules.
- Backend projects target `net10.0`, but many ASP.NET/EF Core package references are 8.x. Do not normalize framework/package versions unless the task is specifically about dependency upgrades.

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
dotnet test apps/services/xenia/Xenia.Tests/Xenia.Tests.csproj
dotnet test shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/BuildingBlocks.Tests.csproj
```

Build service boundaries not fully covered by `LegalSynq.sln` or commonly built separately by startup scripts:

```bash
dotnet build apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj
dotnet build apps/services/reports/src/Reports.Api/Reports.Api.csproj
dotnet build apps/services/support/Support.Api/Support.Api.csproj
dotnet build apps/services/commerce/src/Commerce.Api/Commerce.Api.csproj
dotnet build apps/services/tenant-billing/src/Billing.Api/Billing.Api.csproj
dotnet build apps/services/tenant-billing-api/src/TenantBilling.Api/TenantBilling.Api.csproj
dotnet build apps/services/xenia/Xenia.Api/Xenia.Api.csproj
```

## Local Dev Startup Notes

`scripts/run-dev.sh` contains important operational behavior:

- Tenant portal is served to browsers at `http://localhost:5000` through `scripts/dev-proxy.js`.
- The tenant portal's internal Next.js process runs on port `3050`.
- Control center runs on `http://localhost:5004`.
- Gateway runs on `http://localhost:5010`.
- The artifacts API server starts on port `5020`.
- Some services are restored/built outside `LegalSynq.sln` because they are separate or memory-sensitive boundaries.
- The script uses low-memory .NET settings and staggered service startup to avoid OOM and database connection storms.
- It deliberately pins the main frontends to the Next 16 binary in the pnpm store.

If changing startup, ports, service URLs, frontend dev behavior, or gateway routing, read `scripts/run-dev.sh`, `scripts/stop-dev.sh`, `apps/gateway/Gateway.Api/appsettings.json`, and the relevant app README first.

## Backend Architecture Rules

- Services are intended to remain independently startable. Do not add direct database or EF context dependencies across services.
- Shared libraries must stay additive. A service should not be forced to adopt a shared feature unless explicitly wired in.
- Browser-to-backend calls should flow through frontend BFF route handlers and then the gateway. Do not expose raw JWT handling to browser client code.
- Gateway validates JWTs and routes requests, but downstream services must continue validating JWTs independently.
- Tenant/user/correlation context is propagated through headers and JWT-derived context helpers. Preserve this when adding HTTP clients or endpoint handlers.
- Internal service-to-service calls generally bypass the gateway and use direct HTTP with service tokens/provisioning secrets.
- Flow is a separate workflow boundary. Keep workflow orchestration contracts explicit and avoid leaking one service's EF/domain model into Flow.
- Xenia is a standalone automation platform. Keep its core independent from LegalSynq-specific domain logic and use adapter interfaces for platform capabilities.
- Product-specific assistant tools must be owned by the product/service that owns the domain data and exposed through a dedicated assistant-tools API surface in that service. Xenia may orchestrate tool selection and aggregate results for the UI, but it must not implement product-domain lookup composition by calling user-facing product APIs directly.
- For EF changes, update the correct service DbContext and create migrations in that service's infrastructure/migrations location. Do not put one service's schema change in another service.
- Table prefix conventions matter. For example, Identity uses `idt_` tables and Xenia uses `xn_` tables; check existing migrations/configuration before adding tables.

## Frontend Architecture Rules

- `apps/web` and `apps/control-center` use the Next.js App Router, TypeScript, React 18, and Tailwind.
- Use server route handlers as BFF endpoints for backend access. Client code should prefer relative `/api/...` paths.
- Session/auth uses the `platform_session` HttpOnly cookie. Do not add client-side JWT decoding/storage.
- For protected server components/routes, use existing auth/session helpers such as `requirePlatformAdmin()` in control center and existing session providers/guards in the tenant portal.
- Respect existing product route groups in `apps/web/src/app/(platform)`.
- Keep frontend API types and service mappers aligned with backend DTOs. Avoid hand-waving response shapes.
- When editing Next.js code, check the installed Next version and local docs if behavior is version-sensitive.

## Product and Auth Model

Canonical product codes are defined in `shared/building-blocks/BuildingBlocks/Authorization/ProductCodes.cs`:

| Code | Product |
|---|---|
| `SYNQ_CARECONNECT` | CareConnect |
| `SYNQ_FUND` | SynqFund |
| `SYNQ_LIENS` | SynqLien |
| `SYNQ_PAY` | SynqPay |
| `SYNQ_INSIGHTS` | SynqInsights |
| `SYNQ_COMMS` | SynqComms |
| `SYNQ_AI` | Xenia / SynqAI |
| `SYNQ_PLATFORM` | Platform pseudo-product for tenant permissions |

Common roles include:

- System roles: `PlatformAdmin`, `TenantAdmin`.
- CareConnect roles: `CARECONNECT_REFERRER`, `CARECONNECT_RECEIVER`, `CARECONNECT_NETWORK_MANAGER`.
- SynqLien roles: `SYNQLIEN_SELLER`, `SYNQLIEN_BUYER`, `SYNQLIEN_HOLDER`.
- SynqFund roles: `SYNQFUND_REFERRER`, `SYNQFUND_FUNDER`, `SYNQFUND_APPLICANT_PORTAL`.
- Xenia roles: `XENIA_USER`, `XENIA_ADMIN`.

Before adding permissions, product checks, or role gates, inspect existing policy/role helpers in `shared/building-blocks`, Identity, and the relevant frontend `lib` folder. Do not invent new product-code strings in application code.

## Data, Secrets, and Config

- Do not commit `.env.local`, secrets, connection strings, JWT signing keys, service tokens, AWS credentials, SendGrid keys, Twilio keys, or production URLs unless they are already committed placeholders.
- Use `appsettings.Development.json` only for safe development defaults.
- Keep service-token config names and environment variable names consistent with existing code and startup scripts.
- For systemd/deployment env var changes, note gateway README guidance: YARP cluster/destination override keys should be underscore-only when used with `EnvironmentFile=`.

## Verification Expectations

Always run the narrowest meaningful validation for the files you changed:

- Docs-only changes: review the rendered Markdown/diff; no build is required unless generated docs are involved.
- Documentation-sensitive changes: run `python3 scripts/check-doc-sync.py` or explain why the project-local Codex hook already covered the check.
- C# application/service logic: `dotnet test` for the closest test project, plus `dotnet build` for the affected API/project.
- Shared C# libraries: build the library and run corresponding shared tests.
- EF/model changes: build the affected API/infrastructure project and verify migrations/snapshot changes are intentional.
- Tenant portal UI or BFF changes: `pnpm --dir apps/web type-check` and relevant `test`/`build`/`test:e2e` as appropriate.
- Control center UI or BFF changes: `pnpm --dir apps/control-center type-check` and relevant `test`/`build`.
- Flow frontend changes: follow `apps/services/flow/frontend/AGENTS.md`, then run its local lint/build commands.
- Startup/deployment script changes: run the relevant script test under `scripts/tests` when possible.
- Xenia changes: run `dotnet test apps/services/xenia/Xenia.Tests/Xenia.Tests.csproj` and build `apps/services/xenia/Xenia.Api/Xenia.Api.csproj`.

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
