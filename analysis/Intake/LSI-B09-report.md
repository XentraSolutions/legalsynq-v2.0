# LSI-B09 — Normalization & Evidence for Synq Intake

## Ticket and objective

Implement deterministic normalization and evidence preservation for B08 source facts in
`apps/services/intake/**`. B09 must produce comparison-ready, structurally validated
normalized candidates while preserving B08 raw values, source confidence, evidence, tenant
isolation, immutable history, and current-result semantics. Matching, verification, review,
business decisioning, and downstream routing remain out of scope.

## Repository baseline recorded before implementation

- Repository: `XentraSolutions/legalsynq-v3.0`
- Branch: `xenia`
- Baseline HEAD: `8d10555a22f6e80d8208dd6de0023e2cad1b4cc0`
- Baseline `origin/xenia`: `9d26ea4a30130c5594db7ac3b32a60703aad61e9`
- Initial relationship: local branch was 6 commits ahead of `origin/xenia`.
- Initial tracked working-tree state was not clean. Pre-existing changes included deletion
  of the tracked historical B01–B08 reports and an untracked `analysis/Intake/` tree.
  Those changes are preserved and are not part of B09.
- The uploaded B09 specification is present under `attached_assets/` and is not included
  in the implementation report or tracked deliverables.

## Architecture reviewed

Reviewed the B02 `LIEN_INTAKE_V1` processing-profile validation and tenant configuration
resolution, B07/B08 authorization and API conventions, B08 extraction entities/repository
history/idempotency/current-result behavior, the B08 fact catalog/data types/evidence JSON,
the Intake EF context/configurations/migrations, dependency injection, and existing tests.

The implementation evidence and final validation results will be added below after the
normalization work is complete.

## Implementation summary

B09 is implemented as a deterministic normalization layer over the immutable B08
`ArtifactExtraction` and `ArtifactExtractedFact` records:

```text
B08 ArtifactExtractedFact.RawValue
  -> deterministic fact normalizer
  -> normalized value / typed JSON / comparison key
  -> structural validation and warning codes
  -> ArtifactNormalizedFact with source-fact and evidence references
  -> B10 matching input
```

No AI provider, business-domain database, entity matching, duplicate detection, review,
verification, Case/Lien creation, or downstream routing was introduced.

## Files added

- `Intake.Domain/Normalization/NormalizationCodes.cs`
- `Intake.Domain/Normalization/NormalizationProfileDefinition.cs`
- `Intake.Domain/Normalization/ArtifactNormalization.cs`
- `Intake.Domain/Normalization/ArtifactNormalizedFact.cs`
- `Intake.Domain/Normalization/NormalizationDefinitionIds.cs`
- `Intake.Contracts/Normalization/NormalizationContracts.cs`
- `Intake.Application/Normalization/FactNormalizationContracts.cs`
- `Intake.Application/Normalization/FactNormalizerRegistry.cs`
- `Intake.Application/Normalization/NormalizationText.cs`
- `Intake.Application/Normalization/FactNormalizers.cs`
- `Intake.Application/Normalization/IArtifactNormalizationRepository.cs`
- `Intake.Application/Normalization/IArtifactNormalizationService.cs`
- `Intake.Application/Normalization/NormalizationAudit.cs`
- `Intake.Application/Normalization/NormalizationService.cs`
- `Intake.Infrastructure/Audit/NormalizationAuditSink.cs`
- `Intake.Infrastructure/Persistence/EfArtifactNormalizationRepository.cs`
- `Intake.Api/Endpoints/NormalizationEndpoints.cs`
- `Intake.Infrastructure/Persistence/Migrations/20260814043939_AddNormalizationAndEvidenceV1.cs`
- `Intake.Infrastructure/Persistence/Migrations/20260814043939_AddNormalizationAndEvidenceV1.Designer.cs`
- `Intake.Tests/NormalizationTests.cs`

## Files modified

- `Intake.Contracts/Configuration/LienIntakeV1Configuration.cs`
- `Intake.Application/Configuration/ProcessingProfileRegistry.cs`
- `Intake.Infrastructure/Persistence/Configurations/ExtractionConfiguration.cs`
- `Intake.Infrastructure/Persistence/IntakeDbContext.cs`
- `Intake.Infrastructure/DependencyInjection.cs`
- `Intake.Infrastructure/Persistence/Migrations/IntakeDbContextModelSnapshot.cs`
- `Intake.Api/Program.cs`

The uploaded specification asset remains untracked and was not added to the implementation.
Pre-existing report deletions and the untracked `analysis/Intake/` tree were not changed.

## Normalization profile and B02 configuration

`LIEN_INTAKE_NORMALIZATION_V1` version 1 is seeded in `NormalizationProfileDefinitions`.
It declares:

- supported B08 fact codes and their data types;
- normalizer version `1`;
- Unicode form `NFKC`;
- comparison strategy `UPPER_ASCII_ALNUM`;
- default date culture `en-US`;
- default country `US`;
- default currency `USD`.

`LIEN_INTAKE_V1` now supports:

- `EnableNormalization` (default `true`);
- `NormalizationProfileCode` (default `LIEN_INTAKE_NORMALIZATION_V1`);
- `DefaultCountryCode` (default `US`);
- `DefaultCurrencyCode` (default `USD`);
- `DateCulture` (default `en-US`);
- `AllowAmbiguousDateNormalization` (default `false`).

The processing-profile registry validates profile-code syntax, two-letter country codes,
three-letter currency codes, and installed .NET date cultures. Normalization is rejected
when disabled, the profile is unavailable/inactive, or no completed current B08 extraction
exists.

## ArtifactNormalization model

`ArtifactNormalization` is an Intake-owned immutable execution/history record with:

- tenant, artifact, and exact `ArtifactExtractionId`;
- normalization profile/version and normalizer rule version;
- deterministic execution key;
- processing, completed, partial, or failed status;
- current-result marker;
- request/completion timestamps;
- safe failure code/message fields.

The repository uses a unique execution key, tenant-scoped queries, immutable history rows,
and an atomic current-result finalization transaction. A new extraction or profile/rule
version produces a distinct execution key and therefore a new normalization run.

## ArtifactNormalizedFact model

`ArtifactNormalizedFact` stores a separate candidate record for every B08 source fact:

- exact `ArtifactExtractedFactId`;
- unchanged copied `RawValue`;
- `NormalizedValue`;
- typed `NormalizedJson`;
- separate `ComparisonKey`;
- `NormalizationStatus`;
- separate `ValidationStatus`;
- deterministic method/version;
- preserved `SourceConfidence`;
- structured warning-code JSON;
- exact B08 evidence JSON reference;
- original fact ordinal.

There is a unique `(ArtifactNormalizationId, ArtifactExtractedFactId)` constraint, so a
source fact cannot be duplicated inside one run. Duplicate normalized values are not
deduplicated across source facts.

## Source binding, raw values, confidence, and evidence

- Every run binds to the exact current completed `ArtifactExtraction.Id`, extraction
  execution identity, profile version, and normalizer version.
- Every normalized fact binds to the exact `ArtifactExtractedFact.Id`.
- B08 `RawValue` is never updated. B09 stores it separately for direct response projection
  while retaining the source-fact relationship.
- B08 `Confidence` is copied to `SourceConfidence`; B09 does not produce a second AI score.
- Low-confidence but schema-valid B08 facts are still normalized.
- B08 `EvidenceJson` is copied byte-for-byte into `EvidenceReferenceJson`; no page, offset,
  bounding box, or source locator is fabricated.
- Evidence is returned only through the authorized normalization read surface and is absent
  from logs and audit metadata.

## Normalizer registry and deterministic normalizers

`IFactNormalizerRegistry` resolves `FactCode`/`DataType` to separate deterministic
normalizers. Normalizers never access business databases:

- `PersonNameNormalizer`: supports patient/attorney names, comma-form names, first/middle/
  last/suffix structure, conservative ambiguity handling, and diacritic-preserving display.
- `OrganizationNormalizer`: supports provider, facility, law-firm, and insurer names;
  standardizes known corporate suffix punctuation without expanding abbreviations.
- `DateNormalizer`: uses configured culture, emits `YYYY-MM-DD`, preserves parse metadata,
  warns when culture resolves a numeric date, and rejects invalid dates.
- `MoneyNormalizer`: parses decimal values, explicit/default currency, parentheses and
  negative formats, emits currency assumptions, and never converts currencies.
- `PhoneNormalizer`: supports US default-country parsing, E.164 output, and common
  extensions; country assumptions are warned.
- `EmailNormalizer`: trims, validates syntax, lowercases only the domain, and does not
  repair malformed addresses.
- `AddressNormalizer`: conservatively parses simple US-style structure and returns
  partial/ambiguous results when segmentation is not reliable.
- `IdentifierNormalizer`: preserves meaningful display punctuation while producing a
  comparison key with irrelevant punctuation removed.
- `TextNormalizer`: applies NFKC, removes control characters, normalizes line breaks and
  whitespace, and preserves substantive text without paraphrasing.

Display-normalized values retain meaningful punctuation and diacritics. Comparison keys
are separate internal matching aids and use deterministic uppercase, diacritic-free
alphanumeric representations. They are returned only on authorized normalization reads
and are never logged.

## Structural validation and warnings

Normalization status is separate from source confidence and validation status:

- run statuses: `PROCESSING`, `COMPLETED`, `PARTIAL`, `FAILED`;
- fact statuses: `NORMALIZED`, `PARTIAL`, `INVALID`, `AMBIGUOUS`, `UNSUPPORTED`;
- validation statuses: `VALID`, `INVALID_FORMAT`, `INCOMPLETE`, `AMBIGUOUS`, `UNVERIFIED`.

Stable warning codes include:

- `DATE_AMBIGUOUS`, `DATE_CULTURE_APPLIED`, `DATE_RANGE_INVALID`;
- `PHONE_COUNTRY_ASSUMED`;
- `ADDRESS_INCOMPLETE`;
- `NAME_COMPONENTS_PARTIAL`;
- `CURRENCY_ASSUMED`;
- `IDENTIFIER_FORMAT_UNRECOGNIZED`;
- `EMAIL_INVALID`;
- `ORGANIZATION_NORMALIZATION_PARTIAL`.

Safe cross-fact checks flag, but never silently rewrite:

- service start/end ordering;
- effective/expiration ordering;
- date of birth/document date ordering.

Malformed, ambiguous, or unsupported individual facts remain represented. A run becomes
`PARTIAL` when at least one fact is not fully normalized; a zero-fact successful extraction
can complete with an empty normalized fact set.

## APIs and authorization

Added endpoints:

- `GET /normalization/profiles`
- `GET /artifacts/{artifactId}/normalization`
- `GET /artifacts/{artifactId}/normalization/history`
- `POST /artifacts/{artifactId}/normalization`

The read routes reuse `intake.ai.read` through the existing classification-read policy,
and the execute route reuses `intake.ai.manage` through the existing classification-manage
policy. Tenant ID comes only from the trusted request context. Artifact, extraction,
normalization, source facts, and evidence are all tenant-scoped.

## Audit, logging, and analytics

`NormalizationAuditSink` emits tenant-visible `intake.artifact.normalization` events with
only safe metadata: IDs, profile code, status, correlation, and aggregate fact counts.
Raw values, normalized values, comparison keys, and evidence snippets are excluded.

Operational logs contain only execution identifiers, statuses, and aggregate failure
context. No normalization analytics beyond audit counts were added, and no PHI/PII values
are exposed to cross-tenant analytics.

Normalization is an explicit API-triggered operation. No event/outbox trigger was added,
so B09 does not execute inside the B08 extraction transaction and does not change the B08
manual/explicit trigger model.

## Database schema, indexes, and migration

Migration: `20260814043939_AddNormalizationAndEvidenceV1`.

Added tables:

- `NormalizationProfileDefinitions`
- `ArtifactNormalizations`
- `ArtifactNormalizedFacts`

The migration includes:

- unique profile code/version;
- unique normalization execution key;
- current-result lookup and uniqueness indexes;
- tenant/artifact/extraction lookup indexes;
- tenant/normalization/fact-code indexes;
- FK `ArtifactNormalization -> ArtifactExtraction`;
- FK `ArtifactNormalizedFact -> ArtifactNormalization`;
- FK `ArtifactNormalizedFact -> ArtifactExtractedFact`;
- unique source-fact-per-normalization constraint.

`dotnet ef dbcontext info` resolved `IntakeDbContext` with
`Pomelo.EntityFrameworkCore.MySql`, database name `intake_design`, and localhost data
source. `dotnet ef migrations list` lists the complete B02–B09 chain, including the new
B09 migration.

## Test evidence

Added `NormalizationTests.cs` with 9 B09-focused tests covering:

- profile/registry resolution;
- person names, comma forms, suffix/middle handling, ambiguity, and diacritics;
- organization suffix handling and no abbreviation expansion;
- culture-based dates and invalid dates;
- money parsing, default/explicit currencies, assumptions, and negative support;
- phone, extension, email, address, identifier, Unicode, and generic text behavior;
- raw value, evidence, source confidence, and low-confidence preservation;
- partial runs with invalid and unsupported facts retained;
- idempotency and immutable history across a new extraction;
- tenant isolation and completed-extraction eligibility.

Final Intake test result: **55 passed, 0 failed, 0 skipped**.

## Build and repository validation

- `dotnet build apps/services/intake/Intake.Api/Intake.Api.csproj --no-restore -m:1 /p:BuildInParallel=false`:
  **0 errors**.
- `dotnet test apps/services/intake/Intake.Tests/Intake.Tests.csproj --no-restore -m:1 /p:BuildInParallel=false`:
  **55 passed, 0 failed**.
- `dotnet build LegalSynq.sln --no-restore -m:1 /p:BuildInParallel=false`:
  **0 errors**, 106 existing warnings.
- `dotnet ef dbcontext info`: succeeded.
- `dotnet ef migrations list`: B09 migration discovered and listed; applied status was
  unavailable because the configured localhost MySQL database was unreachable.
- `git diff --check`: passed.
- No Intake `bin/` or `obj/` files were added to the tracked/untracked implementation set.

The warnings observed are existing package/dependency warnings, including the repository's
known MimeKit and Microsoft.Extensions.Caching.Memory advisories, plus unrelated solution
warnings. No new B09 build errors remain.

## MySQL validation and known gaps

Full B02–B09 migration application and live FK/index/uniqueness verification were not
possible. The repository-pinned EF tooling could inspect the model and enumerate all
migrations, but the configured local MySQL endpoint at `localhost` was not reachable and
no disposable MySQL 8 instance was available. This leaves the prior B04–B08 disposable
database-validation debt open as well; no cloud/shared database was used.

HTTP integration tests were not added because the Intake test project currently uses
service-level fakes rather than a reusable HTTP test host. The API route wiring,
authorization policy reuse, and service contracts are compiled and covered at the
application boundary.

## Security, performance, and out-of-scope confirmation

- No AI credentials or external network calls are involved in B09.
- No raw values, normalized values, comparison keys, names, dates, phones, emails,
  addresses, amounts, or evidence snippets are logged.
- No business-domain queries or matching behavior were introduced.
- Facts are normalized in memory and persisted as one bounded batch per run; old history
  is loaded only by explicit history reads.
- No patient/provider/facility/attorney/case/lien matching, fuzzy search, deduplication,
  ranking, confidence routing, human review, correction UI, OCR, address verification,
  phone-owner verification, currency conversion, Case/Lien creation, downstream routing,
  AWS, RDS, ECS, Route53, deployment, push, commit, PR, or merge work was performed.
- LSI-B10 was not started.

## Final repository validation status

**LSI-B09 is implemented and locally validated at the application/code level.**
The B09 migration and model are generated and discoverable. MySQL application remains
explicitly unverified because the disposable/local database endpoint was unavailable.
Pre-existing unrelated working-tree changes remain preserved.

## Recommended next bundle

The next scoped bundle is **LSI-B10 matching**, using B09's
`FactCode`, `NormalizedValue`, `NormalizedJson`, `ComparisonKey`, structural statuses,
warning codes, `SourceConfidence`, evidence references, ordinal, and exact B08 source
identifiers. B10 must keep matching and verification separate from B09 normalization.