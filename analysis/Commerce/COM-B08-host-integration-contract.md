# Commerce ↔ Host Integration Contract (COM-B08)

> Specification of the host-platform-neutral contract surface exposed by
> the standalone Commerce service. Defines the DTOs, interfaces, default
> (no-op) implementations, HTTP endpoints, and rules that any future
> host integration (e.g. LegalSynq, a generic SaaS host) must implement
> against.
>
> **Status:** Frozen for COM-B08 (HARD STOP block). No host integration,
> JWT/OIDC validation, YARP routing, schema migration, or LegalSynq call
> is shipped in this block.

---

## 1. Purpose & Scope

The Commerce service is a **standalone** ASP.NET Core 8 product. It owns
catalog, billing accounts, subscriptions, payments, account standing,
and provider-event reconciliation. Real-world deployments will sit
behind a **host platform** that owns tenant identity, user identity, and
in-product enforcement.

COM-B08 introduces the contract surface that lets a host platform
integrate with Commerce **without coupling Commerce to any specific
host**:

* Commerce **publishes** facts (entitlement snapshots, access
  recommendations, contracts health).
* Commerce **consumes** small DTOs (host identity context, host tenant
  reference, provisioning hook request) — but in COM-B08 it only
  consumes **default no-op** producers of those DTOs.
* The host **owns enforcement** in its product surface. Commerce
  recommends; the host enforces.

The scope of this document is the contract itself: types, interfaces,
HTTP shape, and behavioural rules. Implementation strategies for any
specific host (LegalSynq Tenant/Identity, Stripe/Paddle webhooks routed
through the host, etc.) are explicitly **out of scope**.

---

## 2. Boundaries & Non-Goals (HARD STOP)

The following are **explicitly excluded** from COM-B08 and **must not**
be added in this block. They belong to a future "host integration"
phase tracked separately.

| Excluded | Rationale |
| --- | --- |
| LegalSynq Tenant/Identity service calls | Commerce stays host-neutral. |
| YARP / reverse-proxy wiring | Hosting topology is a host concern. |
| JWT / OIDC validation, key resolution, audience checks | No real auth in B08. |
| Authorization policies on `/api/commerce/integration/*` | Surface is open in B08; future block adds host-issued tokens. |
| In-product enforcement of `AccessRecommendation` | Host enforces; Commerce only recommends. |
| Any outbound provisioning HTTP call | Default publisher is no-op; logs only. |
| Schema/migration changes for host integration | All data sourced from existing tables (COM-B03 mappings, etc.). |
| Background sync / cache of host tenant directory | Mapping read directly from `BillingAccountExternalRef`. |
| Stripe/Paddle event re-routing through a host | Provider events stay direct to Commerce per COM-B05. |

If a future change touches any of the rows above it is **not** part of
COM-B08; it must be planned as its own block with its own report.

---

## 3. Roles & Terminology

* **Commerce service** — this service. Owns catalog, billing, payments,
  subscriptions, account standing, provider events.
* **Host platform** — an external system that owns tenant identity and
  user identity (e.g. LegalSynq). Commerce is host-neutral; multiple
  host platforms can integrate with the same Commerce deployment in
  principle, distinguished by `HostPlatformKey`.
* **Billing account** (`BillingAccount`) — Commerce's own commercial
  customer record, identified by `BillingAccountId` (Guid).
* **Host tenant reference** (`BillingAccountExternalRef`,
  `HostTenantRef` DTO) — the seeded mapping between a host's tenant id
  and a Commerce billing account, established in COM-B03.
* **Entitlement snapshot** — a host-neutral, output-only projection of
  "what does this account commercially have right now".
* **Access recommendation** — Commerce's commercial opinion (Allow,
  ReadOnly, GraceLimited, Block, Unknown) derived from billing account
  status, account standing, and active/trialing subscriptions.
* **Provisioning hook** — a contract DTO + publisher pair that future
  blocks will use to ask a host to provision/deprovision/suspend/resume
  a tenant in response to a commercial event. **No-op in B08.**
* **Identity context** — host-asserted caller identity DTO. **Anonymous
  no-op in B08.**

---

## 4. Contract DTOs

All DTOs live in `Commerce.Contracts/Integration/` and are immutable
records. They are intentionally framework-light (no MediatR, FluentValidation,
or persistence attributes) so any host adapter can serialise them to
JSON over HTTP without depending on Commerce internals.

### 4.1 `HostTenantRef`
```csharp
HostTenantRef(string HostPlatformKey,
              string ExternalTenantId,
              string? DisplayName = null,
              string? MetadataJson = null)
```
* `HostPlatformKey` — required, lowercase normalised (see
  `Commerce.Domain.Billing.HostPlatformKey.Normalize`).
* `ExternalTenantId` — required, opaque to Commerce; stored verbatim.
* `DisplayName`, `MetadataJson` — optional pass-through; not interpreted.

### 4.2 `HostIdentityContext`
```csharp
HostIdentityContext(string HostPlatformKey,
                    string? ExternalTenantId,
                    string? ExternalUserId,
                    string? Subject,
                    IReadOnlyList<string> Roles,
                    IReadOnlyList<string> Scopes,
                    bool IsAuthenticated,
                    string? MetadataJson)
```
* Provides a static `HostIdentityContext.Anonymous(hostPlatformKey)`
  factory — the only producer used in B08.
* `IsAuthenticated == false` for the default local accessor.

### 4.3 `AccessRecommendation` (enum)
`Unknown=0, Allow=1, ReadOnly=2, GraceLimited=3, Block=4`. See §8 for
the derivation table.

### 4.4 `AccessRecommendationResponse`
```csharp
AccessRecommendationResponse(Guid BillingAccountId,
                             string? HostPlatformKey,
                             string? ExternalTenantId,
                             AccessRecommendation Recommendation,
                             string Reason,
                             string AccountStandingStatus,
                             bool HasActiveOrTrialingSubscription,
                             DateTime GeneratedAtUtc)
```
* `Reason` is human-readable; safe for host UI surfacing.

### 4.5 `ProvisioningHookRequest` / `ProvisioningHookResult`
```csharp
ProvisioningAction { Provision=0, Deprovision=1, Suspend=2, Resume=3 }
ProvisioningHookRequest(HostTenantRef HostTenantRef,
                        Guid BillingAccountId,
                        Guid? SubscriptionId,
                        string? ProductKey,
                        string? PlanKey,
                        ProvisioningAction RequestedAction,
                        string? CorrelationId = null)
ProvisioningHookResult(bool Accepted, bool Delivered, string? Reason)
```
* No-op publisher returns `Accepted=true`, `Delivered=false` and never
  contacts a host.

### 4.6 Entitlement snapshot family
```csharp
CommerceEntitlementSnapshot(
  Guid BillingAccountId, string AccountNumber, string DisplayName,
  string? HostPlatformKey, string? ExternalTenantId,
  string AccountStandingStatus, string? AccountStandingReason,
  DateTime? AccountStandingGracePeriodEndsAtUtc,
  AccessRecommendation AccessRecommendation,
  IReadOnlyList<EntitlementProductRef> Products,
  IReadOnlyList<EntitlementPlanRef> Plans,
  IReadOnlyList<EntitlementSubscriptionRef> Subscriptions,
  IReadOnlyList<EntitlementFeatureLimit> Limits,
  DateTime GeneratedAtUtc)
```
plus `EntitlementProductRef`, `EntitlementPlanRef`,
`EntitlementSubscriptionRef`, `EntitlementSubscriptionItemRef`,
`EntitlementFeatureLimit`, and the trivial
`IntegrationContractsHealthResponse`. See §9 for composition rules.

---

## 5. Application Interfaces

All interfaces live in `Commerce.Application/Integration/Abstractions/`.
They are pure abstractions — no EF, HTTP, or auth types leak in.

| Interface | Lifetime | Purpose |
| --- | --- | --- |
| `IHostIdentityContextAccessor` | Singleton | Surfaces the host-asserted identity for the current request. Replaceable per host. |
| `IHostTenantResolver` | Scoped | Two-way translation between `HostTenantRef` and `BillingAccountId`, sourced from `BillingAccountExternalRef`. |
| `IProvisioningHookPublisher` | Singleton | Publishes a `ProvisioningHookRequest`. No-op default. |
| `IHostIntegrationAdapter` | Scoped | Composite that bundles the three above + a `HostPlatformKey`. Hosts register one concrete adapter. |
| `ICommerceAccessRecommendationService` | Scoped | Computes the access recommendation for a billing account. Pure read-model. |
| `ICommerceEntitlementSnapshotService` | Scoped | Builds entitlement snapshots from Commerce-owned data. Pure read-model. |

The two `Commerce*Service` interfaces are **Commerce-owned**: they are
not intended to be replaced by host adapters. The remaining four are
**host-owned** seams with safe no-op defaults.

---

## 6. Default (No-Op) Implementations

Registered in `Commerce.Infrastructure/DependencyInjection.cs` under the
"Host Integration Contracts (COM-B08)" block.

| Concrete | Replaces | Behaviour |
| --- | --- | --- |
| `LocalHostIdentityContextAccessor` | `IHostIdentityContextAccessor` | Returns `HostIdentityContext.Anonymous("local")`. `IsAuthenticated=false`. |
| `NoopHostTenantResolver` | `IHostTenantResolver` | Reads `BillingAccountExternalRef` directly. Normalises `HostPlatformKey` via `HostPlatformKey.Normalize`. Picks the row with `IsPrimary` desc, then `CreatedAtUtc` asc. |
| `NoopProvisioningHookPublisher` | `IProvisioningHookPublisher` | Logs at Information; returns `Accepted=true, Delivered=false, Reason="noop publisher: no host adapter registered (COM-B08)."`. `Name = "noop"`. |
| `LocalHostIntegrationAdapter` | `IHostIntegrationAdapter` | Composes the three above. `HostPlatformKey == "local"`. |

These defaults are deliberately the only implementations shipped in
COM-B08. They are sufficient to keep Commerce running standalone and to
exercise every endpoint and interface end-to-end in tests.

---

## 7. HTTP Endpoints

Controller: `Commerce.Api/Controllers/Integration/HostIntegrationController.cs`.
Route prefix: `/api/commerce/integration`.

| Method | Path | Returns | Notes |
| --- | --- | --- | --- |
| GET | `/contracts/health` | `IntegrationContractsHealthResponse` | Reports the registered concrete type names of the identity accessor, tenant resolver, and provisioning publisher. |
| GET | `/billing-accounts/{billingAccountId:guid}/entitlement-snapshot?includeAllStatuses=false` | `CommerceEntitlementSnapshot` or `404` | `includeAllStatuses=false` (default) keeps only `Active`/`Trialing` subscriptions. |
| GET | `/host-tenants/{hostPlatformKey}/{externalTenantId}/entitlement-snapshot?includeAllStatuses=false` | `CommerceEntitlementSnapshot` or `404` | Maps via `IHostTenantResolver` then delegates to the billing-account form. |
| GET | `/billing-accounts/{billingAccountId:guid}/access-recommendation` | `AccessRecommendationResponse` or `404` | See §8. |

**404 semantics.** Both snapshot endpoints return 404 with a small JSON
body when no matching billing account or external mapping exists. They
do not leak host directory state — a 404 means "Commerce has no record
of this".

**No authentication.** COM-B08 deliberately ships these endpoints
without authentication or authorisation. A future block introduces the
host-issued token model.

**No write surface.** Every endpoint is `GET`. Provisioning publishing
remains an in-process call — there is no `POST /provisioning-hooks`
endpoint in B08.

---

## 8. Access Recommendation Rules

Computed by `CommerceAccessRecommendationService.ComputeRecommendation`,
a pure static method. Inputs:

* `billingAccountStatus` (`BillingAccountStatus`)
* `standingStatus` (`AccountStandingStatus?`)
* `standingReason` (`string?`, surfaced in the rendered reason)
* `hasActiveOrTrialing` (`bool`) — at least one subscription with status
  `Active` or `Trialing`.

The mapping table (precedence top-to-bottom; first match wins):

| # | Condition | Recommendation | Reason (rendered) |
| - | --- | --- | --- |
| 1 | `billingAccountStatus == Closed` | `Block` | `Billing account is closed.` |
| 2 | `billingAccountStatus == Suspended` | `Block` | `Billing account is suspended.` |
| 3 | `standingStatus is null` | `Unknown` | `No account-standing record exists for this billing account.` |
| 4 | `standingStatus == Closed` | `Block` | `Account standing: Closed.` |
| 5 | `standingStatus == Suspended` | `Block` | `Account standing: Suspended.` |
| 6 | `standingStatus == PastDue` | `ReadOnly` | `Account standing: PastDue[ ({reason})].` |
| 7 | `standingStatus == GracePeriod` | `GraceLimited` | `Account standing: GracePeriod[ ({reason})].` |
| 8 | `standingStatus == Trialing` | `Allow` | `Account standing: Trialing.` |
| 9 | `standingStatus == Good && hasActiveOrTrialing` | `Allow` | `Account standing: Good.` |
| 10 | `standingStatus == Good && !hasActiveOrTrialing` | `ReadOnly` | `No active or trialing subscription.` |
| 11 | `standingStatus == Cancelled` | `ReadOnly` | `Account standing: Cancelled.` |
| 12 | otherwise | `Unknown` | `Unhandled account standing status: {value}.` |

Rationale notes baked into the implementation:

* **PastDue → ReadOnly** rather than `GraceLimited` to keep grace
  semantics owned by `GracePeriod`. PastDue is the post-grace state
  where writes should stop but data must remain visible for remediation.
* **Good without subscription → ReadOnly** preserves the "not blocked,
  but no entitlement" stance and keeps the host UI honest.
* **Cancelled → ReadOnly** mirrors the "no entitlement, not blocked"
  stance for retention windows.
* The function is **pure** and **synchronous**; the async public surface
  exists only to perform the EF reads that feed it.

---

## 9. Entitlement Snapshot Composition

Built by `CommerceEntitlementSnapshotService.BuildAsync`. The snapshot
joins read-only data from these sources, in this order:

1. `BillingAccount` (id, account number, display name).
2. `AccountStanding` (status, reason, grace-period end). When absent,
   the snapshot reports `AccountStandingStatus = "Good"` and a null
   reason — but the recommendation engine still treats absence as
   `Unknown` (see §8).
3. `IHostTenantResolver.ResolveByBillingAccountAsync` — surfaces the
   primary `HostPlatformKey` / `ExternalTenantId` if any.
4. `ICommerceAccessRecommendationService.GetForBillingAccountAsync` —
   stamped onto the snapshot's `AccessRecommendation` field.
5. `Subscription` — filtered to `Active`/`Trialing` unless
   `includeAllSubscriptionStatuses=true`. Ordered by
   `CurrentPeriodEndUtc` desc.
6. `SubscriptionItem` joined to `Plan` — produces
   `EntitlementSubscriptionItemRef` and the distinct plan list.
7. `Product` — joined separately by id and re-stitched onto plans, so
   `EntitlementPlanRef.ProductKey` is populated when the plan has a
   product.
8. `PlanFeature` joined to `Feature` and `Plan` — produces the flat
   `Limits` list across every plan in the snapshot.

**Strict exclusions.** The snapshot intentionally omits payment
provider raw payloads, Stripe/Paddle event JSON, secrets, and any
host-side identity fields. It is safe to log and to surface to host UI.

**Time stamping.** `GeneratedAtUtc` is set from `IClock.UtcNow` at the
end of composition so the snapshot is reproducible under a fake clock.

---

## 10. Future Adapter Implementation Guide

A future "host integration" block (separate from B08) will introduce a
real adapter for one or more host platforms. The expected steps are:

1. **Add a host-specific class library** (e.g.
   `Commerce.Hosts.LegalSynq`) referencing
   `Commerce.Application` and `Commerce.Contracts`. Do not modify the
   B08 contracts.
2. **Implement `IHostIdentityContextAccessor`** to surface the
   verified caller identity from the chosen identity protocol
   (typically OIDC/JWT). Populate `HostPlatformKey`, `ExternalTenantId`,
   `ExternalUserId`, `Subject`, `Roles`, `Scopes`, and set
   `IsAuthenticated=true` only on a verified identity.
3. **Implement `IHostTenantResolver`** if the host wants live mapping
   (e.g. directory sync). The default `NoopHostTenantResolver` is
   already adequate for hosts that pre-seed mappings via COM-B03.
4. **Implement `IProvisioningHookPublisher`** to call the host's
   provisioning API. Set `Name` to a stable identifier; return
   `Delivered=true` only when the host accepted the call.
5. **Implement `IHostIntegrationAdapter`** as a thin composite of the
   three above with a stable `HostPlatformKey`.
6. **Register replacements** in DI by removing the B08 defaults and
   registering the host implementations with the same lifetimes.
7. **Add authorization** on `/api/commerce/integration/*` (host-issued
   token policy). B08 leaves the surface open.
8. **Wire enforcement on the host side.** Commerce continues to only
   *recommend* — the host's product surface enforces the recommendation
   (e.g. read-only banner, blocked routes, reactivation prompts).

The contract DTOs in §4 and the interfaces in §5 are the only
integration points; nothing else needs to change in Commerce to
integrate with a new host.

---

## 11. Versioning & Stability

* The contracts in §4 and the endpoint shapes in §7 are **frozen for
  COM-B08**. Any breaking change must be tracked in a new block with
  its own report.
* Additive changes (new optional fields on records, new enum values
  with `Unknown` fallback semantics) are permissible without a major
  bump because the records are JSON-serialised positional records and
  the `AccessRecommendation` consumer is documented to treat unknown
  values as `Unknown`.
* The Commerce-owned services
  (`ICommerceAccessRecommendationService`,
  `ICommerceEntitlementSnapshotService`) are internal SPI; they may
  evolve without contract bumps.
* Default no-op implementations are an explicit part of the contract.
  They are the only implementations shipped in B08 and remain valid
  fallbacks in production until a host adapter is registered.
* The contract is exercised end-to-end by
  `Commerce.Tests.Integration.*` (see report §11) and a host-neutrality
  test asserts no host-specific identifiers leak into the Commerce
  layers in this block.
