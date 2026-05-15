# TB-DATA-02 — Commerce Subscription & Entitlement Bridge

> **Status:** in progress.
> Updated incrementally as each major step lands.

## 1. Summary

Adds a local **entitlement bridge** to the canonical Tenant Billing service
(`services/tenant-billing/`, `Billing.*` assemblies) so a tenant's right to
operate Tenant Billing can be driven by Commerce platform billing state
**without merging the two billing domains**.

Pipeline:

```
Commerce SaaS Billing
        ↓ (snapshot DTO; no live HTTP in this block)
TenantBillingEntitlementSnapshot   ← TB-DATA-02
        ↓
TenantBillingProfile (TB-DATA-01)
        ↓
ITenantBillingEnablementResolver   ← TB-DATA-02
```

This block is **advisory only** — no existing customer / invoice / payment
endpoint is gated by entitlement yet (deferred to a later block).

## 2. Codebase Analysis

- TB-DATA-01 already shipped `TenantBillingProfile` (Draft/Active/Suspended/Closed)
  + `ITenantBillingAccountResolver` returning the Commerce `BillingAccountId`
  for Active profiles only.
- Repository, controller, DI, migration patterns established.
- Internal-token + tenant-resolution middleware already gate every `/api/*`.
- DbContext supports both InMemory (tests) and Pomelo MySQL (runtime).

## 3. Commerce Entitlement Contract Findings

`services/Commerce/src/Commerce.Contracts/Integration/EntitlementSnapshotDtos.cs`
already defines `CommerceEntitlementSnapshot` with these fields the bridge
must mirror as opaque external refs:

- `BillingAccountId` (Guid)
- `AccountStandingStatus` — string (Good / Trialing / GracePeriod / PastDue /
  Suspended / Cancelled / Closed)
- `AccessRecommendation` — enum (Unknown / Allow / ReadOnly / GraceLimited / Block)
- `Subscriptions[]` (`SubscriptionId`, `SubscriptionNumber`, `Status`)
- `Plans[]`, `Products[]`
- `GeneratedAtUtc`

The Tenant Billing snapshot DTO uses identical wire names so a future
publisher can serialize Commerce → TB without translation.

## 4. TenantBillingProfile Findings

The TB-DATA-01 profile is intentionally minimal (lifecycle only). Three
convenience mirror fields would be needed on the profile to read
"is this tenant enabled?" without a join. We chose **not** to duplicate
state on the profile — see §5.

## 5. Entitlement Bridge Design Decision

**Chosen:** separate `TenantBillingEntitlementSnapshot` aggregate, one
current row per profile (update in place). The profile entity stays
unchanged.

Why:
- Avoids duplicating snapshot state on two tables (no risk of drift).
- Keeps the profile a pure lifecycle entity; entitlement is a different
  concern that may evolve (history, multi-source) without churning the
  profile schema.
- A `1:1` index `(TenantBillingProfileId)` UNIQUE enforces "one current row
  per profile" at the DB level on relational providers.

Update model: **upsert in place.** History (additional rows + `IsCurrent`)
is documented as a follow-up; not required for TB-DATA-02.

## 6. Domain Model Added

- `TenantBillingEntitlementSnapshot` (entity) — one row per profile, holds:
  - `Id`, `TenantBillingProfileId`, `TenantId`, `BillingAccountId`
  - `SourceSystem`, `SourceSnapshotId?`, `SourceSubscriptionId?`,
    `SourcePlanKey?`, `SourceProductKey?`
  - `EntitlementStatus`, `AccessRecommendation`, `Reason?`
  - `EffectiveFromUtc?`, `EffectiveToUtc?`, `LastSyncedAtUtc`
  - `RawSnapshotJson?` (validated as JSON before persist)
  - `CreatedAtUtc`, `UpdatedAtUtc`
- `TenantBillingEntitlementStatus` (string constants):
  Unknown / Enabled / Disabled / Suspended / Expired
- `TenantBillingAccessRecommendation` (string constants):
  Unknown / Allow / ReadOnly / GraceLimited / Block
- `ITenantBillingEntitlementService` + `TenantBillingEntitlementService`
  with `ApplySnapshotAsync` / `GetCurrentSnapshotAsync` / `GetByProfileIdAsync`
  / `GetAccessRecommendationAsync`.
- `ITenantBillingEnablementResolver` + `TenantBillingEnablementResolver`
  composing the profile resolver + entitlement service.
- Exceptions: `TenantBillingEntitlementProfileMismatchException`,
  `TenantBillingEntitlementInvalidJsonException`.

## 7. API Endpoints Added

All routes are tenant-scoped via the existing `X-Tenant-Id` middleware and
gated by `X-Internal-Token`. None are customer-facing.

- `POST /api/tenant-billing/entitlements/apply` — apply or replace the
  current snapshot for the profile that matches `(X-Tenant-Id, BillingAccountId)`.
- `GET  /api/tenant-billing/entitlements/current` — current snapshot for
  the tenant's active/non-closed profile.
- `GET  /api/tenant-billing/entitlements/access` — computed access decision
  (status + recommendation + write-allowed flag + reason).
- `GET  /api/tenant-billing/profiles/{profileId}/entitlement` — snapshot
  for an explicit profile id (404 if profile not in this tenant).

## 8. Database / Migration Changes

New migration `20260516120000_TenantBillingEntitlementSnapshots`:

Table `tenant_billing_entitlement_snapshots`:

- `Id` PK (char(36))
- `TenantBillingProfileId` (char(36), UNIQUE) — one current row per profile
- `TenantId`, `BillingAccountId` (char(36))
- `SourceSystem` varchar(100), `SourceSnapshotId/SubscriptionId` varchar(200),
  `SourcePlanKey/ProductKey` varchar(100)
- `EntitlementStatus` varchar(16), `AccessRecommendation` varchar(16)
- `Reason` varchar(1000)
- `EffectiveFromUtc?`, `EffectiveToUtc?`, `LastSyncedAtUtc`
- `RawSnapshotJson` LONGTEXT nullable
- `CreatedAtUtc`, `UpdatedAtUtc`

Indexes: `TenantId`, `BillingAccountId`, `EntitlementStatus`,
`AccessRecommendation`, `LastSyncedAtUtc`, UNIQUE `TenantBillingProfileId`.

Designer placeholder follows the WRITE-005/ERP-001/ERP-003/TB-DATA-01
convention (regenerate snapshot in dotnet-SDK env before next schema
migration).

## 9. Resolver Behavior

`ITenantBillingEnablementResolver.GetTenantBillingAccessAsync` returns:

| Profile status | Snapshot status | Snapshot recommendation | → Decision         |
|----------------|-----------------|--------------------------|---------------------|
| missing        | n/a             | n/a                      | NotEnabled, Unknown |
| Draft          | any             | any                      | NotEnabled          |
| Suspended      | any             | any                      | NotEnabled          |
| Closed         | any             | any                      | NotEnabled          |
| Active         | missing         | n/a                      | NotEnabled, Unknown (standalone fallback documented separately) |
| Active         | Disabled        | any                      | NotEnabled          |
| Active         | Suspended       | any                      | NotEnabled          |
| Active         | Expired         | any                      | NotEnabled          |
| Active         | Enabled         | Block                    | NotEnabled          |
| Active         | Enabled         | ReadOnly / GraceLimited  | Read-only (writes not enabled) |
| Active         | Enabled         | Allow                    | **Enabled**         |
| Active         | Unknown         | Unknown                  | NotEnabled          |

`IsTenantBillingEnabledAsync` returns true only for the Active+Enabled+Allow row.

## 10. Standalone Compatibility

- Existing customer / invoice / payment / statement / template / ERP / report
  endpoints are **untouched**. None of them call the entitlement resolver.
- Tenant Billing continues to function with **no** entitlement snapshot
  applied — the service just reports `AccessDecision = NotEnabled` from the
  enablement resolver, which no production code path consumes yet.
- Enforcement on existing CRUD is explicitly **out of scope** for this
  block and documented as a future deliverable.

## 11. Tests Added

### Domain (`Billing.Domain.Tests`)

- `TenantBillingEntitlementServiceTests` — apply / update / reject mismatch
  / reject closed profile / invalid JSON / invalid enum / tenant isolation.
- `TenantBillingEnablementResolverTests` — full decision matrix from §9.

### API (`Billing.Tests`)

- `TenantBillingEntitlementsApiTests` — round-trip POST apply, GET current,
  GET access, GET by profile, missing tenant header, missing internal token,
  cross-tenant 404.

### Regression

- All pre-existing `Billing.Domain.Tests` and `Billing.Tests` continue to
  pass (no changes to invoice, customer, payment, statement, template, ERP
  tests).
- TB-DATA-01 profile tests still pass (no change to the profile entity,
  service, repository, or controller).

## 12. Validation Results

### Build

```
dotnet build services/tenant-billing/Billing.sln -c Debug
→ 0 Warnings, 0 Errors
```

### Domain tests

```
dotnet test services/tenant-billing/tests/Billing.Domain.Tests --no-build
→ Passed: 462 / 462  (Failed: 0, Skipped: 0)
```

The 462 includes the 24 new TB-DATA-02 cases:
- `TenantBillingEntitlementServiceTests` — 11 cases.
- `TenantBillingEnablementResolverTests` — 13 cases (incl. an 8-row
  `[Theory]` enumerating non-Allow status × recommendation combos).
- All TB-DATA-01 (TenantBillingProfile) and earlier domain tests still
  green.

### API tests

The `Billing.Tests` integration suite is too large to fit in a single
120-second shell timeout in this environment, so it was run in five
class-grouped chunks; aggregate result: **Passed: 121 / 121, Failed: 0**.

| Chunk filter                                                  | Passed |
|---------------------------------------------------------------|--------|
| `~CustomersLookup ∪ ~CustomerStatement ∪ ~Statements*`        | 37     |
| `~InvoiceCreation ∪ ~InvoiceRender ∪ ~InvoicesMarkOverdue ∪ ~InvoicesTemplateStamping` | 42     |
| `~InvoiceTemplatesPlatform ∪ ~InvoiceTemplatesConflict`       | 13     |
| `~InvoiceTemplatesTenant`                                     | 5      |
| `~TenantBillingProfiles ∪ ~TenantBillingEntitlements`         | 24     |

The new `TenantBillingEntitlementsApiTests` accounts for **14** of the
24 in the last chunk:

- Apply success / in-place update / 404 (no profile) / 409 (mismatch) /
  **409 (closed profile)** / 400 (bad enum) / 400 (bad JSON)
- GetCurrent success / 404
- GetAccess enabled-true / NotEnabled when no profile
- GetByProfile owner-200 / stranger-404
- Apply without `X-Tenant-Id` → 400
- Apply without `X-Internal-Token` → 401

### Architect-review fix (post-implementation)

A code review caught that `TenantBillingEntitlementClosedProfileException`
was originally unreachable: the bridge looked up profiles via
`ITenantBillingProfileRepository.GetByBillingAccountAsync`, which
intentionally filters out `Closed` rows for the standard CRUD use case,
so a closed-profile apply produced a 404 instead of the spec'd 409.

Fix:

- Added `ITenantBillingProfileRepository.GetAnyByBillingAccountAsync`
  (and EF + in-memory implementations) which performs the same
  tenant-scoped lookup but does NOT filter on status.
- `TenantBillingEntitlementService.ApplySnapshotAsync` now uses the
  include-Closed lookup, so it can distinguish "no profile" (→ 404)
  from "profile exists but is Closed" (→ 409). The mismatch path
  (different active billing account → 409) is preserved.
- Updated the closed-profile domain test to assert
  `TenantBillingEntitlementClosedProfileException` (was incorrectly
  asserting `TenantBillingProfileNotFoundException`).
- Added a new API test `Apply_returns_409_when_profile_is_closed`
  that creates a profile, closes it, and verifies the apply call
  returns `409 Conflict`.

No public surface change; no other repository call sites were touched.

## 13. Risks / Deferred Items

- **No live Commerce HTTP** — push-only API; the publisher is a future
  block (TB-INT-01).
- **No history / audit log** — current snapshot is overwritten in place;
  add `IsCurrent` + history rows in a follow-up if needed.
- **No enforcement** on existing CRUD endpoints. The resolver is observable
  only.
- **No background sync** — operator/process must POST snapshots.
- **Designer migration snapshot** is a placeholder; regenerate in a
  dotnet-SDK environment before adding the next schema migration (same
  convention as TB-DATA-01).

## 14. Confirmation of Strict Exclusions

The following items in the task brief were **NOT** implemented (by design):

- ✗ live HTTP calls to Commerce
- ✗ LegalSynq Identity integration
- ✗ Control Center UI
- ✗ Tenant Portal UI
- ✗ route namespace migration
- ✗ invoice/payment table merge
- ✗ payment provider changes
- ✗ subscription billing engine rewrite
- ✗ entitlement enforcement on customer/invoice/payment APIs
- ✗ automatic background sync job
- ✗ Notifications integration
- ✗ Documents integration

## 15. Recommended Next Block

**TB-INT-01 — Commerce → Tenant Billing entitlement publisher**: a
small, isolated job (or webhook) on the Commerce side that emits the
already-defined `CommerceEntitlementSnapshot` to Tenant Billing's
`POST /api/tenant-billing/entitlements/apply`. Still no schema merge.

Following that, **TB-ENF-01 — entitlement enforcement** wires
`ITenantBillingEnablementResolver` into the customer / invoice / payment
controllers as a soft-block (read-only or block).
