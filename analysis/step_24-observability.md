# Step 24 – Observability

## Files Created

| File | Purpose |
|------|---------|
| `src/lib/logger.ts` | Structured logger with `logInfo`, `logWarn`, `logError`; dev = human-readable console lines; prod = NDJSON to stdout |

## Files Updated

| File | What changed |
|------|-------------|
| `src/lib/api-client.ts` | Added `crypto.randomUUID()` requestId, `X-Request-Id` header, `getTenantContext()`/`getImpersonation()` context reading, network-error boundary around `fetch()`, `logInfo`/`logError` at every lifecycle point |

---

## Logging Strategy

### Two-environment model

| Environment | Output format | Destination | Use |
|-------------|--------------|-------------|-----|
| `development` | Human-readable prefix lines with ANSI colour | `console.log/warn/error` | Dev console / Next.js terminal |
| `production` | NDJSON (one compact JSON object per line) | `process.stdout` | Log aggregator (CloudWatch, Datadog, GCP Logging, …) |

The environment is determined by `process.env.NODE_ENV`. The logger does not
require any configuration — it selects the correct mode automatically.

### Dev format

```
[CC] INFO  05:30:00.123  api.request.start  GET /identity/api/admin/tenants  req=f4e9a1c2  tenant=LEGALSYNQ
[CC] INFO  05:30:00.456  api.request.success  GET /identity/api/admin/tenants  HTTP 200  +333ms  req=f4e9a1c2  tenant=LEGALSYNQ
[CC] ERROR 05:30:00.456  api.request.error  POST /identity/api/admin/support  HTTP 500  +250ms  req=f4e9a1c2  "Internal Server Error"
[CC] ERROR 05:30:00.457  api.network_failure  GET /identity/api/admin/tenants  req=f4e9a1c2  "fetch failed"
```

Stack traces are printed on the next line for ERROR events (dev only).

### Prod format (NDJSON)

```json
{"level":"INFO","message":"api.request.start","timestamp":"2026-03-29T05:30:00.123Z","service":"control-center","requestId":"f4e9a1c2-...","method":"GET","endpoint":"/identity/api/admin/tenants","tenantId":"legalsynq-tenant-id","tenantCode":"LEGALSYNQ"}
{"level":"INFO","message":"api.request.success","timestamp":"2026-03-29T05:30:00.456Z","service":"control-center","requestId":"f4e9a1c2-...","method":"GET","endpoint":"/identity/api/admin/tenants","tenantId":"legalsynq-tenant-id","tenantCode":"LEGALSYNQ","durationMs":333,"status":200}
{"level":"ERROR","message":"api.request.error","timestamp":"2026-03-29T05:30:00.456Z","service":"control-center","requestId":"f4e9a1c2-...","method":"POST","endpoint":"/identity/api/admin/support","durationMs":250,"status":500,"errorName":"ApiError","errorMessage":"Internal Server Error"}
```

Stack traces are **omitted** in production to reduce log volume and avoid
accidental source-path leakage.

### LogMeta interface

```ts
export interface LogMeta {
  requestId?:             string;
  endpoint?:              string;
  method?:                string;
  durationMs?:            number;
  status?:                number;
  tenantId?:              string;
  tenantCode?:            string;
  impersonatedUserId?:    string;
  impersonatedUserEmail?: string;
  [key: string]:          unknown;  // extra caller-defined fields
}
```

The index signature allows callers to include arbitrary extra fields without
casting. Unknown fields are forwarded verbatim to the log output in both modes.

### Public API

| Function | Level | Usage |
|----------|-------|-------|
| `logInfo(message, meta?)` | INFO | Normal operational events (request start/success, context switches) |
| `logWarn(message, meta?)` | WARN | Degraded but recoverable (unexpected API field values, slow responses) |
| `logError(message, error?, meta?)` | ERROR | Failures requiring investigation (4xx/5xx, network errors, exceptions) |

`logError` accepts the raw thrown value as its second argument. It calls
`serialiseError()` internally to extract `{ errorName, errorMessage, errorStack? }`
without crashing if the value is not an `Error` instance.

---

## Request Tracing

### requestId generation

```ts
const requestId = crypto.randomUUID();
```

`crypto.randomUUID()` is a WHATWG standard available on Node.js 18+ (project
uses 22.19.0). Each call to `apiFetch` generates a fresh RFC-4122 UUID v4.

### Propagation

The requestId is propagated in two ways:

1. **Outbound header** — every request to the API gateway carries:
   ```
   X-Request-Id: f4e9a1c2-1234-4abc-9def-000000000001
   ```
   The gateway can forward this to downstream Identity / Fund / CareConnect
   services so a single error visible in the Control Center UI can be
   traced to the exact backend log entry.

2. **All log entries** — every `logInfo`/`logError` call for the request
   lifecycle includes `requestId` in `LogMeta`, making the full lifecycle
   searchable by a single string in any log aggregator:
   ```
   api.request.start → api.request.success  (happy path)
   api.request.start → api.network_failure  (network error)
   api.request.start → api.request.error    (4xx/5xx)
   api.request.start → api.request.redirect_401  (session expired)
   ```

### Log events emitted by apiFetch

| Event label | Level | When | Extra fields |
|-------------|-------|------|-------------|
| `api.request.start` | INFO | Before `fetch()` call | method, endpoint, tenantId?, impersonatedUserId? |
| `api.request.success` | INFO | After 2xx response | durationMs, status |
| `api.request.redirect_401` | INFO | On HTTP 401 (before redirect) | durationMs, status=401 |
| `api.request.error` | ERROR | On non-2xx (4xx/5xx) | durationMs, status, errorName, errorMessage |
| `api.network_failure` | ERROR | When `fetch()` throws (pre-response) | durationMs, errorName, errorMessage, errorStack(dev) |

---

## Context Included

### Tenant context

`getTenantContext()` is called at the start of every `apiFetch` call. When a
platform admin has switched into a tenant context (via the tenant switcher), the
following fields are automatically included in all log entries for that request:

```json
{ "tenantId": "uuid-of-the-tenant", "tenantCode": "HARTWELL" }
```

This allows filtering all API calls made within a specific tenant context in the
log aggregator.

### Impersonation context

`getImpersonation()` is called at the start of every `apiFetch` call. When
impersonation is active, the following fields are automatically included:

```json
{
  "impersonatedUserId":    "uuid-of-the-target-user",
  "impersonatedUserEmail": "margaret@hartwell.law"
}
```

This is a critical audit trail — any API call made while impersonating a user
is automatically tagged with the impersonated identity in the log entry.

### How context is attached

```ts
const logMeta = {
  requestId,
  method,
  endpoint: path,
  ...(tenantCtx     ? { tenantId: tenantCtx.tenantId, tenantCode: tenantCtx.tenantCode } : {}),
  ...(impersonation ? {
    impersonatedUserId:    impersonation.impersonatedUserId,
    impersonatedUserEmail: impersonation.impersonatedUserEmail,
  } : {}),
};
```

The `logMeta` object is spread into every log call for the request, so context
fields are never omitted from any lifecycle event (start, success, error, etc.).

---

## TODOs

```ts
// TODO: integrate with Datadog / OpenTelemetry
//   — Replace the current console/stdout logging with the OTel SDK.
//     The requestId maps to trace.span_id; the service field maps to
//     resource.service.name. This would enable distributed tracing
//     across the API gateway and all downstream microservices.

// TODO: send logs to centralized logging service
//   — In production, pipe stdout to CloudWatch Logs (via ECS/Fargate),
//     Datadog Agent (log collection), or equivalent. The NDJSON format
//     is compatible with all major log aggregators without transformation.

// TODO: add log sampling for high-volume INFO events
//   — api.request.start + api.request.success are emitted on every
//     API call. Under high load this could dominate log volume.
//     Sample at e.g. 10% for INFO in production once a baseline is set.

// TODO: add correlation tracing (trace-id across microservices)
//   — Extend X-Request-Id to a W3C traceparent header so the gateway
//     and backend services can participate in the same trace context.

// TODO: add slow-response warning threshold
//   — If durationMs > SLOW_THRESHOLD (e.g. 2000ms), emit a logWarn
//     'api.slow_response' entry so slow endpoints are visible in dashboards.

// TODO: add log redaction for PII
//   — Scan LogMeta for fields that look like email addresses or phone
//     numbers before emitting in production.
```

---

## Independence Validation

- `logger.ts`: zero imports from project code; only `process.env`, `process.stdout`, and `console.*`
- `api-client.ts` import additions:
  - `import { logInfo, logError } from '@/lib/logger'` — logger.ts has no external deps
  - `import { getTenantContext, getImpersonation } from '@/lib/auth'` — import chain:
    - `auth.ts` → `session.ts` → raw `fetch()` (no `api-client` import)
    - `auth.ts` → `auth-guards.ts` → `session.ts` (no `api-client` import)
    - `auth.ts` → `app-config.ts` (no imports from project code)
    - **No circular dependency**
- Public API signatures of `apiFetch`, `apiClient`, `ApiError`, `ApiFetchOptions`,
  `CACHE_TAGS`, `CacheTag` are **all unchanged** — zero impact on callers
- No new npm dependencies — uses only:
  - `crypto.randomUUID()` — Node.js 18+ global (project: Node.js 22.19.0)
  - `process.stdout.write` — Node.js built-in
  - `process.env.NODE_ENV` — Next.js built-in
- TypeScript check: **0 errors**

---

## Any Issues or Assumptions

1. **`crypto.randomUUID()` global** — Next.js 14 App Router (Node.js runtime) exposes
   `globalThis.crypto` conformant with the WHATWG Web Crypto API. On Node.js 18+
   this is always available without any import. The code calls `crypto.randomUUID()`
   directly (no `import crypto from 'crypto'`) which matches the Next.js runtime
   environment. TypeScript may require `"lib": ["ES2022"]` or `"DOM"` to type-check
   this without an explicit import; the tsconfig already targets ES2022.

2. **`getTenantContext()` and `getImpersonation()` in `apiFetch`** — Both functions
   call `cookies()` from `next/headers`. Next.js memoises `cookies()` per-request
   so calling it three times (once in `apiFetch` directly + once inside each helper)
   hits the same in-memory store — no I/O penalty.

3. **Context is read-only in `apiFetch`** — The logger enriches log entries with
   tenantId and impersonatedUserId but does NOT modify the cookie or inject any
   extra headers based on impersonation. Impersonation token propagation to the
   backend is a separate concern (tracked in auth.ts TODOs).

4. **401 is logged as INFO, not ERROR** — Session expiry is a normal expected
   lifecycle event in a long-lived admin session. Logging it at ERROR level would
   create false positives in alert rules. The redirect itself handles the UX.

5. **Network errors retain the original error type** — `apiFetch` catches network
   errors for logging but re-throws the original value unchanged. Callers that
   already catch `ApiError` will not accidentally catch a `TypeError: fetch failed`
   as an `ApiError` because the types are different.

6. **`process.stdout.write` vs `console.log` in prod** — `process.stdout.write`
   is used in production to avoid the extra newline behaviour and timestamp
   prepending that some Node.js `console.log` overrides add. The `\n` is appended
   manually so each JSON object is on its own line (NDJSON standard).
