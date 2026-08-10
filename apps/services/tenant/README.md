# Tenant Service

Canonical tenant registry — the authoritative source for tenant records, branding, domains, entitlements, capabilities, and settings.

**Port:** 5005

## Responsibilities

- Canonical tenant create (Tenant-first; calls Identity provisioning adapter downstream)
- Tenant status management (Active / Inactive / Suspended)
- Tenant branding (logo, colours, brand name)
- Custom domain management
- Product entitlement records (Tenant-owned; synced to Identity)
- Capability and settings management
- Read-source for tenant resolution and branding (replaces Identity read-through in production)
- Commerce lifecycle notifications on tenant create/activate/suspend (ECO-02)

## Layer Structure

```
Tenant.Api/            Endpoints, middleware, Program.cs (port 5005)
Tenant.Application/    Interfaces, DTOs, services
                         TenantAdminService — create, status update, entitlement toggle
                         TenantService — provision, create, deactivate, sync upsert
                         BrandingService, DomainService, EntitlementService,
                         CapabilityService, SettingService, ResolutionService
Tenant.Domain/         Tenant, TenantStatus, TenantProductEntitlement,
                         TenantDomain, TenantCapability, TenantSetting, TenantBranding
Tenant.Infrastructure/ DbContext (TenantDb), repositories, EF migrations,
                         HttpIdentityProvisioningAdapter, HttpIdentityCompatAdapter,
                         HttpDocumentsAdapter, NoOpTenantSyncAdapter
```

## Key Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/admin/tenants` | Create tenant (admin; canonical path) |
| `PATCH` | `/api/admin/tenants/{id}/status` | Update tenant status |
| `GET` | `/api/admin/tenants` | List tenants (paged; includes Identity-backed `type`, primary contact, and full `url`) |
| `GET` | `/api/admin/tenants/{id}` | Tenant admin detail (includes Identity-backed `type`, primary contact, and full `url`) |
| `POST` | `/api/tenants/provision` | Minimal provision (internal) |
| `PUT` | `/api/internal/sync` | Idempotent upsert from Identity dual-write |
| `GET` | `/api/resolution/{code}` | Resolve tenant by code |
| `GET` | `/api/branding/{tenantId}` | Tenant branding |

## Database

`TenantDb` (MySQL).

## External Integrations

- **Identity service** — `HttpIdentityProvisioningAdapter` calls `POST /api/internal/tenant-provisioning/provision` for canonical tenant create
- **Documents service** — logo registration
- **Commerce** — `ICommerceLifecycleNotifier` wired for `TenantCreated`, `TenantActivated`, `TenantSuspended` events (`Enabled: false` by default)

## Config (`appsettings.json`)

```json
{
  "IdentityService": { "InternalUrl": "http://127.0.0.1:5001" },
  "DocumentsService": { "InternalUrl": "http://127.0.0.1:5006" },
  "CommerceIntegration": { "Enabled": false, "BaseUrl": "http://127.0.0.1:5030" },
  "Features": {
    "TenantReadSource": "Tenant",
    "TenantDualWriteEnabled": true
  }
}
```

## Notes

- Tenant record is the canonical source of truth. Identity-side tenant records are kept in sync via the dual-write adapter.
- `TenantStatus` enum: `Active`, `Inactive`, `Suspended`. No `Closed` status exists in the current domain model.
