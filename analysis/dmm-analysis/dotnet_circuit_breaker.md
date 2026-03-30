# ClamAV Circuit Breaker — Design Document

**Service:** Documents (.NET 8, `apps/services/documents-dotnet`)
**Date:** 2026-03-30
**Phase:** Documents Infrastructure Hardening

---

## Objective

Add a production-grade circuit breaker around `ClamAvFileScannerProvider` to prevent
retry storms during scanner outages, without changing scan-status semantics or exposing
circuit logic outside the infrastructure layer.

---

## Design Decisions

### 1. Decorator Pattern (Infrastructure Layer Only)

The circuit breaker is implemented as a decorator — `CircuitBreakerScannerProvider` — that
wraps the existing `ClamAvFileScannerProvider` and is registered as `IFileScannerProvider`
in the DI container. Controllers, application services, and the scan worker have zero
knowledge of the circuit breaker; they interact only through the unchanged `IFileScannerProvider`
interface.

```
ClamAvFileScannerProvider
        ↓ wrapped by
CircuitBreakerScannerProvider  ← registered as IFileScannerProvider
        ↓ consumed by
ScanService / ScanOrchestrationService (application layer — unchanged)
        ↓ consumed by
Scan Worker (host — unchanged)
```

### 2. Polly Advanced Circuit Breaker (v7.2.4)

Polly's `AdvancedCircuitBreakerAsync` was chosen because it supports a **ratio-based
failure threshold over a rolling sampling window**, which maps directly to the required
configuration shape (`FailureThreshold`, `MinimumThroughput`, `SamplingDurationSeconds`).

The simple `CircuitBreakerAsync` (consecutive-failure counter) was rejected because it does
not respect sampling windows and would be too sensitive on low-traffic services.

### 3. Failure Threshold as Ratio

The config exposes two intuitive integers:
- `FailureThreshold` — how many failures are needed to trip the circuit
- `MinimumThroughput` — the minimum number of calls before the circuit can open

Polly expects a float ratio (0.0–1.0). The ratio is derived as:

```
failureRatio = Clamp(FailureThreshold / MinimumThroughput, 0.01, 1.0)
```

With defaults `5/5 = 1.0` this means "100% failure rate across at least 5 calls within
60 s opens the circuit for 30 s." Operators can relax this (e.g., `FailureThreshold: 3`,
`MinimumThroughput: 5` → 60% threshold).

### 4. Fail-Closed Guarantee

When the circuit is OPEN, `ScanAsync` catches `BrokenCircuitException` and returns
`ScanStatus.Failed` — **never** `ScanStatus.Clean`. This preserves the fail-closed
semantics required by the `RequireCleanScanForAccess` document access policy.

### 5. INFECTED Is Not a Failure

ClamAV returning `INFECTED` is a fully valid, expected outcome. It is parsed into a
`ScanResult { Status = Infected }` by `ClamAvFileScannerProvider` and returned without
throwing. Polly only counts **exceptions** as failures, so INFECTED results never
contribute to the failure counter.

### 6. Health Check Integration

`ClamAvHealthCheck` now resolves `IFileScannerProvider` from DI and casts it to
`CircuitBreakerScannerProvider` (if present). When the circuit is OPEN, the health check
immediately returns `Degraded` without attempting a TCP connection to ClamAV:

| Circuit State | Health Status | Notes |
|---|---|---|
| CLOSED | Healthy / Degraded | Result of TCP PING/PONG |
| HALF-OPEN | Healthy / Degraded | TCP probe runs; result prefixed `[circuit=half-open]` |
| OPEN | Degraded | Immediate — no TCP probe |

---

## Configuration

Binds from `Scanner:ClamAv:CircuitBreaker` in `appsettings.json`:

```json
{
  "Scanner": {
    "Provider": "clamav",
    "ClamAv": {
      "Host": "localhost",
      "Port": 3310,
      "TimeoutMs": 30000,
      "ChunkSizeBytes": 2097152,
      "CircuitBreaker": {
        "FailureThreshold": 5,
        "BreakDurationSeconds": 30,
        "SamplingDurationSeconds": 60,
        "MinimumThroughput": 5
      }
    }
  }
}
```

| Field | Default | Meaning |
|---|---|---|
| `FailureThreshold` | 5 | Failures (within sampling window) needed to open circuit |
| `BreakDurationSeconds` | 30 | Seconds circuit stays OPEN before entering HALF-OPEN |
| `SamplingDurationSeconds` | 60 | Rolling window (seconds) over which failure rate is measured |
| `MinimumThroughput` | 5 | Minimum calls within the window before circuit can trip |

The circuit is **only activated** when `Scanner:Provider` is `clamav`. The `mock` and
`none` providers bypass circuit logic entirely.

---

## Files Changed

| File | Change |
|---|---|
| `Documents.Infrastructure/Scanner/ClamAvFileScannerProvider.cs` | Added `ClamAvCircuitBreakerOptions` class; added `CircuitBreaker` property to `ClamAvOptions` |
| `Documents.Infrastructure/Scanner/CircuitBreakerScannerProvider.cs` | **New file** — Polly circuit breaker decorator |
| `Documents.Infrastructure/Observability/ScanMetrics.cs` | Added 3 circuit breaker metrics |
| `Documents.Infrastructure/Health/ClamAvHealthCheck.cs` | Injected `IFileScannerProvider`; added circuit state check |
| `Documents.Infrastructure/DependencyInjection.cs` | Replaced switch expression with factory that wraps ClamAV with circuit breaker |
| `Documents.Infrastructure/Documents.Infrastructure.csproj` | Added `Polly` v7.2.4 package reference |
| `Documents.Api/appsettings.json` | Added `Scanner:ClamAv:CircuitBreaker` section with defaults |

---

## Metrics Added

All metrics exposed at `GET /metrics` (prometheus-net).

| Metric | Type | Description |
|---|---|---|
| `docs_clamav_circuit_state` | Gauge | Current state: 0=closed, 1=open, 2=half-open |
| `docs_clamav_circuit_open_total` | Counter | Times circuit has opened (state transitions to OPEN) |
| `docs_clamav_circuit_short_circuit_total` | Counter | Scan calls blocked by open circuit |

Metric updates occur on:
- State transition to OPEN (`onBreak`) → `circuit_state=1`, `circuit_open_total++`
- State transition to CLOSED (`onReset`) → `circuit_state=0`
- State transition to HALF-OPEN (`onHalfOpen`) → `circuit_state=2`
- Each short-circuited scan call → `circuit_short_circuit_total++`

---

## Log Messages

| Event | Level | Message |
|---|---|---|
| Circuit opens | WARNING | `ClamAV circuit opened after repeated failures — pausing for {N}s` |
| Circuit closes | INFO | `ClamAV circuit closed — normal operation resumed` |
| Circuit half-opens | INFO | `ClamAV circuit half-open — probing ClamAV availability` |
| Scan short-circuited | WARNING | `ClamAV scan skipped due to open circuit for file {FileName}` |

---

## Behavior Validation

### Scenario 1 — Normal Operation (Circuit CLOSED)
1. `Scanner:Provider = clamav`, ClamAV running.
2. Every `ScanAsync` call passes through `_policy.ExecuteAsync` to `ClamAvFileScannerProvider`.
3. `docs_clamav_circuit_state` gauge remains at 0.
4. Health check: `GET /health` → `{"status":"Healthy","clamav":"ClamAV reachable at localhost:3310"}`.

### Scenario 2 — ClamAV Outage (Circuit OPEN)
1. ClamAV stops. Every `ScanAsync` call throws `SocketException`.
2. After 5 failures within 60 s, Polly calls `onBreak`.
3. `WARNING: ClamAV circuit opened...` logged; `circuit_state=1`, `circuit_open_total++`.
4. Subsequent `ScanAsync` calls throw `BrokenCircuitException` immediately (no TCP attempt).
5. Provider catches exception, increments `circuit_short_circuit_total`, returns `ScanStatus.Failed`.
6. Worker's existing retry/backoff logic handles `Failed` result — document stays `PENDING` or becomes `FAILED` at max retries.
7. Health check: `GET /health` → `Degraded` with `circuit_state: open`.

### Scenario 3 — Recovery (HALF-OPEN → CLOSED)
1. After 30 s (`BreakDurationSeconds`), Polly enters HALF-OPEN.
2. `INFO: ClamAV circuit half-open...` logged; `circuit_state=2`.
3. Next `ScanAsync` call is allowed through as a probe.
4. If probe succeeds: `onReset` fires → `INFO: ClamAV circuit closed...` → `circuit_state=0`.
5. Normal scanning resumes.

### Scenario 4 — Recovery Probe Fails (HALF-OPEN → OPEN again)
1. Probe attempt fails (ClamAV still down).
2. `onBreak` fires again — circuit re-opens for another 30 s.
3. Cycle repeats until ClamAV is healthy.

---

## Worker Behavior

No changes to the scan worker are required. The worker calls `IFileScannerProvider.ScanAsync`
and observes `ScanResult.Status`. When the circuit is open, the call returns synchronously
with `ScanStatus.Failed`. The worker's existing retry logic (exponential backoff up to
`MaxRetryAttempts`) continues to apply identically to any other failure result.

This means:
- No retry storms: the circuit fails-fast without touching ClamAV TCP.
- Backpressure is preserved: the worker still waits between retries.
- At `MaxRetryAttempts` exhaustion, the document is marked `Failed` as normal.

---

## Risks and Limitations

| Risk | Mitigation |
|---|---|
| False positives (circuit opens on transient blip) | `MinimumThroughput=5` prevents circuit open on a single failure |
| Half-open probe consumed by non-representative request (e.g., very small file) | Acceptable; probe is same INSTREAM path as real scans |
| Circuit state is in-memory (per process) | Multi-process deployments have independent circuit states; acceptable given Redis queue handles work distribution |
| `BreakDurationSeconds=30` may be too short under sustained outage | Operators can tune via config; Polly will keep re-opening the circuit on probe failures |
| `appsettings.json` defaults apply only when `Scanner:Provider=clamav` | Circuit is wired conditionally — no overhead for `mock`/`none` providers |

---

## Build Status

```
Documents.Domain:           0 errors
Documents.Application:      0 errors
Documents.Infrastructure:   0 errors, 0 warnings (Polly 7.2.4 resolved)
Documents.Api:              0 errors, 1 pre-existing warning (CS1998 in Program.cs — unrelated)
```
