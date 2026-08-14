# LSI-B11 — Confidence & Policy Engine

## Baseline captured before implementation

- Ticket: LSI-B11 — Confidence & Policy Engine for Synq Intake.
- Repository: `XentraSolutions/legalsynq-v3.0`.
- Service root: `apps/services/intake`.
- Branch: `xenia`.
- Baseline HEAD: `c4812b3d8abced3e62bdafa4ccfd327f40ab665c`.
- `origin/xenia`: `9d26ea4a30130c5594db7ac3b32a60703aad61e9`.
- Initial branch relationship: local `xenia` was 8 commits ahead of `origin/xenia`.

## Initial working-tree state

Pre-existing changes were preserved:

- `analysis/LSI-B10-report.md` appeared deleted.
- `analysis/Intake/LSI-B10-report.md` was untracked.
- The uploaded B11 specification was untracked.

No prior analysis report is being modified. The uploaded B11 specification will remain
untracked. No cloud, deployment, push, merge, or B12 work will be performed.

## Objective

Implement a deterministic, tenant-scoped policy evaluation layer that consumes the
persisted B07 classification, B08 extraction, B09 normalization, and B10 matching
results, produces an explainable disposition/confidence/review-priority recommendation,
and preserves immutable evaluation history without downstream business writes.

## Implementation inventory

### Domain and contracts

- Added versioned `LIEN_INTAKE_POLICY_V1` policy profile definitions and seed metadata.
- Added tenant/artifact policy evaluation aggregates and immutable explainability findings.
- Added status, disposition, review-priority, confidence, failure, reason, upstream-stage,
  category, severity, outcome, and rule-code constants.
- Added API contracts for profiles, current evaluations, history, findings, and evaluation
  requests.

### Deterministic evaluation

- Added a policy context that loads the current persisted lineage in the explicit order:
  classification, extraction, normalization, then matching.
- Added deterministic rule registry and rules for classification, required facts, confidence,
  structural validity, evidence, patient/provider/case matches, ambiguity, hard identifiers,
  configured hard conflicts, duplicates, and normalization warnings.
- Findings retain rule code, category, severity, outcome, reason, entity/fact references,
  scores, thresholds, and evidence references where applicable.
- Disposition precedence is conservative: blocked/incomplete upstream data, exact duplicate,
  hard conflict, required no-match, review/low-confidence findings, and finally
  auto-acceptable only when both tenant flags and the policy profile permit it.
- Duplicate policies now honor profile enablement, severity, and disposition, including exact
  duplicates; tenant flags do not override an explicitly less-severe profile disposition.
- Required upstream stages are profile-controlled. Ordinary fuzzy/name conflicts are not
  escalated as hard conflicts unless a configured hard reason or hard identifier conflict is
  present.
- Evaluations are deterministic and idempotent by tenant-scoped execution key and preserve
  historical evaluations separately from the one current result.
- Cancellation finalizes a claimed evaluation as failed with
  `POLICY_EXECUTION_CANCELLED` using a non-cancelled cleanup save, allowing retry.

### Configuration and persistence

- Extended `LienIntakeV1Configuration` with B11 enablement, profile selection, thresholds,
  margins, required-match flags, evidence/conflict/duplicate controls, and explicit
  auto-acceptable controls.
- Added profile validation and guardrails for policy codes, threshold ranges, and margins.
- Added EF mappings, indexes, tenant-aware composite relationships, current-result
  uniqueness, and seeded policy JSON.
- Added `20260814055950_AddConfidencePolicyEngineV1` and updated the EF model snapshot.
- Added a tenant-scoped audit sink that sends minimized policy metadata through the existing
  audit client; no finding reason text or PHI/PII is emitted as an audit payload.

### API surface

- `GET /policy/profiles`
- `GET /artifacts/{artifactId}/policy`
- `GET /artifacts/{artifactId}/policy/history`
- `POST /artifacts/{artifactId}/policy/evaluate`

No B12 reviewer UI/actions, downstream Case/Lien writes, Flow tasks, routing, or
notifications were added.

## Validation

| Check | Result |
| --- | --- |
| `dotnet build apps/services/intake/Intake.Api/Intake.Api.csproj --no-restore` | Passed |
| Focused policy tests | **18 passed** |
| Full Intake test project | **82 passed** |
| `git diff --check` | Passed |
| EF migration generation/listing | Succeeded previously; model unchanged by final rule/test fixes |
| Architecture follow-up review | Cancellation and profile precedence fixes reviewed; no security issue reported |
| Start application workflow | Restarted successfully; workflow remains running |

The test commands emitted existing package advisories for `Microsoft.Extensions.Caching.Memory`
8.0.0 and `MimeKit` 4.9.0, plus the existing `NU1510` package-reference warning. These are
not introduced by B11.

## Local environment limitations

- The generated migration was not applied to a local MySQL database because no usable local
  Intake database connection was available during validation. The migration was generated,
  listed, and the API project built successfully; applying it remains an environment/database
  operation.
- The aggregate `Start application` workflow starts many services. Its logs contain
  pre-existing startup warnings and transient monitoring failures for services that are still
  coming up; no B11-specific startup exception was observed.
- No deployment, push, commit, cloud migration, or production validation was performed.

## Scope and preserved working-tree state

- B11 changes are limited to the Intake service, its tests, migration/model files, and this
  report.
- The pre-existing deleted `analysis/LSI-B10-report.md`, untracked
  `analysis/Intake/LSI-B10-report.md`, and uploaded B11 specification remain preserved.
- B12 and unrelated business services were not started.