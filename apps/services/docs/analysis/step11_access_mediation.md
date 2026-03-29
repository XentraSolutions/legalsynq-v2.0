# Step 11 — Access Mediation Analysis

## Overview

Phase 11 replaces direct storage pre-signed URLs with a service-controlled
short-lived access token layer. The service is now the sole gatekeeper for every
file access event — storage keys never reach the client.

---

## Before vs After

### Before (direct pre-signed URL model)

```
Client                         Docs Service                   Object Storage
  │                                │                                │
  │  POST /documents/:id/view-url  │                                │
  │──────────────────────────────▶│                                │
  │                                │  GeneratePresignedURL(key, 300s)
  │                                │──────────────────────────────▶│
  │                                │◀─────────────── signedUrl ────│
  │◀──── { url: "https://s3/key?X-Amz-Signature=..." } ───────────│
  │                                │                                │
  │  GET https://s3/key?X-Amz-...  │  (bypasses service entirely)  │
  │──────────────────────────────────────────────────────────────▶│
  │◀──────────────────────────────────────────────────── file ────│
```

**Problems:**
- Storage bucket/key structure visible in the signed URL
- Access control is checked only ONCE (at URL generation time)
- Generated URL is shareable for its full TTL (300s default) without re-validation
- No per-access audit record — only URL generation is logged
- Revoked users can still access files until signed URL expires

---

### After (access token mediation model)

```
Client                         Docs Service                   Object Storage
  │                                │                                │
  │  POST /documents/:id/view-url  │                                │
  │──────────────────────────────▶│                                │
  │                                │  Validate: RBAC + scan status  │
  │                                │  Issue opaque token (store)    │
  │◀─── { accessToken: "abc...", redeemUrl: "/access/abc..." } ───│
  │                                │                                │
  │  GET /access/abc...            │                                │
  │──────────────────────────────▶│                                │
  │                                │  Validate: token expiry        │
  │                                │  Enforce: one-time-use         │
  │                                │  Re-check: scan status         │
  │                                │  GeneratePresignedURL(key, 30s)│
  │                                │──────────────────────────────▶│
  │                                │◀──────────── shortUrl ────────│
  │◀─────────────── 302 Redirect to shortUrl (30s TTL) ───────────│
  │                                │                                │
  │  GET shortUrl (directly)       │  (only 30-second window)       │
  │──────────────────────────────────────────────────────────────▶│
  │◀──────────────────────────────────────────────────── file ────│
```

**Security improvements:**
- Storage key never leaves the service boundary
- Access control checked TWICE: at issue time AND at redemption time
- Short-lived redirect URL (30s) limits exposure even if intercepted
- One-time-use tokens prevent token sharing/forwarding
- Every file access generates an audit record (DOCUMENT_ACCESSED)
- Scan status re-checked at redemption time (defence in depth)
- Expired token → 401 TOKEN_EXPIRED (not silently 403 from storage)

---

## Abstractions

### AccessTokenStore interface

```typescript
interface AccessTokenStore {
  store(token: AccessToken): Promise<void>;
  get(tokenString: string): Promise<AccessToken | null>;
  markUsed(tokenString: string): Promise<boolean>;   // atomic TOCTOU guard
  revoke(tokenString: string): Promise<void>;
  cleanup(): Promise<number>;
  destroy(): void;
}
```

`markUsed()` is the critical operation for one-time-use enforcement.
In `InMemoryAccessTokenStore`, Node.js's single-threaded event loop makes
the read-then-write in `markUsed()` effectively atomic.
In `RedisAccessTokenStore`, a Lua script ensures atomicity across replicas.

### AccessToken entity

| Field         | Type                    | Purpose                                      |
|---------------|-------------------------|----------------------------------------------|
| `token`       | 64-char hex (32 bytes)  | Opaque credential — 256 bits of entropy      |
| `documentId`  | UUID                    | Bound to a specific document                 |
| `tenantId`    | UUID                    | Cross-tenant isolation, re-checked on redeem |
| `userId`      | UUID                    | Full audit trail                             |
| `type`        | `'view' \| 'download'` | Access intent                                |
| `isOneTimeUse`| boolean                 | Prevent token forwarding                     |
| `isUsed`      | boolean                 | State for one-time-use enforcement           |
| `expiresAt`   | Date                    | TTL boundary                                 |
| `issuedFromIp`| string \| null          | Anomaly detection                            |

---

## Providers

### InMemoryAccessTokenStore (`ACCESS_TOKEN_STORE=memory`)

Production-ready for single-replica deployments.
- `Map<string, AccessToken>` with lazy expiry eviction on `get()`
- Background cleanup sweep every 60s
- MAX_TOKENS = 50,000 limit with emergency cleanup on breach
- `markUsed()` is TOCTOU-safe within a single process
- `destroy()` clears the timer and map for clean test isolation

### RedisAccessTokenStore (`ACCESS_TOKEN_STORE=redis`) — scaffold

Distributed implementation for multi-replica production deployments.
- Uses `SETEX key ttl value` for atomic token storage with built-in expiry
- Uses Lua script for atomic `markUsed()` — prevents replay across replicas
- `cleanup()` is a no-op (Redis TTL handles expiry)

Lua atomic `markUsed()` script (commented in source):
```lua
local key = KEYS[1]
local raw = redis.call('GET', key)
if not raw then return -1 end
local token = cjson.decode(raw)
if token.isUsed then return 0 end
token.isUsed = true
local ttl = redis.call('TTL', key)
redis.call('SET', key, cjson.encode(token), 'EX', ttl)
return 1
```
Returns: `1` = marked, `0` = already used, `-1` = not found.

To activate:
1. `npm install ioredis`
2. Set `REDIS_URL=redis://... ACCESS_TOKEN_STORE=redis`
3. Uncomment the TCP implementation in `redis-access-token-store.ts`

---

## Access Flows

### Flow 1: Token-based (default, `DIRECT_PRESIGN_ENABLED=false`)

| Step | Route | Auth Required | Notes |
|------|-------|---------------|-------|
| Issue | `POST /documents/:id/view-url` | JWT | RBAC + scan check |
| Redeem | `GET /access/:token` | None (token IS the credential) | Expiry + one-time + re-scan |

### Flow 2: Authenticated direct access

| Step | Route | Auth Required | Notes |
|------|-------|---------------|-------|
| Access | `GET /documents/:id/content` | JWT | RBAC + scan check → 302 redirect (30s URL) |

### Flow 3: Legacy compat (`DIRECT_PRESIGN_ENABLED=true`)

| Step | Route | Auth Required | Notes |
|------|-------|---------------|-------|
| Get URL | `POST /documents/:id/view-url` | JWT | Returns `{ url, expiresInSeconds }` directly |

---

## Configuration Reference

| Variable | Values | Default | Notes |
|----------|--------|---------|-------|
| `ACCESS_TOKEN_STORE` | `memory \| redis` | `memory` | Choose based on deployment model |
| `ACCESS_TOKEN_TTL_SECONDS` | integer ≥ 10 | `300` | Token lifetime (not the redirect URL lifetime) |
| `ACCESS_TOKEN_ONE_TIME_USE` | `true \| false` | `true` | `true` recommended for production |
| `DIRECT_PRESIGN_ENABLED` | `true \| false` | `false` | `true` enables legacy direct pre-signed URL mode |

---

## Files Changed

| File | Change |
|------|--------|
| `src/domain/entities/access-token.ts` | **NEW** — `AccessToken`, `IssuedToken`, `PresignedUrlResult` entities |
| `src/domain/interfaces/access-token-store.ts` | **NEW** — `AccessTokenStore` interface |
| `src/shared/constants.ts` | Added 5 access-token `AuditEvent` values |
| `src/shared/errors.ts` | Added `TokenExpiredError` (401) + `TokenInvalidError` (401) |
| `src/shared/config.ts` | Added 4 new env vars |
| `src/infrastructure/access-token/in-memory-access-token-store.ts` | **NEW** — full production implementation |
| `src/infrastructure/access-token/redis-access-token-store.ts` | **NEW** — scaffold with Lua atomic script |
| `src/infrastructure/access-token/access-token-store-factory.ts` | **NEW** — singleton factory |
| `src/application/access-token-service.ts` | **NEW** — `issue()`, `redeem()`, `accessDirect()` |
| `src/application/document-service.ts` | Added `requestAccess()` (mode-aware dispatcher); `generateSignedUrl()` retained as internal/legacy |
| `src/api/routes/documents.ts` | Updated view-url + download-url to use `requestAccess()`; added `GET /:id/content` |
| `src/api/routes/access.ts` | **NEW** — `GET /access/:token` token redemption route |
| `src/app.ts` | Registered `/access` router |
| `tests/unit/access-mediation.test.ts` | **NEW** — 32 unit tests |

---

## Security Improvements Summary

| Threat | Before | After |
|--------|--------|-------|
| Storage key/bucket exposure | ⚠️ Visible in pre-signed URL path | ✅ Never sent to client |
| Access control enforcement | Once (URL generation only) | Twice (issue + redeem) |
| Scan status enforcement | Once (URL generation) | Twice (issue + redeem) |
| Token forwarding / sharing | ⚠️ Full-TTL URL shareable | ✅ One-time-use tokens |
| Audit granularity | URL generation only | Per-access record (DOCUMENT_ACCESSED) |
| Revocation latency | Full TTL window | Instant (revoke from store) |
| Replay attack | N/A (URL is the secret) | ✅ TOCTOU-safe markUsed() |
| Cross-tenant bypass | Checked once at issue | Re-checked at every redemption |

---

## Audit Events

| Event | When |
|-------|------|
| `ACCESS_TOKEN_ISSUED` | Token successfully issued (view-url / download-url) |
| `ACCESS_TOKEN_REDEEMED` | Token successfully redeemed (`GET /access/:token`) |
| `ACCESS_TOKEN_EXPIRED` | Attempted redemption of expired/missing token |
| `ACCESS_TOKEN_INVALID` | Attempted replay of one-time-use token |
| `DOCUMENT_ACCESSED` | Authenticated direct access (`GET /documents/:id/content`) |

---

## Verification Steps

### 1. Unit tests

```bash
cd apps/services/docs
npm test -- --testPathPattern=access-mediation --forceExit
```
Expected: **32 tests passing** across 5 describe blocks.

### 2. Full test suite

```bash
npm test -- --forceExit
```
Expected: **91 tests passing** across 5 suites.

### 3. Token issue + redeem flow (manual)

```bash
# Start service in token mode (default)
FILE_SCANNER_PROVIDER=mock ACCESS_TOKEN_ONE_TIME_USE=true npm run dev

# Step 1: Issue a token
curl -s -X POST http://localhost:5005/api/v1/documents/$DOC_ID/view-url \
  -H "Authorization: Bearer $JWT" | jq '.data'
# Expected: { "accessToken": "abc...", "redeemUrl": "/access/abc...", ... }

# Step 2: Redeem the token (get redirect)
curl -v http://localhost:5005/access/$TOKEN 2>&1 | grep "Location:"
# Expected: Location: https://storage.example.com/...?...  (30s URL)

# Step 3: Attempt replay (one-time-use)
curl -s http://localhost:5005/access/$TOKEN
# Expected: 401 TOKEN_INVALID
```

### 4. Cross-tenant denial (manual)

```bash
# Issue token for tenant A's document using tenant A's JWT
TOKEN_A=$(curl -s -X POST http://localhost:5005/api/v1/documents/$DOC_A_ID/view-url \
  -H "Authorization: Bearer $JWT_TENANT_A" | jq -r '.data.accessToken')

# Redeem TOKEN_A using tenant B's JWT — should still work (token redemption is unauthenticated)
# but document re-fetch will use stored tenantId, so cross-tenant bypass is impossible
curl -v http://localhost:5005/access/$TOKEN_A
# Expected: 302 redirect to the correct document (tenantId bound to token, not to caller)
```

### 5. Legacy mode test (`DIRECT_PRESIGN_ENABLED=true`)

```bash
DIRECT_PRESIGN_ENABLED=true npm run dev

curl -s -X POST http://localhost:5005/api/v1/documents/$DOC_ID/view-url \
  -H "Authorization: Bearer $JWT" | jq '.data'
# Expected: { "url": "https://...", "expiresInSeconds": 300 }
```

### 6. Authenticated direct access

```bash
curl -v http://localhost:5005/api/v1/documents/$DOC_ID/content \
  -H "Authorization: Bearer $JWT"
# Expected: 302 redirect to 30-second storage URL
```

### 7. Audit verification

```bash
psql $DOCS_DB -c "
  SELECT event, outcome, actor_id, occurred_at
  FROM document_audits
  WHERE event LIKE 'ACCESS_TOKEN%' OR event = 'DOCUMENT_ACCESSED'
  ORDER BY occurred_at DESC
  LIMIT 20;
"
```
