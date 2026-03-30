# LegalSynq Phase I — Identity Finalization and Relationship Enforcement

## 1. Current State

Before Phase I the platform had the following characteristics:

- **OrganizationTypeId** was the authoritative FK but still *nullable* — existing rows created before Phase H could have a null FK with only the legacy `OrgType` string set.
- **OrgType string** existed in two roles: the column value (authoritative write source) and the JWT claim value (derived from that column). Phase H already shifted JWT derivation to `OrgTypeMapper.TryResolveCode(OrganizationTypeId) ?? OrgType`, but did not backfill the column.
- **ScopedRoleAssignments** supported five scope types (GLOBAL, TENANT, ORGANIZATION, PRODUCT, RELATIONSHIP) but the API only wrote and read GLOBAL scope assignments; no real non-global scope check was exercised at runtime.
- **CareConnect relationship integrity** was operationally correct — `HttpOrganizationRelationshipResolver` was the registered implementation and appointment creation already copied `OrganizationRelationshipId` from the referral — but no integrity inspection endpoint existed.
- **Platform-readiness** endpoint existed but did not break down SRA counts by scope type.

---

## 2. Final Risks Being Addressed

| Risk | Severity | Addressed By |
|------|----------|--------------|
| Existing orgs with null `OrganizationTypeId` (pre-Phase H rows) | High | Migration 200005 backfill |
| OrgType string drift on `AssignOrganizationType` (caller passes wrong code) | Medium | Catalog-consistency guard in `AssignOrganizationType` |
| Product-role eligibility silently falling back to OrgType string | Medium | Warning log in `AuthService.LoginAsync` |
| No API path to create non-GLOBAL SRAs | High | Extended `AssignRole` endpoint |
| No runtime visibility into non-global scope assignments | Medium | `GET /api/admin/users/{id}/scoped-roles` endpoint |
| No CareConnect integrity inspection | Medium | `GET /api/admin/integrity` endpoint in CareConnect |
| `AssignOrganizationType` could accept a mismatch between ID and code | Medium | Catalog-priority override in domain method |

---

## 3. Identity Finalization Changes

### 3a. EF Data Migration — `20260330200005_PhaseI_BackfillOrganizationTypeId`

**File:** `apps/services/identity/Identity.Infrastructure/Persistence/Migrations/20260330200005_PhaseI_BackfillOrganizationTypeId.cs`

Runs five `UPDATE Organizations SET OrganizationTypeId = <catalog-guid> WHERE OrgType = '<code>' AND OrganizationTypeId IS NULL` statements — one per recognized OrgType code. After this migration runs, every active organization row will have a non-null `OrganizationTypeId`. No schema changes.

**Down** is intentionally a no-op (backfill cannot be safely reversed without risk of nulling IDs set by other operations).

### 3b. `Organization.Create(tenantId, name, organizationTypeId, ...)` overload

**File:** `apps/services/identity/Identity.Domain/Organization.cs`

Added a new `Create` overload that accepts `Guid organizationTypeId` as the primary argument, derives `orgType` from `OrgTypeMapper.TryResolveCode(organizationTypeId)`, and delegates to the canonical string-based overload. This makes `OrganizationTypeId` the *write authority* for callers that already hold the catalog ID.

### 3c. `AssignOrganizationType` consistency guard

When the catalog resolves a code for the supplied `organizationTypeId`, that catalog-derived code is now *always preferred* over any caller-supplied `orgTypeCode`. This means `Organization.OrgType` and `Organization.OrganizationTypeId` can never drift out of sync via this method.

### 3d. `AuthService.LoginAsync` — backfill warning

Before computing product-role eligibility, if `org.OrganizationTypeId` is null, a `LogWarning` is emitted instructing the operator to run migration 200005. After the migration executes this path should never trigger.

### 3e. `IScopedAuthorizationService` + `ScopedAuthorizationService`

**Files:**
- `apps/services/identity/Identity.Application/Interfaces/IScopedAuthorizationService.cs`
- `apps/services/identity/Identity.Application/DTOs/ScopedRoleSummaryResponse.cs`
- `apps/services/identity/Identity.Infrastructure/Services/ScopedAuthorizationService.cs`

Three-method interface:
- `HasOrganizationRoleAsync(userId, roleName, orgId)` — checks GLOBAL or ORGANIZATION-scoped SRA.
- `HasProductRoleAsync(userId, roleName, productId)` — checks GLOBAL or PRODUCT-scoped SRA.
- `GetScopedRoleSummaryAsync(userId)` — returns all active SRAs ordered by scope type.

The EF implementation queries `ScopedRoleAssignments` directly (includes `Role` nav property). Registered as `IScoped` in `Identity.Infrastructure/DependencyInjection.cs`.

---

## 4. Relationship Integrity Changes

### 4a. CareConnect Integrity Report Endpoint

**File:** `apps/services/careconnect/CareConnect.Api/Endpoints/CareConnectIntegrityEndpoints.cs`

New endpoint: `GET /api/admin/integrity`

Returns four operational counters without ever throwing (any individual query failure returns `-1` for that counter):

| Counter | Description |
|---------|-------------|
| `referrals.withOrgPairButNullRelationship` | Referrals where both `ReferringOrganizationId` and `ReceivingOrganizationId` are set but `OrganizationRelationshipId` is null — indicates either a missing Identity relationship record or a pre-Phase H referral |
| `appointments.missingRelationshipWhereReferralHasOne` | Appointments whose linked Referral has an `OrganizationRelationshipId` but the Appointment itself does not — indicates legacy appointments created before relationship resolution was active |
| `providers.withoutOrganizationId` | Active providers not yet linked to an Identity Organization |
| `facilities.withoutOrganizationId` | Active facilities not yet linked to an Identity Organization |

A top-level `clean: true` field is set only when all four counters equal zero.

### 4b. Existing relationship enforcement confirmed active

The CareConnect `DependencyInjection.cs` already registers `HttpOrganizationRelationshipResolver` as the default `IOrganizationRelationshipResolver`. The `ReferralService.CreateAsync` already resolves and warns on missing relationships. The `AppointmentService.CreateAppointmentAsync` already copies `OrganizationRelationshipId` from the referral. No changes required to these paths.

---

## 5. Scoped Authorization Activation

### 5a. `AssignRole` endpoint extended for non-GLOBAL scope

**File:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`

`POST /api/admin/users/{id}/roles` now accepts:

```json
{
  "roleId":                    "guid",
  "scopeType":                 "GLOBAL|ORGANIZATION|PRODUCT|RELATIONSHIP|TENANT",
  "organizationId":            "guid?",
  "productId":                 "guid?",
  "organizationRelationshipId": "guid?"
}
```

- `scopeType` defaults to `GLOBAL` when omitted (fully backward compatible).
- `ORGANIZATION` scope requires `organizationId` and validates the org exists.
- `PRODUCT` scope requires `productId` and validates the product exists.
- `RELATIONSHIP` scope requires `organizationRelationshipId` and validates it is active.
- Conflict check is scope-aware: same user + same role + same scope type + same scope context ID combination.
- Response includes `assignmentId`, `scopeType`, and the applicable scope context ID.

### 5b. `GET /api/admin/users/{id}/scoped-roles` — new endpoint

Returns all active SRAs for a user, with:
- flat `assignments[]` array (each entry includes `scopeType`, `roleName`, and the applicable scope context ID)
- `byScope` object grouping counts by scope type key

This is the first real runtime query of non-global scope data at the API layer.

### 5c. `scopedAssignmentsByScope` in platform-readiness

`GET /api/admin/platform-readiness` now returns an additional section:

```json
"scopedAssignmentsByScope": {
  "global": 5,
  "organization": 0,
  "product": 0,
  "relationship": 0,
  "tenant": 0
}
```

Non-zero `organization`, `product`, or `relationship` values confirm that real non-global scope enforcement is exercised in the current environment.

**What scope-aware behavior is truly active after Phase I:**
1. **Write path**: `AssignRole` can create ORGANIZATION, PRODUCT, RELATIONSHIP, and TENANT-scoped SRAs via the admin API.
2. **Read path**: `GetScopedRoles` queries and surfaces non-global SRAs per user.
3. **Check path**: `IScopedAuthorizationService.HasOrganizationRoleAsync` and `HasProductRoleAsync` perform real DB-backed scope checks (ready for use in any protected endpoint).
4. **Observability**: platform-readiness exposes per-scope-type assignment counts.

---

## 6. Files Changed

### New Files
| File | Purpose |
|------|---------|
| `Identity.Infrastructure/Persistence/Migrations/20260330200005_PhaseI_BackfillOrganizationTypeId.cs` | Data migration: backfill OrganizationTypeId from OrgType string |
| `Identity.Application/Interfaces/IScopedAuthorizationService.cs` | Interface for scope-aware role checks |
| `Identity.Application/DTOs/ScopedRoleSummaryResponse.cs` | DTO: ScopedRoleEntry + ScopedRoleSummaryResponse |
| `Identity.Infrastructure/Services/ScopedAuthorizationService.cs` | EF-backed implementation of IScopedAuthorizationService |
| `CareConnect.Api/Endpoints/CareConnectIntegrityEndpoints.cs` | Integrity report endpoint for CareConnect |

### Modified Files
| File | Change |
|------|--------|
| `Identity.Domain/Organization.cs` | Added `Create(typeId)` overload; strengthened `AssignOrganizationType` consistency guard |
| `Identity.Application/Services/AuthService.cs` | Added backfill warning log when `org.OrganizationTypeId` is null |
| `Identity.Infrastructure/DependencyInjection.cs` | Registered `IScopedAuthorizationService → ScopedAuthorizationService` |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Extended `AssignRole` for non-global scopes; added `GetScopedRoles` handler; added `scopedAssignmentsByScope` to readiness; added route registration |
| `CareConnect.Api/Program.cs` | Registered `MapCareConnectIntegrityEndpoints()` |
| `control-center/src/types/control-center.ts` | Added `ScopedAssignmentsByScope` interface; extended `PlatformReadinessSummary` |
| `control-center/src/lib/api-mappers.ts` | Extended `mapPlatformReadiness` to map `scopedAssignmentsByScope` |

---

## 7. Schema / Migration Notes

### Migration 200005: `PhaseI_BackfillOrganizationTypeId`
- **Type**: Data-only (no schema DDL changes)
- **Operation**: Five `UPDATE` statements targeting `Organizations` rows where `OrganizationTypeId IS NULL`
- **Reversibility**: Intentionally non-reversible (Down is a no-op)
- **Safety**: Idempotent — rows with `OrganizationTypeId` already set are untouched
- **After migration**: `orgsWithMissingTypeId` in platform-readiness will be 0; `orgTypeConsistent` will be true

### OrgType column retention
The `OrgType` string column is retained for this phase. It is:
- Kept in sync by `AssignOrganizationType` (catalog code always wins)
- Auto-populated by `Organization.Create()` in all code paths
- Used only as a backward-compat fallback in JWT emission (TryResolveCode goes first)

Dropping the `OrgType` column is deferred to a future phase once all consumers have confirmed they only read `org_type_id` and `org_type` from JWT claims rather than the DB column.

---

## 8. Build and Verification Status

### Backend builds

| Project | Result | Notes |
|---------|--------|-------|
| `Identity.Api` | ✅ Build succeeded — 0 errors, 0 warnings | |
| `Identity.Domain` | ✅ Build succeeded | |
| `Identity.Infrastructure` | ✅ Build succeeded | |
| `CareConnect.Api` | ✅ Build succeeded — 0 errors | Pre-existing `CS0168` warning in `ExceptionHandlingMiddleware.cs` (unrelated to Phase I) |

### TypeScript builds

| Project | Result |
|---------|--------|
| `apps/control-center` | ✅ `tsc --noEmit` — 0 errors |

### Runtime verification

| Check | Result |
|-------|--------|
| `GET /health` (Identity, port 5001) | ✅ `{"status":"ok","service":"identity"}` |
| `GET /health` (CareConnect, port 5003) | ✅ `{"status":"healthy"}` |
| Control Center web UI (port 5004) | ✅ Serving — login page renders |
| Application workflow | ✅ Running |

---

## 9. Remaining Optional Future Enhancements

The following items are intentionally deferred. They do not compromise the architecture correctness achieved in Phase I.

| Item | Notes |
|------|-------|
| Drop the `OrgType` string column | Safe to do once all JWT consumers have migrated to `org_type_id`. Requires confirming no external service reads the DB column directly. |
| Enforce `OrganizationTypeId NOT NULL` at the DB level | Add a `NOT NULL` constraint to `Organizations.OrganizationTypeId` after migration 200005 confirms all rows are populated. |
| CareConnect integrity counter repair tools | Admin actions to backfill `OrganizationRelationshipId` on historical referrals/appointments; script to link providers/facilities to Identity orgs. |
| JWT org-scoped roles claim | Emit org-scoped role assignments as a structured JWT claim (e.g. `org_roles[orgId][]`) to avoid DB lookup on every request for org-scoped checks. |
| Subdomain tenancy | Not in scope for Phase I. |
| New products (SynqBill, SynqRx, etc.) | Not in scope for Phase I. |
| Full RELATIONSHIP-scoped enforcement in CareConnect | Activate `HasOrganizationRoleAsync` or a relationship-scoped check inside `ReferralService.CreateAsync` to enforce that the requesting user actually belongs to the referring organization. |
| Enforce `NOT NULL` on `Referral.OrganizationRelationshipId` in enforcement mode | Configurable via `IdentityService:EnforceRelationshipOnReferral` appsetting — currently defaults to warn-only. |
