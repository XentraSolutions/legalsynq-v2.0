# Xenia Product and AI Service Implementation Plan

## 1. Summary and Fixed Decisions

Install **Xenia** as a new LegalSynq Core Platform Service and tenant-toggleable product. Xenia is the renamed implementation of the BRD's `Synq AI` concept.

Xenia will centralize AI capabilities for the LegalSynq ecosystem instead of embedding separate AI implementations in SynqLien, SynqFund, CareConnect, Flow, Documents, Reports, Commerce, Search, or future products.

Fixed implementation decisions:

- Product name: `Xenia`
- Backend product code: `XENIA`
- Frontend product code: `Xenia`
- Route prefix: `/xenia`
- Gateway API prefix: `/api/xenia`
- Service path: `apps/services/xenia`
- API project: `Xenia.Api`
- Application project: `Xenia.Application`
- Domain project: `Xenia.Domain`
- Infrastructure project: `Xenia.Infrastructure`
- Test project: `Xenia.Tests`
- API port: `5032`
- Database: `xenia_db`
- Table prefix: `xen_`
- Product owner: `PlatformAdmin`
- Tenant BYOAI owner: `TenantAdmin`
- No Xenia-specific product roles in v1
- Provider integrations in v1: OpenAI, Anthropic, Google Gemini, Azure OpenAI, AWS Bedrock
- Deployment models in v1: `Managed` and `BringYourOwnAI`

Xenia must support all BRD capabilities except the explicitly out-of-scope items: proprietary model training, custom LLM development, consumer public AI services, autonomous legal advice, autonomous medical diagnosis, voice AI, image understanding, private fine-tuned models, and advanced multi-agent collaboration.

## 2. Product, Identity, Tenant, and Entitlement Changes

### Shared Product Constants

Update shared product identifiers so all services use one canonical product code.

Required changes:

- Add `XENIA` to shared product constants.
- Remove or stop using `SYNQ_AI` as the canonical product code.
- Ensure product-code normalization maps any legacy `SYNQ_AI`, `SYNQAI`, or `SynqAI` references to `XENIA` only as a compatibility alias.
- Ensure frontend-facing product code maps to `Xenia`.

Affected areas:

- `shared/building-blocks`
- Identity product seed/configuration
- Tenant product entitlement APIs
- Control Center product types
- Tenant portal product maps
- Commerce product registry

### Identity Service

Update Identity so Xenia is a real product in the platform product catalog.

Required changes:

- Rename the existing seeded `SynqAI` product to `Xenia`.
- Change product code from `SYNQ_AI` to `XENIA`.
- Keep the existing product seed ID if possible to avoid unnecessary data churn.
- Update product description to reflect the BRD: "Enterprise intelligence platform for LegalSynq products."
- Add compatibility alias handling for inbound product identifiers:
  - `XENIA`
  - `Xenia`
  - `SYNQ_AI`
  - `SynqAI`
  - `SYNQAI`
- Ensure Identity product-access APIs can return `Xenia` to frontends.
- Do not seed roles such as `XENIA_ADMIN`, `XENIA_USER`, or `XENIA_MANAGER`.
- Product access remains controlled through existing tenant/user/group product access plus platform role checks.

### Tenant Service

Update tenant product configuration so Xenia can be enabled or disabled per tenant.

Required changes:

- Add Xenia to canonical product maps.
- Ensure Control Center tenant product toggles can enable or disable `XENIA`.
- Ensure tenant entitlement responses include Xenia when enabled.
- Ensure tenant product state supports:
  - enabled
  - disabled
  - trial or subscription-backed if existing product model supports it
- Do not add Xenia-specific tenant roles.
- Tenant-level Xenia behavior is controlled by entitlement plus Xenia tenant AI configuration.

### Control Center Product Toggle

Add Xenia to Control Center as a selectable tenant product.

Required changes:

- Add Xenia to the product catalog used by Control Center.
- Add Xenia to ProductCode TypeScript unions and API mappers.
- Add icon, label, description, and category.
- Recommended label: `Xenia`
- Recommended description: `Enterprise AI orchestration, agents, skills, knowledge, governance, and usage management.`
- Xenia appears alongside other products in the tenant product management UI.
- PlatformAdmin can toggle Xenia for a tenant.
- Enabling Xenia should allow selection of the deployment model:
  - Managed
  - Bring Your Own AI
- Disabling Xenia should block tenant and product-service calls to Xenia APIs.

### Tenant Portal Product Access

Update tenant portal product awareness so enabled tenants can access Xenia-powered features.

Required changes:

- Add frontend product code `Xenia`.
- Add nav/product metadata for Xenia.
- Rename existing `/ai` surface to either:
  - keep `/ai` as a compatibility route that redirects to `/xenia`, or
  - replace it with `/xenia`.
- Recommended decision: use `/xenia` as canonical and keep `/ai` as a temporary redirect.
- Existing placeholder AI dashboard should become a Xenia entry point.
- User-facing Xenia pages must require:
  - valid session
  - tenant context
  - Xenia tenant entitlement
  - existing product access where the request originates from a specific product

## 3. Xenia Service Boundary

Create Xenia as an independently startable .NET service following existing LegalSynq service conventions.

### Projects

Create:

- `apps/services/xenia/Xenia.Api`
- `apps/services/xenia/Xenia.Application`
- `apps/services/xenia/Xenia.Domain`
- `apps/services/xenia/Xenia.Infrastructure`
- `apps/services/xenia/Xenia.Tests`

Add the projects to `LegalSynq.sln` unless the repo's solution structure requires a separate service boundary.

### API Service

`Xenia.Api` responsibilities:

- Host Minimal API endpoints.
- Validate user JWTs for browser/BFF-originated calls.
- Validate service-token JWTs for internal product-service calls.
- Register tenant context, correlation ID, logging, health checks, and OpenAPI.
- Expose readiness and health endpoints.
- Register application services and infrastructure adapters.
- Apply authorization policies for PlatformAdmin and TenantAdmin APIs.

Required endpoints:

- `GET /health`
- `GET /ready`
- `GET /info`
- `GET /xenia/admin/overview`
- `GET /xenia/admin/tenants/{tenantId}/configuration`
- `PUT /xenia/admin/tenants/{tenantId}/configuration`
- `GET /xenia/admin/providers`
- `POST /xenia/admin/providers`
- `PUT /xenia/admin/providers/{providerConfigId}`
- `POST /xenia/admin/providers/{providerConfigId}/test`
- `GET /xenia/admin/models`
- `GET /xenia/admin/usage`
- `GET /xenia/admin/audit`
- `GET /xenia/admin/health/providers`
- `GET /xenia/tenant/configuration`
- `PUT /xenia/tenant/byoai/configuration`
- `POST /xenia/tenant/byoai/providers/test`
- `POST /xenia/conversations`
- `GET /xenia/conversations`
- `GET /xenia/conversations/{conversationId}`
- `POST /xenia/conversations/{conversationId}/messages`
- `POST /xenia/conversations/{conversationId}/messages/stream`
- `POST /xenia/internal/complete`
- `POST /xenia/internal/stream`
- `POST /xenia/internal/skills/{skillCode}/execute`
- `POST /xenia/internal/agents/{agentCode}/execute`
- `POST /xenia/internal/tools/{toolCode}/execute`

### Authorization Rules

PlatformAdmin APIs:

- Require `PlatformAdmin`.
- Can manage product-level Xenia settings.
- Can enable tenants for Managed or BYOAI.
- Can manage platform-scoped provider configs.
- Can view cross-tenant usage, audit, health, and cost reporting.

TenantAdmin BYOAI APIs:

- Require `TenantAdmin`.
- Require Xenia enabled for tenant.
- Can manage only tenant-scoped BYOAI provider settings.
- Cannot view platform provider secrets.
- Cannot view other tenant configs.
- Cannot change global model catalog, prompt catalog, marketplace publishing, or platform provider settings.

Internal APIs:

- Require service-token authentication.
- Require tenant ID.
- Require calling product/service identity.
- Require Xenia enabled for tenant.
- Preserve correlation ID, user ID where applicable, product code, and tenant context.

## 4. Data Model and Migrations

Create Xenia-owned tables in `xenia_db` with `xen_` prefix. Xenia must not write directly to other service databases.

### Core Configuration Tables

Add:

- `xen_tenant_ai_configurations`
- `xen_provider_configurations`
- `xen_provider_credentials`
- `xen_model_catalog`
- `xen_tenant_model_policies`
- `xen_budget_policies`
- `xen_quota_policies`

`xen_tenant_ai_configurations` stores:

- tenant ID
- deployment model: `Managed` or `BringYourOwnAI`
- default provider config ID
- default model
- temperature
- max tokens
- reasoning level
- retention policy
- moderation policy
- failover enabled
- allowed skills policy
- allowed agents policy
- tool permission policy
- created/updated metadata

`xen_provider_configurations` stores:

- provider config ID
- provider type: `OpenAI`, `Anthropic`, `Gemini`, `AzureOpenAI`, `AwsBedrock`
- scope: `Platform` or `Tenant`
- tenant ID when tenant-scoped
- display name
- base URL or endpoint
- region
- Azure deployment name when applicable
- default model
- allowed models
- timeout settings
- retry settings
- failover priority
- enabled status
- health status
- secret reference
- created/updated metadata

`xen_provider_credentials` stores encrypted credential material or vault references:

- credential ID
- provider config ID
- credential storage mode: `EncryptedDatabase` or `ExternalSecretReference`
- encrypted secret payload for BYOAI v1
- external secret reference for Managed providers
- fingerprint
- last four characters where safe
- last verified timestamp
- verification status
- rotation metadata
- created/updated metadata

Raw API keys must never be returned from APIs after creation.

### Conversation, Prompt, Skill, and Agent Tables

Add:

- `xen_conversations`
- `xen_conversation_messages`
- `xen_prompt_templates`
- `xen_prompt_versions`
- `xen_skills`
- `xen_skill_versions`
- `xen_agents`
- `xen_agent_versions`
- `xen_agent_skill_links`
- `xen_agent_tool_links`
- `xen_agent_knowledge_links`

These support:

- conversation persistence
- streaming responses
- session history
- prompt versioning
- variable substitution
- approval status
- rollback
- reusable skills
- reusable agents
- agent-to-skill/tool/knowledge composition

### Knowledge and RAG Tables

Add:

- `xen_knowledge_sources`
- `xen_knowledge_documents`
- `xen_knowledge_chunks`
- `xen_embedding_indexes`
- `xen_embedding_records`
- `xen_citations`

These support:

- document ingestion
- chunking
- embeddings
- semantic search
- similarity search
- citation tracking
- embedding model versioning
- knowledge freshness

Vector storage can be implemented using the repo's existing search/vector approach if present. If no existing vector backend is available, use a provider-neutral abstraction with an initial database-backed implementation suitable for development and tests.

### Tool, Audit, and Usage Tables

Add:

- `xen_tool_definitions`
- `xen_tool_execution_logs`
- `xen_ai_request_logs`
- `xen_usage_ledger`
- `xen_cost_ledger`
- `xen_provider_health_events`
- `xen_governance_events`
- `xen_marketplace_assets`
- `xen_marketplace_installations`

These support:

- secure LegalSynq service tool invocation
- request/response audit
- provider/model/user/tenant/product attribution
- latency tracking
- token tracking
- estimated cost tracking
- budget/quota enforcement
- provider health monitoring
- marketplace installation records

## 5. Provider Integration and Credential Handling

### Provider Gateway

Implement provider abstraction in `Xenia.Application` and provider adapters in `Xenia.Infrastructure`.

Core interfaces:

- `IAiProviderGateway`
- `IAiProviderAdapter`
- `IAiCredentialStore`
- `IAiUsageNormalizer`
- `IAiProviderHealthCheck`
- `IProviderRoutingPolicy`
- `IProviderFailoverPolicy`

Provider gateway responsibilities:

- Select provider based on tenant deployment model.
- Prefer tenant-scoped BYOAI config for BYOAI tenants.
- Prefer platform-scoped provider config for Managed tenants.
- Enforce allowed providers and models.
- Resolve credentials securely.
- Dispatch completion requests.
- Dispatch streaming requests.
- Normalize provider responses.
- Normalize errors.
- Normalize token and cost usage.
- Apply timeout, retry, rate limit, and failover policy.
- Emit audit and usage records for every request.

### Provider Adapters

Implement adapters for:

- OpenAI:
  - Responses API for completion/response generation
  - streaming support
  - embeddings support where needed for RAG
- Anthropic:
  - Messages API
  - streaming support
- Google Gemini:
  - Generate content API
  - streaming where available
- Azure OpenAI:
  - Azure endpoint and deployment-name based routing
  - API version support through config
- AWS Bedrock:
  - Converse API
  - ConverseStream where available
  - region-based configuration

Each adapter must support:

- connection validation
- non-streaming generation
- streaming generation if provider supports it
- provider-specific error mapping
- usage extraction
- health check
- model capability reporting where practical

### Credential Store

Implement two credential storage modes.

Managed provider credentials:

- Stored as platform secret references.
- Config rows contain only secret references.
- Runtime resolves through `IAiCredentialStore`.
- No raw LegalSynq-managed provider key is exposed in UI or API.

BYOAI credentials:

- TenantAdmin enters credentials once.
- Xenia validates the provider connection before marking it active.
- Xenia encrypts credential payload before persistence.
- Config rows store only credential reference/fingerprint.
- UI displays provider name, status, last verified date, and safe fingerprint.
- Raw key is never returned after save.
- Rotation creates a new encrypted credential version and marks the old version inactive.

Future vault migration:

- Keep credential APIs independent of storage backend.
- External vault integration can replace encrypted DB storage without API changes.

## 6. Deployment Model Behavior

### Managed Mode

When a tenant uses Managed mode:

- Xenia routes requests to platform-scoped provider configs.
- PlatformAdmin controls provider/model availability.
- TenantAdmin cannot edit provider credentials.
- Usage is tracked for Commerce billing.
- Xenia subscription and AI consumption can be billed by Commerce.
- Failover uses PlatformAdmin-approved providers only.
- Provider health is visible to PlatformAdmin.

Managed mode required flow:

1. PlatformAdmin enables Xenia for tenant.
2. PlatformAdmin selects `Managed`.
3. PlatformAdmin assigns default provider/model policy.
4. Tenant users and product services can invoke Xenia.
5. Xenia records audit, usage, estimated cost, and provider health.

### BYOAI Mode

When a tenant uses BYOAI mode:

- Xenia routes requests to tenant-scoped provider configs.
- TenantAdmin configures provider endpoint/region/model and credentials.
- Tenant owns provider billing outside LegalSynq.
- Commerce bills Xenia platform licensing and premium features only.
- Platform governance, audit, skills, agents, knowledge, and tool execution still apply.
- Managed fallback is disabled by default.
- Managed fallback may only be enabled by PlatformAdmin as an explicit audited policy.

BYOAI required flow:

1. PlatformAdmin enables Xenia for tenant.
2. PlatformAdmin selects `BringYourOwnAI`.
3. TenantAdmin opens tenant Xenia settings.
4. TenantAdmin selects provider.
5. TenantAdmin enters provider connection details and credential.
6. Xenia validates the connection.
7. Tenant users and product services can invoke Xenia.
8. Xenia records audit and usage but does not bill provider consumption through Commerce.

## 7. Control Center Xenia Module

Add a PlatformAdmin-only Xenia module to Control Center.

Required views:

- Overview:
  - enabled tenants
  - deployment model distribution
  - provider health
  - usage summary
  - cost summary
  - error rate
- Tenant Configuration:
  - tenant Xenia status
  - deployment model
  - assigned provider/model policy
  - budget/quota policy
  - retention policy
  - moderation policy
  - allowed skills/agents/tools
- Managed Provider Management:
  - create/edit platform provider config
  - test connection
  - enable/disable provider
  - set failover priority
  - configure default models
- Model Catalog:
  - provider
  - model ID
  - capability flags
  - context size
  - streaming support
  - embedding support
  - active/inactive
- Prompt Catalog:
  - prompt templates
  - versions
  - approval state
  - rollback
- Skill Catalog:
  - seeded BRD skills
  - version state
  - enabled/disabled
- Agent Catalog:
  - seeded BRD agents
  - skills/tools/knowledge links
  - version state
- Marketplace:
  - skills
  - agents
  - knowledge packs
  - workflow packs
  - prompt packs
  - industry templates
- Knowledge Sources:
  - source registry
  - ingestion status
  - freshness
  - citation coverage
- Usage and Cost:
  - tenant/product/user/provider/model breakdowns
  - token usage
  - estimated cost
  - latency
  - error rate
- Audit Logs:
  - prompts
  - responses
  - tool calls
  - knowledge references
  - governance events
- Conversation Browser:
  - searchable conversation list
  - tenant/product/user filters
  - retention-aware access
- Health Monitoring:
  - provider status
  - recent failures
  - failover events
- Secrets Management:
  - secret reference status
  - rotation metadata
  - verification status
  - no raw secret display

Control Center must use BFF route handlers and existing session helpers. Browser client code must not handle raw JWTs or call services directly.

## 8. Tenant Portal BYOAI and Xenia User Experience

### Tenant BYOAI Settings

Add tenant-facing Xenia settings only for TenantAdmins.

Required behavior:

- TenantAdmin can view Xenia deployment model.
- If tenant is Managed:
  - show read-only Managed status
  - do not expose platform credentials
- If tenant is BYOAI:
  - allow provider selection
  - allow endpoint/region/deployment configuration
  - allow credential entry/update
  - allow connection test
  - allow model selection within platform-allowed limits
  - show last verified date and provider health
- TenantAdmin cannot manage global catalog, platform providers, marketplace publishing, or other tenants.

### Xenia User Surface

Add or update user-facing Xenia pages.

Required behavior:

- Provide a Xenia dashboard entry point.
- Show available conversations, agents, skills, and knowledge features based on tenant policy.
- Allow conversation creation.
- Support streaming messages.
- Show citations when RAG is used.
- Show product context when launched from SynqLien, SynqFund, CareConnect, or another product.
- Keep end-user experience identical between Managed and BYOAI.

### User-Initiated Xenia Activation

Xenia must not run automatically inside normal product workflows. Each AI-assisted action must be explicitly initiated by the user or by an admin-configured workflow rule.

Required behavior:

- Product screens must expose a clear `Use Xenia` entry point where AI is available.
- Product-specific AI actions may use more precise labels, such as:
  - `Use Xenia`
  - `Ask Xenia`
  - `Summarize with Xenia`
  - `Analyze with Xenia`
  - `Draft with Xenia`
  - `Find Similar with Xenia`
  - `Generate with Xenia`
- Existing product actions such as search, document viewing, task editing, or report viewing must continue to work without automatically invoking Xenia.
- Xenia is invoked only after the user clicks the Xenia action or after a workflow rule explicitly configured by an authorized admin triggers it.
- Before sending context to Xenia, the UI must identify the scope of the request where practical:
  - current record
  - selected records
  - current search results
  - selected documents
  - current conversation
  - current report
  - current workflow/task
- Xenia-generated output must be presented for user review before it changes business data.
- Applying AI output to a product record, task, workflow, report, document, notification, or generated PDF requires an explicit user confirmation unless the action is part of an admin-approved workflow automation.
- The UI must make it clear when a result came from Xenia.
- The audit record must capture the user action that triggered Xenia, the product context, and whether the user applied or discarded the result.

Recommended product UI pattern:

1. User performs a normal product task, such as search, viewing a lien, reviewing a document, or editing a report.
2. Product displays a contextual `Use Xenia` action only if Xenia is enabled and the user is authorized.
3. User clicks the Xenia action and chooses or confirms the AI task.
4. Product sends tenant, user, product, correlation, source object references, and requested Xenia skill/agent/tool.
5. Xenia enforces entitlement, policy, quota, budget, provider routing, and audit.
6. Product displays the Xenia result in context.
7. User explicitly applies, saves, exports, sends, creates, or dismisses the result.

## 9. Cross-Product Integration

Xenia must be usable by all enabled LegalSynq products for the tenant.

Xenia usage is always explicit. Enabling Xenia for a tenant makes product-level Xenia actions available, but it does not cause normal product workflows to call AI automatically.

Initial integration targets:

- SynqLien:
  - `Summarize with Xenia` for medical records, lien notes, and case files
  - `Analyze with Xenia` for lien risk, recovery likelihood, and settlement posture
  - `Draft with Xenia` for demand letters and lien correspondence
  - `Review Portfolio with Xenia` for receivable and portfolio insights
  - `Ask Xenia` from a lien detail page using selected lien context
- SynqFund:
  - `Use Xenia` for underwriting assistance on a selected funding application
  - `Analyze Risk with Xenia` for risk scoring and funding recommendation support
  - `Forecast with Xenia` for settlement and repayment scenario analysis
  - `Review Portfolio with Xenia` for funder portfolio valuation
  - `Summarize with Xenia` for funding agreements and case materials
- CareConnect:
  - `Use Xenia` during intake to summarize entered information
  - `Analyze Referral with Xenia` for referral intelligence and provider-fit support
  - `Summarize with Xenia` for patient, provider, and appointment context
  - `Build Timeline with Xenia` for medical timeline generation
  - `Interpret with Xenia` for HL7/FHIR-related clinical data explanations
- Flow / Workflow:
  - `Create Workflow with Xenia` from a selected business goal or case context
  - `Suggest Tasks with Xenia` for current workflow state
  - `Analyze Workflow with Xenia` for bottlenecks and next-step recommendations
  - admin-approved workflow rules may invoke Xenia automatically only when explicitly configured
- Documents / Storage:
  - `Classify with Xenia` for selected uploaded documents
  - `Extract with Xenia` for OCR/entity extraction orchestration
  - `Summarize with Xenia` for selected documents
  - `Ask Xenia` for citation-backed document Q&A
  - `Find Similar with Xenia` for selected document similarity
- Reports:
  - `Generate with Xenia` for report narratives
  - `Summarize with Xenia` for analytics and executive summaries
  - `Explain with Xenia` for report metrics and trends
  - generated report text remains draft until the user applies it
- Notifications / Comms:
  - `Draft with Xenia` for notifications, emails, and message templates
  - `Summarize with Xenia` for communication history
  - `Rewrite with Xenia` for tone, clarity, or brevity
  - generated messages require user review before sending
- Commerce:
  - `Analyze Usage with Xenia` for usage, budget, and cost summaries
  - `Explain Billing with Xenia` for subscription and consumption questions
  - `Recommend Plan with Xenia` for plan or marketplace recommendations
  - Xenia usage metering and billing still run as platform accounting, but customer-facing AI analysis requires explicit action
- Search:
  - normal search must not automatically invoke Xenia
  - `Use Xenia` on a search page lets the user convert a natural-language question into a structured query
  - `Explain Results with Xenia` summarizes the current results
  - `Find Similar with Xenia` runs semantic retrieval from selected result records
  - `Refine with Xenia` suggests filters or related search terms
- Task:
  - `Create Tasks with Xenia` extracts action items from selected context
  - `Assign with Xenia` recommends assignees based on task context
  - `Summarize with Xenia` summarizes task history
  - created or reassigned tasks require explicit user confirmation

Internal product-service request requirements:

- service token
- tenant ID
- calling product code
- optional user ID
- correlation ID
- requested skill/agent/tool
- source object references where applicable
- audit classification
- user-triggered Xenia action label
- activation source: `UserClick` or `AdminConfiguredWorkflow`
- apply outcome: `Applied`, `Dismissed`, `Exported`, `Sent`, `Created`, or `NoChange`

All product-service calls must produce usage and audit records.

## 10. Tool Registry and Secure Tool Calling

Implement a Xenia tool registry for controlled calls into LegalSynq services.

Initial tools:

- `search_liens`
- `search_providers`
- `search_patients`
- `search_documents`
- `search_funding`
- `generate_report`
- `assign_task`
- `send_notification`
- `create_workflow`
- `generate_pdf`

Tool execution rules:

- Every tool declares required product/service permission.
- Every call enforces Identity authorization.
- Every call enforces tenant isolation.
- Every call preserves tenant/user/correlation context.
- Every call emits audit logs.
- Tools return structured results, not raw service internals.
- Tool execution can be disabled per tenant policy.
- Tool execution can be restricted per skill or agent.

## 11. Governance, Security, Compliance, and Retention

Required governance capabilities:

- model allow/deny policies
- provider allow/deny policies
- prompt approval state
- tenant budget enforcement
- monthly quota enforcement
- retention policies
- content moderation hooks
- optional PHI/PII redaction
- secure tool execution
- audit export support

Security requirements:

- strict tenant isolation
- encrypted secrets
- encryption at rest and in transit
- no raw API key exposure after save
- no client-side JWT handling
- service-to-service calls use service tokens
- downstream services continue validating auth
- product-service calls preserve tenant and user context
- audit records for every prompt, response, provider call, tool call, error, and governance decision

Compliance design targets:

- HIPAA-aware deployments
- SOC 2 operational controls
- GDPR data management
- customer-defined retention policies
- audit exports
- provider isolation for BYOAI tenants

## 12. Commerce, Billing, Budgets, and Marketplace

### Commerce Integration

Add Xenia to Commerce monetization.

Managed mode billing:

- Xenia subscription
- managed AI usage
- token or request consumption
- premium skills/agents
- marketplace assets

BYOAI billing:

- Xenia platform license
- premium skills/agents
- marketplace assets
- no provider consumption billing through LegalSynq

Budget and quota behavior:

- enforce tenant monthly quotas
- enforce budget thresholds
- record estimated cost
- emit budget governance events
- block or degrade requests when hard limits are reached
- warn when soft thresholds are reached if notification infrastructure supports it

### Marketplace

Support asset types:

- skills
- agents
- knowledge packs
- workflow packs
- prompt packs
- industry templates

V1 requirements:

- marketplace asset registry
- installation records
- tenant enablement state
- PlatformAdmin management
- Commerce purchase/linkage hooks

Future partner publishing should be represented in the model but does not require full UI in v1.

## 13. Observability and Non-Functional Requirements

Required observability:

- structured logs
- metrics
- distributed tracing hooks
- provider health checks
- service health checks
- alerting integration hooks
- request latency tracking
- provider latency tracking
- streaming initiation latency tracking
- token and cost metrics
- error-rate metrics

Non-functional targets:

- 99.9% availability target
- horizontally scalable APIs
- stateless API layer
- multi-region ready design
- streaming response support
- average response initiation under two seconds where provider latency permits
- provider failover support
- extensible provider framework
- future multimodal provider support without product-service rewrites

## 14. Gateway, Startup, Deployment, and Operations

### Gateway

Add Xenia YARP routes and cluster config.

Required behavior:

- route `/api/xenia/*` to Xenia API
- preserve authorization headers
- preserve tenant and correlation headers
- configure local destination `http://localhost:5032`
- include environment-variable override compatibility for deployment

### Local Development Scripts

Update scripts so Xenia can run with the full stack.

Required updates:

- `scripts/run-dev.sh`
- `scripts/stop-dev.sh`
- production build scripts
- publish scripts
- systemd/all-service scripts where applicable
- database setup scripts if services are explicitly listed

Startup behavior:

- restore/build Xenia service
- start Xenia API on port `5032`
- include health check in readiness output
- stop Xenia process cleanly
- avoid duplicate ports

### Configuration

Add safe development config only.

Required configuration:

- connection string placeholder for `xenia_db`
- JWT validation settings consistent with other services
- service-token validation settings
- provider configs must not include real secrets
- local provider credentials must come from environment variables or local secret mechanisms
- no committed real API keys

## 15. Seed Data

Seed platform-level Xenia records needed for v1.

Required seed data:

- Xenia product catalog entry
- default model catalog entries for supported providers
- BRD skills:
  - Medical Summary
  - Medical Chronology
  - Lien Risk Assessment
  - Settlement Timeline
  - Provider Summary
  - Funding Recommendation
  - Portfolio Analysis
  - OCR Extraction
  - Translation
  - Classification
  - Entity Extraction
  - Report Generation
  - Natural Language Search
- BRD agents:
  - Lien Analyst
  - Funding Underwriter
  - Medical Reviewer
  - Medical Coding Assistant
  - Compliance Officer
  - Executive Assistant
  - Provider Success Assistant
  - Customer Support Assistant
  - Operations Coordinator
- initial tool definitions
- default governance policies
- default retention policy
- default marketplace asset placeholders where needed

Seeded provider configs should not include real credentials.

## 16. Testing and Validation Plan

### Backend Tests

Add Xenia unit and integration tests for:

- deployment model routing
- Managed provider routing
- BYOAI provider routing
- credential encryption and no raw-key readback
- provider config validation
- provider connection testing
- prompt rendering/versioning
- skill execution
- agent execution
- RAG retrieval/citation behavior
- tool authorization
- tenant isolation
- quota enforcement
- budget enforcement
- audit logging
- usage ledger creation
- provider failover
- disabled tenant rejection

Provider adapter contract tests:

- OpenAI mocked HTTP tests
- Anthropic mocked HTTP tests
- Gemini mocked HTTP tests
- Azure OpenAI mocked HTTP tests
- AWS Bedrock mocked tests
- streaming response tests
- provider error normalization tests
- usage normalization tests

### Frontend Tests

Control Center:

- Xenia appears in product catalog.
- PlatformAdmin can enable/disable Xenia for a tenant.
- PlatformAdmin can select Managed or BYOAI.
- PlatformAdmin can manage provider configs.
- Provider secrets are never displayed.
- Usage/audit/health screens render from API responses.

Tenant portal:

- TenantAdmin can view BYOAI settings only for own tenant.
- Managed tenants see read-only managed status.
- BYOAI tenants can configure and test provider connection.
- Xenia dashboard is gated by entitlement.
- Disabled tenants cannot access Xenia pages.
- Product pages show contextual Xenia actions only when Xenia is enabled and the user is authorized.
- Normal product actions, including search, document view, task edit, and report view, do not call Xenia until the user clicks a Xenia action.
- Xenia results are presented for review and are not applied to product data until the user confirms.

### E2E Acceptance Scenarios

Required scenarios:

- PlatformAdmin enables Xenia for a tenant.
- PlatformAdmin selects Managed deployment.
- Tenant user sends a Xenia conversation message through Managed provider.
- PlatformAdmin selects BYOAI deployment.
- TenantAdmin configures OpenAI BYOAI credentials.
- Tenant user sends a Xenia conversation message through tenant provider config.
- Product service invokes Xenia through internal service-token API.
- Xenia calls a registered tool with tenant/user/product context.
- User performs normal product search and no Xenia request is created.
- User clicks `Use Xenia` on search results and Xenia creates a request with `UserClick` activation source.
- User clicks `Summarize with Xenia` for a selected document and the summary stays draft until applied.
- User dismisses a Xenia result and no product business data changes.
- Admin-configured workflow automation invokes Xenia only after an authorized admin explicitly enables that workflow rule.
- Audit record is created for prompt, response, provider, model, user, tenant, product, tool call, latency, tokens, and cost.
- Audit record includes Xenia action label, activation source, source object references, and apply outcome.
- Usage ledger is created.
- Disabled tenant receives authorization failure.
- Tenant cannot access another tenant's provider config, credential metadata, conversations, knowledge, audit, or usage.

### Commands

Run targeted validation:

- `dotnet build apps/services/xenia/Xenia.Api/Xenia.Api.csproj`
- `dotnet test apps/services/xenia/Xenia.Tests/Xenia.Tests.csproj`
- `dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj`
- `dotnet test shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/BuildingBlocks.Tests.csproj`
- `pnpm --dir apps/control-center type-check`
- `pnpm --dir apps/web type-check`

Also run affected Identity, Tenant, and Commerce builds/tests after their specific changes.

Live provider smoke tests are optional and should run only when safe local credentials are available.

## 17. Acceptance Criteria

Xenia is complete when:

- It operates as a standalone LegalSynq Core Platform Service.
- It is available as a tenant-toggleable product in Control Center.
- PlatformAdmin can manage Xenia globally.
- TenantAdmin can manage only tenant-scoped BYOAI settings.
- No Xenia-specific product roles are introduced.
- It supports Managed and BYOAI deployment models.
- It supports OpenAI, Anthropic, Gemini, Azure OpenAI, and AWS Bedrock through a pluggable provider framework.
- It exposes unified APIs for LegalSynq products.
- It supports conversations, streaming, prompts, skills, agents, knowledge/RAG, tool calling, governance, audit, usage, and marketplace assets.
- It requires explicit user initiation, such as `Use Xenia`, `Ask Xenia`, `Summarize with Xenia`, or another product-specific Xenia action, before invoking AI from product workflows.
- It does not automatically call AI from normal product actions such as search, document viewing, task editing, or report viewing.
- It requires explicit user confirmation before Xenia output changes product business data, except for admin-configured workflow automation.
- It maintains strict tenant isolation.
- It stores provider configs in DB records.
- It stores API keys through credential storage, not plain config rows.
- It integrates with Identity, Commerce, Workflow, Notification, Search, Documents/Storage, Monitoring, and product services.
- It supports cross-product usage by SynqLien, SynqFund, CareConnect, Administration, Flow, Commerce, Search, Documents, Reports, Notifications, Task, and future products.
- It can support future providers and multimodal capabilities without product-service rewrites.

## 18. Assumptions

- `Xenia` is the final name and replaces `Synq AI` everywhere user-facing.
- `XENIA` is the canonical product code.
- Existing `SynqAI` seed IDs may be reused to avoid unnecessary migration churn.
- Legacy `SynqAI`/`SYNQ_AI` names are compatibility aliases only.
- Xenia is a core platform service, not a single-product feature.
- Xenia is consumed by all enabled LegalSynq products for a tenant.
- PlatformAdmin manages product/global settings.
- TenantAdmin manages only tenant-owned BYOAI settings.
- Provider config is DB-backed.
- Raw provider credentials are never stored in normal config tables.
- The first implementation includes real provider adapters with mocked contract tests.
- External vault integration is future-compatible but not required for v1 if encrypted DB-backed tenant credentials are implemented.
