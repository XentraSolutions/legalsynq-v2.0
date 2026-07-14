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

## API Endpoints

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
stuck behind stale `bin/...` copies.

## Assistant Product Grounding

The first grounded assistant integration is CareConnect referral lookup. When the user opens Xenia from a
`/careconnect/referrals/{id}` route, Xenia resolves the referral id from the current page context, performs a
read-only server-side referral lookup against CareConnect, and injects a sanitized summary plus recent status history
into the assistant prompt. Assistant replies cite the current referral record when grounding succeeds.

CareConnect grounding is configured from `appsettings` only:

- `XeniaAssistant:CareConnect:BaseUrl`
- `XeniaAssistant:CareConnect:TimeoutSeconds`
- `XeniaAssistant:CareConnect:MaxHistoryItems`

Xenia forwards the caller's bearer token to CareConnect for this lookup so downstream product and participant
authorization still applies.

---

## Xenia is Independent

- LegalSynq is a **consuming platform**, not a dependency.
- Xenia's core has no LegalSynq domain model imports.
- All platform capabilities are accessed through replaceable adapter interfaces.
- Xenia can be extracted to a separate repository without changes to its core.
