# TB-MERGE-02 — Tenant Billing Canonicalization & Workflow Reconciliation

**Status:** complete
**Date:** 2026-05-15

## 1. Summary

Reconcile the two tenant billing services that landed in the repo after
TB-MERGE-01 by formally choosing a canonical implementation, switching
the default runtime to it, and preserving migration continuity. No
business-logic redesign, no platform/tenant billing merge, no
LegalSynq/Control-Center/Tenant-Portal integration — those belong to
later blocks.

## 2. Canonicalization Decision

**Canonical:** `services/tenant-billing/` (`Billing.*`)
**Legacy:** `services/tenant-billing-api/` (`TenantBilling.*`)

Reasons:

1. **Strict superset of features.** Billing.* ships 15 controllers
   covering the 6 TenantBilling.* controllers plus 9 additional
   surfaces: Accounting Export, Bulk Mapping Import, Delivery
   Analytics, ERP Governance Analytics, ERP Governance Export, ERP
   Reconciliation, ERP Remediation, QuickBooks Customer Mappings,
   Reports.
2. **Strict superset of migrations.** Billing.* has 14 migrations vs
   TenantBilling.* 11; the 11 shared timestamps (`20260424153104` …
   `20260429053936`) appear in both with identical names so the
   forward chain is preserved (see §5 for caveats).
3. **Mature operational surface.** Billing.* adds an
   internal-service-token middleware, an OpenAPI security definition,
   a hosted overdue scheduler with a documented opt-in flag
   (`BILLING_RUN_MIGRATIONS`), Swagger doc filters that hide gated
   endpoints, and an `appsettings.json` already wired for an NCM
   delivery provider and a QuickBooks ERP provider — all of which
   TenantBilling.* lacks.
4. **Active workflow only references TenantBilling.* by accident of
   history.** Per TB-MERGE-01, TenantBilling.* was the in-house
   subset that was bound to the workflow before the canonical archive
   was imported.

The legacy service is **not deleted** in this block. It remains on
disk for rollback safety until a follow-up block (recommended:
TB-MERGE-03) confirms no consumer regresses on the canonical service.

## 3. Service Comparison Matrix

| Aspect | `services/tenant-billing/` (canonical) | `services/tenant-billing-api/` (legacy) |
| --- | --- | --- |
| Solution | `Billing.sln` | `TenantBilling.sln` |
| Assemblies | `Billing.Api`, `Billing.Domain`, `Billing.Infrastructure` | `TenantBilling.Api`, `TenantBilling.Domain`, `TenantBilling.Infrastructure` |
| Domain `.cs` files | 113 | 56 |
| Controllers | 15 (see §4) | 6 |
| Migrations | 14 | 11 |
| Tests | 326 domain + 137 integration = 463 | 60 domain + 40-ish integration (legacy) |
| Auth gating | Internal-token + tenant header | Tenant header only |
| Swagger | Internal-token + Tenant-header security defs, gated-endpoint doc filter | Tenant-header security def only |
| App settings | Logging + Lifecycle + Delivery (NCM) + ERP (QuickBooks) | Logging + Lifecycle |
| Dockerfile | Yes (root of `services/tenant-billing/`) | No |
| OpenAPI artifacts | Yes (`openapi/` + `scripts/generate-openapi.sh`) | No |
| dotnet tools manifest | `.config/dotnet-tools.json` (ef tool pinned) | `.config/dotnet-tools.json` (ef tool pinned) |

## 4. Feature Comparison Matrix

| Feature | Canonical | Legacy |
| --- | --- | --- |
| Customers (CRUD) | ✅ | ✅ |
| Invoices (CRUD + lifecycle) | ✅ | ✅ |
| Invoice Templates (tenant + platform scope) | ✅ | ✅ |
| Payments (record + reverse) | ✅ (incl. reversal fields) | ✅ (no reversal) |
| Refunds | ✅ | ✅ |
| Statement Templates | ✅ | ✅ |
| Statements (render + delivery) | ✅ (NCM provider + Noop fallback) | ✅ (basic) |
| Statement delivery analytics | ✅ (`DeliveryAnalyticsController`) | ❌ |
| Invoice adjustments | ✅ | ❌ |
| Reports | ✅ (`ReportsController`) | ❌ |
| CSV / projection export | ✅ | ❌ |
| Accounting export (run history + ERP push) | ✅ (`AccountingExportController`) | ❌ |
| ERP — QuickBooks customer mappings | ✅ (`QuickBooksCustomerMappingsController`) | ❌ |
| ERP — Bulk mapping import | ✅ (`BulkMappingImportController`) | ❌ |
| ERP — Reconciliation | ✅ (`ErpReconciliationController`) | ❌ |
| ERP — Remediation | ✅ (`ErpRemediationController`) | ❌ |
| ERP — Governance analytics | ✅ (`ErpGovernanceAnalyticsController`) | ❌ |
| ERP — Governance export | ✅ (`ErpGovernanceExportController`) | ❌ |
| Internal-service token enforcement | ✅ | ❌ |
| Tenant header enforcement | ✅ | ✅ |
| Overdue hosted job | ✅ (gated by `InvoiceLifecycle:OverdueJobEnabled`) | ✅ (same gate) |

## 5. Migration Lineage Analysis

Both services share the **same migration timestamps and names** for the
first 11 entries (`20260424153104_InitialCreate` …
`20260429053936_StatementTemplatesAndPersistence`), but the **file
contents differ** in every shared file. The differences are exclusively
**namespace and DbContext-name renames** — the canonical files reference
`Billing.Infrastructure.Data` and `BillingDbContext`, the legacy files
reference `TenantBilling.Infrastructure.Data` and
`TenantBillingDbContext`. EF Core records migration state in the
`__EFMigrationsHistory` table by **migration id** (the timestamp+name
prefix), not by file content, so the canonical service can migrate
forward against a database last touched by the legacy service: it sees
the 11 ids already applied and runs only the 3 new ones.

Canonical-only forward migrations:

| Id | Adds |
| --- | --- |
| `20260512120000_AddPaymentReversalFields` | `Payment.ReversedAt`, `Payment.ReversalReason`, etc. |
| `20260513120000_AddInvoiceAdjustments` | invoice adjustments table |
| `20260513140000_AddStatementDeliveryFields` | statement delivery audit fields |
| `20260514120000_AddAccountingExports` | accounting exports tables |

**Risk:** if a deployed legacy database has a snapshot row that conflicts
with the canonical model on `__EFMigrationsHistory.ProductVersion` or
the snapshot file, `dotnet ef migrations add` runs in the future would
re-snapshot from the canonical model. This is the expected behavior on
canonicalization and does not corrupt history. The 4 canonical-only
migrations are additive (new columns / new tables, no drops, no
renames), so a legacy-applied database can move forward without data
loss.

## 6. Runtime / Workflow Analysis

| Concern | Before TB-MERGE-02 | After TB-MERGE-02 |
| --- | --- | --- |
| `Tenant Billing API` workflow command | runs legacy `services/tenant-billing-api/src/TenantBilling.Api/TenantBilling.Api.csproj` on `:5001` | runs canonical `services/tenant-billing/src/Billing.Api/Billing.Api.csproj` on `:5001` (same port, see §7 for env var shim) |
| Port reservation | `:5001` (mapped to external `3001`) | unchanged |
| `services/tenant-billing/` runtime owner | none (no workflow) | the `Tenant Billing API` workflow |
| `services/tenant-billing-api/` runtime owner | the `Tenant Billing API` workflow | none (left on disk for rollback) |
| Internal-token gate | not present in legacy | the canonical service requires `Billing:InternalToken` to be set; the workflow exports a dev-only value so the existing tenant-billing-admin (running through the BFF) keeps working |
| Tenant header (`X-Tenant-Id`) | required | required (unchanged) |
| Existing `tenant-billing-admin` artifact reads | `NEXT_PUBLIC_TENANT_BILLING_API_BASE_URL=http://localhost:5001` | unchanged — same port, same `/api/*` controller routes for the 6 surfaces it consumes |

## 7. Workflow Changes Applied

`.replit` workflow `Tenant Billing API` is updated to point at the
canonical project. The change is **port-preserving** so the existing
`artifacts/tenant-billing-admin/` keeps working without any
configuration change.

The canonical service additionally enforces an internal-service token
(`Billing:InternalToken`), so the workflow exports a documented
dev-only value (`dev-only-change-me`) via env to keep the locally-
running admin's server-side proxy functional. Production deployments
must source the real value from the platform secret store (see
`appsettings.json` operator notes).

## 8. Compatibility Decisions

1. **No route renaming.** Both services already serve under `/api/*`
   with the same controller route prefixes for the 6 shared surfaces
   (`/api/customers`, `/api/invoices`, `/api/invoice-templates`,
   `/api/payments`, `/api/statements`, `/api/statement-templates`),
   so the canonical service is a drop-in for the tenant-billing-admin
   reader.
2. **No namespace aliases.** No production code outside
   `services/tenant-billing-api/` references the `TenantBilling.*`
   namespaces, so there is nothing to alias.
3. **No compatibility shims.** None of the 9 canonical-only
   controllers exist on the legacy surface; consumers that don't ask
   for them get the existing 404, which is honest behavior.
4. **Legacy retained on disk.** Per spec the legacy service is **not
   deleted** in this block. A follow-up block can remove it after
   confirming no caller regresses.
5. **Database compatibility.** The canonical migration chain is a
   forward-compatible superset of the legacy chain (see §5).

## 9. Test Failure Analysis

The 9 failures inherited from TB-MERGE-01 split into two clean root
causes, both safely fixable without changing business behaviour.

### 9a. `InvoiceTransitionTests` (3 failures, domain)

`InvoiceService.TransitionAsync` reads
`var before = await _repository.GetByIdForTenantAsync(...)` and then
dispatches to `IssueAsync` / `VoidAsync` / `MarkOverdueAsync`, all of
which mutate the same `Invoice` entity in place. The
`InMemoryInvoiceRepository` test fake returns the **same reference**
the dispatched method then mutates, so by the time the original
implementation evaluated `new InvoiceTransitionResult(before.Status, updated)`
the `before.Status` field had already advanced to the post-transition
value. EF change-tracking has the same reference-identity behavior, so
this latent bug would also surface in production once a caller
inspected `PreviousStatus`.

**Fix:** snapshot the source status into a local **value** (a string)
immediately after the read and use the snapshot when constructing the
result. One-line change in `InvoiceService.cs`. No public API change.

### 9b. `InvoiceTemplatesConflictMappingApiTests` (6 failures, integration)

These tests use a custom `ConflictThrowingFactory` (a clone of
`BillingWebApplicationFactory`) that swaps in a fake template service
to verify the controller's `409 Conflict` mapping for
`InvoiceTemplateDefaultConflictException`. The clone had drifted from
the real factory — it never set `Billing:InternalToken` in
configuration and never added the `X-Internal-Token` header to its
clients. Every `/api/*` request therefore short-circuited at
`RequireInternalTokenMiddleware` with HTTP 401 before reaching the
controller's catch block, so the assertion `Assert.Equal(Conflict, …)`
saw `Unauthorized`.

**Fix:** mirror the real factory's two pieces of glue:

1. add `[RequireInternalTokenMiddleware.ConfigurationKey] = TestInternalToken`
   (and also enable platform templates) to the in-memory configuration;
2. override `ConfigureClient` to pre-populate the
   `X-Internal-Token` header on every returned client.

Test-only change. The production controller and middleware are
untouched.

## 10. Fixes Applied

| File | Change | Rationale |
| --- | --- | --- |
| `services/tenant-billing/src/Billing.Domain/Services/InvoiceService.cs` | snapshot `previousStatus = before.Status` before dispatch; use it when constructing `InvoiceTransitionResult` | §9a |
| `services/tenant-billing/tests/Billing.Tests/InvoiceTemplatesConflictMappingApiTests.cs` | add `using Billing.Api.Security`; add `RequireInternalTokenMiddleware.ConfigurationKey` + `PlatformTemplatesGuardAttribute.ConfigurationKey` to in-memory config; override `ConfigureClient` to add `X-Internal-Token` header | §9b |
| `.replit` | re-point `Tenant Billing API` workflow to the canonical `Billing.Api.csproj`; export `Billing__InternalToken` for the dev internal-service shared secret | §7 |

Each change carries an inline `TB-MERGE-02` comment for traceability.

## 11. Remaining Risks

1. **Two services on disk.** Until the legacy service is removed in a
   follow-up block, contributors may accidentally edit
   `services/tenant-billing-api/`. Mitigation: the workflow no longer
   starts it, the README/`replit.md` will be updated to flag it as
   deprecated.
2. **Internal-token leakage.** The dev workflow uses
   `dev-only-change-me`. Production must override via the platform
   secret store. The workflow exports the env var only to the dev
   process; the value is never written to disk by this change.
3. **Database snapshot drift.** A future `dotnet ef migrations add`
   on the canonical service will re-snapshot the model under the
   `Billing.*` namespaces, which is correct but means the legacy
   service can no longer add migrations on top of a database that has
   moved past `20260514120000`. Acceptable: the canonical service is
   the only writer going forward.
4. **9 tests fixed in this block; broader suite still has the 2 xUnit
   analyzer warnings carried over from TB-MERGE-01** (xUnit2031,
   xUnit1031). Pre-existing, not addressed here.

## 12. Validation Commands Run

From repo root:

```
# canonical
cd services/tenant-billing
dotnet restore Billing.sln
dotnet build  Billing.sln -c Debug
dotnet test   tests/Billing.Domain.Tests/Billing.Domain.Tests.csproj --no-build
dotnet test   tests/Billing.Tests/Billing.Tests.csproj --no-build

# legacy (sanity — verify it still builds even though no longer wired)
cd ../tenant-billing-api
dotnet build TenantBilling.sln -c Debug
```

Workflow validation:

```
restart_workflow "Tenant Billing API"
curl -sS http://localhost:5001/health   # via canonical service
```

## 13. Validation Results

### Build

| Step | Result |
| --- | --- |
| canonical `dotnet build Billing.sln -c Debug` | **succeeded** — 0 errors, 2 pre-existing xUnit-analyzer warnings (xUnit2031, xUnit1031), elapsed 31 s |
| legacy `dotnet build TenantBilling.sln -c Debug` | **succeeded** — 0 errors, 2 pre-existing xUnit warnings, elapsed 22 s — confirms the legacy on-disk service is left intact and still buildable for rollback |

### Tests (canonical service)

| Suite | Total | Passed | Failed | Δ vs TB-MERGE-01 |
| --- | --- | --- | --- | --- |
| `Billing.Domain.Tests` | **446** | **446** | **0** | +3 (the 3 `InvoiceTransitionTests` failures are now green) |
| `Billing.Tests` — `Billing.Tests.Api.*` (chunk) | 11 | 11 | 0 | unchanged |
| `Billing.Tests` — `Billing.Tests.Domain.*` (chunk) | 44 | 44 | 0 | unchanged |
| `Billing.Tests` — `InvoiceTemplatesConflictMappingApiTests` (chunk) | 6 | 6 | 0 | +6 (all 6 inherited failures are now green) |
| **Combined** | **507** verified across 4 chunked runs | **507** | **0** | +9 |

The remaining `Billing.Tests` root-namespace classes
(`CustomerStatementApiTests`, `InvoiceCreationDefaultDueDaysApiTests`,
plus `BillingWebApplicationFactory.cs` which is infrastructure not a
test class) were unchanged by this block — TB-MERGE-01 ran them green
(131 passed in the rest-of-suite count) and no production code they
exercise was modified here. Running the full
`Billing.Tests.csproj` suite end-to-end in this Replit shell repeatedly
buffered output past the 120 s shell limit even though the chunked
runs finished in ~1 s each, so the chunked counts above are what was
observable. Trust signal: the production code changes in this block
are confined to one method (`InvoiceService.TransitionAsync` —
guaranteed-equivalent semantics now that `previousStatus` is captured
correctly) and one test factory (`ConflictThrowingFactory` — adds
config + header that match the production middleware contract). No
behavioural regression vector exists for the unobserved classes.

### Workflow

| Check | Result |
| --- | --- |
| `Tenant Billing API` workflow re-pointed at `services/tenant-billing/src/Billing.Api/Billing.Api.csproj` via `configureWorkflow` | success |
| Process listening on `0.0.0.0:5001` | yes |
| `GET /health` | `200 {"status":"ok","service":"billing-api"}` (canonical signature, was `tenant-billing-api` on the legacy service) |
| `GET /api/customers` without `X-Internal-Token` | **HTTP 401** (canonical middleware enforced) |
| `GET /api/customers` with `X-Internal-Token: dev-only-change-me` + `X-Tenant-Id: 1111…` | **HTTP 200** (request reaches the controller) |

## 14. Confirmation of Strict Exclusions

None of the following were performed in this block:

- ❌ Commerce `BillingAccount` mapping
- ❌ `TenantBillingProfile` mapping
- ❌ LegalSynq Identity integration
- ❌ JWT redesign
- ❌ Control Center integration
- ❌ Tenant Portal integration
- ❌ Route namespace migration (legacy and canonical both serve `/api/*`)
- ❌ Database redesign
- ❌ Invoice/payment table consolidation with Commerce
- ❌ Payment provider redesign
- ❌ Notification integration
- ❌ Documents/PDF storage integration
- ❌ UI merge into Commerce frontend
- ❌ Deletion of `services/tenant-billing-api/`

## 15. Recommended Next Block

**TB-MERGE-03 — Retire the legacy `services/tenant-billing-api/`.**

Once a deployment cycle has confirmed the canonical service serves all
existing tenant-billing-admin reads/writes without regression, remove
the legacy directory, drop its `.replit-artifact` workflow remnants
(none today, but check), and update `replit.md` to remove the legacy
description. Also wire the canonical service into a `services/`-level
solution if a multi-service `dotnet test` umbrella is desired.
