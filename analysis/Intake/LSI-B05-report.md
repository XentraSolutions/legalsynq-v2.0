# LSI-B05 — Email Artifact & Documents Integration

## 1. Ticket and objective

This report covers only LSI-B05. The objective is to process already captured
B04 `InboundEmail` records into Intake-owned artifact provenance records and
managed Documents Service references, without interpreting business meaning.

Implementation is complete locally; deployment, cloud changes, commits, pushes,
and pull requests were not performed.

## 2. Repository baseline

- Repository: `XentraSolutions/legalsynq-v3.0`
- Branch: `xenia`
- Baseline `HEAD`: `dac63fb0d19e2ce0672bf2e1df89153561b64458`
- Baseline `origin/xenia`: `9d26ea4a30130c5594db7ac3b32a60703aad61e9`
- Relationship: `origin/xenia` is an ancestor of `HEAD`; the branch is ahead.
- Initial working tree: clean except for the newly uploaded, untracked B05
  specification asset under `attached_assets/`. It will not be modified or
  committed.
- No tracked Intake `bin/` or `obj/` artifacts were found.
- B04 report and prior analysis reports are preserved read-only.

## 3. Baseline architecture review

### B04

B04 provides the authoritative tenant-bound `InboundEmail`, attachment
metadata, persisted text/HTML/raw message content, B04 source provenance, and
the separate email processing-status field. B05 must reuse that boundary and
must not infer or override tenant ownership.

### B03 and B02

B03 `TenantIntakeSource` supplies the source and tenant relationship. B02
`IIntakeConfigurationService.ResolveAsync` returns the effective
`LienIntakeV1Configuration`, including `ProcessAttachments`,
`ProcessEmailBody`, and `AllowUnsupportedDocuments`.

### Documents Service

The existing Documents Service accepts authenticated multipart uploads at
`POST /documents/`. Its request requires tenant ID, product ID, reference ID,
reference type, document type ID, title, optional description, and a file.
The response is `201 Created` with `{ data: DocumentResponse }`, including the
Documents document ID and current version ID. Documents authenticates standard
user JWTs and signed platform service JWTs, derives effective tenant scope from
the JWT, validates MIME/file signatures, stores files through its own storage
provider, queues malware scanning, and audits document creation.

No reusable shared Documents upload client or contract was found in the
repository. Existing service-specific clients use named `HttpClient`s and
signed service tokens. B05 adds the smallest Intake-owned adapter around the
existing multipart Documents endpoint, without direct Documents database or
storage access.

## 4. Required implementation evidence

## 5. Implemented design

### Intake-owned persistence

`IntakeArtifact` is an Intake-owned entity linked to the captured email, its
tenant/source, and (when applicable) the B04 attachment metadata row. It
stores only bounded metadata, SHA-256, deterministic source keys, lifecycle
state, retryability, attempt count, and Documents document/version/reference
identifiers. Artifact content is never persisted in the Intake database.

The email's existing B04 `ProcessingStatus` is used as the email-level
artifact-processing state, with B05 values `NOT_STARTED`, `IN_PROGRESS`,
`COMPLETED`, `PARTIAL`, and `FAILED`. This is separate from B04
`CaptureStatus`; no capture state is overwritten.

Migration added:

- `20260814023351_AddIntakeArtifacts`

The migration creates `IntakeArtifacts`, unique per-email artifact keys,
tenant/status/ordering indexes, and restricted foreign keys to the captured
email, B04 attachment metadata, and registered Intake source.

### MIME extraction and safety

The implementation uses the existing mature `MimeKit` parser. It walks nested
MIME multipart entities without following external references and extracts
regular and inline attachment parts in source order. Text and HTML body
artifacts use the already captured B04 body fields, avoiding duplicate body
extraction.

Bounds are enforced before an extracted part is retained:

- raw MIME input: 16 MiB by default, below the B04 25 MiB capture ceiling;
- individual artifact: 25 MiB;
- total decoded artifacts: 100 MiB;
- artifact count: 100;
- MIME nesting depth: 20;
- effective filename: 240 characters.

Filenames have path separators, traversal markers, and control characters
removed/replaced and receive a deterministic ordinal prefix. Artifact keys use
stable B04 metadata IDs when available, with source ordinals only for MIME
parts that cannot be correlated to metadata. Failure messages and logs do not
contain body text, raw MIME, binary content, credentials, or tokens.

### Correlation and lifecycle

Attachment correlation prefers normalized content ID, then SHA-256, then
filename plus size, and uses ordinal only as a validated fallback. Extracted
content is hashed and sized before upload. Hash/size mismatches, missing MIME
parts, missing metadata, parse failures, and safety-limit violations produce
stable failure codes and no Documents upload.

Processing is idempotent at the Intake boundary through deterministic keys and
an atomic database claim. Completed artifacts are reused; retryable Documents
failures may be retried; unsupported artifacts are `SKIPPED` when
`AllowUnsupportedDocuments` is false. Mixed outcomes produce email-level
`PARTIAL`; all-failure and no-failure outcomes produce `FAILED` and
`COMPLETED`, respectively.

For ambiguous retry outcomes, each artifact uses its own deterministic
Documents `referenceId` and `referenceType`. Retries first query the Documents
Service for that reference and persist an already-created document instead of
blindly creating another one. Intake does not claim exactly-once semantics
when the upstream lookup itself is unavailable; it records a retryable
integration failure instead.

### B02 configuration and Documents boundary

Processing resolves the effective B02 `LienIntakeV1Configuration` for the
captured email and honors `ProcessAttachments`, `ProcessEmailBody`, and
`AllowUnsupportedDocuments` on every process/retry operation. The Documents
adapter sends tenant ID, product, reference metadata, configured document type,
safe title/description metadata, and multipart bytes using a signed service JWT
with audience `documents-service`. It stores returned document ID, current
version ID, and a safe local reference only.

HTML body content is represented as an `EMAIL_BODY_HTML` artifact while being
sent to the existing Documents allow-list as `text/plain`; the original
declared content type remains in Intake metadata. No Documents Service schema,
database, or storage code was changed.

### APIs and operations

Tenant-scoped endpoints were added:

- `GET /emails/{emailId}/artifacts`
- `GET /emails/{emailId}/artifacts/reconcile`
- `GET /emails/artifacts/analytics` (optional `emailId` filter)
- `POST /emails/{emailId}/artifacts/process`
- `POST /emails/{emailId}/artifacts/{artifactId}/retry`

Artifact reads use the existing email-read permission or dedicated artifact
read permission. Processing and retry require dedicated artifact-management
permission. All operations derive tenant scope from the authenticated request
context and query the tenant in persistence predicates.

Reconciliation reports metadata/artifact counts and warnings without exposing
content. Analytics performs tenant-filtered server-side aggregation of status
counts and total/uploaded bytes.

## 6. Validation

Passed:

- Intake API build: zero errors.
- Full `LegalSynq.sln` build: zero errors.
- Intake test suite: 28/28 passed.
- `dotnet ef migrations list`: all five Intake migrations detected,
  including `AddIntakeArtifacts`.
- `git diff --check`.
- Focused MIME tests for regular/inline extraction, input bounds, metadata
  verification, and Documents-reference persistence.

The configured full-stack development workflow was restarted successfully after
the implementation. Intake reached `GET /health` with HTTP 200 after startup.
Its existing web preview still reports unrelated missing frontend modules
(`msw`, `@tanstack/react-query`, and `sonner`); these are outside LSI-B05 and
do not affect the focused Intake build/tests.

Disposable MySQL application of the new migration remains unverified because
the earlier container-runtime validation was blocked by the OCI `setns`
runtime error before startup. EF model generation and migration discovery
were completed locally; no production database was changed.

## 7. Scope boundary

Not implemented: AI/OCR/matching/review, Case/Lien integration, Flow,
notifications, mailbox polling, archive expansion, custom malware scanning,
cloud/deployment changes, Documents Service schema changes, LSI-B06, commits,
pushes, and PR creation.