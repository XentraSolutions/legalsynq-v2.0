# LegalSynq — CareConnect Enablement Audit
## Identity Service: Entitlements & Session Role Issuance

**Audit date:** 2026-03-29  
**Scope:** Identity service code — TenantProducts, OrganizationProducts, login/session, product roles, CareConnect seeds, admin entitlement endpoints  
**Auditor:** automated source-code analysis

---

## 1. Executive Summary

**The answer to the headline question is: No. Enabling CareConnect for a tenant today does NOT cause any law-firm user to receive `CARECONNECT_REFERRER` in their session — and it never could, given the current code.**

There are two parallel, disconnected entitlement models in production:

| Model | Table | What reads it |
|---|---|---|
| **A — TenantProducts (legacy)** | `TenantProducts` | Admin UI display only |
| **B — OrganizationProducts (live)** | `OrganizationProducts` | `AuthService.LoginAsync` → JWT |

The admin entitlement endpoint (`POST /api/admin/tenants/{id}/entitlement`) exclusively writes to Model A (`TenantProducts`). Model B (`OrganizationProducts`) is what actually determines whether a product role appears in the JWT. These two tables are completely decoupled; writing to one has zero effect on the other.

Additionally, no customer tenant (e.g. HARTWELL, MERIDIAN) has any `Organization` row, no `OrganizationProduct` row, and no `UserOrganizationMembership` row. There is no admin endpoint to create any of these. Even if the OrganizationProducts plumbing were fixed, a law-firm user would still get no `CARECONNECT_REFERRER` because the prerequisite org graph simply doesn't exist for any customer tenant.

**Three blockers must be resolved in sequence before any customer user can receive `CARECONNECT_REFERRER`:**
1. Customer tenants must have an `Organization` of `OrgType = LAW_FIRM`.
2. That org must have an `OrganizationProduct` row linking it to CareConnect with `IsEnabled = true`.
3. The user must have an active `UserOrganizationMembership` in that org.

---

## 2. Files Inspected

| File | Purpose |
|---|---|
| `Identity.Application/Services/AuthService.cs` | Login + JWT assembly logic |
| `Identity.Infrastructure/Services/JwtTokenService.cs` | JWT claim emission |
| `Identity.Infrastructure/Repositories/UserRepository.cs` | `GetPrimaryOrgMembershipAsync` query |
| `Identity.Infrastructure/Data/IdentityDbContext.cs` | DbSet declarations |
| `Identity.Infrastructure/Data/SeedIds.cs` | All seeded GUIDs |
| `Identity.Infrastructure/Data/Configurations/ProductConfiguration.cs` | Product seed |
| `Identity.Infrastructure/Data/Configurations/ProductRoleConfiguration.cs` | ProductRole seed with `EligibleOrgType` |
| `Identity.Infrastructure/Data/Configurations/OrganizationProductConfiguration.cs` | OrganizationProduct seed (LegalSynq org only) |
| `Identity.Infrastructure/Data/Configurations/UserOrganizationMembershipConfiguration.cs` | Membership config |
| `Identity.Infrastructure/Auth/CapabilityService.cs` | Capability resolution (post-login) |
| `Identity.Domain/ProductRole.cs` | `EligibleOrgType` field |
| `Identity.Domain/OrganizationProduct.cs` | Enable/Disable |
| `Identity.Domain/Organization.cs` | `OrgType` |
| `Identity.Domain/TenantProduct.cs` | Legacy entitlement |
| `Identity.Domain/OrgType.cs` | Valid org type constants |
| `Identity.Domain/MemberRole.cs` | Valid member role constants |
| `Identity.Domain/UserOrganizationMembership.cs` | Membership domain |
| `Identity.Api/Endpoints/AuthEndpoints.cs` | Login + me routes |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Admin entitlement toggle |
| `Identity.Application/DTOs/AuthMeResponse.cs` (inferred from GetCurrentUserAsync) | Session shape |
| `Migrations/20260328024003_InitialIdentitySchema.cs` | Initial schema |
| `Migrations/20260328200000_AddMultiOrgProductRoleModel.cs` | Org/ProductRole/Membership tables |
| `Migrations/20260328200001_SeedAdminOrgMembership.cs` | Admin org seeding |
| `apps/web/src/app/(platform)/careconnect/**` | Frontend role checks |

---

## 3. Current Entitlement Flow

### 3a. Model A — TenantProducts (Legacy, Orphaned)

```
Admin UI: POST /api/admin/tenants/{id}/entitlement
  → reads body.ProductCode, resolves Product
  → reads/writes TenantProducts table (TenantId × ProductId × IsEnabled)
  → returns { enabled, status, enabledAtUtc }
```

**Who reads TenantProducts at runtime?**
- `AdminEndpoints.cs` — for listing a tenant's entitlements in the control center UI (`GET /api/admin/tenants/{id}`)
- **Nobody else.** `AuthService`, `JwtTokenService`, `UserRepository` — none of them touch `TenantProducts`.

### 3b. Model B — OrganizationProducts (Active, Live)

```
OrganizationProduct {
  OrganizationId (FK → Organizations)
  ProductId      (FK → Products)
  IsEnabled
  EnabledAtUtc
  GrantedByUserId
}
```

**Seeded data:** Only `OrgLegalSynq` (the internal LegalSynq platform org) has OrganizationProducts rows — all five products enabled. No customer tenant has any `Organization` row and therefore no `OrganizationProduct` rows.

### 3c. Decision Gap

There is no synchronization, trigger, or event between the two models. Toggling `TenantProducts.IsEnabled` via the admin panel has **no effect** on `OrganizationProducts`.

---

## 4. Current Session Role Issuance Flow

The complete call chain for a user login:

```
POST /api/auth/login  { email, password, tenantCode }
│
├─ AuthService.LoginAsync()
│   ├─ TenantRepository.GetByCodeAsync(tenantCode)      → validates tenant is active
│   ├─ UserRepository.GetByTenantAndEmailAsync()        → validates user is active
│   ├─ PasswordHasher.Verify()                          → validates password
│   ├─ UserRepository.GetByIdWithRolesAsync()           → loads UserRoles → Role.Name list
│   │                                                      (these become JWT ClaimTypes.Role)
│   │
│   ├─ UserRepository.GetPrimaryOrgMembershipAsync()    ← KEY QUERY (see below)
│   │   SELECT * FROM UserOrganizationMemberships
│   │   WHERE UserId = @uid AND IsActive = 1
│   │   ORDER BY JoinedAtUtc ASC
│   │   LIMIT 1
│   │   INCLUDE Organization
│   │       .OrganizationProducts
│   │           .Product
│   │               .ProductRoles
│   │
│   └─ productRoles = org.OrganizationProducts
│         .Where(op => op.IsEnabled)
│         .SelectMany(op => op.Product.ProductRoles)
│         .Where(pr => pr.IsActive
│                   && (pr.EligibleOrgType is null
│                       || pr.EligibleOrgType == org.OrgType))
│         .Select(pr => pr.Code)
│         .Distinct().OrderBy(c => c)
│
└─ JwtTokenService.GenerateToken()
    → Claims: sub, email, jti, tenant_id, tenant_code
    → ClaimTypes.Role (one per system role name)
    → "org_id", "org_type"   (if org is not null)
    → "product_roles"        (one claim per product role code)
```

### Conditions for `CARECONNECT_REFERRER` to appear in JWT

All four conditions must be true simultaneously:

| # | Condition | Current state for HARTWELL |
|---|---|---|
| 1 | User has an active `UserOrganizationMembership` | **Missing** — no rows |
| 2 | That org has `OrganizationProduct` row for CareConnect with `IsEnabled = true` | **Missing** — no org |
| 3 | Org's `OrgType == "LAW_FIRM"` | **Not applicable** — no org |
| 4 | `ProductRole` `CARECONNECT_REFERRER` is seeded and `IsActive = true` | ✅ Seeded in `ProductRoleConfiguration` |

---

## 5. CareConnect-Specific Findings

### 5a. ProductRole seed is correct

`ProductRoleConfiguration.cs` seeds:
```
CARECONNECT_REFERRER  →  ProductId = SYNQ_CARECONNECT  →  EligibleOrgType = "LAW_FIRM"
CARECONNECT_RECEIVER  →  ProductId = SYNQ_CARECONNECT  →  EligibleOrgType = "PROVIDER"
```
Both are `IsActive = true`. The role codes match what the frontend checks:
- `apps/web` uses `ProductRole.CareConnectReferrer` = `"CARECONNECT_REFERRER"` consistently across all CareConnect pages.

### 5b. The EligibleOrgType filter is correct but never exercised

`AuthService` correctly filters by `pr.EligibleOrgType == org.OrgType`. A LAW_FIRM org with CareConnect enabled would correctly yield `CARECONNECT_REFERRER` (and not `CARECONNECT_RECEIVER`). The logic is sound; the data simply doesn't exist.

### 5c. Admin panel entitlement toggle is misleading

When an admin enables CareConnect for HARTWELL via the control center, the row in `TenantProducts` flips to `IsEnabled = true`. The UI shows status "Active". From a user's perspective this looks like the product is enabled. In reality, **no HARTWELL user can access CareConnect** because the login path ignores `TenantProducts` entirely.

### 5d. GetPrimaryOrgMembership picks oldest — not most privileged

If a user ever has multiple active memberships (possible in future multi-org scenarios), the query picks the one with the smallest `JoinedAtUtc`. This is an arbitrary selection that could produce the wrong org type and thus wrong product roles.

### 5e. OrgName is always null in the session

`AuthService.GetCurrentUserAsync` (the `/api/auth/me` path) hard-codes `OrgName = null`:
```csharp
OrgName: null,  // Phase 2: DB lookup by orgId for DisplayName ?? Name
```
The frontend receives `orgName: null` on every session refresh. This is a placeholder that was never completed.

### 5f. 'OWNER' MemberRole is invalid

`SeedAdminOrgMembership.cs` inserts `MemberRole = 'OWNER'` via raw SQL. The `MemberRole` domain class only accepts `ADMIN`, `MEMBER`, `READ_ONLY`. The migration bypasses the domain guard (raw SQL), leaving an inconsistent value in the DB. When application code tries to read and re-validate this value (if any future code calls `MemberRole.IsValid`), it would return `false`.

---

## 6. Gaps / Blockers

| Priority | Gap | Impact |
|---|---|---|
| **P0** | Admin entitlement endpoint writes `TenantProducts` but login reads `OrganizationProducts` — models are completely disconnected | Every admin entitlement action is a no-op for actual access |
| **P0** | No `Organization` rows exist for any customer tenant | No customer user can receive any product role |
| **P0** | No `UserOrganizationMembership` rows for any customer user | `GetPrimaryOrgMembershipAsync` always returns null for customer users |
| **P0** | No admin endpoint to create Organizations, assign OrganizationProducts, or link users to orgs | No operational path to fix the above without direct DB writes |
| **P1** | `GetPrimaryOrgMembership` picks oldest membership arbitrarily | Wrong product roles possible in multi-org future |
| **P1** | `OrgName` always null in `/api/auth/me` response | Frontend cannot display org name |
| **P2** | `MemberRole = 'OWNER'` seeded for admin — not a valid domain value | Silent inconsistency; could fail future validation code |
| **P2** | `TenantProducts` table is populated but serves no runtime purpose | Misleading data; wastes admin effort |

---

## 7. Recommended Target Model

### System of Record Decision

> **`OrganizationProducts` is the system of record for product entitlements.**  
> `TenantProducts` should be retired or repurposed as a coarse-grained "tenant-level kill switch" (optional) but must never be the primary entitlement gate, since access is fundamentally org-scoped.

The rationale: Login and JWT issuance already use `OrganizationProducts`. Changing this would require re-architecting the session model. The admin UI is easy to redirect.

### Target Entitlement Model

```
Tenant
 └── Organization(s)  [OrgType: LAW_FIRM | PROVIDER | FUNDER | ...]
      └── OrganizationProducts  [IsEnabled per product]
           └── ProductRoles     [issued to user at login, filtered by OrgType]
               └── Capabilities [resolved at authorization time by CapabilityService]

User
 └── UserOrganizationMemberships (one primary, possibly multiple)
      └── resolves to: which org → which products → which roles
```

### Lifecycle

1. Admin creates a tenant (e.g. HARTWELL).
2. Admin creates an Organization for that tenant (`OrgType = LAW_FIRM`).
3. Admin enables CareConnect on that org (`OrganizationProduct.IsEnabled = true`).
4. When a HARTWELL user is created, they are added to that org (`UserOrganizationMembership`).
5. On login, the user automatically receives `CARECONNECT_REFERRER` in their JWT.

---

## 8. Concrete Implementation Tasks

### T1 — Fix the admin entitlement endpoint (P0)
**File:** `Identity.Api/Endpoints/AdminEndpoints.cs` — `UpdateEntitlement` handler  
**Change:** Instead of writing to `TenantProducts`, find the tenant's primary org (or all orgs) and write to `OrganizationProducts`. If no org exists yet, return a 409 with a message requiring org creation first.  
**Fallback option:** Keep `TenantProducts` as a display-only record; additionally propagate the change to `OrganizationProducts` on the primary org.

### T2 — Add admin endpoints for Organization management (P0)
**File:** New `OrganizationEndpoints.cs` in `Identity.Api/Endpoints/`  
Needed routes:
- `POST /api/admin/tenants/{tenantId}/organizations` — create org (name, orgType)
- `GET  /api/admin/tenants/{tenantId}/organizations` — list orgs
- `POST /api/admin/organizations/{orgId}/products`   — enable/disable a product on an org
- `POST /api/admin/organizations/{orgId}/members`    — add a user to an org
- `DELETE /api/admin/organizations/{orgId}/members/{userId}` — remove

### T3 — Seed Organizations and Memberships for existing tenants (P0)
**File:** New EF Core migration  
For each existing customer tenant (HARTWELL, MERIDIAN, etc.):
1. Insert `Organization` row with appropriate `OrgType`.
2. Insert `OrganizationProduct` rows for currently-enabled products (read from `TenantProducts` as the migration source of truth for which products were "intended" to be on).
3. Insert `UserOrganizationMembership` for all users in each tenant.

### T4 — Wire GetPrimaryOrgMembership to prefer most recently granted (P1)
**File:** `Identity.Infrastructure/Repositories/UserRepository.cs`  
**Change:** Change `OrderBy(m => m.JoinedAtUtc)` to `OrderByDescending(m => m.JoinedAtUtc)` or, better, add a concept of a `IsPrimary` flag on `UserOrganizationMembership`.

### T5 — Populate OrgName in GetCurrentUserAsync (P1)
**File:** `Identity.Application/Services/AuthService.cs` — `GetCurrentUserAsync`  
**Change:** Add `org_name` JWT claim in `JwtTokenService.GenerateToken` (sourced from `Organization.DisplayName ?? Organization.Name`). Then read it from the claims principal in `GetCurrentUserAsync` instead of doing a DB round-trip.

### T6 — Fix OWNER MemberRole in seed migration (P2)
**File:** `Migrations/20260328200001_SeedAdminOrgMembership.cs`  
**Change:** Replace `'OWNER'` with `'ADMIN'` and write a corrective data migration.

### T7 — Retire or clarify TenantProducts (P2)
**File:** `AdminEndpoints.cs`, `Identity.Domain/TenantProduct.cs`  
**Options:**
- Option A: Delete `TenantProducts` entirely; replace with a view or computed field over `OrganizationProducts`.
- Option B: Keep `TenantProducts` as a fast "tenant-level product catalog" (what products can a tenant subscribe to) and `OrganizationProducts` as the org-scoped activation. Document the distinction.

---

## 9. Risks / Assumptions

| Risk | Severity | Mitigation |
|---|---|---|
| Existing customer users have no org membership — all product roles are missing today | High | T3 seed migration must run before any CareConnect rollout |
| Admin entitlement toggle gives false confidence (looks like it works) | High | T1 fix must land before the next customer onboarding |
| Migration T3 must infer `OrgType` for existing tenants from context — this may require a manual data decision for each tenant | Medium | Create a mapping table or admin script; don't automate blindly |
| Changing `GetPrimaryOrgMembership` ordering (T4) could change roles for any user with multiple memberships | Medium | Audit all `UserOrganizationMemberships` before T4 lands |
| `CapabilityService` cache (5-min TTL) means role changes aren't immediate post-fix | Low | Acceptable; document and extend to `IDistributedCache` for multi-instance |
| `OrgName` always null — affects control center admin display | Low | Fix in T5; no security impact |
| `OWNER` MemberRole data is in the live DB — no domain enforcement breaks today | Low | Corrective migration + domain validator tightening |

---

## Decision

> **Single system of record: `OrganizationProducts`.**  
> The `TenantProducts` table is deprecated for access-control purposes effective immediately. It may be retained as a display-only catalog (to show what products a tenant has "subscribed to" at the tenant level) but must never gate actual access. All entitlement writes from the admin panel must target `OrganizationProducts` on a per-org basis. All new product-role checks must derive from org membership.

---

## Patch Plan

Files most likely to require changes, in recommended order:

| Order | File | Change type |
|---|---|---|
| 1 | `Identity.Infrastructure/Persistence/Migrations/` — new migration | Seed `Organizations` + `OrganizationProducts` + `UserOrganizationMemberships` for all existing customer tenants |
| 2 | `Identity.Infrastructure/Persistence/Migrations/` — corrective migration | Fix `MemberRole = 'OWNER'` → `'ADMIN'` for admin seed |
| 3 | `Identity.Api/Endpoints/AdminEndpoints.cs` | Redirect `UpdateEntitlement` to write `OrganizationProducts` instead of `TenantProducts` |
| 4 | `Identity.Api/Endpoints/OrganizationEndpoints.cs` — **new file** | CRUD for Organizations, OrganizationProducts, and UserOrganizationMemberships |
| 5 | `Identity.Api/Program.cs` | Register `MapOrganizationEndpoints()` |
| 6 | `Identity.Infrastructure/Services/JwtTokenService.cs` | Add `"org_name"` claim |
| 7 | `Identity.Application/Services/AuthService.cs` | Read `"org_name"` claim in `GetCurrentUserAsync` |
| 8 | `Identity.Infrastructure/Repositories/UserRepository.cs` | Fix `GetPrimaryOrgMembership` ordering / add `IsPrimary` |
| 9 | `apps/control-center/src/lib/control-center-api.ts` | Add org management API calls to match new endpoints |
| 10 | `apps/control-center/src/app/(admin)/tenants/[id]/` | Update tenant detail to show org-level entitlements, not tenant-level |
