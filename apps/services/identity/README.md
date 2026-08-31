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
| `POST` | `/api/auth/session/refresh` | Rotate a biometric device session refresh token |
| `POST` | `/api/auth/logout` | Stateless web logout or biometric device-session revocation when refresh credentials are supplied |
| `GET` | `/api/auth/device-sessions` | List the authenticated user's active device sessions |
| `POST` | `/api/users` | Create user |
| `GET` | `/api/users` | List users (tenant-scoped) |
| `POST` | `/api/internal/tenant-provisioning/provision` | Internal: full tenant provision |
| `GET` | `/api/internal/users/account-exists` | Internal: trusted product services can check whether an email already belongs to an Identity account |
| `GET` | `/api/internal/users/{userId}/display` | Internal: trusted product services can resolve a tenant-scoped user's first/last display name from `idt_Users`; optional `organizationId` also accepts active org membership in that tenant |
| `GET` | `/api/internal/users/tenant-owner/display` | Internal: trusted product services can resolve the tenant owner's first/last display name from `idt_Tenants.OwnerUserId` and `idt_Users` |
| `POST` | `/api/admin/products/{code}/provision` | Enable/disable product for tenant |
| `POST` | `/api/admin/organizations/synqlien-buyer` | Internal: create/resolve a tenant-scoped `LIEN_OWNER` org for SynqLien public buyer activation |
| `GET` | `/api/admin/organizations?tenantId={tenantId}&orgType=LAW_FIRM` | Internal/admin: list law firm organizations for CareConnect referral portal selection; tenant-scoped requests include global law firms |
| `POST` | `/api/admin/organizations/{id}/self-register` | Internal: CareConnect self-enrollment creates or links an active Identity user; accepts optional user `title` |
| `POST` | `/api/admin/organizations/{id}/synqlien-buyer-self-register` | Internal: create a SynqLien buyer user and grant `SYNQ_LIENS:SYNQLIEN_BUYER`; returns `409 FUNDING_COMPANY_USER_ALREADY_EXISTS` when the funding-company organization already has an active member, or `409 ACCOUNT_ALREADY_EXISTS` for existing emails |
| `GET` | `/api/tenants/current/branding` | Anonymous branding by tenant code |
| `GET`/`POST` | `/api/internal/organizations/{organizationId}/users[/invite\|/{userId}/resend-invite\|/{userId}/activate\|/{userId}/deactivate\|/{userId}/product-roles]` | Internal (provisioning token, not public JWT): list/invite/resend pending invite/activate/deactivate a law-firm organization's users and assign/revoke their `CARECONNECT_REFERRER`/`CARECONNECT_REFERRER_ADMIN` roles (LSV3-1083). Users with pending invitations are listed as `Invited` even though their account is not active yet. Called by CareConnect's `/api/law-firm-users` on behalf of a caller already verified to hold `CARECONNECT_REFERRER_ADMIN` for that org; every route re-derives org membership itself, treating the caller's own ownership check as advisory only. |
| `GET` | `/api/v1/products/SYNQ_LIENS/user-management/users[/{userId}]` | Tenant-scoped SynqLien user directory and details. Requires `SYNQ_LIENS.user:read`; responses include direct/inherited roles, product-access state, invitation state, and `AccessVersion`. |
| `GET`/`POST` | `/api/v1/products/SYNQ_LIENS/user-management/invitations` | List or create product-aware SynqLien invitations. Requires `SYNQ_LIENS.user:invite`. |
| `POST`/`DELETE` | `/api/v1/products/SYNQ_LIENS/user-management/invitations/{invitationId}/resend` or `/invitations/{invitationId}` | Resend or revoke a pending SynqLien invitation. |
| `GET` | `/api/v1/products/SYNQ_LIENS/user-management/roles` | List the fixed assignable SynqLien role catalog. Requires `SYNQ_LIENS.user_role:assign`. |
| `PUT` | `/api/v1/products/SYNQ_LIENS/user-management/users/{userId}/access` | Grant or revoke direct SynqLien-only access. Requires `SYNQ_LIENS.user_access:manage` and an `If-Match` access-version ETag. Revocation also removes direct SynqLien roles but never deactivates the global account; inherited group access remains read-only. |
| `PUT` | `/api/v1/products/SYNQ_LIENS/user-management/users/{userId}/roles` | Atomically replace direct SynqLien roles. Requires `SYNQ_LIENS.user_role:assign` and `If-Match`; inherited group roles remain read-only. |

Through the gateway, prefix these routes with `/identity`. This module manages tenant-scoped
records only (`OrganizationId = null`) and rejects legacy organization-scoped SynqLien access
instead of broadening it. Sensitive operations re-check current Identity state after JWT
permission filtering, so a stale permission claim cannot continue managing users after revocation.

## Database

`IdentityDb` (MySQL) — all tables prefixed `idt_`.

Run `scripts/add-synq-selling-product.sql` against `IdentityDb` to idempotently
add or reactivate the `SYNQ_SELLING` catalog product before enabling it for tenants.

Biometric device sessions are installed by EF migration
`20260810113000_AddBiometricDeviceSessions`; startup does not create these tables manually.

`idt_Users` includes an optional `Title` column (`varchar(50)`) for professional titles captured during
CareConnect portal enrollment and exposed on user DTOs. Existing rows may leave it `NULL`.

CareConnect seeds `CARECONNECT_NETWORK_MANAGER` for law-firm provider network management, including
provider search, map, and provider-management capabilities. `LAW_FIRM` and `LIEN_OWNER` organization
eligibility is seeded for `SYNQ_CARECONNECT` so law-firm-scoped CareConnect users can be provisioned
without tenant-wide user-management access.

`20260821093425_AddCareConnectReferrerAdminRole` seeds `CARECONNECT_REFERRER_ADMIN` — the same
network/provider capabilities as `CARECONNECT_NETWORK_MANAGER`, but with `LAW_FIRM`-only organization
eligibility (no `LIEN_OWNER` row), so a law firm's own admin can be granted network self-management
without also becoming eligible for the lien-company-oriented role (LSV3-1084).
`20260824120000_MigrateCareConnectLawFirmReferrerToAdmin` upgrades active law-firm
`CARECONNECT_REFERRER` assignments to `CARECONNECT_REFERRER_ADMIN` and increments affected users'
`AccessVersion` so stale JWT access claims are rejected/refreshed.
`20260824124500_AddCareConnectReferrerAdminReferralCapabilities` backfills the admin role's
direct referral create/read/cancel and appointment read permissions, then increments affected
admin users' `AccessVersion` so refreshed JWT permission claims include the new capabilities.

`20260728000001_SeedSynqLienSellWorkflowPermission` maps
`SYNQ_LIENS.lien:sell` to `SYNQLIEN_SELLER`. This is the explicit Flow
capability for seller workflow access; it supplements the lien-sale API
permissions seeded by `20260627000002_SeedSynqLienSalePermissions`.

`20260830193454_AddSynqLienUserManagement` adds product-aware invitation grants and seeds
`SYNQLIEN_USER_ADMIN` with `SYNQ_LIENS.user:read`, `SYNQ_LIENS.user:invite`,
`SYNQ_LIENS.user_access:manage`, `SYNQ_LIENS.user_role:assign`, and
`SYNQ_LIENS.user_audit:read`. The public management
API is tenant-scoped and fixes the product to `SYNQ_LIENS`; clients cannot submit a
tenant or product authority. Product access and roles for inactive users are applied
atomically only when the invitation is accepted. The Audit read permission is seeded
for forward compatibility, but no audit-query endpoint is exposed until the Audit
service provides a tenant/product/user-filtered query contract.

## External Integrations

- **AWS Route53** — DNS record management for tenant subdomain provisioning; create/delete waits for Route53 changes to become `INSYNC` before verification continues
- **Notifications service** — transactional email delivery (tenant-registration acceptance before DNS/product provisioning, invite, password reset). Registration acceptance embeds the LegalSynq logo as an inline attachment so it does not depend on the not-yet-provisioned tenant hostname.
- **Tenant service** — dual-write sync for tenant data consistency
- **Documents service** — logo registration after tenant logo upload
- **Audit service** — all identity events published via `LegalSynq.AuditClient`
- **Commerce** — `ICommerceLifecycleNotifier` called on product enable/disable (`Enabled: false` by default)

## Config (`appsettings.json`)

```json
{
  "Jwt": { "Issuer": "legalsynq-identity", "Audience": "legalsynq-platform" },
  "AuditClient": { "BaseUrl": "http://127.0.0.1:5007" },
  "Route53": {
    "BaseDomain": "demo.legalsynq.com",
    "ChangeWaitTimeoutSeconds": 120,
    "ChangeWaitPollSeconds": 5
  },
  "NotificationsService": { "BaseUrl": "", "PortalBaseDomain": "" },
  "TenantService": { "InternalUrl": "http://127.0.0.1:5005" },
  "CommerceIntegration": { "Enabled": false, "BaseUrl": "http://127.0.0.1:5030" }
}
```
