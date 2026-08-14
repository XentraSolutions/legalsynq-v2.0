# LSI-B02 — Tenant Configuration & Processing Profile Framework

## 1. Ticket

LSI-B02 — Tenant Configuration & Processing Profile Framework for Synq Intake.

## 2. Objective

Implemented an Intake-owned, tenant-scoped configuration and processing-profile
framework for the single initial profile `LIEN_INTAKE_V1`. The implementation
covers persistence, strict typed validation, deterministic resolution, optimistic
concurrency, audit evidence, Minimal API routes, and a first real EF migration.

Email ingestion, AI processing, matching, review, downstream product
integration, and cloud/deployment work remain out of scope.

## 3. Repository Baseline

- Branch: `xenia`
- Existing Intake foundation and B01-02 readiness behavior were preserved.
- Uploaded specifications remain read-only and unmodified.
- No AWS, ECS, Route53, secrets, cloud migrations, deployment, push, PR, or merge
  operation was performed.

## 4. Existing Intake Architecture Reviewed

The existing .NET 10 layered Intake service was retained:

- `Intake.Domain`
- `Intake.Contracts`
- `Intake.Application`
- `Intake.Infrastructure`
- `Intake.Api`
- `Intake.Tests`

The implementation continues to use ASP.NET Core Minimal APIs, EF Core with
Pomelo MySQL, `ICurrentRequestContext`, existing JWT/service-token
authentication, correlation IDs, structured logging, health endpoints, and the
dedicated Intake `DbContext`.

## 5. Files Added

### Domain

- `Intake.Domain/Configuration/ProcessingProfileDefinition.cs`
- `Intake.Domain/Configuration/TenantIntakeConfiguration.cs`
- `Intake.Domain/Configuration/TenantProcessingProfile.cs`
- `Intake.Domain/Configuration/ProcessingProfileDefinitionIds.cs`

### Contracts

- `Intake.Contracts/Configuration/ProcessingProfileCodes.cs`
- `Intake.Contracts/Configuration/LienIntakeV1Configuration.cs`
- `Intake.Contracts/Configuration/ProcessingProfileContracts.cs`

### Application

- `Intake.Application/Configuration/IProcessingProfileRegistry.cs`
- `Intake.Application/Configuration/ProcessingProfileRegistry.cs`
- `Intake.Application/Configuration/IIntakeConfigurationRepository.cs`
- `Intake.Application/Configuration/IIntakeConfigurationAuditSink.cs`
- `Intake.Application/Configuration/IIntakeConfigurationService.cs`
- `Intake.Application/Configuration/IntakeConfigurationService.cs`
- `Intake.Application/Configuration/IntakeConfigurationExceptions.cs`

### Infrastructure

- EF entity configurations for all three B02 entities
- `EfIntakeConfigurationRepository.cs`
- `IntakeConfigurationAuditSink.cs`
- `20260813232726_InitialIntakeConfiguration.cs`
- EF migration designer and model snapshot

### API and tests

- `Intake.Api/Authorization/IntakeAuthorizationPolicies.cs`
- `Intake.Api/Endpoints/IntakeConfigurationEndpoints.cs`
- `Intake.Api/Middleware/IntakeConfigurationExceptionMiddleware.cs`
- `Intake.Tests/ConfigurationFrameworkTests.cs`

## 6. Files Modified

- `Intake.Api/DesignTimeDbContextFactory.cs`
- `Intake.Api/Program.cs`
- `Intake.Api/appsettings.json`
- `Intake.Application/Intake.Application.csproj`
- `Intake.Infrastructure/DependencyInjection.cs`
- `Intake.Infrastructure/Intake.Infrastructure.csproj`
- `Intake.Infrastructure/Persistence/IntakeDbContext.cs`
- This report only.

No unrelated product-domain service was modified.

## 7. Domain Model Implemented

### `TenantIntakeConfiguration`

Tenant-wide Intake settings:

- `TenantId`
- optional `OrgId`
- `IsEnabled`
- `DefaultProcessingProfileCode`
- `RequireHumanReviewByDefault`
- `AutoProcessingEnabled`
- `ConfigurationVersion`
- created/updated timestamps and actor IDs

There is at most one row per tenant.

### `ProcessingProfileDefinition`

Global profile metadata:

- stable unique `Code`
- display name and description
- profile `Version`
- `IsActive`
- `IsSystemDefined`
- timestamps

The migration seeds exactly one active system definition:
`LIEN_INTAKE_V1`, version `1`.

### `TenantProcessingProfile`

Tenant assignment state:

- `TenantId`
- `ProcessingProfileDefinitionId`
- `IsEnabled`
- `IsDefault`
- validated `ConfigurationJson`
- `ConfigurationVersion`
- created/updated timestamps and actor IDs

`DefaultTenantKey` is an Intake persistence invariant key. It is populated only
for a default assignment and is nullable for non-default assignments.

## 8. Database Schema

Migration `20260813232726_InitialIntakeConfiguration` creates:

- `ProcessingProfileDefinitions`
- `TenantIntakeConfigurations`
- `TenantProcessingProfiles`
- `__EFMigrationsHistory` through EF Core

The schema uses the existing Intake MySQL conventions, UTF-8/`utf8mb4`,
`char(36)` GUID columns, `datetime(6)` timestamps, and `longtext` profile
configuration JSON.

## 9. Migration Name

`20260813232726_InitialIntakeConfiguration`

The migration includes the seeded `LIEN_INTAKE_V1` definition and no unrelated
service tables.

## 10. Indexes and Constraints

Implemented constraints and indexes:

- unique `ProcessingProfileDefinitions.Code`
- unique `TenantIntakeConfigurations.TenantId`
- unique `(TenantId, ProcessingProfileDefinitionId)`
- unique nullable `TenantProcessingProfiles.DefaultTenantKey`
- supporting `(TenantId, IsDefault)` index
- supporting `(TenantId, IsEnabled)` index
- lookup index on tenant default profile code
- restricted foreign key from tenant assignments to definitions

The nullable unique default key permits multiple non-default rows while
guaranteeing at most one default row per tenant at the database layer.
Application logic additionally requires a default to be assigned, enabled, and
backed by an active global definition.

## 11. Processing Profile Registry

`IProcessingProfileRegistry` is the single registry boundary for profile codes,
supported versions, canonical defaults, serialization, and validation.

The registry currently exposes only `LIEN_INTAKE_V1`. Unknown codes fail with
`UNKNOWN_PROFILE`; malformed codes fail with `INVALID_PROFILE_CODE` and the
specified shape `^[A-Z][A-Z0-9_]{2,63}$`.

The design is version-aware and permits future registry entries without adding
new profile-specific columns to the schema.

## 12. `LIEN_INTAKE_V1` Configuration Contract

The strongly typed contract contains configuration only:

- human-review and auto-approval flags
- auto-approve, review, and reject confidence thresholds
- patient, case, facility, and duplicate-detection capability flags
- attachment/body processing flags
- unsupported-document behavior
- optional destination adapter code

It contains no email ingestion, AI execution, matching implementation, review
workflow, downstream adapter, credential, or executable-code field.

## 13. `LIEN_INTAKE_V1` Defaults

Canonical defaults are explicit and conservative:

- `RequireHumanReview = true`
- `AllowAutoApproval = false`
- `AutoApproveThreshold = 0.95`
- `ReviewThreshold = 0.75`
- `RejectThreshold = 0.50`
- patient/case/facility matching disabled
- duplicate detection disabled
- attachment and email-body processing enabled
- unsupported documents not allowed
- no destination adapter selected

## 14. Configuration Validation Rules

- JSON must be an object and must deserialize into the registered typed
  contract.
- Unknown JSON properties are rejected through
  `JsonUnmappedMemberHandling.Disallow`.
- Thresholds must be in `[0.00, 1.00]`.
- Threshold order must satisfy:
  `AutoApproveThreshold > ReviewThreshold >= RejectThreshold`.
- Destination adapter codes must start with a letter, contain only letters,
  digits, or underscores, and be at most 64 characters.
- Profile code input is normalized to uppercase only after format validation.
- Inactive global definitions cannot be newly assigned or selected for
  processing.

## 15. Tenant Configuration Behavior

`PUT /configuration` creates a row at version 1 or updates an existing row.
Existing-resource mutations require the current `configurationVersion`.
Successful mutations increment the version monotonically; enable/disable does
not reset it.

If a default profile code is supplied, the assignment must already exist, be
enabled, and reference an active definition. Default selection/removal updates
both the tenant configuration and assignment rows in the same EF save.

Assigning the first default profile automatically creates a conservative
tenant configuration row with that profile as its default, preserving the
bidirectional invariant.

## 16. Tenant Profile Assignment Behavior

`POST /configuration/processing-profiles`:

- accepts only registry-supported profile codes
- rejects inactive definitions
- rejects duplicate tenant/profile assignments with HTTP 409
- validates and canonicalizes profile configuration JSON
- starts assignment configuration versions at 1
- optionally selects the assignment as the tenant default

`PUT` updates validated JSON and/or default state. `PATCH /status` changes only
tenant enablement and preserves historical configuration.

## 17. Default-Profile Behavior

The selected rule is deterministic rejection:

- disabling the current default returns HTTP 400
  `DEFAULT_PROFILE_MUST_BE_CHANGED_FIRST`
- callers must first select another enabled assignment or remove the default
- a disabled assignment cannot be default
- an inactive global definition cannot be default
- a tenant config default must match an enabled tenant assignment
- a config default update synchronizes `IsDefault` and `DefaultTenantKey`
  across assignments

## 18. Versioning Strategy

Both tenant-wide configuration and tenant-profile assignment have independent
integer `ConfigurationVersion` values:

- initial version: 1
- each successful mutation: +1
- read responses expose the current version
- mutation requests carry the expected version

The EF model marks both version properties as concurrency tokens.

## 19. Concurrency Behavior

The application checks expected versions before mutation. EF also includes the
original concurrency-token value in updates. A stale application-level or EF
write returns HTTP 409 with `STALE_CONFIGURATION_VERSION`.

Default changes modify all affected tracked rows and are committed through the
single `SaveChangesAsync` operation, so the nullable unique default key protects
the one-default invariant during concurrent writes.

## 20. Configuration Resolver Behavior

`IIntakeConfigurationService.ResolveAsync` resolves in this order:

1. tenant Intake configuration and enabled state
2. explicit profile code, or the configured tenant default
3. global profile definition and active state
4. tenant assignment and enabled state
5. strict typed profile JSON merged with canonical contract defaults

The returned `ResolvedProcessingConfiguration` includes:

- tenant ID
- profile code and global profile version
- tenant configuration version
- tenant profile configuration version
- typed effective configuration
- resolution timestamp

No tenant type, message content, email content, or AI output is used to infer a
profile. No-default tenants must supply an explicit profile code.

## 21. API Endpoints

Routes are internal Intake routes and remain reachable through the existing
gateway `/api/intake/*` prefix:

- `GET /processing-profiles`
- `GET /configuration`
- `PUT /configuration`
- `GET /configuration/processing-profiles`
- `POST /configuration/processing-profiles`
- `GET /configuration/processing-profiles/{profileCode}`
- `PUT /configuration/processing-profiles/{profileCode}`
- `PATCH /configuration/processing-profiles/{profileCode}/status`

Responses expose tenant/profile identifiers, enabled/default state, profile
version, configuration version, and timestamps. Secrets, connection strings,
tokens, credentials, and unrelated internal data are not returned.

## 22. Authentication/Authorization Behavior

Health and existing foundation routes are unchanged.

- configuration reads require an authenticated principal
- configuration mutations require `PlatformAdmin`, `TenantAdmin`, or an
  existing-style `intake.configuration.manage` permission/scope claim
- tenant-scoped operations derive `TenantId` only from `ICurrentRequestContext`
- missing tenant context returns HTTP 403
- request bodies do not accept a tenant ID and cannot select another tenant

The route middleware preserves the existing authentication challenge behavior
for HTTP 401 and policy denial behavior for HTTP 403.

## 23. Tenant Isolation Evidence

The application service and tests cover:

- tenant A assignments are not returned for tenant B
- tenant B cannot retrieve tenant A's assignment by profile code
- tenant IDs are never taken from mutation payloads
- resolver queries are tenant-scoped
- default selection/disable operations query only the current tenant

## 24. Audit Behavior

Successful create/update/enable/disable/default operations invoke the shared
`LegalSynq.AuditClient` through `IntakeConfigurationAuditSink`.

Audit events include:

- tenant scope
- resource type and stable resource identifier
- actor ID
- operation
- previous and new configuration versions
- correlation ID
- safe metadata

The client follows the platform persist-first, fire-and-observe convention:
audit delivery is not allowed to fail a persisted configuration mutation.
Rejected or failed delivery is logged with tenant, resource, operation, status,
and correlation metadata; Intake does not become a second central audit store.

## 25. Logging Behavior

Configuration mutations emit structured logs containing:

- `CorrelationId`
- `TenantId`
- profile code when applicable
- configuration version
- operation
- result

Full configuration JSON, JWTs, service tokens, secrets, and credentials are
not logged.

## 26. Migration Validation

Validated with repository-pinned `dotnet-ef` 8.0.0:

```text
dotnet ef migrations list
20260813232726_InitialIntakeConfiguration
```

Applied successfully to a fresh disposable MySQL database. The migration
history, seed row, tables, unique default key, tenant/default supporting index,
and tenant/profile uniqueness indexes were queried through MySQL afterward.

## 27. MySQL Validation Environment

Local disposable database:

- MySQL `8.0.46`
- database `legalsynq_intake_b02`
- container was used only for local validation
- no production or cloud database was touched

Observed migration history:

```text
20260813232726_InitialIntakeConfiguration
```

Observed seed:

```text
LIEN_INTAKE_V1 | Version 1 | IsActive 1
```

## 28. Build Commands/Results

Passed:

```text
dotnet build apps/services/intake/Intake.Api/Intake.Api.csproj --no-restore -m:1
```

Result: 0 errors.

## 29. Test Commands/Results

Passed:

```text
dotnet test apps/services/intake/Intake.Tests/Intake.Tests.csproj --no-restore -m:1
```

Result: 10 passed, 0 failed, 0 skipped.

Coverage includes registry defaults, strict JSON rejection, threshold
validation, profile-code validation, default assignment/resolution, default
disable rejection, stale tenant versions, config/default synchronization, and
tenant isolation.

## 30. Full Solution Build Result

Passed after stopping the running multi-service workflow to avoid output-DLL
locks:

```text
dotnet build LegalSynq.sln --no-restore -m:1
```

Result: 0 errors and 66 warnings. Warnings were existing repository package
vulnerability notices, package-pruning notices, and unrelated cross-version
reference/analyzer warnings.

The application workflow was restarted afterward and is running.

## 31. Security Assessment

- Tenant IDs come from authenticated request context only.
- Profile JSON is data-only and strictly typed; executable code is not accepted.
- Responses and logs do not expose secrets or authentication material.
- Inactive definitions cannot be selected for new processing.
- Unique tenant/default constraints prevent duplicate default rows.
- Uploaded specifications were not modified.

No cloud, deployment, secret, or production validation claim is made.

## 32. Existing-Service Impact

No existing product-domain business logic was changed. Intake infrastructure
adds only its own configuration entities, repository, audit adapter, registry,
API routes, and migration. Existing health/readiness and gateway behavior remain
additive and unchanged.

## 33. Git Diff Summary

The working tree contains the Intake B02 implementation and this report. The
two uploaded specification files remain untracked inputs and were not edited.
No commit, push, PR, merge, or deployment was performed.

## 34. Known Gaps

- The automated tests are application-level tests with a fake repository; the
  migration, schema indexes, and seed were validated separately against
  disposable MySQL.
- No full authenticated HTTP `WebApplicationFactory` suite was added; endpoint
  route registration and authorization policy wiring were compile-validated,
  while service tests cover the tenant-isolation behavior behind those routes.
- Audit delivery intentionally follows the shared non-blocking client
  convention; Intake does not add a separate durable audit outbox.

## 35. Out-of-Scope Confirmation

Not implemented:

- `TenantIntakeSource`
- email ingestion
- message-content tenant inference
- AI processing
- patient/case/facility matching execution
- human review workflow
- downstream Case/Lien/Flow/Fund integrations
- automation
- LSI-B03 work
- cloud/deployment changes

## 36. Final Repository Validation Status

**REPOSITORY IMPLEMENTATION COMPLETE — LSI-B02**

Intake B02 builds, its focused tests pass, the full solution builds with
warnings only, and the first Intake configuration migration applies cleanly to
disposable MySQL 8.0.46.

## 37. Recommended Next Bundle: LSI-B03

Do not begin LSI-B03 automatically. The repository boundary for B02 is
complete.