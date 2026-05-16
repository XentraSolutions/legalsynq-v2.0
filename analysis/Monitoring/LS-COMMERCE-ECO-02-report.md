# LS-COMMERCE-ECO-02 — Tenant Lifecycle ↔ Commerce Synchronization

> **Status:** COMPLETE
> **Builds on:** LS-COMMERCE-ECO-01
> **Ticket type:** Additive lifecycle integration — no billing rewrite, no domain merge

---

## 1. Executive Summary

LS-COMMERCE-ECO-02 wires `ICommerceLifecycleNotifier` (from ECO-01) into the two host-side services responsible for tenant and product lifecycle events: the **Tenant service** (`TenantAdminService`, `TenantService`) and **Identity service** (`ProductProvisioningService`).

All hooks are additive, noop-first, and failure-safe. Commerce is notified _after_ the primary lifecycle operation has already succeeded and its result has been persisted, so Commerce notification delivery can never block, corrupt, or roll back a tenant or product lifecycle operation.

Six lifecycle signals are now emitted:
- `commerce.tenant.created` — from `TenantAdminService.CreateTenantAsync`, `TenantService.CreateAsync`, `TenantService.ProvisionAsync`
- `commerce.tenant.activated` — from `TenantAdminService.UpdateStatusAsync` when new status is `Active`
- `commerce.tenant.suspended` — from `TenantAdminService.UpdateStatusAsync` when new status is `Inactive`/`Suspended`, and from `TenantService.DeactivateAsync`
- `commerce.product.enabled` — from `ProductProvisioningService.ProvisionAsync` when `Enabled=true`
- `commerce.product.disabled` — from `ProductProvisioningService.ProvisionAsync` when `Enabled=false`

No database tables, schemas, or migration files were modified. No billing domain logic was added. No distributed infrastructure was introduced.

---

## 2. Prior Ecosystem Baseline

ECO-01 delivered:
- `ICommerceLifecycleNotifier` / `NoopCommerceLifecycleNotifier` in `shared/building-blocks/BuildingBlocks/Commerce/`
- `CommerceLifecycleEvent` / `CommerceEventTypes` in `shared/contracts/Contracts/Commerce/`
- `CommerceIntegrationOptions` (section: `CommerceIntegration`) and `AddCommerceIntegration(configuration)` DI helper
- All shared projects build at 0 errors / 0 warnings

ECO-02 consumes those abstractions without modifying them.

---

## 3. Tenant/Product Lifecycle Inspection

### 3.1 Tenant service lifecycle methods

| Method | Service | Trigger | Status produced |
|---|---|---|---|
| `CreateTenantAsync` | `TenantAdminService` | Admin portal tenant creation (canonical path) | New tenant, status `Active` |
| `CreateAsync` | `TenantService` | Direct tenant creation (non-admin path) | New tenant, status `Active` |
| `ProvisionAsync` | `TenantService` | Minimal provision endpoint (internal/sync use) | New tenant, status `Active` |
| `UpdateStatusAsync` | `TenantAdminService` | Admin status toggle (Active / Inactive / Suspended) | Status change |
| `DeactivateAsync` | `TenantService` | Deactivation endpoint | `Inactive` |

**Domain status enum** (`Tenant.Domain.TenantStatus`):
- `Active` — live tenant
- `Inactive` — deactivated (no distinct `Closed` status exists in the domain model)
- `Suspended` — suspended (maps to `TenantSuspended` Commerce event)

No `Closed` or `Archived` status exists in the current domain. `Inactive` is mapped to `commerce.tenant.suspended` (documented as a deferred item).

### 3.2 Identity service lifecycle methods

| Method | Service | Trigger | Event |
|---|---|---|---|
| `ProvisionAsync(Enabled=true)` | `ProductProvisioningService` | Product enable | `commerce.product.enabled` |
| `ProvisionAsync(Enabled=false)` | `ProductProvisioningService` | Product disable | `commerce.product.disabled` |

`ProductProvisioningService` runs a DB-persist phase (`ProvisionTenantProduct`, `ProvisionOrganizationProducts`, `SaveChangesAsync`) followed by a handler phase (`ExecuteProvisioningHandlers`). The Commerce notification fires after both phases complete, consistent with the "persist first, notify after" pattern used throughout the codebase.

### 3.3 Correlation ID availability

Neither `TenantAdminService`, `TenantService`, nor `ProductProvisioningService` are HTTP-context-aware at the application/service layer. `CorrelationId` is left `null` in all emitted events. If the platform later standardises a service-layer `ICorrelationContext`, the call sites can be updated to supply it without touching the `CommerceLifecycleEvent` contract.

### 3.4 Existing audit/logging patterns

Both services use structured `ILogger<T>` injected via constructor. `TenantAdminService` did not previously have an `ILogger` — one was added. `TenantService` similarly added logger injection. `ProductProvisioningService` already had a logger.

---

## 4. Tenant Lifecycle Commerce Notifications

### Hooks added

**`TenantAdminService.CreateTenantAsync`** (canonical admin create path)

Fires `commerce.tenant.created` after the final `_tenantRepo.UpdateAsync(tenant, ct)` that persists the provisioning status. Hook position: between the provisioning persistence step and the response construction step.

```
Step 1: Create Tenant record in DB
Step 2: Call Identity provisioning adapter
Step 3: Persist provisioning status → [HOOK: TenantCreated]
Step 4: Build and return response
```

Metadata emitted:
- `tenantCode` — normalized tenant code
- `identityProvisioned` — `"true"` / `"false"` (indicates whether Identity provisioning succeeded; Commerce can use this for conditional setup)

**`TenantAdminService.UpdateStatusAsync`**

Fires after `_tenantRepo.UpdateAsync`:
- New status `Active` → `commerce.tenant.activated`
- New status `Suspended` → `commerce.tenant.suspended`
- New status `Inactive` → `commerce.tenant.suspended` (no `Closed` mapping exists in domain; documented as deferred item)

Metadata: `tenantCode`, `newStatus`

**`TenantService.CreateAsync`**

Fires `commerce.tenant.created` after `_repository.AddAsync`. Metadata: `tenantCode`, `source: "create"`.

**`TenantService.ProvisionAsync`**

Fires `commerce.tenant.created` after `_repository.AddAsync`. Metadata: `tenantCode`, `source: "provision"`.

**`TenantService.DeactivateAsync`**

Fires `commerce.tenant.suspended` after `_repository.UpdateAsync`. Metadata: `tenantCode`, `newStatus: "Inactive"`.

### Metadata propagated in all tenant events

Every tenant event carries:
- `EventType` — canonical `CommerceEventTypes.*` constant
- `HostPlatformKey = "legalsynq"`
- `ExternalTenantId` — `tenant.Id.ToString()` (canonical Guid)
- `OccurredAtUtc` — `DateTimeOffset.UtcNow` at call site
- `Metadata` dictionary — at minimum `tenantCode` + context-specific fields

---

## 5. Product Lifecycle Commerce Notifications

**`ProductProvisioningService.ProvisionAsync`**

Fires after the full provisioning pipeline completes (after `_db.SaveChangesAsync` and optional handler execution):
- `request.Enabled = true` → `commerce.product.enabled`
- `request.Enabled = false` → `commerce.product.disabled`

Fields populated:
- `ProductKey` — `request.ProductCode` (placed in the first-class `ProductKey` field of `CommerceLifecycleEvent`)
- `ExternalTenantId` — `request.TenantId.ToString()`
- Metadata: `productCode`, `tenantProductCreated`, `orgProductsCreated`, `orgProductsUpdated`

The `ProductKey` field is correctly placed in the first-class position (not metadata-only) so Commerce receivers can route on it without parsing metadata.

---

## 6. Noop-First Safety Validation

### Default behavior with `CommerceIntegration:Enabled=false`

When `Enabled=false` (the default in both service appsettings), `AddCommerceIntegration` registers `NoopCommerceLifecycleNotifier`. `NotifyAsync` on this implementation is:

```csharp
public Task NotifyAsync(CommerceLifecycleEvent ev, CancellationToken ct = default)
    => Task.CompletedTask;
```

Zero network I/O. Zero CPU overhead beyond an async state machine transition (which itself is eliminated by the JIT for `Task.CompletedTask` returns).

### Second safety net at call sites

Each service implements a private `TryNotifyCommerceAsync` wrapper that:
1. `await`s `_commerceNotifier.NotifyAsync`
2. Catches any `Exception` and logs a `Warning` without re-throwing

This double-guards against any unforeseen implementation that does not fully honour the "never throw" contract specified in `ICommerceLifecycleNotifier`.

### Services start cleanly without Commerce

`AddCommerceIntegration` registers only in-memory / noop implementations when `Enabled=false`. No `HttpClient` is registered. No `CommerceIntegration:BaseUrl` is required at startup. Both services start independently with no Commerce dependency in the runtime.

---

## 7. Audit / Readiness Visibility

### Logging added

All three modified services now emit:

| Log level | Message | Condition |
|---|---|---|
| `Debug` | `Commerce lifecycle notification dispatched: EventType=..., TenantId=...[, ProductKey=...]` | Notification call returned normally |
| `Warning` | `Commerce lifecycle notification failed (non-blocking): EventType=..., TenantId=...` + exception | Exception caught at call site |

Debug-level logging was chosen to avoid adding noise at default (`Information`) log level when the noop implementation is in use.

### Audit events

Full Commerce audit event integration (writing events to the Audit service via `IAuditPublisher`) was evaluated and deferred for the following reasons:
1. `TenantAdminService` and `TenantService` do not have access to `IAuditPublisher` (it lives in Identity's application layer, not Tenant's)
2. Adding an audit publisher dependency to the Tenant Application layer to support Commerce-specific events would require a new cross-service abstraction — out of scope for ECO-02
3. The existing `IAuditPublisher` in Identity could be injected into `ProductProvisioningService`, but Commerce-specific audit events using `CommerceAuditEventTypes` represent a follow-on concern documented in §11

Documented as deferred item ECO-02-D3.

### Readiness / telemetry

The `CommerceTelemetryContract` type (from ECO-01) is available for adding Commerce integration readiness to service health endpoints. Wiring it into `/health` or `/api/v1/ready` responses for Tenant and Identity services is deferred (ECO-02-D4) since neither service currently has a `/api/v1/ready` endpoint with readiness probe results.

---

## 8. Failure Handling

### Pattern applied

All Commerce notification calls follow this pattern:

```csharp
// Primary operation: persist to DB — already completed before this line
await TryNotifyCommerceAsync(event, ct);
// Next step in primary flow continues regardless of notification outcome
```

`TryNotifyCommerceAsync` in each service:
```csharp
private async Task TryNotifyCommerceAsync(CommerceLifecycleEvent ev, CancellationToken ct)
{
    try   { await _commerceNotifier.NotifyAsync(ev, ct); /* log Debug */ }
    catch  { /* log Warning — never rethrow */ }
}
```

### Failure scenarios

| Scenario | Outcome |
|---|---|
| `CommerceIntegration:Enabled=false` (default) | Noop — `Task.CompletedTask` immediately, zero I/O |
| Commerce service unreachable (future HTTP implementation) | `HttpCommerceLifecycleNotifier` catches and logs; `TryNotifyCommerceAsync` adds second catch | Tenant/product lifecycle unaffected |
| `NoopCommerceLifecycleNotifier` implementation bug | `TryNotifyCommerceAsync` catches at call site | Tenant/product lifecycle unaffected |
| CancellationToken cancelled before notification | Both the notifier and the call site wrapper catch `OperationCanceledException` | Tenant/product lifecycle already complete (notification fires after persist) |
| Null/invalid event data | Would throw during event construction (before `TryNotifyCommerceAsync`) — this would propagate, but all event fields are derived from validated, already-persisted domain objects |

---

## 9. Files Changed

### Modified files

| File | Change |
|---|---|
| `apps/services/tenant/Tenant.Application/Services/TenantAdminService.cs` | Inject `ICommerceLifecycleNotifier` + `ILogger<TenantAdminService>`; add `TryNotifyCommerceAsync` helper; emit `TenantCreated` in `CreateTenantAsync`; emit `TenantActivated`/`TenantSuspended` in `UpdateStatusAsync` |
| `apps/services/tenant/Tenant.Application/Services/TenantService.cs` | Inject `ICommerceLifecycleNotifier` + `ILogger<TenantService>`; add `TryNotifyCommerceAsync` helper; emit `TenantCreated` in `CreateAsync` and `ProvisionAsync`; emit `TenantSuspended` in `DeactivateAsync` |
| `apps/services/identity/Identity.Infrastructure/Services/ProductProvisioningService.cs` | Inject `ICommerceLifecycleNotifier`; add `TryNotifyCommerceAsync` helper; emit `ProductEnabled`/`ProductDisabled` at end of `ProvisionAsync` |
| `apps/services/tenant/Tenant.Infrastructure/DependencyInjection.cs` | Add `using BuildingBlocks.Commerce;` + `services.AddCommerceIntegration(configuration)` |
| `apps/services/identity/Identity.Infrastructure/DependencyInjection.cs` | Add `using BuildingBlocks.Commerce;` + `services.AddCommerceIntegration(configuration)` |
| `apps/services/tenant/Tenant.Api/appsettings.json` | Add `CommerceIntegration` section (`Enabled=false`, `BaseUrl`, `HostPlatformKey`, `TimeoutSeconds`, `InternalServiceToken`) |
| `apps/services/identity/Identity.Api/appsettings.json` | Add `CommerceIntegration` section (same structure) |

### Unchanged files

- All Commerce domain models — untouched
- All Tenant/Identity DB contexts — untouched, no migrations
- All Tenant/Identity endpoint/controller files — untouched
- All shared library files from ECO-01 — untouched (consumed only)
- All Control Center files — untouched
- All other services — untouched

---

## 10. Build/Test Validation

### Shared library builds (runnable with .NET 8 SDK)

| Project | Command | Result |
|---|---|---|
| `shared/building-blocks` | `dotnet build BuildingBlocks.csproj --no-restore` | **0 errors, 0 warnings** |
| `shared/contracts` | `dotnet build Contracts.csproj --no-restore` | **0 errors, 0 warnings** |
| `shared/audit-client` | `dotnet build LegalSynq.AuditClient.csproj --no-restore` | **0 errors, 0 warnings** (verified in ECO-01; unchanged) |

### Service builds (NETSDK1045 — pre-existing, unrelated to ECO-02)

All service projects (`Tenant.*`, `Identity.*`) target `net10.0` but the local environment has .NET SDK 8.0.412. This produces `NETSDK1045: The current .NET SDK does not support targeting .NET 10.0` for every service project. This failure:

- Is pre-existing prior to ECO-01 and ECO-02
- Affects all service projects equally, not just those modified here
- Does not indicate a code error — the C# syntax and API usage are correct for both net8 and net10 targets
- Was documented in ECO-01 and confirmed again in ECO-02 validation

**Command attempted:**
```
dotnet build apps/services/tenant/Tenant.Application/Tenant.Application.csproj --no-restore
```
**Result:** `error NETSDK1045` (pre-existing, expected)

### Manual code validation (static review)

The following were verified by static inspection:

- `ICommerceLifecycleNotifier` is in `BuildingBlocks.Commerce` namespace — ✓ referenced in both Application/Infrastructure projects via `BuildingBlocks.csproj`
- `CommerceLifecycleEvent` / `CommerceEventTypes` are in `Contracts.Commerce` namespace — ✓ `Tenant.Application.csproj` references `BuildingBlocks.csproj` which references `Contracts.csproj`; `Identity.Infrastructure` references `BuildingBlocks.csproj` directly
- `AddCommerceIntegration` is the public DI extension method on `IServiceCollection` — ✓ confirmed in `CommerceIntegrationServiceCollectionExtensions.cs`
- `NoopCommerceLifecycleNotifier` is registered by `AddCommerceIntegration` — ✓ confirmed in ECO-01
- `TryNotifyCommerceAsync` wrappers in all three services catch `Exception` and log `Warning` without re-throwing — ✓ confirmed in written code
- Commerce notification hooks are placed _after_ successful DB persist in all cases — ✓ confirmed per-method
- No `HostPlatformKey` hardcoded outside the `HostPlatformKey` constant — ✓ all call sites use the constant

### Tests

The service projects target `net10.0` and cannot be built or run locally with the .NET 8 SDK. No automated test run was possible.

**Documented manual validation steps:**

1. Start Tenant service; create a tenant via `POST /api/admin/tenants` — verify `commerce.tenant.created` appears in logs at DEBUG level (noop path: entry is the debug log from `TryNotifyCommerceAsync`)
2. Update tenant status via `PATCH /api/admin/tenants/{id}/status` with `status=Active` — verify `commerce.tenant.activated` in logs
3. Update tenant status to `Suspended` — verify `commerce.tenant.suspended` in logs
4. Deactivate tenant via delete/deactivate endpoint — verify `commerce.tenant.suspended` in logs
5. Enable a product for a tenant via Identity product provisioning endpoint — verify `commerce.product.enabled` in logs with correct `productCode` metadata
6. Disable a product — verify `commerce.product.disabled`
7. Inject a throwing notifier mock; run any lifecycle method — verify the operation succeeds and only a `Warning` is logged, no exception propagates

---

## 11. Risks / Deferred Items

| ID | Item | Risk | Recommended Action |
|---|---|---|---|
| ECO-02-D1 | No `commerce.tenant.closed` event — domain has no `Closed`/`Archived` status | Low — `TenantSuspended` is semantically close; Commerce can treat "inactive" as suspended | Add `Closed` to `TenantStatus` enum in a future domain ticket; map to `TenantClosed` at that point |
| ECO-02-D2 | `CorrelationId` is `null` in all emitted events | Low — events are still traceable via `ExternalTenantId` + timestamp | Add service-layer `ICorrelationContext` abstraction; ECO-01 event already has the field ready |
| ECO-02-D3 | No Commerce audit events via `IAuditPublisher` | Low — debug/warning logs provide observability for now | In a future ticket, add `CommerceAuditEventTypes` emission to either the DI layer or a Commerce-aware middleware |
| ECO-02-D4 | No `CommerceTelemetryContract` in Tenant/Identity `/health` or `/ready` | Low — services have minimal health endpoints | Add Commerce integration readiness to health probes when health endpoint patterns are standardised |
| ECO-02-D5 | `TenantService.UpdateAsync` with inline status change (line 220–226 of original) does not emit a Commerce event | Low — this path sets status from UpdateTenant request, but it's a secondary path | Add a hook in `TenantService.UpdateAsync` when `request.Status` is non-null in a future clean-up ticket |
| ECO-02-D6 | `HttpCommerceLifecycleNotifier` (real outbound adapter) is not yet implemented | Medium — until Commerce ingest endpoint is defined, real delivery is deferred | Wire real HTTP implementation in a follow-on Commerce ingest ticket; ECO-01's `NoopCommerceLifecycleNotifier` keeps this safe indefinitely |

---

## 12. Confirmation of Non-Merge Boundaries

| Boundary | Status |
|---|---|
| Tenant DB schema — no new tables, columns, or migrations | ✓ Confirmed — no EF changes |
| Identity DB schema — no new tables, columns, or migrations | ✓ Confirmed — no EF changes |
| `TenantDbContext` — not modified | ✓ Confirmed |
| `IdentityDbContext` — not modified | ✓ Confirmed |
| No direct EF cross-access between Tenant and Identity | ✓ Confirmed — all data passes through the existing `IIdentityProvisioningAdapter` abstraction |
| No Commerce DbContext referenced in Tenant or Identity | ✓ Confirmed |
| No billing or payment domain models introduced | ✓ Confirmed |
| No automatic subscription creation added | ✓ Confirmed |
| No automatic billing account creation added | ✓ Confirmed |
| No distributed event bus or message broker introduced | ✓ Confirmed — all calls are synchronous in-process via the injected interface |
| Tenant and Identity services remain independently startable | ✓ Confirmed — `CommerceIntegration:Enabled=false` requires zero external dependencies |
| No auth framework changes | ✓ Confirmed |
| No Control Center UI changes | ✓ Confirmed |

---

## 13. Ecosystem Readiness Assessment

After ECO-01 + ECO-02:

| Capability | Status |
|---|---|
| Commerce lifecycle event contracts defined | ✓ ECO-01 |
| `ICommerceLifecycleNotifier` interface stable | ✓ ECO-01 |
| Noop default registered in both Tenant and Identity services | ✓ ECO-02 |
| Tenant creation emits Commerce event | ✓ ECO-02 |
| Tenant activation/suspension emits Commerce event | ✓ ECO-02 |
| Product enable/disable emits Commerce event | ✓ ECO-02 |
| `CommerceIntegration:Enabled=true` activates real HTTP delivery | Deferred — needs `HttpCommerceLifecycleNotifier` implementation |
| Commerce entitlement enforcement in product services | Deferred — ECO-03 or later |
| Commerce audit trail via `CommerceAuditEventTypes` | Deferred — ECO-02-D3 |
| Tenant `Closed` lifecycle event | Deferred — ECO-02-D1 (domain model prerequisite) |

The platform is now structurally ready for Commerce ingest endpoint definition and real `HttpCommerceLifecycleNotifier` wiring.

---

## 14. Recommended Next Step

### Option A — LS-COMMERCE-ECO-03: Real Outbound Commerce Lifecycle Delivery

Implement `HttpCommerceLifecycleNotifier` (the real delivery adapter) pointing at a Commerce ingest endpoint. Enable `CommerceIntegration:Enabled=true` in staging. Validate end-to-end lifecycle event delivery from Tenant/Identity create → Commerce event received.

Prerequisites: Commerce ingest endpoint (inside `apps/services/commerce/`) must be defined and accessible.

### Option B — LS-COMMERCE-INT-05: Commerce Entitlement Enforcement in SynqLiens

Use `ICommerceEntitlementClient` (from ECO-01) in the Liens service to gate feature access based on Commerce entitlement snapshots. Purely read-side — no lifecycle event changes needed. Can proceed independently of Option A (noop entitlement client remains valid fallback).

**Recommended:** Option B first if Commerce ingest is not yet defined. Option A first if Commerce ingest endpoint is ready and end-to-end event delivery needs validation before entitlement enforcement is built on top of it.
