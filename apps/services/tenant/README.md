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
| `POST` | `/api/v1/public/tenant-registrations` | Anonymous review-first registration (rate limited) |
| `GET` | `/api/v1/admin/tenant-registrations` | PlatformAdmin application queue |
| `POST` | `/api/v1/admin/tenant-registrations/{id}/approve` | Approve and provision |
| `POST` | `/api/v1/admin/tenant-registrations/{id}/decline` | Decline with a reason |
| `POST` | `/api/v1/admin/tenant-registrations/{id}/provisioning/retry` | Retry incomplete provisioning |
| `PUT` | `/api/internal/sync` | Idempotent upsert from Identity dual-write |
| `GET` | `/api/resolution/{code}` | Resolve tenant by code |
| `GET` | `/api/branding/{tenantId}` | Tenant branding |
| `GET` | `/api/v1/public/tenants/{tenantId}/capabilities/{capabilityKey}` | Public/service-to-service single-capability read (boolean) — used by product services (e.g. CareConnect's Referral Representative Portal flag) to check a tenant feature without AdminOnly credentials |
| `GET` | `/api/internal/tenants/eligible/synqliens` | Service-token-only list of active tenants with a currently effective enabled SynqLiens entitlement. Used by the Liens weekly report scheduler. |

## Database

`TenantDb` (MySQL).

## External Integrations

- **Identity service** — `HttpIdentityProvisioningAdapter` checks `GET /api/internal/users/account-exists` before accepting a registration and calls `POST /api/internal/tenant-provisioning/provision` for canonical tenant create
- **Documents service** — logo registration
- **Commerce** — `ICommerceLifecycleNotifier` wired for `TenantCreated`, `TenantActivated`, `TenantSuspended` events (`Enabled: false` by default)
- **Notifications service** — branded registration-submitted and registration-declined emails through the canonical producer endpoint

Self-registration is disabled by default (`TenantRegistration__Enabled=false`). A
submission creates only a `PendingReview/NotStarted` application. Approval and
provisioning remain separate; DNS failure leaves the application `Approved/Failed`.
Submissions are rejected when the proposed administrator email already belongs
to an Identity account or to another pending registration.
Successful submissions send a pending-review confirmation email. Declines send
the applicant a decision email containing the recorded reason. Notification
delivery is best-effort and does not roll back the registration state. These
emails embed the LegalSynq logo as an inline attachment, so rendering does not
depend on tenant DNS or an externally hosted image.
The checked-in Development environment override enables self-registration locally.

Self-registration is disabled by default (`TenantRegistration__Enabled=false`). A
submission creates only a `PendingReview/NotStarted` application. Approval and
provisioning remain separate; DNS failure leaves the application `Approved/Failed`.
Submissions are rejected when the proposed administrator email already belongs
to an Identity account or to another pending registration.
The checked-in Development environment override enables self-registration locally.

## Config (`appsettings.json`)

```json
{
  "IdentityService": { "InternalUrl": "http://127.0.0.1:5001" },
  "DocumentsService": { "InternalUrl": "http://127.0.0.1:5006" },
  "NotificationsService": { "BaseUrl": "http://127.0.0.1:5008" },
  "ServiceTokens": { "SigningKey": "development-only; use FLOW_SERVICE_TOKEN_SECRET outside local development" },
  "CommerceIntegration": { "Enabled": false, "BaseUrl": "http://127.0.0.1:5030" },
  "ServiceTokens": { "Audience": "tenant-service", "SigningKey": "" },
  "Features": {
    "TenantReadSource": "Tenant",
    "TenantDualWriteEnabled": true
  }
}
```

Tenant registration notification calls require a service JWT. Local development
uses the development-only `ServiceTokens:SigningKey` override; deployed environments
must provide the shared `FLOW_SERVICE_TOKEN_SECRET` to both Tenant and Notifications.

## Notes

- Tenant record is the canonical source of truth. Identity-side tenant records are kept in sync via the dual-write adapter.
- `TenantStatus` enum: `Active`, `Inactive`, `Suspended`. No `Closed` status exists in the current domain model.
- Internal eligible-tenant discovery validates platform service tokens. Non-development deployments must provide the shared `FLOW_SERVICE_TOKEN_SECRET` used by the calling Liens service.
