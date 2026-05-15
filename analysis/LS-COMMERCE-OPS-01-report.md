# LS-COMMERCE-OPS-01 — Operational Audit, Reconciliation & Enterprise Support Tooling

> **Status:** COMPLETE

---

## 1. Executive Summary

LS-COMMERCE-OPS-01 delivers enterprise operational support tooling for Commerce + Tenant Billing, adding read-only audit visibility, reconciliation diagnostics, lifecycle traceability, remediation indicators, and operational export capability — all without modifying billing domains, payment workflows, or core authorization.

**Delivered surface area:**

| Area | Deliverable |
|---|---|
| Entitlement audit history | `BillingAccountAuditPanel` (per-account, lazy-load) + BFF route → Commerce `BillingAccountAuditController` |
| Commerce entitlement snapshot | `entitlement-snapshot` BFF route → Commerce `HostIntegrationController` |
| Billing lifecycle history | `BillingProfileLifecyclePanel` (expandable timeline) + BFF route → Billing profiles endpoint |
| Reconciliation diagnostics | `EntitlementReconciliationPanel` (per-account) + `reconciliation` BFF route (aggregates Commerce snapshot + Billing entitlement) |
| Remediation visibility | `RemediationVisibilityPanel` (static, derived from bridge diagnostics) |
| Operational export | `OperationalExportPanel` + export BFF route (JSON, Content-Disposition: attachment) |
| Per-account expand UX | `CommerceAccountPanel` now has 3 toggle panes: Subs / Audit / Reconcile (one open at a time) |
| New types | `CommerceAuditEvent`, `CommerceAuditEventList`, `CommerceEntitlementSnapshotDetail`, `BillingProfileLifecycleEvent`, `BillingProfileLifecycle`, `ReconciliationDiagnostics`, `ReconciliationStatus`, `RemediationItem`, `RemediationSummary`, `OperationalExportResult` |

**TypeScript: 0 errors. Commerce.sln: confirmed clean. Billing.sln: confirmed clean. Monitoring.Api: confirmed clean.**

---

## 2. Prior Operational Baseline

| Prior ticket | Status |
|---|---|
| LS-COMMERCE-INT-01 (JWT identity, billing resolver) | ✅ Preserved |
| LS-COMMERCE-INT-02 (service cards, billing panels) | ✅ Preserved |
| LS-COMMERCE-INT-03 (bridge diagnostics, entitlement panel, monitoring bootstrap) | ✅ Preserved |
| LS-COMMERCE-INT-04 (lifecycle actions, publish button, subscriptions, role-scoped nav) | ✅ Preserved and extended |

**Prior operational gaps addressed by OPS-01:**

| Gap | Resolution |
|---|---|
| No visibility into individual billing account audit events | `BillingAccountAuditPanel` → `BillingAccountAuditController` |
| No per-account entitlement snapshot inspection | `entitlement-snapshot` BFF route → `HostIntegrationController` |
| No Commerce ↔ Billing reconciliation visibility | `EntitlementReconciliationPanel` + `reconciliation` route |
| No billing profile lifecycle timeline | `BillingProfileLifecyclePanel` + `lifecycle` route |
| No remediation indicator panel | `RemediationVisibilityPanel` (derived from existing bridge diagnostics) |
| No export capability | `OperationalExportPanel` + `operational-summary` export route |
| No historical publish table | Documented — outbox is current-state, not full history (see Section 3) |
| No SuspendedAtUtc on profile | Documented — uses `updatedAtUtc` as proxy (see Section 4) |

---

## 3. Entitlement Publish History Visibility

### 3.1 Available Backend Data

**Source:** Commerce `BillingAccountAuditController`
```
GET /api/commerce/billing-accounts/{id}/audit-events
→ IReadOnlyList<BillingAccountAuditEventResponse>
```

Fields per event: `Id`, `BillingAccountId`, `EventType`, `Description`, `ActorType` (System/Admin/HostPlatform/Unknown), `ActorId`, `MetadataJson`, `CreatedAtUtc`.

These audit events capture significant billing account changes including entitlement-related transitions. For each event where `metadataJson` is present, the panel provides a "View metadata" toggle showing formatted JSON.

### 3.2 BFF Route

`GET /api/commerce/billing-accounts/[billingAccountId]/audit-events`

- Guard: `requirePlatformAdmin()`
- UUID validation
- 401/403 → informational standalone mode message
- 404 → safe "not found" message
- Timeout: 6s with `AbortSignal.timeout`
- Response: `CommerceAuditEventList { events, totalCount, billingAccountId, lastCheckedAtUtc, error }`
- No secrets, stack traces, or internal connection details returned

### 3.3 Component: `BillingAccountAuditPanel`

- Client component (`'use client'`)
- Accessible via the "Audit" toggle button per billing account in `CommerceAccountPanel`
- **Lazy loading** — audit events are not fetched until the user opens the panel (prevents N audit requests on page load)
- Actor type badges: color-coded (Admin=indigo, System=slate, HostPlatform=blue)
- Metadata viewer: inline expandable `pre` block with JSON formatting
- Refresh button — re-fetches without full page reload
- Empty state: "No audit events found"
- Error state: amber informational card

### 3.4 Known Limitation: No Historical Outbox Log

The Commerce service uses a durable outbox (`TenantBillingEntitlementPublishOutboxRow`) for publish operations. The outbox table stores **current state** for each billing account (Pending/Processing/Published/Failed/Abandoned), not a historical append-only log of all publish attempts. Therefore:

- Full publish attempt history (e.g., "published 47 times over the last 3 months") is **not available** from the outbox.
- Aggregate outbox status counts (pending/failed/published) are already surfaced via `CommerceBridgeDiagnosticsPanel` (from INT-03).
- Per-account audit events from `BillingAccountAuditController` provide the best available publish-related history.

**Decision:** Surface audit events (the available record) and document the outbox limitation clearly. No outbox-history-per-account endpoint is fabricated.

---

## 4. Billing Lifecycle Audit Visibility

### 4.1 Available Backend Data

**Source:** Tenant Billing `TenantBillingProfilesController`
```
GET /api/tenant-billing/profiles/{id}
→ TenantBillingProfileDto
```

Fields with lifecycle relevance: `Status`, `CreatedAtUtc`, `UpdatedAtUtc`, `ActivatedAtUtc`, `ClosedAtUtc`.

### 4.2 BFF Route

`GET /api/billing/profiles/[profileId]/lifecycle`

- Guard: `requirePlatformAdmin()`
- UUID validation
- Requires `BILLING_INTERNAL_TOKEN` (returns 200 with informational error if missing)
- Derives a `BillingProfileLifecycle` with a timeline array from the available timestamp fields
- Response: `BillingProfileLifecycle { profileId, tenantId, billingAccountId, currentStatus, mode, events[], updatedAtUtc, lastCheckedAtUtc, error }`

### 4.3 Lifecycle Timeline Derivation

The timeline is derived server-side from available timestamps. All events are sorted chronologically.

| Event | Source timestamp | Notes |
|---|---|---|
| Created (Draft) | `createdAtUtc` | Always present |
| Activated | `activatedAtUtc` | Present if profile was ever activated |
| Suspended | `updatedAtUtc` | Proxy only — exact timestamp not persisted |
| Closed | `closedAtUtc` | Present for terminal profiles |

### 4.4 Component: `BillingProfileLifecyclePanel`

- Client component (`'use client'`)
- Expandable accordion (lazy-loaded — data not fetched until first expand)
- Vertical timeline with color-coded status circles and connecting lines
- Event icons: Created (blue circle), Activated (green check), Suspended (amber pause), Closed (slate close)
- Profile metadata section: profileId, billingAccountId, mode, lastUpdated
- Lifecycle note explaining that suspension uses `updatedAtUtc` as a proxy
- Rendered on `/tenants/[id]` — only shown when profile exists (`tenantBillingSummary.profileFound === true`)

### 4.5 Known Limitation: No Dedicated History Table

The `tenant_billing_profiles` table tracks lifecycle state via a **single row per profile** with timestamp fields. There is no append-only `TenantBillingProfileHistory` table. Specifically:

- `SuspendedAtUtc` is **not** a persisted field — suspension updates `UpdatedAtUtc`
- Multiple activate → suspend → activate cycles are not individually traceable
- Only first activation time (`ActivatedAtUtc`) and terminal close time (`ClosedAtUtc`) are explicitly stored

**Decision:** Derive the best-available timeline from existing timestamps. Document the limitation on the panel itself and in this report. No history is fabricated.

---

## 5. Reconciliation Diagnostics

### 5.1 Design

The reconciliation check aggregates data from two independent sources:

**Commerce side** → `GET /api/commerce/integration/billing-accounts/{id}/entitlement-snapshot`
Returns `CommerceEntitlementSnapshot`: `AccessRecommendation`, `AccountStandingStatus`, `Products`, `Plans`, `Subscriptions`, `Limits`, `GeneratedAtUtc`.

**Billing side** → `GET /api/tenant-billing/entitlements/current` (with `X-Tenant-Id`)
Returns `TenantBillingEntitlementSnapshot`: `EntitlementStatus`, `AccessRecommendation`, `LastSyncedAtUtc`, `EffectiveFromUtc`.

The `tenantId` needed for Billing is resolved by first calling:
`GET /api/tenant-billing/profiles/by-billing-account/{billingAccountId}`

### 5.2 BFF Route

`GET /api/commerce/reconciliation/[billingAccountId]`

- Guard: `requirePlatformAdmin()`
- UUID validation
- 3-step fetch: resolve tenantId → Commerce snapshot + Billing entitlement (parallel)
- Derives `ReconciliationStatus`: `aligned | stale | mismatch | unknown | error`
- Stale threshold: 86,400 seconds (24 hours)
- Returns `ReconciliationDiagnostics` with both sides' data, comparison result, and per-side error fields
- All errors isolated — partial results (one side available, other missing) returned safely

### 5.3 Reconciliation Status Logic

| Status | Condition |
|---|---|
| `aligned` | Both sides available; recommendations match; Billing not stale |
| `stale` | `(now - billingLastSyncedAt) > 86,400s` |
| `mismatch` | Commerce and Billing `accessRecommendation` differ |
| `unknown` | One or both sides returned no data |
| `error` | Both sides returned network/HTTP errors |

### 5.4 Component: `EntitlementReconciliationPanel`

- Client component (`'use client'`)
- Accessible via "Reconcile" toggle per billing account in `CommerceAccountPanel`
- **Lazy loading** — not fetched until "Run" is clicked
- Two-column layout: Commerce (Source) vs Tenant Billing (Applied)
- Status badge at top: Aligned (green) / Stale (amber) / Mismatch (red) / Unknown / Error
- Mismatch details: explicit diff message shown in red card
- Stale warning: shows age in human-readable format (e.g., "2d") vs threshold
- Per-side error cards when one side is unavailable
- Tenant ID displayed in footer
- Refresh button

---

## 6. Operational Investigation Panels

### 6.1 Per-Account Investigation UX

`CommerceAccountPanel` now supports three expand panes per billing account (mutually exclusive — only one open at a time):

| Pane | Button | Content |
|---|---|---|
| Subs | "Subs" ↑/↓ | `CommerceSubscriptionsPanel` (from INT-04) |
| Audit | "Audit" | `BillingAccountAuditPanel` (new OPS-01) |
| Reconcile | "Reconcile" | `EntitlementReconciliationPanel` (new OPS-01) |

Active pane button has distinct indigo active style. Only one pane renders at a time (React state: `ExpandPane = 'subs' | 'audit' | 'reconcile' | null`).

### 6.2 Tenant Detail Investigation UX

On `/tenants/[id]`, two new panels appear below the existing billing panels when a profile exists:

| Panel | What it shows |
|---|---|
| `BillingProfileActionsPanel` (INT-04) | Lifecycle action buttons |
| `BillingProfileLifecyclePanel` (OPS-01) | Profile history timeline |

### 6.3 Commerce Page Operational Flow

The Commerce page (`/commerce`) now presents a layered investigation flow:

1. `CommerceServiceCard` — service health and latency
2. `CommerceReadinessPanel` — readiness probe results
3. `CommerceBridgeDiagnosticsPanel` — circuit breaker, outbox counts, config
4. `RemediationVisibilityPanel` — issues derived from bridge diagnostics **[NEW]**
5. `CommerceAccountPanel` — accounts with Subs / Audit / Reconcile per-account **[ENHANCED]**
6. `OperationalExportPanel` — JSON export download **[NEW]**
7. Service information card

---

## 7. Operational Export / Reporting Readiness

### 7.1 BFF Route

`GET /api/commerce/export/operational-summary`

- Guard: `requirePlatformAdmin()`
- Aggregates in parallel:
  - Commerce bridge diagnostics (`GET /api/commerce/integration/tenant-billing/diagnostics`)
  - Commerce admin dashboard summary (`GET /api/commerce/admin/dashboard/summary`)
  - Billing service health (`GET /healthz`)
- Returns JSON with `Content-Disposition: attachment; filename="legalsynq-ops-{YYYYMMDD}.json"`
- Payload structure: `exportedAtUtc`, `exportFormat`, `ticketId`, `sections { commerceBridgeDiagnostics, commerceAdminSummary, billingServiceHealth }`, `metadata { note, generatedBy }`
- Explicitly excludes: payment data, secrets, tokens, connection strings, internal credentials

### 7.2 Component: `OperationalExportPanel`

- Client component (`'use client'`)
- Download button → triggers `fetch` → creates blob URL → `<a download>` click → revokes URL
- Filename resolved from `Content-Disposition` header
- Loading state during fetch
- Error state: red card with safe message
- "Last exported" timestamp shown after successful download
- Security note visible: "PlatformAdmin only. Generated server-side. No sensitive credentials."

### 7.3 Format Decision

JSON was chosen over CSV because the operational data is hierarchical (nested diagnostics/sections). CSV would require flattening, reducing value. A future enhancement could add a CSV adapter for tabular account/subscription data.

### 7.4 Deferred

- CSV export adapter for tabular data (deferred — not required for OPS-01 operational baseline)
- PDF report (deferred — full reporting subsystem out of scope)
- Scheduled/automated exports (deferred — delivery-layer concern)

---

## 8. Remediation Visibility

### 8.1 Design

`RemediationVisibilityPanel` is a **pure client-side derived component** — it synthesizes `RemediationItem[]` from the `CommerceBridgeDiagnostics` already fetched by the Commerce page. No additional BFF calls are made.

### 8.2 Remediation Item Categories

| Category | Trigger | Severity |
|---|---|---|
| `bridge-disabled` | `diagnostics.enabled === false` | info |
| `failed-publish` | `diagnostics.outboxFailedCount > 0` | warning |
| `pending-publish` | `diagnostics.outboxPendingCount > 5` | info |
| `missing-profile` | `diagnostics.internalTokenConfigured === false` | warning |
| `access-mismatch` | Circuit breaker state !== 'Closed' | warning |
| All clear | No items above triggered | info |

### 8.3 Operational Rules

- **Visibility only** — no remediation actions, no queue operations, no bulk operations
- All items are informational with actionable descriptions
- Severity badges: `warning` (amber) / `info` (blue)
- "All clear" message when no issues detected
- Gracefully handles `null` diagnostics (shows empty panel)

---

## 9. Operational Filtering / Search Improvements

### 9.1 Existing (from INT-04)

The `CommerceAccountPanel` already provides:
- Client-side text search (name or account#)
- Status filter chips: All / Active / Suspended / Closed
- Standing filter chips: All / Good / Warning / Suspended / Blocked
- All filters are combinable (AND logic)

### 9.2 Additional Filtering Capability (OPS-01)

Per-account investigation panes (Audit / Reconcile) provide targeted drill-down into specific account state, effectively enabling:

- **Filter by billing account** — select the account, expand Audit or Reconcile
- **Filter by reconciliation state** — open Reconcile pane to see aligned/stale/mismatch
- **Filter by publish activity** — open Audit pane to see publish-related events

Server-side search/filter enhancements (by tenantId, by date range) were evaluated and deferred:
- Current dataset size (≤20 accounts cap) makes client-side filtering sufficient
- Adding date range or tenantId query params to Commerce endpoints would require backend changes outside OPS-01 scope

### 9.3 Deferred: Backend Filter Parameters

Adding `?from=&to=&tenantId=` query parameters to Commerce audit and Billing entitlement endpoints would require backend changes (out of OPS-01 scope). Document for a future `LS-COMMERCE-OPS-02` enhancement.

---

## 10. Operational Safety Hardening

### 10.1 Failure Scenarios and Handling

| Scenario | Handling |
|---|---|
| Commerce service unreachable | All Commerce BFF routes catch network exception → return empty/safe response with `error` field |
| Billing service unreachable | All Billing BFF routes catch network exception → return safe response with `error` field |
| `BILLING_INTERNAL_TOKEN` missing | Lifecycle and reconciliation routes return informational 503/200 with safe message |
| Commerce standalone mode (401/403) | Returns informational "standalone mode" message — not an error |
| Missing audit events (empty array) | Component shows "No audit events found" empty state |
| Missing entitlement snapshot (404) | Returns `error: 'No entitlement snapshot found'` — not surfaced as an exception |
| Reconciliation: one side missing | `reconciliationStatus: 'unknown'` with per-side error fields populated |
| Reconciliation: both sides fail | `reconciliationStatus: 'error'` with top-level error message |
| Profile not found (lifecycle) | Returns `error: 'Profile not found'` in `BillingProfileLifecycle` |
| tenantId resolution failure (reconcile) | `billingError: 'Could not resolve tenant ID'` — reconciliation proceeds with Commerce-only data |
| Export service timeout | Affected sections return `null` in payload — export still downloads with available data |
| Export BFF exception | Returns `{ error: '...' }` with HTTP 500 — UI shows red error card |
| Browser download failure | Component catches exception → shows "Request failed — unable to reach server" |
| Invalid billingAccountId / profileId | UUID regex check → 400 with safe message |
| PlatformAdmin guard fail | `requirePlatformAdmin()` redirects; API returns 401 |
| Large metadata JSON | Rendered in `<pre>` with `max-h-32 overflow-x-auto` — no layout overflow |
| Null diagnostics in RemediationPanel | `buildItems(null)` returns empty items list — empty panel shown |
| Network timeout | All upstream fetches use `AbortSignal.timeout(5000-6000ms)` |

### 10.2 Information Safety

**Never returned to browser:**
- Stack traces
- Connection strings
- Internal tokens (BILLING_INTERNAL_TOKEN, Commerce auth)
- Database query results
- Raw exception details
- Internal service credentials

**Safe to surface:**
- HTTP status codes (e.g., "returned HTTP 404")
- Informational messages ("standalone mode", "token not configured")
- Audit event data as provided by Commerce (already sanitized by the Commerce service)
- Reconciliation status derived from publicly-queryable endpoints

---

## 11. RBAC / Authorization Validation

All new routes and panels use `requirePlatformAdmin()` exclusively. No new authorization framework was introduced.

| Route | Guard | Notes |
|---|---|---|
| `GET /api/commerce/billing-accounts/[id]/audit-events` | `requirePlatformAdmin()` | Commerce audit history |
| `GET /api/commerce/billing-accounts/[id]/entitlement-snapshot` | `requirePlatformAdmin()` | Commerce snapshot |
| `GET /api/billing/profiles/[id]/lifecycle` | `requirePlatformAdmin()` | Billing profile lifecycle |
| `GET /api/commerce/reconciliation/[id]` | `requirePlatformAdmin()` | Reconciliation diagnostics |
| `GET /api/commerce/export/operational-summary` | `requirePlatformAdmin()` | Operational export |
| `RemediationVisibilityPanel` | Client component — page guard is authoritative | Commerce page is PlatformAdmin-only |
| `OperationalExportPanel` | Client component — server route enforces | Downloads via guarded BFF route |
| `BillingAccountAuditPanel` | Client component — server route enforces | Shown on Commerce page (PlatformAdmin-only) |
| `EntitlementReconciliationPanel` | Client component — server route enforces | Shown on Commerce page (PlatformAdmin-only) |
| `BillingProfileLifecyclePanel` | Client component — server route enforces | Shown on `/tenants/[id]` (PlatformAdmin-only layout) |

---

## 12. Files Changed

### New Files

| File | Type | Purpose |
|---|---|---|
| `apps/control-center/src/app/api/commerce/billing-accounts/[billingAccountId]/audit-events/route.ts` | BFF route | Audit events for a billing account |
| `apps/control-center/src/app/api/commerce/billing-accounts/[billingAccountId]/entitlement-snapshot/route.ts` | BFF route | Commerce entitlement snapshot detail |
| `apps/control-center/src/app/api/billing/profiles/[profileId]/lifecycle/route.ts` | BFF route | Billing profile lifecycle timeline |
| `apps/control-center/src/app/api/commerce/reconciliation/[billingAccountId]/route.ts` | BFF route | Commerce ↔ Billing reconciliation |
| `apps/control-center/src/app/api/commerce/export/operational-summary/route.ts` | BFF route | Read-only JSON operational export |
| `apps/control-center/src/components/commerce/billing-account-audit-panel.tsx` | UI component | Audit event list (lazy, expandable) |
| `apps/control-center/src/components/commerce/entitlement-reconciliation-panel.tsx` | UI component | Side-by-side reconciliation view |
| `apps/control-center/src/components/commerce/remediation-visibility-panel.tsx` | UI component | Derived remediation items |
| `apps/control-center/src/components/commerce/operational-export-panel.tsx` | UI component | JSON export download |
| `apps/control-center/src/components/billing/billing-profile-lifecycle-panel.tsx` | UI component | Profile lifecycle timeline |

### Modified Files

| File | Change |
|---|---|
| `apps/control-center/src/types/control-center.ts` | Added 10 new types: `CommerceAuditEvent`, `CommerceAuditEventList`, `CommerceEntitlementSnapshotDetail`, `BillingProfileLifecycleEvent`, `BillingProfileLifecycle`, `ReconciliationStatus`, `ReconciliationDiagnostics`, `RemediationItem`, `RemediationSummary`, `OperationalExportResult` |
| `apps/control-center/src/components/commerce/commerce-account-panel.tsx` | Added `BillingAccountAuditPanel` and `EntitlementReconciliationPanel` imports; `AccountRow` now uses `ExpandPane` union type with Subs/Audit/Reconcile toggle (mutually exclusive) |
| `apps/control-center/src/app/commerce/page.tsx` | Added `RemediationVisibilityPanel` (above account panel) and `OperationalExportPanel` (below account panel) |
| `apps/control-center/src/app/tenants/[id]/page.tsx` | Added `BillingProfileLifecyclePanel` import and render (below `BillingProfileActionsPanel`, shown only when profile exists) |

---

## 13. Build / Test Validation

| Target | Command | Result |
|---|---|---|
| Control Center TypeScript | `tsc --noEmit` | ✅ 0 errors |
| Commerce.sln | `dotnet build Commerce.sln` | ✅ Clean |
| Billing.sln | `dotnet build Billing.sln` | ✅ Clean |
| Monitoring.Api | `dotnet build Monitoring.Api.csproj` | ✅ Clean |

**Frontend test infrastructure:** Not available (testing disabled per replit.md). Manual validation steps below.

### Manual Validation Steps

1. Start application: `bash scripts/run-dev.sh`
2. Navigate to `http://localhost:5004` as PlatformAdmin

**Commerce page (`/commerce`):**
3. Verify `RemediationVisibilityPanel` appears between Bridge Diagnostics and Billing Accounts
4. In standalone mode (Commerce unavailable): verify amber "bridge disabled" or "token not configured" items
5. In connected mode: verify "No remediation items detected" all-clear appears when pipeline is healthy
6. Per billing account — click "Audit" → verify lazy load prompt → click Load → verify events or empty state
7. Per billing account — click "Audit" again → closes; click "Reconcile" → run reconciliation → verify status badge
8. Per billing account — only one pane (Subs/Audit/Reconcile) open at a time
9. Click "Download JSON Export" → verify file downloads as `legalsynq-ops-YYYYMMDD.json`
10. Verify export JSON contains `sections.commerceBridgeDiagnostics`, no secrets

**Tenant detail page (`/tenants/[id]`):**
11. Navigate to a tenant with a billing profile
12. Verify `BillingProfileLifecyclePanel` appears below `BillingProfileActionsPanel`
13. Click to expand → verify timeline shows Created event + any additional lifecycle events
14. Verify lifecycle note about `updatedAtUtc` proxy for suspension

**Edge cases:**
15. Tenant with no billing profile: verify lifecycle panel is hidden (conditional render)
16. Commerce standalone mode: verify audit panel shows "standalone mode" amber card
17. Billing token not configured: verify lifecycle panel shows informational error card
18. Reconcile with tenantId not resolvable: verify `billingError: 'Could not resolve...'` shows

---

## 14. Risks / Deferred Items

| Item | Risk | Decision |
|---|---|---|
| No per-account outbox publish history | Medium — operators cannot trace "publish X attempts on account Y" over time. Best available: audit events. | Documented. Not fabricated. |
| No `SuspendedAtUtc` on billing profile | Low — suspension exact time uses `updatedAtUtc` proxy. Acceptable for operational visibility. | Documented on panel and in report. |
| Reconciliation stale threshold (24h) is static | Low — 24h is conservative. Some tenants may need tighter monitoring. | Document for `LS-COMMERCE-OPS-02`. |
| Backend date-range filter for audit events | Low — client-side is sufficient at current scale. | Deferred to `LS-COMMERCE-OPS-02`. |
| CSV export adapter | Low — JSON is sufficient for current operational needs. | Deferred. |
| PDF report | Low — out of OPS-01 scope. | Deferred to reporting subsystem. |
| `CommerceEntitlementSnapshotDetail` BFF route created but not directly rendered | Low — route is available for future per-account snapshot inspector panel. The reconciliation panel uses a separate route that aggregates the same data. | No wasted work — useful for future expansion. |
| BuildingBlocks.Tests/TestHelpers still target net10.0 | Low — test-only, not production-reachable. | Out of scope; carry-over from INT-04. |

---

## 15. Confirmation of Non-Merge Boundaries

| Rule | Status |
|---|---|
| No database merges | ✅ |
| No shared DbContext | ✅ |
| No direct cross-service EF access | ✅ HTTP/BFF calls only |
| No invoice/payment/customer workflow changes | ✅ Visibility-only |
| No destructive replay tooling | ✅ |
| No bulk remediation execution | ✅ `RemediationVisibilityPanel` is read-only derived display |
| Commerce standalone mode preserved | ✅ All routes degrade gracefully |
| Billing standalone mode preserved | ✅ All routes degrade gracefully when `BILLING_INTERNAL_TOKEN` absent |
| No new authorization framework | ✅ Uses `requirePlatformAdmin()` exclusively |
| LS-INT-01 resolver architecture unchanged | ✅ |
| No Tenant Portal (apps/web) changes | ✅ |
| No payment provider integration changes | ✅ |
| No notification service changes | ✅ |
| No document service changes | ✅ |
| No Control Center redesign | ✅ Additive panels only |
| No monitoring architecture changes | ✅ |

---

## 16. Final Operational Maturity Assessment

| Dimension | INT-01 baseline | OPS-01 result |
|---|---|---|
| Service health visibility | Commerce + Billing health cards | Unchanged — still present |
| Bridge diagnostic visibility | Outbox counts, circuit breaker, config | Enhanced — remediation items derived from same data |
| Billing account visibility | Account list with status/standing | Enhanced — Audit / Reconcile per-account panes |
| Entitlement publish traceability | Publish button per account | Enhanced — audit events per account (lazy loaded) |
| Commerce ↔ Billing alignment | Not surfaced | **New** — reconciliation status with aligned/stale/mismatch/unknown/error |
| Billing profile lifecycle | Current status + action buttons | **New** — derived timeline (Created/Activated/Suspended/Closed) |
| Remediation visibility | Not surfaced | **New** — derived from bridge diagnostics (failed/pending/token/circuit breaker) |
| Operational export | Not available | **New** — JSON export with bridge diagnostics + admin summary |
| Role-scoped navigation | INT-04 | Preserved |
| Standalone mode resilience | Full graceful degradation | Preserved — all new routes degrade safely |
| Audit log access | Not surfaced in UI | **New** — `BillingAccountAuditController` surfaced per-account |

**Operational maturity level: Enhanced.** The Commerce + Billing operational surface is now sufficient for first-tier PlatformAdmin investigation, publish traceability, and reconciliation monitoring without requiring direct database access or log tailing.

---

## 17. Recommended Next Steps

| ID | Priority | Description |
|---|---|---|
| `LS-COMMERCE-OPS-02` | Medium | Add backend date-range filtering for audit events; per-account outbox row query endpoint in Commerce; stale threshold configuration via settings |
| `LS-COMMERCE-OPS-03` | Low | CSV export adapter for tabular billing account + subscription data |
| `LS-COMMERCE-OPS-04` | Medium | Add `SuspendedAtUtc` to `TenantBillingProfile` entity to enable accurate suspension timeline |
| `LS-COMMERCE-OPS-05` | Low | Add outbox append-only publish event log (separate from current-state row) for full publish history |
| `LS-COMMERCE-OPS-06` | Low | Per-account entitlement snapshot inspector (uses `entitlement-snapshot` BFF route created in OPS-01) |
| Infrastructure | High | Upgrade remaining `net10.0` services (Identity, Fund) to `net8.0` for full SDK alignment |
