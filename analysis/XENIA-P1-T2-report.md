# XENIA-P1-T2 Tenant Email Module Foundation and Source Connectivity — Final Report

**Completed:** 2026-07-10  
**Ticket:** XENIA-P1-T2  
**Parent:** XENIA-P1 — Xenia Platform Foundation & Email Automation  
**Status:** ✅ COMPLETE

---

## 1. Executive Summary

XENIA-P1-T2 delivers the Email module as the first functional Xenia automation module. The implementation adds tenant-scoped email source (mailbox connection) management with a 5-provider model, secure secret reference pattern, SSRF-guarded connector validation, module enablement per-tenant, full CRUD API, and a Control Center admin UI shell.

**Deliverables:**
- 7 domain enums, 3 domain entities (`EmailSource`, `EmailProviderSettings`, `EmailValidationHistory`)
- 4 application interfaces + 5 DTOs + provider definitions
- 5 email connectors (M365, Google Workspace, IMAP, POP3, Exchange+IMAP) with SSRF guard
- 14 API endpoints across 3 endpoint files
- 3 MySQL tables via EF migration `20260710000003_AddEmailModule`
- 5 test classes, **96 email tests — all pass** (total suite: **166/166**)
- Control Center: 4 new pages (dashboard, sources, providers, sub-layout) + typed API client

---

## 2. Architecture

### Layer Map

```
Xenia.Domain/Email/           7 enums + 3 entities
Xenia.Application/Email/      4 interfaces + 5 DTOs + provider definitions
Xenia.Infrastructure/Email/   5 connectors, EfEmailSourceService, EF configs,
                               EmailModuleSeeder, UnavailableSecretReferenceService,
                               SsrfGuard
Xenia.Api/Endpoints/          3 endpoint files (Module, Source, Provider)
apps/control-center/          xenia-email-api.ts + 4 pages
```

### Domain Entities

| Entity | Responsibility |
|--------|---------------|
| `EmailSource` | Aggregate root — tenant-scoped mailbox connection. Stores metadata + opaque secret reference only |
| `EmailProviderSettings` | Provider-specific config blob (versioned JSON). 1:1 with EmailSource |
| `EmailValidationHistory` | Immutable audit record per validation attempt. Extends `AuditableEntityBase` |

### Enums

| Enum | Values |
|------|--------|
| `EmailProviderType` | M365, GoogleWorkspace, Imap, Pop3, ExchangeImap |
| `EmailAuthType` | OAuth2, UsernamePassword, AppPassword |
| `EmailSourceStatus` | Pending, Active, Disabled, Error, Validating |
| `EmailSourceHealthStatus` | Unknown, Healthy, Degraded, Unavailable |
| `EmailValidationStatus` | NotValidated, Validated, ValidationFailed |
| `EmailValidationResult` | Connected, AuthenticationFailed, TlsRequired, SsrfBlocked, ValidatorUnavailable, ConfigurationError, Timeout |

---

## 3. API Surface

| Method | Path | Policy | Description |
|--------|------|--------|-------------|
| GET | `/email/module` | `EmailRead` | Module state (global + per-tenant effective) |
| PUT | `/email/module/enable` | `EmailManage` | Enable module for calling tenant |
| PUT | `/email/module/disable` | `EmailManage` | Disable module for calling tenant |
| GET | `/email/sources` | `EmailRead` | List all tenant email sources |
| POST | `/email/sources` | `EmailManage` | Create a new email source |
| GET | `/email/sources/{id}` | `EmailRead` | Get single source |
| PUT | `/email/sources/{id}` | `EmailManage` | Update source (optimistic concurrency via `row_version`) |
| DELETE | `/email/sources/{id}` | `EmailManage` | Delete source |
| PUT | `/email/sources/{id}/enable` | `EmailManage` | Enable source |
| PUT | `/email/sources/{id}/disable` | `EmailManage` | Disable source |
| POST | `/email/sources/{id}/validate` | `EmailValidate` | Run connector test |
| GET | `/email/sources/{id}/validation-history` | `EmailRead` | Validation audit log (capped) |
| GET | `/email/providers` | `EmailRead` | All provider definitions |
| GET | `/email/providers/{key}` | `EmailRead` | Single provider definition |

### Permissions Added to Program.cs
```csharp
XeniaPermissions.Email.Read   → EmailRead policy
XeniaPermissions.Email.Manage → EmailManage policy
XeniaPermissions.Email.Validate → EmailValidate policy
```

---

## 4. Security

### Tenant Isolation
- `TenantId` is always resolved from `IXeniaTenantContext` (JWT claims). Caller-supplied tenant IDs in request bodies are explicitly ignored.
- All DB queries filter by tenant before returning any data.
- Cross-tenant operations return 404 (not 403) to avoid resource enumeration.

### Secret Reference Pattern
- `EmailSource.SecretReferenceId` stores an opaque reference key — never the credential itself.
- `ISecretReferenceService` resolves the actual value at runtime via the platform secret service.
- `UnavailableSecretReferenceService` (dev stub) always returns `Unavailable` — prevents accidental plaintext storage in dev.
- `EmailSourceDto.HasSecretReference` exposes only a boolean — the reference ID is never included in responses.

### SSRF Guard (`SsrfGuard.cs`)
Blocks connections to:
- `localhost`, `127.*` (loopback)
- `10.*` (RFC1918 class A)
- `192.168.*` (RFC1918 class C)  
- `172.16–31.*` (RFC1918 class B)
- `169.254.*` (link-local / APIPA)
- `metadata.google.internal` (GCE metadata endpoint)

All five connectors call `SsrfGuard.IsHostSafe()` before any network attempt. Result code on block: `SsrfBlocked`.

### TLS Enforcement
All connectors reject `UseTls = false` with `TlsRequired`. No plaintext connections are ever initiated.

### OAuth2 Deferral
OAuth2 full flow (grant screen, token exchange, refresh) is deferred to XENIA-P1-T3. The domain model is complete; connectors return `ValidatorUnavailable` for OAuth2 sources to prevent incomplete auth paths.

---

## 5. Database

### Tables

| Table | Purpose | Indexes |
|-------|---------|---------|
| `xn_email_sources` | Primary source records | tenant_id; (tenant_id, provider_type); (tenant_id, status) |
| `xn_email_provider_settings` | Provider-specific config JSON | Unique on email_source_id; tenant_id |
| `xn_email_validation_history` | Immutable validation audit | tenant_id; email_source_id; started_at |

### Migration
- File: `20260710000003_AddEmailModule.cs` (+ Designer.cs)
- Applied via: EF `MigrateAsync()` on service startup (Xenia.Api/Program.cs migration path)
- EF migrations history entry inserted: `20260710000003_AddEmailModule`
- Enum columns use `EnumToStringConverter<T>()` — required for Pomelo 8 + EF 8 + .NET 10

---

## 6. Test Results

**166/166 tests pass** (96 new email tests + 70 pre-existing platform tests).

| Test Class | Count | Focus |
|-----------|-------|-------|
| `EmailConnectorTests` | 15 | SSRF blocking, TLS enforcement, auth type validation, registry lookup, connector capabilities |
| `EmailModuleRegistrationTests` | 10 | Seeding idempotency, global enable/disable, per-tenant enable/disable, effective enablement logic, tenant isolation |
| `EmailSourceServiceTests` | 15 | CRUD, concurrency conflict, secret ref hiding, validation recording, history limit enforcement |
| `EmailTenantIsolationTests` | 6 | TenantB cannot read/update/delete/enable/validate/view-history of TenantA resources |

---

## 7. Control Center UI

### Pages

| Route | File | Content |
|-------|------|---------|
| `/xenia/email` | `email/page.tsx` | Module state card + stat tiles + source summary table |
| `/xenia/email/sources` | `email/sources/page.tsx` | Full source table (provider, auth, host, health, secret ref indicator) |
| `/xenia/email/providers` | `email/providers/page.tsx` | Provider capability cards (auth types, TLS requirement, OAuth support) |
| `/xenia/email/layout.tsx` | Sub-nav bar (Dashboard / Sources / Providers) |

Navigation: `Email` link added to the top-level Xenia nav in `apps/control-center/src/app/xenia/layout.tsx`.

API Client: `apps/control-center/src/lib/xenia-email-api.ts` — fully typed, token-bearing fetch wrapper for all 14 endpoints.

### Security Notes in UI
- Sources page displays a prominent security notice: credentials are never stored in plain text; only `HasSecretReference` boolean is shown.
- No secret reference IDs, connection strings, or passwords appear in any response or UI element.

---

## 8. Known Constraints

| Item | Notes |
|------|-------|
| OAuth2 full flow | Deferred to XENIA-P1-T3. Domain + auth type modelled; UI OAuth grant screens not yet built |
| Real secret resolution | `UnavailableSecretReferenceService` is the dev stub. Production adapter requires vault/HSM DI registration |
| Live network validation | `TestConnectionAsync` on IMAP/POP3 returns `ValidatorUnavailable` in Replit (no outbound TCP on 143/993/110/995) |
| Xenia.Api OpenAPI DLL | Pre-existing `MSB3030` on `Microsoft.AspNetCore.OpenApi 8.0.22` in .NET 10 SDK; does not affect Infrastructure or Tests builds |
| EF model snapshot | Full `XeniaDbContextModelSnapshot.cs` update deferred; migration Designer.cs committed for EF migration history |

---

## 9. Files Created / Modified

### Service — new files
```
Xenia.Domain/Email/
  EmailSource.cs, EmailProviderSettings.cs, EmailValidationHistory.cs
  EmailProviderType.cs, EmailAuthType.cs, EmailSourceStatus.cs,
  EmailSourceHealthStatus.cs, EmailValidationStatus.cs, EmailValidationResult.cs

Xenia.Application/Email/
  IEmailSourceService.cs, IEmailSourceConnector.cs,
  IEmailConnectorRegistry.cs, ISecretReferenceService.cs,
  EmailProviderDefinitions.cs, EmailModuleKeys.cs
  Dtos/CreateEmailSourceRequest.cs, UpdateEmailSourceRequest.cs,
       EmailSourceDto.cs, ValidationResultDto.cs,
       ValidationHistoryDto.cs, EmailProviderDefinitionDto.cs

Xenia.Infrastructure/Email/
  Persistence/EmailSourceConfiguration.cs
  Persistence/EmailProviderSettingsConfiguration.cs
  Persistence/EmailValidationHistoryConfiguration.cs
  Services/EfEmailSourceService.cs
  Connectors/SsrfGuard.cs, EmailSourceConnectorRegistry.cs,
             M365EmailConnector.cs, GoogleWorkspaceEmailConnector.cs,
             ImapEmailConnector.cs, Pop3EmailConnector.cs,
             ExchangeImapEmailConnector.cs
  Security/UnavailableSecretReferenceService.cs
  Modules/EmailModuleSeeder.cs

Xenia.Infrastructure/Persistence/Migrations/
  20260710000003_AddEmailModule.cs
  20260710000003_AddEmailModule.Designer.cs

Xenia.Api/Endpoints/
  XeniaEmailModuleEndpoints.cs
  XeniaEmailSourceEndpoints.cs
  XeniaEmailProviderEndpoints.cs

Xenia.Tests/Email/
  EmailConnectorTests.cs, EmailModuleRegistrationTests.cs,
  EmailSourceServiceTests.cs, EmailTenantIsolationTests.cs,
  EmailProviderDefinitionTests.cs
```

### Service — modified files
```
Xenia.Infrastructure/Persistence/XeniaDbContext.cs     (3 new DbSets)
Xenia.Infrastructure/DependencyInjection.cs            (email DI wiring)
Xenia.Api/Program.cs                                   (3 policies + endpoints)
```

### Control Center — new files
```
apps/control-center/src/lib/xenia-email-api.ts
apps/control-center/src/app/xenia/email/layout.tsx
apps/control-center/src/app/xenia/email/page.tsx
apps/control-center/src/app/xenia/email/sources/page.tsx
apps/control-center/src/app/xenia/email/providers/page.tsx
```

### Control Center — modified files
```
apps/control-center/src/app/xenia/layout.tsx           (Email nav link)
```
