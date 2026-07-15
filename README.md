# LegalSynq Platform

LegalSynq is a multi-tenant SaaS platform for legal, healthcare, funding, workflow, billing, communications, automation, and support operations. It is a mixed .NET/Node monorepo with ASP.NET Core Minimal API services behind a YARP gateway and two main Next.js frontends.

For agent-specific instructions, read [AGENTS.md](AGENTS.md) before changing code. If documentation and code disagree, trust the current package files, project files, configuration, and startup scripts.

## Products

| Code | Name | Description |
|---|---|---|
| `SYNQ_LIENS` | SynqLien | Medical lien lifecycle, marketplace, offers, purchase, and servicing |
| `SYNQ_FUND` | SynqFund | Funding application submission, review, approval, and related workflows |
| `SYNQ_CARECONNECT` | CareConnect | Healthcare referral network, providers, appointments, and networks |
| `SYNQ_AI` | Xenia / SynqAI | Tenant-aware assistant and automation capabilities |
| `SYNQ_COMMS` | SynqComms | Platform messaging and communication capabilities |
| `SYNQ_INSIGHTS` | SynqInsights | Analytics, reports, schedules, and exports |
| `SYNQ_PAY` | SynqPay | Payment-related product catalog anchor |
| `SYNQ_PLATFORM` | Platform | Pseudo-product for tenant-level platform permissions; not a tenant subscription product |

Product constants live in `shared/building-blocks/BuildingBlocks/Authorization/ProductCodes.cs`. Role constants live in `shared/building-blocks/BuildingBlocks/Authorization/ProductRoleCodes.cs`.

## Architecture

Browser traffic enters through the tenant portal or the control center. Frontend server route handlers act as BFF endpoints and call the gateway. The gateway fronts backend services, but each backend service remains responsible for its own authentication, authorization, tenant context, and persistence boundary.

```text
Tenant Portal (:5000 via dev proxy, Next internal :3050)
Control Center (:5004)
        |
Gateway / YARP (:5010)
        |
Identity (:5001)        Fund (:5002)          CareConnect (:5003)
Tenant (:5005)          Documents (:5006)     Audit (:5007)
Notifications (:5008)   Liens (:5009)         Comms (:5011)
Flow (:5012)            Monitoring (:5015)    Task (:5016)
Support (:5017)         Reports (:5029)       Commerce (:5030)
Tenant Billing (:5031)  Xenia (:5035)
```

Important boundaries:

- Browser clients should call relative `/api/...` routes in the frontend, not raw backend service URLs.
- Frontend BFF routes should call the gateway unless an existing internal-service pattern says otherwise.
- Services must not share databases or EF contexts across service boundaries.
- Shared libraries under `shared/` should remain additive and dependency-light.
- Flow and Xenia are separate service boundaries. Xenia is designed as an independent automation platform and should not take LegalSynq domain-model dependencies.

## Quick Start

Install Node dependencies with the repository package manager:

```bash
pnpm install
```

Run the full local development stack:

```bash
bash scripts/run-dev.sh
```

Run backend services only:

```bash
bash scripts/run-backend-dev.sh
# or
pnpm dev:be
```

Stop backend services only:

```bash
bash scripts/stop-backend-dev.sh
# or
pnpm stop:be
```

Stop the local stack:

```bash
bash scripts/stop-dev.sh
```

The full dev script starts the tenant portal at `http://localhost:5000` through `scripts/dev-proxy.js`, with the underlying Next.js process on port `3050`. The control center runs at `http://localhost:5004`, and the gateway runs at `http://localhost:5010`.

If you want FE and BE separated, use the backend-only script above and then start the frontends independently:

```bash
pnpm dev:web
pnpm dev:control-center
```

## Repository Layout

```text
apps/
  gateway/             YARP reverse proxy
  web/                 Tenant portal, Next.js App Router
  control-center/      Operator control center, Next.js App Router
  services/
    identity/          Auth, users, orgs, products, RBAC
    tenant/            Canonical tenant registry and branding
    careconnect/       Referrals, providers, appointments, networks
    liens/             SynqLien marketplace and servicing
    fund/              SynqFund funding applications
    documents/         Document storage, scanning, access tokens
    notifications/     Notification delivery
    comms/             Messaging and communications service
    audit/             Audit event log
    monitoring/        Service health probes and alerting
    reports/           Report templates, execution, export, scheduling
    flow/              Workflow engine, task orchestration, separate frontend
    task/              Platform task service
    support/           Support case management
    commerce/          Commerce/billing integration boundary
    tenant-billing/    Billing service boundary
    tenant-billing-api/ Alternate/imported tenant billing boundary
    xenia/             Tenant-aware automation and assistant platform
shared/
  contracts/           Shared C# DTOs, constants, event contracts
  building-blocks/     Middleware, auth helpers, context, commerce abstractions
  audit-client/        Typed audit event HTTP client
scripts/               Development, deployment, startup, DB, and smoke scripts
artifacts/             Generated/support material and local artifact API
analysis/              Per-ticket implementation reports and notes
exports/, publish/,
dist/                  Generated or support output
```

Avoid changing generated/support directories unless the task explicitly targets them.

## Tech Stack

| Layer | Technology |
|---|---|
| Frontends | Next.js `16.2.6`, React `18.3.x`, TypeScript, Tailwind CSS |
| Flow frontend | Next.js 16 and React 19 under `apps/services/flow/frontend` |
| Gateway | ASP.NET Core Minimal API + YARP |
| Backend services | ASP.NET Core Minimal APIs targeting `net10.0` |
| ORM | Entity Framework Core packages are service-specific, mostly 8.x with Pomelo MySQL |
| Database | MySQL 8 for service databases, with some tests/in-memory development paths |
| Auth | JWT bearer tokens plus HttpOnly session cookies in browser-facing apps |
| Messaging/Comms | SendGrid, SMTP/MailKit, Twilio, and service-specific adapters |

The root package manager is `pnpm@10.26.1`. The repo currently contains both `pnpm-lock.yaml` and `package-lock.json`; do not change lockfiles unless a dependency change requires it.

## Common Commands

Run only the tenant portal:

```bash
pnpm --dir apps/web dev
```

Run only the control center:

```bash
pnpm --dir apps/control-center dev
```

Build, type-check, and test the tenant portal:

```bash
pnpm --dir apps/web type-check
pnpm --dir apps/web build
pnpm --dir apps/web test
pnpm --dir apps/web test:e2e
```

Build, type-check, and test the control center:

```bash
pnpm --dir apps/control-center type-check
pnpm --dir apps/control-center build
pnpm --dir apps/control-center test
```

Restore and build the main .NET solution:

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

Build service boundaries that may not be fully covered by `LegalSynq.sln`:

```bash
dotnet build apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj
dotnet build apps/services/reports/src/Reports.Api/Reports.Api.csproj
dotnet build apps/services/support/Support.Api/Support.Api.csproj
dotnet build apps/services/commerce/src/Commerce.Api/Commerce.Api.csproj
dotnet build apps/services/tenant-billing/src/Billing.Api/Billing.Api.csproj
dotnet build apps/services/tenant-billing-api/src/TenantBilling.Api/TenantBilling.Api.csproj
dotnet build apps/services/xenia/Xenia.Api/Xenia.Api.csproj
```

Check whether current code/config changes should include documentation updates:

```bash
python3 scripts/check-doc-sync.py
```

Codex also runs this check through the project-local `.codex/hooks.json` hook when the project hook is trusted.

## Service READMEs

- [Gateway](apps/gateway/README.md)
- [Tenant Portal](apps/web/README.md)
- [Control Center](apps/control-center/README.md)
- [Identity Service](apps/services/identity/README.md)
- [Tenant Service](apps/services/tenant/README.md)
- [CareConnect Service](apps/services/careconnect/README.md)
- [Liens Service](apps/services/liens/README.md)
- [Fund Service](apps/services/fund/README.md)
- [Documents Service](apps/services/documents/README.md)
- [Notifications Service](apps/services/notifications/README.md)
- [Audit Service](apps/services/audit/README.md)
- [Monitoring Service](apps/services/monitoring/README.md)
- [Reports Service](apps/services/reports/README.md)
- [Flow Service](apps/services/flow/README.md)
- [Support Service](apps/services/support/README.md)
- [Commerce Service](apps/services/commerce/README.md)
- [Tenant Billing Service](apps/services/tenant-billing/README.md)
- [Tenant Billing API](apps/services/tenant-billing-api/README.md)
- [Xenia Service](apps/services/xenia/README.md)
- [Shared Libraries](shared/README.md)
