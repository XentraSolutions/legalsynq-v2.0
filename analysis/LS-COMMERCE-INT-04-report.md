# LS-COMMERCE-INT-04 — Production Operational Finalization & Billing Lifecycle Operations

> **Status:** COMPLETE

---

## 1. Executive Summary

LS-COMMERCE-INT-04 finalizes the Commerce + Tenant Billing platform integration for production operations, building on LS-COMMERCE-INT-01 through INT-03.

**Delivered:**

| Area | What was added |
|---|---|
| Role-scoped navigation | `filterNavForRole()` in `nav.ts`; sidebar + shell updated; TenantAdmin sees only Billing Status; PlatformAdmin sees full Commerce & Billing section |
| Billing profile lifecycle actions | 3 actions (Activate / Suspend / Close); BFF route with confirmation gate; `BillingProfileActionsPanel` client component; rendered on `/tenants/[id]` |
| Manual entitlement publish | POST BFF route per billing account; per-account Publish button with confirm/result in `CommerceAccountPanel`; safe standalone-mode messaging |
| Commerce subscription detail | Expandable subscriptions per billing account (lazy-loaded client component); BFF route for `GET /api/commerce/billing-accounts/{id}/subscriptions` |
| Commerce account search/filter | Client-side search (name or account#), status filter chips, standing filter chips; all preserved within 20-account cap |
| Monitoring SDK alignment | All 4 monitoring .csproj files aligned to `net8.0`; shared `BuildingBlocks`, `AuditClient`, `Contracts` aligned to `net8.0`; Monitoring.Api now buildable with .NET 8.0.412 SDK |
| New types | `BillingProfileActionResult`, `CommerceSubscriptionItem`, `CommerceSubscriptionSummary`, `EntitlementPublishResult` |

**TypeScript: 0 errors. Commerce.sln: 0 errors. Billing.sln: 0 errors. Monitoring.Api: build successful.**

---

## 2. Prior Integration Baseline

| Component | Status |
|---|---|
| Commerce JWT identity integration (INT-01) | ✅ Preserved, untouched |
| Tenant Billing dual-mode resolver (INT-01) | ✅ Preserved, untouched |
| Gateway YARP routes :5030/:5031 (INT-01) | ✅ Preserved |
| Commerce + Billing service cards (INT-02) | ✅ Preserved |
| TenantBillingPanel on tenant detail (INT-02) | ✅ Preserved |
| Bridge diagnostics + account panels (INT-03) | ✅ Preserved; account panel enhanced with search/filter/publish |
| BillingEntitlementPanel (INT-03) | ✅ Preserved |
| MonitoringEntityBootstrap per-name reconciliation (INT-03) | ✅ Preserved |
| `/billing-status` page + BFF route (INT-03) | ✅ Preserved |

---

## 3. Role-Scoped Commerce/Billing Navigation

### 3.1 Implementation

**`filterNavForRole(isPlatformAdmin, isTenantAdmin)` — `apps/control-center/src/lib/nav.ts`**

```
PlatformAdmin  → full CC_NAV (no filtering)
TenantAdmin    → COMMERCE & BILLING section kept with only /billing-status item
Neither        → COMMERCE & BILLING section removed entirely
```

- Pure UX hardening — page guards remain the authoritative security layer
- No new authorization system — uses existing `session.isPlatformAdmin` / `session.isTenantAdmin` flags
- Zero impact on all other nav sections

**`cc-shell.tsx` (Server Component)** computes `filteredNav` using the session already fetched in the shell and passes it as a prop to `CCSidebar`.

**`cc-sidebar.tsx` (Client Component)** accepts optional `navSections?: NavSection[]` prop. When present, used for `getSectionBySlug` and `getSectionForPathname` lookups. Falls back to `CC_NAV` if not provided (preserves all existing callers).

**`nav-utils.ts`** — `getSectionBySlug` and `getSectionForPathname` updated to accept optional `sections` override. `getNavGroupModels()` unchanged (uses full `CC_NAV` — dashboard grid always shows PlatformAdmin's full section list since dashboard is PlatformAdmin-only in practice).

### 3.2 Role Visibility Table

| Nav Section | PlatformAdmin | TenantAdmin | Other |
|---|---|---|---|
| Commerce | ✅ | ❌ | ❌ |
| Tenant Billing | ✅ | ❌ | ❌ |
| Billing Status | ✅ | ✅ | ❌ |
| All other sections | ✅ (unchanged) | ✅ (unchanged) | ✅ (unchanged) |

---

## 4. Billing Profile Lifecycle Actions

### 4.1 Available Transitions

| Action | Allowed from Status | Effect |
|---|---|---|
| Activate | Draft, Suspended | Profile goes Active; resolver returns billing account for tenant |
| Suspend | Active | Temporarily paused; resolver returns null; reversible |
| Close | Draft, Active, Suspended | Terminal retirement; new profile may be created afterward |

Source: `TenantBillingProfileStatus.cs` + `TenantBillingProfilesController.cs`

### 4.2 BFF Route: `POST /api/billing/profiles/[profileId]/[action]`

- **Guard:** `requirePlatformAdmin()` — redirect/401 for non-PlatformAdmins
- **Validation:** UUID regex on `profileId`; allowlist on `action` (`activate | suspend | close`)
- **Token check:** Returns 503 with informational message if `BILLING_INTERNAL_TOKEN` not set
- **Tenant resolution:** Calls `/identity/api/auth/me` to resolve `tenantId` from session cookie; sends as `X-Tenant-Id` to Billing service
- **HTTP 404:** Returns safe `{ success: false, error: "Profile not found" }`
- **HTTP 409:** Returns safe `{ success: false, error: detail }` (invalid transition)
- **Response:** `BillingProfileActionResult { success, action, profileId, newStatus, error, executedAtUtc }`
- **Never** returns stack traces, tokens, connection strings, or internal exception details

### 4.3 Component: `BillingProfileActionsPanel`

- Client component (`'use client'`)
- Shows current status pill (color-coded by `Draft/Active/Suspended/Closed`)
- Only shows actions valid for current status (client-side logic matches server-side business rules)
- Two-step flow: click action → inline confirm → run → result card
- Success: shows new status in green card; failure: shows safe error in red card
- Result persists until dismissed; status updates locally on success (no full-page reload needed)
- Closed profiles: empty state note ("no further lifecycle actions available")
- `onActionComplete` callback prop for parent coordination if needed

### 4.4 Integration

Rendered on `/tenants/[id]/page.tsx` — only shown when `tenantBillingSummary.profileFound === true && tenantBillingSummary.profile !== null`. This means no actions panel appears for tenants without a billing profile.

---

## 5. Manual Entitlement Publish

### 5.1 Source Endpoint

`POST /api/commerce/integration/tenant-billing/billing-accounts/{billingAccountId}/publish-entitlement`

Implemented in `TenantBillingPublisherController.cs`. Returns `PublishEntitlementResultResponse` with `outcome: published | skipped | failed`.

### 5.2 BFF Route: `POST /api/commerce/billing-accounts/[billingAccountId]/publish-entitlement`

- **Guard:** `requirePlatformAdmin()`
- **Validation:** UUID regex on `billingAccountId`
- **401/403 from Commerce:** Returns informational 503 — "Commerce identity integration not enabled. Set `LegalSynq:Identity:Enabled=true`"
- **404:** Safe "Billing account not found" message
- **Response:** `EntitlementPublishResult { outcome, billingAccountId, tenantId, httpStatus, reason, attempts, executedAtUtc }`
- **Never** exposes internal tokens; no Commerce auth credentials forwarded (Commerce controls its own auth)

### 5.3 Integration

Per-account **Publish** button in `CommerceAccountPanel`. Each `AccountRow` has its own `PublishButton` component:
- Click → inline confirm (`"Confirm publish for {accountName}?"`)
- Success: green result card with outcome and attempts count; dismiss button
- Failure/standalone mode: amber result card with safe message
- Loading state during request

---

## 6. Commerce Subscription Detail Visibility

### 6.1 Source Endpoint

`GET /api/commerce/billing-accounts/{billingAccountId}/subscriptions` (BillingAccountSubscriptionsController)

Returns `IReadOnlyList<SubscriptionResponse>` with: `Id`, `SubscriptionNumber`, `Status`, `StartDateUtc`, `CurrentPeriodStartUtc`, `CurrentPeriodEndUtc`, `CancelAtPeriodEnd`, `CancelledAtUtc`, `CancellationReason`, `Items`.

### 6.2 BFF Route: `GET /api/commerce/billing-accounts/[billingAccountId]/subscriptions`

- **Guard:** `requirePlatformAdmin()`
- **Auth:** 401/403 → returns empty summary with informational error (expected in standalone mode)
- **Response:** `CommerceSubscriptionSummary { subscriptions, totalCount, billingAccountId, lastCheckedAtUtc, error }`
- All fields mapped from `SubscriptionResponse` with safe string coercions

### 6.3 Component: `CommerceSubscriptionsPanel`

- Client component (`'use client'`)
- **Lazy loading** — expandable accordion per billing account; subscriptions not loaded until expanded
- **Refresh button** — allows re-fetching without page reload
- Shows: subscription number, status pill (Active/Trialing/Paused/Cancelled/Expired), current period dates, cancellation info, line item count
- "Cancel at period end" badge
- Empty state: "No subscriptions found"
- Standalone mode error: amber informational card

### 6.4 Integration

`CommerceSubscriptionsPanel` is rendered inside each `AccountRow` in `CommerceAccountPanel` when the user clicks the "Subs" toggle button. This keeps the Commerce page clean while providing on-demand subscription detail for any account.

**Fields surfaced per subscription:**
- Subscription number (font-mono)
- Status (color-coded pill)
- Current period: start → end
- Start date
- Cancel at period end flag
- Cancellation date and reason (when applicable)
- Line item count

---

## 7. Commerce Account Search / Filter

### 7.1 Implementation

All filtering is client-side — acceptable for the existing 20-account cap.

**Search input** — searches `displayName` and `accountNumber` (case-insensitive, substring match).

**Status filter chips** — `All | Active | Suspended | Closed`; filters by `account.status`.

**Standing filter chips** — `All | Good | Warning | Suspended | Blocked`; filters by `account.standing`.

### 7.2 Behavior

- Filters are combinable (AND logic: must match all active filters)
- "No accounts match the current filter" empty state when filter produces zero results
- Account count in header reflects total (`summary.accountCount`), not filtered count
- "Showing 20 of N accounts. Use search to narrow results." overflow note preserved
- Active filter chips have distinct visual selected state (indigo for status, slate-700 for standing)
- Filters reset on component remount (no localStorage persistence — intentional: operational tool)

---

## 8. Monitoring SDK / Runtime Alignment

### 8.1 Root Cause

Monitoring service projects explicitly declared `net10.0` as the target framework. The repository uses .NET SDK 8.0.412 (confirmed in `replit.nix`/`replit.md`). Commerce and Billing services correctly use `net8.0` via their `Directory.Build.props` files. Monitoring lacked a `Directory.Build.props` and declared net10.0 in each individual csproj.

### 8.2 Solution

**Step 1:** Updated 4 Monitoring service `.csproj` files to `net8.0`:
- `Monitoring.Api/Monitoring.Api.csproj`
- `Monitoring.Application/Monitoring.Application.csproj`
- `Monitoring.Domain/Monitoring.Domain.csproj`
- `Monitoring.Infrastructure/Monitoring.Infrastructure.csproj`

**Step 2:** Updated 3 shared library `.csproj` files to `net8.0`:
- `shared/building-blocks/BuildingBlocks/BuildingBlocks.csproj`
- `shared/audit-client/LegalSynq.AuditClient/LegalSynq.AuditClient.csproj`
- `shared/contracts/Contracts/Contracts.csproj`

These are the direct dependencies of the Monitoring service. Shared library changes are backwards-compatible: services targeting `net10.0` (Identity, Fund) can still consume `net8.0` libraries due to .NET's TFM compatibility guarantees.

**Step 3:** Validated `Monitoring.Api` build — succeeded (0 errors).

### 8.3 Services Not Changed

| Service | Framework | Rationale |
|---|---|---|
| Commerce | net8.0 (Directory.Build.props) | Already correct |
| Tenant Billing | net8.0 (Directory.Build.props) | Already correct |
| Identity | net10.0 (explicit) | Out of scope; can still consume net8.0 shared libs |
| Fund | net10.0 (explicit) | Out of scope; can still consume net8.0 shared libs |
| BuildingBlocks.Tests / TestHelpers / IntegrationTests | net10.0 | Test-only; not production-reachable; out of scope |

### 8.4 `MonitoringEntityBootstrap.cs` — Previously Validated

The per-name reconciliation change from INT-03 compiles correctly under net8.0 (confirmed: `HashSet<string>`, `ToListAsync`, and `Linq` are all available via global usings in the net8.0 build).

---

## 9. Operational Safety Hardening

| Scenario | Handling |
|---|---|
| Missing `BILLING_INTERNAL_TOKEN` | BFF returns 503 with informational message; actions panel shows amber error card |
| Profile not found (404) | BFF returns safe `{ success: false, error: "Profile not found" }` |
| Invalid transition (409) | BFF extracts `problem.detail` from Billing response; returns safe error message |
| Billing service unreachable | BFF catches exception; returns `{ success: false, error: "Billing service unreachable" }` |
| Commerce identity disabled (publish) | BFF returns 503 with "Commerce identity integration not enabled" message; UI shows amber card |
| Commerce billing account not found | BFF returns 404 with safe message |
| Commerce service unreachable (subscriptions) | BFF returns empty summary with error field; UI shows amber informational card |
| Unauthorized lifecycle action attempt | `requirePlatformAdmin()` redirects; API route returns 401 |
| Invalid profileId or billingAccountId | UUID regex check → HTTP 400 with safe message |
| Network error in browser (fetch) | Component catches exception; displays "Request failed — unable to reach server" |
| Entitlement publish for standalone Commerce | Informational message (not an error) shown in result card |
| Subscription load for standalone Commerce | Informational amber card; subscriptions panel shows empty state |
| Action in progress | Button disabled while `pending !== null` |
| Action success | Local status update + green result card; no page reload required |
| Action failure | Red result card with safe error message; status unchanged |
| Cancel confirm without submitting | Returns to initial action button row |

**No stack traces, tokens, connection strings, or internal exception details are ever returned to the browser.**

---

## 10. RBAC / Authorization Summary

| Route / Component | Guard | Notes |
|---|---|---|
| `POST /api/billing/profiles/[id]/[action]` | `requirePlatformAdmin()` | PlatformAdmin only |
| `POST /api/commerce/billing-accounts/[id]/publish-entitlement` | `requirePlatformAdmin()` | PlatformAdmin only |
| `GET /api/commerce/billing-accounts/[id]/subscriptions` | `requirePlatformAdmin()` | PlatformAdmin only |
| `BillingProfileActionsPanel` | Client component — server route enforces | Actions panel only shown when profile exists |
| `PublishButton` (in CommerceAccountPanel) | Client component — server route enforces | Shown in `/commerce` page (PlatformAdmin-only page) |
| `CommerceSubscriptionsPanel` | Client component — server route enforces | Shown in `/commerce` page (PlatformAdmin-only page) |
| Role-scoped nav | UX hardening | Page guards remain authoritative security layer |

**No new authorization system introduced.** All guards use `requirePlatformAdmin()` from `lib/auth-guards.ts` exclusively.

---

## 11. Files Changed

### New Files

| File | Type | Purpose |
|---|---|---|
| `apps/control-center/src/app/api/billing/profiles/[profileId]/[action]/route.ts` | BFF route | Lifecycle actions: activate / suspend / close |
| `apps/control-center/src/app/api/commerce/billing-accounts/[billingAccountId]/publish-entitlement/route.ts` | BFF route | Trigger Commerce → Billing entitlement publish |
| `apps/control-center/src/app/api/commerce/billing-accounts/[billingAccountId]/subscriptions/route.ts` | BFF route | List subscriptions for a billing account |
| `apps/control-center/src/components/billing/billing-profile-actions-panel.tsx` | UI component | Lifecycle action buttons with inline confirm |
| `apps/control-center/src/components/commerce/commerce-subscriptions-panel.tsx` | UI component | Expandable subscription list per billing account |

### Modified Files

| File | Change |
|---|---|
| `apps/control-center/src/types/control-center.ts` | Added `BillingProfileActionResult`, `CommerceSubscriptionItem`, `CommerceSubscriptionSummary`, `EntitlementPublishResult` |
| `apps/control-center/src/lib/nav.ts` | Added `filterNavForRole()` function |
| `apps/control-center/src/lib/nav-utils.ts` | Updated `getSectionBySlug` + `getSectionForPathname` to accept optional `sections` param |
| `apps/control-center/src/components/shell/cc-sidebar.tsx` | Added `navSections?: NavSection[]` prop; passed through to inner component + section lookups |
| `apps/control-center/src/components/shell/cc-shell.tsx` | Imports + calls `filterNavForRole`; passes `filteredNav` to `CCSidebar` |
| `apps/control-center/src/components/commerce/commerce-account-panel.tsx` | Added search input, status/standing filter chips, per-account Publish button, expandable subscriptions; now a full client component |
| `apps/control-center/src/app/tenants/[id]/page.tsx` | Added `BillingProfileActionsPanel` (shown only when profile exists) |
| `apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj` | `net10.0` → `net8.0` |
| `apps/services/monitoring/Monitoring.Application/Monitoring.Application.csproj` | `net10.0` → `net8.0` |
| `apps/services/monitoring/Monitoring.Domain/Monitoring.Domain.csproj` | `net10.0` → `net8.0` |
| `apps/services/monitoring/Monitoring.Infrastructure/Monitoring.Infrastructure.csproj` | `net10.0` → `net8.0` |
| `shared/building-blocks/BuildingBlocks/BuildingBlocks.csproj` | `net10.0` → `net8.0` |
| `shared/audit-client/LegalSynq.AuditClient/LegalSynq.AuditClient.csproj` | `net10.0` → `net8.0` |
| `shared/contracts/Contracts/Contracts.csproj` | `net10.0` → `net8.0` |

---

## 12. Build / Test Validation

| Target | Result | Notes |
|---|---|---|
| `tsc --noEmit` (Control Center) | ✅ 0 errors | All new types, imports, component props, and route signatures resolve correctly |
| `Commerce.sln` | ✅ 0 errors | No regressions; Commerce + Billing unchanged |
| `Billing.sln` | ✅ 0 errors | No regressions |
| `Monitoring.Api` (post net8.0 alignment) | ✅ Build succeeded | Resolved by aligning monitoring + shared libs to net8.0 |
| Next.js dev server | ✅ Running | App live and serving; hot reload accepted all changes |

**Manual validation steps:**

1. Start application: `bash scripts/run-dev.sh`
2. Log in as **PlatformAdmin** at `http://localhost:5004`
3. Verify sidebar COMMERCE & BILLING shows: Commerce, Tenant Billing, Billing Status
4. Navigate to **COMMERCE & BILLING → Commerce**
   - Verify search input appears and filters the account list
   - Verify Status and Standing filter chips work (try "Active", try "Blocked")
   - Per account: click **Subs** to expand subscription panel (loads lazily)
   - Per account: click **Publish** → confirm → verify result card (informational in standalone mode)
5. Navigate to any **Tenant** with a billing profile — verify `BillingProfileActionsPanel` renders below `BillingEntitlementPanel`
   - Verify only valid transitions are shown for the current status
   - Click Activate / Suspend / Close → confirm → verify result card
6. Log out; log in as **TenantAdmin**
7. Verify sidebar COMMERCE & BILLING shows **only** Billing Status
8. Verify Commerce and Tenant Billing pages return 401/redirect for TenantAdmin
9. Verify `/billing-status` loads correctly for TenantAdmin

---

## 13. Risks / Deferred Items

| Item | Risk | Notes |
|---|---|---|
| `BuildingBlocks.Tests`, `BuildingBlocks.TestHelpers`, `BuildingBlocks.IntegrationTests` | Low | Still target net10.0; test-only projects, not production-reachable; deliberately not changed to avoid scope creep |
| Identity and Fund services still target net10.0 | Low | They can consume net8.0 shared libs via TFM compatibility. Full net8.0 alignment for Identity/Fund is a separate infrastructure concern. |
| Nav dashboard grid always shows full `CC_NAV` for hub cards | Low | `getNavGroupModels()` uses the full `CC_NAV` array. Dashboard is PlatformAdmin-only in practice. TenantAdmin cannot access `/` dashboard hub. |
| Lifecycle action tenant resolution via `/auth/me` per request | Low | Makes one additional /auth/me call per POST. Acceptable for infrequent PlatformAdmin actions. Could be optimized with session store in future. |
| Subscription detail is lazy-loaded (no server-side prefetch) | Low | Intentional — avoids N subscriptions-per-account requests on every Commerce page load. User must click to expand. |
| Publish entitlement for Commerce standalone mode | Low | Returns informational 503 with clear message; not an error; expected in dev deployments |
| CommerceAccountPanel is now a full client component | Low | Search/filter requires client interactivity. `'use client'` boundary is appropriate. |

---

## 14. Non-Merge Boundaries Confirmed

| Rule | Status |
|---|---|
| No database merges | ✅ |
| No shared DbContext | ✅ |
| No direct cross-service EF access | ✅ HTTP/BFF calls only |
| No invoice/payment/customer record mutation | ✅ Lifecycle actions only change `TenantBillingProfile.Status` |
| No new authorization framework | ✅ Uses `requirePlatformAdmin()` exclusively |
| Commerce standalone mode preserved | ✅ `LegalSynq:Identity:Enabled=false` fully supported |
| Billing standalone mode preserved | ✅ |
| LS-INT-01 resolver architecture unchanged | ✅ |
| No Tenant Portal (apps/web) changes | ✅ |
| No payment provider integration changes | ✅ |
| No notification service changes | ✅ |
| No document service changes | ✅ |

---

## 15. Deployment Readiness

### Prerequisites

| Item | Status |
|---|---|
| `.NET 8.0.412` SDK (already in environment) | ✅ |
| Commerce service running on :5030 | Required for account detail, diagnostics, publish, subscriptions |
| Tenant Billing service running on :5031 | Required for profile detail, entitlement, lifecycle actions |
| `BILLING_INTERNAL_TOKEN` env var set | Required for lifecycle action BFF; graceful degradation if missing |
| `COMMERCE_SERVICE_URL` env var (optional) | Defaults to `http://127.0.0.1:5030` |
| `BILLING_SERVICE_URL` env var (optional) | Defaults to `http://127.0.0.1:5031` |

### Graceful Degradation Summary

All new panels and buttons degrade gracefully when their upstream service is unavailable. No new hard dependencies are introduced. The Control Center remains fully operational for all non-Commerce/Billing features even if both commerce services are offline.

### SDK Note

Services that still explicitly target `net10.0` (Identity, Fund, and the test helper projects in shared/building-blocks) require a .NET 10+ SDK to build. These are not affected by this ticket's changes. The production Monitoring service, Commerce, and Billing can all be built and deployed with .NET SDK 8.

### Production Checklist

- [x] TypeScript 0 errors
- [x] Commerce.sln 0 errors
- [x] Billing.sln 0 errors  
- [x] Monitoring.Api builds with .NET 8 SDK
- [x] All BFF routes use server-side guards
- [x] No tokens or sensitive data exposed to browser
- [x] All actions require PlatformAdmin role
- [x] TenantAdmin blocked from PlatformAdmin routes (page + API guards)
- [x] All fallback/error states are safe and informative
- [x] Role-scoped nav implemented as UX hardening (not security replacement)
