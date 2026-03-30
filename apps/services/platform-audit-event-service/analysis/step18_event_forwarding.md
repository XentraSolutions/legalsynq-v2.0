# Step 18 — Event Forwarding Abstraction

**Service**: Platform Audit Event Service  
**Date**: 2026-03-30  
**Status**: Complete  
**Build**: 0 errors, 0 warnings

---

## Objective

Add a lightweight, non-breaking event forwarding abstraction to the audit ingest pipeline so that downstream systems (notification services, compliance dashboards, data pipelines) can consume selected audit events in near-real-time without polling the audit store.

**Hard constraints**:
- Persistence must remain the primary responsibility.
- Forwarding failure must never cause an ingest request to fail.
- Append-only behavior must not be compromised.
- No full broker integration required for v1.

---

## Abstractions Created

### `IAuditEventForwarder`
**File**: `Services/Forwarding/IAuditEventForwarder.cs`

The ingest-pipeline-facing interface. Called by `AuditEventIngestionService` once per successfully persisted record. Single method:

```csharp
ValueTask ForwardAsync(AuditEventRecord record, CancellationToken ct = default);
```

Responsibilities delegated to implementors:
- Check `EventForwarding:Enabled`
- Apply category / event-type / severity / replay filters
- Map `AuditEventRecord` → `AuditRecordIntegrationEvent` (no hashes)
- Wrap in `IntegrationEvent<T>` envelope
- Call `IIntegrationEventPublisher`

### `IIntegrationEventPublisher`
**File**: `Services/Forwarding/IIntegrationEventPublisher.cs`

The broker-facing interface. Knows nothing about the audit domain.

```csharp
string BrokerName { get; }
ValueTask PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken ct = default);
```

Separating these two interfaces means:
- Broker changes require only a new `IIntegrationEventPublisher` + Program.cs update.
- Domain filtering / mapping changes require only a new `IAuditEventForwarder`.
- Neither interface leaks into the other's concern.

### `IntegrationEvent<TPayload>` — Envelope
**File**: `Services/Forwarding/IntegrationEvent.cs`

Generic broker envelope carrying routing metadata (`EventId`, `EventType`, `SchemaVersion`, `PublishedAtUtc`, `CorrelationId`, `SourceService`) plus a strongly-typed domain payload.

### `AuditRecordIntegrationEvent` — Payload Contract
**File**: `Services/Forwarding/AuditRecordIntegrationEvent.cs`

Flat, JSON-serialisable downstream contract. Includes:
- `AuditId`, `EventType`, `EventCategory`, `Severity`, `SourceSystem`
- `TenantId`, `OrganizationId`, `ActorId`, `ActorType`
- `EntityType`, `EntityId`, `Action`
- `OccurredAtUtc`, `RecordedAtUtc`, `CorrelationId`, `IsReplay`

**Deliberately excluded**: `Hash`, `PreviousHash`, `BeforeJson`, `AfterJson`, `Tags`.

### `EventForwardingOptions`
**File**: `Configuration/EventForwardingOptions.cs`

Configuration POCO bound from `appsettings.json` section `EventForwarding`. Key options:
- `Enabled` (bool, default false) — master switch
- `BrokerType` (string, default "NoOp") — publisher selection
- `ForwardCategories` (list, default empty = all)
- `ForwardEventTypePrefixes` (list, default empty = all)
- `MinSeverity` (string, default "Info")
- `ForwardReplayRecords` (bool, default false)
- `SubjectPrefix` (string, default "legalsynq.audit.")
- `ConnectionString`, `TopicOrExchangeName` — future broker config

---

## No-Op Implementations

### `NoOpAuditEventForwarder`
**File**: `Services/Forwarding/NoOpAuditEventForwarder.cs`

- `Enabled = false` → returns `ValueTask.CompletedTask` immediately (zero overhead per ingest call).
- `Enabled = true` → exercises the full filter/map/publish pipeline; delegates to `IIntegrationEventPublisher`.
- Logs at `Debug` for each skip decision and each forward attempt.
- **DI lifetime**: Singleton.

### `NoOpIntegrationEventPublisher`
**File**: `Services/Forwarding/NoOpIntegrationEventPublisher.cs`

- Logs "would publish…" at `Debug` level with all envelope fields.
- Sends nothing. No outbound connections.
- `BrokerName` = `"NoOp"`.
- **DI lifetime**: Singleton.

---

## Integration Points

### Primary integration point — `AuditEventIngestionService.IngestOneAsync`

The forwarding call is placed at **Step 7**, immediately after `AppendAsync` returns successfully and the success log is written. Location: `Services/AuditEventIngestionService.cs`, inside the outer `try` block.

```
Step 1: Idempotency check
Step 2: AuditId + RecordedAtUtc
Step 3: PreviousHash chain lookup
Step 4: Hash computation
Step 5: Entity construction  (AuditEventRecordMapper.ToEntity)
Step 6: AppendAsync()         ← record durably persisted ← primary responsibility
Step 7: IAuditEventForwarder.ForwardAsync(persisted)  ← NEW — post-persist, best-effort
        (inner try-catch: failure → LogWarning, never re-throws)
Return: IngestItemResult { Accepted = true }
```

The inner try-catch pattern:
```csharp
try   { await _forwarder.ForwardAsync(persisted, ct); }
catch (Exception fwdEx)
{
    _logger.LogWarning(fwdEx,
        "Event forwarding failed (non-fatal): AuditId={AuditId} ...");
}
```

The outer try-catch that handles `DbUpdateException` (persistence errors) is not affected.

### Constructor injection

```csharp
public AuditEventIngestionService(
    IAuditEventRecordRepository         records,
    IOptions<IntegrityOptions>          integrityOptions,
    IAuditEventForwarder                forwarder,   // ← new
    ILogger<AuditEventIngestionService> logger)
```

`AuditEventIngestionService` is **Scoped**; `IAuditEventForwarder` is **Singleton**. Scoped → Singleton injection is safe.

### DI Registration (Program.cs)

```csharp
builder.Services.Configure<EventForwardingOptions>(
    cfg.GetSection(EventForwardingOptions.SectionName));

builder.Services.AddSingleton<IIntegrationEventPublisher, NoOpIntegrationEventPublisher>();
builder.Services.AddSingleton<IAuditEventForwarder, NoOpAuditEventForwarder>();
```

Startup log:
- `Enabled = false` → `[WRN] EventForwarding:Enabled = false — ...`
- `Enabled = true` → `[INF] EventForwarding: enabled. BrokerType=... MinSeverity=...`

---

## Files Modified

| File | Change |
|---|---|
| `Services/AuditEventIngestionService.cs` | Added `IAuditEventForwarder` field + constructor param; added Step 7 inner try-catch forwarding call |
| `Program.cs` | Added `using Forwarding;`, registered `EventForwardingOptions`, `IIntegrationEventPublisher`, `IAuditEventForwarder`, startup log |
| `appsettings.json` | Added `EventForwarding` section (all defaults) |
| `appsettings.Development.json` | Added `EventForwarding` section (dev overrides) |

---

## Files Created

| File | Purpose |
|---|---|
| `Configuration/EventForwardingOptions.cs` | Configuration POCO |
| `Services/Forwarding/IAuditEventForwarder.cs` | Ingest-facing interface |
| `Services/Forwarding/IIntegrationEventPublisher.cs` | Broker-facing interface |
| `Services/Forwarding/IntegrationEvent.cs` | Generic envelope |
| `Services/Forwarding/AuditRecordIntegrationEvent.cs` | Downstream payload contract |
| `Services/Forwarding/NoOpAuditEventForwarder.cs` | v1 domain forwarder |
| `Services/Forwarding/NoOpIntegrationEventPublisher.cs` | v1 broker publisher |
| `Docs/event-forwarding-model.md` | Architecture reference |
| `analysis/step18_event_forwarding.md` | This report |

---

## Future Broker Compatibility Notes

### Broker swap (no domain changes required)

To swap to a real broker, implement `IIntegrationEventPublisher` and change one registration in Program.cs:

```csharp
// Current:
builder.Services.AddSingleton<IIntegrationEventPublisher, NoOpIntegrationEventPublisher>();

// With RabbitMQ:
builder.Services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();
```

`IAuditEventForwarder`, `AuditEventIngestionService`, and all filters remain unchanged.

### Supported broker patterns

| `BrokerType` | Implementation strategy |
|---|---|
| `NoOp` | `NoOpIntegrationEventPublisher` (current) |
| `InMemory` | `Channel<IntegrationEvent<T>>` + `IHostedService` consumer — zero external deps, same process |
| `RabbitMq` | AMQP exchange publish via `RabbitMQ.Client` |
| `AzureServiceBus` | Topic publish via `Azure.Messaging.ServiceBus` |
| `AwsSns` | SNS topic publish via `AWSSDK.SimpleNotificationService` |
| `Kafka` | Topic produce via `Confluent.Kafka` |

### At-least-once delivery (transactional outbox)

Current delivery is at-most-once (fire-and-forget). For guaranteed at-least-once delivery:

1. In `IAuditEventForwarder.ForwardAsync`, write the serialised `IntegrationEvent` to an `OutboxEvents` database table inside a transaction scoped with `AppendAsync`. This requires the forwarder to have access to the EF `DbContext`.
2. A relay `IHostedService` polls `OutboxEvents`, publishes to the broker, marks rows as delivered.
3. A cleanup job prunes acknowledged rows after a retention window.

This approach doesn't change the `IAuditEventForwarder` or `IIntegrationEventPublisher` interfaces — it's an implementation detail of a future `OutboxAuditEventForwarder`.

### Content-based routing

For multi-topic scenarios (e.g., `Security` events → `audit.security`, `Compliance` events → `audit.compliance`), inject a routing strategy into the forwarder:

```csharp
public interface IForwardingRouter
{
    string ResolveEventType(AuditRecordIntegrationEvent payload);
}
```

Register per-category routing rules in configuration and resolve in `NoOpAuditEventForwarder` before building the envelope.

### Schema versioning

`IntegrationEvent<T>.SchemaVersion` is currently `"1"`. When `AuditRecordIntegrationEvent` changes in a breaking way:

1. Bump `SchemaVersion` to `"2"`.
2. Consumers subscribe to both versions during a migration window.
3. After all consumers are on v2, stop publishing v1.

Adding new nullable fields to `AuditRecordIntegrationEvent` is always backwards-compatible (no version bump needed).

---

## HIPAA / Compliance Notes

- **PHI exclusion**: `AuditRecordIntegrationEvent` contains no `BeforeJson`/`AfterJson` state snapshots. If those fields ever contain PHI, the integration event contract must remain as-is.
- **Hash exclusion**: `Hash` and `PreviousHash` values are integrity-critical fields that must not appear in messages sent to external brokers. Any future implementation must enforce this exclusion.
- **Broker encryption**: when a real broker is configured, connections must use TLS. Inject credentials via `EventForwarding:ConnectionString` from an environment variable or secrets manager — never commit to appsettings.
- **Tenant isolation**: `TenantId` is forwarded so consumers can apply tenant-level access controls. Multi-tenant broker setups should consider per-tenant topic partitioning.
