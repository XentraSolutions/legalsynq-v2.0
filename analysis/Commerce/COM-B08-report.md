# COM-B08 — Host Integration Contracts (HARD STOP)

> **Status:** _Complete. All work in this report is host-platform-neutral._
> **Companion contract spec:** `analysis/COM-B08-host-integration-contract.md`.

## 1. Summary

COM-B08 introduces the host-integration **contract surface** for the
standalone Commerce service. It defines DTOs, application interfaces,
default no-op infrastructure implementations, four read-only HTTP
endpoints under `/api/commerce/integration/*`, and a host-neutrality
test suite. **No** real host integration is performed: no LegalSynq
calls, no JWT/OIDC, no YARP, no enforcement, no schema migrations, no
provisioning HTTP calls. Commerce continues to run standalone exactly
as before; the new surface is additive.

The block adds 23 xUnit tests; the full Commerce test suite is **235
tests passing** (was 212 at the end of COM-B07). The EF idempotent
migration script regenerates unchanged at 1 675 lines (the single
`20260423230303_InitialCreate` migration; B08 introduces no schema
changes).

## 2. Stories Completed

* **B08.S1** — Define host-neutral contract DTOs in
  `Commerce.Contracts.Integration` (host tenant ref, identity context,
  access recommendation enum + response, provisioning hook
  request/result, entitlement snapshot family, contracts health
  response).
* **B08.S2** — Define application interfaces in
  `Commerce.Application.Integration.Abstractions` for: identity
  accessor, tenant resolver, provisioning publisher, integration
  adapter, access recommendation service, entitlement snapshot service.
* **B08.S3** — Provide default no-op infrastructure implementations
  (local identity accessor, no-op tenant resolver, no-op provisioning
  publisher, local integration adapter) so the standalone service runs
  unchanged.
* **B08.S4** — Implement Commerce-owned read-models
  (`CommerceAccessRecommendationService`,
  `CommerceEntitlementSnapshotService`) sourced purely from existing
  tables.
* **B08.S5** — Wire the four read-only endpoints under
  `/api/commerce/integration` (contracts health, snapshot by billing
  account, snapshot by host tenant, access recommendation).
* **B08.S6** — Cover the contract surface end-to-end with xUnit tests
  including a host-neutrality assertion suite.
* **B08.S7** — Author the contract specification document
  (`COM-B08-host-integration-contract.md`) and this report.

## 3. Contract Architecture Implemented

The block introduces three concentric rings:

1. **Contracts (`Commerce.Contracts/Integration`)** — frozen,
   framework-light DTOs. No EF, no MediatR, no FluentValidation. These
   are the only types a future host adapter must depend on.
2. **Application abstractions
   (`Commerce.Application/Integration/Abstractions`)** — six interfaces
   that split into "host-owned seams" (identity accessor, tenant
   resolver, provisioning publisher, integration adapter) and
   "Commerce-owned read-models" (access recommendation, entitlement
   snapshot).
3. **Infrastructure defaults
   (`Commerce.Infrastructure/Integration`)** — local/no-op
   implementations for the host-owned seams plus the read-model
   implementations that join Commerce-owned tables. All registered in
   the `// Host Integration Contracts (COM-B08)` block of
   `DependencyInjection.cs`.

The read-only HTTP surface
(`Commerce.Api/Controllers/Integration/HostIntegrationController`) is a
thin pass-through over the application services and depends only on
contracts + abstractions.

## 4. Files Created/Changed

**Created (15 source files + 5 test files + 2 docs)**:

* `services/Commerce/src/Commerce.Contracts/Integration/`
  * `HostTenantRef.cs`
  * `HostIdentityContext.cs`
  * `AccessRecommendation.cs`
  * `AccessRecommendationResponse.cs`
  * `ProvisioningHookRequest.cs`
  * `EntitlementSnapshotDtos.cs`
* `services/Commerce/src/Commerce.Application/Integration/Abstractions/`
  * `IHostIdentityContextAccessor.cs`
  * `IHostTenantResolver.cs`
  * `IProvisioningHookPublisher.cs`
  * `IHostIntegrationAdapter.cs`
  * `ICommerceAccessRecommendationService.cs`
  * `ICommerceEntitlementSnapshotService.cs`
* `services/Commerce/src/Commerce.Infrastructure/Integration/HostAdapters/`
  * `LocalHostIdentityContextAccessor.cs`
  * `NoopHostTenantResolver.cs`
  * `NoopProvisioningHookPublisher.cs`
  * `LocalHostIntegrationAdapter.cs`
* `services/Commerce/src/Commerce.Infrastructure/Integration/Services/`
  * `CommerceAccessRecommendationService.cs`
  * `CommerceEntitlementSnapshotService.cs`
* `services/Commerce/src/Commerce.Api/Controllers/Integration/HostIntegrationController.cs`
* `services/Commerce/tests/Commerce.Tests/Integration/`
  * `IntegrationTestHost.cs`
  * `EntitlementSnapshotServiceTests.cs`
  * `AccessRecommendationServiceTests.cs`
  * `HostIntegrationEndpointsTests.cs`
  * `HostNeutralityTests.cs`
* `analysis/COM-B08-host-integration-contract.md`
* `analysis/COM-B08-report.md` _(this file)_

**Modified (1 file)**:

* `services/Commerce/src/Commerce.Infrastructure/DependencyInjection.cs`
  — added the `// Host Integration Contracts (COM-B08)` block
  registering six implementations (singleton accessor + publisher,
  scoped resolver + adapter + the two services).

No other source file is touched. No domain entities were added or
modified.

## 5. Database / Migration Changes

**None.** All data is sourced from tables already created by COM-B03 /
COM-B04 / COM-B06:

* `BillingAccount` (id, account number, display name, status)
* `BillingAccountExternalRef` (host platform key, external tenant id,
  is primary, created at) — read by `NoopHostTenantResolver`
* `AccountStanding` (status, reason, grace period end)
* `Subscription`, `SubscriptionItem`, `Plan`, `Product`,
  `PlanFeature`, `Feature`

The EF idempotent migration script regenerates **unchanged** at 1 675
lines — see §12. The single migration is
`20260423230303_InitialCreate`; B08 adds none.

## 6. API Endpoints Added

Controller: `Commerce.Api/Controllers/Integration/HostIntegrationController`
at route `/api/commerce/integration`.

| Verb | Path | 200 body | 404 body |
| --- | --- | --- | --- |
| GET | `/contracts/health` | `IntegrationContractsHealthResponse` (status, accessor type name, resolver type name, publisher name, generatedAtUtc) | n/a |
| GET | `/billing-accounts/{id:guid}/entitlement-snapshot?includeAllStatuses=false` | `CommerceEntitlementSnapshot` | `{ resource: "billing-account", id }` |
| GET | `/host-tenants/{hostPlatformKey}/{externalTenantId}/entitlement-snapshot?includeAllStatuses=false` | `CommerceEntitlementSnapshot` | `{ hostPlatformKey, externalTenantId }` |
| GET | `/billing-accounts/{id:guid}/access-recommendation` | `AccessRecommendationResponse` | `{ resource: "billing-account", id }` |

All endpoints are read-only. None requires authentication in B08; a
future block will add a host-issued token policy.

## 7. Entitlement Snapshot Behavior

`CommerceEntitlementSnapshotService.GetByBillingAccountAsync` builds
the snapshot by joining: billing account → account standing → host
tenant ref (via `IHostTenantResolver`) → access recommendation (via
`ICommerceAccessRecommendationService`) → subscriptions (filtered to
`Active`/`Trialing` unless `includeAllSubscriptionStatuses=true`) →
subscription items joined to plans → distinct plans →
product-key resolution by id → plan-feature limits joined to features.
`GetByHostTenantAsync` resolves `(hostPlatformKey, externalTenantId)`
to a billing account id then delegates.

The snapshot:

* Excludes payment provider raw payloads, Stripe/Paddle event JSON, and
  any host identity fields by design.
* Stamps `GeneratedAtUtc` from `IClock.UtcNow` so it is reproducible
  under fakes.
* Reports `AccountStandingStatus = "Good"` when no standing record
  exists (the recommendation engine still treats absence as `Unknown`
  — see §8).

Returns `null` (translated to 404) when the billing account does not
exist or the host tenant mapping is not present.

## 8. Access Recommendation Behavior

`CommerceAccessRecommendationService` is split into a pure static
mapping function `ComputeRecommendation(billingAccountStatus,
standingStatus, standingReason, hasActiveOrTrialing)` and a thin async
wrapper that performs three EF reads (billing account status, account
standing row, any active/trialing subscription) plus a tenant-ref
lookup. The full mapping table is documented in
`analysis/COM-B08-host-integration-contract.md` §8. Highlights:

* Billing-account `Closed`/`Suspended` short-circuit to `Block`.
* Missing account-standing row → `Unknown` (Commerce has no opinion).
* `Closed`/`Suspended` standing → `Block`; `PastDue` → `ReadOnly`;
  `GracePeriod` → `GraceLimited`; `Trialing` → `Allow`; `Cancelled` →
  `ReadOnly`.
* `Good` with active/trialing subscription → `Allow`; `Good` without →
  `ReadOnly`.
* Returns `null` when the billing account does not exist.

The recommendation enum is small and stable
(`Unknown=0, Allow=1, ReadOnly=2, GraceLimited=3, Block=4`); a host
that encounters an unknown numeric value is documented to treat it as
`Unknown`.

## 9. Host Adapter Interfaces

| Interface | Lifetime | Default |
| --- | --- | --- |
| `IHostIdentityContextAccessor` | Singleton | `LocalHostIdentityContextAccessor` returning `HostIdentityContext.Anonymous("local")` (`IsAuthenticated=false`). |
| `IHostTenantResolver` | Scoped | `NoopHostTenantResolver` (reads `BillingAccountExternalRef` directly, normalises `HostPlatformKey`, prefers `IsPrimary` then earliest `CreatedAtUtc`). |
| `IProvisioningHookPublisher` | Singleton | `NoopProvisioningHookPublisher` (logs the requested action; returns `Accepted=true, Delivered=false, Reason="noop publisher: no host adapter registered (COM-B08).", Name="noop"`). |
| `IHostIntegrationAdapter` | Scoped | `LocalHostIntegrationAdapter` composing the three above with `HostPlatformKey="local"`. |

The two Commerce-owned services
(`ICommerceAccessRecommendationService`,
`ICommerceEntitlementSnapshotService`) are also scoped and are not
intended to be replaced by host adapters.

## 10. Documentation Created

* `analysis/COM-B08-host-integration-contract.md` — 11 sections:
  Purpose & Scope; Boundaries & Non-Goals (HARD STOP); Roles &
  Terminology; Contract DTOs; Application Interfaces; Default (No-Op)
  Implementations; HTTP Endpoints; Access Recommendation Rules;
  Entitlement Snapshot Composition; Future Adapter Implementation
  Guide; Versioning & Stability.
* `analysis/COM-B08-report.md` — this report (16 sections).

## 11. Tests Added

23 new tests across 5 files in
`services/Commerce/tests/Commerce.Tests/Integration/`:

| File | Tests | Coverage |
| --- | --- | --- |
| `AccessRecommendationServiceTests.cs` | 5 (incl. `[Theory]` over the §8 mapping table) | Pure mapping rules; missing standing → Unknown; billing-account overrides; end-to-end against seeded `IntegrationTestHost`; null when account missing. |
| `EntitlementSnapshotServiceTests.cs` | 7 | Snapshot by billing account; by host tenant; product/plan/feature/limit joins; default exclusion of cancelled subscriptions; null for missing billing account; null for missing tenant mapping; assertion that no payment provider event payload leaks into the snapshot. |
| `HostIntegrationEndpointsTests.cs` | 5 | `/contracts/health` returns adapter type names; 404 for unknown billing account snapshot, unknown host tenant snapshot, and unknown access-recommendation; round-trip snapshot for a seeded billing account. |
| `HostNeutralityTests.cs` | 6 | Reflection-based assertions that contract/infrastructure assemblies contain no host-specific identifiers; contract assembly only references base class libraries; default adapters are the local/no-op implementations; no YARP/ReverseProxy/Identity host packages are referenced; local adapter yields anonymous unauthenticated identity; no-op provisioning publisher accepts without delivering. |
| `IntegrationTestHost.cs` | (helper) | Shared test host that boots the Api in-memory with a clock, seeds a billing account + standing + subscription + plan + product + feature + external ref, and exposes typed `HttpClient` access. |

Validation result: **235 / 235 tests passed** in 6.49s on the
`bf42935bfcef` runner. No warnings, no flaky tests.

## 12. Validation Results

Performed on 24 Apr 2026 (UTC):

* `dotnet build` for `Commerce.Domain`, `Commerce.Contracts`,
  `Commerce.Application`, `Commerce.Infrastructure`, `Commerce.Api`,
  `Commerce.Tests` — all succeed in `Release` configuration with
  `/p:UseSharedCompilation=false`. Per-project times (no warnings):
  Domain ~2 s, Contracts ~3 s, Application ~6 s, Infrastructure ~12 s,
  Api ~11 s, Tests ~150 s.
* `dotnet test tests/Commerce.Tests/Commerce.Tests.csproj -c Release
  --no-build`:
  * **Total tests: 235**
  * **Passed: 235**
  * **Failed: 0, Skipped: 0**
  * **Total time: 6.4946 s**
* `dotnet ef migrations script --idempotent --project
  src/Commerce.Infrastructure --startup-project src/Commerce.Api --no-build`:
  succeeded, **1 675 lines**, single migration
  `20260423230303_InitialCreate`. No new migrations introduced by B08.
* In-process integration host (`IntegrationTestHost`) boots the Api,
  exercises every `/api/commerce/integration/*` endpoint, and tears
  down cleanly with no port conflicts or scope leaks.

## 13. Known Gaps or Deferred Items

* **No authentication** on `/api/commerce/integration/*`. Deferred to a
  future host-integration block that will introduce host-issued tokens.
* **No outbound provisioning** — the `IProvisioningHookPublisher`
  default is a no-op logger. A real adapter (LegalSynq or generic) is
  the next phase's responsibility.
* **No live host directory sync.** `IHostTenantResolver` reads only the
  `BillingAccountExternalRef` table seeded in COM-B03. A future block
  may add a sync background job; B08 must not.
* **`HostIdentityContext` is unused inside Commerce in B08** beyond the
  no-op accessor and a unit test. It exists so that future adapters can
  publish identity into the request scope without contract churn.
* **No JSON examples shipped** for the entitlement snapshot. The
  contract is documented; examples will accompany the host-integration
  block that consumes them.
* **The integration controller is unauthorised**, so it is intentionally
  not exposed publicly in any deployment yet.

## 14. Confirmation of Strict Exclusions

The block was implemented and verified against the strict exclusions
list. None of the following are present in any file modified or created
by this block:

* No reference to LegalSynq Tenant or Identity services (verified by
  `HostNeutralityTests.Contract_and_infrastructure_assemblies_have_no_host_specific_*`).
* No `Microsoft.AspNetCore.Authentication.*`, `Yarp.ReverseProxy`,
  `Microsoft.IdentityModel.*`, `System.IdentityModel.*` package
  references in `Commerce.Contracts`, `Commerce.Application`, or
  `Commerce.Infrastructure` (verified by
  `HostNeutralityTests.No_yarp_reverseproxy_or_identity_host_packages_are_referenced_*`).
* No JWT validation, OAuth/OIDC client, or auth middleware additions.
* No authorisation policies or `[Authorize]` attributes on the
  integration controller.
* No call to any host-side provisioning, identity, or tenant API.
* No EF migrations introduced.
* No enforcement of `AccessRecommendation` inside Commerce — every
  caller treats it as advisory.
* No changes to existing controllers, services, domain entities, or
  background jobs outside the new files in §4 (and the single DI
  registration block).

## 15. Hard Stop Confirmation

COM-B08 is a **HARD STOP** block. The contract surface is intentionally
the only deliverable — no host adapter is implemented in this block,
and no follow-on work was begun. The next step (a real host adapter
for one or more host platforms) is owned by a separate, later block.

The Commerce service continues to build, run, and serve all pre-B08
endpoints exactly as before. The new endpoints under
`/api/commerce/integration/*` are additive and read-only. Nothing in
this block depends on, or enables, any specific host platform.

## 16. Recommended Next Phase

Suggested follow-on (each is its own block, **not** in scope here):

1. **Host adapter implementation** for the chosen first host platform
   (e.g. LegalSynq) — implementing `IHostIdentityContextAccessor`,
   `IHostTenantResolver`, `IProvisioningHookPublisher`, and
   `IHostIntegrationAdapter` in a separate `Commerce.Hosts.<Name>`
   library.
2. **Authorisation policy** on `/api/commerce/integration/*` consuming
   the host-issued token model.
3. **Outbound provisioning client** (HTTP) replacing
   `NoopProvisioningHookPublisher`, with retry/backoff/circuit-breaker.
4. **Snapshot caching** if host-side polling volume warrants it.
5. **Webhook fan-out** so Commerce can push entitlement deltas to the
   host instead of being polled.
6. **JSON contract examples + OpenAPI publication** for the four
   integration endpoints.
