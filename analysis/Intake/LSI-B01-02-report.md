# LSI-B01-02 — Persistence & Readiness Validation

## 1. Ticket

LSI-B01-02 — Persistence & Readiness Validation

## 2. Task Type

Validation / repository hardening.

## 3. LSI-B01-01 Baseline

The LSI-B01-01 implementation is present in the current `xenia` HEAD and
includes the independent .NET 10 Synq Intake service, dedicated
`IntakeDbContext`, Pomelo MySQL registration, `/health`, `/health/ready`,
`/info`, gateway routes, authentication, tenant context, correlation IDs,
startup integration, and architecture/product-neutral tests.

The LSI-B01-01 report was treated as read-only. This file is the only report
created or updated for LSI-B01-02.

The two carried-forward gaps were:

1. Database-backed `/health/ready` was not proven healthy with a valid local
   `IntakeDatabase`.
2. `dotnet-ef` design-time validation was unavailable.

No Intake business scope was added by this ticket.

## 4. Repository Baseline

- Branch: `xenia`
- Current HEAD: `2e2f05a728d86ccd7fb1ce2ba9fd8bab1077773f`
- `origin/xenia`: `0e12b6744d31bc5849c358c51de37438c697fd0b`
- Baseline working-tree status: only the uploaded specification was
  untracked:
  `attached_assets/Pasted--LSI-B01-02-Persistence-Readiness-Validation-Final-Repl_1786659317939.txt`
- LSI-B01-01 implementation present: **Yes**
- Pre-existing uncommitted changes: **Yes**, only the uploaded specification;
  it was not modified.

## 5. Current Validation Gaps

The carried-forward database-backed readiness and EF design-time gaps were
validated. No material persistence/readiness gap remains.

## 6. Persistence Configuration

The existing Intake registration uses:

```text
ConnectionStrings__IntakeDatabase
```

`Intake.Infrastructure.DependencyInjection` resolves the setting and
configures `IDbContextFactory<IntakeDbContext>` with
`Pomelo.EntityFrameworkCore.MySql` and `MySqlServerVersion(8.0.0)`.

Runtime validation used a dedicated sanitized local database:

```text
legalsynq_intake_validation
```

No other LegalSynq database was configured or touched.

## 7. Disposable MySQL Environment

Passed using a disposable Docker container:

- Image: `mysql:8.0`
- Server version observed: `8.0.46`
- Database: `legalsynq_intake_validation`
- Host binding: local-only disposable port `33306`
- Credentials: generated locally and retained only in `/tmp` during
  validation; no credential value was written to source, report, or logs.

The container was removed after validation. No AWS, RDS, QA, staging, shared,
or production database was used.

## 8. IntakeDbContext Runtime Validation

**Passed.**

The Intake process resolved `IntakeDbContext` from DI and the existing
readiness check executed `Database.CanConnectAsync()` successfully against the
dedicated MySQL database.

Provider confirmed: `Pomelo.EntityFrameworkCore.MySql`.

The validation did not touch Liens, Identity, Documents, Fund, CareConnect, or
any other LegalSynq database.

## 9. Healthy Readiness Validation

**Passed.**

With the dedicated MySQL container running and
`ConnectionStrings__IntakeDatabase` configured:

- `GET /health` → HTTP 200
- `GET /health/ready` → HTTP 200
- Readiness body reported:
  `"status":"healthy"` and
  `"description":"Intake database reachable"`

## 10. Negative Readiness Validation

**Passed.**

After stopping the disposable MySQL container:

- `GET /health` → HTTP 200
- `GET /health/ready` → HTTP 503
- Liveness body reported a healthy `process` check.
- Readiness body reported an unhealthy `database` check with
  `"Intake database is not reachable"`.

This confirms liveness remains independent from database readiness.

## 11. Readiness Recovery Validation

**Passed.**

After restarting the same disposable MySQL container and waiting for both
internal MySQL readiness and the local host port to reopen:

- `GET /health/ready` → HTTP 200
- Readiness body again reported
  `"description":"Intake database reachable"`.

## 12. EF Tooling Strategy

The repository already provides a local tool manifest at
`.config/dotnet-tools.json`; no new tooling or package dependency was added.

The existing manifest was restored with:

```text
dotnet tool restore --tool-manifest .config/dotnet-tools.json
```

## 13. EF Tool Version

**Passed.**

- .NET SDK: `10.0.101`
- EF tool: `8.0.0`
- EF runtime: `8.0.2`

The tool emitted the existing compatibility warning that the tool is older
than the runtime, but the required commands completed successfully.

## 14. Design-Time Context Validation

**Passed.**

Command:

```text
ConnectionStrings__IntakeDatabase=<sanitized-local-value> \
  dotnet ef dbcontext info \
  --project apps/services/intake/Intake.Infrastructure/Intake.Infrastructure.csproj \
  --startup-project apps/services/intake/Intake.Api/Intake.Api.csproj \
  --no-build
```

Sanitized result:

```text
Type: Intake.Infrastructure.Persistence.IntakeDbContext
Provider name: Pomelo.EntityFrameworkCore.MySql
Database name: design_time_validation
Data source: localhost
Options: ServerVersion=8.0.0-mysql
```

The design-time factory instantiated the context outside normal application
startup. An initial no-connection diagnostic correctly failed with
`The string argument 'connectionString' cannot be empty`; the rerun with a
non-secret local value passed.

## 15. Migration Validation

**Passed.**

Command:

```text
ConnectionStrings__IntakeDatabase=<sanitized-local-value> \
  dotnet ef migrations list \
  --project apps/services/intake/Intake.Infrastructure/Intake.Infrastructure.csproj \
  --startup-project apps/services/intake/Intake.Api/Intake.Api.csproj \
  --no-build
```

Result:

```text
No migrations were found.
```

Zero migrations is the correct state for the intentionally empty Intake model.
No placeholder entity, bootstrap table, fake migration, or business schema was
created.

## 16. Files Added

- `analysis/LSI-B01-02-report.md`

No Intake source files were added for this validation ticket.

## 17. Files Modified

None. No files under `apps/services/intake/**`, existing service code, package
configuration, or platform configuration were modified.

## 18. Tests Added / Changed

None. Existing LSI-B01-01 Intake tests sufficiently cover the current
foundation, and no additional test infrastructure was necessary.

## 19. Validation Commands

Commands executed included:

```text
git branch --show-current
git rev-parse HEAD
git rev-parse origin/xenia
git status --short
dotnet tool restore --tool-manifest .config/dotnet-tools.json
dotnet --version
dotnet ef --version
dotnet restore LegalSynq.sln --verbosity minimal
dotnet build apps/services/intake/Intake.Api/Intake.Api.csproj --no-restore --configuration Debug --verbosity minimal
dotnet test apps/services/intake/Intake.Tests/Intake.Tests.csproj --no-restore --configuration Debug --verbosity minimal -m:1
dotnet build LegalSynq.sln --no-restore --configuration Debug --verbosity minimal -m:1
dotnet ef dbcontext info --project apps/services/intake/Intake.Infrastructure/Intake.Infrastructure.csproj --startup-project apps/services/intake/Intake.Api/Intake.Api.csproj --no-build
dotnet ef migrations list --project apps/services/intake/Intake.Infrastructure/Intake.Infrastructure.csproj --startup-project apps/services/intake/Intake.Api/Intake.Api.csproj --no-build
git diff --check
```

Runtime validation used only the disposable local MySQL container and a local
Intake process on port `5013`. The database password was generated and passed
through process-local temporary state only.

## 20. Validation Results

- Repository baseline: **Passed** — branch, HEAD, origin SHA, status, and
  LSI-B01-01 presence recorded.
- Local MySQL 8: **Passed** — isolated Docker MySQL 8.0.46 database used.
- IntakeDbContext DI resolution: **Passed**.
- Pomelo/MySQL provider: **Passed**.
- Real database connectivity: **Passed**.
- Liveness with database available: **Passed** — HTTP 200.
- Healthy readiness: **Passed** — HTTP 200.
- Liveness with database stopped: **Passed** — HTTP 200.
- Negative readiness with database stopped: **Passed** — HTTP 503.
- Readiness recovery after database restart: **Passed** — HTTP 200.
- EF tool restoration: **Passed** — repository-pinned `dotnet-ef` 8.0.0.
- EF design-time context construction: **Passed**.
- Migration listing: **Passed** — zero migrations found.
- Intake build: **Passed** — 0 errors.
- Intake tests: **Passed** — 3 passed, 0 failed, 0 skipped.
- Full platform build: **Passed** with `-m:1` — 0 errors.
- Final diff whitespace check: **Passed**.

The full solution restore/build retained existing unrelated dependency,
solution-configuration, and package vulnerability warnings. No new source
correction was required.

## 21. Security Assessment

- Dedicated local/disposable Intake database used: **Yes**
- Other LegalSynq database touched: **No**
- Shared cloud database touched: **No**
- AWS/RDS/ECS/Route53 modified: **No**
- Secrets committed: **No**
- Credentials included in report: **No**
- Raw authorization or service tokens exposed: **No**
- Readiness response exposed database secrets: **No**
- Tenant/auth architecture changed: **No**
- Placeholder business entities or fake migrations introduced: **No**

## 22. Existing-Service Impact

Existing LegalSynq service business logic changed: **NO**.

No files outside the report were modified for LSI-B01-02. The uploaded
specification remains the only other untracked pre-existing file.

## 23. Git Diff Summary

LSI-B01-02 added only:

- `analysis/LSI-B01-02-report.md`

No source, project, package, workflow, gateway, startup, database, or cloud
configuration changes were made. `git diff --check` passed. No generated
binaries or unrelated modifications were introduced.

## 24. Known Gaps

No material LSI-B01-02 persistence/readiness gap remains.

The EF tool/runtime versions differ (`8.0.0` versus `8.0.2`) and emit a
non-blocking existing warning; the repository-pinned tool successfully loads
the context and lists migrations.

## 25. Current Phase Status

REPOSITORY VALIDATION PASSED

All applicable local persistence, readiness, recovery, EF design-time,
migration-structure, build, test, security, and existing-service impact
criteria passed.

## 26. Recommended Next Phase

GitHub validation for the completed LSI-B01 foundation.

This report does not claim GitHub, deployed, AWS, or production validation.
LSI-B02 was not started.