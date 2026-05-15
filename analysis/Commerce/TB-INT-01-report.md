# TB-INT-01 — Commerce → Tenant Billing Entitlement Publisher

## 1. Summary

Adds the first cross-service integration bridge from the Commerce
service to the canonical Tenant Billing service (`services/tenant-billing/`,
`Billing.*` assemblies). Commerce can now build a
`CommerceEntitlementSnapshot` for a `BillingAccountId`, map it onto the
Tenant Billing apply contract added in TB-DATA-02, and POST it to
`POST /api/tenant-billing/entitlements/apply` over HTTP — without any
shared database, project reference, or domain merge.

The bridge is:

- **Config-gated**: `Commerce:TenantBilling:Enabled` defaults to `false`.
  When disabled the publisher short-circuits and returns `Skipped` with
  reason `"publisher-disabled"`. No HTTP call is made.
- **Explicit**: invoked by an internal-only endpoint
  `POST /api/commerce/integration/tenant-billing/billing-accounts/{id}/publish-entitlement`
  or by a service consumer holding `ITenantBillingEntitlementPublisher`.
  No background scheduler, no automatic publish on commerce mutations.
- **Reversible**: zero changes to existing Commerce billing/entitlement
  code, zero rows mutated on Commerce side regardless of outcome, and a
  single new options section. Removing the publisher is a delete-files
  operation.

This block does NOT enforce entitlements anywhere, does NOT integrate
with Identity, Control Center, or Tenant Portal, and does NOT touch any
billing tables on either side.

## 2. Codebase Analysis

Commerce already exposes the data we need:

- `Commerce.Application.Integration.Abstractions.ICommerceEntitlementSnapshotService`
  — `GetByBillingAccountAsync(Guid, bool, CancellationToken)` returns
  the read-model `CommerceEntitlementSnapshot` or `null` if the
  `BillingAccount` is unknown.
- `Commerce.Contracts.Integration.CommerceEntitlementSnapshot` —
  output-only DTO with `BillingAccountId`, `HostPlatformKey`,
  `ExternalTenantId`, `AccountStandingStatus` (string),
  `AccessRecommendation` (enum), `Subscriptions`, `Plans`, `Products`,
  `Limits`, `GeneratedAtUtc`.
- The existing `HostIntegrationController` (`api/commerce/integration`)
  is the natural home for the new publish endpoint.
- DI is centralised in `Commerce.Infrastructure.DependencyInjection`,
  with the existing `AddHttpClient<StripePaymentProvider>()` precedent
  for typed HTTP clients and `services.Configure<...Options>()` for
  bound config sections.

Tests use `CommerceWebApplicationFactory` which pins
`Database:ConnectionString=""` to force the in-memory DB fallback.

No existing Commerce code was modified beyond DI registration and one
appsettings section. (Filled in §11/§12 after implementation.)

## 3. Commerce Entitlement Source Analysis

Filled below — see §9 Mapping Behavior.

## 4. Tenant Billing Contract Analysis

The TB-DATA-02 apply endpoint is:

- `POST /api/tenant-billing/entitlements/apply`
- Headers: `X-Tenant-Id` (must parse as a non-empty `Guid`),
  `X-Internal-Token` (shared internal secret).
- Body: `ApplyEntitlementSnapshotRequestDto`
  (`services/tenant-billing/src/Billing.Api/Contracts/TenantBillingEntitlementDtos.cs`).
- Status enum: `TenantBillingEntitlementStatus` with values
  `Unknown`, `Enabled`, `Disabled`, `Suspended`, `Expired`.
- Recommendation enum: `TenantBillingAccessRecommendation` with values
  `Unknown`, `Allow`, `ReadOnly`, `GraceLimited`, `Block`.
- Responses:
  - `200 OK` — `TenantBillingEntitlementSnapshotResponse`
  - `400 Bad Request` — validation / invalid `X-Tenant-Id`
  - `401 Unauthorized` — internal token missing / wrong
  - `404 Not Found` — no profile for `(X-Tenant-Id, BillingAccountId)`
  - `409 Conflict` — profile mismatch / closed profile

The recommendation enum is identical to Commerce's
`AccessRecommendation` enum (same five names) so Commerce's value
passes through verbatim as a string.

## 5. TenantId Resolution Decision

The Tenant Billing apply endpoint requires `X-Tenant-Id` to parse as a
non-empty `Guid`. Commerce models tenant identity in
`BillingAccountExternalRef` rows whose `ExternalTenantId` is a free-form
string (the host platform decides the format).

**Resolution rule (implemented in
`TenantBillingEntitlementPublisher.TryResolveTenantId`):**

1. Read `snapshot.ExternalTenantId` (which the snapshot service populates
   from the primary `BillingAccountExternalRef` row).
2. If null / whitespace → return `Skipped("no-external-tenant-id")`.
3. If non-empty but `Guid.TryParse` fails or yields `Guid.Empty` →
   return `Skipped("external-tenant-id-not-a-guid")`.
4. Otherwise use the parsed GUID.

**What we explicitly did NOT do:**

- We do **not** call any Identity / Tenant / Control Center service.
- We do **not** invent a tenant id (e.g. derive a deterministic GUID
  from the BillingAccountId).
- We do **not** fall back to the BillingAccountId itself as the tenant
  id — those are intentionally separate identity spaces.

This is deliberately conservative: hosts whose `ExternalTenantId` is not
already a GUID will see `Skipped` (a non-error outcome) until they
either (a) supply a GUID-shaped external ref, or (b) a future block adds
a real Identity-backed tenant resolver in Commerce.

## 6. Publisher Architecture Implemented

New components, all additive — no existing Commerce code was modified
beyond DI registration and one `appsettings.json` section:

| Layer            | Type                                                 | File |
|------------------|------------------------------------------------------|------|
| Application      | `ITenantBillingEntitlementPublisher`, `PublishEntitlementOutcome`, `PublishEntitlementResult` | `services/Commerce/src/Commerce.Application/Integration/Abstractions/ITenantBillingEntitlementPublisher.cs` |
| Infrastructure   | `TenantBillingClientOptions`                         | `…/Commerce.Infrastructure/Integration/TenantBilling/TenantBillingClientOptions.cs` |
| Infrastructure   | `TenantBillingApplyRequestDto` (internal wire DTO)   | `…/TenantBilling/TenantBillingApplyRequestDto.cs` |
| Infrastructure   | `TenantBillingEntitlementMapper` (pure static)       | `…/TenantBilling/TenantBillingEntitlementMapper.cs` |
| Infrastructure   | `TenantBillingEntitlementPublisher` (typed HttpClient) | `…/TenantBilling/TenantBillingEntitlementPublisher.cs` |
| Api              | `TenantBillingPublisherController`                   | `services/Commerce/src/Commerce.Api/Controllers/Integration/TenantBillingPublisherController.cs` |

DI wiring (in `Commerce.Infrastructure.DependencyInjection`):

```csharp
services.Configure<TenantBillingClientOptions>(
    configuration.GetSection(TenantBillingClientOptions.SectionName));
services.AddHttpClient<ITenantBillingEntitlementPublisher,
                      TenantBillingEntitlementPublisher>();
```

`AddHttpClient<TInterface, TImpl>` is the same typed-HttpClient pattern
already used for `StripePaymentProvider`, so resilience and lifecycle
concerns can be added later via
`AddPolicyHandler` without changing call sites.

## 7. API Endpoint Added

```
POST /api/commerce/integration/tenant-billing/billing-accounts/{billingAccountId:guid}/publish-entitlement
```

Mounted under the existing
`api/commerce/integration` prefix alongside `HostIntegrationController`.
There is no tenant-facing route exposing this; it is intended to be
called by an internal admin/operator surface.

Behaviour:

- `404 Not Found` — when `billingAccountId` does not match any
  `BillingAccount` in Commerce.
- `400 Bad Request` — when `billingAccountId` is `Guid.Empty`.
- `200 OK` — always when the BillingAccount exists, with body
  `PublishEntitlementResultResponse`:

```json
{
  "outcome": "published" | "skipped" | "failed",
  "billingAccountId": "<guid>",
  "tenantId": "<guid|null>",
  "httpStatus": 200,
  "reason": "published",
  "responseBodySummary": null
}
```

The endpoint never throws to the client and never mutates Commerce
state — even on 5xx from Tenant Billing, the response is `200 OK` with
`outcome: "failed"` and a diagnostic `reason`.

## 8. Configuration Added

New section in `services/Commerce/src/Commerce.Api/appsettings.json`:

```json
"Commerce:TenantBilling": {
  "_comment": "TB-INT-01 — entitlement publisher to canonical Tenant Billing service. Disabled by default; supply env vars Commerce__TenantBilling__Enabled / __BaseUrl / __InternalToken to activate. Never commit real tokens here.",
  "Enabled": false,
  "BaseUrl": "",
  "InternalToken": "",
  "TimeoutSeconds": 10
}
```

Environment variable overrides (standard ASP.NET Core mapping):

- `Commerce__TenantBilling__Enabled=true`
- `Commerce__TenantBilling__BaseUrl=http://localhost:5001`
- `Commerce__TenantBilling__InternalToken=<value-from-secret-store>`
- `Commerce__TenantBilling__TimeoutSeconds=10`

No real secrets are checked in. The committed defaults make the
publisher inert (Enabled=false), so a deploy that forgets to set the
env vars will fall through to `Skipped("publisher-disabled")` and never
attempt a network call.

## 9. Mapping Behavior

Mapping is implemented in
`TenantBillingEntitlementMapper` (pure static class) so it is unit-test
friendly. `RawSnapshotJson` always carries the full Commerce snapshot
serialised as camelCase JSON for downstream trace/debugging.

**EntitlementStatus** is derived from
`CommerceEntitlementSnapshot.AccountStandingStatus`:

| Commerce standing | Tenant Billing entitlementStatus |
|-------------------|----------------------------------|
| `Good`            | `Enabled`                        |
| `Trialing`        | `Enabled`                        |
| `GracePeriod`     | `Enabled`                        |
| `PastDue`         | `Enabled` *(see note)*           |
| `Suspended`       | `Suspended`                      |
| `Cancelled`       | `Disabled`                       |
| `Closed`          | `Disabled`                       |
| empty / unknown   | `Unknown`                        |

*Note on PastDue:* per the TB-INT-01 spec's "Preferred" guidance,
PastDue stays `Enabled` and degradation is signalled via the
`accessRecommendation` field. This matches Commerce's own access
recommendation policy (post-grace `PastDue` → `ReadOnly`), so the
receiver can implement either soft-gate or hard-block based on policy.

**AccessRecommendation** is taken verbatim from Commerce's
`AccessRecommendation` enum. The two enums share the same five names
(`Unknown`, `Allow`, `ReadOnly`, `GraceLimited`, `Block`) and Tenant
Billing's apply endpoint validates `[Required, MaxLength(16)]` strings
matching exactly those values, so the publisher passes Commerce's
authoritative recommendation through unchanged. We deliberately do
**not** rederive the recommendation from `AccountStandingStatus`;
Commerce already owns that policy via
`ICommerceAccessRecommendationService`.

**Other mapped fields:**

- `sourceSystem`     → constant `"commerce"`
- `sourceSnapshotId` → `snapshot.GeneratedAtUtc.ToString("O")` (ISO-8601)
- `sourceSubscriptionId` → first subscription in the snapshot (or null)
- `sourcePlanKey`    → first plan key on that subscription's first item
- `sourceProductKey` → product key resolved from `Plans` for that plan
- `reason`           → `snapshot.AccountStandingReason`, truncated to 1000 chars
- `effectiveFromUtc` → `snapshot.GeneratedAtUtc`
- `effectiveToUtc`   → `snapshot.AccountStandingGracePeriodEndsAtUtc` if any
- `rawSnapshotJson`  → full snapshot JSON

## 10. Error Handling / Resilience Behavior

| Condition                                          | Outcome      | Reason string                                            |
|----------------------------------------------------|--------------|----------------------------------------------------------|
| `Enabled=false`                                    | `Skipped`    | `publisher-disabled`                                     |
| BillingAccount unknown to Commerce snapshot service| `Skipped`    | `billing-account-not-found`                              |
| `ExternalTenantId` null/blank                      | `Skipped`    | `no-external-tenant-id`                                  |
| `ExternalTenantId` non-GUID                        | `Skipped`    | `external-tenant-id-not-a-guid`                          |
| `BaseUrl` blank but `Enabled=true`                 | `Failed`     | `base-url-not-configured`                                |
| `InternalToken` blank but `Enabled=true`           | `Failed`     | `internal-token-not-configured`                          |
| `PublishSnapshotAsync` called with `Guid.Empty` tenant | `Skipped` | `tenant-id-empty`                                        |
| Tenant Billing 200/201/204                         | `Published`  | `published`                                              |
| Tenant Billing 401                                 | `Failed`     | `tenant-billing-401-internal-token-rejected`             |
| Tenant Billing 404                                 | `Failed`     | `tenant-billing-404-no-profile-for-billing-account`      |
| Tenant Billing 409                                 | `Failed`     | `tenant-billing-409-profile-mismatch-or-closed`          |
| Tenant Billing 400                                 | `Failed`     | `tenant-billing-400-bad-request`                         |
| Tenant Billing other 4xx/5xx                       | `Failed`     | `tenant-billing-{code}`                                  |
| `HttpRequestException` / connection refused        | `Failed`     | `tenant-billing-unreachable` *(stable; detail in `responseBodySummary` and logs only)* |
| Per-request timeout                                | `Failed`     | `tenant-billing-timeout`                                 |
| Any other unexpected exception (URI parse, serialiser, etc.) | `Failed` | `tenant-billing-publish-exception` *(catch-all guard)* |

Resilience choices, intentionally minimal:

- **Per-request timeout** via `CancellationTokenSource.CancelAfter` so a
  hung Tenant Billing endpoint does not block the caller indefinitely.
- **No retries / no Polly policies** in this block — Commerce's existing
  resilience pattern (`AddPolicyHandler`) can be layered on the typed
  HttpClient registration in a follow-up without code changes here.
- **No automatic re-publish on Commerce mutations** — publisher is
  invoked only via the explicit endpoint or a service consumer.

Commerce state is **never** mutated by the publisher under any outcome.

## 11. Tests Added

All tests live under
`services/Commerce/tests/Commerce.Tests/Integration/TenantBilling/`.

**Mapper (`TenantBillingEntitlementMapperTests`) — 16 cases:**

- 9-row `[Theory]` covering the full account-standing → entitlement
  matrix (`Good`, `Trialing`, `GracePeriod`, `PastDue`, `Suspended`,
  `Cancelled`, `Closed`, empty, weird-new).
- Null standing → `Unknown`.
- 5-row `[Theory]` covering every `AccessRecommendation` enum value.
- Full-snapshot mapping populates every field including
  `rawSnapshotJson`, `sourceSubscriptionId`, `sourcePlanKey`,
  `sourceProductKey`.
- Empty subscriptions/plans yield null source-* fields.
- Long reason truncates to 1000 characters.

**Publisher (`TenantBillingEntitlementPublisherTests`) — 14 cases**
using a `FakeHttpMessageHandler` and a `FakeSnapshots` adapter:

- Disabled publisher returns `Skipped` and makes no HTTP call.
- Unknown billing account returns `Skipped/billing-account-not-found`.
- Missing `ExternalTenantId` returns `Skipped/no-external-tenant-id`.
- Non-GUID `ExternalTenantId` returns `Skipped/external-tenant-id-not-a-guid`.
- Successful POST returns `Published`, asserts URL, method, both
  headers (`X-Tenant-Id`, `X-Internal-Token`), and that body contains
  `"entitlementStatus":"Enabled"` plus the BillingAccountId.
- 401 → `Failed/tenant-billing-401-internal-token-rejected`.
- 404 → `Failed/tenant-billing-404-no-profile-for-billing-account`.
- 409 → `Failed/tenant-billing-409-profile-mismatch-or-closed`.
- `HttpRequestException` → `Failed/tenant-billing-unreachable` (stable
  reason; exception detail surfaced in `ResponseBodySummary` only;
  does not throw).
- 500 Internal Server Error → `Failed/tenant-billing-500`.
- 503 Service Unavailable → `Failed/tenant-billing-503`.
- Empty BaseUrl → `Failed/base-url-not-configured`.
- Empty InternalToken → `Failed/internal-token-not-configured`.
- `PublishSnapshotAsync(_, Guid.Empty)` → `Skipped/tenant-id-empty`.
- `PublishSnapshotAsync` honours disabled flag → `Skipped/publisher-disabled`.

**Endpoint (`TenantBillingPublisherEndpointTests`) — 3 cases** via
`CommerceWebApplicationFactory`:

- Unknown billing account → `404 Not Found`.
- `Guid.Empty` billing account → `400 Bad Request`.
- Existing billing account with publisher disabled (default) →
  `200 OK` with `outcome: "skipped"`, `reason: "publisher-disabled"`,
  and the correct `billingAccountId` echoed back.

Tenant resolution is covered both in the publisher tests above (cases
3, 4, 12) and the endpoint test (case 3 — the disabled config means we
verify the Commerce side never reaches the resolver, satisfying the
"does not call Identity/Tenant service" requirement; the unit tests
exercise resolution directly with a fake snapshot service).

## 12. Validation Results

```
dotnet build services/Commerce/Commerce.sln -c Debug
→ Build succeeded. 0 Errors, 3 Warnings (pre-existing OpenTelemetry NU1902 + 1 CS1998 in EntitlementSnapshotServiceTests; not introduced by this block)
```

```
dotnet test services/Commerce/tests/Commerce.Tests/Commerce.Tests.csproj
  --filter "FullyQualifiedName~Commerce.Tests.Integration.TenantBilling"
→ Passed: 36 / 36, Failed: 0  (all new TB-INT-01 cases, including
   2 added 5xx cases and the deterministic-reason assertion for
   transport errors per the architect review feedback)
```

Full Commerce regression run (split by class group due to 120s shell
timeout):

| Filter (FullyQualifiedName ~ …)                                     | Passed | Failed |
|---------------------------------------------------------------------|--------|--------|
| `Commerce.Tests.Integration ∪ Billing ∪ Catalog`                    | 120    | 0      |
| `AccountStanding ∪ Invoicing ∪ Payments`                            | 105    | 0      |
| `Admin ∪ HealthEndpoints ∪ SystemEndpoints ∪ DbContext`             | 18     | 0      |
| `Commerce.Tests.Subscriptions`                                      | 49     | **1**  |

**Aggregate: 292 / 293 passed; 1 pre-existing failure unrelated to TB-INT-01.**

The failing test is
`Commerce.Tests.Subscriptions.SubscriptionServiceTests.ChangePlan_closes_old_item_and_creates_new`,
which constructs a `ChangeSubscriptionPlanRequest.EffectiveAtUtc` from
its injected `Clock` while the validator
(`Commerce.Application.Subscriptions.Validators`) compares against real
wall-clock `DateTime.UtcNow` and rejects values "more than 1 day in the
past". This is a time-sensitive test that fails on this date regardless
of the TB-INT-01 changes — TB-INT-01 touches no Subscriptions code, no
validators, and no test fixtures. Verified the test exists at
`tests/Commerce.Tests/Subscriptions/SubscriptionServiceTests.cs:222-230`
and uses `h.Clock.UtcNow.AddDays(5)` paths that pre-date this block.

Tenant Billing was **not** modified, so the Tenant Billing test suite
was not re-run: the publisher targets the existing TB-DATA-02 contract
unchanged.

## 13. Risks / Deferred Items

- **No Polly retry / circuit-breaker** wired on the typed HttpClient.
  Adding `AddPolicyHandler(...)` to the existing
  `services.AddHttpClient<ITenantBillingEntitlementPublisher, …>()`
  registration is the natural next step; deferred to keep this block
  small and reversible.
- **TenantId resolution is GUID-only.** Hosts that store
  `ExternalTenantId` as a non-GUID string (e.g. slug) will see
  `Skipped/external-tenant-id-not-a-guid`. A future block can add a
  proper `ITenantIdResolver` abstraction (Identity-backed) to map
  arbitrary external strings to GUIDs.
- **No background scheduler.** Publishing is triggered only by the
  explicit endpoint or a service consumer holding
  `ITenantBillingEntitlementPublisher`. Subscription/payment lifecycle
  events do not auto-publish.
- **No publish-receipt persistence on the Commerce side.** The endpoint
  returns the result inline; if a caller needs an audit trail it must
  log the response. Tenant Billing already persists the snapshot row
  it accepted, so the system of record for the bridged state lives in
  Tenant Billing.
- **No idempotency token** beyond `sourceSnapshotId` (the Commerce
  `GeneratedAtUtc`). Two parallel publishes for the same account will
  both apply (the receiver upserts by `BillingAccountId` per profile);
  this matches the TB-DATA-02 contract.

## 14. Confirmation of Strict Exclusions

None of the following are touched by this block:

- LegalSynq Identity integration — **not added**.
- Control Center UI — **not added**.
- Tenant Portal UI — **not added**.
- Entitlement enforcement in Tenant Billing CRUD — **not added** (the
  TB-DATA-02 enablement resolver remains advisory).
- Background scheduler — **not added**.
- Shared database between Commerce and Tenant Billing — **not added**;
  zero project references between the two services.
- Direct table access between services — **not added**; communication
  is HTTP only.
- Invoice/payment table merge — **not done**.
- Route namespace migration — **not done**; existing routes unchanged.
- Payment provider changes — **none**.
- Notification integration — **not added**.
- Documents integration — **not added**.
- Deletion of standalone compatibility — **none**; both services still
  build, boot, and serve their existing routes independently.

## 15. Recommended Next Block

Either:

1. **TB-INT-02 — Resilience & observability around the publisher.**
   Add a Polly retry + circuit-breaker policy to the typed HttpClient,
   structured logging fields (`tb.publish.outcome`, `tb.publish.reason`,
   `tb.publish.tenant_id`), and metrics (counter by outcome). Optional:
   a small admin "preview" endpoint that returns the mapped payload
   without sending it.
2. **TB-INT-03 — Tenant id resolver.** Introduce a Commerce-side
   `ITenantIdResolver` so non-GUID `ExternalTenantId` strings can be
   mapped to GUIDs (initially via a `BillingAccountExternalRef`-derived
   table, later via Identity).
3. **TB-INT-04 — Caller integration.** Wire the publisher into the
   handful of Commerce code paths where we want automatic re-publish
   (subscription transitions, account-standing changes), still gated by
   `Commerce:TenantBilling:Enabled` and using the existing endpoint or
   service interface.

(2) is the prerequisite for safely turning the publisher on against
real host platforms whose tenant ids are not already GUIDs.
