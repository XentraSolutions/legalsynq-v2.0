# LSI-B04 — Complete Email Capture & Analytics Repository

Date: 2026-08-14  
Repository: `XentraSolutions/legalsynq-v3.0`  
Branch: `xenia`

## 1. Execution boundary and baseline

This report covers only LSI-B04. No B05 work, deployment, push, commit, pull
request, AWS/RDS/ECS/Route53 work, production change, or shared/cloud database
change was performed.

Baseline recorded before B04 implementation:

- branch: `xenia`
- `HEAD`: `877623334ab7218199718754655cbb9df6a9133a`
- `origin/xenia`: `9d26ea4a30130c5594db7ac3b32a60703aad61e9`
- B03 was present and its report was left read-only.
- Initial working tree contained the unrelated generated
  `apps/control-center/next-env.d.ts` change and the uploaded specification
  under `attached_assets/`. Both were preserved and are excluded from the B04
  implementation change set.

## 2. Scope delivered

B04 now provides an Intake-owned `InboundEmail` capture repository with:

- immutable source provenance: tenant, organization, registered source,
  source configuration version, purpose, processing profile, and B02 version
  snapshots;
- provider and RFC identity fields, thread/reply/reference metadata;
- sender, sender-envelope, reply-to, subject, text, HTML, structured
  recipients, deterministic headers, and attachment metadata;
- Intake-local raw MIME/message preservation with SHA-256 and byte size;
- tenant-scoped failed-capture telemetry without message content;
- `CAPTURED` capture state and `NOT_STARTED` processing state;
- configurable message/body/header/recipient/attachment limits;
- transactional capture and tenant-scoped lookup/list/analytics repository
  operations;
- duplicate delivery counters with database-backed idempotency protection;
- tenant-authorized `GET /emails`, `GET /emails/{emailId}`, and
  `GET /emails/analytics` routes.

No Documents, AI, OCR, matching, review, Case/Lien, Flow, notification,
provider polling, mailbox retrieval, or product routing behavior was added.

## 3. Architecture decisions

### Tenant binding and provenance

Capture first loads the requested B03 source in the supplied tenant scope,
requires an active `EMAIL` source, checks the current source configuration
version, re-resolves the registered source address through
`IIntakeSourceResolver`, and requires the resolver result to match the source,
tenant, and version. Provider, purpose, and processing profile are also
required to match the registered source. Sender, subject, body, headers,
attachments, and RFC thread metadata never participate in tenant resolution.

The selected source deactivation race policy is the safer B04 fallback:
because B04 has no trusted delivery-time acceptance token, a currently
inactive source rejects new capture. A later delivery can only be admitted
after a future connector supplies verifiable delivery-time provenance; B04
does not invent that proof.

### Raw source storage

No approved existing generic storage layer was established and new AWS/object
storage is explicitly out of scope. B04 therefore stores raw content in an
Intake `longtext` column with configurable maximum size, UTF-8 byte hash, and
byte count. List and analytics projections do not select raw/body columns.
The detail response exposes bodies and raw integrity metadata, but not raw MIME
content. There is deliberately no `/emails/{id}/raw` endpoint in B04; raw
content remains internal until a separately authorized/audited business need
exists.

Stored HTML is original untrusted source data. It must be sanitized or
isolated before downstream browser rendering; B04 does not rewrite it or treat
it as safe markup.

### Idempotency and concurrency

`IdempotencyKey` is non-null so MySQL nullable-composite uniqueness semantics
cannot create a duplicate gap. It is derived from:

- source + provider + provider message identity when available;
- source + provider + Internet-Message-ID when provider identity is absent.

The identity material is SHA-256 encoded into a compact key. A unique
`utf8mb4` index protects the database boundary. Capture performs an
application duplicate check, then a transactional insert; a duplicate-key
race reloads the canonical record, increments `DuplicateCaptureCount`, and
returns it without inserting a second email. Successful commit precedes audit
and logging.

Full provider/RFC identity columns remain persisted for inspection and
tenant-scoped lookup. Direct wide composite indexes were intentionally not
created because 768-character `utf8mb4` identity columns would exceed safe
MySQL index widths; the compact unique idempotency key is the duplicate lookup
index.

### Headers and recipients

Headers are represented as a deterministic JSON array supporting multi-value
headers and stable name ordering. Authorization/token/secret/password/API-key
style header names, including cookie/session credential headers, are not
persisted or logged. Recipients are separate rows with `TO`, `CC`, and `BCC`,
original display value, B03-compatible normalized address, and ordinal.

## 4. Implementation map

- Domain: `Intake.Domain/Emails/InboundEmail.cs`,
  `InboundEmailRecipient.cs`, `InboundEmailAttachmentMetadata.cs`, and
  `InboundEmailCaptureFailure.cs`
- Contracts: `Intake.Contracts/Emails/`
- Capture application service:
  `Intake.Application/Emails/EmailCaptureService.cs`
- Capture options:
  `Intake.Application/Emails/EmailCaptureOptions.cs`
- Repository contract/query service:
  `Intake.Application/Emails/IInboundEmailRepository.cs`,
  `InboundEmailQueryService.cs`
- EF repository and mappings:
  `Intake.Infrastructure/Persistence/EfInboundEmailRepository.cs` and
  `Persistence/Configurations/`
- Migrations:
  `20260814004756_AddInboundEmailRepository` and
  `20260814010032_AddInboundEmailCaptureFailures`
- API:
  `Intake.Api/Endpoints/InboundEmailEndpoints.cs`
- Authorization:
  `IntakeAuthorizationPolicies.EmailRead` and
  `IntakeAuthorizationPolicies.EmailAnalytics`
- Focused tests:
  `Intake.Tests/EmailCaptureTests.cs`

## 5. API behavior

`GET /emails` derives tenant identity exclusively from
`ICurrentRequestContext`, applies bounded page/pageSize pagination, stable
`ReceivedAt DESC, Id DESC` ordering, and source/provider/purpose/profile/status,
date, attachment, and sender filters. It returns a projection DTO without
raw MIME, text body, HTML body, or attachment binary content.

`GET /emails/{emailId}` requires the read policy and applies tenant scope to
the ID lookup. It returns detail content, structured recipients, attachment
metadata, provenance, and raw hash/size/presence metadata only.

`GET /emails/analytics` requires the separate analytics policy and performs
tenant-scoped SQL aggregation for totals, day/source/provider/purpose/status
counts, attachment presence, average attachment count, duplicate prevention,
 and persisted capture-failure counts. Failure telemetry is recorded only after a
 trusted tenant/source has been resolved; malformed requests without trusted
 source context are rejected without creating tenant-attributable telemetry.

No capture HTTP endpoint is exposed. Connectors are not operational in B04,
and direct application-service invocation avoids an unnecessary internal
HTTP spoofing surface.

## 6. Database and migration

The migration creates only:

- `InboundEmails`
- `InboundEmailRecipients`
- `InboundEmailAttachmentMetadata`
- `InboundEmailCaptureFailures`

It adds:

- restricted `InboundEmails.TenantIntakeSourceId` foreign key;
- cascading recipient/attachment child foreign keys;
- unique `IX_InboundEmails_IdempotencyKey`;
- tenant/date, tenant/source/date, tenant/provider/date,
  tenant/status/date, and tenant/purpose/date indexes;
- recipient and attachment FK/ordering indexes;
- failure telemetry tenant/time, tenant/source/time, and tenant/code/time indexes;
- `utf8mb4` text columns and `utf8mb4_bin` normalized-recipient values.

B02/B03 tables and migrations remain present and unchanged. Source deletion is
restricted by the historical email FK, so deactivation cannot remove captured
history.

## 7. Validation performed

### Build and tests

- `dotnet build apps/services/intake/Intake.Api/Intake.Api.csproj --no-restore -m:1`
  — passed, 0 errors; existing package warnings only.
- `dotnet test apps/services/intake/Intake.Tests/Intake.Tests.csproj --no-restore -m:1`
  — passed, 24 tests, 0 failures.
- `git diff --check` — passed.

The focused B04 tests cover valid capture, provenance, tenant binding,
Unicode and recipient normalization, TO/CC/BCC records, bodies and raw
metadata, deterministic header redaction, attachment metadata, duplicate
canonicalization/audit, inactive-source rejection, missing identity, and
size limits.

### EF and MySQL

The repository-pinned `dotnet-ef` 8.0.0 tool was restored and used. The
original B04 migration set was applied to disposable MySQL 8.0.46 using a
local container only. `__EFMigrationsHistory` contained:

- `20260813232726_InitialIntakeConfiguration`
- `20260814000306_AddTenantIntakeSources`
- `20260814004756_AddInboundEmailRepository`

Schema checks confirmed the original three B04 tables, the source FK, child
FKs, unique idempotency index, tenant/date indexes, source/date index, and
`utf8mb4_bin` on `InboundEmailRecipients.NormalizedEmailAddress`.

Direct MySQL checks confirmed:

- Unicode recipient display name and Unicode body persisted;
- trusted-source capture failures persisted without body, raw MIME, or headers
  and included in tenant-scoped analytics;
- duplicate idempotency insertion failed with MySQL error 1062;
- an unknown source FK insertion failed with MySQL error 1452;
- B02/B03 tables remained present.

A temporary, non-repository EF harness against the same disposable database
successfully executed the real Pomelo repository list query and all analytics
aggregations. It confirmed the list returned one projection without raw
content, and analytics returned the expected day, source, provider, purpose,
status, attachment, and duplicate values.

The disposable MySQL container and validation data are not part of the
repository and were removed after validation. After adding the durable failure
telemetry migration, a second container attempt failed before startup with an
OCI runtime `setns` error; no migration command ran in that attempt. EF
migration enumeration confirms the new migration is generated and ordered, but
the new table's live MySQL application/index inspection remains unverified in
this run.

## 8. Security and privacy review

- No secrets, OAuth tokens, mailbox passwords, production connection strings,
  or connector credentials were added.
- No unrestricted capture route exists.
- Tenant reads and analytics are request-context scoped.
- Capture validates source ownership and does not trust sender/content for
  tenancy.
- List/analytics queries never load bodies or raw MIME.
- Audit/log metadata excludes body, HTML, raw MIME, full headers, and
  attachment contents.
- Sensitive header names are excluded from stored header JSON.
- Cookie, Set-Cookie, X-Auth*, X-Session*, and X-Credential* names are excluded
  alongside token/secret/password/API-key names.
- Attachment binaries are not stored or sent to Documents.
- HTML remains source-faithful but is explicitly untrusted.
- No cross-service product database or AI/Digital Documents dependency was
  introduced.
- `bin/` and `obj/` outputs are not tracked.

## 9. Performance and operational notes

Raw content is bounded and only loaded by tenant-scoped detail repository
queries. List queries project before pagination. Analytics uses database
grouping/counting and formats only aggregate keys after materialization.
The compact idempotency index avoids oversized MySQL `utf8mb4` indexes while
the principal tenant/date filters have targeted indexes.

The configured full-stack application workflow was restarted once after the
changes, but its constrained multi-service launcher finished without leaving
ports open because the pre-existing Monitoring process encountered duplicate
rollup rows while optional services were unavailable. No Intake exception was
reported. A focused rebuilt `Intake.Api` process was then started directly on
port 5013 and `/health` returned 200 with `{"service":"intake"}`. The full-stack
workflow limitation is environmental and was not changed as part of B04.

## 10. Known limits and B05 handoff

B04 intentionally does not implement mailbox polling, Graph/Gmail/IMAP
retrieval, webhook delivery, raw-content HTTP access, attachment upload,
document processing, AI, OCR, matching, review, or downstream routing.
Failure telemetry is intentionally limited to attempts that reached a trusted
tenant/source boundary; untrusted malformed requests are rejected without
creating tenant-attributable rows.
There is no WebApplicationFactory HTTP suite in this repository yet, so 401/403
and cross-tenant endpoint isolation remain covered by the authorization and
tenant-scoped implementation plus repository-level validation rather than
automated HTTP assertions.

B05 should consume the captured email aggregate and attachment metadata for
the next bounded responsibility (document/artifact integration) without
rewriting the immutable source provenance or treating capture completion as
artifact-processing completion. B05 was not started.