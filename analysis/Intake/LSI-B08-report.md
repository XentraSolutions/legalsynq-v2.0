# LSI-B08 Lien Intake Intelligence V1

## Ticket and objective

LSI-B08 adds tenant-scoped, document-type-aware lien-intake fact extraction to Synq Intake. It consumes a successful B07 classification and produces source-preserved, structured facts with provenance and bounded evidence. It does not normalize, match, verify, route, review, or create downstream business records.

## Repository baseline recorded before implementation

- Repository path: `/home/runner/workspace`
- Branch: `xenia`
- Baseline HEAD: `1d80d035b318920a7a816a70324594ce1be26615`
- `origin/xenia`: `9d26ea4a30130c5594db7ac3b32a60703aad61e9`
- Initial relationship: `origin/xenia...HEAD = 0 behind / 5 ahead`
- Initial working tree: clean relative to tracked files; only the uploaded B08 specification was untracked:
  `attached_assets/Pasted-You-are-implementing-LSI-B08-Lien-Intake-Intelligence-V_1786680627246.txt`
- Prior reports are preserved and read-only: `analysis/LSI-B05-report.md`, `analysis/LSI-B06-report.md`, and `analysis/LSI-B07-report.md`.
- No cloud, deployment, push, PR, merge, or B09 work is part of this ticket.

## Baseline architecture reviewed

B02–B07 provide the Intake configuration/profile framework, tenant-scoped `IntakeArtifact` and Documents content boundary, manual/email artifact paths, B07 classification profiles/taxonomies/prompts, tenant AI policy and credential-reference isolation, provider registry and structured-output adapter, classification history/idempotency/telemetry, authorization, audit, MySQL migrations, and focused unit-test conventions.

The implementation details, changed-file inventory, validation evidence, limitations, and final acceptance status will be completed below after the B08 changes are implemented.

## Implemented scope

### Configuration and definitions

- Extended the typed `LIEN_INTAKE_V1` processing profile with extraction enablement, profile selection, automatic-extraction permission, minimum fact confidence, bounded input/output, timeout, retry, fact-count, value-length, and evidence-length controls.
- Added the versioned `LIEN_INTAKE_EXTRACTION_V1` profile.
- Added document-type-specific versioned prompt and schema definitions for:
  `MEDICAL_BILL`, `MEDICAL_RECORD`, `LIEN_DOCUMENT`, `LETTER_OF_PROTECTION`,
  `EXPLANATION_OF_BENEFITS`, `SETTLEMENT_DOCUMENT`, `ATTORNEY_DOCUMENT`,
  `CORRESPONDENCE`, and `INSURANCE_DOCUMENT`.
- `IDENTIFICATION_DOCUMENT`, `OTHER`, and `UNKNOWN` are deterministically rejected as unsupported extraction classifications.
- Added a centralized fact catalog with explicit source semantics and data types for names, identifiers, dates, money, addresses, booleans, and text.

### Extraction and provenance

- Added `ArtifactExtraction` history/current-result persistence and `ArtifactExtractedFact` child persistence.
- Each execution binds to the artifact SHA-256, exact completed classification ID/code, profile/schema/prompt versions, provider/model, and an immutable execution key.
- Raw values are preserved. `NormalizedCandidateValue` is explicitly noncanonical and is not used for matching, decisioning, or downstream writes.
- Repeated facts are retained independently with per-fact confidence, bounded evidence, ordinal, and optional normalized candidates. Facts below the configured minimum confidence are omitted from the returned fact set without changing the source record.
- Extraction requires a completed artifact, a SHA-256 binding, and a successful current classification whose hash matches the artifact.
- Explicit extraction is available through the API; automatic extraction remains disabled unless a future caller deliberately adds that workflow.

### AI, security, and operations

- Reused the B07 provider registry, tenant AI policy, credential-reference resolution, timeout, retry, structured-output, audit, and usage telemetry boundaries.
- Extended the provider contract with a structured-extraction capability interface and added an OpenAI JSON-schema adapter. No raw credential is persisted or logged.
- Provider responses are strictly checked for allowed fact fields, known fact codes, catalog membership for the classified document type, data types, confidence range, bounded values, bounded evidence, and fact count.
- Prompts and provider requests treat document text as untrusted data, prohibit hidden reasoning, and preserve only bounded source evidence.
- Extraction APIs reuse the B07 classification read/manage authorization policies and all repository queries are tenant-scoped.
- Audit events use the `intake.artifact.extraction` event type and operational logs contain identifiers, status, provider, and failure codes only—not document text, fact values, evidence, or prompts.

### API and database

- Added:
  - `GET /extraction/profiles`
  - `GET /artifacts/{artifactId}/extraction`
  - `GET /artifacts/{artifactId}/extraction/history`
  - `POST /artifacts/{artifactId}/extraction`
- Added migration `20260814041457_AddLienIntakeExtractionV1` and updated the Intake model snapshot.
- Current-result finalization is transactional and clears prior current extraction markers while preserving immutable history.
- Duplicate execution claims are guarded by the unique execution key; retries use a new immutable attempt row and monotonic attempt number.

## Changed-file inventory

- Domain: extraction statuses/failures/data types, extraction entities, definition entities, and stable definition IDs.
- Contracts: extraction request/profile/result/fact DTOs and B02 extraction guardrails.
- Application: provider-neutral extraction contracts, fact catalog, input policy, repository boundary, audit contract, service, and processing-profile validation.
- Infrastructure: OpenAI extraction adapter, EF mappings/seeds/repository/context registrations, audit sink, migration, and model snapshot.
- API: extraction endpoints and endpoint registration.
- Tests: `Intake.Tests/ExtractionTests.cs`.
- Report: this file only; prior B05–B07 reports were not modified.

## Validation evidence

- B08-focused tests: **6 passed, 0 failed**.
- Full Intake test project: **46 passed, 0 failed, 0 skipped**.
- Intake API build: **0 errors**, existing package/security and dependency warnings only.
- Full `LegalSynq.sln` build: **0 errors**; existing repository warnings remain.
- EF migration discovery lists `20260814041457_AddLienIntakeExtractionV1` after the B07 migrations.
- `git diff --check`: passed.
- Workflow `Start application`: restarted successfully after implementation and final validation.

The focused tests cover document-type mapping/exclusion, guardrail validation, raw-value and candidate preservation, exact classification/hash provenance, retry history, idempotent execution, and tenant isolation.

## Verification limits and follow-up boundary

- No live provider call was made; OpenAI response parsing and service validation are covered by the provider-neutral test doubles and application tests, while external provider availability remains environment-dependent.
- Applying the migration to a disposable MySQL instance was not verified because no reachable disposable MySQL database was available in this environment. `dotnet ef migrations list` successfully discovered the generated migration but could not query applied status against the unavailable local database.
- OCR, PDF/binary extraction, normalization, entity matching, verification, human review, lien creation, settlement decisions, and B09 behavior remain intentionally out of scope.

## Acceptance status

**LSI-B08 implementation complete and locally validated within the available environment.** No deployment, push, commit, PR, or B09 work was performed.