# LSI-B03 — Tenant Email Sources & Connector Framework

## 1. Ticket

LSI-B03 — Tenant Email Sources & Connector Framework for Synq Intake.

## 2. Objective

Implement an Intake-owned, tenant-bound source registry that can resolve a
registered inbound recipient address to its authoritative tenant, purpose, and
processing profile without inspecting sender identity or message content.

## 3. Repository Baseline

- Repository: `XentraSolutions/legalsynq-v3.0`
- Branch: `xenia`
- Intake root: `apps/services/intake/**`
- Existing B02 report: `analysis/LSI-B02-report.md` (preserved read-only)
- Existing B02 configuration/profile framework reviewed before implementation.

## 4. Baseline HEAD

```text
9752b6f85caeca7ee367fd845115ae68ded6e913
Add LSI B03 tenant email sources documentation asset
```

## 5. Baseline `origin/xenia`

```text
9d26ea4a30130c5594db7ac3b32a60703aad61e
```

## 6. Initial Branch Relationship

`origin/xenia` is an ancestor of local `HEAD`; local `HEAD` is one commit
ahead. There were no local commits beyond that baseline commit.

## 7. Initial Working-Tree Status

The working tree was clean before B03 implementation. The uploaded B03
specification was already present in the baseline commit as an input asset and
was not modified.

## 8. Existing B02 Architecture Reviewed

The implementation will compose with the existing .NET 10 layered Intake
service, B02 `TenantIntakeConfiguration`, `TenantProcessingProfile`,
`ProcessingProfileDefinition`, `IIntakeConfigurationService`,
`IIntakeConfigurationRepository`, registry, EF `IntakeDbContext`, Minimal API
authorization policies, correlation middleware, structured logging, and
`LegalSynq.AuditClient`.

## 9. Implemented Scope

Implemented the B03 source registry and connector framework without beginning
B04 or adding email ingestion, mailbox polling, message retrieval, AI,
matching, review, downstream delivery, cloud resources, deployment, or
production changes.

The initial supported registry values are:

- Source type: `EMAIL`
- Purpose: `LIEN_INTAKE`
- Processing profile: `LIEN_INTAKE_V1`
- Providers: `MICROSOFT_365`, `GOOGLE_WORKSPACE`, and `GENERIC`

Only `LIEN_INTAKE -> LIEN_INTAKE_V1` is currently compatible. Source
resolution is an internal application service; no unrestricted public
email-to-tenant endpoint was exposed.

## 10. Files Added

- `apps/services/intake/Intake.Domain/Sources/TenantIntakeSource.cs`
- `apps/services/intake/Intake.Contracts/Sources/IntakeSourceCodes.cs`
- `apps/services/intake/Intake.Contracts/Sources/IntakeSourceContracts.cs`
- `apps/services/intake/Intake.Application/Sources/EmailAddressNormalizer.cs`
- `apps/services/intake/Intake.Application/Sources/EmailConnectorRegistry.cs`
- `apps/services/intake/Intake.Application/Sources/IEmailConnectorRegistry.cs`
- `apps/services/intake/Intake.Application/Sources/IIntakeSourceRepository.cs`
- `apps/services/intake/Intake.Application/Sources/IIntakeSourceResolver.cs`
- `apps/services/intake/Intake.Application/Sources/IIntakeSourceService.cs`
- `apps/services/intake/Intake.Application/Sources/IntakeSourceResolver.cs`
- `apps/services/intake/Intake.Application/Sources/IntakeSourceService.cs`
- `apps/services/intake/Intake.Application/Sources/SourceRegistries.cs`
- `apps/services/intake/Intake.Api/Endpoints/IntakeSourceEndpoints.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/Configurations/TenantIntakeSourceConfiguration.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/EfIntakeSourceRepository.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/Migrations/20260814000306_AddTenantIntakeSources.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/Migrations/20260814000306_AddTenantIntakeSources.Designer.cs`
- `apps/services/intake/Intake.Tests/SourceFrameworkTests.cs`

## 11. Files Modified

- `apps/services/intake/Intake.Api/Authorization/IntakeAuthorizationPolicies.cs`
  — added source read/manage policies.
- `apps/services/intake/Intake.Api/Program.cs` — mapped source endpoints.
- `apps/services/intake/Intake.Infrastructure/DependencyInjection.cs` —
  registered source repository, registries, service, and resolver.
- `apps/services/intake/Intake.Infrastructure/Persistence/IntakeDbContext.cs`
  — added `TenantIntakeSources`.
- `apps/services/intake/Intake.Infrastructure/Persistence/Migrations/IntakeDbContextModelSnapshot.cs`
  — recorded the B03 model.

`analysis/LSI-B02-report.md` and the uploaded B03 specification asset were
preserved unchanged.

## 12. Source Model, Normalization, and Ownership

`TenantIntakeSource` stores tenant ownership, optional organization ID,
source type, user-facing and normalized email addresses, provider, purpose,
processing profile, active/default state, connector configuration,
credential reference, validation state, audit timestamps/actors, and an
integer `ConfigurationVersion`.

Email normalization is deterministic and conservative:

1. trim surrounding whitespace;
2. reject blank, malformed, multi-address, display-name, and whitespace-bearing
   values;
3. preserve the local-part exactly as supplied after trimming;
4. lowercase only the domain;
5. retain provider-specific aliases rather than collapsing them.

`NormalizedEmailAddress` is globally unique across all tenants, including
inactive sources. The EF model and migration explicitly use MySQL
`utf8mb4_bin` collation so the database lookup/index semantics match the
local-part-preserving application normalization. The resolver uses only the
normalized recipient address to identify the source and then calls B02
`IIntakeConfigurationService.ResolveAsync` for authoritative tenant/profile
enablement and configuration resolution. It does not inspect sender identity
or message content.

Tenant IDs for all public source operations come from
`ICurrentRequestContext.TenantId`; request payloads cannot select a different
tenant. Tenant-scoped repository predicates are used for list, get, update,
status, validation, and connector-test operations.

## 13. Lifecycle, Defaults, and Concurrency

Source creation, update, status changes, and validation use required
optimistic-concurrency versions. Missing versions return
`SOURCE_CONFIGURATION_VERSION_REQUIRED`; stale versions return
`STALE_SOURCE_CONFIGURATION_VERSION`. Successful source mutations increment
the version. Validation accepts `ValidateIntakeSourceRequest` with the
expected version because validation updates persisted validation state.

At most one default source exists per tenant and purpose. The nullable
`DefaultTenantPurposeKey` unique index permits multiple non-default rows but
allows only one non-null default key. Disabling the current default is
rejected with `DEFAULT_SOURCE_MUST_BE_CHANGED_FIRST`; changing a source to
non-default is an explicit removal of default status. There is no hard-delete
endpoint in this bundle.

Default replacement is performed in an explicit EF transaction. Existing
default keys are cleared and flushed before the replacement key is written,
then the transaction commits or rolls back as one unit. This avoids an
immediate MySQL unique-index conflict during a valid create-default or
update-default operation. Source audits and structured mutation logs are
queued during the transaction and published only after commit, so a failed
transaction cannot emit a successful source audit.

## 14. Provider, Connector, and Credential Strategy

The provider registry exposes Microsoft 365, Google Workspace, and Generic
Email as configuration-only providers. All operational capability flags are
false: B03 does not claim polling, webhooks, OAuth, attachment retrieval,
message-ID lookup, or mailbox-folder support.

Configuration-only connectors accept only an empty JSON object. Malformed JSON,
non-object JSON, unknown provider fields, and provider mismatches are rejected.
The connector test endpoint reports that live mailbox connectivity is not
implemented rather than claiming a successful connection.

Persisted credential values are references only. Accepted schemes are
`secret://`, `credential://`, and `connection://`; raw tokens, passwords, and
other arbitrary values are rejected. No connector retrieves or stores
credential values in B03.

## 15. API and Authorization

Added authenticated source-read routes:

- `GET /sources`
- `GET /sources/{sourceId}`
- `GET /sources/types`
- `GET /sources/purposes`
- `GET /sources/providers`

Added source-manage routes:

- `POST /sources`
- `PUT /sources/{sourceId}`
- `PATCH /sources/{sourceId}/status`
- `POST /sources/{sourceId}/validate`
- `POST /sources/{sourceId}/test`

Source reads allow platform/tenant administrators and
`intake.sources.read` or `intake.sources.manage`. Mutations allow platform/
tenant administrators and `intake.sources.manage`. Existing correlation
middleware and exception middleware remain in use.

## 16. Audit and Logging

Successful source lifecycle mutations use the existing B02
`IIntakeConfigurationAuditSink` with resource type `TenantIntakeSource`,
tenant ID, source ID, operation, previous/new versions, actor, correlation ID,
and safe provider/purpose/profile metadata. Default reassignment also emits a
`REMOVE_DEFAULT_SOURCE` audit for each automatically cleared source.

Source mutations emit structured logs containing result, correlation ID,
tenant/source IDs, provider, purpose, profile, version, and operation. Audit
delivery remains persist-first and fire-and-observe; an audit delivery failure
is logged without failing the source mutation. The explicit source transaction
ensures audit/log publication starts only after the source transaction commits.

## 17. Schema and Migration Evidence

Generated migration:

```text
20260814000306_AddTenantIntakeSources
```

The migration was applied to the disposable MySQL 8.0.46 validation database
`legalsynq_intake_b02`. `dotnet ef migrations list` showed:

```text
20260813232726_InitialIntakeConfiguration
20260814000306_AddTenantIntakeSources
```

The new table includes the source lifecycle/configuration columns and indexes
for:

- global unique `NormalizedEmailAddress`;
- `TenantId`;
- `(TenantId, Purpose)`;
- `(TenantId, ProcessingProfileCode)`;
- `(NormalizedEmailAddress, IsActive)`;
- unique nullable `DefaultTenantPurposeKey`.

`dotnet ef dbcontext info` completed successfully and reported the
Pomelo MySQL provider, database `legalsynq_intake_b02`, and
`127.0.0.1` as the data source.

Direct MySQL checks confirmed:

- `NormalizedEmailAddress` uses `utf8mb4_bin`;
- `Sales@example.com` and `sales@example.com` can coexist as distinct
  normalized owners;
- an exact `sales@example.com` lookup returns one row;
- a duplicate normalized email is rejected by
  `IX_TenantIntakeSources_NormalizedEmailAddress`;
- a duplicate default key is rejected by
  `IX_TenantIntakeSources_DefaultTenantPurposeKey`.

The disposable validation rows were removed after the checks.

## 18. Validation Evidence

Focused Intake tests:

```text
dotnet test apps/services/intake/Intake.Tests/Intake.Tests.csproj --no-restore -m:1
Passed: 20, Failed: 0, Skipped: 0
```

The focused tests cover source creation and normalization, global duplicate
ownership, default/lifecycle rules, resolver behavior, tenant/profile
validation, provider/configuration/credential validation, stale mutation
versions including validation, tenant isolation, and failed-transaction
no-audit behavior.

Intake API build and full solution build:

```text
dotnet build LegalSynq.sln --no-restore -m:1
0 errors, 103 warnings
```

The warnings are existing repository warnings, including package vulnerability
advisories and generated nullability/analyzer warnings; no B03 build error was
reported.

The B03 architecture review was rerun after the final transaction, collation,
validation-version, and audit-publication fixes and returned PASS with no
remaining concrete correctness or serious security gap.

`git diff --check` passed. The configured `Start application` workflow was
stopped for the serialized build and restarted successfully afterward. Its
workflow log shows the normal LegalSynq dev startup and running state. Existing
web preview console messages about missing unrelated frontend packages
(`msw`, `@tanstack/react-query`, and `sonner`) remain outside this B03 scope.

## 19. Known Gaps and Explicitly Out of Scope

- No authenticated `WebApplicationFactory` HTTP integration suite was added;
  coverage is application-level plus direct disposable-MySQL schema/constraint
  checks.
- MySQL default replacement was implemented with an explicit transaction and
  ordered flush, but a dedicated automated EF/MySQL test for the full
  create-default and update-default service paths remains suitable for a
  follow-up hardening bundle.
- Providers are configuration-only; live mailbox connectivity and operational
  connector behavior intentionally remain unimplemented.
- No email ingestion, polling, webhook handling, message retrieval, sender
  classification, content classification, AI, matching, human review,
  downstream integration, cloud resource, deployment, production, commit,
  push, or pull-request work was performed.

## 20. Recommended Next Bundle

Proceed next with **LSI-B04** only after separately scoping its specification.
Recommended B04 preparation includes authenticated HTTP coverage for source
authorization and version conflicts, plus disposable-MySQL service-path
coverage for default replacement and commit-failure behavior.
