# LS-LIENS-02-002-01 — Liens Capability Abstraction Layer Report

**Date:** 2026-04-14
**Scope:** Lightweight business capability abstraction over the existing SYNQ_LIENS permission model
**Status:** Complete
**Depends on:** LS-LIENS-02-002 (permission model, permission codes, endpoint guards)

---

## 1. Summary of What Was Implemented

A thin capability abstraction layer was added to `Liens.Domain` that maps the existing fine-grained permission codes to stable, business-facing capability identifiers. This allows future Liens workflows, UI gating, and business logic branching to reason in terms of **what the user can do** (`Sell`, `ManageInternal`) rather than enumerating raw permission strings.

Three files were added:

| File | Purpose |
|---|---|
| `LiensCapabilities.cs` | Stable string constants for business capabilities |
| `LiensCapabilityResolver.cs` | Centralized permission → capability derivation logic |
| `LiensCapabilityExtensions.cs` | Extension methods on `ICurrentRequestContext` for clean consumption |

The existing permission model, endpoint guards, and JWT claim flow are **completely untouched**. Capabilities sit above permissions; they do not replace them.

---

## 2. Existing Permission Model Reused

The capability layer builds entirely on the v2 permission model established in LS-LIENS-02-002:

- **Permission codes** — `LiensPermissions` constants (e.g., `SYNQ_LIENS.lien:create`)
- **JWT propagation** — `permissions` multi-value claim emitted by `JwtTokenService`
- **Request context** — `ICurrentRequestContext.Permissions` (added in LS-LIENS-02-002)
- **Endpoint enforcement** — `RequirePermissionFilter` + `RequireProductAccessFilter`

No new security primitives, claim types, or authorization filters were introduced.

---

## 3. Capability Definitions

### `LIENS_SELL`

Represents the ability to participate as a **seller** in the liens marketplace. A user has this capability when they hold all three seller-side permissions.

### `LIENS_MANAGE_INTERNAL`

Represents the ability to **manage liens internally** — viewing held liens, servicing active liens, and settling liens. A user has this capability when they hold all three holder/buyer-side management permissions.

---

## 4. Exact Permission-to-Capability Mapping

### `LIENS_SELL` requires ALL of:

| Permission Code | Rationale |
|---|---|
| `SYNQ_LIENS.lien:create` | Can create new lien records |
| `SYNQ_LIENS.lien:offer` | Can offer liens for sale on marketplace |
| `SYNQ_LIENS.lien:read:own` | Can view own organization's liens |

### `LIENS_MANAGE_INTERNAL` requires ALL of:

| Permission Code | Rationale |
|---|---|
| `SYNQ_LIENS.lien:read:held` | Can view liens held by the organization |
| `SYNQ_LIENS.lien:service` | Can service active liens |
| `SYNQ_LIENS.lien:settle` | Can settle and close liens |

The mapping uses **ALL** semantics (conjunctive) — every permission in the set must be present for the capability to evaluate true. This prevents partial permission sets from granting capabilities they shouldn't.

Note: `SYNQ_LIENS.lien:browse` and `SYNQ_LIENS.lien:purchase` are not included in either capability. These are marketplace browsing/purchasing actions that could form a future `LIENS_BUY` capability if needed.

---

## 5. Where the Capability Abstraction Is Implemented

All capability logic lives in `Liens.Domain`:

```
apps/services/liens/Liens.Domain/
├── LiensCapabilities.cs            ← const string identifiers
├── LiensCapabilityResolver.cs      ← centralized mapping logic
├── LiensCapabilityExtensions.cs    ← ICurrentRequestContext extensions
└── LiensPermissions.cs             ← existing permission codes (unchanged)
```

**`LiensCapabilities`** — Two `const string` fields: `Sell` and `ManageInternal`.

**`LiensCapabilityResolver`** — Pure static class. Two methods:
- `HasCapability(permissions, capability)` — Checks if a permission set grants a specific capability.
- `ResolveAll(permissions)` — Returns the list of all capabilities the permission set grants.

No allocations beyond the result list. No DI, no database, no external dependencies.

**`LiensCapabilityExtensions`** — Extension methods on `ICurrentRequestContext`:
- `HasCapability(capability)` — Generic check (includes admin bypass)
- `CanSellLiens()` — Convenience shorthand
- `CanManageLiensInternal()` — Convenience shorthand
- `GetLiensCapabilities()` — Full resolution

Admin bypass: All extension methods check `IsPlatformAdmin` and TenantAdmin role membership first, consistent with `IsTenantAdminOrAbove()` in the endpoint authorization filters. Admin users always resolve all capabilities as `true`.

---

## 6. How Future Liens Features Should Consume It

### Workflow branching (Application layer)
```csharp
if (context.CanSellLiens())
{
    // show seller-side workflows
}

if (context.CanManageLiensInternal())
{
    // show holder/servicer workflows
}
```

### UI/navigation gating (via /context diagnostic endpoint)
The `/context` endpoint now returns:
```json
{
  "capabilities": {
    "sell": true,
    "manageInternal": false,
    "resolved": ["LIENS_SELL"]
  }
}
```
Front-end code can use `capabilities.resolved` or the boolean flags to show/hide menu items.

### Application service logic
```csharp
public async Task<DashboardView> GetDashboardAsync(ICurrentRequestContext ctx)
{
    var caps = ctx.GetLiensCapabilities();
    // Build view based on caps, not raw permission strings
}
```

### What NOT to do
- Do NOT use capabilities for endpoint authorization — `RequirePermission` remains the enforcement layer.
- Do NOT add capability checks inside domain entities — capabilities are external to the domain model.

---

## 7. Provider-Mode Interpretation

### Default Provider (Sell Only)
```
LIENS_SELL          = true
LIENS_MANAGE_INTERNAL = false
```
The provider (law firm) can create, offer, and view their own liens. They cannot service or settle liens. This is the standard SYNQLIEN_SELLER role assignment.

### Provider with Internal Management Enabled
```
LIENS_SELL          = true
LIENS_MANAGE_INTERNAL = true
```
The provider has both seller and holder permissions. This occurs when an organization holds both SYNQLIEN_SELLER and SYNQLIEN_HOLDER roles (or a combined assignment). Allows the organization to manage liens end-to-end.

### Future: Manage-Internal-Only Mode
```
LIENS_SELL          = false
LIENS_MANAGE_INTERNAL = true
```
The capability abstraction fully supports this combination. It would apply to an organization that can manage held liens (service, settle) but cannot create or offer new liens. This could represent a servicer-only organization.

The current provisioning flow does not assign this combination, but the capability model does not prevent it. When provisioning UI or API is extended to support this assignment pattern, no changes to the capability layer are needed.

### Verification
The resolver is a pure function of the permission set. Each capability is independently derived. There is no coupling between `Sell` and `ManageInternal` — any combination of `{true, false} × {true, false}` is valid and will resolve correctly.

---

## 8. Files Changed

### New Files (Liens.Domain)
| File | Description |
|---|---|
| `LiensCapabilities.cs` | Business capability constant identifiers |
| `LiensCapabilityResolver.cs` | Centralized permission → capability derivation |
| `LiensCapabilityExtensions.cs` | `ICurrentRequestContext` extension methods |

### Modified Files
| File | Change |
|---|---|
| `Liens.Api/Program.cs` | Added `capabilities` block to `/context` diagnostic endpoint |

### Unchanged
| File | Status |
|---|---|
| `LiensPermissions.cs` | No changes |
| `LienEndpoints.cs` | No changes — endpoint guards remain permission-based |
| `ICurrentRequestContext.cs` | No changes |
| `CurrentRequestContext.cs` | No changes |
| `RequirePermissionFilter.cs` | No changes |
| `RequireProductAccessFilter.cs` | No changes |
| All domain entities | No changes |

---

## 9. Build Results

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

---

## 10. Confirmation

### No v1 Logic Introduced
- No legacy role names, session tokens, or v1 authorization patterns were used.
- All capability derivation uses v2 `ICurrentRequestContext.Permissions` from JWT claims.
- No references to v1 entities, tables, or code paths.

### No Domain Contamination
- No domain entities were modified (Lien, LienOffer, Case, Facility, Contact, LookupValue — all unchanged).
- Capabilities are external to the domain model — they are identity-derived and consumed through context/helpers.
- `LiensCapabilityResolver` is a pure static function with no domain entity dependencies.

### No Replacement of Permission-Based Enforcement
- All 8 endpoint guards in `LienEndpoints.cs` remain unchanged.
- `RequirePermission` filters are untouched.
- `RequireProductAccess` group filter is untouched.
- Capabilities are explicitly positioned for business logic, workflow branching, and UI gating — not for authorization enforcement.

---

## 11. Database / Ownership Alignment

### Schema Changes Required?
**No.** Zero database changes. No migrations created. No schema modifications of any kind.

### Liens-Owned Tables Created?
**No.** No tables with the `liens_` prefix (or any prefix) were created.

### Identity Schema Changes?
**No.** The Identity database was not touched in this feature.

### Summary
This feature is purely in-memory code — three new `.cs` files and one modified line in the diagnostic endpoint. It has zero persistence footprint.

---

## 12. Risks / Assumptions

| Risk | Severity | Mitigation |
|---|---|---|
| **Capability mapping may not match future role restructuring** — If permission assignments change (e.g., SYNQLIEN_SELLER loses `lien:create`), the capability mapping must be updated. | Low | The mapping is centralized in `LiensCapabilityResolver` — exactly one place to update. |
| **ALL semantics may be too strict** — A user with 2 of 3 sell permissions gets `LIENS_SELL = false`. | Low | This is intentional. Partial permission sets should not grant capabilities. If ANY semantics are needed for specific workflows, a separate helper can be added. |
| **`lien:browse` and `lien:purchase` are unmapped** — These marketplace actions are not represented in either capability. | Informational | These could form a future `LIENS_BUY` capability. The current two capabilities cover the seller and holder operating modes as specified. |
| **No unit tests included** — The resolver is a pure function and easy to test, but no test project exists for Liens yet. | Low | The resolver's behavior is verified by the `/context` diagnostic endpoint at runtime. Unit tests should be added when the Liens test project is established. |

---

## 13. Final Readiness Statement

### Is the capability abstraction layer established?
**Yes.** The abstraction provides:
- Stable, business-facing capability identifiers (`LIENS_SELL`, `LIENS_MANAGE_INTERNAL`)
- A centralized, pure-function resolver mapping permissions to capabilities
- Clean extension methods for consumption from any code with access to `ICurrentRequestContext`
- Runtime visibility via the `/context` diagnostic endpoint

### Is the system ready for capability-based workflow branching?
**Yes.** Future Liens application services, controllers, and UI code can now:
- Branch on `context.CanSellLiens()` or `context.CanManageLiensInternal()`
- Build mode-specific views via `context.GetLiensCapabilities()`
- Gate navigation and feature visibility without inspecting raw permission strings

The permission model remains the enforcement layer. The capability model provides the business readability layer. Both are independently maintained and independently testable.
