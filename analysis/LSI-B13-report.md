# LSI-B13 — Approved Snapshot & Adapter Framework

## Ticket and objective

- Ticket: LSI-B13 — Approved Snapshot & Adapter Framework for Synq Intake.
- Objective: create an immutable, versioned approved snapshot from the canonical
  B12 effective reviewed projection and expose a product-neutral adapter boundary
  with a safe `NOOP_V1` implementation.
- Service root: `apps/services/intake`.
- Product-specific adapters and all B14 downstream writes remain out of scope.

## Repository baseline

- Repository: `XentraSolutions/legalsynq-v3.0`.
- Branch: `xenia`.
- Baseline HEAD: `adafd45441644d977b4241eb51172289aea03b81`.
- Baseline `origin/xenia`: `9d26ea4a30130c5594db7ac3b32a60703aad61e9`.
- Initial branch relationship: local `xenia` was 10 commits ahead of
  `origin/xenia`.

## Initial working-tree state

Before B13 implementation, the only working-tree change was the uploaded B13
specification under `attached_assets/`. Prior B04–B12 source changes and
analysis reports were preserved. No B13 implementation or report existed.

## B12 architecture reviewed

B13 consumes `IIntakeReviewService.GetEffectiveAsync`, the existing canonical
B12 reviewed projection boundary. B13 does not load `IntakeReview` or rebuild
corrections, additions, rejected facts, matches, duplicate decisions, or
classification overrides independently.

## Implemented design

### Approved snapshot contract

- Added the product-neutral `LIEN_INTAKE_APPROVED_SNAPSHOT_V1` schema definition,
  version 1, active and system-defined.
- Added strongly typed contracts for classification, effective facts, entity
  decisions, duplicate decisions, evidence/documents, review metadata, and full
  upstream provenance.
- Snapshot payloads use deterministic property ordering, stable collection
  ordering, compact canonical JSON, and uppercase hexadecimal SHA-256 hashes.
- Snapshot creation is eligible only for a current, completed B12 review whose
  outcome is `APPROVED` or `APPROVED_WITH_CORRECTIONS`. Rejected, duplicate
  confirmed, return-for-reprocessing, stale, unresolved-finding, and invalid
  active-fact states are refused.
- Snapshot rows are tenant-scoped, immutable from the application boundary,
  execution-key idempotent, versioned per artifact, and maintain a single
  current marker with supersession history.

### Adapter boundary

- Adapters receive only `ApprovedIntakeSnapshotV1` plus a request context.
- A registry exposes adapter capabilities without exposing downstream product
  services or B12 entities.
- `NOOP_V1` is the only registered adapter. It validates the approved schema,
  creates no product records, returns no product references, and supports dry
  run and retry.
- Adapter executions are tenant-scoped, idempotent, claimable with optimistic
  attempt state, bounded by a configurable timeout, finalized outside the
  claim transaction, and retain attempt history and safe external references.
- Adapter claims carry an attempt-specific claim token; stale workers cannot
  finalize a later recovered attempt. Retry limits are configurable through
  `Intake:Adapters:MaxAttempts`, and post-claim validation/cancellation
  finalization uses a non-cancelled persistence token.
- Retryable, permanent, cancellation, timeout, and stale-processing recovery
  states are represented explicitly.
- Identifier-only snapshot and adapter audit events use the existing audit
  client boundary. Payload JSON and PHI/PII are never sent to the audit sink.

### API and persistence

- Added snapshot creation, current/history/read APIs.
- Added adapter capability, execution, retry, read, and history APIs.
- Added dedicated snapshot and adapter authorization policies.
- Added EF configurations, composite tenant-aware relationships/indexes,
  current-marker uniqueness, adapter attempt/reference tables, and migrations:
  `20260814070953_AddApprovedSnapshotAdapterFrameworkV1` and
  `20260814071043_SeedApprovedSnapshotSchemaV1`, plus
  `20260814071632_AddAdapterClaimTokenV1`.
- No SynqLien, SynqFund, CareConnect, Flow, notification, document-association,
  AWS, or other product adapter was added.

## Tests and validation

- `dotnet build apps/services/intake/Intake.Api/Intake.Api.csproj --no-restore`
  passed with 0 errors.
- Focused B13 tests passed: 3/3. They cover deterministic serialization and
  hashing, `NOOP_V1` validation/execution, no external references, and stable
  schema/adapter codes.
- Existing Intake test suite passed previously at 85/85 before the B13 tests
  were added. A later full-suite process hit an environment-level CoreCLR
  startup OOM while the multi-service workflow was under memory pressure; this
  was not an assertion failure. After the final safety changes, the Intake
  test project compiled successfully but both full and no-build focused test
  hosts were blocked by the same environment OOM, so the post-fix test rerun is
  unverified.
- `git diff --check` passed.
- EF design-time model creation and migration generation passed. Local
  disposable MySQL application was not available in this environment, so SQL
  migration execution remains unverified locally.
- The configured `Start application` workflow was restarted after implementation
  and is running. Its visible warnings are pre-existing multi-service health
  probe failures and package vulnerability advisories; no Intake B13 startup
  exception was observed.

## Scope boundary

B13 ends at the immutable approved snapshot and product-neutral adapter
execution boundary. No B14 work or SynqLien/domain-record creation is included.