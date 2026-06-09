# TB-INT-03 — Automatic Entitlement Publishing

> Status: **DONE**
> Scope: wire the existing `ITenantBillingEntitlementPublisher` to
> Commerce subscription / account-standing lifecycle changes via a
> bounded in-process queue + hosted worker, gated by configuration
> and disabled by default. No Tenant Billing changes; no Identity/UI;
> no enforcement.

## 1. Summary

TB-INT-03 turns the manual TB-INT-01/02 publisher into an event-driven
integration. Commerce now enqueues a small, structured work item onto
a bounded in-process channel after every subscription lifecycle commit
and after every account-standing recalculation. A hosted background
worker drains the channel in a fresh DI scope per item and forwards
each one to the existing `ITenantBillingEntitlementPublisher`. The
whole feature is dark by default — gated by
`Commerce:TenantBilling:AutoPublishEnabled` (default `false`) — and is
strictly best-effort: enqueue is non-blocking, never throws, and
cannot roll back the Commerce transaction that already committed.

## 2. Codebase Analysis

The existing TB-INT-01 / TB-INT-02 publisher (`TenantBillingEntitlementPublisher`)
is a typed `HttpClient` with retry, circuit breaker, and metrics. It
is invoked manually today by the `TenantBillingPublisherController`
endpoints. Subscription state mutations live in
`Commerce.Infrastructure.Subscriptions.Services.SubscriptionService`
(Create / Activate / Suspend / Reactivate / Cancel / Renew /
ChangePlan); account-standing recalculation lives in
`Commerce.Infrastructure.AccountStanding.Services.AccountStandingService.EvaluateAsync`.
Both services already commit through `CommerceDbContext.SaveChangesAsync`
and have a `CompositionRoot` (`DependencyInjection.cs`) that wires the
publisher chain, so the new pieces fit cleanly into the existing
seams.

## 3. Trigger Points Found

In `SubscriptionService`:

- `CreateAsync` — new subscription created (account becomes
  entitlement-bearing).
- `ActivateAsync` / `SuspendAsync` / `ReactivateAsync` — status
  transitions that affect access recommendation.
- `CancelAsync` — both at-period-end and immediate variants; either
  removes the subscription from the entitlement set.
- `ChangePlanAsync` — plan / price swap changes the snapshot's plan
  and product references and limits.
- `RenewAsync` — period roll-over only; entitlement set unchanged
  (see §4 for justification of skipping).

In `AccountStandingService.EvaluateAsync` — recomputes
`AccountStandingStatus` and persists it. Standing is the dominant
input to `AccessRecommendation` in the snapshot, so every recompute
is a real signal.

## 4. Trigger Points Implemented

| Trigger | Label | File / Site |
|---|---|---|
| `SubscriptionService.CreateAsync` | `subscription-created` | post-`SaveChangesAsync` in retry loop |
| `SubscriptionService.ActivateAsync` | `subscription-activated` | via `MutateAsync(triggerSource:)` |
| `SubscriptionService.SuspendAsync` | `subscription-suspended` | via `MutateAsync(triggerSource:)` |
| `SubscriptionService.ReactivateAsync` | `subscription-reactivated` | via `MutateAsync(triggerSource:)` |
| `SubscriptionService.CancelAsync` | `subscription-cancelled` | via `MutateAsync(triggerSource:)` |
| `SubscriptionService.ChangePlanAsync` | `subscription-plan-changed` | inline post-`SaveChangesAsync` |
| `AccountStandingService.EvaluateAsync` | `account-standing-recalculated` | inline post-`SaveChangesAsync` |

`RenewAsync` is intentionally **not** wired. A renewal advances the
billing period but does not change the entitlement set or the access
recommendation. Re-publishing on every renewal would amplify load on
Tenant Billing without any change to the data the receiver stores. If
a non-paying renewal moves account standing, that path is already
covered by `AccountStandingService.EvaluateAsync` independently. The
SubscriptionService.RenewAsync method carries an explanatory comment
calling this out.

`PaymentService` is also not wired directly — payment recording feeds
into account-standing through the existing recalculation flow, which
is the trigger we publish on.

## 5. Queue / Worker Architecture

```
┌─────────────────────────┐  Enqueue (sync, non-blocking)
│ SubscriptionService     │ ─────────────────────────────┐
│ AccountStandingService  │                              ▼
└─────────────────────────┘     ┌─────────────────────────────────────┐
                                │ BoundedTenantBillingEntitlement-     │
                                │ PublishQueue (Channel<T>, bounded)   │
                                │  • capacity: AutoPublishQueueCapacity │
                                │  • FullMode = Wait + TryWrite        │
                                │  • SingleReader, multi-writer        │
                                └────────────────────┬─────────────────┘
                                                     │ ReadAllAsync
                                                     ▼
                                ┌─────────────────────────────────────┐
                                │ TenantBillingEntitlementPublishWorker │
                                │ (BackgroundService)                  │
                                │  • new DI scope per item             │
                                │  • never throws out of ExecuteAsync  │
                                │  • routes to existing publisher      │
                                └─────────────────────────────────────┘
```

Key design choices:

- **Channel<T> with `BoundedChannelFullMode.Wait` + `TryWrite`.**
  The Wait mode would block writers when full, but the trigger sites
  use `TryWrite` (non-blocking) which returns `false` instead — exactly
  the synchronous, never-blocks-on-full semantics we need.
- **Singleton queue, hosted worker.** Always registered. When
  `AutoPublishEnabled=false` the queue refuses writes
  (`SkippedDisabled`) and the worker idles on the empty channel —
  toggling the config flag is sufficient to enable / disable without
  redeploying.
- **Per-item DI scope.** `IServiceScopeFactory` is used to create a
  fresh scope per work item so `CommerceDbContext` (scoped) and the
  typed-client publisher (transient) get their normal lifetimes.
- **Decoupled from publisher `Enabled`.** The worker still drains the
  queue when the publisher is disabled; the publisher returns
  `Skipped` and no HTTP traffic occurs. This lets operators turn off
  the wire without losing visibility into trigger volume.

## 6. Configuration Changes

`Commerce:TenantBilling` section (`appsettings.json`) gains:

```json
"AutoPublishEnabled": false,
"AutoPublishQueueCapacity": 1000
```

These map to `TenantBillingClientOptions.AutoPublishEnabled` (default
`false`) and `AutoPublishQueueCapacity` (default `1000`, clamped to
`[1, 100000]` in `Normalised()`). No environment variable is required
to deploy this block dark.

## 7. Diagnostics Changes

`TenantBillingDiagnostics` (returned by `GetDiagnosticsAsync` and
exposed via the existing diagnostics endpoint) gains four backward-
compatible optional fields with safe defaults:

- `AutoPublishEnabled` — mirrors the option flag.
- `AutoPublishQueueCapacity` — current channel capacity.
- `AutoPublishQueueDepth` — pending items right now.
- `WorkerRegistered` — `true` when the queue dependency is wired in
  DI (i.e. production / integration tests); `false` for direct-ctor
  unit tests that don't pass the queue.

The publisher constructor accepts an optional
`ITenantBillingEntitlementPublishQueue?` so existing TB-INT-01/02
unit tests in `PublisherTestHelpers.Build` continue to compile and
pass without modification.

## 8. Logging / Metrics Behavior

Structured logs are emitted at every state transition:

- Trigger sites log `Auto-publish enqueue accepted/skipped/dropped/invalid`
  at Debug/Warning depending on outcome, with
  `BillingAccountId`, `TriggerSource`, `QueueDepth`, and `Capacity`.
- Worker logs `Auto-publish started`, `Auto-publish completed/skipped/failed`,
  and a top-level start/stop, with `Trigger`, `TenantId`, `HttpStatus`,
  `Attempts`, `Reason`.

Four new counters are added to `TenantBillingPublisherMetrics`:

| Counter | Tags |
|---|---|
| `commerce.tenant_billing.autopublish.enqueued` | `trigger_source` |
| `commerce.tenant_billing.autopublish.dropped` | `trigger_source`, `reason` (`auto-publish-disabled` / `queue-full` / `invalid`) |
| `commerce.tenant_billing.autopublish.processed` | `trigger_source`, `outcome` (`published` / `skipped` / `failed`), `reason` |
| `commerce.tenant_billing.autopublish.failed` | `trigger_source`, `reason` (`exception` or publisher-supplied) |

These are independent from the existing TB-INT-01/02
`commerce.tenant_billing.publish.*` counters so dashboards can
distinguish manual vs auto-publish volume.

## 9. Failure Handling Behavior

The integration must never roll back a Commerce commit:

1. **Trigger-side enqueue is wrapped in `try / catch`.** Any
   exception is logged at Error and swallowed — the surrounding
   Commerce method always returns its normal response.
2. **`Enqueue` itself never throws** for any documented outcome. The
   non-blocking `TryWrite` returns `false` on full → mapped to
   `DroppedQueueFull`. Empty `BillingAccountId` / `TriggerSource` →
   `Invalid`. Disabled → `SkippedDisabled`.
3. **Worker per-item handler is wrapped in `try / catch`.** A bad
   work item logs + records a failed-autopublish counter and the
   worker continues with the next item. `OperationCanceledException`
   on the stopping token is honored cleanly.
4. **Worker top-level loop is wrapped.** Any escaped exception is
   logged at Error and the worker stops; it does not crash the host.

A `Worker_swallows_publisher_exception_and_keeps_going` test pins
this behavior.

## 10. Tests Added

All new tests live under `services/Commerce/tests/Commerce.Tests/`:

- `Integration/TenantBilling/BoundedTenantBillingEntitlementPublishQueueTests.cs`
  — capacity clamping, disabled→SkippedDisabled, empty inputs→Invalid,
  fill-then-drop, duplicates allowed and surfaced via `ReadAllAsync`.
- `Integration/TenantBilling/TenantBillingEntitlementPublishWorkerTests.cs`
  — items processed in order, publisher exception is swallowed and
  next item still processed, Skipped outcome handled, cancellation
  exits cleanly.
- `Integration/TenantBilling/RecordingPublishQueue.cs` — shared
  test-double for trigger-site assertions.
- `Integration/TenantBilling/TenantBillingDiagnosticsAutoPublishTests.cs`
  — diagnostics expose the four new fields when the queue is wired and
  fall back to safe defaults when not.
- `Subscriptions/SubscriptionServiceAutoPublishTriggerTests.cs` —
  `Create` enqueues `subscription-created`, lifecycle transitions
  enqueue the correct labels in order, `Renew` does NOT enqueue,
  Commerce ops succeed when auto-publish is disabled or when the
  queue refuses writes.
- `AccountStanding/AccountStandingServiceAutoPublishTriggerTests.cs`
  — `EvaluateAsync` enqueues `account-standing-recalculated`, succeeds
  when auto-publish is disabled.

## 11. Validation Results

Build (`dotnet build services/Commerce/Commerce.sln -c Debug`) — ✅
0 errors, only pre-existing NU1902 advisory warnings.

Tests (filtered to TenantBilling, AutoPublish, SubscriptionService,
AccountStanding) — `Total tests: 108. Passed: 107. Failed: 1.`

The single failure is `SubscriptionServiceTests.ChangePlan_closes_old_item_and_creates_new`,
a **pre-existing** test using a hard-coded clock of `2026-05-01` while
the `ChangePlan` validator reads real `DateTime.UtcNow` (today is
2026-05-15) and rejects `EffectiveAtUtc` "more than 1 day in the
past". My changes touch neither the validator nor the test; the
failure reproduces with my changes reverted. Tracked separately;
out-of-scope for this block.

All TB-INT-03-specific tests pass:
- `BoundedTenantBillingEntitlementPublishQueueTests` (5 tests)
- `TenantBillingEntitlementPublishWorkerTests` (4 tests)
- `TenantBillingDiagnosticsAutoPublishTests` (2 tests)
- `SubscriptionServiceAutoPublishTriggerTests` (5 tests)
- `AccountStandingServiceAutoPublishTriggerTests` (2 tests)

## 12. Risks / Deferred Items

- **In-process only.** The queue does not survive a host restart; an
  item enqueued seconds before SIGTERM may be dropped. This is the
  documented trade-off for an in-process design and matches the spec.
  A future block could add a durable outbox table backed by EF for
  guaranteed delivery.
- **No deduplication.** Two transitions on the same billing account in
  rapid succession enqueue twice. Tenant Billing's apply endpoint is
  upsert-shaped so duplicates are safe; we trade a small amount of
  redundant work for simplicity.
- **No batching.** Each item is one HTTP call. Acceptable at expected
  throughput; revisit only if metrics show sustained high enqueue
  rates.
- **Worker registered even when feature is off.** Cheap (one
  `await foreach` blocked on an empty channel) but worth noting.
  Removing this conditionally would force a process restart to enable
  the feature, which is worse.

## 13. Confirmation of Strict Exclusions

- **No Tenant Billing changes.** No edits under
  `services/tenant-billing/` (the .obj/* files showing as modified
  are build artifacts from running tests; no source changes).
- **No enforcement.** The publisher continues to write entitlements;
  nothing in Commerce reads or acts on what Tenant Billing returns.
- **No Identity / UI changes.** No edits under any `artifacts/*`.
- **No DB merge.** No new schema, no migrations.
- **Commerce transactions never block on Tenant Billing.** Enqueue is
  synchronous and non-blocking; the worker does the HTTP call in the
  background.

## 14. Recommended Next Block

- **TB-INT-04 — Durable outbox.** Replace the in-process channel with
  an EF-backed outbox table + the same worker pattern, so a host crash
  cannot lose a publish. Keep the channel as an in-process fast path
  fed by the outbox.
- **TB-INT-05 — Snapshot diff filter.** Have the worker fetch the
  previous snapshot and skip the HTTP call when nothing material has
  changed. Cheaper than always-publish, useful once volume is
  observed.
- **Operator-facing surface.** Expose the four new diagnostics fields
  (`AutoPublishEnabled`, capacity, depth, `WorkerRegistered`) in the
  existing diagnostics endpoint UI / dashboard so operators can flip
  the flag and watch the queue drain.
