# COM-B02 — Catalog Core — Implementation Report

> Status: **Complete.** Build, tests, and migration generation are green.
> Docker image build is the only validation step that could not be executed
> in this environment (no Docker daemon — see Section 10).

## 1. Summary

COM-B02 implements the **Commerce Catalog Core** on top of the COM-B01
foundation. The Catalog defines the commercial items the Commerce service
can sell (products, features, plans, plan-feature mappings, add-ons,
bundles, and prices), exposes admin CRUD + lifecycle endpoints under
`/api/commerce/catalog`, enforces validation through FluentValidation, and
persists everything via EF Core + Pomelo MySQL with a new `CatalogCore`
migration.

No tenant, identity, billing, subscription, payment, Stripe, webhook,
checkout, entitlement, or host-platform code is introduced. Those concerns
remain reserved for later blocks (see Section 12).

## 2. Stories Completed

| Story        | Title                       | Status |
| ------------ | --------------------------- | ------ |
| COM-E02-001  | Product entity              | ✅     |
| COM-E02-002  | Feature entity              | ✅     |
| COM-E02-003  | Plan entity                 | ✅     |
| COM-E02-004  | Plan–Feature mapping        | ✅     |
| COM-E02-005  | Add-ons model               | ✅     |
| COM-E02-006  | Bundles model               | ✅     |
| COM-E02-007  | Pricing model               | ✅     |
| COM-E02-008  | Catalog status states       | ✅     |
| COM-E02-009  | Catalog CRUD APIs           | ✅     |
| COM-E02-010  | Catalog validation rules    | ✅     |
| COM-E02-011  | Catalog query APIs          | ✅     |

## 3. Architecture Implemented

The COM-B01 layered structure is preserved. Catalog code is added to each
layer in dedicated `Catalog/` folders so domain features stay isolated
from foundation concerns.

```
services/Commerce/
├── src/
│   ├── Commerce.Domain/Catalog/             ← entities + enums (no deps)
│   ├── Commerce.Contracts/Catalog/          ← request/response DTOs
│   ├── Commerce.Application/
│   │   ├── Catalog/Abstractions/            ← service interfaces
│   │   ├── Catalog/Validators/              ← FluentValidation
│   │   ├── Common/Exceptions/               ← typed app errors
│   │   └── Common/Time/                     ← IClock abstraction
│   ├── Commerce.Infrastructure/
│   │   ├── Catalog/Services/                ← service implementations
│   │   ├── Catalog/Mapping/                 ← entity → DTO mappers
│   │   └── Persistence/
│   │       ├── Configurations/CatalogConfigurations.cs
│   │       └── Migrations/                  ← + CatalogCore migration
│   └── Commerce.Api/
│       ├── Controllers/Catalog/             ← admin CRUD endpoints
│       └── Middleware/                      ← ProblemDetails handler
└── tests/Commerce.Tests/Catalog/            ← unit + integration tests
```

Key design choices:

- **Catalog entity IDs are `Guid`** (server-generated). Avoids hardcoded
  IDs and is portable across environments.
- **Enums live in `Commerce.Domain.Catalog.Enums`** and are referenced by
  `Commerce.Contracts` via a small `Contracts → Domain` project reference
  so wire format and domain share one source of truth.
- **Service layer in Infrastructure, interfaces in Application**:
  controllers depend only on `Commerce.Application` interfaces; EF Core
  never appears in a controller.
- **Status lifecycle** is driven by the `CatalogStatus { Draft, Active,
  Retired }` enum and explicit `Activate()` / `Retire()` domain methods,
  not by free-form status strings.
- **Validation** uses FluentValidation. Each request DTO has its own
  validator. Services invoke the validator before mutating state, and the
  Application DI module assembly-scans validators (already wired in B01).
- **Errors** are surfaced as typed application exceptions
  (`NotFoundException`, `DuplicateKeyException`,
  `InvalidStateTransitionException`, `InvalidRelationshipException`,
  `ValidationException`) and translated to RFC 7807 `ProblemDetails`
  responses by `ProblemDetailsExceptionMiddleware`.

## 4. Files Created / Changed

### `Commerce.Domain` (new)
- `Catalog/Enums/CatalogStatus.cs`
- `Catalog/Enums/FeatureType.cs`
- `Catalog/Enums/BillingInterval.cs`
- `Catalog/CatalogKey.cs` — key normalization helper
- `Catalog/Product.cs`
- `Catalog/Feature.cs`
- `Catalog/Plan.cs`
- `Catalog/PlanFeature.cs`
- `Catalog/Addon.cs`
- `Catalog/Bundle.cs`
- `Catalog/BundleItem.cs`
- `Catalog/Price.cs`

### `Commerce.Contracts` (new)
- `Catalog/ProductDtos.cs`
- `Catalog/FeatureDtos.cs`
- `Catalog/PlanDtos.cs`
- `Catalog/PlanFeatureDtos.cs`
- `Catalog/AddonDtos.cs`
- `Catalog/BundleDtos.cs`
- `Catalog/PriceDtos.cs`
- `Commerce.Contracts.csproj` — added `ProjectReference` to `Commerce.Domain`

### `Commerce.Application` (new)
- `Common/Exceptions/NotFoundException.cs`
- `Common/Exceptions/DuplicateKeyException.cs`
- `Common/Exceptions/InvalidStateTransitionException.cs`
- `Common/Exceptions/InvalidRelationshipException.cs`
- `Common/Time/IClock.cs` + `SystemClock.cs`
- `Catalog/Abstractions/IProductCatalogService.cs`
- `Catalog/Abstractions/IFeatureCatalogService.cs`
- `Catalog/Abstractions/IPlanCatalogService.cs`
- `Catalog/Abstractions/IAddonCatalogService.cs`
- `Catalog/Abstractions/IBundleCatalogService.cs`
- `Catalog/Abstractions/IPriceCatalogService.cs`
- `Catalog/Validators/CatalogRequestValidators.cs`
- `DependencyInjection.cs` — adds `IClock` registration

### `Commerce.Infrastructure` (new + changed)
- `Persistence/Configurations/CatalogConfigurations.cs` — 8 `IEntityTypeConfiguration<>`
- `Persistence/CommerceDbContext.cs` — adds 8 DbSets and `ApplyConfiguration` calls
- `Catalog/Mapping/CatalogMappers.cs`
- `Catalog/Services/ProductCatalogService.cs`
- `Catalog/Services/FeatureCatalogService.cs`
- `Catalog/Services/PlanCatalogService.cs`
- `Catalog/Services/AddonCatalogService.cs`
- `Catalog/Services/BundleCatalogService.cs`
- `Catalog/Services/PriceCatalogService.cs`
- `DependencyInjection.cs` — registers the 6 services as `Scoped`
- `Persistence/Migrations/20260423232809_CatalogCore.cs` (+`.Designer.cs`)
- `Persistence/Migrations/CommerceDbContextModelSnapshot.cs` — updated

### `Commerce.Api` (new + changed)
- `Middleware/ProblemDetailsExceptionMiddleware.cs`
- `Controllers/Catalog/ProductsController.cs`
- `Controllers/Catalog/FeaturesController.cs`
- `Controllers/Catalog/PlansController.cs`
- `Controllers/Catalog/AddonsController.cs`
- `Controllers/Catalog/BundlesController.cs`
- `Controllers/Catalog/PricesController.cs`
- `Program.cs` — wires `ProblemDetailsExceptionMiddleware`

### `Commerce.Tests` (new)
- `Catalog/CatalogTestHost.cs` — in-memory DbContext + reflection-resolved validators
- `Catalog/ProductCatalogTests.cs`
- `Catalog/FeatureCatalogTests.cs`
- `Catalog/PlanCatalogTests.cs`
- `Catalog/PriceCatalogTests.cs`
- `Catalog/BundleCatalogTests.cs`
- `Catalog/CatalogApiTests.cs` — uses `CommerceWebApplicationFactory`

### Other
- `analysis/COM-B02-report.md` (this file)
- `analysis/catalog-core.sql` — idempotent schema script generated from the migration

## 5. Database / Migration Changes

Migration `CatalogCore` (`20260423232809_CatalogCore`) creates 8 tables:

| Table                  | Purpose                                  |
| ---------------------- | ---------------------------------------- |
| `catalog_products`     | Product catalog root                     |
| `catalog_features`     | Per-product capability declarations      |
| `catalog_plans`        | Subscription plans (optionally per-product) |
| `catalog_plan_features`| Plan-to-feature mapping with limits      |
| `catalog_addons`       | Optional purchasable add-ons             |
| `catalog_bundles`      | Composite offerings                      |
| `catalog_bundle_items` | Bundle membership (product/plan/addon)   |
| `catalog_prices`       | Currency-scoped pricing windows          |

Conventions:

- Primary keys: `id` (binary(16) Guid).
- All tables include `created_at_utc`, `updated_at_utc`, and a `status`
  smallint mapped from `CatalogStatus`.
- Unique indexes on the normalized `key` column for Product, Plan, Addon,
  Bundle (case-insensitive lower-case keys, validator-enforced).
- Composite unique on `(product_id, key)` for Feature.
- Foreign keys with `Restrict` delete behavior so the catalog cannot be
  silently destroyed by a parent removal.

Idempotent SQL exported to `analysis/catalog-core.sql` via
`dotnet ef migrations script --idempotent`.

## 6. API Endpoints Added

All endpoints rooted at **`/api/commerce/catalog`**.

| Method  | Route                                                    | Action                           |
| ------- | -------------------------------------------------------- | -------------------------------- |
| POST    | `/products`                                              | Create product                   |
| GET     | `/products`                                              | List products                    |
| GET     | `/products/{id}`                                         | Get product                      |
| PUT     | `/products/{id}`                                         | Update product                   |
| POST    | `/products/{id}/activate`                                | Activate                         |
| POST    | `/products/{id}/retire`                                  | Retire                           |
| POST    | `/products/{productId}/features`                         | Create feature under product     |
| GET     | `/products/{productId}/features`                         | List features for product        |
| PUT     | `/features/{id}`                                         | Update feature                   |
| POST    | `/features/{id}/activate`                                | Activate feature                 |
| POST    | `/features/{id}/retire`                                  | Retire feature                   |
| POST    | `/plans`                                                 | Create plan                      |
| GET     | `/plans`                                                 | List plans                       |
| GET     | `/plans/{id}`                                            | Get plan                         |
| PUT     | `/plans/{id}`                                            | Update plan                      |
| POST    | `/plans/{id}/activate`                                   | Activate plan                    |
| POST    | `/plans/{id}/retire`                                     | Retire plan                      |
| POST    | `/plans/{planId}/features`                               | Add feature to plan              |
| GET     | `/plans/{planId}/features`                               | List plan features               |
| DELETE  | `/plans/{planId}/features/{featureId}`                   | Remove feature from plan         |
| POST    | `/addons`                                                | Create add-on                    |
| GET     | `/addons` / `/addons/{id}`                               | List / get                       |
| PUT     | `/addons/{id}`                                           | Update                           |
| POST    | `/addons/{id}/activate` \| `/retire`                     | Lifecycle                        |
| POST    | `/bundles`                                               | Create bundle                    |
| GET     | `/bundles` / `/bundles/{id}`                             | List / get                       |
| PUT     | `/bundles/{id}`                                          | Update                           |
| POST    | `/bundles/{id}/activate` \| `/retire`                    | Lifecycle                        |
| POST    | `/bundles/{bundleId}/items`                              | Add bundle item                  |
| GET     | `/bundles/{bundleId}/items`                              | List bundle items                |
| DELETE  | `/bundles/{bundleId}/items/{itemId}`                     | Remove bundle item               |
| POST    | `/prices`                                                | Create price                     |
| GET     | `/prices` / `/prices/{id}`                               | List / get                       |
| PUT     | `/prices/{id}`                                           | Update price                     |
| POST    | `/prices/{id}/activate` \| `/retire`                     | Lifecycle (overlap checked)      |

Error responses follow RFC 7807. Status code mapping:

| Exception                          | HTTP | `title`                       |
| ---------------------------------- | ---- | ----------------------------- |
| `ValidationException`              | 400  | `validation_failed`           |
| `NotFoundException`                | 404  | `not_found`                   |
| `DuplicateKeyException`            | 409  | `duplicate_key`               |
| `InvalidStateTransitionException`  | 409  | `invalid_state_transition`    |
| `InvalidRelationshipException`     | 422  | `invalid_relationship`        |
| (other)                            | 500  | `internal_error`              |

## 7. Catalog Domain Model

| Entity        | Identity   | Key fields                                        | Lifecycle |
| ------------- | ---------- | ------------------------------------------------- | --------- |
| `Product`     | `Guid Id`  | `Key (unique)`, `Name`, `Description`, `SortOrder`| Draft → Active → Retired |
| `Feature`     | `Guid Id`  | `ProductId`, `Key (unique per product)`, `Type`   | Draft → Active → Retired |
| `Plan`        | `Guid Id`  | `Key (unique)`, `ProductId?`, `BillingInterval`, `TrialDays?`, `SortOrder` | Draft → Active → Retired |
| `PlanFeature` | `Guid Id`  | `PlanId`, `FeatureId`, `IsEnabled`, `LimitValue?`, `MeteredIncludedUnits?` | n/a |
| `Addon`       | `Guid Id`  | `Key (unique)`, `ProductId?`, `Name`              | Draft → Active → Retired |
| `Bundle`      | `Guid Id`  | `Key (unique)`, `Name`                            | Draft → Active → Retired |
| `BundleItem`  | `Guid Id`  | `BundleId`, exactly one of `ProductId/PlanId/AddonId` | n/a |
| `Price`       | `Guid Id`  | exactly one of `PlanId/AddonId/BundleId`, `Currency`, `AmountMinor`, `BillingInterval`, `EffectiveFromUtc`, `EffectiveToUtc?` | Draft → Active → Retired |

All entities use **private setters** with intent-revealing factory and
mutator methods (`Create`, `Update`, `Activate`, `Retire`) so invariants
are enforced at the domain layer rather than scattered across services.

## 8. Validation Rules Implemented

Implemented in
`Commerce.Application/Catalog/Validators/CatalogRequestValidators.cs`.

| Subject       | Rule                                                              |
| ------------- | ----------------------------------------------------------------- |
| `Key`         | 2–64 chars, alphanumeric or `-`, `_`, `.`; normalized to lowercase|
| `Name`        | 1–200 chars                                                       |
| `Description` | optional, ≤ 2000 chars                                            |
| `SortOrder`   | 0–10000                                                           |
| `TrialDays`   | optional, 0–365 if set                                            |
| `Currency`    | exactly 3 uppercase ASCII letters (e.g. `USD`, `EUR`)             |
| `AmountMinor` | ≥ 0 (integer minor units)                                         |
| `EffectiveFromUtc` / `EffectiveToUtc` | `To` must be strictly greater than `From` |
| `Price` ref   | exactly one of `PlanId`, `AddonId`, `BundleId`                    |
| `BundleItem` ref | exactly one of `ProductId`, `PlanId`, `AddonId`                |
| `BillingInterval` | must be a defined enum value                                  |
| `FeatureType` | must be a defined enum value                                      |

Cross-entity rules enforced in services (raise
`InvalidRelationshipException`):

- Cannot create a feature on a retired product.
- Cannot create / activate a plan attached to a retired product.
- Cannot add a retired feature to a plan.
- Plan-feature: feature's product must match plan's product when the plan
  is product-specific.
- Boolean feature on a plan must NOT carry `LimitValue`; Limit feature
  MUST carry `LimitValue` when enabled.
- Bundle items cannot reference retired catalog items.
- Active prices for the same item, currency, and billing interval must
  not overlap. Re-checked on **both** `Activate` and `Update` (when the
  price is Active) so the invariant cannot be bypassed by editing an
  already-active row.
- Plan-feature add/remove is blocked when the plan is Retired
  (`InvalidStateTransitionException`); only metadata updates are allowed
  on retired plans.

## 9. Tests Added

39 tests in `Commerce.Tests` (5 from COM-B01 + 34 new):

- `Catalog/ProductCatalogTests.cs` — create/normalize key, duplicate
  rejection, activate/retire lifecycle, retired-cannot-reactivate.
- `Catalog/FeatureCatalogTests.cs` — create under product, duplicate key
  per product, same key allowed across products, retired product rejects
  new features.
- `Catalog/PlanCatalogTests.cs` — create, activate, retired-product
  rejection, retired-plan metadata-only updates.
- `Catalog/PlanCatalogTests.cs::PlanFeatureTests` — limit-feature requires
  `LimitValue`, boolean-feature rejects `LimitValue`, product mismatch,
  retired-feature add rejected, **retired-plan rejects AddFeature**,
  **retired-plan rejects RemoveFeature**.
- `Catalog/PriceCatalogTests.cs` — exactly-one-ref enforcement, currency
  shape, negative amount, `EffectiveTo > EffectiveFrom`, overlap on
  activation, **overlap on update of an Active price**, non-overlapping
  windows allowed.
- `Catalog/BundleCatalogTests.cs` — exactly-one-ref enforcement,
  retired-item rejection, add+list happy path.
- `Catalog/CatalogApiTests.cs` — health/swagger still load,
  product create→get→list HTTP roundtrip, duplicate key → 409, unknown
  id → 404, validation failure → 400. Uses the existing
  `CommerceWebApplicationFactory` which already forces the InMemory
  fallback for tests.

## 10. Validation Results

| Command                                                             | Result   | Notes |
| ------------------------------------------------------------------- | -------- | ----- |
| `dotnet restore Commerce.sln`                                       | ✅ pass  | All 6 projects restored |
| `dotnet build Commerce.sln -c Release`                              | ✅ pass  | 0 warnings, 0 errors |
| `dotnet test Commerce.sln -c Release --no-build`                    | ✅ pass  | **39 / 39 tests passed** |
| `dotnet ef migrations add CatalogCore`                              | ✅ pass  | Generated 8 `CreateTable` ops |
| `dotnet ef migrations script --idempotent`                          | ✅ pass  | Wrote `analysis/catalog-core.sql` (540 lines, 10 `CREATE TABLE`s — InitialCreate + CatalogCore) |
| `docker build -t commerce-api:b02 services/Commerce`                | ⚠ skipped | No Docker daemon in this environment (`Cannot connect to the Docker daemon`). The existing `services/Commerce/Dockerfile` from COM-B01 was not modified; project layout, ports, and entrypoint are unchanged, so the image build is expected to succeed in any environment with Docker. |

## 11. Known Gaps / Deferred Items

Within COM-B02 scope:

- **No seed data.** The catalog ships empty; populating it is an admin
  operation via the new endpoints.
- **No paging on list endpoints.** Catalogs are expected to be small
  (tens to low-hundreds of rows). Pagination is trivial to add later if
  needed but was outside this block's acceptance.
- **No optimistic concurrency token** on catalog rows. Concurrent admin
  updates use last-write-wins. Acceptable for a draft/active/retire admin
  surface; revisit if multi-admin contention becomes a problem.
- **Price overlap check is per item + currency + billing interval.**
  Cross-currency or cross-interval overlap is intentionally allowed.
- **No audit trail entity.** `created_at_utc` / `updated_at_utc` are
  recorded but no per-mutation history is kept. Audit is deferred to a
  later block (would also require user identity, see B03).

Outside COM-B02 by design (see Section 12).

## 12. Confirmation of Strict Exclusions

The following were **explicitly NOT implemented** in COM-B02 and are
deferred to later blocks:

- Tenant mapping / `HostTenantId`
- Identity adapters / JWT tenant extraction
- Billing accounts, subscriptions, invoices, payments
- Stripe integration, webhooks, checkout flows
- Account standing, entitlement enforcement, product provisioning
- LegalSynq-specific platform integration
- Tenant Portal UI, Control Center UI

Catalog APIs are admin-style but **host-platform-agnostic**: they do not
read tenant context, do not enforce per-tenant authorization, and do not
read or mutate any subscription/billing state. The exception middleware
emits standard ProblemDetails without leaking infrastructure detail.

## 13. Recommended Next Block

**COM-B03 — Identity & Tenant Context Adapters** (auth/JWT, tenant
extraction, request scoping) — required before any subscription or
entitlement work can attach catalog data to a customer.
