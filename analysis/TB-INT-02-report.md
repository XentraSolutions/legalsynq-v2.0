# TB-INT-02 — Publisher Resilience & Observability

> Status: **DONE**
> Scope: Harden the Commerce → Tenant Billing entitlement publisher
> built in TB-INT-01. No business-rule changes; no Tenant Billing
> changes; no enforcement; no Identity/UI/Notifications/Documents work.

## 1. Summary

This block adds operational hardening to the existing
`TenantBillingEntitlementPublisher`:

- bounded retry on transient transport / 408 / 429 / 5xx failures
- a tiny in-process circuit breaker (off by default)
- structured logging on every code path with a fixed field set
- counters via `System.Diagnostics.Metrics.Meter` (no new dependency;
  picked up by the existing OpenTelemetry runtime in `Commerce.Api`)
- a non-mutating publisher diagnostics endpoint
- a non-mutating publisher payload-preview endpoint
- tests covering retry, circuit breaker, preview, diagnostics,
  configuration clamping, and the new API surface

It does not change entitlement mapping, the apply contract, or any
Commerce write-paths. Disabled by default: `Commerce:TenantBilling:Enabled=false`.

## 2. Codebase Analysis

Existing surface (TB-INT-01):

- Application contract: `ITenantBillingEntitlementPublisher`
- Infrastructure: `TenantBillingClientOptions`,
  `TenantBillingApplyRequestDto`, `TenantBillingEntitlementMapper`,
  `TenantBillingEntitlementPublisher`
- API: `TenantBillingPublisherController` →
  `POST /api/commerce/integration/tenant-billing/billing-accounts/{id}/publish-entitlement`
- DI: `services.AddHttpClient<ITenantBillingEntitlementPublisher, TenantBillingEntitlementPublisher>()`
- Config section: `Commerce:TenantBilling`
- Existing packages we can reuse:
  - `Polly` (already referenced in `Commerce.Infrastructure.csproj`)
  - `Serilog.*` (structured logging in `Commerce.Api`)
  - `OpenTelemetry.*` (already wired in `Commerce.Api`); will pick up
    any `System.Diagnostics.Metrics.Meter` we register.

Choice: implement retry / circuit breaker as small in-process
components rather than introducing a Polly-pipeline DI rewrite.
Rationale: the policy is short, deterministic, and fully testable
without a clock provider package; the broader Commerce HTTP stack
already configures Polly elsewhere via `Resilience:Http`, but this
publisher needs publisher-specific knobs that don't belong on the
shared HTTP resilience policy.

## 3. Publisher Changes Implemented

`ITenantBillingEntitlementPublisher` (Application layer) gains:

- `PreviewForBillingAccountAsync(Guid, CancellationToken) → PreviewEntitlementResult?`
- `GetDiagnosticsAsync(CancellationToken) → TenantBillingDiagnostics`
- new public records: `PreviewEntitlementResult`,
  `TenantBillingPreviewPayload`, `TenantBillingDiagnostics`
- `PublishEntitlementResult` gains an `Attempts` field (default 1, 0
  for skipped/short-circuit).

`TenantBillingEntitlementPublisher` (Infrastructure):

- All option reads now flow through `Options.Normalised()` so retry /
  CB knobs are clamped per-call.
- New constructor dependencies: `ITenantBillingPublisherCircuitBreaker`,
  `TenantBillingPublisherMetrics`. Logger and HttpClient unchanged.
- `PublishSnapshotInternalAsync` → `SendApplyWithRetryAsync` →
  `SendOneAsync`. The retry loop:
  1. Asks the circuit breaker via `TryEnter()`. If false → return
     `Failed/tenant-billing-circuit-open` (no HTTP, no metrics-attempt
     recorded but the failed-outcome counter is still incremented).
  2. For each attempt 1..(RetryAttempts+1), logs `attempt N/total`
     with the structured fields (`BillingAccountId`, `TenantId`,
     `SourceSubscriptionId`, `SourcePlanKey`, `SourceProductKey`),
     calls `SendOneAsync`, and records a metrics attempt.
  3. On success → breaker.RecordSuccess() and return Published with
     the attempt number.
  4. On non-retryable failure (4xx other than 408/429) → return
     immediately. We do **not** record a transient failure on the
     breaker (it is not a service-health signal), but we **do** call
     `RecordSuccess()` so that a HalfOpen probe answered with a
     deterministic 4xx closes the breaker rather than being left in
     HalfOpen forever. The downstream is reachable; only our request
     was rejected. `RecordSuccess` is a safe no-op when Closed.
  5. On retryable failure → wait `RetryDelayMilliseconds` (skipped if
     0; cancellable) and try again.
  6. After exhausting retries → breaker.RecordTransientFailure() and
     return the last transient result.
- `PreviewForBillingAccountAsync` reuses `TenantBillingEntitlementMapper`
  + `TryResolveTenantId` to build the same payload the publish path
  would send, but returns it as a `TenantBillingPreviewPayload` and
  never opens an HTTP connection. Returns `null` for unknown billing
  accounts (controller maps to 404). Skip-reason precedence is
  preserved: tenant resolution runs before the disabled / config
  checks.
- `GetDiagnosticsAsync` derives `Mode ∈ {Disabled, Misconfigured, Ready}`
  from the live (clamped) options + breaker state. Internal token is
  never reflected in the response — only the `InternalTokenConfigured`
  boolean.

`TenantBillingPublisherCircuitBreaker` (Infrastructure, singleton):

- States `Closed | Open | HalfOpen` behind a single lock.
- Public methods: `TryEnter()`, `RecordSuccess()`,
  `RecordTransientFailure()`, `State` (string).
- Test seam: an `internal` constructor accepts a `Func<DateTimeOffset>`
  clock; `InternalsVisibleTo("Commerce.Tests")` is already declared
  on `Commerce.Infrastructure.csproj` so the tests instantiate the
  type directly with a deterministic clock.

`TenantBillingPublisherMetrics` (Infrastructure, singleton):

- Owns one `Meter` named `Commerce.TenantBilling.Publisher`.
- Five counters with the names and tags documented in §8.

`TenantBillingPublisherController` (Api):

- `POST /preview-entitlement` — returns `PreviewEntitlementResult`,
  404 if BA absent, 400 if BA `Guid.Empty`.
- `GET /diagnostics` — returns `TenantBillingDiagnostics`.

DI (`Commerce.Infrastructure.DependencyInjection`):

- Adds `AddSingleton<ITenantBillingPublisherCircuitBreaker, …>` and
  `AddSingleton<TenantBillingPublisherMetrics>` ahead of the existing
  typed-client registration.

`Commerce.Api/Program.cs`:

- The OpenTelemetry `WithMetrics(...)` builder now also calls
  `.AddMeter(TenantBillingPublisherMetrics.MeterName)` so the new
  counters flow through the existing OTLP exporter when enabled.

`Commerce.Api/appsettings.json`:

- `Commerce:TenantBilling` gains `RetryAttempts`,
  `RetryDelayMilliseconds`, `CircuitBreakerEnabled`,
  `CircuitBreakerFailures`, `CircuitBreakerDurationSeconds`. All
  clamped at runtime by `TenantBillingClientOptions.Normalised`.

## 4. Configuration Changes

`Commerce:TenantBilling` gains four new fields. All bound and clamped
in `TenantBillingClientOptions.Normalised()` so a misconfigured
deployment can never produce unbounded retries or zero-duration
breakers:

```jsonc
"Commerce:TenantBilling": {
  "Enabled": false,
  "BaseUrl": "",
  "InternalToken": "",
  "TimeoutSeconds": 10,
  "RetryAttempts": 2,                 // clamped to [0, 10]
  "RetryDelayMilliseconds": 250,      // clamped to [0, 10000]
  "CircuitBreakerEnabled": false,
  "CircuitBreakerFailures": 5,        // clamped to [1, 100]
  "CircuitBreakerDurationSeconds": 30  // clamped to [1, 3600]
}
```

`Enabled` remains `false` by default and `InternalToken` remains empty
in the committed config — secrets must come from environment variables
(`Commerce__TenantBilling__InternalToken`) or a secret store.

## 5. Retry Behavior

Retry is a small loop inside `TenantBillingEntitlementPublisher.SendApplyAsync`.

- Total attempts = `RetryAttempts + 1` (default `2 + 1 = 3`).
- Delay between attempts = `RetryDelayMilliseconds` (linear; tests
  set this to `0`).
- **Retried** outcomes:
  - `HttpRequestException` (connection refused / DNS / etc.)
  - per-request timeout (`TaskCanceledException` not caused by the
    caller's `CancellationToken`)
  - HTTP `408 Request Timeout`
  - HTTP `429 Too Many Requests`
  - any HTTP `5xx`
- **Not retried** outcomes:
  - HTTP `400`, `401`, `403`, `404`, `409`
  - any other unexpected exception (caught once, surfaced as
    `tenant-billing-publish-exception` — same catch-all behaviour as
    TB-INT-01)
- If a retry succeeds → final outcome is `Published`; the attempt
  count is logged.
- If retries are exhausted → final outcome is `Failed` with the
  reason of the last attempt.
- Caller-cancellation (`ct.IsCancellationRequested`) short-circuits
  the loop immediately; we do not retry after the caller cancels.

## 6. Circuit Breaker Decision

Implemented as an in-process singleton
(`ITenantBillingPublisherCircuitBreaker` /
`TenantBillingPublisherCircuitBreaker`). State is held per process,
not per typed-client instance, so the breaker holds across
`HttpClient` reuse. It is **disabled by default**.

States: `Closed`, `Open`, `HalfOpen`.

- Closed: every call goes through. Each retry-exhausted transient
  failure increments a consecutive-failures counter. When it reaches
  `CircuitBreakerFailures`, the breaker opens until
  `now + CircuitBreakerDurationSeconds`.
- Open: `TryEnter()` returns `false`; the publisher returns
  `Failed/tenant-billing-circuit-open` without making any HTTP call.
  Skip path is treated as "no-op" — it does not record success or
  failure.
- HalfOpen (after the duration elapses): the next single call is
  allowed as a probe. Success → close + reset counter. Failure →
  reopen the breaker for another full duration.

A non-transient failure (4xx other than 408/429) does **not** trip
the breaker; those are caller bugs, not service-health signals.

## 7. Logging Behavior

`ILogger<TenantBillingEntitlementPublisher>` already injected; this
block formalises the field set and severity policy.

Shared structured fields (kept stable so dashboards can pivot):

- `BillingAccountId`, `TenantId`, `Outcome`, `Reason`, `HttpStatus`
- `SourceSystem`, `SourceSubscriptionId`, `SourcePlanKey`, `SourceProductKey`
- `AttemptNumber`, `TotalAttempts`

Severity policy:

- Information: publisher disabled, publish skipped (any reason),
  publish attempt started, publish success (incl. retry-recovered),
  retry attempt scheduled.
- Warning: tenant id unresolved, client-side Tenant Billing failure
  (400/401/403/404/409).
- Error: server / transient failure after retry exhaustion, timeout
  after retry exhaustion, circuit breaker opened, unexpected
  exception, configuration missing while `Enabled=true`.

Never logged: `InternalToken`, raw snapshot JSON, secrets.

## 8. Metrics Behavior

Implemented via `System.Diagnostics.Metrics.Meter` (no new package).
Meter name: `Commerce.TenantBilling.Publisher`.

Counters:

- `commerce.tenant_billing.publish.attempts` — every wire attempt
  (incremented even on retries).
- `commerce.tenant_billing.publish.published` — final outcome
  Published.
- `commerce.tenant_billing.publish.skipped` — final outcome Skipped.
- `commerce.tenant_billing.publish.failed` — final outcome Failed.
- `commerce.tenant_billing.publish.retry_attempts` — only the
  retry attempts (i.e. attempts 2..N).

Tags emitted: `outcome`, `reason`, `http_status` (when known).

The `Meter` is registered as a singleton; the existing OpenTelemetry
metrics provider in `Commerce.Api/Program.cs` will pick it up as soon
as the meter name is added to `MeterProviderBuilder.AddMeter(...)`.
We register it in DI so consumers (and tests) can inspect it via
`MeterListener`.

## 9. Diagnostics Endpoint

`GET /api/commerce/integration/tenant-billing/diagnostics` →
`TenantBillingDiagnostics`:

- `Enabled`, `BaseUrlConfigured`, `InternalTokenConfigured`,
  `TimeoutSeconds`, `RetryAttempts`, `RetryDelayMilliseconds`,
  `CircuitBreakerEnabled`, `CircuitBreakerFailures`,
  `CircuitBreakerDurationSeconds`, `CircuitBreakerState`,
  `TargetRoute`, `Mode` ∈ {`Disabled`, `Misconfigured`, `Ready`}.

Rules: `InternalToken` is **never** returned (only a presence flag);
no token length, no prefix, no hash. `Enabled=false` ⇒ `Mode=Disabled`
regardless of other config. `Enabled=true` and either `BaseUrl` or
`InternalToken` blank ⇒ `Mode=Misconfigured`. Otherwise `Ready`.

## 10. Preview Endpoint

`POST /api/commerce/integration/tenant-billing/billing-accounts/{id}/preview-entitlement` →
`PreviewEntitlementResult`:

- `BillingAccountId`, `TenantId?`, `CanPublish`, `SkipReason?`,
  `TenantBillingPayload?`.

Behaviour:

- Builds the same Commerce snapshot the publish path uses.
- Runs the same tenant-id resolution and same mapper.
- **Sends no HTTP.** **Mutates no state.**
- 400 if `billingAccountId` is `Guid.Empty`.
- 404 if the BillingAccount row does not exist (same convention as
  publish).
- If publisher is disabled, the preview still builds the payload and
  returns it with `CanPublish=false` and `SkipReason="publisher-disabled"`
  so the operator can verify what *would* be sent.
- If tenant id can't be resolved, `CanPublish=false`,
  `SkipReason ∈ {"no-external-tenant-id","external-tenant-id-not-a-guid"}`,
  `TenantBillingPayload=null` (we don't ship a payload that the
  receiver could not route).

## 11. Error Handling / Resilience Behavior

(Same matrix as TB-INT-01 plus the new transient/circuit rows; see
sections 5 and 6.)

| Trigger                                          | Final outcome | Reason                                    |
|--------------------------------------------------|---------------|-------------------------------------------|
| 2xx                                              | Published     | `published`                               |
| 408 / 429 / 5xx after retries                    | Failed        | `tenant-billing-{code}`                   |
| Transport error after retries                    | Failed        | `tenant-billing-unreachable`              |
| Timeout after retries                            | Failed        | `tenant-billing-timeout`                  |
| 400 / 401 / 403 / 404 / 409                      | Failed        | `tenant-billing-{code}-…`                 |
| Circuit breaker open                             | Failed        | `tenant-billing-circuit-open`             |
| Any other unexpected exception                   | Failed        | `tenant-billing-publish-exception`        |
| Disabled / unknown BA / no-tenant / non-guid id  | Skipped       | (existing TB-INT-01 reasons)              |

## 12. Tests Added

All under `services/Commerce/tests/Commerce.Tests/Integration/TenantBilling/`.

- **PublisherTestHelpers.cs** — shared `Build(...)` factory for the
  publisher under test, a `FakeSnapshots` snapshot service, and a
  `StaticOptionsMonitor<T>` shim. Returns a 6-tuple
  `(pub, http, snaps, breaker, metrics, opts)` with sensible defaults.
- **FakeHttpMessageHandler.cs** — extended with `Sequence(...)` for
  retry scripts (per-attempt response or thrown exception) and a
  `CallCount` accessor.
- **TenantBillingEntitlementPublisherTests.cs** — TB-INT-01 unit
  tests reworked onto the new helpers; behaviour and reasons unchanged.
  16 tests.
- **TenantBillingEntitlementPublisherRetryTests.cs** — TB-INT-02 retry
  matrix: 500 then OK, 503 exhaustion, 429 → OK, 408 → OK,
  HttpRequestException exhaustion, 400/401/403/404/409 not retried,
  zero-retry single-attempt path, and option clamping. 13 tests.
- **TenantBillingPublisherCircuitBreakerTests.cs** — disabled
  breaker never short-circuits, opens after configured failures,
  reopens after a failed transient probe, closes after a successful
  probe, closes after a probe answered with non-transient 4xx
  (regression for the half-open-stuck bug), 4xx does not trip from
  Closed. 6 tests.
- **TenantBillingPublisherPreviewAndDiagnosticsTests.cs** — preview
  null for unknown BA, payload + tenant id for resolvable BA, skip
  reasons for missing/non-guid tenant id, payload-with-skip-reason
  when publisher disabled; diagnostics modes (Disabled / Ready /
  Misconfigured) and a serialisation test asserting the internal
  token never leaks. 11 tests.
- **TenantBillingPublisherEndpointTests.cs** — controller surface:
  publish 404 / 400 / 200-skipped (existing) plus diagnostics 200
  with secret-not-leaked check, preview 404 / 400 /
  200-with-skip-reason for the disabled in-memory factory. 6 tests.

Total: 70 TenantBilling tests in the publisher area, all passing
locally (see §13).

## 13. Validation Results

- `dotnet build services/Commerce/Commerce.sln -c Debug` — succeeds
  via `dotnet test`'s implicit build (no errors; the only warnings
  are the pre-existing `NU1902` advisory on
  `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.9.0` and the
  pre-existing `CS1998` on `EntitlementSnapshotServiceTests`).
- `dotnet test --filter FullyQualifiedName~Commerce.Tests.Integration.TenantBilling`
  — `Passed: 70, Failed: 0, Skipped: 0, Duration: 2 s`.
- `Commerce API` workflow restarts cleanly with the new DI graph and
  starts listening on `:5000`.
- No changes to migrations, no DB schema diff.
- No changes to `services/tenant-billing/` or `artifacts/*`.

## 14. Risks / Deferred Items

- Real distributed circuit breaker (per-host shared state) is
  deferred; the in-process breaker is sufficient given a single
  Commerce instance per environment today.
- Per-tenant rate limits / Polly retry policy on the shared
  `Resilience:Http` are intentionally **not** wired into this
  publisher's typed client to keep its policy explicit and
  inspectable from the diagnostics endpoint.
- `RetryDelayMilliseconds` uses linear (constant) delay, not
  exponential backoff, because the destination is a single internal
  service and the cap is small.

## 15. Confirmation of Strict Exclusions

This block does **not**:

- Wire automatic publishing on subscription / account-standing
  changes (no event handler added).
- Add entitlement enforcement in Tenant Billing CRUD.
- Touch LegalSynq Identity / Control Center UI / Tenant Portal UI.
- Share a database, table, or domain assembly between Commerce and
  Tenant Billing.
- Modify Tenant Billing schema, controllers, or business logic.
- Add Notification or Documents integration.
- Change payment providers.

## 16. Recommended Next Block

- **TB-INT-03 — Auto-publish hook**: subscribe to existing Commerce
  domain events (subscription started/changed/cancelled, account
  standing changed) and call `PublishForBillingAccountAsync` from a
  background queue. Builds on the diagnostics endpoint added here so
  operators can watch publish health in real time.
