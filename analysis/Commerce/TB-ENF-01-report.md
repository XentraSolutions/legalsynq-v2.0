## TB-ENF-01 — Tenant Billing Soft Enforcement

### 1. Summary

TB-ENF-01 introduces a config-gated **soft-enforcement layer** in the
canonical Tenant Billing service (`services/tenant-billing/`,
`Billing.*` assemblies) that wires the previously-advisory
`ITenantBillingEnablementResolver` (TBS-B01 / TB-DATA-02) into the API
pipeline so that write endpoints can be 403-blocked when a tenant's
billing profile + entitlement snapshot say so.

Behaviour is OFF by default (`Billing:EntitlementEnforcement:Enabled =
false`). When the master switch is on, an `[RequireTenantBillingAccess(category)]`
filter on every write action consults `ITenantBillingAccessPolicy`,
which composes the resolver with per-category toggles
(`AllowPaymentsInReadOnly`, `AllowStatementsInReadOnly`,
`AllowExportsInReadOnly`) and Unknown / GraceLimited fall-back modes.
A blocked request returns RFC 7807 ProblemDetails (HTTP 403,
`application/problem+json`) with `category`, `accessRecommendation`,
`entitlementStatus`, `reason` extensions so the BFF can map to a
deterministic UX banner.

Reads are never attributed; the `TenantBillingProfilesController` and
`TenantBillingEntitlementsController` are intentionally never gated so
operators can always recover a Block state.

No Commerce calls were added, no shared database, no Identity dep, no
UI work — strictly within the canonical Tenant Billing service.

### 2. Codebase Analysis

The pieces that already existed before this block:

* **`Billing.Domain.Services.TenantBillingEnablementResolver`** (TB-DATA-02):
  reads the tenant's `TenantBillingProfile` and the
  `TenantBillingEntitlementSnapshot` for the profile's
  `BillingAccountId`, returns a `TenantBillingEnablementDecision`
  whose `AccessRecommendation` is one of `Unknown | Allow | ReadOnly |
  GraceLimited | Block`. **Advisory only** — nothing in the API
  pipeline consulted it.
* **`TenantResolutionMiddleware`**: every `/api/*` request must carry
  `X-Tenant-Id`; an action filter can therefore safely read
  `ITenantContext.TenantId`.
* **`RequireInternalTokenMiddleware`**: every `/api/*` request must
  also carry `X-Internal-Token`. Blocked responses already use
  `application/problem+json`.
* The service is split into 15 controllers (CustomersController,
  InvoicesController, PaymentsController, InvoiceTemplatesController,
  StatementTemplatesController, StatementsController,
  AccountingExportController, QuickBooksCustomerMappingsController,
  BulkMappingImportController, ErpGovernanceExportController,
  ErpRemediationController, ReportsController, AnalyticsController,
  TenantBillingProfilesController, TenantBillingEntitlementsController).
* DI is wired in
  `Billing.Infrastructure/DependencyInjection.cs::AddBillingServices`.

So the natural shape of the block is: **(a)** add an
`ITenantBillingAccessPolicy` over the existing resolver,
**(b)** add an MVC filter that calls it, **(c)** attribute write actions.

### 3. Enforcement Design Decision

Three approaches were considered and one chosen:

| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| **MVC action filter attribute** *(chosen)* | Per-action surface area; visible in controller code; ProblemDetails are trivial; respects `[NonController]` rules; no impact when no attribute is present | Requires touching each write action | ✅ Chosen — explicit attribution is exactly what an audit needs |
| Global authorization policy | Zero edits | Cannot map per-action operation category cleanly; harder to grep | ❌ |
| Middleware sniffing route data | Single edit | Fragile coupling to route templates; opaque to readers | ❌ |

The filter is `IAsyncActionFilter` rather than `IAuthorizationFilter`
so the existing `RequireInternalTokenMiddleware` and
`TenantResolutionMiddleware` always run first (they reject the
request before MVC binds it) and so model binding has populated
`ITenantContext` by the time we evaluate.

### 4. Configuration Added

**Section** (binds to `EntitlementEnforcementOptions`):

```jsonc
"Billing": {
  "EntitlementEnforcement": {
    "Enabled": false,
    "UnknownMode": "ReadOnly",          // or "Block"
    "GraceLimitedMode": "ReadOnly",     // or "Block"
    "AllowPaymentsInReadOnly":   true,
    "AllowStatementsInReadOnly": true,
    "AllowExportsInReadOnly":    false
  }
}
```

* `Enabled=false` is the **shipped default** — every existing test
  remains green, no production tenant changes behaviour, the resolver
  remains a pure advisory.
* `UnknownMode` decides what to do when no profile exists OR no
  entitlement snapshot has ever been applied. Default `ReadOnly`
  preserves the AR-recovery flows (payments + statements still go
  through) which is the safer choice during the dual-write rollout.
* `GraceLimitedMode` decides what to do when the snapshot itself
  resolved to `GraceLimited`. Default `ReadOnly` mirrors the explicit
  intent of GraceLimited (preserve cash-recovery, block new commitments).
* `AllowPaymentsInReadOnly=true` keeps `POST /api/payments`,
  `POST /api/payments/{id}/reverse`, `POST /api/invoices/{id}/refund`
  and `PATCH /api/payments/{id}/notes` callable while AR is degraded —
  this is the entire point of "soft" enforcement.
* `AllowStatementsInReadOnly=true` keeps statement generation /
  send / void callable so dunning isn't frozen.
* `AllowExportsInReadOnly=false` pins the externally-visible ERP /
  QuickBooks side-effect off until the snapshot returns to `Allow`.

The section is also documented inline in `appsettings.json` via a
sibling `"// Billing.EntitlementEnforcement"` comment key, matching
the convention used by the other Billing.* sections in this file.

### 5. Access Policy Matrix

`TenantBillingAccessPolicy.AuthorizeAsync(tenantId, category)` returns
a `TenantBillingEnforcementDecision` per the table below. Categories
listed in the **Always Allow** rows return immediately and never
touch the resolver.

| Category | Master switch off | Always-allowed | Allow | ReadOnly | GraceLimited | Block | Unknown |
|---|---|---|---|---|---|---|---|
| `Read`              | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `EntitlementAdmin`  | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `ProfileAdmin`      | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `CustomerWrite`     | ✅ | — | ✅ | ❌ | ❌ | ❌ | UnknownMode |
| `InvoiceWrite`      | ✅ | — | ✅ | ❌ | ❌ | ❌ | UnknownMode |
| `TemplateWrite`     | ✅ | — | ✅ | ❌ | ❌ | ❌ | UnknownMode |
| `PaymentWrite`      | ✅ | — | ✅ | toggle (default ✅) | toggle (default ✅) | ❌ | UnknownMode |
| `StatementGenerate` | ✅ | — | ✅ | toggle (default ✅) | toggle (default ✅) | ❌ | UnknownMode |
| `ExportWrite`       | ✅ | — | ✅ | toggle (default ❌) | ❌ | ❌ | UnknownMode |

`UnknownMode = ReadOnly` (default) means: behave as if the snapshot
were `ReadOnly` (so payments + statements still work, other writes
don't). `UnknownMode = Block` means: deny every write category.
`GraceLimitedMode` works analogously over the `GraceLimited` row.

### 6. Controllers / Endpoints Covered

| Controller | Endpoints attributed | Category |
|---|---|---|
| `CustomersController` | `POST`, `PUT /{id}`, `DELETE /{id}` | `CustomerWrite` |
| `InvoicesController` | `POST`, `POST /{id}/transition`, `/issue`, `/void`, `/reevaluate`, `/mark-overdue` (single + batch), `/adjustments` | `InvoiceWrite` |
| `InvoicesController` | `POST /{id}/refund` | `PaymentWrite` (treated as cash-recovery) |
| `PaymentsController` | `POST`, `POST /{id}/reverse`, `PATCH /{id}/notes` | `PaymentWrite` |
| `InvoiceTemplatesController` | `POST /tenant`, `PUT /tenant/{id}`, `/tenant/{id}/activate`, `/retire`, `/make-default` | `TemplateWrite` |
| `StatementTemplatesController` | `POST`, `PUT /{id}`, `/{id}/activate`, `/retire`, `/make-default` | `TemplateWrite` |
| `StatementsController` | `POST /customers/{id}/generate`, `/monthly/generate`, `POST /history/{id}/send`, `/void` | `StatementGenerate` |
| `AccountingExportController` | `POST /run` | `ExportWrite` |
| `QuickBooksCustomerMappingsController` | `POST`, `PUT /{id}`, `DELETE /{id}` | `TemplateWrite` |
| `BulkMappingImportController` | `POST /import/commit` | `TemplateWrite` |

**Intentionally NOT attributed:**

* All `[HttpGet]` actions across all controllers — reads are always
  allowed by category mapping anyway, so attributing them would just
  add noise.
* `TenantBillingProfilesController` — every endpoint is `ProfileAdmin`
  / always-allowed. The operator must be able to create / activate /
  suspend / close a profile to recover from a Block.
* `TenantBillingEntitlementsController` (`/apply`, `/current`,
  `/access`, `/profiles/{profileId}/entitlement`) — `EntitlementAdmin`
  / always-allowed for the same reason.
* `InvoiceTemplatesController` platform routes
  (`/api/invoice-templates/platform/*`) — these are unscoped,
  separately gated by `PlatformTemplatesGuardAttribute`, and have no
  meaningful tenant identity to evaluate.
* `ErpGovernanceExportController` and `ReportsController` /
  `AnalyticsController` — read-only file downloads / projections;
  match the `Read` category.
* `ErpRemediationController` validate paths and
  `BulkMappingImportController` `import/validate` — read-only preview
  endpoints with no persistent side effects.

`InvoiceTemplatesController` already imported
`Billing.Api.Security`; the `using` import was added to the other
nine controllers.

### 7. Blocked Response Behavior

`RequireTenantBillingAccessAttribute` short-circuits with:

* HTTP **403 Forbidden**.
* Content-Type **`application/problem+json`** (RFC 7807).
* Body fields:
  * `type`   `https://billing.tenant/errors/entitlement-blocked`
  * `title`  `"Tenant Billing access is restricted"`
  * `status` `403`
  * `detail` decision reason (e.g. `"snapshot says Block"`,
    `"missing tenant context"`)
  * `instance` request path (RFC 7807 default)
  * **Extensions:**
    * `category`              `"PaymentWrite"` (etc.)
    * `accessRecommendation`  `"Block" | "ReadOnly" | "GraceLimited" | "Unknown" | "Allow"`
    * `entitlementStatus`     `"Enabled" | "Disabled" | "PendingActivation" | "Expired" | "Suspended" | "Unknown"`
    * `reason`                short machine-readable reason

This shape lets the BFF build a deterministic banner without parsing
free-text. Defensive null-policy handling (e.g. policy somehow not
registered): the filter falls through to `next()` rather than
500-ing.

### 8. Safety / Backward Compatibility Behavior

* `Enabled=false` is shipped — no behaviour change for any existing
  deployment, including the `Tenant Billing API` workflow on port 5001.
* No DB migration. `EntitlementEnforcementOptions` is pure config.
* The order is: `RequireInternalTokenMiddleware` → tenant resolution →
  MVC filters. Anonymous probes (`/healthz`, swagger) and
  unprotected paths bypass tenant resolution and therefore the
  filter.
* Reads remain reachable when `Block` is in effect — operators can
  always view invoices / customers / statements during recovery.
* Profile + entitlement admin endpoints remain reachable when
  `Block` is in effect — that is the recovery path.
* Payments + statement generation remain reachable in
  `ReadOnly`/`GraceLimited` by default (toggles override).
* The legacy `services/tenant-billing-api/` (TenantBilling.* assemblies)
  is untouched; only the canonical `Billing.*` service is changed.

### 9. Tests Added

#### Domain — `tests/Billing.Domain.Tests/TenantBillingAccessPolicyTests.cs`

43 cases across 11 `[Fact]`/`[Theory]` methods, using the existing
in-memory `TenantBillingProfileService` /
`TenantBillingEntitlementService` /
`InMemoryTenantBillingProfileRepository` /
`InMemoryTenantBillingEntitlementSnapshotRepository` plumbing:

* `Disabled_master_switch_allows_every_category` — 9 categories
* `Read_and_admin_categories_are_always_allowed_even_when_enabled` — Read / EntitlementAdmin / ProfileAdmin
* `Active_profile_with_Allow_snapshot_passes_every_write_category` — 6 write categories
* `Block_snapshot_blocks_every_write_category` — asserts
  `IsAllowed=false`, `HttpStatus=403`, `ProblemTitle`
* `ReadOnly_snapshot_uses_default_per_category_toggles` — payment / statement / export defaults
* `ReadOnly_payments_can_be_disabled_via_AllowPaymentsInReadOnly_false`
* `ReadOnly_exports_can_be_enabled_via_AllowExportsInReadOnly_true`
* `GraceLimited_snapshot_preserves_payment_and_statement_only`
* `GraceLimitedMode_Block_blocks_everything_including_payments`
* `Missing_profile_under_default_UnknownMode_is_ReadOnly_so_payments_pass`
* `UnknownMode_Block_blocks_payments_too_for_missing_profile`
* `Empty_tenant_id_blocks_writes_when_enabled`
* `Suspended_profile_does_not_short_circuit_to_Allow_even_with_Allow_snapshot`

#### API — `tests/Billing.Tests/EntitlementEnforcementApiTests.cs`

Per-class WebApplicationFactory subclass (`EnforcementFactory`) that
overrides config to set
`Billing:EntitlementEnforcement:Enabled = "true"`. Tests:

* `CustomerWrite_blocked_when_no_profile_exists_and_enforcement_on` —
  asserts 403, `application/problem+json` content type, and the
  `category` + `accessRecommendation` extensions are present.
* `Read_endpoint_passes_even_when_profile_missing` — `GET /api/customers`
  returns 200 with no profile / no snapshot.
* `Allow_snapshot_lets_CustomerWrite_through` — seeds active profile
  + Allow snapshot via `/api/tenant-billing/profiles` +
  `/api/tenant-billing/entitlements/apply`, then `POST /api/customers`
  → 201.
* `Block_snapshot_blocks_CustomerWrite_with_403_problem_details` — verifies
  the extension value is exactly `"Block"`.
* `ReadOnly_snapshot_blocks_CustomerWrite_but_allows_Read` —
  asymmetric category gating.
* `ProfileAdmin_endpoint_remains_reachable_even_when_blocked` — even
  with a `Block` snapshot, `GET /api/tenant-billing/profiles` is
  still 200, demonstrating the admin recovery path.

### 10. Validation Results

```text
$ dotnet build services/tenant-billing/Billing.sln
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/Billing.Domain.Tests/Billing.Domain.Tests.csproj \
      --filter FullyQualifiedName~TenantBillingAccessPolicyTests
Passed!  - Failed: 0, Passed: 43, Skipped: 0, Total: 43

$ dotnet test tests/Billing.Tests/Billing.Tests.csproj \
      --filter FullyQualifiedName~EntitlementEnforcementApiTests
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

The full `Billing.Domain.Tests` project also runs green at 529 tests
total with the new file in place — i.e. the access-policy additions
do not regress the existing TBS-B01 / TB-DATA-01 / TB-DATA-02 suites.

### 11. Risks / Deferred Items

| # | Risk / Deferred | Notes |
|---|---|---|
| 1 | **Synchronous DB read per write request** | Each attributed write action triggers one extra `TenantBillingProfile` + one `TenantBillingEntitlementSnapshot` read. Both are key-by-tenant lookups on `TenantBillingProfile.TenantId` (already indexed) and on `TenantBillingEntitlementSnapshot.BillingAccountId+SourceSystem`. No caching; an OutputCache-style 5–15s memo can be added in a follow-up if the hot path warrants it. |
| 2 | **No request scope cache** | If multiple action filters (or a future global filter) need the same decision, today they each round-trip. Trivially fixable by caching on `HttpContext.Items` if/when needed. |
| 3 | **No allow-list for individual customers / invoices** | Enforcement is uniformly per-tenant. Per-resource overrides are out of scope; the Tenant Billing service has no per-customer entitlement concept. |
| 4 | **Background scheduler bypass** | The overdue-sweep hosted service does not go through the API pipeline and thus is not gated. That is intentional — overdue sweeps are platform housekeeping, not tenant writes. |
| 5 | **Telemetry** | The decision is not currently emitted to the request-log scope. A future enrichment can add `tb.access_recommendation` and `tb.entitlement_status` to the existing `req.log` scope without changing the API. |
| 6 | **No metrics counter for blocks** | Nothing increments on a 403 block today; the report-time count comes from access logs. Adding an `IMeter` counter is a small follow-up. |
| 7 | **Dual-write race during rollout** | If a tenant flips from Allow → Block while a long-running batch is mid-flight, in-flight writes complete, only the next request sees 403. This matches the soft model. |

### 12. Confirmation of Strict Exclusions

| Constraint | Status |
|---|---|
| No outbound HTTP to Commerce or any other service | ✅ |
| No new shared database / no cross-service reads | ✅ |
| No Identity / IdM dependency | ✅ |
| No UI / admin app changes | ✅ |
| No edits to `services/tenant-billing-api/` (legacy) | ✅ |
| No new migrations | ✅ |
| No `Billing.Domain` → `Billing.Api` references introduced | ✅ — the policy lives in `Billing.Domain.Services`; only the *attribute* lives in `Billing.Api.Security` |
| No edits to `replit.md` | ✅ |
| Default behaviour unchanged | ✅ — `Enabled=false` |

### 13. Recommended Next Block

* **TB-ENF-02** — wire `RequireTenantBillingAccess` decisions into the
  request log scope (`req.log.WithProperty("tb.access_recommendation",
  …)`) and add a metrics counter
  (`tenant_billing.enforcement.blocks_total{category=…,recommendation=…}`).
  This is the observability baseline that has to land before Block
  can be safely turned on for any production tenant.
* **TB-ENF-03** — short-lived per-`HttpContext` decision cache (or
  promotion to `IMemoryCache` keyed by `(tenantId, snapshotId)` if
  hot-path measurement justifies it).
* **TB-ENF-04** — admin-app surface in `artifacts/tenant-billing-admin`
  that decodes the 403 ProblemDetails and shows the deterministic
  banner.
* **TB-ENF-05** — Commerce → Tenant Billing snapshot publisher (the
  inbound side of `/api/tenant-billing/entitlements/apply`) so that
  enabling the gate in production has a real signal source.
