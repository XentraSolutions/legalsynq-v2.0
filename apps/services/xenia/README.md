# Xenia — Automation Platform

Xenia is a **standalone, tenant-aware automation platform** built within the LegalSynq monorepo. It is intentionally independent of LegalSynq's domain logic and is designed to be portable across platforms.

---

## Architecture

Xenia follows the same four-layer clean architecture used by all LegalSynq services:

```
Xenia.Domain/           Domain entities, enums, event contracts
Xenia.Application/      Interfaces, DTOs, adapter contracts
Xenia.Infrastructure/   EF Core, MySQL, adapter implementations, DI
Xenia.Api/              ASP.NET Core minimal API, endpoints, middleware
Xenia.Tests/            xUnit unit tests
```

---

## Service Independence

Xenia must not contain LegalSynq-specific business logic. It accesses platform capabilities through **adapter interfaces**:

| Adapter | Interface | Dev Implementation |
|---|---|---|
| Tenant | `ITenantAdapter` | `UnavailableTenantAdapter` |
| Identity | `IIdentityAdapter` | `UnavailableIdentityAdapter` |
| Document | `IDocumentAdapter` | `UnavailableDocumentAdapter` |
| Audit | `IAuditAdapter` | `UnavailableAuditAdapter` (logs fallback) |
| Notification | `INotificationAdapter` | `UnavailableNotificationAdapter` |
| Storage | `IStorageAdapter` | `UnavailableStorageAdapter` |
| Workflow | `IWorkflowAdapter` | `UnavailableWorkflowAdapter` |
| AI | `IAiAdapter` | `UnavailableAiAdapter` |

To wire a real adapter, register a concrete implementation in `DependencyInjection.cs` before the corresponding `Unavailable*Adapter`.

---

## Tenant Context

Tenant context is resolved from the cryptographically signed JWT `tenant_id` claim. Xenia does not trust arbitrary caller-supplied tenant identifiers.

- `JwtTenantContextResolver` — reads `tenant_id` from JWT
- `XeniaTenantContextAccessor` — scoped store for the request context
- Tenant-scoped endpoints return `400 Bad Request` when context is missing

---

## Module System

Modules are the extensibility unit of Xenia. Each module encapsulates a specific automation capability.

```csharp
// Register a module (idempotent — do this at startup)
await moduleRegistry.RegisterModuleAsync(
    moduleKey: "xenia.email",
    name: "Email Automation",
    version: "1.0.0",
    description: "Processes inbound email automation rules.",
    configurationNamespace: "xenia.email");
```

Global module registry: `IModuleRegistry`
Per-tenant enablement: `ITenantModuleRegistry`

---

## Configuration Precedence

Configuration resolves in ascending precedence:

1. **Global** — applies to all tenants and modules
2. **Tenant** — overrides global for a specific tenant
3. **Module** — overrides global for a specific module
4. **TenantModule** — highest precedence, tenant + module specific

Secrets are stored as references (not plaintext). The `/configuration` endpoint never returns secret values.
Assistant provider settings use the same store for non-secret runtime overrides. Control-center `/xenia/settings`
persists the OpenAI provider selection, model key, reasoning effort, text verbosity, output token cap, base URL,
and timeout without editing `appsettings`.

---

## Event System

Xenia uses a platform-neutral event envelope:

```csharp
var envelope = new XeniaEventEnvelope<MyPayload>
{
    EventId = Guid.CreateVersion7(),
    EventType = "xenia.module.enabled",
    EventVersion = 1,
    OccurredAt = DateTime.UtcNow,
    TenantId = tenantId,
    Payload = myPayload,
};
await publisher.PublishAsync(envelope, ct);
```

Development: `InMemoryEventPublisher` — no persistence, no guaranteed delivery.
Production: Replace with a durable broker adapter (SQS, RabbitMQ).

---

## Common API Endpoints

This table is a service-local summary of common Xenia routes. For the Xenia AI assistant
gateway reference intended for mobile app integration, see
[`docs/xenia-ai-assistant-gateway-api-reference.md`](../../../docs/xenia-ai-assistant-gateway-api-reference.md).

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/health` | Anonymous | Liveness probe |
| GET | `/ready` | Anonymous | Readiness (DB + deps) |
| GET | `/info` | Anonymous | Service metadata |
| GET | `/modules` | `xenia.read` | Module list |
| GET | `/modules/{key}` | `xenia.read` | Single module |
| PUT | `/modules/{key}/enable` | `xenia.admin` | Enable globally |
| PUT | `/modules/{key}/disable` | `xenia.admin` | Disable globally |
| GET | `/modules/tenant` | `xenia.read` | Tenant module list |
| GET | `/adapters` | `xenia.read` | Adapter status |
| GET | `/configuration` | `xenia.read` | Non-secret config |
| GET | `/admin/settings` | `xenia.assistant.manage` | Effective assistant runtime settings |
| PUT | `/admin/settings` | `xenia.assistant.manage` | Update global assistant runtime settings |
| GET | `/assistant/bootstrap` | `xenia.assistant.use` | Assistant feature flags, agent catalog, and starter state |
| GET | `/assistant/agents` | `xenia.assistant.use` | Available assistant agents for the tenant |
| GET | `/assistant/conversations` | `xenia.assistant.use` | List visible conversations |
| POST | `/assistant/conversations` | `xenia.assistant.use` | Create a conversation |
| GET | `/assistant/conversations/{conversationId}` | `xenia.assistant.use` | Read a conversation with user/assistant messages only |
| PATCH | `/assistant/conversations/{conversationId}` | `xenia.assistant.use` | Rename or update conversation metadata |
| DELETE | `/assistant/conversations/{conversationId}` | `xenia.assistant.use` | Archive a conversation |
| POST | `/assistant/conversations/{conversationId}/messages` | `xenia.assistant.use` | Create a non-streaming message |
| POST | `/assistant/conversations/{conversationId}/messages:stream` | `xenia.assistant.use` | Stream an assistant response with grounded tool execution |
| GET | `/assistant/preferences` | `xenia.assistant.use` | Read assistant UI preferences |
| PATCH | `/assistant/preferences` | `xenia.assistant.use` | Update assistant UI preferences |
| GET | `/email/module` | `xenia.email.read` | Email module status for the current tenant |
| PUT | `/email/module/enable` | `xenia.email.manage` | Enable the email module for the current tenant |
| PUT | `/email/module/disable` | `xenia.email.manage` | Disable the email module for the current tenant |
| GET | `/email/providers` | `xenia.email.read` | Supported provider catalog and connector defaults |
| GET | `/email/sources` | `xenia.email.read` | List tenant email sources |
| POST | `/email/sources` | `xenia.email.manage` | Create an email source |
| GET | `/email/sources/{id}` | `xenia.email.read` | Get one email source |
| PUT | `/email/sources/{id}` | `xenia.email.manage` | Update an email source |
| DELETE | `/email/sources/{id}` | `xenia.email.manage` | Delete an email source |
| PUT | `/email/sources/{id}/enable` | `xenia.email.manage` | Enable an email source |
| PUT | `/email/sources/{id}/disable` | `xenia.email.manage` | Disable an email source |
| POST | `/email/sources/{id}/validate` | `xenia.email.validate` | Run a source connectivity validation |
| GET | `/email/sources/{id}/validation-history` | `xenia.email.read` | Read recent validation results |
| GET | `/email/settings` | `xenia.email.read` | Read tenant email settings |
| PUT | `/email/settings` | `xenia.email.manage` | Update tenant email settings |
| POST | `/email/sources/{sourceId}/sync` | `xenia.email.sync` | Trigger a sync run for a source |
| GET | `/email/sources/{sourceId}/sync-state` | `xenia.email.sync` | Read current sync cursor/lease state |
| GET | `/email/sources/{sourceId}/ingestion-history` | `xenia.email.sync` | Read source ingestion history |
| GET | `/email/messages` | `xenia.email.read` | Browse imported email messages |
| GET | `/email/messages/{id}` | `xenia.email.read` | Read one imported message |
| GET | `/api/v1/email/operations/summary` | `xenia.email.operations.read` | Operations dashboard summary |
| GET | `/api/v1/email/operations/runs` | `xenia.email.operations.read` | List ingestion runs |
| GET | `/api/v1/email/operations/alerts` | `xenia.email.operations.read` | List operational alerts |
| POST | `/api/v1/email/operations/alerts/{alertId}/acknowledge` | `xenia.email.alerts.manage` | Acknowledge an alert |
| POST | `/api/v1/email/operations/retention/run` | `xenia.email.retention.manage` | Run retention cleanup |

---

## Database

MySQL 8, EF Core 8 (Pomelo). Tables use `xn_` prefix.

| Table | Purpose |
|---|---|
| `xn_modules` | Global module registry |
| `xn_tenant_modules` | Per-tenant module enablement |
| `xn_platform_adapters` | Adapter health registry |
| `xn_configuration` | Layered configuration store |
| `xn_tenant_settings` | Per-tenant Xenia settings |

Connection string: `ConnectionStrings__XeniaDb`

---

## Local Development

```bash
# Set environment variables
export ConnectionStrings__XeniaDb="Server=127.0.0.1;Port=3306;Database=xenia_db;User=xenia;Password=xenia;"
export Jwt__SigningKey="dev-only-signing-key-minimum-32-chars-long!"
export ASPNETCORE_ENVIRONMENT="Development"
export XeniaAssistant__Provider="Fake"
export XeniaAssistant__ModelKey="xenia-fake"
export XeniaAssistant__OpenAI__ReasoningEffort="medium"
export XeniaAssistant__OpenAI__TextVerbosity="medium"
export XeniaAssistant__OpenAI__MaxOutputTokens="4096"
export XeniaAssistant__CareConnect__BaseUrl="http://127.0.0.1:5003"
export XeniaAssistant__SynqLien__BaseUrl="http://127.0.0.1:5009"
export Xenia__SkipDatabaseStartup="false"

# Run service
cd apps/services/xenia/Xenia.Api
dotnet run
```

Service starts on port **5035**.

If `ConnectionStrings__XeniaDb` is missing or still uses the placeholder value from `appsettings`, Xenia starts in a
degraded no-database mode for local bootstrap: DB-backed migrations, seeders, email ingestion workers, and durable
automation stores stay off, while selected email and automation endpoints fall back to in-memory or unavailable
implementations so the service can still boot. Set `Xenia__SkipDatabaseStartup=true` when you have a real database
configured but need to suppress migrations, seeders, and background workers for a local session.

---

## Testing

```bash
cd apps/services/xenia/Xenia.Tests
dotnet test
```

Tests use `Microsoft.EntityFrameworkCore.InMemory` — no real DB required.

---

## Email Module

Email automation is the first operational Xenia module. Current scope includes:

- Tenant-scoped email source CRUD, enable/disable, and connectivity validation.
- Provider catalog and connector defaults for Microsoft 365, Google, IMAP, POP3, and Exchange IMAP.
- Inbox/message browsing APIs plus attachment re-dispatch hooks.
- Manual sync, sync-state/history, and email operations endpoints for runs, alerts, metrics, and retention.
- Tenant portal integration through SynqLien settings pages at `/lien/settings/email-sources` and `/lien/settings/email-inbox`, with BFF route handlers under `/api/xenia/email/*`.

When a real `XeniaDb` connection is present and `Xenia:SkipDatabaseStartup` is false, Xenia also runs migrations,
seeders, ingestion workers, and lock renewal services for the email module. Without a real database, only the
bootstrap-safe fallback surfaces are available.

---

## Authorization

Xenia defines its own permission constants:

| Permission | Description |
|---|---|
| `xenia.read` | Read modules, adapters, configuration |
| `xenia.admin` | Manage modules and configuration |
| `xenia.modules.read` | Read module registry |
| `xenia.modules.manage` | Enable/disable modules |
| `xenia.adapters.read` | Read platform adapter status |
| `xenia.configuration.read` | Read non-secret configuration |
| `xenia.configuration.manage` | Set configuration values |
| `xenia.email.read` | Read module state, providers, sources, settings, and imported messages |
| `xenia.email.manage` | Create/update/delete/enable/disable sources and settings |
| `xenia.email.validate` | Run source validation checks |
| `xenia.email.sync` | Trigger or inspect source sync state and history |
| `xenia.email.operations.read` | View operational summaries, runs, alerts, and health state |
| `xenia.email.operations.manage` | Retry/cancel runs and update operational settings |
| `xenia.email.alerts.manage` | Acknowledge, resolve, or suppress alerts |
| `xenia.email.retention.manage` | Run or simulate retention cleanup |
| `xenia.assistant.use` | Use the Xenia assistant |
| `xenia.assistant.manage` | Manage assistant runtime settings |
| `xenia.usage.read` | Read assistant usage summaries |

These must be provisioned in the Identity service permission catalog for production use.

---

## Assistant Provider Configuration

Use control-center `/xenia/settings` for OpenAI assistant runtime configuration. The UI writes global overrides into
the Xenia configuration store for non-secret runtime fields only. `appsettings` provides the OpenAI API key and
safe fallback defaults for local bootstrap and fake-provider startup. Current persisted OpenAI runtime fields include
provider, model key, reasoning effort, text verbosity, max output tokens, base URL, and timeout. Set the actual key
in `XeniaAssistant:OpenAI:ApiKey` in the Xenia service appsettings; it is not persisted from control-center. During
local `dotnet run`, Xenia resolves those appsettings from the source `Xenia.Api` project directory so changes are not
stuck behind stale `bin/...` copies. `XeniaAssistant:MaxToolIterations` caps how many internal tool-planning passes
Xenia can make before it must return a final answer.

## Assistant Product Grounding

Xenia now uses an internal tool loop for grounded replies instead of a single prompt-only referral snapshot. The
assistant stores user-visible conversation messages separately from hidden tool messages, asks the provider to either
select a tool or finalize an answer, executes the selected tool server-side, and then synthesizes the final assistant
reply from the grounded results.

Product-specific tools are no longer composed from Xenia against user-facing product endpoints. Xenia now treats each
product service as the owner of its own assistant-tool API and calls those dedicated tool endpoints over HTTP using the
caller's bearer token and correlation id.

Current tenant-portal integration also sends structured page context with every message:

- Product, section, and route path
- Current CareConnect entity type and entity id when present
- Current SynqLien lien or case entity type and entity id when present
- Current list filters such as `search`, `status`, `providerName`, `referrerName`, `createdFrom`, and `createdTo`
- Page-scoped starter prompts for the Xenia drawer

Assistant message payloads now include `metadataJson` for UI-only enrichment. The tenant portal uses that metadata to
render lookup result cards, follow-up prompts, and grounded links without exposing tool messages in the transcript.

The first grounded assistant integration is CareConnect. When the user opens Xenia from a CareConnect route, Xenia can
resolve contextual ids from the current page and execute read-only CareConnect lookups before answering. Current tools
cover:

- Referral detail lookup with recent status history
- Referral history lookup for an explicit referral id
- Referral search across patient/client, provider, provider organization, law firm/referrer, and status/date filters
- Provider lookup by name, city, state, status, and specialty
- Referrer lookup from referral traffic
- Referral queue and KPI summaries with totals, status groups, date windows, and recent items

CareConnect assistant access is configured from `appsettings` only:

- `XeniaAssistant:CareConnect:BaseUrl`
- `XeniaAssistant:CareConnect:TimeoutSeconds`
- `XeniaAssistant:CareConnect:MaxHistoryItems`

SynqLien follows the same process. When the user opens Xenia from a SynqLien lien, case, marketplace, portfolio, or
queue route, Xenia can resolve contextual ids and execute read-only lien/case tools before answering. Current tools
cover:

- Lien detail lookup by id or lien number
- Lien search across subject, case number, status/status group, lien type, and created date filters
- Lien queue and KPI summaries with totals, status groups, date windows, and recent liens
- Case detail lookup by id or case number with linked liens and client/case metadata
- Case insights by id or case number for summaries, current status, case manager, law firm, date of loss, minor status, client contact information, linked lien counts, open/rejected/missing-data liens, financial totals/reductions, notes, servicing, tasks, activity, required documents, and optional Excel-ready sheets
- Case search across client, case number, status, law firm, case manager, case type, accident type, state, and opened date filters
- Task search across assignment, current user, case, lien, status/status group, priority, due date windows, overdue, due today, and high priority
- Servicing search across case, lien, assignee, status/status group, priority, due date windows, and overdue
- Report summaries for opened cases, active cases by case manager or law firm, closed liens, recent cases, and recent liens

SynqLien assistant access is configured from `appsettings` only:

- `XeniaAssistant:SynqLien:BaseUrl`
- `XeniaAssistant:SynqLien:TimeoutSeconds`

Xenia forwards the caller's bearer token to CareConnect and SynqLien for these requests so downstream product and
participant authorization still applies. The assistant registry exposes the CareConnect and SynqLien tools to the
generic tenant agent and to their product-specific agents, while the authoritative tool implementations live behind
each product service's `/api/assistant-tools/*` API surface. SynqLien lien tools accept broad or scoped lien read
permissions, and the Liens service applies seller, buyer, holder, and marketplace visibility before returning records.
Grounded date-only fields are ISO calendar dates; Xenia reproduces them without applying timezone conversion so
assistant answers remain consistent with the tenant portal.
The queue-summary tools are also the KPI surfaces used for questions such as "How many referrals do I have?", "How many
liens are open?", and "How many new liens were created in the last 7 days?"

SynqLien assistant tools accept date presets for natural filters such as `this_week`, `last_month`, `this_month`, and
`life_to_date`, plus `today`, `yesterday`, `last_week`, `last_30_days`, `last_60_days`, and `last_90_days`. The
provider/fake-provider normalizes matching natural-language phrases before tool execution. Uploaded document tools are
metadata-only until a product-owned document/OCR assistant surface is added, so Xenia can list and flag documents but
cannot summarize file contents. Excel export requests return Excel-ready sheet payloads from the case-insights tool;
file generation must be performed by the caller/UI.

---

## Xenia is Independent

- LegalSynq is a **consuming platform**, not a dependency.
- Xenia's core has no LegalSynq domain model imports.
- All platform capabilities are accessed through replaceable adapter interfaces.
- Xenia can be extracted to a separate repository without changes to its core.
