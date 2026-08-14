# LSI-B07 Generic Classification & Synq AI

## Acceptance status

**Implemented and locally validated.** The Intake classification foundation is complete for bounded, text-only generic artifact/document classification. The focused Intake suite passes, the Intake API builds, and the full `LegalSynq.sln` build passes with repository-existing warnings. Applying the migrations to disposable MySQL could not be verified because no reachable disposable MySQL instance is available in this environment.

The implementation does not claim that a live AI provider is available by default. The only real adapter is advertised only when its endpoint is configured.

## Scope and design

The implementation preserves the generic chain:

`Tenant → AI Policy → Provider/Model → Classification Profile → IntakeArtifact → Classification Result`

Classification is intentionally limited to generic artifact/document type. It does not extract or infer patient, provider, case, lien amount, settlement, matching, review, or other business-decision data.

The implementation uses:

- A tenant-scoped Intake AI policy.
- LegalSynq-managed and tenant BYOAI access modes.
- Credential references only; raw API keys and tokens are never persisted.
- A provider-neutral registry and request/result contract.
- A versioned classification profile, taxonomy, prompt, and output schema.
- Explicit classification and retry commands rather than coupling artifact upload success to AI availability.
- Separate immutable classification history from the artifact lifecycle.
- SHA-256 binding between the retrieved content and the classified artifact.
- Bounded text-only input. OCR, binary extraction, and PDF extraction remain unsupported.

## Implementation file list

### Domain

- `apps/services/intake/Intake.Domain/Classification/ClassificationCodes.cs`
- `apps/services/intake/Intake.Domain/Classification/ClassificationDefinitionIds.cs`
- `apps/services/intake/Intake.Domain/Classification/TenantAiPolicy.cs`
- `apps/services/intake/Intake.Domain/Classification/ArtifactClassification.cs`
- `apps/services/intake/Intake.Domain/Classification/ClassificationProfileDefinition.cs`
- `apps/services/intake/Intake.Domain/Classification/ClassificationTaxonomyDefinition.cs`
- `apps/services/intake/Intake.Domain/Classification/ClassificationPromptDefinition.cs`

### Application and contracts

- `apps/services/intake/Intake.Application/Classification/IClassificationService.cs`
- `apps/services/intake/Intake.Application/Classification/IClassificationRepository.cs`
- `apps/services/intake/Intake.Application/Classification/ClassificationService.cs`
- `apps/services/intake/Intake.Application/Classification/ClassificationProviderContracts.cs`
- `apps/services/intake/Intake.Application/Classification/ClassificationInputPolicy.cs`
- `apps/services/intake/Intake.Application/Classification/ClassificationTaxonomy.cs`
- `apps/services/intake/Intake.Application/Classification/ClassificationAudit.cs`
- `apps/services/intake/Intake.Application/Classification/ManagedAiPolicyDefaults.cs`
- `apps/services/intake/Intake.Contracts/Classification/ClassificationContracts.cs`
- `apps/services/intake/Intake.Contracts/Configuration/LienIntakeV1Configuration.cs`
- `apps/services/intake/Intake.Application/Configuration/ProcessingProfileRegistry.cs`

### Infrastructure

- `apps/services/intake/Intake.Infrastructure/Classification/SynqAiOptions.cs`
- `apps/services/intake/Intake.Infrastructure/Classification/ConfiguredSynqAiProviderRegistry.cs`
- `apps/services/intake/Intake.Infrastructure/Classification/ConfiguredManagedAiPolicyDefaults.cs`
- `apps/services/intake/Intake.Infrastructure/Classification/EnvironmentAiCredentialResolver.cs`
- `apps/services/intake/Intake.Infrastructure/Classification/OpenAiSynqAiProvider.cs`
- `apps/services/intake/Intake.Infrastructure/Classification/IntakeArtifactContentReader.cs`
- `apps/services/intake/Intake.Infrastructure/Audit/ClassificationAuditSink.cs`
- `apps/services/intake/Intake.Infrastructure/Artifacts/DocumentsServiceClient.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/EfClassificationRepository.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/IntakeDbContext.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/Configurations/ClassificationConfiguration.cs`
- `apps/services/intake/Intake.Infrastructure/DependencyInjection.cs`

### API

- `apps/services/intake/Intake.Api/Authorization/IntakeAuthorizationPolicies.cs`
- `apps/services/intake/Intake.Api/Endpoints/ClassificationEndpoints.cs`
- `apps/services/intake/Intake.Api/Program.cs`

### Database

- `apps/services/intake/Intake.Infrastructure/Persistence/Migrations/20260814032908_AddGenericClassification.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/Migrations/20260814032908_AddGenericClassification.Designer.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/Migrations/20260814034147_RefineGenericClassification.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/Migrations/20260814034147_RefineGenericClassification.Designer.cs`
- `apps/services/intake/Intake.Infrastructure/Persistence/Migrations/IntakeDbContextModelSnapshot.cs`

The follow-on `RefineGenericClassification` migration reconciles the original B07 schema with the final model. It adds policy guardrails, prompt schema versioning, execution/idempotency fields, current-result markers, decision/reason fields, attempt metadata, requested time, token totals, latency, and the unique indexes. Before creating the unique indexes it backfills deterministic execution keys, suffixes duplicate legacy history rows, and normalizes multiple legacy current markers to the newest row.

### Tests

- `apps/services/intake/Intake.Tests/ClassificationTests.cs`

The B05 and B06 reports were not modified. The B07 specification asset under `attached_assets/` was not modified.

## Policy, provider, and credential behavior

`TenantAiPolicy` stores:

- Enablement and access mode.
- Provider and model code.
- A credential reference.
- Maximum output tokens.
- Provider timeout.
- Maximum attempts.
- An optimistic-concurrency policy version.

Managed policies use centrally configured defaults from `SynqAi:ManagedProviderCode`, `SynqAi:ManagedModelCode`, and `SynqAi:ManagedCredentialReference`. Tenant policy requests cannot replace those managed values. The default managed credential is the platform reference `secret://platform/synq-ai`, not a secret value.

BYOAI policies must use a reference scoped to the same tenant:

`secret://tenant/{tenantId}/...`

Raw keys, cross-tenant references, shared environment references, and platform references are rejected for BYOAI. The resolver resolves only approved references at execution time and returns the secret value only to the provider call; it is not persisted or returned in API responses.

The provider registry advertises only providers whose adapter is actually configured. OpenAI is the only real adapter in B07 and remains unavailable until `SynqAi:OpenAi:BaseUrl` is configured.

## Profile, taxonomy, prompt, and output contract

The system seed is:

- Profile: `LIEN_DOCUMENT_CLASSIFICATION_V1`
- Taxonomy: `LIEN_DOCUMENT_TAXONOMY_V1`
- Prompt: `LIEN_DOCUMENT_CLASSIFIER`
- Output schema version: `1`

The lien taxonomy includes:

`MEDICAL_RECORD`, `MEDICAL_BILL`, `MEDICAL_STATEMENT`, `EXPLANATION_OF_BENEFITS`, `LIEN_DOCUMENT`, `LETTER_OF_PROTECTION`, `ATTORNEY_DOCUMENT`, `SETTLEMENT_DOCUMENT`, `INSURANCE_DOCUMENT`, `IDENTIFICATION_DOCUMENT`, `CORRESPONDENCE`, `OTHER`, and `UNKNOWN`.

The versioned output contract requires exactly:

- `classificationCode`
- `classificationLabel`
- `confidence`
- `reason`
- `evidence`

The OpenAI adapter requests strict JSON-schema output, bounds output tokens, and independently validates the returned object. It rejects missing or extra fields, invalid types, invalid confidence, an oversized reason, oversized evidence, or more than three evidence items. The application then validates the selected code against the persisted taxonomy and never stores hidden reasoning.

## Execution, idempotency, history, and decisions

Before provider execution, the service:

1. Resolves the tenant policy and checks enablement.
2. Resolves the active versioned profile, taxonomy, and prompt.
3. Validates schema-version consistency.
4. Loads bounded text through the Documents boundary.
5. Recomputes and verifies the artifact SHA-256.
6. Redacts instruction-like prompt-injection text and bounds reason/evidence.
7. Claims a pending classification attempt with tenant-scoped repository conditions.

The execution key is a SHA-256 digest of the artifact identity/hash, profile, taxonomy, prompt, provider, and model provenance. A unique database index and duplicate-safe insert make repeated requests reuse a completed result and prevent concurrent duplicate active attempts.

Retries create a distinct immutable history row with a monotonic attempt number and an execution-key suffix. Retryability and the lower of the policy/configuration attempt limits are enforced before a new attempt is claimed. A nullable `CURRENT` marker is cleared together with `IsCurrent`; final current-result replacement is performed by a short repository transaction that clears prior markers and saves the replacement together. The unique current-result index therefore permits only one current result per tenant/artifact under MySQL semantics without leaving a losing concurrent attempt stuck in processing.

Successful results persist:

- Provider/model and definition versions.
- Artifact SHA-256.
- Classification code/label and confidence.
- `ACCEPTED` or `LOW_CONFIDENCE` decision status based on the configured threshold.
- Safe reason and evidence.
- Input/output/total token counts.
- Provider response identifier.
- Measured latency.
- Requested, created, updated, completed, and attempt metadata.

Failures persist a bounded safe message, failure code, retryability, attempt metadata, and `UNCLASSIFIED` decision status. Classification history is not deleted when a later result becomes current.

## Audit and authorization

The API exposes tenant-scoped policy management, profile/taxonomy reads, current-result reads, history reads, explicit classify, and explicit retry operations. Classification read/manage policies were added to the Intake authorization set.

Audit entries contain tenant, artifact, classification, status, failure code, actor, and correlation metadata. They do not contain document text, raw credentials, prompt contents, or chain-of-thought.

## Validation performed

- Intake test project build: passed with 0 errors.
- Intake classification and existing Intake tests: **40 passed, 0 failed**.
- Full `LegalSynq.sln` build after restore: passed with **0 errors**; existing package/security and generated-code warnings remain.
- `git diff --check`: passed.
- EF migration listing includes:
  - `20260813232726_InitialIntakeConfiguration`
  - `20260814000306_AddTenantIntakeSources`
  - `20260814004756_AddInboundEmailRepository`
  - `20260814010032_AddInboundEmailCaptureFailures`
  - `20260814023351_AddIntakeArtifacts`
  - `20260814025609_AddManualIntake`
  - `20260814032908_AddGenericClassification`
  - `20260814034147_RefineGenericClassification`
- The configured `Start application` workflow was restarted successfully.
- Proxy health returned HTTP 200 with `{"status":"ok","service":"proxy"}`.

The focused tests cover prompt-injection redaction, evidence/reason bounds, duplicate taxonomy rejection, reference-only credentials, cross-tenant BYOAI rejection, managed-policy constraints, schema rejection, artifact hash binding, retry/history behavior, retry-limit enforcement, low-level tenant isolation, completed-result idempotency, decision status, and usage telemetry.

## Limitations and known gaps

- Disposable MySQL migration application remains unverified because no reachable disposable MySQL environment was available. EF generation, snapshot reconciliation, index definitions, and migration listing were verified.
- No live OpenAI endpoint or AI credential is configured locally, so live provider execution was not claimed or tested.
- HTTP-level classification integration tests are not present; current coverage is focused application/provider-contract unit coverage.
- OCR, PDF/binary extraction, and image classification are intentionally out of scope for B07. The content reader accepts bounded text from Documents only.
- Existing repository package vulnerability warnings remain, including known advisories for `Microsoft.Extensions.Caching.Memory`, `MimeKit`, and other unrelated packages.

## Explicitly out of scope

B07 does not implement patient/provider/case extraction, lien or settlement amount extraction, entity matching, legal or business decisions, review queues, human adjudication, OCR, PDF parsing, automated upload-time classification, provider-specific credential storage, prompt-managed tenant editing, or deployment.

## Recommendation

Proceed to **LSI-B08 only** after a disposable MySQL migration run and, if required by release policy, HTTP integration coverage against a test provider endpoint.