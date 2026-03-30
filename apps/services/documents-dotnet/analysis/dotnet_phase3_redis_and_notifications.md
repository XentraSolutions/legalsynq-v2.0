# Phase 3 — Redis HA Readiness + Scan Completion Notifications

## 1. Objective

Phase 3 hardens the Documents service in two areas that remained open after Phase 2:

1. **Redis HA / operational readiness** — make the Redis dependency explicitly visible, measurable, and safe-to-fail across all configured use-cases (scan queue, access-token store, notification publisher).
2. **Scan completion notification model** — introduce a structured, extensible event emitted on every terminal scan outcome (CLEAN, INFECTED, FAILED), with a clean abstraction and two concrete implementations.

Both improvements are additive: no existing APIs, scan gating, retry behaviour, or circuit-breaker logic were changed.

---

## 2. Redis Readiness Strategy

### A. Current Redis usage surface

| Use-case | Config key | Provider type |
|---|---|---|
| Scan job queue | `ScanWorker:QueueProvider=redis` | Redis Streams (XADD / XREADGROUP / XACK) |
| Access token store | `AccessToken:Store=redis` | Redis String (GET / SET / Lua CAS) |
| Scan completion notifications | `Notifications:ScanCompletion:Provider=redis` | Redis Pub/Sub |

All three share a single `IConnectionMultiplexer` singleton registered by the first active Redis user.

### B. Health check

**New:** `Documents.Infrastructure/Health/RedisHealthCheck.cs`

- Performs `db.PingAsync()` — a lightweight round-trip that confirms the connection is alive.
- Sets `docs_redis_healthy` Gauge to 1 on success, 0 on failure.
- Increments `docs_redis_connection_failures_total` on failure.
- Tagged `"ready"` — appears in `/health/ready` only (not in the liveness `/health` endpoint).
- Registered **conditionally**: only added to the health pipeline when `IConnectionMultiplexer` is registered in the DI container (i.e., when any Redis-backed feature is active).

**Health endpoint behaviour:**

| Redis mode | `/health` | `/health/ready` |
|---|---|---|
| Redis not configured | `redis` check absent | `redis` check absent |
| Redis configured + reachable | process + DB only | includes `redis` → Healthy |
| Redis configured + unreachable | process + DB only | includes `redis` → Unhealthy (503) |

### C. Startup validation

Existing rules preserved and extended:

| Condition | Behaviour |
|---|---|
| `QueueProvider=redis` + `Redis:Url` missing | Hard fail (throws) at startup |
| `AccessToken:Store=redis` + `Redis:Url` missing | Hard fail (throws) at startup |
| `Notifications:ScanCompletion:Provider=redis` + no Redis connection | **Warning** logged; falls back to `log` publisher (non-fatal) |

The notification fallback is intentionally soft: pushing notifications to a Redis channel is a delivery convenience, not a security gate.

### D. Metrics added (Redis)

Defined in `Documents.Infrastructure/Observability/RedisMetrics.cs`:

| Metric | Type | Description |
|---|---|---|
| `docs_redis_healthy` | Gauge | 1 if Redis PING succeeded, 0 otherwise |
| `docs_redis_connection_failures_total` | Counter | Total Redis connection/command failures |
| `docs_redis_stream_reclaims_total` | Counter | Jobs reclaimed from crashed consumers via XAUTOCLAIM |

`RedisStreamReclaims` is incremented inside `RedisScanJobQueue.DequeueAsync` when XAUTOCLAIM returns entries — high values indicate workers are crashing before ACKing messages.

`RedisConnectionFailures` is incremented on XADD and XREADGROUP errors in `RedisScanJobQueue`, and on `RedisHealthCheck` failures.

### E. Production HA configuration guidance

The `IConnectionMultiplexer` is created via `ConnectionMultiplexer.Connect(redisUrl)`. StackExchange.Redis supports all major Redis topologies through the connection string.

#### Standalone (dev / single-node)
```json
"Redis": { "Url": "redis://localhost:6379" }
```

#### Password-authenticated
```json
"Redis": { "Url": "redis://:mypassword@redis-host:6379" }
```

Or use the full config-string form:
```
redis-host:6379,password=mypassword,ssl=false,abortConnect=false
```

#### Redis Sentinel (HA failover)
```
sentinel-host1:26379,sentinel-host2:26379,serviceName=mymaster,abortConnect=false
```

#### Redis Cluster (sharding)
```
node1:6379,node2:6379,node3:6379,abortConnect=false
```

#### TLS (e.g. Azure Cache for Redis)
```
my-cache.redis.cache.windows.net:6380,ssl=true,password=<key>,abortConnect=false
```

**Critical production settings:**
- Always set `abortConnect=false` — prevents startup crash if Redis is transiently unavailable.
- Set `connectTimeout` and `syncTimeout` to match your SLA (default 5000ms each).
- Use `reconnectRetryPolicy=ExponentialRetry` for automatic reconnect with backoff.

#### Recommended production config block:
```json
"Redis": {
  "Url": "redis-primary:6379,redis-replica:6379,password=<secret>,abortConnect=false,connectTimeout=5000,syncTimeout=5000,allowAdmin=false"
}
```

#### Limitations
- The current implementation uses a single `IConnectionMultiplexer`. Sentinel and Cluster are supported by StackExchange.Redis but not integration-tested in this service.
- Redis Streams (scan queue) do not replicate within a single-node setup. Use Redis Cluster or Sentinel primary + replica for HA durability.
- Pub/Sub notifications (`RedisScanCompletionPublisher`) are ephemeral — messages are lost if no subscriber is connected at publish time. For guaranteed delivery, extend to Redis Streams (see §6 Limitations).

---

## 3. Scan Completion Notification Strategy

### Architecture

```
DocumentScanWorker
    │
    ├── CLEAN / INFECTED / FAILED outcome
    │
    └── PublishCompletionEventAsync(job, status, ...)
            │
            └── IScanCompletionPublisher.PublishAsync(DocumentScanCompletedEvent)
                        │
                        ├── LogScanCompletionPublisher    (dev / default)
                        ├── RedisScanCompletionPublisher  (production with Redis)
                        └── NullScanCompletionPublisher   (disabled)
```

### Event emitted at three terminal points

| Worker code path | Status emitted |
|---|---|
| `ProcessJobAsync` — max retry exceeded check (top) | FAILED |
| `ProcessJobAsync` — after normal scan + ACK (bottom) | CLEAN / INFECTED / FAILED |
| `RetryOrFailAsync` — after exceeding retry limit | FAILED |

Events are **never** emitted on intermediate retry attempts — only on irreversible terminal outcomes.

### A. Event contract

`Documents.Domain/Events/DocumentScanCompletedEvent.cs`

```csharp
public sealed class DocumentScanCompletedEvent
{
    public Guid       EventId       { get; init; }  // Guid.NewGuid() per event
    public string     ServiceName   { get; init; }  // "documents-dotnet"
    public Guid       DocumentId    { get; init; }
    public Guid       TenantId      { get; init; }
    public Guid?      VersionId     { get; init; }
    public ScanStatus ScanStatus    { get; init; }  // Clean | Infected | Failed
    public DateTime   OccurredAt    { get; init; }
    public string?    CorrelationId { get; init; }
    public int        AttemptCount  { get; init; }
    public string?    EngineVersion { get; init; }
    public string?    FileName      { get; init; }
}
```

**Security:** The payload contains identifiers and status only. No file contents, no PII beyond what is already in the document metadata. Tenant isolation is preserved by including `TenantId` — consumers must enforce their own tenant filtering.

### B. Publisher abstraction

`Documents.Domain/Interfaces/IScanCompletionPublisher.cs`

```csharp
public interface IScanCompletionPublisher
{
    ValueTask PublishAsync(DocumentScanCompletedEvent evt, CancellationToken ct = default);
}
```

Placed in Domain so Application layer services (if needed in future) can reference it without taking a dependency on Infrastructure.

### C. Concrete implementations

| Class | Provider key | Description |
|---|---|---|
| `LogScanCompletionPublisher` | `"log"` | Structured `ILogger` Information message. Zero external dependencies. |
| `RedisScanCompletionPublisher` | `"redis"` | JSON payload published to Redis Pub/Sub channel. |
| `NullScanCompletionPublisher` | `"none"` | Silently discards all events. |

### D. Delivery guarantees

| Provider | Guarantee | Notes |
|---|---|---|
| `log` | Best-effort, at-most-once | Depends on logger sink flushing; survives in-process |
| `redis` | Best-effort, at-most-once | Redis Pub/Sub only reaches currently-connected subscribers |
| `none` | No delivery | Intentional discard |

**Reliability rule:** All publisher implementations are non-throwing by contract. All exceptions are caught internally, increments `docs_scan_completion_delivery_failures_total`, and logs at Warning. The scan pipeline (`DocumentScanWorker`) additionally wraps `PublishCompletionEventAsync` in a try/catch as a belt-and-suspenders guard.

**Scan state persistence is always primary.** The ACK to the queue happens before the publish attempt. If the service crashes after ACK but before publish, the event is lost — acceptable under at-most-once semantics for the current notification model.

---

## 4. Configuration Added

### Notifications section (new)
```json
"Notifications": {
  "ScanCompletion": {
    "Provider": "log",
    "Redis": {
      "Channel": "documents.scan.completed"
    }
  }
}
```

**Provider values:**
- `"log"` — default; safe for all environments
- `"redis"` — requires `Redis:Url` to be set and a Redis-backed feature to be active
- `"none"` — disables notifications

### Redis section (unchanged; documented for HA)
```json
"Redis": {
  "Url": ""
}
```

Supports StackExchange.Redis connection string format — standalone, Sentinel, Cluster, and TLS.

---

## 5. Files Changed

### New files

| File | Purpose |
|---|---|
| `Documents.Domain/Events/DocumentScanCompletedEvent.cs` | Event contract (Domain layer) |
| `Documents.Domain/Interfaces/IScanCompletionPublisher.cs` | Publisher abstraction (Domain layer) |
| `Documents.Infrastructure/Health/RedisHealthCheck.cs` | Redis PING health check |
| `Documents.Infrastructure/Observability/RedisMetrics.cs` | Redis + notification Prometheus metrics |
| `Documents.Infrastructure/Notifications/NotificationOptions.cs` | Config POCOs for Notifications section |
| `Documents.Infrastructure/Notifications/NullScanCompletionPublisher.cs` | No-op publisher |
| `Documents.Infrastructure/Notifications/LogScanCompletionPublisher.cs` | Structured log publisher |
| `Documents.Infrastructure/Notifications/RedisScanCompletionPublisher.cs` | Redis Pub/Sub publisher |

### Modified files

| File | Change |
|---|---|
| `Documents.Infrastructure/Scanner/RedisScanJobQueue.cs` | Added `RedisStreamReclaims.Inc()` on XAUTOCLAIM hits; `RedisConnectionFailures.Inc()` on XADD/XREADGROUP errors |
| `Documents.Infrastructure/DependencyInjection.cs` | Redis health check conditional registration; notification options + IScanCompletionPublisher factory; startup validation warning for redis publisher without active Redis |
| `Documents.Api/Background/DocumentScanWorker.cs` | Added `IScanCompletionPublisher _publisher` constructor parameter; `PublishCompletionEventAsync` helper; emissions at all three terminal outcome paths |
| `Documents.Api/appsettings.json` | Added `Notifications` section |

---

## 6. Metrics / Health Changes

### New Prometheus metrics

| Metric | Type | Labels | Description |
|---|---|---|---|
| `docs_redis_healthy` | Gauge | — | 1 if Redis PING passes |
| `docs_redis_connection_failures_total` | Counter | — | Redis command/connection failures |
| `docs_redis_stream_reclaims_total` | Counter | — | Jobs reclaimed via XAUTOCLAIM |
| `docs_scan_completion_events_emitted_total` | Counter | `status` | Events emitted (label: `clean`/`infected`/`failed`) |
| `docs_scan_completion_delivery_success_total` | Counter | — | Events successfully delivered |
| `docs_scan_completion_delivery_failures_total` | Counter | — | Events that failed to deliver |

### Health endpoint changes

`/health/ready` gains the `redis` entry when Redis is active:

```json
{
  "status": "healthy",
  "checks": [
    { "name": "database",          "status": "healthy" },
    { "name": "clamav",            "status": "healthy" },
    { "name": "clamav-signatures", "status": "healthy" },
    { "name": "redis",             "status": "healthy" }
  ]
}
```

`/health` (liveness) is unchanged — Redis is not a liveness concern.

---

## 7. Delivery Guarantees Summary

| Component | Guarantee | Durability |
|---|---|---|
| Scan job queue (in-memory) | At-most-once; data lost on restart | None |
| Scan job queue (Redis Streams) | At-least-once; persists restarts | Redis persistence config |
| Access token store (Redis) | At-least-once with Lua CAS | Redis persistence config |
| Scan completion (log) | Best-effort at-most-once | Logger sink dependent |
| Scan completion (Redis Pub/Sub) | Best-effort at-most-once | No persistence; ephemeral |

---

## 8. Verification Steps

### Redis health check

1. Start service with `ScanWorker:QueueProvider=redis` and a valid `Redis:Url`
2. `GET /health/ready` → should show `"redis": "healthy"` with `Healthy` status
3. Kill Redis → health endpoint should return 503 with `"redis": "unhealthy"`
4. `GET /metrics` → `docs_redis_healthy` should drop to 0; `docs_redis_connection_failures_total` should increment

### Scan completion events (log provider)

1. Upload a document with `Scanner:Provider=mock`
2. Worker processes job → check logs for `DocumentScanCompleted:` structured message
3. `GET /metrics` → `docs_scan_completion_events_emitted_total{status="clean"}` should be 1
4. `docs_scan_completion_delivery_success_total` should be 1

### Scan completion events (Redis Pub/Sub)

1. Set `Notifications:ScanCompletion:Provider=redis`
2. Subscribe to channel: `redis-cli SUBSCRIBE documents.scan.completed`
3. Upload a document → after worker processes it, message should appear in redis-cli
4. Message should be a camelCase JSON object matching `DocumentScanCompletedEvent` schema

### Stream reclaim metrics

1. Set `ScanWorker:QueueProvider=redis`
2. Enqueue a job, kill the worker before it ACKs
3. After `ClaimStaleJobsAfterSeconds` (default 300s), restart the worker
4. Check `docs_redis_stream_reclaims_total` increments

### Notification fallback warning

1. Set `Notifications:ScanCompletion:Provider=redis` with `ScanWorker:QueueProvider=memory`
2. Start service → warning logged: "Notifications:ScanCompletion:Provider=redis but no Redis connection..."
3. Events are delivered via `log` publisher instead

---

## 9. Limitations and Follow-up Recommendations

| Item | Description |
|---|---|
| **Pub/Sub at-most-once** | Redis Pub/Sub drops messages if no subscriber is connected. For at-least-once notification delivery, implement `RedisScanCompletionStreamPublisher` using `XADD` to a separate completion stream (consumer groups can provide replay capability). |
| **No persistence for log events** | The log publisher emits to the application log only. If the log sink is async and the process is killed, the last buffered messages may be lost. Use `flushOnShutdown=true` in your Serilog sink config. |
| **Correlation ID not populated** | `DocumentScanCompletedEvent.CorrelationId` is currently null. Thread the correlation ID from the HTTP upload request through `ScanJob` to enable end-to-end tracing for scan events. |
| **Single multiplexer for all Redis uses** | If the scan queue and token store use Redis but notifications need a different Redis endpoint, a secondary multiplexer registration is required. Currently all uses share one `IConnectionMultiplexer`. |
| **Sentinel / Cluster integration testing** | StackExchange.Redis supports both topologies via connection string, but no integration test coverage exists in this service. Verify failover behaviour in staging before production promotion. |
| **Webhook delivery** | A `WebhookScanCompletionPublisher` (HTTP POST to configured URLs per tenant) is a natural next step. The `IScanCompletionPublisher` abstraction and `NullScanCompletionPublisher` make adding this a pure extension — no existing code changes required. |
