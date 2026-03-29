# Step 12 — Redis Backing for Ephemeral State

## Overview

Phase 12 replaces the in-process memory stores for access tokens and rate
limiting with production-ready Redis implementations.  A shared connection
layer (`redis-client.ts`) encapsulates all ioredis lifecycle management so
the two providers never import ioredis directly.

The guiding constraint: **the service must run without Redis**.  Every factory
falls back to the memory provider when `REDIS_URL` is unset or the
Redis constructor fails, and all runtime errors are wrapped in
`RedisUnavailableError` (HTTP 503) so callers can decide what to do.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                       Docs Service process                       │
│                                                                  │
│   ┌──────────────────────┐   ┌──────────────────────────────┐   │
│   │  AccessTokenService  │   │  Rate-limit middleware        │   │
│   └─────────┬────────────┘   └─────────────┬────────────────┘   │
│             │ getAccessTokenStore()         │ getRateLimitProvider()
│   ┌─────────▼────────────┐   ┌─────────────▼────────────────┐   │
│   │ access-token-store-  │   │  rate-limit-factory.ts        │   │
│   │ factory.ts           │   │                               │   │
│   │  ├ REDIS_URL set? ── ┼── │   ├ REDIS_URL set? ───────── │   │
│   │  │  yes → Redis      │   │   │   yes → Redis             │   │
│   │  │  no  → Memory     │   │   │   no  → Memory            │   │
│   └─────────┬────────────┘   └─────────────┬────────────────┘   │
│             │                               │                    │
│   ┌─────────▼───────────────────────────────▼──────────────┐    │
│   │                  redis-client.ts                         │    │
│   │   Single ioredis instance · lazyConnect · structured log│    │
│   └─────────────────────────────┬──────────────────────────┘    │
│                                 │                                │
└─────────────────────────────────┼────────────────────────────────┘
                                  │
                           ┌──────▼───────┐
                           │  Redis / TLS  │
                           │  (optional)   │
                           └──────────────┘
```

Infrastructure layer only.  Application services (`AccessTokenService`,
`AuditService`, `ScanService`) never import anything from
`infrastructure/redis/`.

---

## Files Added / Changed

| File | Type | Description |
|------|------|-------------|
| `src/infrastructure/redis/redis-client.ts` | **NEW** | Shared connection layer; exports `getRedisClient()`, `isRedisHealthy()`, `disconnectRedis()`, `resetRedisClient()` |
| `src/infrastructure/access-token/redis-access-token-store.ts` | **REPLACED** | Full implementation (was scaffold) |
| `src/infrastructure/rate-limit/redis-rate-limit-provider.ts` | **REPLACED** | Full implementation (was scaffold) |
| `src/infrastructure/access-token/access-token-store-factory.ts` | **UPDATED** | Graceful fallback logic |
| `src/infrastructure/rate-limit/rate-limit-factory.ts` | **UPDATED** | Graceful fallback logic + `resetRateLimitProvider()` |
| `src/shared/errors.ts` | **UPDATED** | Added `RedisUnavailableError` (HTTP 503) |
| `tests/unit/redis-backing.test.ts` | **NEW** | 35 unit tests |

---

## Interfaces

### `AccessTokenStore` (unchanged)

```typescript
interface AccessTokenStore {
  store(token: AccessToken): Promise<void>;
  get(tokenString: string): Promise<AccessToken | null>;
  markUsed(tokenString: string): Promise<boolean>;  // atomic TOCTOU guard
  revoke(tokenString: string): Promise<void>;
  cleanup(): Promise<number>;
  destroy(): void;
}
```

Both `InMemoryAccessTokenStore` and `RedisAccessTokenStore` satisfy this
contract so callers see zero difference.

### `RateLimitProvider` (unchanged)

```typescript
interface RateLimitProvider {
  check(key: RateLimitKey): Promise<RateLimitResult>;
  reset(type: string, identifier: string): Promise<void>;
  providerName(): string;
}
```

---

## Redis Connection Layer (`redis-client.ts`)

### Options

| Option | Value | Rationale |
|--------|-------|-----------|
| `lazyConnect` | `true` | Service starts even if Redis is unreachable at boot |
| `enableOfflineQueue` | `false` | Commands fail immediately when disconnected; no unbounded memory queue |
| `maxRetriesPerRequest` | `3` | Retry transient errors before throwing |
| `connectTimeout` | `5 000 ms` | Fail fast on bad REDIS_URL |
| `commandTimeout` | `3 000 ms` | Individual command deadline |
| `reconnectOnError` | `ECONNRESET \| ETIMEDOUT` | Reconnect on network faults, not auth errors |

### Events → structured log

| Event | Log level | `_healthy` |
|-------|-----------|-----------|
| `connect` | info | `true` |
| `ready` | debug | `true` |
| `error` | error (with `err.code`) | `false` |
| `close` | warn | `false` |
| `reconnecting` | info (with `delayMs`) | unchanged |

### Exports

```typescript
getRedisClient(): Redis          // create-or-return singleton
isRedisHealthy(): boolean        // for health-checks and factory decisions
disconnectRedis(): Promise<void> // graceful shutdown (QUIT → disconnect fallback)
resetRedisClient(): void         // test isolation (no network call)
```

---

## Key Schema

| Provider | Key format | TTL |
|----------|------------|-----|
| AccessTokenStore | `access_token:{hex64}` | `ACCESS_TOKEN_TTL_SECONDS` (default 300s) |
| RateLimitProvider | `rl:{type}:{identifier}:{windowBucket}` | `windowSeconds` |

`windowBucket = floor(unixSeconds / windowSeconds)` — a monotonically
increasing integer.  Old buckets expire automatically; no cleanup needed.

---

## Atomic Operations

### Access token: `markUsed()` — Lua script

```lua
local key  = KEYS[1]
local raw  = redis.call('GET', key)
if not raw then return -1 end       -- -1: not found / expired
local tok  = cjson.decode(raw)
if tok.isUsed then return 0 end     --  0: already used (replay attempt)
tok.isUsed = true
local ttl  = redis.call('TTL', key)
if ttl < 1 then return -1 end
redis.call('SET', key, cjson.encode(tok), 'EX', ttl)
return 1                            --  1: marked successfully
```

Lua scripts run as a single atomic Redis command — no two concurrent
requests can both succeed, even across horizontally-scaled replicas.

Return value | Meaning | `markUsed()` returns
-------------|---------|---------------------
`1` | First use — success | `true`
`0` | Already used — replay | `false`
`-1` | Key missing or expired | `false`

### Rate limiting: INCR + conditional EXPIRE — Lua script

```lua
local key    = KEYS[1]
local window = tonumber(ARGV[1])
local count  = redis.call('INCR', key)
if count == 1 then
  redis.call('EXPIRE', key, window)
end
local ttl = redis.call('TTL', key)
return {count, ttl}
```

Atomically:
1. Increment the counter
2. Set the TTL on first hit (prevents the INCR/EXPIRE race)
3. Return `[count, ttl]` — the caller computes `allowed`, `remaining`, `resetAt`

---

## Configuration Reference

| Variable | Values | Default | Notes |
|----------|--------|---------|-------|
| `REDIS_URL` | `redis[s]://...` | _(none)_ | Required for redis providers; if unset, factories fall back to memory |
| `ACCESS_TOKEN_STORE` | `memory \| redis` | `memory` | |
| `RATE_LIMIT_PROVIDER` | `memory \| redis` | `memory` | |
| `ACCESS_TOKEN_TTL_SECONDS` | integer ≥ 10 | `300` | |
| `ACCESS_TOKEN_ONE_TIME_USE` | `true \| false` | `true` | |
| `RATE_LIMIT_WINDOW_SECONDS` | integer ≥ 1 | `60` | |
| `RATE_LIMIT_MAX_REQUESTS` | integer ≥ 1 | `100` | |

---

## Graceful Failure Modes

### 1. REDIS_URL not set + redis provider requested

```
WARN { requested: 'redis', fallback: 'memory' }
     'ACCESS_TOKEN_STORE=redis but REDIS_URL is not set — falling back to memory store'
```

Service continues with in-process memory.  Acceptable for single-instance dev/CI.

### 2. Redis unreachable at startup (REDIS_URL set but no server)

```
WARN { provider: 'redis', err: 'ECONNREFUSED' }
     'Redis initial connect failed — will retry in background'
```

`lazyConnect: true` means the service starts without waiting.  ioredis retries
in background with its built-in exponential back-off.  If a request arrives
before Redis reconnects, `enableOfflineQueue: false` makes it fail immediately
with an `Error` which we catch and wrap as `RedisUnavailableError`.

### 3. Redis command failure at runtime (connection drops mid-request)

Every `RedisAccessTokenStore` method and `RedisRateLimitProvider.check()` catches:

```typescript
} catch (err) {
  const msg = err instanceof Error ? err.message : String(err);
  logger.error({ err: msg, operation }, 'Redis ... error');
  throw new RedisUnavailableError(operation, msg);
}
```

`RedisUnavailableError` has `statusCode: 503` — the existing error-handler
middleware returns `503 REDIS_UNAVAILABLE` to the client with a structured
error body.

### 4. Redis constructor failure (e.g. bad auth at connection time)

```typescript
try {
  _instance = new RedisAccessTokenStore();
} catch (err) {
  logger.warn({ ..., err: msg }, '... falling back to memory store');
  _instance = new InMemoryAccessTokenStore();
}
```

Constructor errors are caught in the factory, not the caller.  The service
remains functional with the memory provider.

---

## Deployment Considerations

### Single-instance (development, staging without HA)

```env
ACCESS_TOKEN_STORE=memory
RATE_LIMIT_PROVIDER=memory
```

No Redis needed.  Rate limits are per-process; accurate enough for dev.

### Single-instance production with Redis

```env
REDIS_URL=redis://redis-host:6379
ACCESS_TOKEN_STORE=redis
RATE_LIMIT_PROVIDER=redis
```

Tokens survive service restarts.  Rate limits accurate.

### Multi-instance (Kubernetes, ECS)

```env
REDIS_URL=rediss://redis-cluster.internal:6380   # TLS via rediss://
ACCESS_TOKEN_STORE=redis
RATE_LIMIT_PROVIDER=redis
```

Atomic Lua scripts ensure correctness even when multiple app replicas
handle concurrent requests for the same token or rate-limit key.

### TLS (Redis 6+ with TLS / ElastiCache with TLS)

ioredis supports `rediss://` URLs natively — no code change required.

### Redis Sentinel / Cluster

ioredis supports both via URL syntax (`redis://sentinel:26379`).
Lua scripts are cluster-safe when all KEYS belong to the same slot —
the single-key `eval()` calls here satisfy that constraint.

---

## Verification Steps

### 1. Unit tests

```bash
cd apps/services/docs

# New Redis tests only
npm test -- --testPathPattern=redis-backing --forceExit

# Full suite (should show 123 tests, 6 suites)
npm test -- --forceExit
```

### 2. Token persistence across service restarts (manual with Redis)

```bash
# Start service with Redis
REDIS_URL=redis://localhost:6379 \
  ACCESS_TOKEN_STORE=redis \
  FILE_SCANNER_PROVIDER=none \
  npm run dev &

# Issue a token
TOKEN=$(curl -s -X POST http://localhost:5005/api/v1/documents/$DOC_ID/view-url \
  -H "Authorization: Bearer $JWT" | jq -r '.data.accessToken')

# Kill and restart the service
kill $!
REDIS_URL=redis://localhost:6379 ACCESS_TOKEN_STORE=redis npm run dev &

# Token is still redeemable (Redis persisted it)
curl -v http://localhost:5005/access/$TOKEN
# Expected: 302 redirect
```

### 3. TTL expiry (manual)

```bash
# Issue token with short TTL
ACCESS_TOKEN_TTL_SECONDS=5 npm run dev &

TOKEN=$(curl -s -X POST .../view-url ... | jq -r '.data.accessToken')

sleep 6

# Token expired in Redis (SETEX TTL elapsed)
curl -s http://localhost:5005/access/$TOKEN
# Expected: 401 TOKEN_EXPIRED
```

### 4. Rate limiting via Redis (concurrent clients)

```bash
# Start two service instances on different ports (same Redis)
RATE_LIMIT_PROVIDER=redis REDIS_URL=redis://localhost:6379 PORT=5005 npm run dev &
RATE_LIMIT_PROVIDER=redis REDIS_URL=redis://localhost:6379 PORT=5006 npm run dev &

# Drive 12 requests through instance 1 (limit = 10)
for i in $(seq 1 12); do
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5005/api/v1/health
done
# Results: 10× 200, then 2× 429 regardless of which instance handles the request
```

### 5. Fallback behaviour (no REDIS_URL)

```bash
# Run without REDIS_URL, requesting redis providers
ACCESS_TOKEN_STORE=redis RATE_LIMIT_PROVIDER=redis npm run dev 2>&1 | grep -E 'WARN|fallback'
# Expected log lines:
# WARN ... ACCESS_TOKEN_STORE=redis but REDIS_URL is not set — falling back to memory store
# WARN ... RATE_LIMIT_PROVIDER=redis but REDIS_URL is not set — falling back to memory provider

# Service should still respond
curl http://localhost:5005/health
# Expected: 200 OK
```

### 6. Redis health endpoint (optional future enhancement)

```bash
curl http://localhost:5005/health | jq '.dependencies.redis'
# Would show: { "status": "healthy" | "degraded" }
# (Not yet implemented — isRedisHealthy() is available for wiring in)
```

### 7. Verify keys in Redis

```bash
redis-cli -u $REDIS_URL

# List access tokens
KEYS access_token:*

# Inspect a token
GET access_token:<hex64>

# Check TTL remaining
TTL access_token:<hex64>

# List rate limit counters
KEYS rl:*
```

---

## Security Notes

- `REDIS_URL` is logged as `'[redacted]'` (the connection is shown, not the credential)
- Tokens stored as JSON — `isUsed` field protected by Lua atomic flip
- Rate limit keys contain no PII (only opaque identifiers passed in from middleware)
- `enableOfflineQueue: false` prevents a Redis pause from causing a thundering-herd
  of queued commands when the connection resumes
- `commandTimeout: 3 000 ms` prevents a slow Redis from hanging request threads

---

## Test Coverage Summary

| Suite | Tests | Covers |
|-------|-------|--------|
| `redis-backing` | 35 | RedisUnavailableError; RedisAccessTokenStore (store/get/markUsed/revoke/cleanup/destroy); RedisRateLimitProvider (check/reset/providerName); factory fallback (both stores, 3 scenarios each) |
| Total (all suites) | 123 | All phases 1–12 |
