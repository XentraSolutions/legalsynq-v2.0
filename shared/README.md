# Shared Libraries

Three shared C# library projects consumed by all platform services. All are purely additive — no service is required to use any feature it does not opt into.

## Projects

### `shared/contracts` — `Contracts.csproj`

Shared DTOs and constants with zero dependencies.

- `Contracts/Audit/` — `AuditEventDto`, `AuditQueryRequest`, `AuditExportRequest`
- `Contracts/Commerce/` — `CommerceEventTypes` (34 constants), `CommerceLifecycleEvent`, `CommerceTelemetryContract`
- `Contracts/Notifications/` — `NotificationTemplateKeys` (all template key constants), `NotificationTemplateRegistry` (platform defaults including 11 Commerce templates)
- `HealthResponse`, `InfoResponse`, `ServiceResponse<T>` — standard health/info shapes used by all services

### `shared/building-blocks` — `BuildingBlocks.csproj`

Shared middleware, auth, context helpers, and Commerce abstractions. References `shared/contracts`.

Key namespaces:

| Namespace | Contents |
|---|---|
| `BuildingBlocks.Authentication` | JWT validation helpers, `ServiceTokenIssuer`, `NotificationsAuthDelegatingHandler` |
| `BuildingBlocks.Authorization` | `Policies`, `Roles`, `PermissionService`, `ScopedAuthorizationService`, policy evaluation pipeline |
| `BuildingBlocks.Context` | `ICurrentRequestContext`, `CurrentRequestContext` — tenant/user/correlation ID from JWT |
| `BuildingBlocks.Commerce` | `ICommerceLifecycleNotifier`, `NoopCommerceLifecycleNotifier`, `ICommerceEntitlementClient`, `HttpCommerceEntitlementClient`, `NoopCommerceEntitlementClient`, `CommerceEntitlementResult`, `CommerceIntegrationOptions`, `AddCommerceIntegration()` DI helper |
| `BuildingBlocks.Exceptions` | `NotFoundException`, `ConflictException`, `ValidationException`, `ForbiddenException` |
| `BuildingBlocks.Notifications` | `INotificationsEmailClient`, `INotificationsCacheClient`, `NotificationsServiceOptions` |
| `BuildingBlocks.Diagnostics` | `MigrationCoverageProbe`, `RuntimeConfigValidator` |

### `shared/audit-client` — `LegalSynq.AuditClient.csproj`

Typed client for publishing audit events to the Audit service.

- `IAuditEventClient` — `IngestAsync(IngestAuditEventRequest)`
- `AddAuditEventClient(configuration)` — registers the HTTP client; reads `AuditClient:BaseUrl` + `AuditClient:ServiceToken`
- `Enums/CommerceAuditEventTypes.cs` — 19 Commerce-specific audit event type strings

## Commerce Integration Pattern

Any service can adopt Commerce notifications in three steps:

```csharp
// 1. In DI setup (Program.cs or Infrastructure/DependencyInjection.cs)
services.AddCommerceIntegration(configuration);

// 2. In appsettings.json
// "CommerceIntegration": { "Enabled": false, "BaseUrl": "http://127.0.0.1:5030", "HostPlatformKey": "legalsynq" }

// 3. Inject ICommerceLifecycleNotifier and call it after primary operations
await _commerceNotifier.NotifyAsync(new CommerceLifecycleEvent(
    EventType: CommerceEventTypes.TenantCreated,
    HostPlatformKey: "legalsynq",
    ExternalTenantId: tenantId.ToString(),
    OccurredAtUtc: DateTimeOffset.UtcNow), ct);
```

`Enabled: false` (the default) registers a noop that returns `Task.CompletedTask`. No network I/O, no startup dependency on Commerce.

## Build

```bash
dotnet build shared/contracts/Contracts/Contracts.csproj
dotnet build shared/building-blocks/BuildingBlocks/BuildingBlocks.csproj
dotnet build shared/audit-client/LegalSynq.AuditClient/LegalSynq.AuditClient.csproj
```

All three build at 0 errors / 0 warnings.
