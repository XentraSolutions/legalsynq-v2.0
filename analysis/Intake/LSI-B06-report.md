# LSI-B06 — Manual Intake & Source Management

## 1. Ticket and objective

Implement the B06 manual intake path and expose the existing B03 Intake source
management capability, while preserving the B04 email path and converging
manual files on the B05 `IntakeArtifact` → Documents Service pipeline.

The implementation is repository/local only. AI/OCR, matching/review, Case,
Lien, Flow, notifications, mailbox polling, external ingestion, cloud work,
deployment, and B07 remain out of scope.

## 2. Repository baseline

Baseline inspection was performed before implementation.

- Repository root: `/home/runner/workspace`
- Synq Intake root: `apps/services/intake/**`
- Stack: .NET 10, ASP.NET Core Minimal APIs, EF Core, MySQL/Pomelo,
  JWT/service tokens, `ICurrentRequestContext`, `LegalSynq.AuditClient`,
  React/Next.js/Tailwind frontend.
- B02 tenant configuration and processing-profile services already provide
  tenant enablement, active global definitions, tenant assignments, defaults,
  versioning, and effective-profile resolution.
- B03 already provides tenant-scoped source CRUD, validation, status changes,
  default-source rules, connector registry, provider capabilities, and safe
  credential-reference handling.
- B04 already provides tenant-scoped `InboundEmail` persistence, attachment
  metadata, raw MIME/body capture, and email processing status.
- B05 already provides Intake-owned `IntakeArtifact`, MIME safety/extraction,
  artifact lifecycle/retry/reconciliation/analytics, and the signed
  Documents Service adapter.
- Intake tests are currently focused application/unit tests; no existing
  Intake `WebApplicationFactory` HTTP integration-test foundation was found.
- The tenant frontend uses the shared `(platform)` layout/AppShell,
  `requireOrg`/`requireTenantAdmin` guards, relative `/api` calls through the
  shared client, Tailwind utility styling, and existing accessible form/table
  patterns.

## 3. Baseline HEAD, origin, and working tree

- Branch: `xenia`
- Baseline HEAD: `febefa105124aba075bf71235d670352a075f893`
- Baseline `origin/xenia`: `9d26ea4a30130c5594db7ac3b32a60703aad61e9`
- Initial relationship: local branch was ahead of `origin/xenia` by 40 commits
  and behind by 0 commits.
- Initial working tree: clean except for the uploaded, intentionally
  uncommitted B06 specification asset:
  `attached_assets/Pasted-You-are-implementing-LSI-B06-Manual-Intake-Source-Manag_1786675787601.txt`

Prior reports, including `analysis/LSI-B04-report.md` and
`analysis/LSI-B05-report.md`, are preserved read-only.

## 4. Architecture reviewed before implementation

Reviewed B02 configuration/profile resolution and version rules, B03 source
registries/repositories/endpoints/service, B04 inbound-email persistence and
status flow, B05 artifact model/processing/Documents adapter, Intake EF
context/configurations/migrations, API authorization and exception middleware,
the tenant AppShell and platform pages, the frontend API client/form patterns,
and the existing Intake test project.

## 5. Implementation

This section is completed after the B06 implementation and validation.

### 5.1 Manual submission aggregate and lifecycle

Added `ManualIntakeSubmission` as an Intake-owned aggregate with:

- tenant and optional organisation ownership;
- optional tenant source reference;
- purpose, processing-profile code, title, external reference, and operator notes;
- caller-supplied idempotency key;
- submitted-by, timestamps, failure information, and optimistic `Version`;
- explicit lifecycle statuses: `PROCESSING`, `COMPLETED`, `PARTIAL`,
  `FAILED`, and `CANCELLED`.

The create endpoint uses an atomic create-and-submit orchestration rather than a
durable draft/upload session. Every uploaded file becomes an Intake-owned
`IntakeArtifact` with `ManualIntakeSubmissionId`, `ArtifactSourceType=MANUAL`,
deterministic tenant/submission/ordinal storage identity, safe filename
metadata, SHA-256, and per-artifact processing state.

### 5.2 Artifact processing and Documents convergence

Manual files use the existing signed Documents client and artifact repository
boundary. The service:

1. validates the active tenant profile and request fields;
2. enforces configured file count, per-file, aggregate, filename, and content
   bounds before copying multipart content;
3. creates provenance-bearing artifacts;
4. uploads valid files to Documents;
5. records the returned document/version references;
6. reconciles existing Documents records on uncertain upload outcomes; and
7. derives aggregate status from the artifact results.

Partial success is represented explicitly; one failed file does not hide
successful files. Manual retry requires exactly one replacement multipart file
and verifies that its SHA-256 matches the original artifact before uploading.
Unsupported/non-retryable artifacts remain visible with a failure code and
message.

### 5.3 Source management

The B03 source registry now advertises `MANUAL` as a supported source type.
Manual source creation does not require a mailbox address or connector
configuration; it receives an internal-safe synthetic address only for the
existing source schema. Connector testing returns `NOT_APPLICABLE` for manual
sources. Existing email/provider validation behavior remains unchanged.

The tenant UI exposes the existing source list, email-source registration,
validation, enable/disable controls, supported purpose/type data, and an
explicit link back to the manual submission surface.

### 5.4 API surface

All routes are mapped by the Intake service and therefore appear behind the
gateway at `/api/intake`:

| Method | Route | Authorization | Purpose |
| --- | --- | --- | --- |
| `POST` | `/manual-intake` | `Intake.Manual.Manage` | Create and process multipart submission |
| `GET` | `/manual-intake` | `Intake.Manual.Read` | List tenant submissions, optionally filtered |
| `GET` | `/manual-intake/{submissionId}` | `Intake.Manual.Read` | Get submission and artifacts |
| `GET` | `/manual-intake/analytics` | `Intake.Manual.Read` | Tenant-scoped aggregate analytics |
| `POST` | `/manual-intake/{submissionId}/artifacts/{artifactId}/retry` | `Intake.Manual.Manage` | Retry one artifact with replacement file |
| `POST` | `/manual-intake/{submissionId}/cancel` | `Intake.Manual.Manage` | Cancel with expected aggregate version |
| `GET/POST/PUT/PATCH` | `/sources...` | Existing B03 source policies | Reused source CRUD/status/validation surface |

The multipart create and retry routes accept `clientRequestId` as a form field
or `Idempotency-Key` header. Idempotency is unique within a tenant, and all
submission, artifact, source, and analytics queries require the current
tenant context.

### 5.5 Schema and migration

Added `ManualIntakeSubmissions` with tenant/idempotency, created-at,
purpose/created-at, status/updated-at, and source indexes. `Version` is mapped
as an EF concurrency token. Added nullable `ManualIntakeSubmissionId` and
`ArtifactSourceType` to `IntakeArtifacts`, nullable source linkage, manual
indexes, a manual-artifact foreign key, and the
`CK_IntakeArtifacts_ExactlyOneParent` check constraint:

```sql
((InboundEmailId IS NOT NULL AND ManualIntakeSubmissionId IS NULL)
 OR
 (InboundEmailId IS NULL AND ManualIntakeSubmissionId IS NOT NULL))
```

The generated migration is
`20260814025609_AddManualIntake`. Its existing-artifact default is explicitly
`EMAIL` so B05 rows retain their original provenance during upgrade. The
migration chain lists cleanly through the B06 migration. A disposable MySQL
database was not available in this run, so applying the migration and executing
the check constraint against MySQL remain unverified locally.

### 5.6 Tenant frontend

Added the tenant pages:

- `/lien/intake/manual` — multipart submission form plus recent
  tenant-scoped history;
- `/lien/intake/manual/{id}` — aggregate details, artifact states, retry file
  selection, and optimistic-concurrency cancel;
- `/lien/intake/sources` — source list and existing B03 email-source controls.

Added `apps/web/src/lib/intake-api.ts` with typed helpers using the shared
`apiClient`/`postForm` conventions. Navigation links are available under
SynqLien settings for tenant administrators. Manual uploads do not expose raw
file content after submission; the detail page displays metadata, status,
failure messages, and Documents references only when supplied by the service.

## 6. Security and operational controls

- All manual and source routes use Intake authorization policies.
- Tenant context is mandatory; repository queries include tenant predicates.
- Idempotency lookup and uniqueness are tenant-scoped.
- Submission cancellation and artifact retry require the tenant-owned
  aggregate/artifact and expected version where applicable.
- Original filenames are sanitized and path separators/control characters are
  removed before storage-key construction.
- File count, per-file, total-byte, filename, and content-type limits are
  enforced before processing.
- SHA-256 is computed from the received bytes and is used for auditability and
  manual retry integrity.
- Audit events contain tenant-safe metadata and do not include file contents,
  credentials, or raw multipart payloads.
- Existing email artifacts are explicitly marked `EMAIL`; manual artifacts
  are explicitly marked `MANUAL`; the database check constraint prevents
  ambiguous parent ownership.
- No AI/OCR, matching, review, downstream routing, mailbox polling, cloud
  deployment, or production database operation was added.

## 7. Tests and validation

### Passed

- `dotnet test apps/services/intake/Intake.Tests/Intake.Tests.csproj --no-restore -m:1 /p:BuildInParallel=false`
  — **33 passed, 0 failed**.
- `dotnet build apps/services/intake/Intake.Api/Intake.Api.csproj --no-restore -m:1 /p:BuildInParallel=false`
  — **0 errors**.
- `dotnet build LegalSynq.sln --no-restore -m:1 /p:BuildInParallel=false`
  — **0 errors**; existing solution/MSBuild and package vulnerability warnings
  remain.
- EF migration listing — B06 migration is present after the five prior Intake
  migrations; database application status could not be read because no
  disposable MySQL connection was available.
- `git diff --check` — passed.
- `pnpm type-check` was rerun after restoring the workspace's missing frontend
  dependencies. No diagnostics reference the B06 files. The command still
  reports **119 pre-existing diagnostics** in unrelated selling/table code.
- The configured `Start application` workflow was restarted successfully.
  Proxy health returned `200 {"status":"ok","service":"proxy"}` and the
  protected manual page correctly redirected unauthenticated requests to
  `/login?reason=unauthenticated`.
- An authenticated HTTP/API integration test foundation does not exist in the
  repository, so endpoint-level B06 tests were not added. Focused service tests
  cover the create/upload, failure/retry, tenant-idempotency, hash mismatch,
  and manual source behavior.

### Warnings and limitations

- The normal frontend type-check is not green because of unrelated existing
  diagnostics; B06 files are clean within that run.
- Browser preview was verified through the auth boundary rather than an
  authenticated tenant session. The preview screenshot shows the existing
  LegalSynq sign-in page.
- The Intake API process was not independently reachable on port `5013` after
  the main workflow restart because the development startup wave exited before
  launching all .NET services. This is a workflow/environment limitation, not
  a failed Intake build or focused test.
- Known NuGet audit warnings for existing `Microsoft.Extensions.Caching.Memory`
  and `MimeKit` packages remain outside B06 scope.

## 8. Acceptance summary

B06 manual create-and-submit, provenance, bounded multipart handling,
Documents convergence, artifact-level status/retry, cancellation,
tenant-scoped idempotency/concurrency, source-type registration, source
management UI, tenant navigation, audit integration, migration metadata, and
focused automated coverage are implemented. Production migration, deployment,
authenticated browser execution, and external mailbox/cloud behavior remain
intentionally out of scope.
