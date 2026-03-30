# Phase 3 — Backpressure and Error Handling

## Summary

Changed the queue's upload behavior from wait/block to fail-fast. When the scan queue is
saturated, the upload API returns HTTP 503 immediately with a `Retry-After` header.

---

## Problem (before)

`BoundedChannelFullMode.Wait` caused `WriteAsync` to block indefinitely when the channel was
full. This had two consequences:

1. Upload HTTP requests stalled while waiting for queue space
2. ASP.NET Core request timeout (default 30s) would trigger an opaque 503/timeout response
3. Clients had no clear signal to back off with retry logic

---

## Solution

### Queue change

`BoundedChannelOptions.FullMode = DropWrite` — `TryWrite` returns `false` immediately.
`IScanJobQueue.TryEnqueueAsync` returns `false` when the queue is full — no blocking.

### Exception model

New exception: `QueueSaturationException` (HTTP 503):

```csharp
public sealed class QueueSaturationException : DocumentsException
{
    public override int    StatusCode => 503;
    public override string ErrorCode  => "QUEUE_SATURATED";

    public QueueSaturationException()
        : base("Scan queue is saturated — upload rejected. Retry after a short delay.") { }
}
```

### Upload flow (after)

```
POST /v1/documents (upload)
  → ScanOrchestrationService.EnqueueDocumentScanAsync
    → queue.TryEnqueueAsync()
      OK → return 201 { scanStatus: "PENDING" }
      FULL → throw QueueSaturationException()
               → ExceptionHandlingMiddleware → 503 response
```

### HTTP 503 response body

```json
{
  "error": "QUEUE_SATURATED",
  "message": "Scan queue is saturated — upload rejected. Retry after a short delay.",
  "retryAfter": 30,
  "correlationId": "8f2a9c1e-..."
}
```

### Response headers

```
HTTP/1.1 503 Service Unavailable
Content-Type: application/json
Retry-After: 30
X-Correlation-Id: 8f2a9c1e-...
```

---

## Middleware ordering

`QueueSaturationException` extends `DocumentsException`, so it must be matched **before**
the generic `DocumentsException` case in the switch:

```csharp
switch (ex)
{
    case QueueSaturationException qse:   // ← checked first (specific)
        statusCode = 503; ...
        break;

    case DocumentsException de:          // ← catches all remaining subtypes
        statusCode = de.StatusCode; ...
        break;
    ...
}
```

---

## Metrics and logging

| Event | Metric | Log level |
|-------|--------|-----------|
| Queue full on `TryWrite` | `docs_scan_queue_saturations_total` | Warning |
| QueueSaturationException caught | (middleware logs) | Warning |

---

## Client guidance

Clients (frontend, API consumers) should implement:
1. Detect `errorCode == "QUEUE_SATURATED"` or status `503`
2. Read `Retry-After` header (default: 30s)
3. Retry after that delay with exponential backoff
4. Alert ops if retries exceed 5–10 attempts

---

## API Behavior Changes

| Before | After |
|--------|-------|
| Upload stalls (indefinite block) | Upload returns 503 immediately |
| No machine-readable error for queue full | `QUEUE_SATURATED` error code |
| No retry guidance | `Retry-After: 30` header |

Existing upload API shape is **preserved** — only the queue saturation error path changes.

---

## Files Changed

| File | Change |
|------|--------|
| `Documents.Application/Exceptions/DocumentsExceptions.cs` | Added `QueueSaturationException` |
| `Documents.Api/Middleware/ExceptionHandlingMiddleware.cs` | Added `QueueSaturationException` case (before base) |
| `Documents.Infrastructure/Scanner/InMemoryScanJobQueue.cs` | `DropWrite` mode + `TryEnqueueAsync` |
| `Documents.Application/Services/ScanOrchestrationService.cs` | Uses `TryEnqueueAsync`, throws on `false` |

---

## Verification Steps

1. Set `ScanWorker:QueueCapacity=1` in dev
2. Upload two files rapidly — second upload should return 503 with `QUEUE_SATURATED`
3. Verify `Retry-After: 30` header is present
4. Verify `docs_scan_queue_saturations_total` counter increments at `/metrics`
5. Reset capacity to 1000 — subsequent uploads should succeed normally
