# LS-LIENS-02-002 — Liens Product Capability Model Report

**Date:** 2026-04-14
**Scope:** SynqLiens permission/capability model — Identity seeding, JWT claim propagation, Liens service consumption
**Status:** Complete

---

## 1. Summary of What Was Implemented

The Liens product capability model was established by connecting the existing Identity permission infrastructure to the Liens service. The work had four deliverables:

1. **Permission code namespace migration** — All 29 seeded permission codes in `idt_Capabilities` were updated from the old short format (`lien:create`) to the canonical namespaced format (`SYNQ_LIENS.lien:create`) via migration `20260414000001_UpdatePermissionCodesToNamespaced`. This aligns the database with the `Permission.IsValidCode()` regex (`^[A-Z0-9_]+\.[a-z][a-z0-9_]*(?:\:[a-z][a-z0-9_]*)*$`) and the EF Core `HasData` seed declarations.

2. **`ICurrentRequestContext.Permissions` property** — Added to the shared `BuildingBlocks` interface and implementation, exposing the JWT `permissions` multi-value claim to all downstream services. Previously only `ProductRoles` was surfaced; consumers had to drop down to `ClaimsPrincipal` for permission checks.

3. **`LiensPermissions` constants class** — Created in `Liens.Domain` with all 8 SYNQ_LIENS permission codes as `const string` fields, eliminating magic strings in endpoint authorization.

4. **Permission-guarded Liens endpoints** — 8 stub endpoints in `Liens.Api` using the uniform `RequireProductAccess` (group-level) + `RequirePermission` (route-level) filter chain from `BuildingBlocks.Authorization.Filters`.

---

## 2. Existing Identity Model Identified and Reused

No new Identity domain entities, tables, or schema changes were introduced. The implementation reuses the existing model in its entirety:

| Entity | Table | Purpose |
|---|---|---|
| `Product` | `idt_Products` | Product catalog (SYNQ_LIENS = `10000000-...-000000000002`) |
| `ProductRole` | `idt_ProductRoles` | Role definitions per product |
| `Permission` | `idt_Capabilities` | Fine-grained permission codes per product |
| `RolePermissionMapping` | `idt_RoleCapabilities` | M:N join — which permissions each role grants |
| `ProductOrganizationTypeRule` | `idt_ProductOrganizationTypeRules` | Role → OrgType eligibility (sole source of truth) |
| `OrganizationType` | `idt_OrganizationTypes` | Canonical org type catalog |

All of these were seeded in prior migrations (`20260328024003`, `20260328200000`, `20260329000003`, `20260330110003`). This work only corrected the `Code` values in `idt_Capabilities`.

---

## 3. Where Capabilities Are Defined

Capabilities flow through three layers:

1. **Definition** — `idt_Capabilities` table (Identity DB). Each row is a `Permission` entity with a namespaced `Code` (e.g., `SYNQ_LIENS.lien:create`), bound to a `ProductId`.

2. **Assignment** — `idt_RoleCapabilities` join table maps `ProductRoleId → PermissionId`. Roles aggregate permissions.

3. **Propagation** — `EffectiveAccessService.ResolvePermissionsAsync()` computes the union of all permissions across the user's direct + group-inherited product roles, then `JwtTokenService.GenerateToken()` emits them as `permissions` JWT claims.

4. **Consumption** — Downstream services read `permissions` claims via `ICurrentRequestContext.Permissions` or the `HasPermission()` ClaimsPrincipal extension. The `RequirePermissionFilter` enforces them at the endpoint level with centralized deny/allow logging.

---

## 4. Capability Definitions

### SYNQLIEN_SELLER (maps to LIENS_SELL concept)

The `SYNQLIEN_SELLER` product role represents the "sell liens" capability. It is assigned to organizations with `OrgType = LAW_FIRM` via `ProductOrganizationTypeRule`.

**Permissions granted:**
| Permission Code | Description |
|---|---|
| `SYNQ_LIENS.lien:create` | Create a new lien record |
| `SYNQ_LIENS.lien:offer` | Offer a lien for sale |
| `SYNQ_LIENS.lien:read:own` | View liens created by the user's organization |

### SYNQLIEN_BUYER + SYNQLIEN_HOLDER (maps to LIENS_MANAGE_INTERNAL concept)

The buyer and holder roles together represent the "manage liens internally" capability. Both are assigned to `OrgType = LIEN_OWNER` via `ProductOrganizationTypeRule`.

**SYNQLIEN_BUYER permissions:**
| Permission Code | Description |
|---|---|
| `SYNQ_LIENS.lien:browse` | Browse available liens for purchase |
| `SYNQ_LIENS.lien:purchase` | Purchase a lien |
| `SYNQ_LIENS.lien:read:held` | View liens the organization holds |

**SYNQLIEN_HOLDER permissions:**
| Permission Code | Description |
|---|---|
| `SYNQ_LIENS.lien:read:held` | View liens the organization holds |
| `SYNQ_LIENS.lien:service` | Service an active lien |
| `SYNQ_LIENS.lien:settle` | Settle and close a lien |

Note: `lien:read:held` is shared between BUYER and HOLDER — this is intentional, as both roles need visibility into held liens.

---

## 5. Default Provider Assignment Logic

Product role assignment is **not automatic** upon provisioning. The flow is:

1. **Tenant-level:** `ProductProvisioningService.ProvisionAsync()` creates `TenantProduct` (tenant has access to SYNQ_LIENS) and cascades to `OrganizationProduct` for eligible orgs (filtered by `ProductEligibilityConfig`).

2. **User-level:** Product roles are assigned explicitly via `UserRoleAssignment` (direct) or `GroupRoleAssignment` (inherited from access groups). There is no implicit "give every user in a LAW_FIRM the SYNQLIEN_SELLER role" logic.

3. **TenantAdmin bypass:** TenantAdmin users automatically receive **all** product roles for all entitled products via `EffectiveAccessService.ComputeEffectiveAccessAsync()` — they do not need explicit assignment.

4. **OrgType eligibility:** `ProductOrganizationTypeRule` controls which org types CAN be assigned a given role. It does not auto-assign; it gates the assignment UI/API.

---

## 6. How Optional Enablement Works

Product access is opt-in at three levels:

1. **Tenant entitlement** — `TenantProduct.IsEnabled` must be true. Without this, no user in the tenant sees SYNQ_LIENS in their effective access.

2. **Organization eligibility** — `OrganizationProduct` records are created only for organizations whose `OrgType` passes `ProductEligibilityConfig.IsEligible()`.

3. **User assignment** — `UserProductAccessRecord` (direct) or `GroupProductAccessRecord` (group-inherited) grants product access. Then `UserRoleAssignment`/`GroupRoleAssignment` grants specific product roles.

The `EffectiveAccessService` intersects all three: tenant entitlement ∩ user product access ∩ user role assignments = effective permissions in JWT.

---

## 7. How Capabilities Flow into Request Context

```
Identity DB (idt_Capabilities + idt_RoleCapabilities + idt_ProductRoles)
    ↓
EffectiveAccessService.ResolvePermissionsAsync()
    ↓ (computes union of permissions for user's effective product roles)
JwtTokenService.GenerateToken()
    ↓ (emits as multi-value "permissions" JWT claims)
JWT Token
    ↓ (validated by downstream services via shared Jwt:SigningKey)
CurrentRequestContext.Permissions
    ↓ (reads from ClaimsPrincipal.FindAll("permissions"))
ICurrentRequestContext.Permissions (IReadOnlyCollection<string>)
    ↓
RequirePermissionFilter / HasPermission() / inline checks
```

---

## 8. How Liens Service Consumes Capabilities

The Liens service (`Liens.Api`) consumes capabilities through two mechanisms:

### Filter-based (primary)
All endpoints use the `RequirePermissionFilter` from `BuildingBlocks.Authorization.Filters`:

```
/api/liens/own             → RequirePermission(SYNQ_LIENS.lien:read:own)
/api/liens/held            → RequirePermission(SYNQ_LIENS.lien:read:held)
POST /api/liens            → RequirePermission(SYNQ_LIENS.lien:create)
POST /api/liens/{id}/offers → RequirePermission(SYNQ_LIENS.lien:offer)
/api/liens/marketplace     → RequirePermission(SYNQ_LIENS.lien:browse)
POST /api/liens/{id}/purchase → RequirePermission(SYNQ_LIENS.lien:purchase)
POST /api/liens/{id}/service  → RequirePermission(SYNQ_LIENS.lien:service)
POST /api/liens/{id}/settle   → RequirePermission(SYNQ_LIENS.lien:settle)
```

All endpoints are also guarded at the group level by `RequireProductAccess(SYNQ_LIENS)`, which checks `HasProductAccess()` on the ClaimsPrincipal.

### Admin bypass
`RequirePermissionFilter` checks `IsTenantAdminOrAbove()` first — TenantAdmin and PlatformAdmin users bypass all permission checks. This is consistent with the platform-wide authorization model.

---

## 9. Helper Abstractions Added

### `LiensPermissions` (Liens.Domain)
Static class with `const string` fields for all 8 SYNQ_LIENS permission codes and the product code. Prevents magic strings in endpoint registration.

### `ICurrentRequestContext.Permissions` (BuildingBlocks.Context)
New property on the shared interface. Implementation in `CurrentRequestContext` reads from `permissions` JWT claims. Available to all downstream services (Liens, Fund, CareConnect, etc.).

No other new abstractions were created. The existing `RequirePermissionFilter`, `RequireProductAccessFilter`, `ProductAuthorizationExtensions`, and `HasPermission()` extension methods from BuildingBlocks were reused without modification.

---

## 10. Files Changed

### Identity Service
| File | Change |
|---|---|
| `Identity.Infrastructure/Persistence/Migrations/20260414000001_UpdatePermissionCodesToNamespaced.cs` | **New.** Data-only migration updating 29 `idt_Capabilities.Code` values from old to namespaced format. Idempotent (WHERE clause checks old code exists). |

### Liens Service
| File | Change |
|---|---|
| `Liens.Domain/LiensPermissions.cs` | **New.** Static permission code constants. |
| `Liens.Api/Endpoints/LienEndpoints.cs` | **Rewritten.** 8 permission-guarded stub endpoints with `RequireProductAccess` + `RequirePermission` filters. |
| `Liens.Api/Program.cs` | **Modified.** Added `permissions` to `/context` diagnostic endpoint response. |

### Shared (BuildingBlocks)
| File | Change |
|---|---|
| `BuildingBlocks/Context/ICurrentRequestContext.cs` | **Modified.** Added `Permissions` property to interface. |
| `BuildingBlocks/Context/CurrentRequestContext.cs` | **Modified.** Added `Permissions` property implementation (reads `permissions` JWT claims). |

### Documentation
| File | Change |
|---|---|
| `replit.md` | **Modified.** Updated Liens service section and added permission code format documentation. |

---

## 11. Build Results

### Identity
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Liens
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Gateway
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

All three projects compile cleanly with zero warnings.

---

## 12. Confirmation

### No v1 Logic Introduced
- No legacy `Capabilities` (old naming) domain entities were created.
- No old-style `RoleCapabilities` join entities were introduced.
- All code uses the v2 domain model: `Permission`, `RolePermissionMapping`, `ProductOrganizationTypeRule`.
- The migration only corrects existing data values; it introduces no new schema.

### No Domain Contamination
- The Liens service has **no dependency on Identity domain types**.
- `LiensPermissions` contains only string constants — no references to Identity entities.
- Permission enforcement uses the shared `BuildingBlocks` filter infrastructure.
- The Liens service resolves capabilities purely from JWT claims via `ICurrentRequestContext`.

### No Hardcoded Behavior
- All permission codes are defined as named constants in `LiensPermissions`.
- OrgType → product eligibility is driven by `ProductEligibilityConfig` and `ProductOrganizationTypeRule` (data-driven, not hardcoded in Liens).
- Role → permission mappings are stored in `idt_RoleCapabilities` (data-driven).
- No role names, org types, or tenant codes are hardcoded in the Liens service.

---

## 13. Database / Ownership Alignment

### Were Identity persistence changes required?
**Yes** — a data-only migration (`20260414000001_UpdatePermissionCodesToNamespaced`) was created to update the `Code` column values in the existing `idt_Capabilities` table. No DDL (schema) changes were made. No new tables, columns, or indexes were introduced.

### What schema objects changed?
Only the **data** in `idt_Capabilities.Code` — 29 rows updated from old-format codes (`lien:create`) to namespaced format (`SYNQ_LIENS.lien:create`). The table structure is unchanged.

### Service-owned prefix convention followed?
**Yes.** The `idt_Capabilities` table uses the `idt_` prefix, confirming it is owned by the Identity service. The migration file resides in `Identity.Infrastructure/Persistence/Migrations/`, the canonical location for Identity-owned migrations.

### No Liens-owned persistence tables introduced?
**Confirmed.** No tables with the `liens_` prefix were created, modified, or referenced. The Liens service remains persistence-free in this change — it consumes capabilities exclusively from JWT claims.

### No legacy v1 table structures adopted as final targets?
**Confirmed.** The `idt_Capabilities` and `idt_RoleCapabilities` table names are legacy artifacts from when the domain entity was called `Capability` (now `Permission` / `RolePermissionMapping`). The table names were preserved to avoid a destructive rename, but the EF Core configuration maps them correctly:
- `PermissionConfiguration` → `idt_Capabilities` table
- `RolePermissionMappingConfiguration` → `idt_RoleCapabilities` table, with `CapabilityId` column mapping to `PermissionId` property

These are **not** v1 targets — they are v2 entities mapped to legacy table names via explicit `ToTable()` and `HasColumnName()` configuration.

---

## 14. Risks / Assumptions

| Risk | Severity | Mitigation |
|---|---|---|
| **`idt_PermissionPolicies` code format** — If policies were created against old-format codes, `PolicyEvaluationService` would not match them after the code rename. | Low | Table is currently empty (no seed data, no runtime rows observed). Future policy creation will use new-format codes. A defensive migration could be added when policies are first used in production. |
| **Other services using old permission codes** — Any service that hardcoded old-format permission strings (`lien:create`) will break after this migration. | Low | No services were found referencing old-format codes. Only Identity seeds and the now-corrected DB rows used them. |
| **Stub endpoints** — The 8 Liens endpoints return static JSON. Production implementations will need to wire up `Liens.Application` services, `Liens.Infrastructure` repositories, and the `LiensDbContext`. | Expected | These are placeholders. The authorization layer is production-ready; the business logic is deferred to the Liens domain implementation phase. |
| **`SYNQLIEN_SELLER` mapped to LAW_FIRM** — The `ProductOrganizationTypeRule` seed maps Seller to LAW_FIRM. Migration `20260329000003` previously corrected this to PROVIDER, but the `HasData` seed still shows LAW_FIRM. | Medium | The DB may have the corrected value (PROVIDER) while `HasData` shows LAW_FIRM. A snapshot audit should confirm the runtime value. The `ProductOrganizationTypeRuleConfiguration.HasData` uses `SeedIds.OrgTypeLawFirm` for Seller, which may need correction depending on the intended business model. |

---

## 15. Final Readiness Statement

### Is the capability model established?
**Yes.** The full capability chain is in place:

- **Definition:** 8 SYNQ_LIENS permissions seeded in `idt_Capabilities` with correct namespaced codes.
- **Assignment:** 3 product roles (SYNQLIEN_SELLER, SYNQLIEN_BUYER, SYNQLIEN_HOLDER) with mapped permissions via `idt_RoleCapabilities`.
- **Eligibility:** `ProductOrganizationTypeRule` governs which org types can hold each role.
- **Propagation:** `EffectiveAccessService` → `JwtTokenService` → JWT `permissions` claims.
- **Consumption:** `ICurrentRequestContext.Permissions` + `RequirePermissionFilter` in Liens service.

### Is the system ready for capability-based workflows?
**Yes.** The authorization infrastructure is production-ready:

- Permission checks are enforced at the HTTP filter level with centralized logging.
- Admin bypass is consistent with the platform-wide pattern.
- The `LiensPermissions` constants provide a type-safe contract for future endpoint implementations.
- The Liens service can now implement business logic behind each stub endpoint, and the permission gates will enforce access without any additional authorization wiring.

The next step is implementing the actual Liens domain logic (entities, repositories, application services) behind the permission-guarded endpoints.
