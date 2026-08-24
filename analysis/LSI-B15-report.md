# LSI-B15 — Document Association / Workflow for Synq Intake

## 1. Objective and completion status

**Status: implemented and locally validated.**

B15 consumes the immutable B13 `ApprovedIntakeSnapshotV1` document references
and the durable B14 adapter references, validates the Documents-service
metadata and Liens-side destination context, then creates idempotent Case/Lien
document-reference associations. No binary content is copied into Intake or
Liens.

The work stops at B15. No B16 work, deployment, cloud migration, push, merge,
or production operation was performed.

## 2. Scope and architecture

- B13 snapshots remain immutable inputs. B15 does not re-run classification,
  matching, or duplicate detection.
- B14 `CASE` and `LIEN` external references are authoritative destination IDs;
  snapshot entity decisions are used only to determine which destinations are
  required.
- Intake owns the versioned association policy, execution/item state,
  validation, retries, deterministic item keys, and destination idempotency
  keys.
- Documents remains authoritative for binary content and metadata. Intake
  calls its tenant-scoped internal metadata endpoint and never downloads or
  stores document bytes.
- Liens remains authoritative for Case/Lien document associations. Intake
  calls a dedicated internal Liens service-token endpoint; it does not read or
  write Liens tables.
- Tenant, actor, correlation, organization, service-token, and idempotency
  context are propagated across service calls.

## 3. Policy behavior

Policy code is `LIEN_INTAKE_DOCUMENT_ASSOCIATION_V1`, version `1`.

| Effective classification | Destination | Role |
| --- | --- | --- |
| `MEDICAL_RECORD`, `MEDICAL_BILL`, `MEDICAL_STATEMENT` | `LIEN` | classification |
| `LIEN_DOCUMENT`, `LETTER_OF_PROTECTION` | `LIEN` | classification |
| `ATTORNEY_DOCUMENT`, `CORRESPONDENCE`, `INSURANCE_DOCUMENT` | `CASE` and `LIEN` | `SUPPORTING_DOCUMENT` |
| unsupported/no valid destination | persisted `SKIP` item | none |

The policy uses the effective classification, so human overrides are honored.
Multiple approved documents produce separate deterministic items. Email bodies
or documents without a valid Documents-service ID are not associated; they
become non-retryable item failures when the execution runs.

## 4. Execution and retry semantics

- Execution keys are tenant-scoped and snapshot-scoped.
- Items are persisted before destination processing to reduce crash-time record
  loss.
- Each item validates tenant ownership, deleted/inactive status, and an
  optional approved-vs-current SHA-256 checksum before association.
- Destination keys are stable across retries. Liens replays return the existing
  association for the same tenant/idempotency key.
- `SUCCEEDED`, `FAILED`, `RETRYABLE`, `PARTIALLY_SUCCEEDED`, and `SKIPPED`
  states are persisted. Retry processes only failed/retryable items.
- Dry-run is not part of the B15 destination API; B15 is explicit and only
  executes when its endpoint is called.

## 5. Service contracts and routes

### Intake

- `GET /api/intake/snapshots/{snapshotId}/document-associations`
- `GET /api/intake/snapshots/{snapshotId}/document-associations/{executionId}`
- `POST /api/intake/snapshots/{snapshotId}/document-associations/execute`
- `POST /api/intake/snapshots/{snapshotId}/document-associations/{executionId}/retry`

### Documents

- `GET /internal/intake/documents/{id}`
- Protected by the `DocumentsServiceInternal` service-token policy.
- Returns tenant, status, MIME type, checksum, and deletion state only.

### Liens

- `POST /api/internal/synqlien/document-associations`
- Protected by `SynqLienInternal`.
- Validates tenant-scoped Case/Lien existence, optional Lien-to-Case
  relationship, required `Idempotency-Key`, and duplicate replay behavior.

## 6. Persistence and implementation files

### Intake

- `DocumentAssociationEntities.cs`
- `DocumentAssociationContracts.cs`
- `SynqLienDocumentAssociationPolicy.cs`
- `DocumentAssociationExecutionService.cs`
- `DocumentAssociationConfiguration.cs`
- `EfDocumentAssociationExecutionRepository.cs`
- `SynqLienDocumentAssociationClient.cs`
- `IntakeDbContext.cs` and dependency-injection registration
- snapshot API endpoint and authorization additions
- generated migration `20260814075140_B15DocumentAssociationModel` and updated
  `IntakeDbContextModelSnapshot`

### Documents

- `InternalDocumentMetadataResponse.cs`
- tenant-scoped internal metadata lookup in `DocumentService`
- internal endpoint and service-token policy registration

### Liens

- `SynqLienDocumentAssociation.cs`
- `SynqLienDocumentAssociationConfiguration.cs`
- `SynqLienInternalEndpoints.cs`
- `LiensDbContext.cs`
- generated migration `20260814075205_B15SynqDocumentAssociationModel` and
  updated `LiensDbContextModelSnapshot`

### Tests

- `Intake.Tests/B15DocumentAssociationTests.cs`
- service-token, idempotency, and relationship test added to
  `Liens.Api.Tests/Tests/LienEndpointTests.cs`
- existing fake Documents clients updated for the tenant-scoped metadata
  contract

## 7. Validation performed

All commands were run locally with single-worker MSBuild; `git diff --check`
passed.

- Intake API build: **passed**
- Liens API build: **passed**
- Documents API build: **passed**
- B15 policy tests: **3/3 passed**
- Full Intake test project: **94/94 passed**
- Liens `LienEndpointTests`: **25/25 passed**
- New Liens internal association HTTP test: **passed**, covering service-token
  authorization, first-write creation, idempotent replay, and relationship
  mismatch rejection
- Configured `Start application` workflow: restarted successfully and remains
  running

Builds report pre-existing NuGet vulnerability/package-pruning warnings, but no
compilation errors. The restarted workflow also reports existing solution
configuration warnings for Monitoring projects. Browser console output contains
pre-existing frontend invalid-hook/hydration messages unrelated to B15.

## 8. Known limitations and follow-up candidates

- B15 does not yet have a full cross-service Intake → Documents → Liens
  integration harness. The focused tests cover policy behavior and the Liens
  HTTP boundary; missing/deleted-document, checksum, partial-success,
  retry-only-failed, concurrency, response-loss, and historical-snapshot
  scenarios remain implementation paths without dedicated automated tests.
- A stale `PROCESSING` execution recovery/sweeper is not included. The initial
  execution is saved before processing, but a process crash can leave it in
  `PROCESSING` until operational reconciliation is added.
- B15-specific audit event emission and PHI-safe audit detail assertions were
  not added; existing platform/service-token conventions are reused.
- The internal Documents metadata endpoint was build-validated but does not
  yet have a dedicated service-token HTTP test.

## 9. Explicit non-scope

OCR, extraction, classification, document editing/redaction, arbitrary
uploads, binary storage, unlinking, lien selling, Flow, notifications,
SynqFund, CareConnect, B16 dashboards/dead-letter tooling, deployment,
production migration, cloud infrastructure, push, and merge remain out of
scope.