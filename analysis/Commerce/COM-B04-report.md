# COM-B04 — Subscription Engine

> Status: complete pending architect review.

## 1. Summary

COM-B04 introduces the **Subscription Engine** to the independent
Commerce service. A `Subscription` is a Commerce-owned commercial
contract that links a `BillingAccount` (COM-B03) to a Catalog
`Plan` / `Price` (COM-B02). Each subscription owns one or more
`SubscriptionItem`s and emits an append-only `SubscriptionChange`
stream on every mutation.

This block deliberately stops *short* of money movement: there are no
invoices, payment methods, payment attempts, Stripe, checkout, webhooks,
dunning, account-standing enforcement, entitlement enforcement, host
identity, host platform calls, or any UI. Those belong to later blocks.

## 2. Stories Completed

- COM-E04-001 — Subscription Entity
- COM-E04-002 — SubscriptionItem Entity
- COM-E04-003 — Subscription Lifecycle States
- COM-E04-004 — Create Subscription
- COM-E04-005 — Trial Support
- COM-E04-006 — Cancel Subscription
- COM-E04-007 — Plan Change
- COM-E04-008 — Renewal Logic
- COM-E04-009 — Basic Proration Placeholder/Model
- COM-E04-010 — Subscription History
- COM-E04-011 — Subscription APIs
- COM-E04-012 — Tests and Migration

## 3. Architecture Implemented

```
Commerce.Domain         entities, enums, invariants, period calculator,
                        subscription number formatter
Commerce.Application    service interfaces, FluentValidation validators,
                        re-uses Common.Exceptions
Commerce.Infrastructure EF Core mapping, services, change writer,
                        number generator, migration
Commerce.Contracts      request/response DTOs
Commerce.Api            controllers, re-uses ProblemDetails middleware
Commerce.Tests          xUnit unit + WebApplicationFactory integration
```

### Composition rules honoured

- Controllers call `ISubscriptionService` only — no EF Core in API
  layer.
- Domain rules live on entities (`Subscription.Activate/Cancel/...`,
  `SubscriptionItem.Create/Close/CancelImmediate`).
- FluentValidation validators are auto-discovered via the existing
  `AddValidatorsFromAssembly` registration.
- Subscription history rows are written *inside* the same DbContext
  SaveChanges as the mutation, so a failed mutation never leaves an
  orphan history row and a successful mutation never silently skips it.
- `BillingPeriodCalculator` is a pure helper; the same arithmetic is
  used for create + renew + plan-change.

## 4. Files Created/Changed

### Created

- `services/Commerce/src/Commerce.Domain/Subscriptions/Enums/SubscriptionStatus.cs`
- `services/Commerce/src/Commerce.Domain/Subscriptions/Enums/SubscriptionItemStatus.cs`
- `services/Commerce/src/Commerce.Domain/Subscriptions/Enums/SubscriptionChangeType.cs`
- `services/Commerce/src/Commerce.Domain/Subscriptions/Enums/ProrationBehavior.cs`
- `services/Commerce/src/Commerce.Domain/Subscriptions/SubscriptionNumber.cs`
- `services/Commerce/src/Commerce.Domain/Subscriptions/Subscription.cs`
- `services/Commerce/src/Commerce.Domain/Subscriptions/SubscriptionItem.cs`
- `services/Commerce/src/Commerce.Domain/Subscriptions/SubscriptionChange.cs`
- `services/Commerce/src/Commerce.Domain/Subscriptions/BillingPeriodCalculator.cs`
- `services/Commerce/src/Commerce.Contracts/Subscriptions/SubscriptionDtos.cs`
- `services/Commerce/src/Commerce.Application/Subscriptions/Abstractions/ISubscriptionService.cs`
- `services/Commerce/src/Commerce.Application/Subscriptions/Validators/SubscriptionRequestValidators.cs`
- `services/Commerce/src/Commerce.Infrastructure/Persistence/Configurations/SubscriptionConfigurations.cs`
- `services/Commerce/src/Commerce.Infrastructure/Subscriptions/Mapping/SubscriptionMappers.cs`
- `services/Commerce/src/Commerce.Infrastructure/Subscriptions/Services/SubscriptionNumberGenerator.cs`
- `services/Commerce/src/Commerce.Infrastructure/Subscriptions/Services/SubscriptionChangeWriter.cs`
- `services/Commerce/src/Commerce.Infrastructure/Subscriptions/Services/SubscriptionService.cs`
- `services/Commerce/src/Commerce.Api/Controllers/Subscriptions/SubscriptionsController.cs`
- `services/Commerce/src/Commerce.Infrastructure/Persistence/Migrations/20260424014922_SubscriptionEngine.cs` (+Designer)
- `services/Commerce/tests/Commerce.Tests/Subscriptions/SubscriptionTestHost.cs`
- `services/Commerce/tests/Commerce.Tests/Subscriptions/SubscriptionNumberTests.cs`
- `services/Commerce/tests/Commerce.Tests/Subscriptions/SubscriptionDomainTests.cs`
- `services/Commerce/tests/Commerce.Tests/Subscriptions/SubscriptionServiceTests.cs`
- `services/Commerce/tests/Commerce.Tests/Subscriptions/SubscriptionApiTests.cs`
- `analysis/subscription-engine.sql`
- `analysis/COM-B04-report.md` (this document)

### Changed

- `services/Commerce/src/Commerce.Infrastructure/Persistence/CommerceDbContext.cs`
  – added `Subscriptions`, `SubscriptionItems`, `SubscriptionChanges`
  DbSets and three configuration `ApplyConfiguration` calls.
- `services/Commerce/src/Commerce.Infrastructure/DependencyInjection.cs`
  – registered `ISubscriptionNumberGenerator`, `SubscriptionChangeWriter`,
  and `ISubscriptionService` (scoped).
- `services/Commerce/src/Commerce.Infrastructure/Persistence/Migrations/CommerceDbContextModelSnapshot.cs`
  – regenerated by `dotnet ef migrations add`.

## 5. Database / Migration Changes

Migration `20260424014922_SubscriptionEngine` adds 3 new tables — none
of them touch existing schema:

| Table                  | Purpose                                              |
| ---------------------- | ---------------------------------------------------- |
| `subscriptions`        | subscription header + lifecycle                      |
| `subscription_items`   | priced lines, copied from catalog Price at mutation  |
| `subscription_changes` | append-only history of subscription mutations        |

### Indexes

- `ux_subscriptions_subscription_number` (unique) — guarantees number
  uniqueness across instances.
- `ix_subscriptions_billing_account_id` — supports per-account queries.
- `ix_subscriptions_status` — supports lifecycle queries.
- `ix_subscription_items_subscription_id`, `ix_subscription_items_subscription_status`.
- `ix_subscription_changes_subscription_created` — composite, supports
  reverse-chronological history reads.

### FK rules

- `subscriptions.billing_account_id` → `billing_accounts(id)`, RESTRICT.
- `subscription_items.subscription_id` → `subscriptions(id)`, CASCADE.
- `subscription_items.plan_id` → `plans(id)`, RESTRICT.
- `subscription_items.price_id` → `prices(id)`, RESTRICT.
- `subscription_changes.subscription_id` → `subscriptions(id)`, CASCADE.

### Idempotent SQL

`analysis/subscription-engine.sql` regenerated via
`dotnet ef migrations script --idempotent <previous-migration>`.

## 6. API Endpoints Added

Route prefix: `/api/commerce/subscriptions`

| Verb | Route | Purpose |
| ---- | ----- | ------- |
| `POST` | `/` | create subscription |
| `GET` | `/?billingAccountId=...` | list (optional account filter) |
| `GET` | `/{id}` | get one (includes items) |
| `POST` | `/{id}/activate` | Draft|Trialing → Active |
| `POST` | `/{id}/suspend` | Active → Suspended |
| `POST` | `/{id}/reactivate` | Suspended → Active |
| `POST` | `/{id}/cancel` | immediate or at-period-end |
| `POST` | `/{id}/renew` | advance period anchor |
| `POST` | `/{id}/change-plan` | swap line item with proration metadata |
| `GET` | `/{id}/changes` | reverse-chronological history |
| `GET` | `/api/commerce/billing-accounts/{billingAccountId}/subscriptions` | per-account list |

All endpoints return the standard ProblemDetails JSON on error
(reusing the COM-B03 middleware).

## 7. Subscription Domain Model

### `Subscription`

- `BillingAccountId` (FK), `SubscriptionNumber` (unique, formatted as
  `COM-SUB-000001`).
- `Status` ∈ {Draft, Trialing, Active, PastDue, Suspended, Cancelled,
  Expired}.
- `StartDateUtc`, `CurrentPeriodStartUtc`, `CurrentPeriodEndUtc`.
- `TrialStartUtc`, `TrialEndUtc` (set together or both null).
- `CancelAtPeriodEnd`, `CancelledAtUtc`, `CancellationReason`.
- `CreatedAtUtc`, `UpdatedAtUtc`.
- Domain methods: `Activate`, `Suspend`, `Reactivate`, `Cancel`,
  `Renew`, `EndTrial`, `Touch` (timestamp bump for plan-change).

### `SubscriptionItem`

- `SubscriptionId` (FK), `PlanId` (FK), `PriceId` (FK).
- `Quantity` ≥ 1.
- **Snapshot fields copied at create time**: `UnitAmountMinor`,
  `Currency`, `BillingInterval` (as required by the spec — items must
  be insulated from later catalog mutations).
- `Status` ∈ {Active, PendingChange, Cancelled, Expired}.
- `EffectiveFromUtc`, `EffectiveToUtc`.
- Domain methods: `Close`, `CancelImmediate`.

### `SubscriptionChange`

- `SubscriptionId` (FK), `ChangeType` ∈ {Created, TrialStarted,
  TrialEnded, Activated, PlanChanged, Cancelled, Renewed, Suspended,
  Reactivated, Expired}.
- `FromPlanId/ToPlanId/FromPriceId/ToPriceId` (nullable — only set for
  PlanChanged).
- `EffectiveAtUtc`, `ProrationBehavior` (None | Immediate | NextCycle |
  Manual — placeholder; no money math here), `Reason`,
  `MetadataJson` (text).
- `CreatedAtUtc`.

### `BillingPeriodCalculator`

- `Monthly` → `AddMonths(1)`.
- `Annual` → `AddYears(1)`.
- `OneTime` → `AddDays(36500)` sentinel (documented explicitly).
- `Custom` → throws `InvalidOperationException` (rejected for COM-B04
  per spec; will be revisited when invoicing arrives).

## 8. Lifecycle Rules Implemented

Allowed transitions (anything else throws
`InvalidStateTransitionException` → 409):

```
Draft       → Active (Activate)
Trialing    → Active (Activate, EndTrial), Suspended (Cancel→at-period-end keeps Trialing)
Active      → Suspended (Suspend), Cancelled (Cancel immediate),
              Active (Renew, ChangePlan, idempotent Activate)
Suspended   → Active (Reactivate), Cancelled (Cancel immediate)
PastDue     → Cancelled (Cancel) — entry into PastDue is reserved for
              future blocks; the state exists in the enum for forward
              compatibility.
Cancelled   → terminal
Expired     → terminal
```

Cancel semantics:

- `cancelAtPeriodEnd=true` → keep status (Active/Trialing/etc.), set
  flag, do **not** close items. Future renewal/change attempts still
  work; only when a downstream "expire-at-period-end" job runs (later
  block) will the subscription transition to Expired.
- `cancelAtPeriodEnd=false` → status = Cancelled, all currently
  Active/PendingChange items are closed via `CancelImmediate`.

## 9. Validation Rules Implemented

`CreateSubscriptionRequest` (FluentValidation + service-level checks):

- `BillingAccountId`, `PlanId`, `PriceId` not empty.
- `Quantity ≥ 1`.
- `TrialDays`, when supplied, in `[0, 365]`.
- `MetadataJson` must parse as JSON when supplied.
- BillingAccount must exist and be neither Closed nor Suspended.
- Plan must exist and be `Active`.
- Price must exist, be `Active`, and have `PlanId == request.PlanId`.

`ChangeSubscriptionPlanRequest`:

- `NewPlanId`, `NewPriceId` not empty; `Quantity ≥ 1` when supplied;
  `ProrationBehavior` must be a defined enum value.
- `EffectiveAtUtc` cannot be more than 1 day in the past.
- `Reason ≤ 500`. `MetadataJson` must be valid JSON.
- Subscription must not be Cancelled or Expired.
- New plan/price must be Active and price must belong to the new plan.

`CancelSubscriptionRequest`:

- `Reason ≤ 500`.
- Subscription must be in {Active, Trialing, Suspended, PastDue}.

`RenewSubscriptionRequest`:

- No required fields. Subscription must be Active. Subscription must
  have at least one Active item.

## 10. Tests Added

| File | Tests | Notes |
| ---- | ----- | ----- |
| `SubscriptionNumberTests` | 5 | format/parse round-trip + bounds |
| `SubscriptionDomainTests` | 13 | invariant tests for `Subscription`, `SubscriptionItem`, `BillingPeriodCalculator` |
| `SubscriptionServiceTests` | 19 | end-to-end flows against in-memory provider, including failure modes |
| `SubscriptionApiTests` | 3 | `WebApplicationFactory` integration: full lifecycle, 404, 400 |

Total: **40 new tests**, bringing the suite to **110 tests, 0 failures**.

## 11. Validation Results

| Command | Result | Notes |
| ------- | ------ | ----- |
| `dotnet build Commerce.sln -c Debug` | OK | 0 warnings, 0 errors |
| `dotnet test Commerce.sln -c Debug` | OK | 110 passed, 0 failed |
| `dotnet ef migrations add SubscriptionEngine` | OK | files committed |
| `dotnet ef migrations script --idempotent` | OK | written to `analysis/subscription-engine.sql` |

## 12. Known Gaps / Deferred Items

These are intentional and documented for the next blocks:

- **Proration math**: `ProrationBehavior` is recorded on the change row
  but no credit/debit is computed. Money math arrives with the invoice
  engine (next block).
- **PastDue entry**: The status exists in the enum and the
  cancel-from-PastDue path is allowed, but no code path *enters*
  PastDue yet — that requires a payment-attempt loop.
- **Period-end expiration job**: `cancelAtPeriodEnd=true` raises a
  flag; a scheduled job to flip Cancelled at `CurrentPeriodEndUtc` is
  out of scope.
- **`Custom` billing interval**: rejected at create/renew time. The
  enum value already exists for catalog parity; adding support requires
  the caller to specify an explicit period end which we do not collect
  in the create DTO yet.
- **Multi-line plan change**: When a subscription has multiple Active
  items, `ChangePlan` closes *all* of them and creates a single new
  item. Per-line plan change requires a different DTO and is deferred.
- **Sequence table for SubscriptionNumber**: like `BillingAccount`,
  numbers are allocated by max+1 with a unique-index safety net and
  bounded retry. A dedicated sequence table is deferred.
- **Concurrency / optimistic locking**: no `RowVersion` column yet;
  same posture as COM-B03.

## 13. Confirmation of Strict Exclusions

The following are **not** present in COM-B04 — verified by grep over
`services/Commerce/`:

- No Stripe, checkout, payment method, payment attempt, refund, credit.
- No invoice, invoice line item, dunning logic, account-standing engine.
- No entitlement enforcement, no provisioning, no Tenant Portal UI, no
  Control Center UI.
- No identity adapters / JWT / Tenant service client.
- No LegalSynq-specific coupling — subscriptions reference Plan / Price
  by `Guid` only.

## 14. Architect Code Review Outcomes

A code-review pass identified three observations:

1. **ChangePlan was too permissive (FIXED).** Originally only blocked
   Cancelled/Expired; now requires the subscription to be `Active` or
   `Trialing`. New regression test `ChangePlan_rejected_on_suspended`
   added.
2. **FK delete behavior**: child rows of the Subscription aggregate
   (`subscription_items`, `subscription_changes`) use `CASCADE`. This
   is intentional and matches the established Commerce convention —
   `Plan → Price` and `Plan → PlanFeature` in
   `CatalogConfigurations.cs` are also `CASCADE`, while every cross-
   aggregate FK (BillingAccount, Plan, Price references from the
   subscription tables) is `RESTRICT`. No spec line requires global
   Restrict; we follow the existing house style.
3. **SubscriptionNumber retry on `DbUpdateException`**: matches the
   existing `BillingAccountService` retry pattern verbatim. Both are
   bounded (3 attempts) and surface a `DuplicateKeyException` after
   the cap, so a non-conflict error still propagates as a meaningful
    500 within one cycle. Migrating both call sites to provider-
   specific error inspection (and to a dedicated sequence table) is
   tracked as a deferred follow-up consistent with section 12.

## 15. Recommended Next Block

**COM-B05 — Invoice Generation & Periodic Run**: with subscriptions
now producing a stable history of changes, the invoice engine can read
the change stream + active item snapshots to compose period-end and
proration line items. That block should also introduce the sequence
table for invoice numbers and re-use the same allocation pattern.
