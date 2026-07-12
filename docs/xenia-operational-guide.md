# Xenia Operational Guide

## Purpose

Xenia is the LegalSynq AI service boundary. It is the canonical implementation of the former `SynqAI` product concept and now uses:

- Backend product code: `XENIA`
- Frontend product code: `Xenia`
- Canonical tenant route: `/xenia`
- Gateway API prefix: `/api/xenia`
- Service path: `apps/services/xenia`
- Local API port: `5032`

The current implementation is a working service scaffold. It supports authentication, routing, tenant/provider configuration, conversations, streaming responses, internal execution endpoints, audit tracking, and usage tracking. It does not yet implement the full planned persistent data model, encrypted secret storage, or real provider adapters.

## Current Architecture

### Core service boundary

The Xenia service is split into the standard LegalSynq layers:

- `apps/services/xenia/Xenia.Api`
- `apps/services/xenia/Xenia.Application`
- `apps/services/xenia/Xenia.Domain`
- `apps/services/xenia/Xenia.Infrastructure`
- `apps/services/xenia/Xenia.Tests`

### Key runtime files

- API entry point: `apps/services/xenia/Xenia.Api/Program.cs`
- Auth and authorization: `apps/services/xenia/Xenia.Api/Authentication/AuthenticationServiceCollectionExtensions.cs`
- Application orchestration: `apps/services/xenia/Xenia.Application/XeniaService.cs`
- API request/response contracts: `apps/services/xenia/Xenia.Application/Contracts.cs`
- Domain state models: `apps/services/xenia/Xenia.Domain/Models.cs`
- Current storage implementation: `apps/services/xenia/Xenia.Infrastructure/InMemoryXeniaStateStore.cs`

### Current storage model

Xenia currently uses an in-memory state store. This means:

- data is process-local
- conversations reset on service restart
- provider configuration resets on service restart unless reseeded
- audit and usage history reset on service restart
- no `xenia_db` persistence is active yet

The current appsettings include a placeholder connection string for `xenia_db`, but the service does not yet use EF-backed persistence.

## Product, Identity, and Tenant Integration

### Product rename and canonicalization

The repository now treats Xenia as the canonical AI product:

- shared product normalization is implemented in `shared/building-blocks/BuildingBlocks/Authorization/ProductCodes.cs`
- identity alias normalization maps legacy `SynqAI`, `SYNQAI`, and `SYNQ_AI` to `XENIA`
- tenant canonical product mapping also accepts `xenia`, `synqai`, and `synq_ai`

### Identity migration

Identity migration `apps/services/identity/Identity.Infrastructure/Persistence/Migrations/20260703000001_RenameSynqAiToXenia.cs` performs the current rename work.

It updates:

- `idt_Products`
- `idt_AccessGroups`
- `idt_GroupProductAccess`
- `idt_GroupRoleAssignments`
- `idt_Policies`
- `idt_TenantProductEntitlements`
- `idt_UserProductAccess`
- `idt_UserRoleAssignments`

This preserves compatibility with existing product data while making `XENIA` the canonical code.

### Tenant entitlement behavior

Tenant-facing product mapping includes Xenia, so tenant detail and entitlement payloads can now resolve Xenia correctly.

Current tenant access expectations are:

- tenant must have Xenia enabled
- tenant user must have valid session and tenant context
- protected Xenia tenant pages require product access
- platform admin can see Xenia in control-center product handling

## Gateway and Runtime Operations

### Gateway routing

Gateway config lives in `apps/gateway/Gateway.Api/appsettings.json`.

The Xenia routes currently include:

- `GET /api/xenia/health`
- `GET /api/xenia/ready`
- `GET /api/xenia/info`
- protected routing for `/api/xenia/{**catch-all}`
- deny rule for `/api/xenia/internal/{**catch-all}` at the public gateway layer

The gateway forwards Xenia traffic to:

- cluster: `xenia_cluster`
- destination: `http://localhost:5032`

### Local startup

Local startup is wired in `scripts/run-dev.sh`.

Operational behavior:

- the full solution is restored and built
- `Xenia.Api` is built explicitly
- the Xenia service is started with `ASPNETCORE_URLS=http://0.0.0.0:5032`
- the gateway starts on `5010`
- web frontend is available at `5000`
- control-center is available at `5004`

Shutdown is wired in `scripts/stop-dev.sh`, which includes `Xenia.Api/Xenia.Api.csproj` in the known .NET service list.

## Authentication and Authorization

Xenia supports two auth modes.

### User JWT auth

Used for:

- tenant portal traffic
- control-center traffic
- browser/BFF-originated requests

Configured policies:

- `XeniaAuthenticatedUser`
- `XeniaPlatformAdmin`
- `XeniaTenantAdminOrAbove`

Role expectations:

- `PlatformAdmin` for admin routes
- `TenantAdmin` or `PlatformAdmin` for tenant BYOAI routes
- any authenticated user for conversation routes

### Service token auth

Used for:

- internal product-service calls
- future service-to-service AI execution

Configured policy:

- `XeniaInternalService`

Requirements:

- valid service JWT
- subject claim must start with `service:`
- tenant ID must be supplied either in the token context, request, or headers

## API Surface

### Operational endpoints

Anonymous endpoints:

- `GET /health`
- `GET /ready`
- `GET /info`

### Platform admin endpoints

Mounted under `/xenia/admin`.

Current capabilities:

- get overview
- get tenant configuration
- save tenant configuration
- list platform and tenant-visible providers
- create platform provider
- update platform provider
- test platform provider connectivity
- list model catalog
- list usage report
- list audit history
- list provider health

### Tenant admin endpoints

Mounted under `/xenia/tenant`.

Current capabilities:

- read current tenant configuration
- save BYOAI tenant provider configuration
- test tenant BYOAI provider configuration

### End-user conversation endpoints

Mounted under `/xenia`.

Current capabilities:

- create conversation
- list conversations
- get conversation
- post message
- post streaming message

### Internal service endpoints

Mounted under `/xenia/internal`.

Current capabilities:

- complete
- stream
- execute skill
- execute agent
- execute tool

These are currently modeled as structured internal execution responses rather than real integrations into downstream providers or tools.

## Functional Behavior

### Tenant configuration model

Xenia tenant configuration currently stores:

- enabled state
- deployment model
- default provider configuration
- default model
- temperature
- max tokens
- reasoning level
- retention policy
- moderation policy
- failover flag
- allowed skills
- allowed agents
- allowed tools

This model is held in `XeniaTenantConfiguration` in `apps/services/xenia/Xenia.Domain/Models.cs`.

### Deployment models

The code supports both declared deployment models:

- `Managed`
- `BringYourOwnAI`

Current behavior:

- `Managed` resolves platform-scoped providers first
- `BringYourOwnAI` prefers tenant-scoped providers
- if no tenant-scoped BYOAI provider is available, provider resolution falls back to an enabled platform provider

This fallback is convenient for the scaffold but is looser than the full production plan, which expects tighter policy enforcement.

### Provider configuration behavior

Provider config currently captures:

- provider type
- scope: `Platform` or `Tenant`
- tenant ID for tenant-scoped configs
- display name
- endpoint
- region
- Azure deployment name
- default model
- allowed models
- timeout
- retry count
- failover priority
- enabled state
- verification status
- last verified timestamp
- credential fingerprint
- stored-credential flag

Supported provider enums:

- `OpenAI`
- `Anthropic`
- `Gemini`
- `AzureOpenAI`
- `AwsBedrock`

### Provider verification

Provider connectivity tests are currently simulated by application logic.

Current test behavior:

- if an existing provider already has stored credentials, verification passes
- if a request includes an `ApiKey`, verification passes and a fingerprint is stored
- platform providers can verify without a new key in the request

What is not yet implemented:

- outbound provider HTTP calls
- provider-specific SDK integrations
- encrypted credential storage
- provider error normalization
- real health checks against external APIs

### Seeded managed providers

The in-memory store seeds three managed providers:

- Managed OpenAI
- Managed Anthropic
- Managed Gemini

It also seeds a simple model catalog with provider/model capabilities.

## Conversation Flow

### Creating a conversation

When a user creates a conversation:

1. Xenia resolves tenant ID from claims or headers.
2. Xenia creates a new conversation record with:
   - title
   - activation source
   - product code
   - source reference
   - creator user ID
3. The conversation is stored in memory.
4. An audit event is appended.
5. If an initial message was supplied, Xenia immediately runs the same add-message flow used by the normal message endpoint.

### Adding a message

When a user posts a message:

1. Xenia loads the conversation for the tenant.
2. Xenia loads or creates the tenant configuration.
3. Xenia resolves the provider based on deployment model and provider scope.
4. Xenia stores the user message.
5. Xenia synthesizes an assistant response with product and activation context.
6. Xenia stores the assistant message.
7. Xenia appends a usage event.
8. Xenia appends an audit event.
9. Xenia returns:
   - updated conversation
   - user message
   - assistant message
   - chunked output
   - usage summary

### Streaming behavior

Streaming endpoints currently do not stream token-by-token from a real provider.

Instead:

1. Xenia creates the assistant response first.
2. The response text is split into chunks of roughly ten words.
3. The API emits SSE events:
   - `message.delta`
   - `message.completed`

This gives the UI a working streaming contract while the provider adapters are still scaffolded.

## Internal Execution Flow

Internal APIs are intended for product-service invocation.

Current flow:

1. calling service presents a valid service token
2. tenant ID is resolved from request or auth context
3. Xenia resolves product code and activation source
4. Xenia resolves provider from tenant configuration
5. Xenia generates a structured internal response
6. Xenia appends usage and audit records
7. Xenia returns:
   - mode
   - tenant ID
   - product code
   - provider
   - model
   - output text
   - chunked output
   - usage summary
   - optional skill, agent, or tool code

Current internal modes are placeholders for:

- completion
- stream
- skill execution
- agent execution
- tool execution

No real downstream tool registry or product integration is implemented yet.

## Frontend Behavior

### Tenant portal

Canonical Xenia tenant route:

- `/xenia/dashboard`

Compatibility route:

- `/ai` redirects to `/xenia/dashboard`

Tenant portal entry point:

- `apps/web/src/app/(platform)/xenia/dashboard/page.tsx`

Current UX behavior:

- requires authenticated session
- requires Xenia product access
- shows Xenia as the AI orchestration surface
- points product teams at the BFF route `/api/xenia/...`
- states that AI output should remain explicit and reviewable

### Tenant BFF proxy

The tenant portal should call:

- `/api/xenia/...`

The proxy route:

- reads `platform_session`
- forwards bearer token to the gateway
- forwards the request body
- preserves content type
- returns the downstream response body
- passes through `X-Correlation-Id` when present

### Control center

Platform admin overview page:

- `apps/control-center/src/app/xenia/page.tsx`

Current behavior:

- requires `PlatformAdmin`
- calls `/api/xenia/admin/overview`
- displays:
  - enabled tenant count
  - provider count
  - conversation count
  - estimated cost
  - deployment model distribution
  - provider health

This is currently a read-only operational overview, not a complete admin console.

## Usage, Audit, and Health

### Usage tracking

Each conversation turn or internal execution appends a usage event with:

- tenant ID
- user ID
- event kind
- provider
- model
- prompt token estimate
- completion token estimate
- estimated USD cost
- timestamp

Token accounting is currently estimated by string length, not provider-reported usage.

### Audit tracking

Xenia appends audit events for actions such as:

- conversation creation
- conversation message creation
- tenant configuration updates
- BYOAI configuration updates
- provider creation
- provider update
- provider testing
- internal execution

### Provider health

Provider health is also stored in-memory and currently reflects:

- seeded managed-provider availability
- latest connectivity test result

It is not yet tied to live provider telemetry.

## Operational Limitations

The current implementation should be treated as a functional foundation, not a production-complete AI platform.

Not implemented yet:

- EF-backed `xenia_db`
- `xen_*` tables
- encrypted BYOAI credential storage
- secret vault integration
- real OpenAI, Anthropic, Gemini, Azure OpenAI, or Bedrock adapters
- RAG / embeddings / knowledge ingestion
- prompt catalog, versioning, or marketplace assets
- secure tool registry with downstream service invocation
- quota and budget enforcement
- tenant disable hard-blocks on all execution paths
- commerce consumption metering integration
- persisted retention policies
- full admin and tenant settings screens

## Recommended Current Use

The current Xenia implementation is suitable for:

- validating the service boundary
- validating auth rules
- validating gateway and BFF wiring
- validating product rename and entitlement behavior
- validating conversation endpoint contracts
- validating streaming event format
- validating audit and usage event plumbing
- providing a base for EF persistence and provider integration work

It is not yet suitable for:

- production AI execution
- secure customer BYOAI storage
- durable conversation history
- regulated audit retention
- cross-service tool automation

## Suggested Next Steps

Highest-priority follow-on work:

1. replace the in-memory state store with EF-backed `xenia_db` persistence
2. implement encrypted credential storage and credential abstraction
3. add real provider adapters and provider health checks
4. enforce tenant enablement and deployment policies on all execution paths
5. build actual control-center and tenant-admin settings screens
6. add internal product-service integrations and tool execution policies
