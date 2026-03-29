# Phase 9 — Enterprise-Grade Rate Limiting

## Objective

Protect the Docs Service from:
- **Brute-force attacks** — repeated auth attempts, enumeration
- **Upload flooding** — resource exhaustion via large/frequent file uploads
- **Signed URL abuse** — harvesting pre-signed URLs in bulk
- **Tenant abuse** — one tenant degrading service quality for others

Rate limiting enforced across **three independent dimensions**:
1. **IP address** — protection before auth is validated
2. **userId** — per-authenticated-user quota
3. **tenantId** — tenant-level fairness enforcement

---

## Design

### Algorithm: Fixed-Window Counter

- Simple, fast, O(1) per check
- Key: `{type}:{identifier}:{windowBucket}` where `windowBucket = floor(now / windowMs)`
- Trade-off: allows up to 2× max at window boundaries — acceptable for this use case
- Alternative (sliding window) available via Redis `ZRANGEBYSCORE` pattern if needed

### Dimension Order

Checks run in order: **IP → userId → tenantId**

Rationale:
- IP check runs even for unauthenticated requests (defence before auth overhead)
- User check catches authenticated users trying to rotate IPs
- Tenant check provides cross-user tenant-level fairness

The FIRST dimension to exceed its limit triggers a 429. Each check is independently configurable.

### Response Headers

All responses (including non-429) include:
```
X-RateLimit-Limit:     100
X-RateLimit-Remaining: 94
X-RateLimit-Reset:     1711904400   (Unix timestamp, seconds)
Retry-After:           42           (429 responses only)
```

---

## New Interface

### `RateLimitProvider` (`domain/interfaces/rate-limit-provider.ts`)

```typescript
interface RateLimitKey {
  type:          string;   // 'ip' | 'user' | 'tenant'
  identifier:    string;
  windowSeconds: number;
  maxRequests:   number;
}

interface RateLimitResult {
  allowed:           boolean;
  remaining:         number;
  limit:             number;
  resetAt:           number;   // epoch ms
  retryAfterSeconds: number;
}

interface RateLimitProvider {
  check(key: RateLimitKey): Promise<RateLimitResult>;
  reset(type: string, identifier: string): Promise<void>;
  providerName(): string;
}
```

---

## Implementations

### `InMemoryRateLimitProvider` (default)
- Fixed-window using a `Map<string, { count, windowStart }>`
- Background sweep every 5 minutes removes expired entries (memory safety)
- Timer is `.unref()`-ed so it doesn't prevent Node.js from exiting
- Zero external dependencies — works without Redis

### `RedisRateLimitProvider` (scaffold)
- Atomic Lua script: `INCR` + conditional `EXPIRE` in a single command
- Lua script prevents race conditions on distributed instances
- Activate by: `npm install ioredis`, set `RATE_LIMIT_PROVIDER=redis`, `REDIS_URL`

---

## Configuration

| Env Var | Default | Description |
|---|---|---|
| `RATE_LIMIT_PROVIDER` | `memory` | `memory` or `redis` |
| `REDIS_URL` | — | Redis connection string (redis provider only) |
| `RATE_LIMIT_WINDOW_SECONDS` | `60` | Window size in seconds |
| `RATE_LIMIT_MAX_REQUESTS` | `100` | General endpoint limit per dimension per window |
| `RATE_LIMIT_UPLOAD_MAX` | `10` | Stricter limit for upload endpoints |
| `RATE_LIMIT_SIGNED_URL_MAX` | `30` | Stricter limit for signed-URL endpoints |

---

## Endpoint Limits

| Endpoint | Applied Limiter | IP Max | User Max | Tenant Max |
|---|---|---|---|---|
| All routes (router-level) | `generalLimiter` | 100/min | 100/min | 200/min |
| `POST /documents` | + `uploadLimiter` | 10/min | 10/min | 20/min |
| `POST /documents/:id/versions` | + `uploadLimiter` | 10/min | 10/min | 20/min |
| `POST /documents/:id/view-url` | + `signedUrlLimiter` | 30/min | 30/min | 60/min |
| `POST /documents/:id/download-url` | + `signedUrlLimiter` | 30/min | 30/min | 60/min |

Upload and signed-URL limiters run **after** the general limiter — the stricter limit wins.

---

## New Error Type

```typescript
class RateLimitError extends DocsError {
  statusCode:       429
  code:             'RATE_LIMIT_EXCEEDED'
  retryAfterSeconds: number
  limitDimension:   'ip' | 'user' | 'tenant'
}
```

Error handler sets `Retry-After` response header before writing the body.

---

## Files Changed / Created

| File | Change |
|---|---|
| `src/domain/interfaces/rate-limit-provider.ts` | **NEW** — provider interface |
| `src/infrastructure/rate-limit/in-memory-rate-limit-provider.ts` | **NEW** — full in-memory impl |
| `src/infrastructure/rate-limit/redis-rate-limit-provider.ts` | **NEW** — Redis scaffold |
| `src/infrastructure/rate-limit/rate-limit-factory.ts` | **NEW** — provider factory |
| `src/api/middleware/rate-limiter.ts` | **NEW** — Express middleware + pre-built limiters |
| `src/shared/config.ts` | **UPDATED** — 6 new rate-limit config vars |
| `src/shared/errors.ts` | **UPDATED** — added `RateLimitError` (429) |
| `src/api/middleware/error-handler.ts` | **UPDATED** — specific 429 handler with `Retry-After` |
| `src/api/routes/documents.ts` | **UPDATED** — `uploadLimiter` + `signedUrlLimiter` on 4 routes |
| `tests/unit/rate-limiting.test.ts` | **NEW** — 22 unit tests |
| `.env.example` | **UPDATED** — rate limit variables documented |

---

## Logging

All rate-limit violations emit a structured `warn` log:

```json
{
  "level":         "warn",
  "dimension":     "user",
  "correlationId": "uuid",
  "retryAfter":    45,
  "msg":           "Rate limit exceeded"
}
```

**No sensitive data in logs**: no userId values, no IP addresses, no JWT claims.

---

## Verification Steps

### Manual

```bash
# 1. Start service locally (memory provider, window=60s, uploadMax=3 for quick test)
RATE_LIMIT_UPLOAD_MAX=3 npm run dev

# 2. Hit upload endpoint 4 times — 4th should return 429
for i in {1..4}; do
  curl -s -o /dev/null -w "%{http_code}\n" \
    -X POST http://localhost:5005/documents \
    -H "Authorization: Bearer <token>"
done
# Expected: 200 200 200 429

# 3. Check rate-limit headers
curl -I -X POST http://localhost:5005/documents/some-id/view-url \
  -H "Authorization: Bearer <token>"
# Expected headers:
#   X-RateLimit-Limit: 30
#   X-RateLimit-Remaining: 29
#   X-RateLimit-Reset: <timestamp>
```

### Automated Tests

```bash
cd apps/services/docs
npm run test:unit
# 22 tests in tests/unit/rate-limiting.test.ts
# + 15 existing tests
# = 37 total
```

### Integration Tests (Recommended Additions)

```
POST /documents ×11 from same IP  → 11th returns 429, dimension=ip
POST /documents ×11 from same userId → 11th returns 429, dimension=user
10 users from same tenant → tenant counter shared → tenant 429 at tenantMax
GET /documents ×100 → allowed (generalLimiter); POST /documents ×11 → blocked
Window reset: wait windowSeconds → counter resets → requests allowed again
Redis provider: run 2 instances → shared counter respected across instances
```

---

## Security Notes

- IP check runs before auth overhead (protection against unauth floods)
- Tenant limit prevents one tenant from hogging the service (multi-tenant fairness)
- `uploadLimiter` runs **before** multer: files are never read/processed when rate-limited
- No raw IPs stored in audit logs or error responses
- `Retry-After` header guides clients to back off correctly
