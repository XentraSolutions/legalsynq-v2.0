# LSI-B12 — Intake Center / Human Review

## Ticket and objective

- Ticket: LSI-B12 — Intake Center / Human Review for Synq Intake.
- Objective: add a tenant-scoped human adjudication layer over immutable B07-B11
  classification, extraction, normalization, matching, duplicate, and policy outputs.
- Service root: `apps/services/intake`.
- Frontend scope: the existing tenant web application under `apps/web`.
- B13 approved-snapshot persistence and all downstream business writes remain out of scope.

## Repository baseline

- Repository: `XentraSolutions/legalsynq-v3.0`.
- Branch: `xenia`.
- Baseline HEAD: `c683ad0cc2f591e0e2be6a403ea5a63030f1216a`.
- `origin/xenia`: `9d26ea4a30130c5594db7ac3b32a60703aad61e9`.
- Initial branch relationship: local `xenia` was 9 commits ahead of `origin/xenia`.

## Initial working-tree state

The following state existed before B12 implementation and is preserved:

- `analysis/LSI-B11-report.md` appeared deleted.
- `analysis/Intake/LSI-B11-report.md` was untracked.
- The uploaded B12 specification was untracked.

No B12 implementation files or B12 report existed at baseline. Prior B04-B11
reports and implementation changes are not being rewritten. No cloud, deployment,
push, merge, or B13 work will be performed.

## Architecture reviewed before implementation

The B04-B11 Intake source/artifact, manual intake, classification, extraction,
normalization, matching, duplicate, and policy boundaries were inspected, along
with the current request tenant/user context, Intake authorization policies,
audit-client patterns, Documents Service client, EF model/migrations, and tenant
web routing/data-fetching conventions. B12 will add Intake-owned review data and
service boundaries while leaving B07-B11 records immutable.

## Implementation inventory

### Domain and application

- Added the `Intake.Domain.Review` aggregate and append-only history entities:
  `IntakeReview`, corrections, entity-match decisions, duplicate decisions,
  policy-finding decisions, and review activities.
- Added stable status, outcome, priority, decision, activity, and error-code
  constants in `ReviewCodes.cs`.
- Added tenant-scoped review repository and service boundaries under
  `Intake.Application/Review`.
- The service binds every review to the exact B07-B11 classification,
  extraction, normalization, matching, and policy-evaluation IDs.
- Human actions increment `Version` and append history; they do not mutate
  upstream B07-B11 records.
- Creation is idempotent for an active artifact/policy lineage. A newer
  lineage makes prior work stale and allows the next review revision to be
  created. Terminal reviews are immutable.
- Candidate, duplicate, and policy decisions are checked against the
  tenant-scoped current B10/B11 lineage. Blocking policy findings cannot be
  overridden.
- Corrections use the existing B09 deterministic fact-normalizer registry.
- The service exposes a deterministic effective reviewed projection for future
  B13 consumers.

### Persistence

- Added review DbSets and EF configuration to `IntakeDbContext`.
- Added migration
  `20260814063739_AddIntakeHumanReviewV1` /
  `AddIntakeHumanReviewV1`.
- The schema contains `IntakeReviews` plus five append-only history tables:
  `IntakeReviewCorrections`, `IntakeReviewMatchDecisions`,
  `IntakeReviewDuplicateDecisions`, `IntakeReviewFindingDecisions`, and
  `IntakeReviewActivities`.
- Review rows have tenant-aware composite relationships to artifacts and
  upstream lineage records, optimistic concurrency through `Version`, review
  status/priority indexes, and a filtered-by-status active-context uniqueness
  strategy.
- `ReviewAuditSink` follows the existing persist-first, non-blocking audit
  delivery pattern.

### API and authorization

Added `intake.review.read`, `intake.review.manage`,
`intake.review.assign`, and `intake.review.complete` policies and mapped:

- `GET /reviews`
- `GET /reviews/summary`
- `GET /reviews/{reviewId}`
- `GET /reviews/{reviewId}/effective`
- `POST /reviews`
- `POST /reviews/{reviewId}/claim`
- `POST /reviews/{reviewId}/unassign`
- `POST /reviews/{reviewId}/corrections`
- `POST /reviews/{reviewId}/matches/{entityType}/decision`
- `POST /reviews/{reviewId}/duplicates/{signalId}/decision`
- `POST /reviews/{reviewId}/findings/{findingId}/decision`
- `PUT /reviews/{reviewId}/assignment`
- `POST /reviews/{reviewId}/complete`

All service calls require the current tenant context. Assignment currently
accepts the requested user ID through the authorized endpoint; explicit
Identity Service tenant-user validation remains a known boundary limitation.

### Frontend

- Added typed B12 API methods to `apps/web/src/lib/intake-api.ts`.
- Added `/intake` queue route with summary cards, bounded pagination, filters
  for status, priority, disposition, source, and unassigned work, plus stale
  indicators.
- Added `/intake/{reviewId}` workspace with source/document context,
  classification and facts, correction entry, candidate-match decisions,
  duplicate decisions, policy-finding decisions, assignment/claim controls,
  completion controls, immutable/stale banners, and optimistic-concurrency
  error handling.
- Added the Intake Center navigation entry with permission-aware controls.

## Validation

Completed:

- `dotnet build apps/services/intake/Intake.Api/Intake.Api.csproj --no-restore`
  — passed; only existing dependency vulnerability/package warnings remain.
- `dotnet test apps/services/intake/Intake.Tests/Intake.Tests.csproj
  --no-restore` — **85 passed**.
- EF migration listing — the complete chain through
  `AddIntakeHumanReviewV1` is discoverable.
- `git diff --check` — passed.
- Frontend type-check confirmed no errors in the new B12 files. The repository
  type-check still reports pre-existing unrelated errors in lien/selling
  components (not introduced by B12).
- The configured `Start application` workflow is running. The local preview
  redirects unauthenticated requests to `/login`, so an authenticated review
  queue/workspace could not be exercised through the browser in this session.

## Known limitations

1. Assignment accepts a user ID supplied by an authorized caller but does not
   yet validate that user against a dedicated Identity Service tenant-user
   lookup boundary.
2. Applying the migration to MySQL could not be completed locally because the
   configured development database was unavailable; generation and migration
   chain discovery succeeded.
3. No B13 approved-snapshot table, downstream write, deployment, push, commit,
   or merge was performed.
4. The full web type-check is currently blocked by unrelated existing
   `TableFeatures` and component prop errors outside the B12 routes.

## Final status

LSI-B12 is implemented through the Intake backend, persistence/migration,
authorization/API, tenant web queue, and review workspace. The implementation
is locally buildable and the Intake test suite passes. The two environment
boundaries above—development MySQL availability and explicit Identity Service
assignment validation—remain documented rather than silently bypassed.