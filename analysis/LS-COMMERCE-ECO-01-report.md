# LS-COMMERCE-ECO-01 — Cross-Service Commerce Integration Foundation

> **Status:** COMPLETE
> **Builds on:** LS-COMMERCE-INT-01, LS-COMMERCE-INT-02, LS-COMMERCE-INT-03, LS-COMMERCE-OPS-01

---

## 1. Executive Summary

LS-COMMERCE-ECO-01 establishes Commerce as a reusable, platform-wide monetization and entitlement foundation without collapsing domain boundaries or introducing shared databases. The deliverables are **purely additive** — no existing service logic was modified, no DbContexts were merged, and all services continue to run standalone.

The foundation consists of five areas:

| Area | Location | Purpose |
|------|----------|---------|
| **Commerce Event Contracts** | `shared/contracts/Contracts/Commerce/` | Canonical event types, lifecycle event envelope, telemetry contract |
| **Commerce Notification Templates** | `shared/contracts/Contracts/Notifications/` | 11 commerce-domain template keys + disabled platform defaults |
| **Commerce Integration Abstractions** | `shared/building-blocks/BuildingBlocks/Commerce/` | `ICommerceEntitlementClient`, `ICommerceLifecycleNotifier`, service metadata, noop + HTTP implementations, DI helpers |
| **Commerce Audit Event Types** | `shared/audit-client/LegalSynq.AuditClient/Enums/` | `CommerceAuditEventTypes` — 19 canonical audit event type strings |
| **BuildingBlocks ↔ Contracts dependency** | `BuildingBlocks.csproj` | Safe `ProjectReference` to `shared/contracts` enabling all integration types to flow through one layer |

---

## 2. Prior Commerce Operational Baseline

The following Commerce integration infrastructure existed prior to this ticket:

| Existing Component | Location | Role |
|--------------------|----------|------|
| `ICommerceEntitlementSnapshotService` | `Commerce.Application/Integration/Abstractions/` | Builds entitlement snapshots from Commerce DB — output-only |
| `IProvisioningHookPublisher` (noop) | `Commerce.Application/Integration/Abstractions/` | Commerce → Host hook; no-op in COM-B08 |
| `IHostIntegrationAdapter` | `Commerce.Application/Integration/Abstractions/` | Composite adapter per host platform |
| `LegalSynqCommerceHostIntegrationAdapter` | `Commerce.Infrastructure/Integration/HostAdapters/` | LegalSynq JWT-backed adapter |
| `Commerce.Contracts/Integration/` | — | `CommerceEntitlementSnapshot`, `ProvisioningHookRequest`, `AccessRecommendation`, `HostTenantRef`, `HostIdentityContext` |
| `HostIntegrationController` | `Commerce.Api/Controllers/Integration/` | Exposes snapshot/recommendation endpoints at `/api/commerce/integration/…` |
| MonitoringEntityBootstrap | `Monitoring.Infrastructure/Bootstrap/` | Commerce + Tenant Billing already seeded with per-name reconciliation |

**Gap this ticket closes**: No consuming service (Liens, Fund, CareConnect, etc.) had a shared, injectable path to Commerce's entitlement data. Each would have had to write its own ad-hoc HTTP client. ECO-01 provides a single `ICommerceEntitlementClient` that any service can register in three lines.

---

## 3. Shared Commerce Integration Abstractions (`shared/building-blocks`)

### 3.1 New files

| File | Purpose |
|------|---------|
| `BuildingBlocks/Commerce/CommerceEntitlementResult.cs` | Self-contained result record; never references `Commerce.Contracts` directly |
| `BuildingBlocks/Commerce/CommerceServiceMetadata.cs` | Declarative per-service monetization metadata |
| `BuildingBlocks/Commerce/ICommerceAwareService.cs` | Optional marker interface for self-describing services |
| `BuildingBlocks/Commerce/ICommerceEntitlementClient.cs` | Platform entitlement resolution contract |
| `BuildingBlocks/Commerce/ICommerceLifecycleNotifier.cs` | Host → Commerce lifecycle notification contract |
| `BuildingBlocks/Commerce/CommerceIntegrationOptions.cs` | Configuration model (section: `CommerceIntegration`) |
| `BuildingBlocks/Commerce/HttpCommerceEntitlementClient.cs` | HTTP implementation; calls Commerce's integration endpoints |
| `BuildingBlocks/Commerce/NoopCommerceEntitlementClient.cs` | Noop fallback; returns `Unavailable` for all calls |
| `BuildingBlocks/Commerce/NoopCommerceLifecycleNotifier.cs` | Noop fallback; accepts all events, no I/O |
| `BuildingBlocks/Commerce/CommerceIntegrationServiceCollectionExtensions.cs` | DI helpers: `AddCommerceIntegration()`, `AddCommerceServiceMetadata()` |

### 3.2 `BuildingBlocks.csproj` change

Added a `ProjectReference` to `shared/contracts/Contracts/Contracts.csproj`. This is the only structural change to an existing project file. The dependency direction is safe:

```
shared/contracts (zero deps) ←── shared/building-blocks ←── services
```

All services that already reference `BuildingBlocks` gain transitive access to `Contracts.Commerce` types. Services that already referenced both packages separately gain no duplicate resolution issue because .NET project references are deduplicated by project path.

### 3.3 Design decisions

**`CommerceEntitlementResult` is decoupled from `Commerce.Contracts`**
The `HttpCommerceEntitlementClient` deserializes Commerce's JSON response into private mirror DTOs and maps to `CommerceEntitlementResult`. This means:
- Consuming services have zero compile-time dependency on Commerce's internal contract versions.
- Commerce can evolve its snapshot DTO without forcing a recompile of all platform services.
- The integration surface is the HTTP response shape, not the C# class hierarchy.

**Noop-first, HTTP-opt-in**
All services start with `CommerceIntegration:Enabled = false`. This means:
- Zero runtime risk from wiring — services that don't set `Enabled = true` behave identically to pre-ECO-01 behaviour.
- Commerce does not need to be running for any service to start.
- Integration can be turned on incrementally per environment/service.

**Never throw**
Both `HttpCommerceEntitlementClient` and the noop fallback swallow all exceptions and return a `CommerceEntitlementResult` with `IsError = true`. Consuming services MUST NOT gate business operations on Commerce availability.

---

## 4. Entitlement Resolution Integration Pattern

### 4.1 How to consume from a product service

```csharp
// In Program.cs
builder.Services.AddCommerceIntegration(builder.Configuration);
builder.Services.AddCommerceServiceMetadata(
    builder.Configuration,
    serviceName:          "Synq Liens",
    productKey:           "SYNQLIEN",
    primaryFeatureKey:    null,
    subscriptionRequired: true,
    monetizationEnabled:  false);   // set true when enforcement is live
```

```json
// In appsettings.json (dev)
{
  "CommerceIntegration": {
    "Enabled": false,
    "BaseUrl": "http://127.0.0.1:5030",
    "HostPlatformKey": "legalsynq",
    "TimeoutSeconds": 10
  }
}
```

```csharp
// In a service/endpoint handler
public class LienAccessService(ICommerceEntitlementClient commerce, IOptions<CommerceIntegrationOptions> opts)
{
    public async Task<bool> CanAccessMarketplaceAsync(string tenantId, CancellationToken ct)
    {
        var result = await commerce.GetByHostTenantAsync("legalsynq", tenantId, ct);
        if (!result.IsAvailable)
            return true;  // permissive fallback when Commerce not available
        return result.HasProduct("SYNQLIEN") && result.IsAccessAllowed;
    }
}
```

### 4.2 Commerce endpoints called

| Method | Path | Used by |
|--------|------|---------|
| `GET` | `/api/commerce/integration/host-tenants/{key}/{id}/entitlement-snapshot` | `GetByHostTenantAsync` |
| `GET` | `/api/commerce/integration/billing-accounts/{id}/entitlement-snapshot` | `GetByBillingAccountAsync` |

Both exist and are served by `HostIntegrationController` in Commerce.Api (verified during exploration).

### 4.3 `CommerceEntitlementResult` shape

| Field | Type | Notes |
|-------|------|-------|
| `IsAvailable` | `bool` | `false` = tenant not found, disabled, or error |
| `AccessRecommendation` | `string` | `Allow` \| `ReadOnly` \| `GraceLimited` \| `Block` \| `Unknown` |
| `AccountStandingStatus` | `string` | Raw standing string from Commerce |
| `ProductKeys` | `IReadOnlyList<string>` | Active product keys on this account |
| `Plans` | `IReadOnlyList<CommerceEntitlementPlan>` | Active plan keys and names |
| `SnapshotGeneratedAtUtc` | `DateTimeOffset?` | When Commerce generated the snapshot |
| `IsAccessAllowed` | `bool` (computed) | `true` when `Allow` or `GraceLimited` |
| `HasProduct(key)` | `bool` (method) | Case-insensitive product key check |
| `IsError` | `bool` | `true` on HTTP/network/parse failure |
| `ErrorMessage` | `string?` | Human-readable error description |

---

## 5. Tenant Lifecycle ↔ Commerce Hooks

### 5.1 `ICommerceLifecycleNotifier`

Defines the Host → Commerce direction of lifecycle notification. The reverse (Commerce → Host via `IProvisioningHookPublisher`) was established in COM-B08.

```csharp
// Always noop at this phase — real outbound adapter in a future phase
await commerceLifecycleNotifier.NotifyAsync(new CommerceLifecycleEvent(
    EventType:        CommerceEventTypes.TenantActivated,
    HostPlatformKey:  "legalsynq",
    ExternalTenantId: tenant.Id.ToString(),
    OccurredAtUtc:    DateTimeOffset.UtcNow,
    CorrelationId:    correlationId));
```

### 5.2 `CommerceLifecycleEvent` record

| Field | Type | Notes |
|-------|------|-------|
| `EventType` | `string` | Use `CommerceEventTypes` constants |
| `HostPlatformKey` | `string` | `"legalsynq"` in all platform services |
| `ExternalTenantId` | `string` | Host tenant GUID string |
| `OccurredAtUtc` | `DateTimeOffset` | Event timestamp |
| `CorrelationId` | `string?` | Optional trace correlation |
| `BillingAccountId` | `string?` | Commerce billing account (when known) |
| `SubscriptionId` | `string?` | Subscription context (when relevant) |
| `ProductKey` | `string?` | Product context (when relevant) |
| `AccessRecommendation` | `string?` | New access posture (for `AccessRecommendationChanged`) |
| `Metadata` | `IReadOnlyDictionary<string,string>?` | Open-ended key/value extension |

### 5.3 Integration points identified for future activation

The following host services are the natural injection points for `ICommerceLifecycleNotifier.NotifyAsync()` calls in later phases:

| Service | Method | Event to emit |
|---------|--------|---------------|
| Tenant | `TenantAdminService.CreateTenantAsync` | `TenantCreated` |
| Tenant | `TenantService.DeactivateAsync` | `TenantSuspended` / `TenantClosed` |
| Identity | `ProductProvisioningService.EnableProductAsync` | `ProductEnabled` |
| Identity | `ProductProvisioningService.DisableProductAsync` | `ProductDisabled` |

No changes were made to these services in ECO-01. The interface + noop are wired and ready; activation is a one-line swap per service.

---

## 6. Commerce Ecosystem Event Conventions

### 6.1 `CommerceEventTypes` constants (`shared/contracts/Contracts/Commerce/`)

34 canonical event type strings organized by domain:

| Domain | Examples |
|--------|---------|
| Tenant lifecycle | `commerce.tenant.created`, `commerce.tenant.suspended`, `commerce.tenant.closed` |
| Product / entitlement | `commerce.product.enabled`, `commerce.entitlement.granted`, `commerce.entitlement.revoked` |
| Subscription | `commerce.subscription.activated`, `commerce.subscription.trial.started/expired` |
| Billing standing | `commerce.billing.standing.changed`, `commerce.billing.gracePeriod.started/expired` |
| Access recommendation | `commerce.access.recommendation.changed` |
| Provisioning hooks | `commerce.provisioning.requested`, `commerce.deprovisioning.requested` |
| Integration health | `commerce.integration.entitlement.check.succeeded/failed/skipped` |

### 6.2 `CommerceTelemetryContract` (`shared/contracts/Contracts/Commerce/`)

A standardised health telemetry record that services can include in their `GET /health` or `GET /api/v1/ready` responses:

```json
{
  "commerce": {
    "serviceName": "Synq Liens",
    "hostPlatformKey": "legalsynq",
    "commerceEnabled": false,
    "entitlementClientWired": false,
    "lifecycleNotifierWired": false,
    "entitlementStatus": "disabled",
    "reportedAtUtc": "2026-05-15T00:00:00Z"
  }
}
```

Use `CommerceEntitlementStatusValues` string constants: `ok`, `stale`, `error`, `not_checked`, `disabled`.

---

## 7. Commerce-Aware Service Registration

### 7.1 `CommerceServiceMetadata`

Declarative per-service metadata record (DI singleton) that the Monitoring layer and Control Center read to map Commerce coverage across the platform:

```csharp
new CommerceServiceMetadata(
    ServiceName:              "Synq Liens",
    ProductKey:               "SYNQLIEN",
    PrimaryFeatureKey:        null,
    SubscriptionRequired:     true,
    MonetizationEnabled:      false,   // false = not enforcing yet
    CommerceIntegrationActive: false); // false = noop client wired
```

### 7.2 `ICommerceAwareService`

Optional marker interface for service classes that want to self-describe their Commerce participation:

```csharp
public sealed class LienService : ICommerceAwareService
{
    private static readonly CommerceServiceMetadata _meta =
        new("Synq Liens", "SYNQLIEN", null, true, false, false);

    public CommerceServiceMetadata CommerceMetadata => _meta;
}
```

### 7.3 Registration helpers

```csharp
// Full control
services.AddCommerceIntegration(configuration);
services.AddCommerceServiceMetadata(new CommerceServiceMetadata(...));

// Convenience (resolves CommerceIntegrationActive from config automatically)
services.AddCommerceServiceMetadata(configuration, "Synq Liens", "SYNQLIEN",
    primaryFeatureKey: null,
    subscriptionRequired: true,
    monetizationEnabled: false);
```

---

## 8. Monitoring / Audit / Notification Hooks

### 8.1 Audit (`shared/audit-client/LegalSynq.AuditClient/Enums/CommerceAuditEventTypes.cs`)

19 canonical audit event type constants covering:
- Billing account lifecycle (created/updated/suspended/closed/reopened)
- Subscription lifecycle (activated/suspended/cancelled/renewed/trial)
- Entitlement changes (granted/revoked/changed)
- Billing standing (changed/grace started/grace expired)
- Access recommendation changes
- Provisioning hook lifecycle (dispatched/delivered/failed)
- Integration health events (check succeeded/failed/skipped)

Use with `EventCategory.Business` for subscription/entitlement events, `EventCategory.Integration` for provisioning hooks, `EventCategory.Administrative` for account lifecycle.

### 8.2 Notifications (`shared/contracts/Contracts/Notifications/`)

**11 new template keys** added to `NotificationTemplateKeys.cs` across three categories:
- Billing standing: grace started, grace expired, account suspended
- Subscription: activated, renewed, cancelled, trial expiring
- Entitlement: granted, revoked, access downgraded

**11 disabled platform-default templates** added to `NotificationTemplateRegistry.PlatformDefaults()`. All have `Enabled = false`. Activation is per-tenant override once Commerce integration is wired. Template subjects and bodies are intentionally generic; per-product phrasing lives in tenant template overrides.

### 8.3 Monitoring

The `MonitoringEntityBootstrap` already registers `Commerce` (port 5030) and `Tenant Billing` (port 5031) as monitored entities (LS-COMMERCE-INT-03). The `CommerceTelemetryContract` record (`shared/contracts`) provides the standardised shape for per-service Commerce health data surfaced at those service health endpoints.

---

## 9. Cross-Service Operational Validation

### 9.1 Build validation

`shared/contracts`, `shared/building-blocks`, and `shared/audit-client` are pure libraries. Build correctness is confirmed by the `dotnet build` check run after implementation. No breaking changes were introduced to existing interfaces or types.

### 9.2 Adoption checklist for each product service

When a product service (Liens, Fund, CareConnect, etc.) is ready to adopt Commerce entitlement enforcement:

- [ ] Add `"CommerceIntegration": { "Enabled": true, "BaseUrl": "...", "HostPlatformKey": "legalsynq" }` to `appsettings.json`
- [ ] Call `services.AddCommerceIntegration(configuration)` in `Program.cs`
- [ ] Call `services.AddCommerceServiceMetadata(...)` to register service monetization metadata
- [ ] Inject `ICommerceEntitlementClient` into the relevant service/handler
- [ ] Implement permissive fallback when `!result.IsAvailable` (never block on Commerce)
- [ ] Set `MonetizationEnabled = true` in metadata once enforcement is live
- [ ] Add `CommerceTelemetryContract` to the service's readiness response

### 9.3 No-risk properties

| Property | Guarantee |
|----------|-----------|
| Standalone operation | All services still start and operate with `CommerceIntegration:Enabled = false` |
| No DB merges | Zero shared DbContexts or cross-service EF access |
| No Commerce dependency at startup | Noop implementations have no external deps |
| Non-blocking | `ICommerceEntitlementClient` never throws; `ICommerceLifecycleNotifier` never throws |
| Additive contracts | `NotificationTemplateKeys` additions are additive; `NotificationTemplateRegistry` additions use `Enabled = false` |
| Audit extension | `CommerceAuditEventTypes` is a static class; no modification to existing `EventCategory` enum |

---

## 10. File Index

### New files

| Path | Description |
|------|-------------|
| `shared/contracts/Contracts/Commerce/CommerceEventTypes.cs` | 34 canonical event type strings |
| `shared/contracts/Contracts/Commerce/CommerceLifecycleEvent.cs` | Lifecycle event envelope + `CommerceAccessRecommendations` |
| `shared/contracts/Contracts/Commerce/CommerceTelemetryContract.cs` | Monitoring telemetry contract + `CommerceEntitlementStatusValues` |
| `shared/building-blocks/BuildingBlocks/Commerce/CommerceEntitlementResult.cs` | Entitlement result record + helpers |
| `shared/building-blocks/BuildingBlocks/Commerce/CommerceServiceMetadata.cs` | Per-service monetization metadata |
| `shared/building-blocks/BuildingBlocks/Commerce/ICommerceAwareService.cs` | Optional marker interface |
| `shared/building-blocks/BuildingBlocks/Commerce/ICommerceEntitlementClient.cs` | Entitlement resolution contract |
| `shared/building-blocks/BuildingBlocks/Commerce/ICommerceLifecycleNotifier.cs` | Host → Commerce lifecycle notification contract |
| `shared/building-blocks/BuildingBlocks/Commerce/CommerceIntegrationOptions.cs` | Integration configuration model |
| `shared/building-blocks/BuildingBlocks/Commerce/HttpCommerceEntitlementClient.cs` | HTTP implementation (wired when `Enabled=true`) |
| `shared/building-blocks/BuildingBlocks/Commerce/NoopCommerceEntitlementClient.cs` | Noop fallback (default) |
| `shared/building-blocks/BuildingBlocks/Commerce/NoopCommerceLifecycleNotifier.cs` | Noop fallback (always) |
| `shared/building-blocks/BuildingBlocks/Commerce/CommerceIntegrationServiceCollectionExtensions.cs` | DI registration helpers |
| `shared/audit-client/LegalSynq.AuditClient/Enums/CommerceAuditEventTypes.cs` | 19 Commerce audit event type strings |

### Modified files

| Path | Change |
|------|--------|
| `shared/building-blocks/BuildingBlocks/BuildingBlocks.csproj` | Added `ProjectReference` to `shared/contracts` |
| `shared/contracts/Contracts/Notifications/NotificationTemplateKeys.cs` | Added 11 Commerce notification template key constants |
| `shared/contracts/Contracts/Notifications/NotificationTemplateRegistry.cs` | Added 11 disabled Commerce platform-default templates |
