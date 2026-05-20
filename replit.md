# LegalSynq — Agent Context File

This file is the agent's persistent memory. It contains architecture, conventions, and key patterns — not ticket history. For human documentation see `README.md` and per-service `README.md` files.

## Runtime Environment

- **.NET SDK:** 10.0 (Nix `dotnet-sdk_10`)
- **Node.js:** 22 (Nix `nodejs-22`)
- **Nix channel:** stable-25_05
- **Start command:** `bash scripts/run-dev.sh`
- **Dev proxy:** port 5000 (gates browser until Next.js warm on 3050), Next.js internal port 3050

## Service Port Map

| Service | Port |
|---|---|
| Tenant Portal (Next.js) | 5000 (proxy) / 3050 (internal) |
| Identity | 5001 |
| Liens | 5002 |
| CareConnect | 5003 |
| Control Center (Next.js) | 5004 |
| Tenant | 5005 |
| Documents | 5006 |
| Audit | 5007 |
| Fund | 5008 |
| Gateway (YARP) | 5010 |
| Flow (backend) | 5015 |
| Task | 5016 |
| Monitoring | 5020 |
| Notifications | 5025 |
| Reports | 5029 |
| Commerce (planned) | 5030 |

## Architecture Rules

- **Gateway** validates JWTs on all routes except health, info, public branding, and document access. Downstream services validate independently.
- **BFF pattern**: both Next.js apps are BFFs. Server route handlers read `platform_session` cookie, exchange it for a Bearer token, proxy to gateway. Client code uses relative `/api/` URLs.
- **Tenant context**: `X-Tenant-Code` header resolved by gateway from subdomain or explicit header. Services read `CurrentTenantId` via `BuildingBlocks.Context.ICurrentRequestContext`.
- **No shared databases or EF contexts** between services.
- **GUID generation:** Always use `Guid.CreateVersion7()` (not `Guid.NewGuid()`) for all primary keys and new IDs. UUIDv7 is time-ordered, prevents B-tree index fragmentation, and allows sorting by ID as a chronological proxy. `Guid.CreateVersion7()` requires .NET 9+; this repo targets net10.0.
- **All server-side localhost fallbacks use `127.0.0.1`** — Node.js resolves `localhost` to `::1` (IPv6) first, but .NET services bind to `0.0.0.0` (IPv4 only).
- **`node_modules`** installed at monorepo root. Both `apps/web` and `apps/control-center` inherit via Node resolution traversal. Control Center must NOT have a local `node_modules` — duplicate React causes `useReducer` null errors.

## Service Layer Pattern (all .NET services)

```
Service.Api/            Endpoints, middleware, Program.cs
Service.Application/    Interfaces, DTOs, business services
Service.Domain/         Entities, enums, value objects
Service.Infrastructure/ DbContext, repositories, EF migrations, external adapters
```

## Shared Libraries

### `shared/contracts` — zero external dependencies
- `Contracts/Audit/` — `AuditEventDto`, `AuditQueryRequest`
- `Contracts/Commerce/` — `CommerceEventTypes` (34 constants), `CommerceLifecycleEvent`
- `Contracts/Notifications/` — `NotificationTemplateKeys`, `NotificationTemplateRegistry`
- `HealthResponse`, `ServiceResponse<T>`

### `shared/building-blocks` — references contracts
- `BuildingBlocks.Authentication` — JWT helpers, `ServiceTokenIssuer`, delegating handler
- `BuildingBlocks.Authorization` — `Policies`, `Roles`, `PermissionService`, policy pipeline
- `BuildingBlocks.Context` — `ICurrentRequestContext` — reads tenant/user/correlation from JWT claims
- `BuildingBlocks.Commerce` — `ICommerceLifecycleNotifier`, `NoopCommerceLifecycleNotifier`, `AddCommerceIntegration()` DI helper
- `BuildingBlocks.Exceptions` — `NotFoundException`, `ConflictException`, `ForbiddenException`, `ValidationException`
- `BuildingBlocks.Notifications` — `INotificationsEmailClient`, `INotificationsCacheClient`

### `shared/audit-client` — `LegalSynq.AuditClient`
- `IAuditEventClient.IngestAsync(IngestAuditEventRequest)`
- `AddAuditEventClient(configuration)` — reads `AuditClient:BaseUrl` + `AuditClient:ServiceToken`
- All audit calls should be non-blocking: wrap in `TryAuditAsync` pattern

## Commerce Integration Pattern (ECO-01 / ECO-02)

```csharp
// DI setup
services.AddCommerceIntegration(configuration);
// appsettings.json
// "CommerceIntegration": { "Enabled": false, "BaseUrl": "http://127.0.0.1:5030", "HostPlatformKey": "legalsynq" }
// Usage — always after primary operation succeeds
await TryNotifyCommerceAsync(() => _commerceNotifier.NotifyAsync(new CommerceLifecycleEvent(...), ct));
```

- `Enabled: false` → `NoopCommerceLifecycleNotifier` (returns `Task.CompletedTask`)
- Call sites: `TenantAdminService` (create, status update), `TenantService` (create, provision, deactivate), `ProductProvisioningService` (enable/disable)
- Event types: `commerce.tenant.created`, `commerce.tenant.activated`, `commerce.tenant.suspended`, `commerce.product.enabled`, `commerce.product.disabled`
- `HostPlatformKey`: always `"legalsynq"`

## Database

- **Provider:** MySQL 8 (AWS RDS us-east-2)
- **Auto-migration:** All services run `Database.Migrate()` or `MigrateOnStartup=true` on startup
- **Connection string env vars:** `ConnectionStrings__IdentityDb`, `ConnectionStrings__DocsDb`, `ConnectionStrings__FlowDb`, `ConnectionStrings__LiensDb`, `ConnectionStrings__MonitoringDb`, `ConnectionStrings__ReportsDb`, `ConnectionStrings__Support`, `ConnectionStrings__TasksDb`, `ConnectionStrings__TenantDb`, `ConnectionStrings__AuditEventDb`
- **Notifications** uses individual env vars: `NOTIF_DB_HOST`, `NOTIF_DB_PORT`, `NOTIF_DB_NAME`, `NOTIF_DB_USER`, `NOTIF_DB_PASSWORD`
- **Table prefix convention:** `idt_` (Identity), `rpt_` (Reports), etc.
- **Migration commands:**
  ```bash
  dotnet ef migrations add <Name> --project <Infra.csproj> --startup-project <Api.csproj>
  dotnet ef database update --project <Infra.csproj> --startup-project <Api.csproj>
  ```

## JWT

- **Issuer:** `legalsynq-identity`
- **Audience:** `legalsynq-platform`
- **Dev signing key:** `dev-only-signing-key-minimum-32-chars-long!` (appsettings.Development.json only)
- **Claims:** `sub` (userId), `email`, `jti`, `tenant_id`, `tenant_code`, `ClaimTypes.Role`, `permissions` (comma-separated)
- **`MapInboundClaims = false`** on all services — claim names are literal
- **Session cookie:** `platform_session` (HttpOnly) set by BFF login routes

## Products & Roles

| Product Code | Product Roles |
|---|---|
| `CARECONNECT` | `CARECONNECT_REFERRER`, `CARECONNECT_RECEIVER` |
| `SYNQFUND` | `SYNQFUND_REFERRER`, `SYNQFUND_FUNDER` |
| `SYNQLIEN` | `SYNQLIEN_SELLER`, `SYNQLIEN_BUYER`, `SYNQLIEN_HOLDER` |

System roles: `PlatformAdmin`, `TenantAdmin`, `StandardUser`

## Permission Code Format

`PRODUCT_CODE.domain:action` — e.g., `SYNQ_LIENS.lien:create`  
Enforced by regex in `Permission.cs`. All permission codes updated in migration `20260414000001_UpdatePermissionCodesToNamespaced`.

## Product Role Resolution (Identity)

`IProductRoleResolutionService.ResolveAsync(userId, tenantId)` → `EffectiveAccessContext`  
Flow: enabled products → org memberships with product/role graph → eligibility gate → `IProductRoleMapper` per product → roles in JWT.  
`CareConnectRoleMapper`: ScopedRoleAssignment → DB OrgType rules → OrgType fallback (PROVIDER→RECEIVER, LAW_FIRM→REFERRER).

## OrgType → Product Eligibility

`ProductEligibilityConfig` (Identity.Domain):
- `LAW_FIRM` → `[CARECONNECT, SYNQFUND, SYNQLIEN]`
- `PROVIDER` → `[CARECONNECT]`
- `FUNDER` → `[SYNQFUND]`
- `LIEN_OWNER` → `[SYNQLIEN]`
- `INTERNAL` → all products

## Tenant Provisioning Flow

Canonical path: `Tenant.Api POST /api/admin/tenants` → `TenantAdminService.CreateTenantAsync` → saves to TenantDb → calls `HttpIdentityProvisioningAdapter` → Identity `POST /api/internal/tenant-provisioning/provision` → Identity creates tenant + orgs + users → emits Commerce events.

Alternate/legacy path: Identity `POST /api/admin/tenants` → `TenantService.CreateAsync` → dual-write to `Tenant.Api PUT /api/internal/sync`.

## Key Secrets

| Secret | Used by |
|---|---|
| `Jwt__SigningKey` | All services — JWT validation |
| `FLOW_SERVICE_TOKEN_SECRET` | Flow service — machine-to-machine auth |
| `IdentityService__ProvisioningToken` | Tenant → Identity provisioning calls |
| `TenantService__ProvisioningSecret` | Cross-service tenant sync |
| `PublicTrustBoundary__InternalRequestSecret` | Internal-only endpoint guard |
| `AWS_S3_*` | Documents service — S3 storage |
| `SENDGRID_API_KEY`, `SENDGRID_FROM_EMAIL`, `SENDGRID_FROM_NAME` | Notifications — email |
| `NOTIF_DB_PASSWORD` | Notifications — database |
| `NEXT_PUBLIC_GOOGLE_MAPS_KEY` | Frontend — maps |

## Frontend: apps/web

- **Next.js 15.2.9**, App Router, TypeScript, Tailwind CSS, React 18
- **Auth guard hierarchy:** `requireAuthenticated` → `requireOrg` → `requireProductRole` → `requireAdmin`
- **BFF routes:** `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/logout`, `POST /api/auth/forgot-password`, `POST /api/auth/reset-password`
- **Rewrite:** `/api/*` → gateway at `GATEWAY_URL=http://127.0.0.1:5010`
- **Service layers:** `lib/{cases,liens,servicing,documents,notifications,reports,provider-mode,unified-activity}/` — each has `types.ts`, `api.ts`, `mapper.ts`, `service.ts`, `index.ts`
- **Role-access:** `lib/role-access/` — `buildRoleAccess()` → `RoleAccessInfo` with `can(LienAction)`, `canViewModule(LienModule)`. Replaces legacy `canPerformAction()` in lien-store.
- **Bulk ops:** `lib/bulk-operations/` — `executeBulk(ids, handler)` → `BulkOperationResult`
- **Provider mode:** `lib/provider-mode/` — org config API, sell vs manage mode, backed by DB claim
- **Unified activity:** `lib/unified-activity/` — merges audit + notification events; resilient (partial results on failure)
- **Hook pattern:** `useSession()`, `useRoleAccess()`, `useProviderMode()`, `useSelectionState<T>()`
- **env.local:** `NEXT_PUBLIC_ENV=development`, `NEXT_PUBLIC_TENANT_CODE=LEGALSYNQ`, `GATEWAY_URL=http://127.0.0.1:5010`

## Frontend: apps/control-center

- **Next.js 15.2.9**, App Router, TypeScript, Tailwind CSS v4, React 18
- **Auth:** `requirePlatformAdmin()` — redirects to `/login` if no valid `PlatformAdmin` session
- **API fallback rewrite strategy:** local route handlers checked first; unmatched `/api/*` falls to `CONTROL_CENTER_API_BASE=http://127.0.0.1:5010`
- **Key lib files:** `env.ts`, `session.ts`, `auth-guards.ts`, `control-center-api.ts`
- **Must NOT have local `node_modules`** — use root monorepo `node_modules` only

## Notifications Service Governance (SMS-019 through SMS-025)

Notification delivery gated by per-tenant governance rule packs. Five enforcement engines (Email, Push, Webhook, SMS, Federation). Governance rules can be versioned, approved via workflow, deployed via canary rollout to tenant segments. Per-tenant scoping ensures complete isolation. Fail-open on runtime error (`FailOpenOnRuntimeError=true` default). Governance decisions persisted for audit.

## Reports Service Key Facts

- Template lifecycle: Draft → Published (published versions are immutable)
- Assignment scope: Global or tenant-targeted
- Override: Tenants can customize without modifying global template (one active override per tenant per template)
- Effective resolution: `GET /api/v1/tenant-templates/{id}/effective?tenantId=`
- Execution guardrails: 500-row cap, 10MB export cap
- Query adapter: currently mock — replace with real SQL/API adapters per product for production
- Background worker: `ScheduleWorkerService` polls every 60s, max 10 schedules/cycle
- Audit: 26 factory methods in `AuditEventFactory`; all calls via `TryAuditAsync`

## Audit Service Key Facts

- Hash chain: SHA-256 or HMAC-SHA256 per `(TenantId, SourceSystem)` chain
- Ingest auth modes: `None` (dev only), `ServiceToken` (production)
- `IngestAuth__ServiceTokens` — one token per source service
- Retention engine wired but `JobEnabled=false`, `DryRun=true` by default
- Event forwarding abstraction present but `Enabled=false` by default

## Known Build Constraint

All projects (services and shared libraries) target `net10.0`. The Replit environment runs .NET SDK 8.0.412 and cannot build this repo (NETSDK1045). Local development requires the .NET 10 SDK. This is a pre-existing environment constraint unrelated to any individual change.

## Completed Major Work Areas

- Platform Foundation (auth, RBAC, multi-tenancy, EF migrations)
- Identity domain (users, orgs, products, groups, permissions, scoped assignments, policy engine)
- CareConnect (providers, referrals, appointments, activation funnel, analytics)
- SynqFund (application lifecycle, review/approve/deny)
- SynqLien (lien CRUD, marketplace, offers, purchase, portfolio, cases, servicing, documents, tasks)
- Flow (workflow stages, task management, task templates, auto-generation rules, task notes, transition engine, governance)
- Documents (S3 upload, virus scanning, versioning, opaque access tokens, public logos)
- Audit (hash chain ingestion, query, export, integrity checkpoints, retention, anomaly detection, correlation)
- Notifications (multi-channel delivery, template management, governance rules 019-025, approval workflow, canary rollout, federation layer)
- Reports (templates, versioning, assignments, overrides, execution engine, export CSV/XLSX/PDF, scheduling, formatting layer)
- Monitoring (health probes, uptime, alerts, latency tracking)
- Tenant service (canonical registry, branding, dual-write, domain management)
- Control Center (dashboard, tenant mgmt, user mgmt, audit investigation, monitoring, notifications, reports)
- Commerce ECO-01: `ICommerceLifecycleNotifier`, noop implementation, `AddCommerceIntegration` DI helper, event type constants, notification template keys
- Commerce ECO-02: Commerce notifications wired into TenantAdminService, TenantService, ProductProvisioningService

## User Preferences

- Prefer `127.0.0.1` over `localhost` in all server-side code
- Keep shared library changes additive — never break service independence
- Always call Commerce notify after primary DB operation succeeds, wrapped in non-throwing helper
- Analysis reports go in `analysis/` with naming `LS-{TICKET}-report.md`
