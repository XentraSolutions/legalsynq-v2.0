# Identity Service

Authentication, user management, organisations, roles, product access, and RBAC policy evaluation.

**Port:** 5001

## Responsibilities

- JWT issuance and validation
- User create / invite / activate / deactivate
- Tenant provisioning (canonical create, downstream to Tenant service)
- Organisation and multi-org membership
- Product enablement / disablement per tenant
- Role assignment (system, tenant, product, scoped)
- Access group management (group-inherited role assignments)
- Permission model (tenant permission catalog + effective resolution)
- Policy evaluation engine (attribute-based with Redis or in-memory caching)
- Audit event publication for all identity operations
- Commerce lifecycle notifications on product enable/disable (ECO-02)

## Layer Structure

```
Identity.Api/            Endpoints, middleware, Program.cs (port 5001)
Identity.Application/    Interfaces, DTOs, services (AuthService, UserService)
Identity.Domain/         Tenant, User, Organization, Product, TenantProduct,
                         ProductRole, Permission, RolePermissionAssignment,
                         AccessGroup, GroupMembership, UserProductAccess, ...
Identity.Infrastructure/ DbContext (IdentityDb), repositories, EF migrations,
                         ProductProvisioningService, TenantProvisioningService,
                         JwtTokenService, BcryptPasswordHasher, Route53DnsService
Identity.Api.Tests/      Integration and unit tests
```

## Key Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/auth/login` | Authenticate, returns JWT |
| `GET` | `/api/auth/me` | Validate current session |
| `POST` | `/api/users` | Create user |
| `GET` | `/api/users` | List users (tenant-scoped) |
| `POST` | `/api/internal/tenant-provisioning/provision` | Internal: full tenant provision |
| `POST` | `/api/admin/products/{code}/provision` | Enable/disable product for tenant |
| `GET` | `/api/tenants/current/branding` | Anonymous branding by tenant code |

## Database

`IdentityDb` (MySQL) — all tables prefixed `idt_`.

## External Integrations

- **AWS Route53** — DNS record management for tenant subdomain provisioning
- **Notifications service** — transactional email delivery (invite, password reset)
- **Tenant service** — dual-write sync for tenant data consistency
- **Documents service** — logo registration after tenant logo upload
- **Audit service** — all identity events published via `LegalSynq.AuditClient`
- **Commerce** — `ICommerceLifecycleNotifier` called on product enable/disable (`Enabled: false` by default)

## Config (`appsettings.json`)

```json
{
  "Jwt": { "Issuer": "legalsynq-identity", "Audience": "legalsynq-platform" },
  "AuditClient": { "BaseUrl": "http://127.0.0.1:5007" },
  "NotificationsService": { "BaseUrl": "", "PortalBaseDomain": "" },
  "TenantService": { "InternalUrl": "http://127.0.0.1:5005" },
  "CommerceIntegration": { "Enabled": false, "BaseUrl": "http://127.0.0.1:5030" }
}
```
