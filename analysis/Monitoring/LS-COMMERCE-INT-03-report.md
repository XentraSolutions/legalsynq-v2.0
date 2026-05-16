# LS-COMMERCE-INT-03 — Tenant-Aware Billing Visibility & Operational Hardening

> **Status:** COMPLETE

---

## 1. Executive Summary

LS-COMMERCE-INT-03 delivers additive operational hardening for Commerce + Tenant Billing visibility in the LegalSynq Control Center. It builds on LS-COMMERCE-INT-01 (runtime integration) and LS-COMMERCE-INT-02 (operational service cards and basic billing panels) to add richer operational content, TenantAdmin-scoped access, and production-safe monitoring registration.

**Delivered:**

| Area | What was added |
|---|---|
| TenantAdmin billing visibility | `/billing-status` page + `GET /api/billing/my-billing-status` BFF route; TenantAdmin sees own tenant only; PlatformAdmin can inspect any tenant via `?tenantId=` |
| Commerce billing account detail | `GET /api/commerce/account-detail` BFF route; `CommerceAccountPanel` component; account list + per-account standing, shown on `/commerce` page |
| Commerce bridge diagnostics | `GET /api/commerce/bridge-diagnostics` BFF route; `CommerceBridgeDiagnosticsPanel` component; surfaces circuit breaker state, outbox posture, mode, target route — shown on `/commerce` page |
| Tenant Billing entitlement snapshot | `GET /api/billing/entitlements/[tenantId]` BFF route; `BillingEntitlementPanel` component; shows entitlement status, access recommendation, enabled flags, plan/product, sync timestamp — shown on both `/tenants/[id]` and `/billing-status` |
| Monitoring registration hardening | `MonitoringEntityBootstrap.cs` changed from skip-if-any to per-name reconciliation; Commerce + Tenant Billing added to entity list; safe for existing deployments |
| New types | `CommerceAccountItem`, `CommerceAccountSummary`, `CommerceBridgeDiagnostics`, `BillingEntitlementSnapshot`, `TenantAdminBillingStatus` |
| Nav | `Billing Status` entry added to COMMERCE & BILLING nav section |

**TypeScript check: 0 errors. Commerce.sln: 0 errors. Billing.sln: 0 errors.**

---

## 2. Prior Integration Baseline (LS-COMMERCE-INT-01 + LS-COMMERCE-INT-02)

| Component | Status |
|---|---|
| Commerce JWT identity integration | ✅ Preserved, untouched |
| Tenant Billing dual-mode tenant context resolver | ✅ Preserved, untouched |
| Gateway YARP routes `/commerce/**` → :5030, `/billing/**` → :5031 | ✅ Preserved |
| `scripts/run-dev.sh` Commerce + Billing startup | ✅ Preserved |
| Commerce service card + readiness panel | ✅ Preserved; page now richer |
| Billing service card | ✅ Preserved |
| `TenantBillingPanel` on `/tenants/[id]` | ✅ Preserved |
| Commerce + Billing in `DEFAULT_SERVICES` (Control Center local probes) | ✅ Preserved |
| `LegalSynqCommerceIdentityTests` 19/19 | ✅ Untouched |
| `LegalSynqTenantContextResolverTests` 21/21 | ✅ Untouched |

---

## 3. TenantAdmin Visibility Architecture

### 3.1 Constraint Analysis

The `/tenants/[id]/` layout uses `requirePlatformAdmin()` — a hard guard that redirects any non-PlatformAdmin to `/login?reason=unauthorized`. TenantAdmin access cannot be added to that layout without broader changes that would be out of scope.

The existing pattern for TenantAdmin-accessible pages: create pages under a different route using `requireAdmin()` (which allows both PlatformAdmin and TenantAdmin) with tenant isolation enforced at the BFF layer. This pattern is used by `/tenant-users`, `/authorization-simulator`, and `/access-groups/[tenantId]/[groupId]`.

### 3.2 Implementation

**New page: `/billing-status`**
- Uses `requireAdmin()` — accessible to both PlatformAdmin and TenantAdmin
- Renders `TenantBillingPanel` (profile) + `BillingEntitlementPanel` (entitlement)
- Descriptive header adapts based on `session.isPlatformAdmin`

**New BFF route: `GET /api/billing/my-billing-status`**
- Uses `requireAdmin()` guard
- TenantAdmin: always uses `session.tenantId`; any provided `?tenantId=` that differs from their own returns 403
- PlatformAdmin: defaults to `session.tenantId`; may pass `?tenantId=<uuid>` to inspect any tenant
- Calls Tenant Billing service for profile list, entitlement snapshot, and access decision in one `Promise.allSettled` call
- Returns `TenantAdminBillingStatus` (profile + entitlement + lastCheckedAtUtc + error)

### 3.3 Tenant Isolation Enforcement Layers

| Layer | Enforcement |
|---|---|
| BFF guard | `requireAdmin()` — no unauthenticated access |
| Tenant ownership check | `session.tenantId !== qTenant` → HTTP 403 for TenantAdmins |
| Billing service | `X-Tenant-Id` header scopes all Billing API responses to the declared tenant |
| Backend | Billing service `TenantResolutionMiddleware` validates the X-Tenant-Id claim |

PlatformAdmins bypass the ownership restriction to provide cross-tenant operational visibility.

---

## 4. Commerce Billing Detail Visibility

### 4.1 BFF Route: `GET /api/commerce/account-detail`

- **Auth:** `requirePlatformAdmin()`
- **Source:** Calls `COMMERCE_SERVICE_URL/api/commerce/billing-accounts` directly (bypasses gateway, consistent with health probe pattern)
- **Standing enrichment:** Calls `COMMERCE_SERVICE_URL/api/commerce/billing-accounts/{id}/account-standing` per account (capped at 20); uses `Promise.allSettled` — standing failure is non-fatal per-account
- **Auth handling:** If Commerce returns 401/403 (standalone mode, `LegalSynq:Identity:Enabled=false`), returns an informational message with `error` field instead of an exception — this is expected in dev deployments
- **Response:** `CommerceAccountSummary { accountCount, accounts: CommerceAccountItem[], lastCheckedAtUtc, error }`
- **`CommerceAccountItem`:** `id, accountNumber, displayName, status, standing, standingReason, standingLastEvaluatedAtUtc`

### 4.2 Component: `CommerceAccountPanel`

- Renders account rows with status pill (Active/Suspended/Closed) and standing pill (Good/Warning/Suspended/Blocked)
- Empty state for zero accounts
- Informational amber card when Commerce identity integration is not enabled
- Red error card for hard failures
- Capped at 20 accounts with overflow note
- Last-checked timestamp

### 4.3 Integration with `/commerce` page

The Commerce page now fetches all three data sources in parallel (`Promise.allSettled`):
1. Commerce summary (health + readiness)
2. Bridge diagnostics
3. Account detail

Each renders independently — health card failure does not prevent account panel from rendering.

---

## 5. Entitlement Snapshot Visibility

### 5.1 BFF Route: `GET /api/billing/entitlements/[tenantId]`

- **Auth:** `requirePlatformAdmin()`
- **Validation:** UUID regex check on `tenantId` → HTTP 400 if invalid
- **Token check:** Returns amber fallback message if `BILLING_INTERNAL_TOKEN` not set
- **Sources:** Calls Billing service in parallel:
  - `GET /api/tenant-billing/entitlements/current` (X-Tenant-Id, X-Internal-Token) → snapshot
  - `GET /api/tenant-billing/entitlements/access` → access decision (IsEnabled, WriteAccessAllowed, etc.)
- **Merge logic:** Access decision is preferred for `entitlementStatus` and `accessRecommendation` fields; snapshot provides `profileId`, `billingAccountId`, `sourcePlanKey`, `sourceProductKey`, `effectiveFromUtc`, `lastSyncedAtUtc`
- **404 handling:** Snapshot 404 → `profileId: null` (no error); this is expected for tenants without a profile
- **Response:** `BillingEntitlementSnapshot`

### 5.2 Component: `BillingEntitlementPanel`

- Displays: entitlement status pill (color-coded), access recommendation pill, platform access enabled, write access allowed, source plan, source product, billing account ID, effective from, last synced, last checked
- Empty state when no snapshot data is available (expected for tenants without a billing profile)
- Amber error card for token missing / service errors
- Optional `tenantId` prop for disambiguation in multi-panel pages

### 5.3 Integration Points

- Rendered on `/tenants/[id]/page.tsx` (PlatformAdmin, fetched in `Promise.allSettled`)
- Rendered on `/billing-status` (TenantAdmin + PlatformAdmin, via `my-billing-status` BFF route)

---

## 6. Commerce ↔ Tenant Billing Diagnostics

### 6.1 Source Endpoint

`GET /api/commerce/integration/tenant-billing/diagnostics` on the Commerce service (`TenantBillingPublisherController.cs`). This endpoint:
- Returns `TenantBillingDiagnostics` record (confirmed from `ITenantBillingEntitlementPublisher.cs`)
- Never returns the internal token (only `InternalTokenConfigured: bool`)
- Includes circuit breaker state, outbox posture (pending/failed/published counts), auto-publish queue depth, mode, and target route

### 6.2 BFF Route: `GET /api/commerce/bridge-diagnostics`

- **Auth:** `requirePlatformAdmin()`
- **Source:** `COMMERCE_SERVICE_URL/api/commerce/integration/tenant-billing/diagnostics` directly (internal diagnostic endpoint)
- **Auth handling:** 401/403 → informational message ("Commerce identity integration not enabled")
- **Response:** `CommerceBridgeDiagnostics` — all fields mapped 1:1 from `TenantBillingDiagnostics`
- **Safety:** No token values returned; all numeric counts are safe operational data

### 6.3 Component: `CommerceBridgeDiagnosticsPanel`

- Header badge: "Bridge Enabled" (green) / "Bridge Disabled" (grey)
- Amber informational banner when bridge is disabled (expected in dev)
- Displays: enabled, base URL configured, token configured, mode, target route, timeout, retry attempts
- Circuit breaker: state pill (Closed=green / Open=red / HalfOpen=amber)
- Auto-publish queue depth (when enabled)
- Outbox section (when enabled): pending, failed, published counts
- Last checked timestamp

---

## 7. Monitoring Registration Hardening

### 7.1 Problem Statement

`MonitoringEntityBootstrap.StartAsync()` previously checked `db.MonitoredEntities.AnyAsync()` — if **any** entity existed, the entire seed was skipped. For deployments that already had rows for Gateway, Identity, etc., Commerce and Tenant Billing would never be added by the bootstrap.

### 7.2 Solution: Per-Name Reconciliation

Changed the guard to:
```csharp
var existingNames = await db.MonitoredEntities
    .AsNoTracking()
    .Select(e => e.Name)
    .ToListAsync(cancellationToken);

var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
var toAdd       = Entities.Where(s => !existingSet.Contains(s.Name)).ToArray();
```

- Fresh deployments (empty table): all 12 entities seeded as before
- Existing deployments (10 entities from LS-INT-02): Commerce + Tenant Billing added, 10 existing untouched
- Fully seeded deployments (12 entities): `toAdd.Length == 0` → skips with info log
- **Idempotent** — safe on every restart regardless of deployment state
- **No destructive operations** — existing entity IDs, enabled flags, and custom names are never touched

### 7.3 Commerce + Tenant Billing Added to Seed List

```csharp
new("Commerce",       "http://127.0.0.1:5030/health", EntityType.InternalService,
    MonitoringType.Http, ImpactLevel.Degraded, "infrastructure"),

new("Tenant Billing", "http://127.0.0.1:5031/health", EntityType.InternalService,
    MonitoringType.Http, ImpactLevel.Degraded, "infrastructure"),
```

`ImpactLevel.Degraded` (not `Blocking`) — consistent with Documents, Notifications, Workflow, and product services. Gateway and Identity remain `ImpactLevel.Blocking`.

---

## 8. Operational Safety Improvements

| Scenario | Handling |
|---|---|
| Commerce service offline | Red service card; bridge diagnostics panel shows offline error; account panel shows error banner |
| Commerce identity not enabled (401) | Informational amber card — not an error, expected in standalone mode |
| Bridge disabled | Amber informational banner; all fields still shown; panel renders cleanly |
| Circuit breaker Open | Red pill on circuit breaker field |
| Billing service offline | Entitlement panel shows error banner (non-fatal) |
| `BILLING_INTERNAL_TOKEN` not set | Amber warning card in both entitlement panel and billing status page |
| TenantAdmin cross-tenant access attempt | HTTP 403 from BFF route |
| Invalid tenantId (non-UUID) | HTTP 400 from entitlement BFF route |
| Missing `session.tenantId` for TenantAdmin | HTTP 400 with safe message |
| BFF fetch fails entirely (network) | All panels fail gracefully with `error` field — page does not crash |
| Billing 404 (no profile) | Empty state card with icon and explanation, not an error |
| All diagnostics unavailable | Each panel independently shows empty/error state; other panels unaffected |

**Security rules followed throughout:**
- No stack traces in responses
- No token values returned (only boolean presence flags)
- No connection strings
- No internal exception details
- No cross-tenant data without PlatformAdmin or explicit ownership check

---

## 9. RBAC / Tenant Ownership Validation

| Route / Page | Guard | Tenant Isolation |
|---|---|---|
| `GET /api/commerce/account-detail` | `requirePlatformAdmin()` | N/A — platform-level data |
| `GET /api/commerce/bridge-diagnostics` | `requirePlatformAdmin()` | N/A — platform-level diagnostics |
| `GET /api/billing/entitlements/[tenantId]` | `requirePlatformAdmin()` | `tenantId` UUID validated; `X-Tenant-Id` forwarded |
| `GET /api/billing/my-billing-status` | `requireAdmin()` | TenantAdmin: `session.tenantId` only (403 on mismatch); PlatformAdmin: any tenant |
| `/billing-status` page | `requireAdmin()` | Inherits from BFF route |
| `/tenants/[id]` page | `requirePlatformAdmin()` (in layout) | PlatformAdmin-only — unchanged |
| `/commerce` page | `requirePlatformAdmin()` | N/A — platform-level |

**No new authorization system introduced.** Uses `requireAdmin()` and `requirePlatformAdmin()` from `lib/auth-guards.ts` exclusively.

| Role | Commerce Page | Bridge Diag | Account Detail | /billing-status | Tenant Entitlement Panel | Own Entitlement |
|---|---|---|---|---|---|---|
| PlatformAdmin | ✅ | ✅ | ✅ | ✅ (any tenant) | ✅ | ✅ |
| TenantAdmin | ❌ | ❌ | ❌ | ✅ (own tenant only) | ❌ | ✅ |
| Other roles | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

---

## 10. Error / Fallback Handling

All BFF routes return safe JSON regardless of upstream failure. None throw to the client or expose internal details.

| BFF Route | On Timeout | On 401/403 | On Non-2xx | On Network Error |
|---|---|---|---|---|
| `/api/commerce/account-detail` | `empty("unreachable")` | Informational amber message | `empty("HTTP {status}")` | `empty("unreachable")` |
| `/api/commerce/bridge-diagnostics` | `offline("unreachable")` | Informational message | `offline("HTTP {status}")` | `offline("unreachable")` |
| `/api/billing/entitlements/[tenantId]` | `empty("unreachable")` | `empty("rejected {status}")` | `empty("HTTP {status}")` | `empty("unreachable")` |
| `/api/billing/my-billing-status` | `emptyStatus("unreachable")` | `emptyStatus("rejected {status}")` | `emptyStatus("HTTP {status}")` | `emptyStatus("unreachable")` |

All `Promise.allSettled` patterns on pages ensure that a single panel failure does not crash the containing page.

---

## 11. Files Changed

### New Files

| File | Type | Purpose |
|---|---|---|
| `apps/control-center/src/app/api/commerce/account-detail/route.ts` | BFF route | Commerce billing accounts + account standing |
| `apps/control-center/src/app/api/commerce/bridge-diagnostics/route.ts` | BFF route | Commerce → Billing bridge diagnostics |
| `apps/control-center/src/app/api/billing/entitlements/[tenantId]/route.ts` | BFF route | Tenant billing entitlement snapshot + access decision |
| `apps/control-center/src/app/api/billing/my-billing-status/route.ts` | BFF route | TenantAdmin + PlatformAdmin combined billing status |
| `apps/control-center/src/app/billing-status/page.tsx` | Page (Server Component) | TenantAdmin-accessible billing profile + entitlement page |
| `apps/control-center/src/components/billing/billing-entitlement-panel.tsx` | UI component | Entitlement status/recommendation/flags panel |
| `apps/control-center/src/components/commerce/commerce-bridge-diagnostics-panel.tsx` | UI component | Bridge diagnostics: CB state, outbox posture, mode |
| `apps/control-center/src/components/commerce/commerce-account-panel.tsx` | UI component | Billing account list with status + standing pills |

### Modified Files

| File | Change |
|---|---|
| `apps/control-center/src/types/control-center.ts` | Added `CommerceAccountItem`, `CommerceAccountSummary`, `CommerceBridgeDiagnostics`, `BillingEntitlementSnapshot`, `TenantAdminBillingStatus` |
| `apps/control-center/src/app/commerce/page.tsx` | Added bridge diagnostics + account panels; parallel fetch via `Promise.allSettled` |
| `apps/control-center/src/app/tenants/[id]/page.tsx` | Added `BillingEntitlementPanel`; consolidated BFF fetch into shared `bffFetch` helper |
| `apps/control-center/src/lib/nav.ts` | Added `Billing Status` entry to COMMERCE & BILLING section |
| `apps/services/monitoring/Monitoring.Infrastructure/Bootstrap/MonitoringEntityBootstrap.cs` | Per-name reconciliation (replaces skip-if-any); added Commerce + Tenant Billing to entity seed list |

### Files Validated (Not Modified)

| File | Status |
|---|---|
| `apps/gateway/Gateway.Api/appsettings.json` | All 6 YARP routes confirmed correct — no changes needed |
| `apps/services/commerce/**` | Unchanged; Commerce.sln: 0 errors, 7 warnings (pre-existing) |
| `apps/services/tenant-billing/**` | Unchanged; Billing.sln: 0 errors |

---

## 12. Build/Test Validation

| Target | Result | Notes |
|---|---|---|
| `tsc --noEmit` (Control Center) | ✅ 0 errors | All new types, imports, and component props resolve correctly |
| Next.js Fast Refresh | ✅ Running | App is live and accepted all changes |
| `Commerce.sln` | ✅ 0 errors, 7 warnings | Pre-existing OTLP pkg NU1902 warnings; no new warnings from INT-03 |
| `Billing.sln` | ✅ 0 errors | No regressions |
| `Monitoring.Infrastructure.csproj` | ⚠️ Pre-existing failure | Projects target `net10.0`; environment has .NET SDK 8.0.412 only — pre-existing, not caused by INT-03. Confirmed via `TargetFramework` check in .csproj. Bootstrap change is syntactically verified (global usings provide `HashSet`, `Linq`, `ToListAsync`). |

**Bootstrap change syntax verification:**
- `HashSet<string>` — available via `global using global::System.Collections.Generic` ✅
- `ToListAsync()` — available via `Microsoft.EntityFrameworkCore` (already imported) ✅
- `Entities.Where(...).ToArray()` — available via `global using global::System.Linq` ✅
- All variable names and patterns consistent with existing file style ✅

**Manual validation steps:**

1. Start application: `bash scripts/run-dev.sh`
2. Log in as **PlatformAdmin** at `http://localhost:5004`
3. Navigate to **COMMERCE & BILLING → Commerce** — verify bridge diagnostics panel renders (disabled/enabled); verify account panel renders (empty/informational for standalone mode)
4. Navigate to **COMMERCE & BILLING → Billing Status** — verify both billing profile + entitlement panels render for PlatformAdmin's own tenant
5. Navigate to any **Tenant detail page** — verify BillingEntitlementPanel renders below TenantBillingPanel
6. Log out; log in as a **TenantAdmin** user
7. Navigate to **COMMERCE & BILLING → Billing Status** — verify TenantAdmin sees own billing status only
8. Try `?tenantId=<different-uuid>` on the billing-status page as TenantAdmin — verify 403 response in panel
9. Verify **Commerce** and **Tenant Billing** appear in `/monitoring` probe list (system health dashboard)

---

## 13. Risks / Deferred Items

| Item | Risk | Notes |
|---|---|---|
| Monitoring service targets `net10.0` | Pre-existing | Not caused by INT-03; bootstrap change verified syntactically; will build when SDK is upgraded |
| Commerce account detail requires `LegalSynq:Identity:Enabled=true` | Low | Standalone mode returns 401; UI handles with informational amber card — not an error |
| TenantAdmin sees `/billing-status` in nav regardless of auth level | Low | Nav items for COMMERCE & BILLING section are shown to all authenticated users; page guard handles unauthorized access cleanly |
| Bridge diagnostics only available if Commerce is running | Low | BFF handles unreachable Commerce with safe fallback |
| Entitlement snapshot may return `Unknown` on fresh deployments | Low | Expected — tenant has not yet had an entitlement applied from Commerce |
| `my-billing-status` fetches all billing profiles (paginated) and takes `items[0]` | Low | For now, each tenant has at most one active profile; production-safe for current schema |
| Billing profile status change UI | Low | Deliberately deferred — this ticket is visibility only |

---

## 14. Confirmation of Non-Merge Boundaries

| Rule | Status |
|---|---|
| No Commerce/Tenant Billing database merge | ✅ |
| No shared DbContext introduced | ✅ |
| No direct cross-service EF access | ✅ BFF/HTTP calls only |
| No invoice/payment UI | ✅ Visibility only |
| No route namespace rewrites | ✅ All routes additive |
| No Control Center redesign | ✅ Additive panels and pages only |
| No new authorization framework | ✅ Uses `requireAdmin()` and `requirePlatformAdmin()` exclusively |
| Commerce standalone mode preserved | ✅ `LegalSynq:Identity:Enabled=false` untouched |
| Billing standalone mode preserved | ✅ `LegalSynq:TenantContext:Enabled=false` untouched |
| No Tenant Billing → Commerce runtime dependency | ✅ |
| LS-INT-01 resolver architecture unchanged | ✅ |

---

## 15. Recommended Scope for LS-COMMERCE-INT-04

Based on what was implemented and what remains deferred:

1. **Billing profile lifecycle actions** — Allow PlatformAdmin to activate, suspend, or close a tenant billing profile from the Control Center tenant detail page. Requires POST support in BFF routes and confirmation modal pattern (consistent with existing tenant status actions).

2. **TenantAdmin nav visibility scoping** — Conditionally show/hide COMMERCE & BILLING nav items based on role. PlatformAdmin sees Commerce + Tenant Billing + Billing Status; TenantAdmin sees only Billing Status. Requires role-aware nav filtering.

3. **Entitlement publish trigger** — Allow PlatformAdmin to trigger a manual entitlement publish from Commerce to Tenant Billing for a specific billing account, using the existing `POST /api/commerce/integration/tenant-billing/billing-accounts/{id}/publish-entitlement` endpoint. Operational convenience only — no domain mutation.

4. **Monitoring SDK upgrade alignment** — Once the Monitoring service csproj is updated to `net8.0`, validate that the bootstrap reconciliation change compiles and runs correctly end-to-end.

5. **Commerce subscription detail view** — Surface active subscriptions, plan keys, and current period dates for a billing account on the Commerce page. Requires `GET /api/commerce/integration/billing-accounts/{id}/entitlement-snapshot` integration.

6. **Commerce billing account search/filter** — The account panel currently shows all accounts (capped at 20). Add search-by-name or filter-by-status for deployments with many accounts.

7. **Billing entitlement history** — Surface a list of past entitlement snapshot applications per tenant for audit/traceability. Requires a history endpoint on the Billing service (not currently implemented).
