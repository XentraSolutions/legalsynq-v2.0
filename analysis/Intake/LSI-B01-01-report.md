# LSI-B01-01 — Synq Intake Service Foundation

## 1. Ticket

LSI-B01-01 — Synq Intake Service Foundation

## 2. Task Type

Repository implementation + local validation.

## 3. Architecture Baseline

LegalSynq v3 uses independently startable .NET microservices behind the
existing ASP.NET Core/YARP gateway, with layered projects where applicable,
separate EF Core persistence boundaries, shared BuildingBlocks, JWT and
service-token authentication, structured logging, health/readiness endpoints,
and repository-standard .NET tests.

Synq Intake is a product-neutral technical service boundary. This ticket does
not implement mailbox connectors, email ingestion, structured email or raw EML
persistence, attachment ingestion, Documents integration, Synq AI, matching,
review, downstream business integrations, or automation.

The locked invariant is preserved: each tenant will designate its own inbound
email address for Synq Intake. Future tenant identity resolution must come
from registered tenant-owned Intake source configuration before business
processing, not from sender or message contents.

## 4. Repository Baseline

- Branch: `xenia`
- HEAD: `7157f780747c7f1ca4a3869b271e03c38d7c184c`
- Working tree before implementation: clean
- Remote branch: `origin/xenia`
- Inspected: `LegalSynq.sln`, Documents, Liens, Notifications, Identity,
  Gateway.Api, shared BuildingBlocks, and development startup scripts.

## 5. LegalSynq v3 Technology Baseline

- Target framework: `net10.0`
- API: ASP.NET Core Minimal APIs
- Persistence: EF Core with Pomelo MySQL
- JWT bearer: `Microsoft.AspNetCore.Authentication.JwtBearer` `8.0.8`
- EF design: `Microsoft.EntityFrameworkCore.Design` `8.0.2`
- Tests: xUnit and Microsoft.NET.Test.Sdk conventions from the current
  Documents test baseline
- Shared libraries: `shared/building-blocks/BuildingBlocks`
- Configuration: appsettings plus environment variables
- Logging: existing structured console/Serilog conventions

No unrelated dependency modernization was performed.

## 6. Primary Reference Service

Documents was the primary reference for layered service composition,
health/readiness, infrastructure registration, JWT/service-token compatibility,
and EF design-time support. Liens was the secondary reference for Minimal API,
request context, startup composition, and metadata. Notifications was inspected
as an additional recent layered backend/test baseline.

## 7. Files Added

Added 22 Intake service source/configuration files under `apps/services/intake/`:

- `Intake.Api`: project, `Program.cs`, design-time factory, health/info
  endpoints, correlation middleware, appsettings files
- `Intake.Application`: project, foundation service interface/implementation
- `Intake.Contracts`: project and service metadata contract
- `Intake.Domain`: project and domain assembly marker
- `Intake.Infrastructure`: project, dependency injection, empty dedicated
  `IntakeDbContext`, database health check
- `Intake.Tests`: project and three architecture/product-neutral tests
- `apps/services/intake/README.md`

Also added:

- `analysis/LSI-B01-01-report.md`

## 8. Files Modified

All modifications outside `apps/services/intake/**` are additive:

- `LegalSynq.sln` — registered the six Intake projects.
- `apps/gateway/Gateway.Api/appsettings.json` — added `/api/intake/*` routes
  and `intake_cluster` targeting port `5013`.
- `scripts/run-backend-dev.sh` — builds and starts Intake.
- `scripts/run-dev.sh` — builds and starts Intake.
- `scripts/stop-dev.sh` — recognizes `Intake.Api`.
- `scripts/_startup-helpers.sh` — labels Intake startup/crash output.

Existing service business logic changed: **NO**.

## 9. Service Structure

- `Intake.Domain` — domain-only assembly; no business aggregate invented.
- `Intake.Contracts` — service-boundary DTO/contract.
- `Intake.Application` — foundation service abstraction and implementation;
  references only Domain and Contracts.
- `Intake.Infrastructure` — dedicated EF context, MySQL registration, and
  readiness health check.
- `Intake.Api` — composition root, auth, correlation, logging, Minimal API,
  health/readiness, metadata, configuration, and Swagger.
- `Intake.Tests` — architecture and product-neutral tests.

## 10. Database / Persistence Changes

`IntakeDbContext` is owned by Intake and configured from
`ConnectionStrings:IntakeDatabase` with Pomelo MySQL 8 conventions. No Liens,
Identity, Documents, or other service database is referenced. No cross-service
EF entities or foreign keys exist.

The EF model is intentionally empty. No placeholder business tables were
created solely to force a migration.

## 11. Tenant Context

The service registers shared `ICurrentRequestContext` /
`CurrentRequestContext`. The authenticated foundation diagnostic endpoint reads
tenant, organization, and user context from the authenticated request. No
request-body tenant override exists.

## 12. Tenant Email Intake Architecture

Each tenant will designate its own inbound email address for Synq Intake. Each
configured address will belong to exactly one tenant. Future source mapping will
be owned by Synq Intake, and tenant identity will be resolved from registered
tenant-owned source configuration before processing.

Tenant identity will not be inferred from sender address/domain, provider,
patient or law-firm name, subject, body, attachments, metadata,
classification, or AI output.

Assessment:

- Shared platform-wide mailbox assumed: **No**
- Tenant-owned source mapping prevented: **No**
- Multiple sources per tenant blocked: **No**
- Future `TenantIntakeSource` implementation compatible: **Yes**
- `TenantIntakeSource` CRUD implemented in this ticket: **No**

## 13. Authentication

JWT bearer authentication follows the existing issuer, audience, signing-key,
and `role` claim conventions. Service-token validation uses the shared
`AddServiceTokenBearer` helper. No competing authentication protocol was
introduced.

## 14. Correlation / Logging

The existing `X-Correlation-Id` behavior is used: request values are stored in
`HttpContext.Items` and echoed in responses, or a version-7 UUID is generated.
Serilog structured request logging includes the service name.

Intake does not log secrets, bearer tokens, connection strings, email bodies,
document contents, or future PHI.

## 15. Gateway Changes

Added additive YARP routes:

- `/api/intake/health` → `/health` (anonymous)
- `/api/intake/health/ready` → `/health/ready` (anonymous)
- `/api/intake/info` → `/info` (anonymous)
- `/api/intake/{**catch-all}` → protected Intake routes

Each route removes `/api/intake` before forwarding to the Intake service.
`intake_cluster` targets `http://localhost:5013`. Existing routes and
clusters were not renamed or behaviorally changed.

## 16. Configuration Changes

Intake listens on `http://0.0.0.0:5013`. Both development startup scripts build
and start it on port `5013`; the stop script recognizes it.

The dedicated database setting is `ConnectionStrings__IntakeDatabase`.
Development can start without it so liveness is inspectable; readiness reports
the database dependency as unavailable until MySQL is configured.

## 17. Security Assessment

- Secrets committed: **No**
- Raw credentials introduced: **No**
- Another service database reused: **No**
- Arbitrary tenant override possible: **No**
- Sensitive material logged by new code: **No**
- Shared auth patterns followed: **Yes**
- Tenant context sourced from platform mechanisms: **Yes**
- Future email tenant resolution source-bound: **Yes**
- Cross-service data coupling introduced: **No**

## 18. Tests Added

`FoundationArchitectureTests` has three passing tests:

1. Domain does not reference API, Application, or Infrastructure.
2. Application does not reference API or Infrastructure.
3. Foundation metadata is product-neutral and does not identify SynqLien.

## 19. Validation Commands

```text
git branch --show-current
git rev-parse HEAD
git status --short
dotnet restore LegalSynq.sln --verbosity minimal
dotnet restore apps/services/intake/Intake.Api/Intake.Api.csproj --verbosity minimal
dotnet build apps/services/intake/Intake.Api/Intake.Api.csproj --no-restore --configuration Debug --verbosity minimal
dotnet test apps/services/intake/Intake.Tests/Intake.Tests.csproj --no-restore --configuration Debug --verbosity minimal -m:1
dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj --no-restore --configuration Debug --verbosity minimal
dotnet build LegalSynq.sln --no-restore --configuration Debug --verbosity minimal -m:1
dotnet ef migrations list --project apps/services/intake/Intake.Infrastructure/Intake.Infrastructure.csproj --startup-project apps/services/intake/Intake.Api/Intake.Api.csproj --no-build
python JSON parse of apps/gateway/Gateway.Api/appsettings.json
bash -n scripts/run-backend-dev.sh scripts/run-dev.sh scripts/stop-dev.sh scripts/_startup-helpers.sh
```

Runtime checks used a disposable local Intake process and disposable gateway
port `5101`; no deployment or AWS resource was touched.

## 20. Validation Results

- Repository baseline: **Passed** — branch `xenia`, recorded HEAD, clean
  pre-implementation status.
- Restore: **Passed** — solution and targeted restore completed.
- Intake build: **Passed** — 0 errors.
- Intake tests: **Passed** — 3 passed, 0 failed, 0 skipped.
- Gateway build: **Passed** — 0 errors.
- Full platform build: **Passed** with `-m:1` — 0 errors. The initial
  parallel attempt hit host resource limits in unrelated compiler processes;
  the serial retry passed.
- Gateway JSON/config validation: **Passed** — four Intake routes and the
  Intake cluster parsed and were present.
- Direct API runtime: **Passed** — `/health` HTTP 200 and `/info` HTTP 200.
- Readiness runtime: **Partially Completed** — `/health/ready` HTTP 503 with
  the expected database-unavailable result because no local
  `IntakeDatabase` was configured. Healthy MySQL-backed readiness was not
  available in this environment.
- Gateway runtime: **Passed** — `/api/intake/health` and `/api/intake/info`
  returned Intake responses through YARP; `X-Correlation-Id` was preserved.
- Startup script syntax: **Passed**.
- EF design-time validation: **Unavailable** — `dotnet-ef` is not installed or
  restored locally; no migration success was claimed.

## 21. Migration Status

Migration intentionally deferred because no meaningful business model exists
yet. The empty `IntakeDbContext` is the persistence foundation only. No
migration was applied locally, to AWS, or to any shared environment.

## 22. Known Gaps

- A live MySQL connection was not available, so healthy readiness and actual
  database connectivity were not proven.
- `dotnet-ef` was unavailable, so design-time migration listing could not run.
- No business endpoints or Intake persistence entities exist by design; those
  belong to subsequent LSI-B01 iterations.

## 23. Existing-Service Impact

All existing files modified outside `apps/services/intake/**` are listed in
Section 8. Every change is additive solution, gateway, or development startup
wiring. Existing service business logic changed: **NO**.

## 24. Git Diff Summary

The working tree contains the Intake service and report plus six additive
existing-platform file changes listed in Section 8. `git diff --check`
passed. No unexpected generated source or binaries are part of the intended
change set.

## 25. Current Phase Status

REPOSITORY IMPLEMENTATION PARTIAL

The repository foundation, builds, tests, direct runtime checks, and gateway
route checks passed. The phase remains partial only because healthy
database-backed readiness and EF design-time tooling could not be validated in
this environment.

## 26. Recommended Next Phase

Repository corrections required before GitHub validation.

The next review should provide approved local/disposable MySQL configuration
and `dotnet-ef` tooling if design-time validation is required. This report is
not GitHub, deployed, or production validation.

---

REPOSITORY IMPLEMENTATION PARTIAL