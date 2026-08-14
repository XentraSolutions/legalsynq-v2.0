# LSI-B10 — Tenant Matching, Scoring & Duplicates

## Status

Implemented and locally validated on branch `xenia`. B10 is deterministic, tenant-scoped,
explainable, and bounded to candidate discovery, scoring, ranking, and duplicate signals.
It does not accept or reject matches, create human-review decisions, create Case/Lien
records, route downstream work, or begin B11.

## Scope and design decisions

- B10 consumes only the current successful or partial B09 normalization result.
- Raw B08 extraction and B09 normalized facts remain immutable; match runs and their
  field-level explanations are separate history records.
- No AI, OCR, address verification, currency conversion, or external identity-resolution
  service is used by the matching algorithm.
- Candidate sources are read-only abstractions. Intake does not own or query Patient,
  Provider, Facility, Attorney, Law Firm, Case, or Lien master tables directly.
- Supported entity types are `PATIENT`, `PROVIDER`, `FACILITY`, `ATTORNEY`, `LAW_FIRM`,
  and `CASE`. Lien matching remains deferred because no clean existing SynqLien
  read-only matching boundary was available.

## Implementation inventory

### Domain and contracts

- `Intake.Domain/Matching/MatchingCodes.cs` defines stable entity, run, match,
  duplicate, failure, and explainability reason codes.
- `MatchingProfileDefinition`, `ArtifactMatchRun`, `ArtifactEntityMatch`,
  `ArtifactMatchField`, and `ArtifactDuplicateSignal` model versioned profiles,
  immutable execution history, candidate ranking, field explanations, and duplicate
  signals.
- `Intake.Contracts/Matching/MatchingContracts.cs` exposes profile, run, candidate,
  field, duplicate, and execution request/response contracts.
- B10 configuration adds matching enablement/profile selection, candidate pool limits,
  minimum score, duplicate enablement, and source-confidence controls.
- `ProcessingProfileRegistry` validates matching profile codes and bounded candidate
  limits before configuration is accepted.

### Matching application layer

- `MatchingProfileContracts.cs` contains the seeded
  `LIEN_INTAKE_MATCHING_V1` profile and its field/entity rules.
- `CandidateProviderContracts.cs` separates discovery from scoring and registers one
  read-only provider for each supported entity type.
- `MatchScoring.cs` performs deterministic exact, normalized-exact, fuzzy, partial,
  missing, invalid, and conflict comparisons. Repeated/aliased source facts use a
  deterministic best-compatible-source policy with source-fact ID tie-breaking.
- `MatchingService.cs` loads the current B09 result, applies tenant/configuration
  guards, creates idempotent runs, discovers candidates, scores and ranks results,
  records provider failures as partial-run state, detects duplicate signals, finalizes
  the current result transactionally, and maps safe explainability responses.
- `IArtifactMatchingRepository` and `MatchingAudit.cs` keep persistence and audit
  boundaries independent of the matching algorithm.

### Candidate-source boundary

`HttpTenantMatchCandidateSource` is a minimal configurable HTTP adapter. It:

- sends only normalized comparison facts, not raw artifact contents;
- bounds the requested and returned candidate pool;
- sends `X-Tenant-Id` and a configured `X-Internal-Token`;
- requires a response-level tenant attestation and per-candidate tenant attestation;
- rejects absolute configured paths to avoid path-based destination escapes;
- maps timeout, HTTP, malformed JSON, unavailable-source, and invalid-projection
  failures to safe B10 codes.

The configured destination remains an integration boundary. A downstream master-data
service must enforce the same tenant scope and implement the response contract.

### API and dependency injection

`MatchingEndpoints.cs` adds authenticated routes for:

- `GET /matching/profiles`
- `GET /artifacts/{artifactId}/matching`
- `GET /artifacts/{artifactId}/matching/history`
- `POST /artifacts/{artifactId}/matching`

Read routes use the existing classification-read policy and execution uses the existing
classification-manage policy. Services, six entity providers, candidate-source options,
repository, and audit sink are registered in
`Intake.Infrastructure/DependencyInjection.cs`. The API registers
`MapMatchingEndpoints()` in `Program.cs`.

## Scoring and ranking

For every configured field:

1. B09 `ComparisonKey` is preferred over normalized text.
2. Exact/normalized-exact comparisons score `1.00`.
3. Person-name, organization, and address fields use bounded Levenshtein similarity:
   `>= 0.80` is fuzzy, `>= 0.45` is partial, and lower values are conflicts.
4. A field's effective weight is:
   `configuredWeight × sourceConfidence`, when enabled.
5. Ambiguous B09 facts receive an additional `0.50` factor.
6. Missing candidate data is neutral and contributes no denominator weight.
7. Invalid/unsupported source facts are retained in the explanation but contribute
   zero weight.
8. Conflicts subtract `effectiveWeight × conflictPenalty`; hard conflicts cap the
   final score at the profile maximum.
9. The normalized score is:
   `(positiveWeightedScore - conflictPenalties) / comparableEffectiveWeight`,
   clamped and rounded to `0.00–1.00`.

Status classification is profile-driven (`STRONG`, `POSSIBLE`, `WEAK`,
`CONFLICTED`, or `INSUFFICIENT_DATA`). Profiles that require a hard identifier now
require an actual positive hard-identifier field, not merely two matched fields.

Candidate ranking is deterministic by descending score, descending matched-field count,
ascending conflict count, and ascending candidate ID. Both search-pool and persisted
candidate limits are configuration-validated and enforced.

## Duplicate detection

Duplicate detection is independent of entity matching:

- Same-tenant SHA-256 lookup emits `EXACT_ARTIFACT_DUPLICATE`.
- Matching `EMAIL` and `MANUAL` source types emit an additional
  `CONTENT_DUPLICATE` signal for the same content.
- The profile-defined
  `PATIENT_PROVIDER_ACCOUNT_SERVICE_DATE` composite fingerprint emits
  `BUSINESS_KEY_DUPLICATE` only when required source facts are valid/exactly keyed and
  the required top candidates have a conflict-free `STRONG` or `POSSIBLE` result with
  an exact/normalized-exact field outcome.
- Duplicate signals include related artifact/entity references, score, status, reason
  code, and safe evidence metadata. No automatic acceptance or rejection occurs.

All duplicate lookups are tenant-filtered. Business fingerprints are SHA-256 hashes of
the stable rule code, required candidate IDs, and required normalized comparison keys.

## Persistence and migration

Migration:

`apps/services/intake/Intake.Infrastructure/Persistence/Migrations/20260814053606_AddTenantMatchingAndDuplicatesV1.cs`

Added tables:

- `MatchingProfileDefinitions`
- `ArtifactMatchRuns`
- `ArtifactEntityMatches`
- `ArtifactMatchFields`
- `ArtifactDuplicateSignals`

The migration seeds the versioned B10 profile and adds indexes for execution identity,
current-result selection, tenant-scoped normalization lookup, candidate ranking,
business-fingerprint lookup, and duplicate signal lookup.

Tenant-aware alternate keys and composite foreign keys bind:

- match runs to the same-tenant artifact and normalization;
- entity matches and duplicate signals to the same-tenant run;
- field explanations to the same-tenant entity match.

`EfArtifactMatchingRepository` writes child results and switches the current marker in
one transaction. Prior runs remain immutable. The same-tenant SHA-256 artifact lookup
was added to `IIntakeArtifactRepository` and its EF implementation.

The local EF context is configured for Pomelo MySQL with `intake_design` on localhost.
`dotnet ef migrations list` could enumerate the migrations but could not determine
applied status because the local database was unavailable; no migration was applied by
this task.

## Audit and failure behavior

`MatchingAuditSink` emits tenant-visible audit events for requested, completed,
cancelled, and failed runs without including raw PHI values. Audit delivery failures
are logged and do not replace the matching result.

Provider failures are isolated per entity type and produce `PARTIAL` runs when other
entity types complete. Cancellation marks the persisted run `FAILED` with
`MATCH_EXECUTION_CANCELLED` before rethrowing, preventing a permanently stuck
`PROCESSING` execution key.

## Tests and validation

`apps/services/intake/Intake.Tests/MatchingTests.cs` covers:

- seeded profile structure and hard-identifier rules;
- exact, normalized, fuzzy, conflict, missing, invalid, and confidence-weighted
  comparisons;
- repeated/aliased source-fact selection;
- tenant-guarded provider registry;
- authenticated candidate-source requests and tenant-attested responses;
- idempotent execution and explainable field persistence;
- partial provider failure;
- same-tenant exact and cross-source duplicate signals.

Validation results:

- Intake API build: passed, 0 errors.
- Intake test project: passed, **66 tests, 0 failures**.
- Full `LegalSynq.sln` build: passed, **0 errors**; existing package and assembly
  version warnings remain.
- `dotnet ef dbcontext info`: passed; Pomelo MySQL context discovered.
- `dotnet ef migrations list`: passed with local database status unavailable.
- `git diff --check`: passed.
- Configured `Start application` workflow: restarted successfully and remained running.

Known pre-existing validation warnings include vulnerable package advisories for
`MimeKit`, `Microsoft.Extensions.Caching.Memory`, and other solution dependencies,
plus unrelated EF assembly-version and solution-configuration warnings. They were not
introduced or remediated by B10.

## Preserved unrelated state

The pre-existing B09 report deletion/untracked copy and the uploaded B10 specification
remain untouched. The uploaded specification remains untracked as requested. No commit,
push, deployment, PR, prior-report rewrite, or B11 work was performed.