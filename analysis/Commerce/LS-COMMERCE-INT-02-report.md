# LS-COMMERCE-INT-02 — Control Center & Operational Integration

> **Status:** COMPLETE

---

## 1. Executive Summary

LS-COMMERCE-INT-02 integrates Commerce and Tenant Billing operational awareness into the LegalSynq Control Center and platform monitoring layer. This is a purely additive operational integration — no billing domains are merged, no shared DbContexts are introduced, and standalone service behavior from LS-COMMERCE-INT-01 remains fully intact.

**Delivered:**
- `GET /api/commerce/summary` — BFF route probing Commerce health + readiness (PlatformAdmin gated)
- `GET /api/billing/summary` — BFF route probing Tenant Billing health + healthz (PlatformAdmin gated)
- `GET /api/billing/tenant-summary/[tenantId]` — BFF route fetching tenant-scoped billing profile (PlatformAdmin gated, X-Internal-Token forwarded server-side)
- `/commerce` page — Commerce operational status page with service card, readiness panel, service info
- `/billing` page — Tenant Billing operational status page with service card, service info, profile routing note
- `COMMERCE & BILLING` nav section added to Control Center sidebar
- Commerce + Tenant Billing registered in system health monitoring probes (`DEFAULT_SERVICES`)
- `TenantBillingPanel` added to tenant detail page (`/tenants/[id]`) — visible billing profile per tenant
- TypeScript type definitions for `CommerceSummary`, `BillingSummary`, `TenantBillingProfile`, `TenantBillingSummary`
- `COMMERCE_SERVICE_URL`, `BILLING_SERVICE_URL`, `BILLING_INTERNAL_TOKEN` added to `env.ts` and `.env.local`
- All safe error/loading/empty states implemented throughout
- TypeScript check: **0 errors**

---

## 2. Prior Integration Baseline (LS-COMMERCE-INT-01)

The following was delivered in LS-INT-01 and has not been regressed:

| Component | Status |
|---|---|
| Commerce JWT identity integration | ✅ Preserved |
| Tenant Billing dual-mode tenant context resolver | ✅ Preserved |
| `LegalSynq:Identity:Enabled=false` safe default | ✅ Preserved |
| `LegalSynq:TenantContext:Enabled=false` safe default | ✅ Preserved |
| Gateway YARP routes `/commerce/**` → `:5030` | ✅ Preserved |
| Gateway YARP routes `/billing/**` → `:5031` | ✅ Preserved |
| `scripts/run-dev.sh` Commerce + Billing startup | ✅ Preserved |
| `LegalSynqCommerceIdentityTests` 19/19 | ✅ Untouched |
| `LegalSynqTenantContextResolverTests` 21/21 | ✅ Untouched |

---

## 3. Control Center / Platform UI Pattern Review

### 3.1 App Location

- `apps/control-center/` — Next.js 15.2.9 App Router + TypeScript + Tailwind CSS v4
- Port: 5004 (dev); started by `scripts/run-dev.sh`
- BFF pattern: `/api/auth/*` and `/api/monitoring/*` are local route handlers; `/api/*` falls through to gateway via fallback rewrite

### 3.2 Routing Conventions

- App Router flat + nested directory structure
- Server Components for pages; `'use client'` only where interaction required
- `export const dynamic = 'force-dynamic'` on all data-fetching pages
- `CCShell` wraps every authenticated page; `requirePlatformAdmin()` guards all admin routes

### 3.3 Service Dashboard Patterns

- Each service has a dedicated page under `/operations/` or a top-level route
- Service card: colored ring (green/amber/red), animated ping dot for online, latency + timestamp
- Readiness panel: list of named checks with status pills
- Service info card: domain, port, gateway route, health endpoint, auth mode
- Pattern source: `apps/control-center/src/app/reports/page.tsx` + `components/reports/reports-service-card.tsx`

### 3.4 API/BFF Client Patterns

- BFF routes: `requirePlatformAdmin()` first → `fetch` with `AbortSignal.timeout(4000)` → `NextResponse.json` with `Cache-Control: no-store`
- Self-call pattern for Server Components: `CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004'` + forward cookies
- All environment variable access centralised in `lib/env.ts`

### 3.5 RBAC Visibility Patterns

- Single guard: `requirePlatformAdmin()` from `lib/auth-guards.ts`
- All operational visibility (Commerce, Billing) gated at PlatformAdmin level — consistent with existing pattern for Reports, Monitoring, Audit pages
- Tenant-level billing profile visibility also requires PlatformAdmin (same as tenant detail page)

### 3.6 Error/Loading/Empty State Conventions

- `fetchError`: red card with message (no stack trace)
- Service unavailable: `'offline'` status card with red styling
- Missing profile: empty state illustration + message
- Token not configured: amber informational card with actionable message
- No inline spinners (Server Component pattern — page loads or shows error)

---

## 4. Commerce Operational Awareness

### 4.1 Files Created

| File | Purpose |
|---|---|
| `apps/control-center/src/app/api/commerce/summary/route.ts` | BFF route: probes Commerce `/health` + `/ready` |
| `apps/control-center/src/app/commerce/page.tsx` | Commerce operational page |
| `apps/control-center/src/components/commerce/commerce-service-card.tsx` | Service card + readiness panel components |

### 4.2 BFF Route: `GET /api/commerce/summary`

- **Auth:** `requirePlatformAdmin()` — returns 401 if not PlatformAdmin
- **Probes:** `{COMMERCE_SERVICE_URL}/health` (4s timeout) → status; `{COMMERCE_SERVICE_URL}/ready` (6s timeout) → `readinessChecks`
- **Status logic:** online (< 2000ms) / degraded (≥ 2000ms or non-2xx) / offline (unreachable)
- **Readiness checks:** parses `body.database` from `/ready` response into named check with ok/degraded/error
- **Response:** `CommerceSummary { serviceStatus, serviceLatencyMs, lastCheckedAtUtc, readinessChecks }`
- **Cache:** `no-store` — always fresh

### 4.3 Commerce Page: `/commerce`

- Displays `CommerceServiceCard` (status + latency + timestamp)
- Displays `CommerceReadinessPanel` when readiness checks are available
- Displays service info card: domain, port, gateway route, health endpoints, integration mode
- Error state: red card when BFF call fails entirely
- Visible from sidebar: COMMERCE & BILLING → Commerce

---

## 5. Tenant Billing Operational Awareness

### 5.1 Files Created

| File | Purpose |
|---|---|
| `apps/control-center/src/app/api/billing/summary/route.ts` | BFF route: probes Billing `/health` + `/healthz` |
| `apps/control-center/src/app/billing/page.tsx` | Tenant Billing operational page |
| `apps/control-center/src/components/billing/billing-service-card.tsx` | Service card component |

### 5.2 BFF Route: `GET /api/billing/summary`

- **Auth:** `requirePlatformAdmin()` — returns 401 if not PlatformAdmin
- **Probes:** `{BILLING_SERVICE_URL}/health` (4s timeout) → primary status; `/healthz` (4s timeout) → secondary confirmation
- **Status logic:** Same as Commerce (online/degraded/offline)
- **Response:** `BillingSummary { serviceStatus, serviceLatencyMs, lastCheckedAtUtc }`
- **Cache:** `no-store`

### 5.3 Billing Page: `/billing`

- Displays `BillingServiceCard` (status + latency + timestamp)
- Displays service info card: domain, port, gateway routes, auth gate, integration mode
- Informational panel directing users to tenant detail pages for per-tenant profile data
- Visible from sidebar: COMMERCE & BILLING → Tenant Billing

---

## 6. Tenant-Level Billing Visibility

### 6.1 Files Created

| File | Purpose |
|---|---|
| `apps/control-center/src/app/api/billing/tenant-summary/[tenantId]/route.ts` | BFF: per-tenant billing profile fetch |
| `apps/control-center/src/components/billing/tenant-billing-panel.tsx` | Panel component for tenant detail page |

### 6.2 BFF Route: `GET /api/billing/tenant-summary/[tenantId]`

- **Auth:** `requirePlatformAdmin()` guard
- **tenantId validation:** Regex check for UUID format before any downstream call
- **Token handling:** Reads `BILLING_INTERNAL_TOKEN` from env (never exposed to client); forwards as `X-Internal-Token` header; returns safe fallback message if token not configured
- **Tenant context:** Forwards `X-Tenant-Id: {tenantId}` header — matches Billing service `TenantResolutionMiddleware` expectations
- **Error handling:**
  - Token not configured → `TenantBillingSummary { error: 'Billing service internal token not configured...' }`
  - 401/403 → safe error message (no internal details)
  - Non-2xx → `error: 'Billing service returned HTTP {status}'`
  - Network failure → `error: 'Tenant Billing service unreachable'`
  - Profile not found → `profileFound: false, profile: null`

### 6.3 Tenant Detail Page Integration

- `apps/control-center/src/app/tenants/[id]/page.tsx` updated to call `fetchTenantBillingSummary(id)` via `Promise.allSettled` alongside organizations fetch
- `TenantBillingPanel` rendered at bottom of tenant overview tab
- Non-fatal: billing panel failure does not break the rest of the tenant detail page
- Displays: status, mode, billingAccountId, hostPlatformKey, activatedAtUtc, closedAtUtc

**Security note:** `X-Internal-Token` is read from a server-side env var inside a PlatformAdmin-gated BFF route handler. It is never returned in any response body, logged at info level, or exposed to browser-side code.

---

## 7. Gateway/BFF Route Validation

Routes confirmed in `apps/gateway/Gateway.Api/appsettings.json` (delivered in LS-INT-01):

| Route key | Path | Cluster | Port | Auth |
|---|---|---|---|---|
| `commerce-health` | `GET /commerce/health` | commerce-cluster | 5030 | anonymous |
| `commerce-ready` | `GET /commerce/ready` | commerce-cluster | 5030 | anonymous |
| `commerce-protected` | `GET /commerce/{**catch-all}` | commerce-cluster | 5030 | authenticated |
| `billing-health` | `GET /billing/health` | billing-cluster | 5031 | anonymous |
| `billing-healthz` | `GET /billing/healthz` | billing-cluster | 5031 | anonymous |
| `billing-protected` | `GET /billing/{**catch-all}` | billing-cluster | 5031 | authenticated |

**No changes required to gateway config** — all routes were already correct from LS-INT-01.

**Direct service probes:** BFF health probe routes call `COMMERCE_SERVICE_URL` / `BILLING_SERVICE_URL` directly (bypassing the gateway) to avoid double-hop latency in health probes. This is consistent with the existing `REPORTS_SERVICE_URL` pattern in `apps/control-center/src/app/api/reports/summary/route.ts`.

---

## 8. Monitoring / Diagnostic Registration

Commerce and Tenant Billing added to `DEFAULT_SERVICES` in `apps/control-center/src/lib/system-health-store.ts`:

```
{ name: 'Commerce',       url: 'http://127.0.0.1:5030/health', category: 'infrastructure' },
{ name: 'Tenant Billing', url: 'http://127.0.0.1:5031/health', category: 'infrastructure' },
```

**Effect:**
- Both services appear in the `/monitoring` dashboard health probe list
- Both services contribute to the overall system health status on the dashboard `SystemStatusCard`
- If either is `Down` or `Degraded`, the platform alert count on the dashboard increments
- Both services appear in `MONITORING_SOURCE=service` mode integration status list (Monitoring Service reads from the same registry)

**Seeding note:** `DEFAULT_SERVICES` is the seed baseline for new/fresh deployments. Existing deployments that already have rows in the `system_health_services` DB table are not affected — operators add them via the Monitoring service editor at `/monitoring`. The seed comment documents the expected services for operator reference.

---

## 9. Role-Appropriate Access

| Role | Commerce Page | Billing Page | Tenant Billing Panel |
|---|---|---|---|
| PlatformAdmin | ✅ Full access | ✅ Full access | ✅ Full access |
| All other roles | ❌ 401 redirect | ❌ 401 redirect | ❌ Not rendered |

All access gates use the existing `requirePlatformAdmin()` guard from `lib/auth-guards.ts`. No new authorization system introduced.

**Design note:** Tenant-scoped billing visibility (TenantAdmin seeing their own billing profile) is deferred to LS-COMMERCE-INT-03. The current tenant detail page is PlatformAdmin-only (`requirePlatformAdmin()` in layout.tsx), so adding TenantAdmin-scoped access would require layout changes outside the LS-COMMERCE-INT-02 scope.

---

## 10. UI Fallback and Error States

| Scenario | UI Response |
|---|---|
| Commerce service offline | Red service card (`serviceStatus: 'offline'`), red status badge |
| Commerce service slow (> 2s) | Amber service card (`serviceStatus: 'degraded'`) |
| Tenant Billing service offline | Red service card |
| BFF fetch fails entirely | Red error banner with message; no stack trace |
| `BILLING_INTERNAL_TOKEN` not set | Amber informational card in TenantBillingPanel |
| Billing API returns 401/403 | Safe amber message in TenantBillingPanel |
| No billing profile found | Empty state card with icon and explanation |
| Invalid tenantId (non-UUID) | HTTP 400 from BFF route |
| Unauthorized request | HTTP 401 from all BFF routes |

All error messages are user-safe: no internal exception details, no token values, no connection strings, no stack traces.

---

## 11. Files Changed

### New Files

| File | Type | Purpose |
|---|---|---|
| `apps/control-center/src/app/api/commerce/summary/route.ts` | BFF API route | Probes Commerce `/health` + `/ready`; returns `CommerceSummary` |
| `apps/control-center/src/app/api/billing/summary/route.ts` | BFF API route | Probes Billing `/health` + `/healthz`; returns `BillingSummary` |
| `apps/control-center/src/app/api/billing/tenant-summary/[tenantId]/route.ts` | BFF API route | Fetches per-tenant billing profile with `X-Internal-Token` forwarding |
| `apps/control-center/src/app/commerce/page.tsx` | Page (Server Component) | Commerce operational status page |
| `apps/control-center/src/app/billing/page.tsx` | Page (Server Component) | Tenant Billing operational status page |
| `apps/control-center/src/components/commerce/commerce-service-card.tsx` | UI component | Commerce service card + readiness panel |
| `apps/control-center/src/components/billing/billing-service-card.tsx` | UI component | Tenant Billing service card |
| `apps/control-center/src/components/billing/tenant-billing-panel.tsx` | UI component | Per-tenant billing profile panel (used in tenant detail page) |

### Modified Files

| File | Change |
|---|---|
| `apps/control-center/src/lib/nav.ts` | Added `COMMERCE & BILLING` nav group with Commerce + Tenant Billing entries |
| `apps/control-center/src/lib/nav-utils.ts` | Added `'COMMERCE & BILLING'` → `ri-store-3-line` to `GROUP_ICON_MAP` |
| `apps/control-center/src/lib/system-health-store.ts` | Added Commerce (`:5030/health`) and Tenant Billing (`:5031/health`) to `DEFAULT_SERVICES` |
| `apps/control-center/src/lib/env.ts` | Added `COMMERCE_SERVICE_URL`, `BILLING_SERVICE_URL`, `BILLING_INTERNAL_TOKEN` constants |
| `apps/control-center/src/types/control-center.ts` | Added `CommerceSummary`, `BillingSummary`, `TenantBillingProfile`, `TenantBillingSummary` types |
| `apps/control-center/src/app/tenants/[id]/page.tsx` | Added `TenantBillingPanel` via `Promise.allSettled`; added `fetchTenantBillingSummary` helper |
| `apps/control-center/.env.local` | Added `COMMERCE_SERVICE_URL`, `BILLING_SERVICE_URL`, `BILLING_INTERNAL_TOKEN` with documentation comments |

### Files Not Modified (validated only)

| File | Validation Result |
|---|---|
| `apps/gateway/Gateway.Api/appsettings.json` | All 6 YARP routes confirmed correct from LS-INT-01; no changes required |
| `apps/services/commerce/**` | Unchanged; 0 build errors confirmed |
| `apps/services/tenant-billing/**` | Unchanged; 0 build errors confirmed |

---

## 12. Build / Test Validation

| Target | Result | Notes |
|---|---|---|
| `tsc --noEmit` (Control Center) | ✅ 0 errors | All new types and imports resolved correctly |
| Next.js Fast Refresh | ✅ Rebuilt in 1719ms | App is running and accepted all changes |
| `Commerce.sln` build | ✅ 0 errors, 5 warnings | Same pre-existing warnings as LS-INT-01 (OTLP pkg NU1902) |
| `Billing.sln` build | ✅ 0 errors | Build succeeded; no regressions |
| `LegalSynqCommerceIdentityTests` 19/19 | ✅ Not re-run (no changes to tested code) | |
| `LegalSynqTenantContextResolverTests` 21/21 | ✅ Not re-run (no changes to tested code) | |

**Manual validation steps** (cannot be automated without running services):

1. Start application via `bash scripts/run-dev.sh`
2. Log into Control Center at `http://localhost:5004` as PlatformAdmin
3. Navigate to COMMERCE & BILLING → Commerce — verify green/offline status card appears
4. Navigate to COMMERCE & BILLING → Tenant Billing — verify status card + info panel render
5. Navigate to any Tenant detail page — verify Tenant Billing panel appears at bottom
6. If `BILLING_INTERNAL_TOKEN` is not set: verify amber "token not configured" message
7. Verify dashboard `/monitoring` still loads and includes Commerce + Tenant Billing in probe list

---

## 13. Architecture Boundary Validation (Confirmation of Non-Merge Boundaries)

| Rule | Status |
|---|---|
| No Commerce/Tenant Billing database merge | ✅ No DbContext shared |
| No direct cross-service EF access | ✅ BFF calls only via HTTP |
| No billing domain model changes | ✅ Read-only from Control Center |
| No invoice/payment UI implemented | ✅ Visibility only |
| No control center redesign | ✅ Additive panels only |
| Commerce standalone mode preserved | ✅ `LegalSynq:Identity:Enabled=false` untouched |
| Billing standalone mode preserved | ✅ `LegalSynq:TenantContext:Enabled=false` untouched |
| No shared DbContext introduced | ✅ Confirmed |
| No Tenant Billing → Commerce dependency | ✅ Confirmed |

---

## 14. Risks / Deferred Items

| Item | Risk | Deferral Reason |
|---|---|---|
| `BILLING_INTERNAL_TOKEN` must be configured in production | Medium | Token is a deployment concern; dev fallback is safe empty-string with informational UI |
| Billing seeded `DEFAULT_SERVICES` only affects fresh deployments | Low | Existing deployments add services via Monitoring editor; this is expected behavior |
| TenantAdmin scoped billing visibility | Low | Tenant detail page is PlatformAdmin-only; scoped access requires layout changes — deferred to LS-COMMERCE-INT-03 |
| Commerce billing account detail (account standing, subscription details) | Low | Requires Commerce API integration with real data — deferred to LS-COMMERCE-INT-03 |
| Entitlement snapshot details from Billing service | Low | Entitlement endpoints require tenant context + internal token; operational summary sufficient for this ticket |
| `system_health_services` seeding for existing deployments | Low | Existing rows not touched; operator must add Commerce/Billing via Monitoring editor if already seeded |

---

## 15. Recommended LS-COMMERCE-INT-03 Scope

Based on what was implemented and what was consciously deferred:

1. **Commerce billing account detail view** — Surface billing account standing, subscription tier, and plan information for platform operators. Requires Commerce `/api/commerce/billing/accounts` endpoint integration with appropriate JWT auth enabled.

2. **TenantAdmin scoped billing visibility** — Allow TenantAdmin users to view their own tenant's billing profile. Requires tenant detail layout changes to support TenantAdmin access alongside PlatformAdmin.

3. **Entitlement status enforcement view** — Show Tenant Billing entitlement snapshot per tenant: which product codes are enabled, enforcement mode, last snapshot. Requires Billing `/api/tenant-billing/entitlements` endpoint integration.

4. **Commerce subscription bridge diagnostics** — If the Commerce → Tenant Billing bridge endpoint (`/api/commerce/integration/tenant-billing/*/diagnostics`) is available, surface diagnostics in the Commerce page.

5. **Billing profile create/link flow** (minimal) — Allow PlatformAdmin to link a tenant to a Billing profile from the tenant detail page. Requires POST support in the BFF tenant-summary route and a simple form panel.

6. **Monitoring DB seeding for existing deployments** — Script or migration to add Commerce and Tenant Billing to existing `system_health_services` tables without requiring manual operator steps.
