# Xenia AI Assistant Gateway API Reference for Mobile Apps

This reference covers only the Xenia AI assistant APIs that are reachable through the LegalSynq gateway. Email automation, operations, and automation registry APIs are intentionally omitted.

Based on:

- Gateway route config: `apps/gateway/Gateway.Api/appsettings.json`
- Xenia assistant endpoints: `apps/services/xenia/Xenia.Api/Endpoints/XeniaAssistantEndpoints.cs`
- Xenia assistant DTOs: `apps/services/xenia/Xenia.Application/Assistant/AssistantDtos.cs`

Last verified against the local code on 2026-07-22.

## Gateway Base

Local development gateway:

```text
http://localhost:5010
```

Production mobile clients should call the production gateway host, not the direct Xenia service port `5035`.

The gateway exposes Xenia under `/xenia` and removes that prefix before forwarding to the service.

```text
Mobile client -> GET /xenia/assistant/bootstrap
Gateway       -> GET /assistant/bootstrap on Xenia (:5035)
```

## Common Headers

Protected assistant routes require:

```http
Authorization: Bearer <LegalSynq JWT>
Content-Type: application/json
```

For streaming:

```http
Accept: text/event-stream
```

Optional:

```http
X-Correlation-Id: mobile-request-001
```

The gateway accepts correlation IDs up to 100 characters using letters, numbers, hyphen, or underscore. Otherwise it generates one and returns it in the response.

## Tenant and User Context

Xenia resolves tenant context from the signed JWT `tenant_id` claim. It does not trust caller-supplied tenant headers or arbitrary tenant IDs for normal assistant usage.

User-specific assistant data depends on the JWT `sub` claim:

- Conversations are listed and created for the caller.
- Preferences are read and updated for the caller.
- Usage summaries are scoped to the caller and tenant.

## Authorization

| API group | Gateway path | Required Xenia policy | Accepted callers in current code |
|---|---|---|---|
| Assistant user APIs | `/xenia/assistant/*` | `XeniaAssistantUse` | `PlatformAdmin`, `SYNQ_AI` or `XENIA` product claims, `xenia.assistant.use`, `SYNQ_AI.assistant:use`, assistant manage permissions, or `xenia.admin`. |
| Assistant settings/config admin | `/xenia/admin/settings`, `/xenia/admin/config/*`, `/xenia/admin/tenants/*/config` | `XeniaAssistantManage` | `PlatformAdmin`, `xenia.assistant.manage`, `SYNQ_AI.assistant:manage`, or `xenia.admin`. |
| Assistant usage/audit admin | `/xenia/admin/usage`, `/xenia/admin/audit` | `XeniaAssistantUsageRead` | `PlatformAdmin`, `xenia.usage.read`, `SYNQ_AI.usage:read`, assistant manage permissions, or `xenia.admin`. |

For an end-user mobile app, the normal integration surface is `/xenia/assistant/*`. Treat `/xenia/admin/*` as control-center or operator-only APIs.

## User Assistant APIs

| Method | Gateway path | Auth | Description |
|---|---|---|---|
| GET | `/xenia/assistant/bootstrap` | Assistant use | First-load payload for the assistant UI. Returns enabled state, available agents, preferences, usage, and feature flags. |
| GET | `/xenia/assistant/agents` | Assistant use | Lists assistant agents available to the caller. |
| GET | `/xenia/assistant/conversations` | Assistant use | Lists visible conversation summaries for the caller. |
| POST | `/xenia/assistant/conversations` | Assistant use | Creates a new assistant conversation. |
| GET | `/xenia/assistant/conversations/{conversationId}` | Assistant use | Reads one conversation with user and assistant messages. |
| PATCH | `/xenia/assistant/conversations/{conversationId}` | Assistant use | Updates conversation metadata, currently title/archive state. |
| DELETE | `/xenia/assistant/conversations/{conversationId}` | Assistant use | Archives a conversation. Returns `204` on success. |
| POST | `/xenia/assistant/conversations/{conversationId}/messages` | Assistant use | Creates a user message and returns a completed assistant response. |
| POST | `/xenia/assistant/conversations/{conversationId}/messages:stream` | Assistant use | Creates a user message and streams assistant response events using Server-Sent Events. |
| GET | `/xenia/assistant/preferences` | Assistant use | Reads caller assistant UI preferences. |
| PATCH | `/xenia/assistant/preferences` | Assistant use | Updates caller assistant UI preferences. |

## Assistant Admin APIs

These are gateway-facing but should not be exposed in a normal end-user mobile app.

| Method | Gateway path | Auth | Description |
|---|---|---|---|
| GET | `/xenia/admin/settings` | Assistant manage | Returns effective global assistant runtime settings, excluding API key value. |
| PUT | `/xenia/admin/settings` | Assistant manage | Updates global non-secret assistant runtime settings. |
| GET | `/xenia/admin/config/global` | Assistant manage | Reads visible global assistant configuration entries. |
| PUT | `/xenia/admin/config/global` | Assistant manage | Upserts global assistant settings and secret references. |
| GET | `/xenia/admin/tenants/{tenantId}/config` | Assistant manage | Reads visible assistant configuration for one tenant. |
| PUT | `/xenia/admin/tenants/{tenantId}/config` | Assistant manage | Upserts tenant-scoped assistant settings and secret references. |
| GET | `/xenia/admin/usage?tenantId={tenantId}` | Assistant usage read | Aggregates last 30 days of assistant usage by tenant, agent, provider, and model. |
| GET | `/xenia/admin/audit` | Assistant usage read | Placeholder audit endpoint. Returns an empty event array until the platform audit adapter backs it. |

## Request and Response Matrix

"None" means no JSON request body.

| Method | Gateway path | Request | Success response |
|---|---|---|---|
| GET | `/xenia/assistant/bootstrap` | None | `AssistantBootstrapDto` |
| GET | `/xenia/assistant/agents` | None | `{ "agents": [AssistantAgentDto] }` |
| GET | `/xenia/assistant/conversations` | None | `{ "conversations": [AssistantConversationSummaryDto] }` |
| POST | `/xenia/assistant/conversations` | Body: `CreateAssistantConversationRequest` | `201 AssistantConversationDto` |
| GET | `/xenia/assistant/conversations/{conversationId}` | Path: `conversationId` | `AssistantConversationDto` |
| PATCH | `/xenia/assistant/conversations/{conversationId}` | Path: `conversationId`; body: `UpdateAssistantConversationRequest` | `AssistantConversationDto` |
| DELETE | `/xenia/assistant/conversations/{conversationId}` | Path: `conversationId`; no body | `204 No Content` |
| POST | `/xenia/assistant/conversations/{conversationId}/messages` | Path: `conversationId`; body: `CreateAssistantMessageRequest` | `AssistantMessageDto` |
| POST | `/xenia/assistant/conversations/{conversationId}/messages:stream` | Path: `conversationId`; body: `CreateAssistantMessageRequest` | SSE stream of `AssistantStreamEventDto` |
| GET | `/xenia/assistant/preferences` | None | `AssistantUserPreferenceDto` |
| PATCH | `/xenia/assistant/preferences` | Body: `UpdateAssistantPreferencesRequest` | `AssistantUserPreferenceDto` |
| GET | `/xenia/admin/settings` | None | `AssistantAdminSettingsDto` |
| PUT | `/xenia/admin/settings` | Body: `UpdateAssistantAdminSettingsRequest` | `204 No Content` |
| GET | `/xenia/admin/config/global` | None | `{ "entries": [ConfigurationEntryDto], "precedence": "...", "secrets": "..." }` |
| PUT | `/xenia/admin/config/global` | Body: `AssistantAdminConfigRequest` | `204 No Content` |
| GET | `/xenia/admin/tenants/{tenantId}/config` | Path: `tenantId` | `{ "tenantId": "<tenantGuid>", "entries": [ConfigurationEntryDto] }` |
| PUT | `/xenia/admin/tenants/{tenantId}/config` | Path: `tenantId`; body: `AssistantAdminConfigRequest` | `204 No Content` |
| GET | `/xenia/admin/usage?tenantId={tenantId}` | Optional query: `tenantId` | Usage aggregate response |
| GET | `/xenia/admin/audit` | None | `{ "events": [], "note": "..." }` |

## Request Bodies

Use lower camelCase JSON.

### Create Conversation

`POST /xenia/assistant/conversations`

```json
{
  "agentKey": "default",
  "title": "Case questions",
  "source": "mobile",
  "contextJson": "{\"route\":\"/lien/cases/123\"}"
}
```

Fields:

| Field | Type | Required | Notes |
|---|---|---|---|
| `agentKey` | string | No | Agent key to use. Server can choose a default when omitted. |
| `title` | string | No | Display title for the conversation. |
| `source` | string | No | Caller/source hint such as `mobile`. |
| `contextJson` | string | No | JSON encoded as a string, not an object. Keep compact. |

### Update Conversation

`PATCH /xenia/assistant/conversations/{conversationId}`

```json
{
  "title": "Updated title",
  "archived": false
}
```

Fields:

| Field | Type | Required | Notes |
|---|---|---|---|
| `title` | string | No | Updates conversation title. |
| `archived` | boolean | No | Updates archive state. |

### Create Message

`POST /xenia/assistant/conversations/{conversationId}/messages`

`POST /xenia/assistant/conversations/{conversationId}/messages:stream`

```json
{
  "content": "Summarize the latest activity for this case.",
  "contextJson": "{\"caseId\":\"018f0000-0000-7000-8000-000000000001\"}",
  "clientMessageId": "mobile-018f0000-0000-7000-8000-000000000001"
}
```

Fields:

| Field | Type | Required | Notes |
|---|---|---|---|
| `content` | string | Yes | User message text. Subject to assistant prompt length limits. |
| `contextJson` | string | No | JSON encoded as a string. Use for page/case/referral context hints. |
| `clientMessageId` | string | No | Client idempotency/correlation hint. Useful for mobile retry UI. |

### Update Preferences

`PATCH /xenia/assistant/preferences`

```json
{
  "defaultAgentKey": "default",
  "contextHintsEnabled": true,
  "preferencesJson": "{\"compactMode\":true}"
}
```

Fields:

| Field | Type | Required | Notes |
|---|---|---|---|
| `defaultAgentKey` | string | No | Preferred default assistant agent. |
| `contextHintsEnabled` | boolean | No | Enables/disables contextual hints in the assistant. |
| `preferencesJson` | string | No | JSON encoded as a string for UI-specific preferences. |

### Update Assistant Runtime Settings

Admin only: `PUT /xenia/admin/settings`

```json
{
  "provider": "OpenAI",
  "modelKey": "gpt-5",
  "openAiBaseUrl": "https://api.openai.com",
  "openAiTimeoutSeconds": 60,
  "openAiReasoningEffort": "medium",
  "openAiTextVerbosity": "medium",
  "openAiMaxOutputTokens": 4096
}
```

Validation:

| Field | Rule |
|---|---|
| `provider` | Must be `Fake` or `OpenAI`. |
| `modelKey` | Required. |
| `openAiBaseUrl` | Required absolute `http` or `https` URL. |
| `openAiTimeoutSeconds` | 1-600. |
| `openAiReasoningEffort` | Blank/null or `minimal`, `low`, `medium`, `high`. |
| `openAiTextVerbosity` | Blank/null or `low`, `medium`, `high`. |
| `openAiMaxOutputTokens` | Null or greater than zero. |

The OpenAI API key is not returned and is not persisted by this endpoint. It remains appsettings/environment configuration.

### Upsert Assistant Config

Admin only:

`PUT /xenia/admin/config/global`

`PUT /xenia/admin/tenants/{tenantId}/config`

```json
{
  "settings": {
    "provider": "OpenAI",
    "modelKey": "gpt-5"
  },
  "secretReferences": {
    "openAi.apiKey": "secretref://xenia/openai"
  }
}
```

Fields:

| Field | Type | Notes |
|---|---|---|
| `settings` | object<string,string|null> | Non-secret assistant configuration values. |
| `secretReferences` | object<string,string|null> | Secret references. Values are not returned by read APIs. |

## Response Models

### Bootstrap

`AssistantBootstrapDto`

```json
{
  "enabled": true,
  "agents": [
    {
      "agentKey": "default",
      "name": "Xenia",
      "description": "Tenant assistant",
      "version": "1.0.0",
      "enabled": true,
      "allowedTools": ["careconnect.referrals.search"],
      "requiredProductCodes": ["SYNQ_AI"]
    }
  ],
  "preferences": {
    "defaultAgentKey": "default",
    "contextHintsEnabled": true,
    "preferencesJson": "{}"
  },
  "usage": {
    "requestsThisMonth": 10,
    "inputTokensThisMonth": 1200,
    "outputTokensThisMonth": 500,
    "estimatedCostUsdThisMonth": 0,
    "monthlyRequestLimit": null,
    "monthlyTokenLimit": null
  },
  "featureFlags": {
    "streaming": "true"
  }
}
```

Main fields:

| Field | Type | Notes |
|---|---|---|
| `enabled` | boolean | Whether assistant is available for the caller. |
| `agents` | `AssistantAgentDto[]` | Agents the caller can use. |
| `preferences` | `AssistantUserPreferenceDto` | Caller preferences. |
| `usage` | `AssistantUsageSummaryDto` | Current monthly usage and optional limits. |
| `featureFlags` | object<string,string> | Assistant feature flags. |

### Agent

`AssistantAgentDto`

```json
{
  "agentKey": "default",
  "name": "Xenia",
  "description": "Tenant assistant",
  "version": "1.0.0",
  "enabled": true,
  "allowedTools": ["careconnect.referrals.search"],
  "requiredProductCodes": ["SYNQ_AI"]
}
```

### Conversation Summary

Returned by `GET /xenia/assistant/conversations`.

```json
{
  "id": "018f0000-0000-7000-8000-000000000001",
  "agentKey": "default",
  "agentVersion": "1.0.0",
  "title": "Case questions",
  "source": "mobile",
  "status": "Active",
  "lastMessageAtUtc": "2026-07-22T00:00:00Z",
  "createdAtUtc": "2026-07-22T00:00:00Z",
  "updatedAtUtc": "2026-07-22T00:00:00Z"
}
```

### Conversation Detail

Returned by create, detail, and update conversation APIs.

```json
{
  "id": "018f0000-0000-7000-8000-000000000001",
  "agentKey": "default",
  "agentVersion": "1.0.0",
  "title": "Case questions",
  "source": "mobile",
  "status": "Active",
  "contextJson": "{}",
  "lastMessageAtUtc": "2026-07-22T00:00:00Z",
  "createdAtUtc": "2026-07-22T00:00:00Z",
  "updatedAtUtc": "2026-07-22T00:00:00Z",
  "messages": []
}
```

### Message

`AssistantMessageDto`

```json
{
  "id": "018f0000-0000-7000-8000-000000000001",
  "conversationId": "018f0000-0000-7000-8000-000000000002",
  "role": "Assistant",
  "content": "Here is the summary.",
  "provider": "OpenAI",
  "providerResponseId": "resp_123",
  "inputTokens": 120,
  "outputTokens": 80,
  "finishReason": "completed",
  "createdAtUtc": "2026-07-22T00:00:00Z",
  "metadataJson": "{}",
  "citations": [
    {
      "id": "018f0000-0000-7000-8000-000000000003",
      "sourceType": "careconnect.referral",
      "sourceId": "REF-100",
      "label": "Referral REF-100",
      "url": null
    }
  ]
}
```

`role` is a string such as `User`, `Assistant`, or a system/tool role depending on persisted message data. Client UIs should render only the roles they understand and fail closed for unknown roles.

### Streaming Events

Streaming uses Server-Sent Events, not WebSocket.

Each event has this wire shape:

```text
event: delta
data: {"type":"delta","delta":"partial text","message":null,"error":null}

event: completed
data: {"type":"completed","delta":null,"message":{...AssistantMessageDto...},"error":null}
```

`AssistantStreamEventDto`

| Field | Type | Notes |
|---|---|---|
| `type` | string | Event type such as `delta`, `completed`, or error-related event names. |
| `delta` | string/null | Partial text for streaming deltas. |
| `message` | `AssistantMessageDto`/null | Final persisted assistant message when available. |
| `error` | string/null | Safe error text when the stream reports an error. |

Mobile client guidance:

- Use an SSE-capable HTTP client.
- Disable response buffering if the framework buffers by default.
- Support cancellation when the user leaves the screen.
- Keep the non-streaming message endpoint as a fallback.
- Treat the final `message` event as authoritative for persisted content.

### Preferences

`AssistantUserPreferenceDto`

```json
{
  "defaultAgentKey": "default",
  "contextHintsEnabled": true,
  "preferencesJson": "{}"
}
```

### Usage Summary

`AssistantUsageSummaryDto`

```json
{
  "requestsThisMonth": 10,
  "inputTokensThisMonth": 1200,
  "outputTokensThisMonth": 500,
  "estimatedCostUsdThisMonth": 0,
  "monthlyRequestLimit": null,
  "monthlyTokenLimit": null
}
```

If monthly request or token limits are configured, message creation can fail after the caller reaches quota.

### Admin Settings

`AssistantAdminSettingsDto`

```json
{
  "provider": "OpenAI",
  "modelKey": "gpt-5",
  "openAiBaseUrl": "https://api.openai.com",
  "openAiApiKeyConfigured": true,
  "openAiTimeoutSeconds": 60,
  "openAiReasoningEffort": "medium",
  "openAiTextVerbosity": "medium",
  "openAiMaxOutputTokens": 4096,
  "lastUpdatedAtUtc": "2026-07-22T00:00:00Z"
}
```

The API returns only `openAiApiKeyConfigured`, never the key value.

### Configuration Entry

`ConfigurationEntryDto`

```json
{
  "id": "018f0000-0000-7000-8000-000000000001",
  "scopeType": "Global",
  "scopeId": null,
  "namespace": "assistant",
  "configurationKey": "modelKey",
  "configurationValue": "gpt-5",
  "valueType": "string",
  "isSecret": false,
  "version": 1,
  "updatedAtUtc": "2026-07-22T00:00:00Z"
}
```

When `isSecret` is true, `configurationValue` and `valueType` are returned as `null`.

### Admin Usage

`GET /xenia/admin/usage?tenantId={tenantId}`

```json
{
  "since": "2026-06-22T00:00:00Z",
  "usage": [
    {
      "tenantId": "018f0000-0000-7000-8000-000000000001",
      "agentKey": "default",
      "provider": "OpenAI",
      "modelKey": "gpt-5",
      "requests": 25,
      "inputTokens": 10000,
      "outputTokens": 5000,
      "estimatedCostUsd": 0,
      "averageLatencyMs": 950
    }
  ]
}
```

The endpoint groups the last 30 days and returns at most 100 rows.

## Error Responses

Common responses:

| Status | Meaning |
|---|---|
| `400` | Bad input or malformed JSON. |
| `401` | Missing/invalid JWT or missing required tenant/user context. |
| `403` | Authenticated but lacks required policy/permission. |
| `404` | Conversation not found, archived, or not visible to caller. |
| `409` | Conflict from application state, such as quota or invalid operation surfaced by middleware. |
| `500` | Unexpected server error. Xenia returns safe `application/problem+json`. |

Unhandled Xenia exceptions are converted to a safe problem response:

```json
{
  "type": "https://httpstatuses.io/500",
  "title": "An unexpected error occurred.",
  "status": 500,
  "detail": null
}
```

Endpoint-level validation can also return ASP.NET validation problem JSON, especially for admin settings.

## Mobile Integration Flow

Recommended normal assistant startup:

1. Authenticate through the platform and obtain a LegalSynq JWT with tenant context and Xenia/SynqAI access.
2. Call `GET /xenia/assistant/bootstrap`.
3. Select `preferences.defaultAgentKey` or the first enabled available agent.
4. Call `GET /xenia/assistant/conversations` to show recent conversations.
5. Create a conversation with `POST /xenia/assistant/conversations` when the user starts a new thread.
6. Send messages with the streaming route when SSE is supported; otherwise use the non-streaming message route.
7. Persist `conversationId` locally for navigation, but always refetch conversation detail from the API when reopening.

## Assistant Grounding

Xenia can call product-owned assistant-tool APIs in services such as CareConnect and SynqLien. Xenia forwards the caller's bearer token to those product services.

Implications:

- The mobile user's JWT must include permissions for the downstream product data being discussed.
- If the product tool adapter is unavailable, permission is denied, or the page context is missing, Xenia may answer with less grounding.
- `contextJson` should include page-level hints such as route, referral ID, case ID, lien ID, or queue context when available.
- Do not put secrets or client-only private data in `contextJson`.

## Known Limitations for Mobile

- No checked-in OpenAPI contract was found for the Xenia assistant. This document reflects the Minimal API registrations in code.
- The direct Xenia service port is `5035`, but mobile apps should use the gateway `/xenia/...` paths.
- Tenant/user context comes from JWT claims. A token without `tenant_id` or a usable `sub` claim will not behave like a normal tenant user.
- `contextJson`, `preferencesJson`, and `metadataJson` are strings containing JSON, not nested JSON objects.
- Assistant streaming uses SSE. Some mobile HTTP clients buffer SSE or drop streams in the background.
- Non-streaming message requests can be slow because the assistant may run tool-planning iterations before returning.
- Assistant prompt input is capped by server config. Current defaults include `XeniaAssistant:MaxPromptCharacters = 8000` and `XeniaAssistant:MaxConversationMessages = 40`.
- Assistant provider timeout defaults to 60 seconds for OpenAI settings unless overridden.
- Monthly request/token limits are nullable config. If enabled, message creation can fail after quota is reached.
- Conversations and usage are DB-backed. In local/degraded no-database startup, behavior can differ from production.
- Admin routes expose platform-level assistant settings and usage. Do not expose them to normal tenant mobile users.
