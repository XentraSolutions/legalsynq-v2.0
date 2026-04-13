# LS-CORE-AUTH-001 — TenantAdmin Product Role Auto-Grant Report

**Date:** 2026-04-13
**Epic:** Core Authorization — Effective Access
**Feature:** TenantAdmin Automatic Product Role & Permission Grant
**Status:** ✅ Complete — Verified end-to-end

---

## 1. Summary

When a user with the **TenantAdmin** system role logs in, the platform now automatically grants them the full scope of all product roles (and their associated permissions) for every product enabled on their tenant — without requiring explicit `UserRoleAssignment` or `GroupRoleAssignment` records.

This eliminates a manual administration step: previously, a TenantAdmin had to be individually assigned to each product role, even though their administrative authority logically implies full product access within their tenant.

---

## 2. Problem Statement

### Symptom
The MANER-LAW TenantAdmin (`maner@xentrasolutions.com`) logged in and received `systemRoles: ["TenantAdmin"]` but `productRoles: []`. Despite having CareConnect, SynqFund, and SynqLiens enabled on the tenant, the TenantAdmin could not access any product-specific features. The frontend product access gates (which check `session.productRoles` for entries like `SYNQ_CARECONNECT:CARECONNECT_RECEIVER`) blocked all product navigation.

### Root Cause
`EffectiveAccessService.ComputeEffectiveAccessAsync` queried the **`TenantProductEntitlements`** table to determine which products are active for a tenant. This table was introduced as a newer entitlement model but was **never populated** for existing tenants. The authoritative source of tenant product enablement is the **`TenantProducts`** table (used by all admin endpoints, the Control Center, and the tenant provisioning flow).

Because `TenantProductEntitlements` returned zero rows, the method short-circuited at line 85 (`if (activeEntitlements.Count == 0)`) and returned an empty result — the TenantAdmin detection logic and auto-grant code were never reached.

### Diagnostic Trail

| Step | Observation |
|---|---|
| Login via BFF | `productRoles: []` in session envelope |
| Login via Identity API directly | `user.productRoles: []` — confirmed issue is backend, not BFF |
| Workflow logs | `"No active entitlements for tenant 956aef48-..."` — `TenantProductEntitlements` empty |
| DB table comparison | `TenantProducts` has 3 enabled products; `TenantProductEntitlements` has 0 rows for MANER-LAW |
| Admin endpoints audit | All use `TenantProducts` (lines 263, 2710, 2865 of `AdminEndpoints.cs`) |

---

## 3. Data Model Context

### Two Tenant-Product Tables

| Table | Schema | Used By | Status |
|---|---|---|---|
| `TenantProducts` | `TenantId (FK)` + `ProductId (FK)` + `IsEnabled` | Admin endpoints, CC panel, provisioning, org product seeder | **Authoritative** — actively populated |
| `TenantProductEntitlements` | `TenantId` + `ProductCode (string)` + `Status (enum)` | `EffectiveAccessService` (before fix) | **Unpopulated** — newer model, no migration/seeder |

### ScopedRoleAssignment Model

TenantAdmin is assigned via `ScopedRoleAssignments` with:
- `ScopeType = "GLOBAL"` (the only valid scope for this table)
- `IsActive = true`
- `Role.Name = "TenantAdmin"` (FK to `Roles` table)

This is the same mechanism used by `AuthService` to resolve `systemRoles` for the JWT (line 103–106 of `AuthService.cs`).

---

## 4. Changes Made

### File: `Identity.Infrastructure/Services/EffectiveAccessService.cs`

**Commit:** `61930828` — _Improve access for TenantAdmins by auto-granting product roles_

#### Change 1: Fix Entitlement Source (Critical Bug Fix)

```diff
- var activeEntitlements = await _db.TenantProductEntitlements
-     .Where(e => e.TenantId == tenantId && e.Status == EntitlementStatus.Active)
-     .Select(e => e.ProductCode)
+ var activeEntitlements = await _db.TenantProducts
+     .Where(tp => tp.TenantId == tenantId && tp.IsEnabled)
+     .Select(tp => tp.Product.Code)
      .ToListAsync(ct);
```

**Why:** `TenantProducts` is the authoritative source. All admin endpoints, the Control Center, and tenant provisioning use this table. `TenantProductEntitlements` was never populated for existing tenants.

#### Change 2: TenantAdmin Detection

```csharp
var isTenantAdmin = await _db.ScopedRoleAssignments
    .AnyAsync(s => s.UserId == userId
        && s.IsActive
        && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global
        && s.Role.Name == "TenantAdmin", ct);
```

**Why:** Detects whether the user holds the TenantAdmin system role. The query joins through the `Role` navigation property to match by name. EF Core translates this to an efficient SQL JOIN + EXISTS.

#### Change 3: Auto-Grant All Entitled Products

```csharp
if (isTenantAdmin)
{
    foreach (var code in activeEntitlements)
    {
        if (effectiveProductSet.Add(code))
            productSources.Add(new EffectiveProductEntry(code, "TenantAdmin"));
    }
}
```

**Why:** TenantAdmin logically has access to all products enabled on their tenant. Products are added with source `"TenantAdmin"` for auditability.

#### Change 4: Auto-Grant All Product Roles (DB-Level Filtered)

```csharp
if (isTenantAdmin)
{
    var entitledProductRoles = await _db.ProductRoles
        .Where(pr => pr.IsActive && pr.Product.IsActive
            && activeEntitlements.Contains(pr.Product.Code))
        .Select(pr => new { pr.Code, ProductCode = pr.Product.Code })
        .ToListAsync(ct);

    foreach (var pr in entitledProductRoles)
    {
        AddRole(pr.Code, pr.ProductCode, "TenantAdmin", null, null);
        tenantAdminRoleCodes.Add(pr.Code);
    }
}
```

**Why:** Queries only product roles for entitled products (SQL-level `WHERE` filter), avoiding unnecessary data transfer. Each role is added via the existing `AddRole` helper which handles deduplication and the `productRoles` dictionary. Role codes are tracked in `tenantAdminRoleCodes` for use in permission resolution.

#### Change 5: Permission Resolution for Auto-Granted Roles

```diff
  var (permissions, permissionSources) = await ResolvePermissionsAsync(
-     tenantId, userId, effectiveProductSet, directRoles, inheritedRoles, activeGroups, ct);
+     tenantId, userId, effectiveProductSet, directRoles, inheritedRoles, activeGroups, tenantAdminRoleCodes, ct);
```

`ResolvePermissionsAsync` signature extended with `HashSet<string> tenantAdminRoleCodes`:

```csharp
var allRoleCodes = directRoles
    .Where(r => r.ProductCode != null)
    .Select(r => r.RoleCode)
    .Concat(inheritedRoles.Where(r => r.ProductCode != null).Select(r => r.RoleCode))
    .Concat(tenantAdminRoleCodes)   // ← NEW
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();
```

And TenantAdmin source attribution:

```csharp
if (tenantAdminRoleCodes.Contains(perm.RoleCode))
{
    if (seenPermissions.Add(permCode + ":TenantAdmin"))
        permissionSources.Add(new EffectivePermissionEntry(
            permCode, perm.ProductCode, "TenantAdmin", perm.RoleCode));
}
```

**Why:** Without this, a TenantAdmin would receive product role claims but zero permission claims — downstream authorization that depends on `permissions` would deny access despite the role auto-grant. The `"TenantAdmin"` source label preserves audit traceability.

---

## 5. Verification Results

### Test 1: TenantAdmin Login (MANER-LAW)

**User:** `maner@xentrasolutions.com` (TenantAdmin, tenant MANER-LAW)
**Endpoint:** `POST http://localhost:5001/api/auth/login`

| Field | Before | After |
|---|---|---|
| `systemRoles` | `["TenantAdmin"]` | `["TenantAdmin"]` |
| `productRoles` | `[]` | 8 roles (see below) |
| Permissions | 0 | 29 |

**Auto-Granted Product Roles:**

| Product | Role Code |
|---|---|
| `SYNQ_CARECONNECT` | `CARECONNECT_RECEIVER` |
| `SYNQ_CARECONNECT` | `CARECONNECT_REFERRER` |
| `SYNQ_FUND` | `SYNQFUND_APPLICANT_PORTAL` |
| `SYNQ_FUND` | `SYNQFUND_FUNDER` |
| `SYNQ_FUND` | `SYNQFUND_REFERRER` |
| `SYNQ_LIENS` | `SYNQLIEN_BUYER` |
| `SYNQ_LIENS` | `SYNQLIEN_HOLDER` |
| `SYNQ_LIENS` | `SYNQLIEN_SELLER` |

### Test 2: BFF Login (End-to-End)

**Endpoint:** `POST http://localhost:5000/api/auth/login` (via dev proxy → Next.js BFF → Gateway → Identity)

Result: Identical `productRoles` array in session envelope. BFF correctly maps `user.productRoles` from the Identity API response.

### Test 3: PlatformAdmin Regression Check

**User:** `admin@legalsynq.com` (PlatformAdmin, tenant LEGALSYNQ)

| Field | Result |
|---|---|
| `systemRoles` | `["PlatformAdmin"]` |
| `productRoles` | `[]` |

**Expected:** PlatformAdmin is not TenantAdmin — auto-grant does not fire. No regression.

### Test 4: Cache Behavior

| Request | Log Output |
|---|---|
| First login | `EffectiveAccess cache MISS ... Products=3, Roles=8` (computed in ~1039ms) |
| Second login | `EffectiveAccess cache HIT ... v0` (served from cache) |

Cache key format `ea:{tenantId}:{userId}:{accessVersion}` ensures cache invalidation on any role/product/group mutation (via `AccessVersion` increment).

### Test 5: Server Logs

```
TenantAdmin auto-grant for user 8b88170b-... in tenant 956aef48-...: 3 products, 8 product roles.
Effective access for user 8b88170b-... in tenant 956aef48-...: 3 products (0 direct, 0 inherited), 8 product roles, 0 tenant roles, 29 permissions.
EffectiveAccess cache MISS for user 8b88170b-... tenant 956aef48-... v0: computed in 1039ms. Products=3, Roles=8.
```

---

## 6. Downstream Impact

### Frontend Product Access Gates
The frontend checks `session.productRoles` for specific entries (e.g., `SYNQ_CARECONNECT:CARECONNECT_RECEIVER`) to gate navigation to product modules. With the auto-grant, TenantAdmins now pass these gates automatically.

### JWT Claims
The auto-granted roles are included in the `product_roles` JWT claim array, so all downstream services (CareConnect, Fund, Liens) that validate product role claims will recognize TenantAdmin access.

### Effective Access Source Attribution
All auto-granted products, roles, and permissions carry `source: "TenantAdmin"` for traceability. This integrates with the existing source attribution model (`"Direct"`, `"Inherited"`) used by the Control Center's access source panel.

---

## 7. Architecture Notes

### Source Priority Order
The `AddRole` helper uses a `seenRoleKeys` set to deduplicate. TenantAdmin auto-granted roles are added **first**, followed by Direct, then Inherited. This means:
- If a TenantAdmin also has explicit Direct or Group-inherited assignments for the same role, the TenantAdmin source takes precedence in the attribution.
- The actual role grant is identical regardless of source — only the attribution label differs.

### Performance
- Product roles query is filtered at the DB level by entitled product codes (`activeEntitlements.Contains(pr.Product.Code)`) — avoids loading all platform-wide product roles.
- The `isTenantAdmin` check uses `AnyAsync` (SQL `EXISTS`) — lightweight single-row check.
- Full computation is cached for 5 minutes with version-aware invalidation.

### Known Limitations
- TenantAdmin detection uses `Role.Name == "TenantAdmin"` string comparison. This is consistent with how `AuthService` resolves system roles (line 105). A future hardening pass could use a stable role identifier or `IsSystemRole` flag.
- `TenantProductEntitlements` table remains unused. A future migration should either populate it from `TenantProducts` or remove it to avoid confusion.

---

## 8. Files Changed

| File | Type | Description |
|---|---|---|
| `apps/services/identity/Identity.Infrastructure/Services/EffectiveAccessService.cs` | Modified | Entitlement source fix, TenantAdmin detection, auto-grant for products/roles/permissions |
| `replit.md` | Modified | Added documentation section for this feature |

---

## 9. Side-Effect Fix — CareConnect Org-Relationship Action Gating

### Problem
The auto-grant gives a Law Firm TenantAdmin both `CARECONNECT_REFERRER` and `CARECONNECT_RECEIVER` roles. The CareConnect UI pages used role-based checks (`isReceiver = session.productRoles.includes(...)`) to decide whether to show action buttons (Accept Referral, Confirm Appointment, etc.). A TenantAdmin with both roles would incorrectly see receiver actions on referrals they sent, and referrer actions on referrals they received.

### Fix Pattern
Changed from pure role-based gating to **role + org-relationship gating**:
- Renamed role checks to `hasReferrerRole` / `hasReceiverRole` (still used for page-level access control)
- Computed per-record flags: `isReferrerOfReferral = hasReferrerRole && session.orgId === referral.referringOrganizationId`
- Passed per-record flags (not role flags) to action components

### Files Changed

| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/careconnect/referrals/[id]/page.tsx` | Detail page: org-relationship check for Accept/Decline/Cancel actions |
| `apps/web/src/app/(platform)/careconnect/appointments/[id]/page.tsx` | Detail page: org-relationship check for Confirm/Complete/NoShow/Reschedule/Cancel actions |
| `apps/web/src/app/(platform)/careconnect/referrals/page.tsx` | List page: passes `orgId` to table component |
| `apps/web/src/components/careconnect/referral-list-table.tsx` | Accepts `orgId`, computes per-row org-relationship flags for quick actions |

### Pages Reviewed but Not Changed
- **Referral list page** (`referrals/page.tsx`): Uses `isReferrer`/`isReceiver` for heading/tab display only — OK for TenantAdmin to see both views
- **Appointment list page** (`appointments/page.tsx`): Uses roles for heading only, no inline action buttons
- **Dashboard** (`dashboard/page.tsx`): Uses roles for layout sections, not action gating
- **Providers pages**: Uses `isReferrer` for access control (correct — only referrers browse providers)

---

## 10. Recommendations for Follow-Up

1. **Populate or deprecate `TenantProductEntitlements`** — The table exists but has no data. Either create a migration to sync it from `TenantProducts`, or drop it to prevent future confusion.
2. **Harden TenantAdmin detection** — Consider matching by a canonical role code/ID + `IsSystemRole` flag rather than display name string, to prevent edge cases if role naming conventions change.
3. **PlatformAdmin product access** — Currently PlatformAdmins receive no auto-granted product roles. If PlatformAdmins need cross-tenant product access for support/debugging, a similar auto-grant mechanism could be added (scoped to the tenant they authenticate against).
4. **Regression tests** — Add automated tests for: (a) TenantAdmin gets full product roles + permissions, (b) non-TenantAdmin unaffected, (c) disabled product not granted, (d) cache invalidation on product toggle.
5. **Extract shared helper** — Consider creating a `getOrgRelationshipFlags(orgId, record)` utility to centralize the org-match logic and prevent drift across CareConnect pages.
