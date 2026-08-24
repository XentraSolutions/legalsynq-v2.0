# LSI-B14 — SynqLien Routing / Case & Lien Creation

## 1. Ticket and objective

Implement explicit `SYNQLIEN_V1` routing from a B13 current `READY`
`ApprovedIntakeSnapshotV1` to the supported LegalSynq Liens HTTP API. The
adapter must validate the reviewed projection, preserve tenant and correlation
context, create or reuse a Case, create exactly one Lien, persist B13 external
references, and remain recoverable after retries, response loss, timeouts, and
partial success.

This report is created before implementation. It will be updated with the
final file list, mappings, validation results, and known gaps.

## 2. Repository baseline

- Repository: `XentraSolutions/legalsynq-v3.0`
- Branch: `xenia`
- Baseline HEAD: `08ec34b8c24659d8f88e9fa96d7c84d11ce32a5e`
- Baseline `origin/xenia`: `9d26ea4a30130c5594db7ac3b32a60703aad61e9`
- Initial relationship: local `xenia` is 11 commits ahead of `origin/xenia`
  (`git rev-list --left-right --count origin/xenia...HEAD` returned `0 11`).
- Initial working tree: pre-existing tracked B01–B13 work was present; the only
  untracked item was the uploaded B14 specification under `attached_assets/`.
  The specification is not being added to the repository.
- No cloud, deployment, push, PR, merge, or production work is performed.

## 3. Architecture inspected before coding

### B13 framework

- `IIntakeDestinationAdapter` accepts only `ApprovedIntakeSnapshotV1` and
  `IntakeAdapterRequestContext`.
- The adapter registry is `IIntakeDestinationAdapterRegistry`.
- `IntakeAdapterExecutionService` owns claims, retries, bounded timeout,
  cancellation, attempt history, and finalization.
- `IAdapterExecutionRepository` persists execution attempts and
  `IntakeAdapterExternalReference` rows.
- B13 external references are tenant-scoped by
  `TenantId + AdapterExecutionId + ReferenceType + ReferenceId`.
- The current request context carries `TenantId`, `SnapshotId`,
  `CorrelationId`, stable `IdempotencyKey`, requesting user, and dry-run state.
- The current B13 `NOOP_V1` adapter is registered in Intake infrastructure and
  will remain available.

### Approved snapshot contract

`ApprovedIntakeSnapshotV1` contains classification, effective facts, entity
decisions, duplicate decisions, document references, review metadata, and
provenance. B14 will consume this contract only and will not load B07–B12
persistence directly.

### Liens service

- Service project: `apps/services/liens/Liens.Api/Liens.Api.csproj`
- Application contracts: `apps/services/liens/Liens.Application/DTOs`
- Case service/API: `ICaseService`, `CaseService`,
  `apps/services/liens/Liens.Api/Endpoints/CaseEndpoints.cs`
- Lien service/API: `ILienService`, `LienService`,
  `apps/services/liens/Liens.Api/Endpoints/LienEndpoints.cs`
- Case base route: `/api/liens/cases`
  - Read: `GET /api/liens/cases/{id}`
  - Lookup: `GET /api/liens/cases/by-number/{caseNumber}`
  - Search: `GET /api/liens/cases/`
  - Create: `POST /api/liens/cases/`
- Lien base route: `/api/liens/liens`
  - Read: `GET /api/liens/liens/{id}`
  - Lookup: `GET /api/liens/liens/by-number/{lienNumber}`
  - Search: `GET /api/liens/liens/`
  - Search body: `POST /api/liens/liens/search`
  - Create: `POST /api/liens/liens/`
- Case DTOs: `CreateCaseRequest`, `CaseResponse`
- Lien DTOs: `CreateLienRequest`, `LienResponse`
- Health/readiness: `GET /health` is anonymous; service startup applies
  migrations and performs schema checks. No separate readiness route was
  found.
- Liens API authentication currently uses the shared JWT bearer scheme and
  `Policies.AuthenticatedUser`. Routes independently require product access
  and Case/Lien permissions.
- Liens service methods require `ICurrentRequestContext.TenantId`, and
  repositories filter by tenant. Referenced Case and Facility reads also use
  the supplied tenant, providing independent tenant enforcement for those
  references.
- Existing Liens create endpoints did not inspect `Idempotency-Key`; create
  methods only rejected duplicate CaseNumber/LienNumber. B14 will add a
  backward-compatible, durable idempotency boundary at the Liens service.
- Existing service-token infrastructure is available through
  `BuildingBlocks.Authentication.ServiceTokens`; Intake already supports
  service-token bearer authentication and will use the same convention for
  the destination client.
- Existing HttpClient, correlation, exception, and audit conventions will be
  reused. Raw destination response bodies will not be persisted or logged.

## 4. Initial scope boundary

This ticket will not add direct Liens database access to Intake, SynqLien
business tables to Intake, automatic READY-triggered execution, B15 document
association, Flow tasks, notifications, SynqFund/CareConnect routing, product
management UI, cloud/deployment changes, or specification/build artifacts.

## 5. Implemented design

- Added the explicitly executable `SYNQLIEN_V1` B13 adapter. It is registered
  alongside `NOOP_V1` and is never invoked automatically when a snapshot
  becomes `READY`.
- Added typed `Intake:SynqLien` options. The destination is disabled by
  default, requires an absolute base URL and configured destination
  organization id, and uses a bounded HttpClient timeout.
- Added `ISynqLienClient`/`SynqLienClient`. It sends tenant, correlation,
  organization, and `Idempotency-Key` headers, mints the shared service-token
  bearer token, never logs response bodies, and maps timeout/429/5xx/network
  failures as retryable.
- Added a dedicated internal Liens HTTP surface:
  `GET /api/internal/synqlien/cases/{id}`,
  `POST /api/internal/synqlien/cases`, and
  `POST /api/internal/synqlien/liens`. It uses the shared service-token
  scheme, requires a signed tenant claim, requires `X-Org-Id`, and calls the
  existing tenant-scoped Case/Lien application services.
- Existing user-facing Case/Lien create endpoints now also honor
  `Idempotency-Key` without changing their response contract.
- Liens Case/Lien creation uses the existing `ExternalReference` columns as
  the durable idempotency record. Repeated keys return the already-created
  resource before CaseNumber/LienNumber duplicate validation. Tenant-scoped
  repository lookups prevent cross-tenant reuse.
- Routing uses only the approved snapshot: selected Case ids are verified
  through Liens; `NO_MATCH` creates a Case; Lien creation occurs only after
  Case resolution; selected Facility ids are forwarded; rejected facts are
  excluded; matching and duplicate decisions are not recomputed.
- Stable child keys are derived as `<execution-idempotency-key>|CASE` and
  `|LIEN`. A Case-only partial result is retained in B13 external references
  and retry/reconciliation safely reuses both keys.
- Dry-run validates routing but makes no destination calls or product records.
  B13 remains responsible for claims, bounded execution timeout, retries,
  cancellation, attempt history, result finalization, and `CASE`/`LIEN`
  external-reference persistence.

## 6. Changed files

- `apps/services/intake/Intake.Application/Snapshot/SynqLienContracts.cs`
- `apps/services/intake/Intake.Application/Snapshot/SynqLienV1Adapter.cs`
- `apps/services/intake/Intake.Infrastructure/Snapshot/SynqLienClient.cs`
- `apps/services/intake/Intake.Infrastructure/DependencyInjection.cs`
- `apps/services/intake/Intake.Api/appsettings.json`
- `apps/services/intake/Intake.Domain/Snapshot/ApprovedSnapshotCodes.cs`
- `apps/services/intake/Intake.Tests/B14SynqLienTests.cs`
- `apps/services/liens/Liens.Api/Endpoints/SynqLienInternalEndpoints.cs`
- `apps/services/liens/Liens.Api/Program.cs`
- `apps/services/liens/Liens.Api/appsettings.json`
- `apps/services/liens/Liens.Api/Endpoints/CaseEndpoints.cs`
- `apps/services/liens/Liens.Api/Endpoints/LienEndpoints.cs`
- `apps/services/liens/Liens.Application/DTOs/CreateCaseRequest.cs`
- `apps/services/liens/Liens.Application/DTOs/CreateLienRequest.cs`
- `apps/services/liens/Liens.Application/Repositories/ICaseRepository.cs`
- `apps/services/liens/Liens.Application/Repositories/ILienRepository.cs`
- `apps/services/liens/Liens.Application/Services/CaseService.cs`
- `apps/services/liens/Liens.Application/Services/LienService.cs`
- `apps/services/liens/Liens.Infrastructure/Repositories/CaseRepository.cs`
- `apps/services/liens/Liens.Infrastructure/Repositories/LienRepository.cs`
- `apps/services/liens/Liens.Api.Tests/Tests/LienEndpointTests.cs`

The uploaded specification under `attached_assets/` remains untracked and was
not added to the repository.

## 7. Validation

### Passed

- `dotnet build apps/services/intake/Intake.Api/Intake.Api.csproj
  --no-restore -p:BuildInParallel=false` — passed, 0 errors.
- `dotnet build apps/services/liens/Liens.Api/Liens.Api.csproj
  --no-restore -p:BuildInParallel=false` — passed, 0 errors.
- Full `Intake.Tests` — passed, 91/91.
- B14-focused Intake tests — passed, 3/3.
- Liens HTTP idempotency regression — passed, 1/1.
- `git diff --check` — passed.
- Source/artifact scan for credentials in the B14 implementation/report —
  no matches; no secret values were accessed or emitted.

### Not yet run / limited

- Full `Liens.Api.Tests` was run serially: 429 passed, 45 failed, 0 skipped.
  The failures are existing selling-portfolio scenarios that require a seller
  email in the test environment; their validation errors are unrelated to the
  B14 Case/Lien routes. The focused B14 idempotency test passed.
- The full `LegalSynq.sln` build was attempted serially and is blocked by
  unrelated shared NuGet-cache gaps in other services/tests (missing
  `MimeKit`, `Testcontainers`, and xUnit analyzer assemblies). The two
  modified service builds pass independently.
- Live MySQL migration application and end-to-end calls against a deployed
  Liens destination were not available locally. The B14 path is therefore
  locally build- and unit/API-tested, but production schema, network, and
  service-token interoperability remain deployment-time validation items.
- No new EF migration was generated: B14 reuses existing `ExternalReference`
  columns and does not add a table or column. The current service-layer
  idempotency lookup is durable for response-loss retries; a database-level
  unique constraint remains a future hardening option if concurrent duplicate
  creation must be prevented at the storage race boundary.

No B15, Flow, notification, SynqFund, CareConnect, deployment, cloud, push,
merge, or production work was performed.