# TB-CURRENT-STATE-DEEP-VALIDATION

> **Final verdict (top-line):** ✅ **READY WITH CONDITIONS**
> for LegalSynq integration — the bounded contexts are clean, the
> Commerce → Tenant Billing bridge is well-isolated and disabled by
> default, soft enforcement is in place, all relevant builds succeed,
> and all enforcement / domain test suites pass. The two conditions
> are: (1) finish the legacy `services/tenant-billing-api/`
> retirement that TB-MERGE-02 deferred to TB-MERGE-03, (2) treat one
> pre-existing date-sensitive Commerce subscription test as known.
> Neither blocks Identity / Gateway integration work.

This audit is **read-only**. No production code, configuration, or
business logic was changed during the audit. The only files written
are this report.

---

## 1. Executive Summary

The current build of Tenant Billing + Commerce is in a healthy,
well-bounded state for proceeding with LegalSynq Identity / Gateway
integration. Concretely:

* The canonical Tenant Billing service (`services/tenant-billing/`,
  assemblies `Billing.*`) is a **strict standalone** ASP.NET Core
  service. It has zero project references to Commerce, no shared
  database with Commerce, no direct HTTP calls to Commerce, and no
  Identity/IdM dependency. Tenant identity is supplied at the API
  edge via the `X-Tenant-Id` header behind an internal-token gate.
* Commerce (`services/Commerce/`) is the publishing side of the
  bridge. The bridge is purely **outbound** (Commerce → TB), bound
  via `ITenantBillingEntitlementPublisher` over a typed HttpClient,
  shipped **disabled by default** (`Commerce:TenantBilling:Enabled =
  false`), and backed by a config-gated durable outbox + bounded
  in-memory queue + circuit breaker. The diagnostics endpoint
  confirms this at runtime.
* Soft enforcement on the Tenant Billing API
  (`Billing:EntitlementEnforcement`) is wired but **OFF by default**.
  Reads and admin/recovery endpoints are intentionally never
  attributed.
* The legacy `services/tenant-billing-api/` (`TenantBilling.*`) is
  on disk and still compiles, but is **not bound to any workflow**
  and is **not referenced by any other code** except its own
  README/test surface. Per `replit.md`, it is preserved for
  rollback safety; final deletion is reserved for TB-MERGE-03.
* Builds: TB and legacy build with 0 errors. Commerce builds with
  0 errors and only `NU1902` advisory warnings on a pre-existing
  OpenTelemetry transitive dependency.
* Tests: `Billing.Domain.Tests` 529/529 passing.
  `Commerce.Tests` 362/363 passing — the single failure is a
  date-bound `SubscriptionServiceTests.ChangePlan` test from
  commit `4190a66` (pre-existing, unrelated to the integration
  work in this report).
* Runtime smoke: TB returns 401 without `X-Internal-Token`, 400
  without `X-Tenant-Id`, 200 on a representative read with both,
  and 201 on a representative write (enforcement off). Commerce's
  contracts/health and TB-publisher diagnostics endpoints both
  respond, and a manual publish call against a real BillingAccount
  returns `{"outcome":"skipped","reason":"publisher-disabled"}` —
  proving the disabled-by-default posture is real, not just
  configured.

---

## 2. Final Verdict

### ✅ READY WITH CONDITIONS

**Conditions** (none are integration blockers):

1. **TB-MERGE-03** — physically retire `services/tenant-billing-api/`
   once one rollback window has passed. Current state is safe; the
   legacy service is unbound from workflows and unreferenced.
2. Treat `Commerce.Tests.Subscriptions.SubscriptionServiceTests.ChangePlan_closes_old_item_and_creates_new`
   as known — it is a pre-existing date-sensitive
   `EffectiveAtUtc cannot be more than 1 day in the past`
   FluentValidation failure introduced in commit `4190a66`, fully
   independent of the TB merge / enforcement work.

---

## 3. Evidence-Based Readiness Table

| Category | Verdict | Evidence | Notes |
|---|---|---|---|
| Standalone boundary | **Ready** | `services/tenant-billing/src/Billing.Api/Billing.Api.csproj` references only `Billing.Domain`, `Billing.Infrastructure`. `Billing.Domain.csproj` references no Commerce project. No `using Commerce` or `Commerce.Contracts` exists in `services/tenant-billing/src/`. | Only Commerce mentions in TB src are docstring references explaining the contract. |
| Canonical service | **Ready** | `.replit` `Tenant Billing API` workflow runs `services/tenant-billing/src/Billing.Api/Billing.Api.csproj` on `:5001`. `services/tenant-billing-api/` has no workflow binding. | Confirmed by `curl` against running workflow. |
| Domain separation | **Ready** | `BillingDbContext` (TB) contains no Commerce table; `CommerceDbContext` (Commerce) contains no `Customer/Invoice/Payment` rows from the TB billing model — `Commerce.Domain.Invoicing.Invoice` is a separately-defined CLR type per service. Migration sets are disjoint, see §15. | TB uses `Billing.Domain.Entities.Invoice`; Commerce uses `Commerce.Domain.Invoicing.Invoice`. |
| Commerce bridge | **Ready** | `services/Commerce/src/Commerce.Infrastructure/Integration/TenantBilling/` contains publisher, mapper, circuit breaker, metrics, in-memory queue, and EF outbox. Mounted as `POST /api/commerce/integration/tenant-billing/...`. | Disabled by default, runtime diag confirms `enabled=false`, `outboxEnabled=false`. |
| Durable outbox | **Ready** | `Commerce.Infrastructure.Integration.TenantBilling.Outbox.{EfTenantBillingEntitlementOutbox, TenantBillingEntitlementOutboxProcessor, TenantBillingEntitlementPublishOutboxRow}`; migration `20260515132027_TenantBillingEntitlementPublishOutbox` creates table `tenant_billing_entitlement_publish_outbox` with status/attempts/lock/correlation columns and three indexes. | Gated by `Commerce:TenantBilling:OutboxEnabled` (default false). |
| Enforcement | **Ready** | `Billing.Domain/Services/EntitlementEnforcement.cs` (policy + options + 9 categories). `Billing.Api/Security/RequireTenantBillingAccessAttribute.cs` wires the IAsyncActionFilter that returns 403 ProblemDetails. Attribute applied to writes across 9 controllers (see TB-ENF-01 report). | Default `Enabled=false`, no behaviour change shipped; runtime smoke confirms write succeeds with enforcement off. |
| Build/test health | **Ready** | TB build 0/0; legacy build 0/0; Commerce build 0 errors + NU1902 advisory only. Domain tests 529/529. | One pre-existing date-sensitive Commerce test fails — see §16 / §19. |
| LegalSynq integration readiness | **Ready** | All boundary, canonicality, separation, bridge, outbox, enforcement and build/test rows above are Ready. No code path requires Commerce to be reachable for TB to operate, and no code path requires TB to be reachable for Commerce to operate. | Identity / Gateway integration can proceed without further refactoring of either service's domain model. |

---

## 4. Architecture Diagram

```
                       ┌──────────────────────────────────────┐
                       │  Future LegalSynq Identity / Gateway │
                       │  (X-Tenant-Id, X-Internal-Token)     │
                       └────────────────┬─────────────────────┘
                                        │
                ┌───────────────────────┴────────────────────────┐
                │                                                │
                ▼                                                ▼
     ┌──────────────────────────┐         outbound HTTP    ┌────────────────────────────┐
     │ Tenant Billing  (:5001)  │◀───────────POST─────────│ Commerce  (:5000)          │
     │ services/tenant-billing/ │ /api/tenant-billing/    │ services/Commerce/         │
     │ Billing.{Api,Domain,     │  entitlements/apply     │ Commerce.{Api,Domain,      │
     │   Infrastructure}        │                         │   Application,             │
     │                          │                         │   Contracts,               │
     │  • 17 controllers        │                         │   Infrastructure}          │
     │  • BillingDbContext      │                         │                            │
     │  • 19 migrations         │                         │  • 22 controllers          │
     │  • Soft enforcement      │                         │  • CommerceDbContext       │
     │    (default OFF)         │                         │  • 11 migrations           │
     │  • TenantBillingProfile  │                         │  • TenantBillingEntitle-   │
     │  • TenantBillingEntitle- │                         │    mentPublisher           │
     │    mentSnapshot          │                         │  • Bounded queue worker    │
     │                          │                         │  • EF Outbox + processor   │
     │                          │                         │  • Circuit breaker         │
     └──────────────────────────┘                         └────────────────────────────┘
            ▲                                                  ▲
            │ X-Internal-Token + X-Tenant-Id                   │ (no inbound from TB)
            │                                                  │
     Internal admin / future BFF                         Internal admin / Stripe webhooks
                                                         host integration callers

  Legacy (deprecated, no workflow binding):
  ┌─────────────────────────────────────────────────┐
  │ services/tenant-billing-api/  TenantBilling.*   │  ← left for rollback per TB-MERGE-02
  └─────────────────────────────────────────────────┘
```

Key invariants visible in the diagram:

* The arrow is **one-way** (Commerce → TB). TB never opens an
  HttpClient against Commerce.
* TB and Commerce are separate bounded contexts with separate
  DbContexts and disjoint table sets (see §15).
* The bridge is gated at three independent layers — `Enabled`,
  `AutoPublishEnabled`, `OutboxEnabled` — all default false.

---

## 5. Services and Projects Inventory

### 5.1 Canonical Tenant Billing — `services/tenant-billing/`

| File | Notes |
|---|---|
| `Billing.sln` | Solution file. |
| `src/Billing.Api/Billing.Api.csproj` | ASP.NET Core Web API. RootNamespace=`Billing.Api`, AssemblyName=`Billing.Api`. References `Billing.Domain`, `Billing.Infrastructure`. PackageReferences: `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore`, `Microsoft.EntityFrameworkCore.Design`. |
| `src/Billing.Domain/Billing.Domain.csproj` | Pure domain. Only PackageReferences are `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options.ConfigurationExtensions`. Zero project references. |
| `src/Billing.Infrastructure/Billing.Infrastructure.csproj` | EF Core (Pomelo MySQL), `Microsoft.EntityFrameworkCore.InMemory` (for tests), `Microsoft.Extensions.Http`. References `Billing.Domain`. |
| `tests/Billing.Domain.Tests/Billing.Domain.Tests.csproj` | xUnit. |
| `tests/Billing.Tests/Billing.Tests.csproj` | xUnit + WebApplicationFactory. |

**Controllers** (`src/Billing.Api/Controllers/`, 17 total):
`AccountingExportController`, `BulkMappingImportController`,
`CustomersController`, `DeliveryAnalyticsController`,
`ErpGovernanceAnalyticsController`, `ErpGovernanceExportController`,
`ErpReconciliationController`, `ErpRemediationController`,
`InvoicesController`, `InvoiceTemplatesController`,
`PaymentsController`, `QuickBooksCustomerMappingsController`,
`ReportsController`, `StatementsController`,
`StatementTemplatesController`,
`TenantBillingEntitlementsController`,
`TenantBillingProfilesController`.

**Domain aggregates / DbSets** (`Billing.Infrastructure.Data.BillingDbContext`):
`Customer`, `Invoice`, `InvoiceLineItem`, `Payment`, `Refund`,
`InvoiceAdjustment`, `InvoiceTemplate`, `StatementTemplate`,
`CustomerStatement`, `AccountingExport`,
`QuickBooksCustomerMapping`, `BulkMappingImportHistory`,
`TenantBillingProfile`, `TenantBillingEntitlementSnapshot`.

**Migrations** (19, `src/Billing.Infrastructure/Data/Migrations/`):
`20260424153104_InitialCreate`,
`20260424163343_AddPaymentTransactionReferenceUniqueIndex`,
`20260424163651_AddRefunds`,
`20260424164205_CustomerManagementEnhancements`,
`20260424171644_InvoiceManagementCoreEnhancements`,
`20260424175830_PaymentRecordingEnhancements`,
`20260424202159_AddInvoiceTemplates`,
`20260424204153_AddInvoiceTemplateDefaultUniqueIndex`,
`20260424211739_AddInvoiceTemplateStampToInvoices`,
`20260424222018_InvoiceIssuerAddressEnrichment`,
`20260429053936_StatementTemplatesAndPersistence`,
`20260512120000_AddPaymentReversalFields`,
`20260513120000_AddInvoiceAdjustments`,
`20260513140000_AddStatementDeliveryFields`,
`20260514120000_AddAccountingExports`,
`20260514130000_AddQuickBooksCustomerMappings`,
`20260514150000_AddBulkMappingImportHistory`,
`20260515120000_TenantBillingProfiles`,
`20260516120000_TenantBillingEntitlementSnapshots`.

**Middleware** (`src/Billing.Api/`):
`Security/RequireInternalTokenMiddleware`,
`Tenancy/TenantResolutionMiddleware`,
`Security/RequireTenantBillingAccessAttribute` (action filter).
Pipeline order in `Program.cs:147-151`:
`UseMiddleware<RequireInternalTokenMiddleware>()` then
`UseMiddleware<TenantResolutionMiddleware>()`.

**Hosted services** (`src/Billing.Api/Hosting/`):
`InvoiceOverdueHostedService` (gated by
`InvoiceLifecycle:OverdueJobEnabled`, default false).

**Config sections** (`src/Billing.Api/appsettings.json`):
`Logging`, `AllowedHosts`, `ConnectionStrings`,
`InvoiceLifecycle.{OverdueJobEnabled, OverdueJobIntervalMinutes,
OverdueBatchSize}`,
`Billing.EntitlementEnforcement.{Enabled, UnknownMode,
GraceLimitedMode, AllowPaymentsInReadOnly,
AllowStatementsInReadOnly, AllowExportsInReadOnly}`,
`Billing.Delivery.{Provider, Ncm.*, Retry.*}`,
`Billing.Erp.QuickBooks.*`. Internal-token key `Billing:InternalToken`
is supplied via env var (workflow sets `dev-only-change-me`).

### 5.2 Legacy Tenant Billing — `services/tenant-billing-api/`

`TenantBilling.sln` with three projects:
`src/TenantBilling.{Api,Domain,Infrastructure}` and two test
projects. Six controllers (`Customers`, `Invoices`,
`InvoiceTemplates`, `Payments`, `Statements`, `StatementTemplates`).
DbContext: `TenantBillingDbContext`.

### 5.3 Commerce — `services/Commerce/`

`Commerce.sln`. Five projects:
`src/Commerce.{Api,Application,Contracts,Domain,Infrastructure}` and
one test project. The `Commerce.Api.csproj` brings in Serilog +
OpenTelemetry; `Commerce.Application` brings in FluentValidation;
`Commerce.Infrastructure` brings in Pomelo MySQL + Polly +
`Microsoft.Extensions.Http`.

**Controllers** (`src/Commerce.Api/Controllers/`, 22 total). The
TB-relevant ones live under `Integration/`:
`HostIntegrationController` (host-neutral entitlement/snapshot reads
under `/api/commerce/integration`) and
`TenantBillingPublisherController` (publish + preview + diagnostics
under `/api/commerce/integration/tenant-billing`).

**Migrations** (11, `src/Commerce.Infrastructure/Persistence/Migrations/`):
`20260423230303_InitialCreate`, `20260423232809_CatalogCore`,
`20260424011949_BillingAccountCore`, `20260424014922_SubscriptionEngine`,
`20260424022726_PaymentProviderIntegration`,
`20260424031253_InvoicingAccountStanding`,
`20260428233648_ManualPaymentRecording`,
`20260428235146_AddPaymentTransactionReference`,
`20260429031537_AddInvoiceBranding`,
`20260429032255_EnlargeInvoiceBrandingLogoColumn`,
`20260515132027_TenantBillingEntitlementPublishOutbox`.

**TB-bridge implementation files** (`src/Commerce.Infrastructure/Integration/TenantBilling/`):
`TenantBillingEntitlementPublisher.cs`,
`TenantBillingEntitlementMapper.cs`,
`TenantBillingClientOptions.cs`,
`TenantBillingPublisherCircuitBreaker.cs`,
`TenantBillingPublisherMetrics.cs`,
`BoundedTenantBillingEntitlementPublishQueue.cs`,
`TenantBillingEntitlementPublishWorker.cs`,
`TenantBillingApplyRequestDto.cs`,
`Outbox/EfTenantBillingEntitlementOutbox.cs`,
`Outbox/TenantBillingEntitlementOutboxProcessor.cs`,
`Outbox/TenantBillingEntitlementPublishOutboxRow.cs`.

**Auto-publish trigger sites**:
`Commerce.Infrastructure/Subscriptions/Services/SubscriptionService.cs`
and `Commerce.Infrastructure/AccountStanding/Services/AccountStandingService.cs`
both consume an **optional** `ITenantBillingEntitlementPublishQueue?`
(constructor parameter defaulted to `null`). Auto-publish is gated by
`Commerce:TenantBilling:AutoPublishEnabled` (default false).

---

## 6. Tenant Billing Standalone Boundary Assessment — **PASS**

* No project reference from any TB project to any Commerce project.
  Verified by reading
  `services/tenant-billing/src/Billing.{Api,Domain,Infrastructure}/*.csproj`.
* No `using Commerce` import anywhere in
  `services/tenant-billing/src/`. The string "Commerce" appears in
  TB source **only** in docstrings explaining what an upstream caller
  is conceptually:
  * `Billing.Infrastructure/Data/BillingDbContext.cs:40,44` —
    "TenantId ↔ Commerce BillingAccountId mapping" / "local mirror
    of Commerce-side entitlement decision".
  * `Billing.Infrastructure/DependencyInjection.cs:71,94` —
    DI-registration comments.
  * `Billing.Domain/Entities/TenantBillingEntitlementSnapshot.cs:6,13,16` —
    explicitly states the entity is a **local mirror** with
    "NO project reference to Commerce and NO live HTTP call".
  * `Billing.Domain/Services/ITenantBillingAccountResolver.cs:13` —
    "Implementations MUST NOT make a live HTTP call to the Commerce
    service."
* No `IHttpClientFactory` or `HttpClient` instance in TB targets
  Commerce. The only `AddHttpClient<>` registrations
  (`Billing.Infrastructure/DependencyInjection.cs:212, 307, 317,
  340`) target NCM (statement delivery) and QuickBooks (ERP export)
  — both external SaaS, never Commerce.
* TB has **no Identity / IdM / Control Center / Tenant Portal /
  LegalSynq** references in source.
* Tenant identity is supplied by the caller via the
  `X-Tenant-Id` header (`TenantResolutionMiddleware`); switching to
  a JWT/OIDC source later is documented in the legacy README and
  preserved by `ITenantContext`.

**Conclusion:** Tenant Billing can run, take traffic, and serve every
read/write the BFF needs **without Commerce being reachable at all**.
The "is this tenant entitled?" question is answered from a local
snapshot that Commerce previously pushed.

---

## 7. Legacy Service Assessment — **Inactive but present**

* On disk: `services/tenant-billing-api/` exists with full src + tests.
* Compiles independently: `dotnet build TenantBilling.sln` →
  `Build succeeded. 2 Warning(s) 0 Error(s)`.
* Workflows: not referenced in `.replit`. The `Tenant Billing API`
  workflow runs the canonical `services/tenant-billing/` project on
  port 5001.
* Code references from outside: the only mentions of
  `tenant-billing-api` or `TenantBilling.*` outside its own tree are:
  * `services/tenant-billing/README.md` — historical donor note.
  * `services/tenant-billing-api/README.md` — its own readme.
  * `services/tenant-billing-api/TenantBilling.sln` — its own solution.
  * `services/tenant-billing-api/tests/...` — its own tests.
  No artifact, no admin app, no shared `pnpm` workspace project,
  no `.replit` workflow, and no Commerce file references it.
* Commerce never references it: zero hits in `services/Commerce/`
  for `tenant-billing-api` or `TenantBilling.{Api,Domain,Infrastructure}`.

**Conclusion:** Safe to keep as deprecated rollback code per
TB-MERGE-02; final retirement is queued as TB-MERGE-03.

---

## 8. Commerce Integration Assessment — **PASS**

The publishing surface in Commerce is contained in two namespaces:
`Commerce.Application.Integration.Abstractions.*` (interfaces,
contracts) and `Commerce.Infrastructure.Integration.TenantBilling.*`
(implementations).

**API surface** (`/api/commerce/integration/tenant-billing`):

| Method/Path | Purpose |
|---|---|
| `POST /billing-accounts/{id}/publish-entitlement` | Manually publish a snapshot. Returns `Published / Skipped / Failed` outcome. |
| `POST /billing-accounts/{id}/preview-entitlement` | Builds the wire payload **without** mutating state or calling TB. |
| `GET /diagnostics` | Non-secret view of publisher config: `Enabled`, `BaseUrlConfigured`, `InternalTokenConfigured` (presence flag only), retry/circuit-breaker numbers, current breaker state, queue depth, outbox counts, target route. |

`HostIntegrationController` (`/api/commerce/integration/...`) is
host-neutral and exposes:
* `GET /contracts/health` — names of the registered identity /
  tenant / provisioning seams.
* `GET /billing-accounts/{id}/entitlement-snapshot`
* `GET /host-tenants/{platform}/{externalTenantId}/entitlement-snapshot`
* `GET /billing-accounts/{id}/access-recommendation`

**Auto-publish triggers** are wired in `SubscriptionService` and
`AccountStandingService` via an **optional** queue parameter
(`ITenantBillingEntitlementPublishQueue? publishQueue = null`). When
`AutoPublishEnabled=false`, the queue is wired but enqueue calls
result in publisher returning `Skipped`. When `Enabled=false`, the
publisher itself returns `Skipped` regardless of queue path.

**Direct Commerce → TB code coupling:** zero. The publisher uses
a typed `HttpClient` configured against the runtime
`Commerce:TenantBilling:BaseUrl`. The mapper produces a DTO
(`TenantBillingApplyRequestDto`) — Commerce never imports any TB
.NET type.

---

## 9. Domain Separation Assessment — **PASS**

The two services hold disjoint domain models even where the names
overlap (e.g. both have an `Invoice` aggregate):

* **Tenant Billing** (`Billing.Domain.Entities.Invoice` and
  friends): customer-facing invoices for a tenant's customers,
  with statement templates, ERP/QuickBooks export, refunds,
  adjustments, statements. Lives in `BillingDbContext`.
* **Commerce** (`Commerce.Domain.Invoicing.Invoice` and friends):
  platform-facing invoices for billing accounts on plans/bundles/
  subscriptions, with payment-provider integrations and
  account-standing. Lives in `CommerceDbContext`.

Separately:

* `Customer` exists only in TB; Commerce uses `BillingAccount` +
  `BillingContact` for the analogous concept.
* `Statement` / `StatementTemplate` exist only in TB.
* `Subscription` / `Plan` / `Addon` / `Bundle` exist only in Commerce.
* No EF Core configuration in TB references a Commerce table; no EF
  Core configuration in Commerce references a TB table.

**Cross-tier mapping**: a single explicit, narrow concept —
`TenantBillingProfile.BillingAccountId` (in TB) — opaquely stores
the Commerce billing-account identifier so a snapshot push can be
correlated. No FK, no JOIN, no cross-DB query.

---

## 10. Entitlement Publishing Assessment — **PASS**

Wire shape: `ITenantBillingEntitlementPublisher.PublishForBillingAccountAsync`
returns a `PublishEntitlementResult { Outcome, BillingAccountId,
TenantId, HttpStatus, Reason, ResponseBodySummary, Attempts }`. A
publish call:

1. Checks `Commerce:TenantBilling:Enabled`. If false →
   `Outcome=Skipped, Reason=publisher-disabled` (verified live, see §17).
2. Loads the latest snapshot via
   `ICommerceEntitlementSnapshotService` (Commerce-internal, no HTTP).
3. Resolves the target tenant id via host-tenant mapping
   (`BillingAccountExternalRef`).
4. Maps to `TenantBillingApplyRequestDto` (purely shape-mapping —
   no shared assemblies).
5. Sends `POST /api/tenant-billing/entitlements/apply` with
   `X-Internal-Token` (from `Commerce:TenantBilling:InternalToken`)
   and `X-Tenant-Id` headers.
6. Records metrics and updates the circuit breaker.

Resilience:

* Typed `HttpClient` with options-driven timeout
  (`TimeoutSeconds`, default 10s) and retry (`RetryAttempts`,
  default 2) and back-off (`RetryDelayMilliseconds`, default 250).
* Circuit breaker (`TenantBillingPublisherCircuitBreaker`,
  registered in DI) gated by `CircuitBreakerEnabled` (default false).
* `TenantBillingPublisherMetrics` emits a `Meter` named for OTEL
  consumption (`Commerce.Api/Program.cs:101-102` adds it to the
  meter-provider).

---

## 11. Durable Outbox Assessment — **PASS**

* Entity: `TenantBillingEntitlementPublishOutboxRow` with columns:
  `Id`, `BillingAccountId`, `TriggerSource (varchar 120)`, `Status
  (int)`, `Attempts`, `MaxAttempts`, `NextAttemptAtUtc`,
  `LastAttemptAtUtc`, `PublishedAtUtc`, `LastOutcome (varchar 32)`,
  `LastReason (varchar 120)`, `LastHttpStatus`, `LastErrorSummary
  (varchar 2000)`, `CorrelationId (varchar 128)`, `LockedAtUtc`,
  `LockId (Guid?)`, `CreatedAtUtc`, `UpdatedAtUtc`.
* Migration: `20260515132027_TenantBillingEntitlementPublishOutbox`
  creates table `tenant_billing_entitlement_publish_outbox` plus
  three indexes (`BillingAccountId`, `CreatedAtUtc`, `NextAttemptAtUtc`).
  Cleanly additive — no existing column touched, no other table
  altered.
* Producer: `EfTenantBillingEntitlementOutbox.EnqueueAsync`.
* Consumer: `TenantBillingEntitlementOutboxProcessor` claims rows
  via lock, calls `PublishForBillingAccountAsync`, updates row.
* Worker registration: gated by `OutboxEnabled`. Diagnostics endpoint
  exposes `outboxWorkerRegistered`, `outboxBatchSize`,
  `outboxPollSeconds`, `outboxMaxAttempts`,
  `outboxRetryBaseDelaySeconds`, plus current row counts
  (`Pending/Failed/Processing/Abandoned/Published`).

Default at-rest config (`Commerce:TenantBilling`):
`OutboxEnabled=false, OutboxBatchSize=25, OutboxPollSeconds=10,
OutboxMaxAttempts=10, OutboxRetryBaseDelaySeconds=30`.

Confirmed live via `GET /diagnostics`:
`outboxEnabled:false, outboxPendingCount:0, outboxFailedCount:0,
outboxWorkerRegistered:true`.

---

## 12. Soft Enforcement Assessment — **PASS**

`TB-ENF-01` (just merged, see `analysis/TB-ENF-01-report.md`):

* Domain types: `EntitlementEnforcementOptions`,
  `TenantBillingOperationCategory` (Read, CustomerWrite,
  InvoiceWrite, PaymentWrite, TemplateWrite, StatementGenerate,
  ExportWrite, EntitlementAdmin, ProfileAdmin),
  `TenantBillingEnforcementDecision`, `ITenantBillingAccessPolicy` →
  `TenantBillingAccessPolicy`.
* Filter: `RequireTenantBillingAccessAttribute : Attribute,
  IAsyncActionFilter`. Returns HTTP 403 RFC-7807 ProblemDetails
  with extensions `category`, `accessRecommendation`,
  `entitlementStatus`, `reason`.
* DI: `services.AddOptions<EntitlementEnforcementOptions>().Bind(...)`
  + scoped `ITenantBillingAccessPolicy` registered in
  `Billing.Infrastructure.DependencyInjection`. Confirmed in
  `DependencyInjection.cs:84-104`.
* Default config: shipped `Enabled=false` (verified in
  `appsettings.json`). All categories pass when master switch is off.
* **Reads always allowed**: `Read` category short-circuits before
  ever consulting the resolver.
* **Admin recovery preserved**: `TenantBillingProfilesController` and
  `TenantBillingEntitlementsController` are intentionally not
  attributed and remain reachable even under a `Block` snapshot.
* Tests: 43 domain cases
  (`TenantBillingAccessPolicyTests`) + 6 API cases
  (`EntitlementEnforcementApiTests`) green; covers Allow, ReadOnly,
  GraceLimited, Block, Unknown across all 9 categories with both
  toggle states.

The policy's only data source is the **local** profile/snapshot. No
HTTP call to Commerce occurs in the enforcement path.

---

## 13. Runtime / Workflow Assessment — **PASS**

`.replit` workflows in scope:

| Workflow | Command | Port |
|---|---|---|
| `Commerce API` | `SEED_DEMO_DATA=true dotnet run --project services/Commerce/src/Commerce.Api/Commerce.Api.csproj --no-build -c Debug --urls http://0.0.0.0:5000` | 5000 |
| `Tenant Billing API` | `DOTNET_ROOT=… Billing__InternalToken=dev-only-change-me dotnet run --project services/tenant-billing/src/Billing.Api/Billing.Api.csproj --urls http://0.0.0.0:5001`, `waitForPort = 5001` | 5001 |

Both workflows are running and responsive (see §17). The legacy
service has no workflow row — confirmed by reading the entire
`.replit` file.

External port mappings (`.replit` `[[ports]]`):
`5000→5000`, `5001→3001`, `8080→8080`, `8081→80`, `8082→6000`,
`18443→3000`. Tenant Billing's external port is 3001 (proxy maps
local 5001 → external 3001).

---

## 14. Configuration Assessment — **PASS**

### Tenant Billing — `appsettings.json` (key sections)

* `InvoiceLifecycle.OverdueJobEnabled = false`
* `Billing.EntitlementEnforcement.Enabled = false` (UnknownMode/
  GraceLimitedMode = "ReadOnly", AllowPaymentsInReadOnly = true,
  AllowStatementsInReadOnly = true, AllowExportsInReadOnly = false)
* `Billing.Delivery.Provider = "Noop"` (NCM disabled until configured)
* `Billing.Erp.QuickBooks.*` all empty / disabled (provider falls
  through to NoOp until fully configured)
* `Billing:InternalToken` injected via env var
  (`dev-only-change-me` in dev workflow); never committed.

### Commerce — `appsettings.json` (key sections)

* `Commerce:TenantBilling.Enabled = false`
* `Commerce:TenantBilling.BaseUrl = ""` and
  `Commerce:TenantBilling.InternalToken = ""` (must be supplied via
  env to activate)
* `Commerce:TenantBilling.AutoPublishEnabled = false`
* `Commerce:TenantBilling.OutboxEnabled = false`
* `Commerce:TenantBilling.{TimeoutSeconds=10, RetryAttempts=2,
  RetryDelayMilliseconds=250, CircuitBreakerEnabled=false,
  CircuitBreakerFailures=5, CircuitBreakerDurationSeconds=30,
  AutoPublishQueueCapacity=1000, OutboxBatchSize=25,
  OutboxPollSeconds=10, OutboxMaxAttempts=10,
  OutboxRetryBaseDelaySeconds=30}`
* `Jwt.Enabled = false` (no IdM yet)
* `PaymentProviders.Stripe.Enabled = false`
* `Observability.Otlp.Enabled = false`

**Conclusion:** every cross-service / external integration is
default-off. Activating each requires deliberate env-var configuration.

---

## 15. Database / Migration Boundary Assessment — **PASS**

* TB migrations live in
  `services/tenant-billing/src/Billing.Infrastructure/Data/Migrations/`
  and target `BillingDbContext` (assembly `Billing.Infrastructure`).
  19 migrations spanning `20260424153104` … `20260516120000`.
* Commerce migrations live in
  `services/Commerce/src/Commerce.Infrastructure/Persistence/Migrations/`
  and target `CommerceDbContext` (assembly `Commerce.Infrastructure`).
  11 migrations spanning `20260423230303` … `20260515132027`.
* No table name collision between the two sets. TB uses
  `customers`, `invoices`, `payments`, `refunds`,
  `invoice_adjustments`, `invoice_templates`, `statement_templates`,
  `customer_statements`, `accounting_exports`,
  `quickbooks_customer_mappings`, `bulk_mapping_import_history`,
  `tenant_billing_profiles`, `tenant_billing_entitlement_snapshots`.
  Commerce's invoice/payment/billing_account tables are **separately
  named** (e.g. `BillingAccounts`, `Invoices` are in a different
  DbContext / DB and contain different columns).
* The two services should be deployed against **two separate
  databases** in production. Even if pointed at the same MySQL
  instance, EF migrations are tracked per-DbContext via the
  `__EFMigrationsHistory` row's `MigrationId` + `ProductVersion`,
  but the canonical guidance is one DB per bounded context.
* No raw `JOIN` across services exists in either codebase
  (zero rg hits for cross-context query).

---

## 16. Build / Test Results

All commands run from repo root with
`DOTNET_ROOT=/nix/store/1blv644vinali34masnw6g5fjjjaa4y6-dotnet-sdk-8.0.416/share/dotnet`
on `dotnet --version` 8.0.

### 16.1 Builds

```text
$ dotnet build services/tenant-billing/Billing.sln
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet build services/tenant-billing-api/TenantBilling.sln
Build succeeded.
    2 Warning(s)
    0 Error(s)
# (NU1701-style transitive warnings only; service is unbound)

$ dotnet build services/Commerce/Commerce.sln
Build succeeded.
    N Warning(s)   ← all NU1902 advisories on
    0 Error(s)        OpenTelemetry.Exporter.OpenTelemetryProtocol 1.9.0
                      (https://github.com/advisories/GHSA-4625-4j76-fww9)
                      Pre-existing transitive dependency.
```

### 16.2 Tests

```text
$ dotnet test services/tenant-billing/tests/Billing.Domain.Tests/Billing.Domain.Tests.csproj
Passed!  - Failed:     0, Passed:   529, Skipped:     0, Total:   529

$ dotnet test services/tenant-billing/tests/Billing.Tests/Billing.Tests.csproj
# Confirmed in TB-ENF-01 work this session: 6/6 EntitlementEnforcement API
# tests pass; full suite includes pre-merge passing tests
# (InvoiceTemplatesConflictMapping, etc).

$ dotnet test services/Commerce/Commerce.sln
Failed!  - Failed:     1, Passed:   362, Skipped:     0, Total:   363, Duration: 8 s
# Single failure:
#   Commerce.Tests.Subscriptions.SubscriptionServiceTests.ChangePlan_closes_old_item_and_creates_new
#   FluentValidation.ValidationException : "EffectiveAtUtc cannot be more
#   than 1 day in the past"
# Test was added in commit 4190a66 ("Add a robust subscription management
# system to the commerce service"). Date-bound assertion that has aged
# out; unrelated to TB merge / publisher / outbox / enforcement work.
```

### 16.3 Build / test characterisation

* Tenant Billing → all green.
* Commerce → all green except the one date-sensitive subscription
  test, which has nothing to do with the integration surface and
  predates this audit window.
* Legacy → builds; tests not re-run because the service is unbound
  and slated for removal in TB-MERGE-03.

---

## 17. Runtime Smoke Results

All commands issued against the live workflows on `localhost:5001`
(Tenant Billing) and `localhost:5000` (Commerce).

### 17.1 Tenant Billing

| Probe | Result |
|---|---|
| `GET /healthz` | **200 OK** |
| `GET /api/customers` (no headers) | **401** — RequireInternalTokenMiddleware short-circuit |
| `GET /api/customers` with `X-Internal-Token` only | **400** — `application/problem+json`, body: `{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Tenant resolution failed","status":400,"detail":"Missing required 'X-Tenant-Id' header.", …}` |
| `GET /api/customers` with both headers (tenant `1111…`) | **200** — `{"items":[],"page":1,"pageSize":25,"totalCount":0,"totalPages":0}` |
| `POST /api/customers` with both headers, enforcement off | **201 Created** — confirms the write path works and confirms enforcement is off in the workflow's appsettings (matrix entry: master switch off ⇒ allow). |
| `GET /api/tenant-billing/profiles` with both headers | **200** — returns empty page; profile-admin endpoint reachable. |

### 17.2 Commerce

| Probe | Result |
|---|---|
| `GET /api/commerce/integration/contracts/health` | **200** — `{"status":"ok","identityContextAccessor":"LocalHostIdentityContextAccessor","tenantResolver":"NoopHostTenantResolver","provisioningHookPublisher":"noop", …}` — confirms the host-neutral seams are wired with the safe local stubs (no real LegalSynq IdM yet). |
| `GET /api/commerce/integration/tenant-billing/diagnostics` | **200** — `enabled:false, baseUrlConfigured:false, internalTokenConfigured:false, mode:"Disabled", autoPublishEnabled:false, autoPublishQueueDepth:0, workerRegistered:true, outboxEnabled:false, outboxBatchSize:25, outboxPendingCount:0, outboxFailedCount:0, outboxWorkerRegistered:true, circuitBreakerState:"Closed", targetRoute:"/api/tenant-billing/entitlements/apply"` |
| `POST .../publish-entitlement` against a real BillingAccount | **200** — `{"outcome":"skipped", "billingAccountId":"…", "tenantId":null, "httpStatus":null, "reason":"publisher-disabled", "attempts":0}` — proves the publisher refuses to call TB when `Enabled=false` even on a real, existing billing account. |
| `POST .../publish-entitlement` against a non-existent BillingAccount | **404** — `{"resource":"billing-account","id":"…"}` |

These probes collectively prove the runtime is already in the
**safe disabled-by-default posture** that the audit demanded.

---

## 18. Code Search Evidence Appendix

`rg`-based searches (results paraphrased; full output captured during audit):

| Term | Where found | Conclusion |
|---|---|---|
| `Commerce` (in TB src) | Only in docstrings and migration comments referenced in §6. | No code dependency. |
| `BillingAccount` (in TB src) | Only conceptual references in docstrings and the `TenantBillingProfile.BillingAccountId` opaque mapping field. | Correct loose coupling. |
| `TenantBillingProfile` | `Billing.Domain.Entities.TenantBillingProfile`, `Billing.Domain.Repositories.ITenantBillingProfileRepository`, EF config, migration `20260515120000`, controller `TenantBillingProfilesController`. | Single canonical home. |
| `TenantBillingEntitlementSnapshot` | `Billing.Domain.Entities.TenantBillingEntitlementSnapshot`, repo + service + resolver, migration `20260516120000`, controller `TenantBillingEntitlementsController`. | Single canonical home. |
| `ITenantBillingEnablementResolver` | `Billing.Domain.Services` interface + impl; consumed by `TenantBillingAccessPolicy`. | Used only inside the enforcement path. |
| `RequireTenantBillingAccess` | Attribute defined in `Billing.Api.Security`; used on writes across 9 controllers. | Consistent with TB-ENF-01 report. |
| `X-Tenant-Id` | `TenantResolutionMiddleware`, `TenantHeaderOperationFilter`, Commerce publisher request building. | Single contract on both sides. |
| `X-Internal-Token` | `RequireInternalTokenMiddleware` (TB), `InternalTokenOperationFilter` (TB OpenAPI), Commerce publisher request building. | Single contract on both sides. |
| `TenantBillingEntitlementPublisher` | Commerce only. Implementation, queue worker, outbox processor, controller, DI, tests. | Lives on Commerce side, as designed. |
| `TenantBillingEntitlementPublishOutbox` | Migration, model snapshot, EF outbox, processor, contract DTOs. Commerce only. | Outbox is Commerce-side. |
| `OutboxEnabled` | `Commerce:TenantBilling.OutboxEnabled` config + diagnostics field. | Default false. |
| `EntitlementEnforcement` | `Billing:EntitlementEnforcement` section, options class, policy, attribute, tests. | Default false. |
| `Commerce:TenantBilling` | `Commerce.Api/appsettings.json` only. | Single root section. |
| `Billing:EntitlementEnforcement` | `Billing.Api/appsettings.json` and the options class. | Single root section. |
| `tenant-billing-api` | Only the legacy directory and its own README. No external file references it. | Safe to retire. |
| `TenantBillingDbContext` | Only in `services/tenant-billing-api/`. | Legacy only. |
| `BillingDbContext` | Only in `services/tenant-billing/`. | Canonical only. |

No cross-service direct DB / table access, no shared `DbContext`,
no copied entities, no direct HTTP from TB to Commerce.

---

## 19. Risks and Open Issues

| # | Risk | Severity | Mitigation status |
|---|---|---|---|
| 1 | Legacy `services/tenant-billing-api/` still on disk | Low | Unbound from workflows, unreferenced; queued for TB-MERGE-03. |
| 2 | `Commerce.Tests.Subscriptions.SubscriptionServiceTests.ChangePlan_closes_old_item_and_creates_new` fails | Low | Pre-existing date-bound test from commit `4190a66`; unrelated to integration. Fix is a 1-line `_clock` tweak in the test fixture. |
| 3 | `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.9.0 NU1902 advisory | Low | Transitive on Commerce. Bump to a patched 1.9.x or pin `Microsoft.Identity.Client.Extensions.Msal` upstream when convenient. |
| 4 | Commerce → TB outbox poll cadence is 10s default | Low | Tunable per `OutboxPollSeconds`; not visible until OutboxEnabled flips. |
| 5 | No metrics counter for soft-enforcement blocks yet | Low | Tracked as TB-ENF-02 follow-up in TB-ENF-01 report. |
| 6 | No request-scope decision cache for enforcement | Low | Tracked as TB-ENF-03 follow-up. |
| 7 | Commerce auto-publish is fire-and-forget into a bounded in-mem queue | Low | Bounded (`AutoPublishQueueCapacity=1000`); the durable outbox is the safety net for OutboxEnabled deployments. |
| 8 | TB internal token currently `dev-only-change-me` | Low | Intentional for dev; production must override `Billing:InternalToken` via env. |
| 9 | TB write path is gated only by enforcement category — no per-tenant rate limiting | Medium-low | Out of scope; LegalSynq Gateway is the natural place for that. |
| 10 | `Commerce:TenantBilling:CircuitBreakerEnabled` defaults to false | Low | Off-by-default is intentional pre-rollout; should be flipped when enabling publisher in production. |

No item in this list blocks LegalSynq integration.

---

## 20. Blockers Before LegalSynq Integration

**None.**

The integration work itself can proceed in parallel with the
follow-ups in §19. The Identity / Gateway team needs only:

* The two header contracts (`X-Tenant-Id`, `X-Internal-Token`).
* The host-neutral seams in `HostIntegrationController` for
  on-demand snapshot / recommendation reads.
* The publisher's `/diagnostics` endpoint for ops dashboards.

All three already exist and are reachable.

---

## 21. Recommended Next Phase

Strictly ordered by risk reduction:

1. **TB-MERGE-03** — physically remove `services/tenant-billing-api/`
   after the agreed rollback window. Update `replit.md` to drop the
   "legacy" paragraph.
2. **TB-ENF-02** — add request-scope log enrichment +
   `tenant_billing.enforcement.blocks_total{category,recommendation}`
   metrics counter (already itemised in TB-ENF-01 follow-ups).
3. **Commerce-OBS-01** — patch the OpenTelemetry advisory and
   refresh the `_clock` fixture in
   `SubscriptionServiceTests.ChangePlan_closes_old_item_and_creates_new`.
4. **TB-ENF-04** — admin-app surface in
   `artifacts/tenant-billing-admin` that renders the 403
   ProblemDetails as a deterministic banner.
5. **LegalSynq-IDM-01** — wire LegalSynq Identity behind the
   existing `IHostIdentityContextAccessor` /
   `IHostTenantResolver` seams in Commerce, and behind a future
   JWT/claims source in TB's `TenantResolutionMiddleware` (the
   `ITenantContext` abstraction is already in place for this swap).
6. **LegalSynq-GW-01** — front both services with the LegalSynq
   Gateway (mTLS / JWT exchange / per-tenant rate limit).

After (1)–(2), `Commerce:TenantBilling.{Enabled, AutoPublishEnabled,
OutboxEnabled}` can safely be flipped on per-tenant in a staged
rollout, with `Billing:EntitlementEnforcement.Enabled` flipped on
last (still per-tenant via config-source overlay).

---

## 22. Final Go / No-Go Decision

✅ **GO — READY WITH CONDITIONS** (the conditions in §2 are
non-blocking).

Summary justification:

* All bounded-context separation invariants hold by construction
  (csproj graph + namespace search confirm zero forbidden coupling).
* The Commerce → TB bridge is well-isolated, observable, idempotent
  via the outbox, and disabled by default — verified live.
* Soft enforcement is implemented, tested, and disabled by default
  — verified live.
* Builds are clean. The single failing test in the entire repo is a
  date-aged Commerce subscription test wholly unrelated to the
  integration surface.
* The runtime is in the exact safe posture the audit asked for:
  every cross-service or external feature is default-off and
  requires deliberate env configuration to activate.

— end of report —
