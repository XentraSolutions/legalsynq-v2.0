# Step 15 — Post-Implementation Review: Docs Service

> **Classification:** Internal Architecture Review  
> **Date:** 2026-03-29  
> **Reviewer role:** Senior Staff Engineer / Architecture Review  
> **Scope:** All seven implementation phases (Rate Limiting → Integration Tests)  
> **Data sensitivity assumption:** Legal and medical documents; HIPAA-applicable

---

## 1. Executive Summary

### Overall Maturity

**Production-ready with significant caveats.** The core security posture is sound and deliberately designed. The tenant isolation strategy is one of the strongest aspects of the implementation. However, multiple production-blocking gaps remain — the most critical being that the database schema is PostgreSQL-specific despite documentation claiming MySQL compatibility, the audit log is non-fatal (access events can be silently lost), the in-memory token and rate-limit stores are not safe for multi-replica deployment, and there is no observability beyond structured logs.

### Key Strengths

1. **Three-layer tenant isolation.** Every data access path runs through independent redundant controls: a pre-query `requireTenantId()` guard, `WHERE tenant_id = ?` SQL predicates in every query, and a service-layer ABAC assertion (`assertDocumentTenantScope`) that fires post-load. Defeating any single layer still leaves two others active.

2. **Access mediation by default.** Storage keys and bucket names are never returned in API responses (`sanitizeDocument`/`sanitizeVersion`) and are never exposed in presigned URLs unless `DIRECT_PRESIGN_ENABLED=true` (defaults to `false`). Clients receive opaque access tokens redeemed via the service, not cloud storage URLs.

3. **Provider abstraction.** All infrastructure concerns (storage, auth, scanner, rate limiter, access token store, secrets) sit behind domain interfaces. Swapping AWS → GCP requires only an environment variable change — no code change.

4. **Structured, HIPAA-conscious logging.** Pino with field-level `redact` configuration strips `Authorization`, `cookie`, `token`, `secret`, `password`, `accessKeyId`, `secretAccessKey` before any log emission. Threat names are never logged; only `threatCount`.

5. **Audit immutability.** A PostgreSQL trigger prevents `UPDATE` and `DELETE` on `document_audits` at the database level. The integration tests verify this property.

6. **Atomic one-time-use token enforcement.** The Redis `markUsed` Lua script prevents TOCTOU races in horizontally-scaled deployments. The in-memory fallback does the same with a synchronous flag check.

### Key Risks

| Risk | Severity |
|------|----------|
| Schema is PostgreSQL-specific; cannot deploy to MySQL without schema rewrite | Critical |
| Audit insert is non-fatal — access events can be silently lost in HIPAA-applicable scenarios | Critical |
| `AUTH_PROVIDER=mock` provides a complete authentication bypass with no guards | Critical |
| In-memory access token store is per-process; unsafe for multi-replica deployment | High |
| In-memory rate limiter is per-process; limits are per-instance not per-cluster | High |
| IP addresses logged in audit trail (and rate limit keys) without hashing, violating GDPR comment in code | Medium |
| No rate limiting on `/access/:token` redemption endpoint | Medium |
| `bucket name` falls back to literal `'docs-local'` when `AWS_BUCKET_NAME` is unset in S3 mode | Medium |
| JWKS cache TTL is 1 hour; compromised key stays trusted for up to 60 minutes | Medium |
| No composite index on `(id, tenant_id)` in `documents` table; tenant-scoped point lookups may table-scan on large datasets | Medium |

---

## 2. Phase-by-Phase Summary

### Phase 1 — Rate Limiting

**What was implemented:**
Three-dimensional rate limiting (IP → userId → tenantId) via a `createRateLimiter` factory producing Express middleware. Three pre-built profiles: `generalLimiter` (100/min), `uploadLimiter` (10/min), `signedUrlLimiter` (30/min). In-memory and Redis-backed providers behind a `RateLimitProvider` interface.

**What works well:**
- Three dimensions checked in sequence with early exit prevents a single user from consuming the entire tenant quota.
- `Retry-After`, `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` headers set correctly on every response including non-429s.
- Rate limit error carries `limitDimension` so clients know which bucket fired.
- Redis provider uses atomic `INCR + EXPIRE` pattern; no race conditions.

**What is incomplete or risky:**
- In-memory provider is per-process. Two replicas behind a load balancer each allow 100 requests/min, giving an effective limit of 200 per minute per user. Documented but must be enforced by deployment policy.
- The redemption endpoint `GET /access/:token` has no rate limiter applied. A burst of 64-char hex token guesses would not be throttled. Entropy makes brute force infeasible (2^256 space) but replay detection (e.g. failed-redemption counter) is absent.
- IP normalisation comment states "hashes the result so raw IPs are not stored (GDPR)" but the implementation does not hash. Raw IPs are stored as Redis keys. This is a documentation-implementation discrepancy with GDPR implications.

**Deviations from requirements:**
- None functional. Rate limiting is applied correctly to all relevant endpoints.

---

### Phase 2 — Malware Scanning

**What was implemented:**
`ScanService` orchestrates synchronous inline scanning during upload. Three providers: `NullScanner` (pass-through), `MockScanner` (configurable results for testing), `ClamAV` (real scanner via TCP socket). Scan result is persisted to `scan_status` + `scan_threats` on the document and version rows. Access gating via `ScanService.enforceCleanScan` blocks access to INFECTED, and optionally PENDING/FAILED documents.

**What works well:**
- Scan runs before the file is committed to storage (`assertNotInfected` throws before `storage.upload`). Infected files never reach persistent storage.
- `INFECTED` status blocks access unconditionally regardless of `REQUIRE_CLEAN_SCAN_FOR_ACCESS` setting.
- Scan lifecycle is fully audited (`SCAN_REQUESTED`, `SCAN_COMPLETED`/`SCAN_FAILED`/`SCAN_INFECTED`).
- Threat names are logged only as `threatCount`, never as their string values — eliminates any risk of PHI/PII leaking through threat signatures.

**What is incomplete or risky:**
- Scanning is synchronous and blocking. A 50 MB upload to a slow ClamAV instance (or via network) will block the request for the full scan duration with no timeout other than the HTTP server timeout. Under concurrent heavy uploads this creates a thread/event-loop bottleneck.
- There is no quarantine bucket. The design note mentions a future async path (quarantine → scan → clean bucket) but it is not implemented. If the scan fails mid-upload due to a ClamAV crash, the file is not persisted but no quarantine record exists.
- `ClamAvFileScannerProvider` connects to ClamAV on every request (no connection pooling). In production, this should use a persistent connection or socket pool.
- If `FILE_SCANNER_PROVIDER=none`, scan status is set to `SKIPPED` and access is immediately allowed. This is correct behaviour but is a security-relevant default: new deployments with no scanner env var set will silently skip all malware scanning.

**Deviations from requirements:**
- No async scanning path implemented. Acceptable for a first version, documented as a known gap.

---

### Phase 3 — Access Mediation (Token-Based Secure Access)

**What was implemented:**
`AccessTokenService` issues 32-byte hex opaque tokens stored in `AccessTokenStore` (memory or Redis). Clients exchange tokens at `GET /access/:token` — an unauthenticated endpoint. Token redemption re-validates the document's scan status before generating a very short-lived (30s) storage presigned URL. One-time-use enforcement is atomic via Redis Lua script. `DIRECT_PRESIGN_ENABLED=false` by default (secure mode).

**What works well:**
- Storage keys and bucket names are never exposed to clients in either mode. Sanitiser strips them from all document/version responses.
- 302 redirect architecture: the service generates the presigned URL internally and redirects the client, keeping the actual storage key internal.
- Re-fetch + re-scan-check at redemption time provides defence-in-depth: a document quarantined after token issuance is blocked at redemption.
- Atomic Lua `markUsed` prevents replay attacks in Redis-backed deployments.
- `issuedFromIp` is stored in the token for forensic audit purposes.

**What is incomplete or risky:**
- IP binding is stored but not enforced on redemption. A token issued to IP `1.2.3.4` can be redeemed from IP `9.9.9.9` without rejection. For sensitive documents this is a meaningful gap.
- Token redemption (`GET /access/:token`) is unauthenticated by design, but there is no rate limit on this endpoint. An attacker who intercepts a token string (e.g. via log exposure) has unlimited attempts to replay it before expiry.
- When `ACCESS_TOKEN_ONE_TIME_USE=false`, a stolen token can be reused any number of times until expiry. The default is `true` (one-time use), but this can be overridden per deployment.
- Redemption audit failure (DB down) does not prevent the redirect. The client receives the file but the access event is not guaranteed to be logged.
- `redeemUrl` returned by `/documents/:id/view-url` is a relative path (`/access/:token`). In a frontend consuming this, the base URL must be the service's own URL — documentation of this coupling is absent.

**Deviations from requirements:**
- None. Token mediation matches the spec. The IP-binding gap is a hardening recommendation, not a spec deviation.

---

### Phase 4 — Redis Backing

**What was implemented:**
`RedisRateLimitProvider` and `RedisAccessTokenStore` backed by a shared `ioredis` singleton with lazy connect, `enableOfflineQueue: false`, configurable timeouts, and structured error logging. Factories detect Redis unavailability and fall back to in-memory providers. `isRedisHealthy()` available for health checks.

**What works well:**
- `enableOfflineQueue: false` ensures Redis failures fail fast rather than building up an unbounded memory queue.
- Graceful fallback: if `REDIS_URL` is not set, factories silently use in-memory providers with a log warning — service starts successfully.
- Atomic Lua script for `markUsed` is correct and handles the concurrency case precisely.
- Connection events are logged at appropriate levels (`info`, `warn`, `error`).

**What is incomplete or risky:**
- Fallback from Redis to memory is silent. An operator may not notice that rate limiting or token enforcement is now per-process, not cluster-wide. The fallback should emit a `REDIS_FALLBACK_ACTIVE` metric or alert.
- There is no Redis reconnection alert escalation. If Redis is down for >5 minutes, the in-memory fallback is actively engaged but ops has no alert unless they monitor logs.
- `resetRedisClient()` is exposed for test isolation but could be called accidentally in production code (no guard). It resets the singleton without closing the connection, creating a potential connection leak.
- No Redis Sentinel or Cluster support configured. Single-node Redis is a single point of failure for access token state.

**Deviations from requirements:**
- None.

---

### Phase 5 — Tenant Isolation (Application Layer)

**What was implemented:**
Three independent layers of isolation:

1. **DB Layer** (`requireTenantId()` + `WHERE tenant_id = ?`): Pre-query guard throws `TenantIsolationError` before SQL runs if `tenantId` is null/empty/whitespace. Every `DocumentRepository` method calls this as its first statement. All SQL predicates include `AND tenant_id = $N`.

2. **Service Layer** (`assertDocumentTenantScope()`): Post-load ABAC guard in every `DocumentService` method that retrieves a document. Blocks non-admin cross-tenant access with `TenantIsolationError`. Logs `ADMIN_CROSS_TENANT_ACCESS` for PlatformAdmin.

3. **Route Layer** (`assertTenantScope()`): Pre-flight check on routes where `tenantId` is present in the request body (POST /documents). Blocks obviously mis-scoped requests before service layer is reached.

**What works well:**
- The three-layer model is independently redundant. Any single layer failing (due to a code bug, new developer, or refactoring) still leaves two others.
- Cross-tenant responses are always 404, never 403 — prevents tenant ID enumeration by confirming whether a resource exists.
- `resolveEffectiveTenantId()` silently ignores `X-Admin-Target-Tenant` from non-admin callers with a security warning log — non-admin callers cannot override their tenantId regardless of what header they send.
- Admin cross-tenant access is always audited with `ADMIN_CROSS_TENANT_ACCESS`, including `actorTenantId`, `resourceTenantId`, `resourceId`, `ipAddress`, `correlationId`.
- The `createVersion` UPDATE bug (missing `AND tenant_id = $10`) was caught during implementation and fixed — the bug would have allowed a rogue `documentId` to update a document row in a different tenant.

**What is incomplete or risky:**
- Layer 1 (`assertTenantScope`) is only applied to `POST /documents`. Routes that receive a document ID in the URL path (`GET /documents/:id`, `PATCH /documents/:id`, `DELETE /documents/:id`) do not call `assertTenantScope()` at the route layer. They rely entirely on Layers 2 and 3. This is architecturally acceptable (3 layers, 2 still active) but means a new developer may not see Layer 1 being consistently applied and could write a new route that only has one layer.
- `tenantQuery()` and `tenantQueryOne()` helpers exist but `DocumentRepository` does not use them — it calls `requireTenantId()` directly then uses raw `query()`. This is correct but a future developer could add a new method using raw `query()` without `requireTenantId()` and the helper would never enforce it.
- There is no ESLint rule or static analysis guard preventing calls to raw `query()` without a preceding `requireTenantId()` in repository files. This is documented as a gap but not mitigated by tooling.
- `softDelete` does not return the number of affected rows. If called with a cross-tenant `documentId` that matches 0 rows (due to tenant filter), it returns `void` silently. The caller calls `findById` first, creating a TOCTOU window (document could be deleted between findById and softDelete by a concurrent call).

**Deviations from requirements:**
- **Critical deviation:** The analysis and documentation consistently describe this as a "MySQL-safe" implementation. However, the actual database schema (`migrate.ts`) uses PostgreSQL-specific DDL throughout:
  - `UUID` column type (MySQL uses `CHAR(36)` or `BINARY(16)`)
  - `TIMESTAMPTZ` (MySQL uses `DATETIME` or `TIMESTAMP`)
  - `JSONB` type (MySQL uses `JSON` with different indexing semantics)
  - `TEXT[]` array type (MySQL has no native array column type)
  - `gen_random_uuid()` function (MySQL uses `UUID()`)
  - PostgreSQL trigger with `RETURNS TRIGGER LANGUAGE plpgsql` syntax
  - Partial indexes with `WHERE` clause (supported in MySQL 8.0+ only with limitations)
  
  This system **cannot be deployed to MySQL without a complete schema rewrite**. The tenant isolation strategy is database-agnostic (application-layer code), but the data layer is PostgreSQL-specific. This discrepancy must be resolved in documentation or the schema must be migrated.

---

### Phase 6 — Tenant Safety Guards

**What was implemented:**
`tenant-query.ts` provides `requireTenantId()`, `tenantQuery()`, and `tenantQueryOne()` as defensive DB layer helpers. `tenant-guard.ts` provides `assertDocumentTenantScope()` and `resolveEffectiveTenantId()` as service-layer ABAC guards.

**What works well:**
- `requireTenantId()` is called as the very first statement in every repository method before any string building or SQL execution. This ensures no query runs with a missing tenantId regardless of the caller's state.
- `TenantIsolationError` is a distinct error class (not a generic `ForbiddenError`) — monitoring can alert specifically on cross-tenant attempts vs ordinary permission denials.
- The guard pattern is well-documented and the comments explain the "why" clearly for future developers.

**What is incomplete or risky:**
- The `tenantQuery()` / `tenantQueryOne()` helpers are not used by `DocumentRepository` — they exist but the repository calls `requireTenantId()` + raw `query()` directly. The helpers are therefore "available but not enforced" — a future developer could use raw `query()` and bypass both the helper and the guard if they don't notice the convention.
- No automated enforcement exists (no lint rule, no PR check). The guard pattern depends entirely on developer discipline and code review.
- `assertDocumentTenantScope()` makes an async audit log write on every PlatformAdmin cross-tenant access. If the audit DB is slow, admin cross-tenant operations will be slow. This is acceptable for an admin path but worth noting.

---

### Phase 7 — Integration Test Suite

**What was implemented:**
97 integration tests across 7 suites against a real PostgreSQL database (`heliumdb`), local storage, and HS256 JWT auth. Covers: auth (25), RBAC (22), tenant isolation (21), upload validation (14), access control (19), rate limiting (9), audit trail (28).

**What works well:**
- Global setup runs 6 DB migrations idempotently — tests are self-contained and repeatable.
- Tenant isolation test suite covers all three layers: cross-tenant read (404), cross-tenant delete (404), cross-tenant update (404), admin cross-tenant with audit verification, and requests with missing tenantId.
- Audit suite verifies event content, ordering, and immutability (UPDATE/DELETE triggers).
- Rate limiting tests verify `Retry-After` header, `limitDimension` field, and per-user bucket isolation.

**What is incomplete or risky (known gaps):**
1. RBAC denial events are not written to `document_audits`. The integration test for RBAC verifies 403 responses but not that the denial was audited.
2. Async scan flow (quarantine → worker → update) is not testable in this test suite — no queue infrastructure is present.
3. Redis-backed rate limiter and access token store are tested only in unit tests (mocked). Integration tests use memory providers.
4. GCS and S3 storage providers have no integration tests — only local storage is exercised.
5. JWKS-based JWT validation has no integration test — only HS256 symmetric secret is tested.
6. The `DIRECT_PRESIGN_ENABLED=true` code path has no integration test.
7. Legal hold enforcement is tested (document with `legalHoldAt` rejects DELETE) but the mechanism for setting legal hold (PATCH with `status: LEGAL_HOLD`) is not tested end-to-end through the API.
8. No load or concurrency tests — race conditions in `createVersion` (SERIALIZABLE transaction) are not exercised.

---

## 3. Architecture Validation

### Separation of Concerns

The layered structure is well-observed:

```
api/               ← HTTP concerns only: routing, validation, auth middleware, error mapping
  middleware/      ← cross-cutting: auth, rate-limit, file-validator, error-handler
  routes/          ← thin handlers: parse input, call service, serialize output
application/       ← domain logic: document-service, rbac, tenant-guard, scan-service, audit-service, access-token-service
  (no infrastructure imports except via interfaces)
domain/            ← pure types and interfaces: no implementations
infrastructure/    ← implementations: DB, storage, scanner, auth, Redis, rate-limit, secrets
shared/            ← config, errors, constants, logger — used by all layers
```

**Finding:** This separation is largely clean. However, `document-service.ts` imports directly from `@/infrastructure/database/document-repository` and `@/infrastructure/storage/storage-factory`. Purist DDD would inject these via interfaces. The current approach is pragmatic and does not cause testability issues (factories are tested via mocking), but it does create hidden coupling to concrete infrastructure in the application layer.

**Finding:** `ScanService` calls `DocumentRepository.updateDocumentScanStatus` directly — the application service reaches into infrastructure. This is a layering violation (application → infrastructure direct call without interface) but is pragmatic for the synchronous scan path.

### Provider Abstraction Integrity

All five provider interfaces (`StorageProvider`, `AuthProvider`, `RateLimitProvider`, `FileScannerProvider`, `AccessTokenStore`) are clean domain interfaces with no infrastructure imports. Concrete implementations (`S3StorageProvider`, `JwtAuthProvider`, etc.) are accessed only through factory functions (`getStorageProvider()`, `getAuthProvider()`). Factories read `config.*_PROVIDER` env vars at runtime — no provider-specific code leaks into callers.

**One gap:** `LocalStorageProvider.generateSignedUrl` returns a relative URL (`/internal/files?token=...`) that assumes a specific internal routing convention. This is not specified in the `StorageProvider` interface contract — the interface only promises a `string` return. A GCS/S3 caller would return an absolute HTTPS URL. Callers (e.g., `AccessTokenService.redeem`) use the URL as a redirect target without distinguishing relative from absolute. In local dev this works; a broken URL could silently fail in a cross-environment test.

### Cloud-Agnostic Design

| Concern | Assessment |
|---------|------------|
| Storage (S3 / GCS / local) | ✅ Cleanly abstracted; switch via `STORAGE_PROVIDER` env var |
| Auth (JWKS / HS256 symmetric / mock) | ✅ Switch via `AUTH_PROVIDER` + JWT env vars |
| Scanner (ClamAV / mock / none) | ✅ Switch via `FILE_SCANNER_PROVIDER` |
| Rate limiting (Redis / memory) | ✅ Switch via `RATE_LIMIT_PROVIDER` |
| Access token store (Redis / memory) | ✅ Switch via `ACCESS_TOKEN_STORE` |
| Secrets (env / AWS Secrets Manager / GCP SM) | ✅ Interface defined; env implementation complete |
| Database | ❌ PostgreSQL-specific DDL; not AWS RDS MySQL / Aurora MySQL compatible |

**GCP readiness:** Switching from S3 to GCS requires `GCS_BUCKET_NAME`, `GCS_PROJECT_ID`, `GCS_KEY_FILE_PATH` env vars and `STORAGE_PROVIDER=gcs`. The JWKS URI can point to a GCP OIDC endpoint. Rate limiting and token store switch to Redis (Cloud Memorystore). The blocker is the PostgreSQL requirement — GCP Cloud SQL for PostgreSQL is supported; Cloud Spanner or Cloud SQL for MySQL is not.

### Dependency Isolation

- No circular imports (infrastructure does not import from application; application does not import from api).
- `zod` is used at the API layer only for request validation — no domain entities depend on it.
- `pino` is shared via the `logger` singleton — acceptable.
- `pg` (PostgreSQL client) is imported only inside `src/infrastructure/database/db.ts` — not leaked into other layers.

---

## 4. Security Review

### JWT Authentication

**Correctness:** Strong. `JwtAuthProvider` supports both JWKS (RS256/ES256) and symmetric secret (HS256). Issuer and audience validation are configurable and applied. The `extractPrincipal` method validates that both `sub` and `tenantId` claims are present before returning a principal.

**Gaps:**
1. `tenantId` claim is not validated to be a UUID format. A valid JWT with `tenantId: "' OR 1=1--"` would pass principal extraction and reach repository code. `requireTenantId()` checks for empty/null/whitespace but not UUID format. SQL injection is mitigated by parameterised queries, but tenantId shape validation belongs at the JWT extraction boundary.
2. `roles` claim defaults to `[]` (empty array) if absent — this silently grants no permissions and results in 403 on every resource request. This is safe (fails closed) but offers no diagnostic feedback.
3. JWKS cache is 1 hour. A signing key rotation or revocation after compromise would take up to 60 minutes to propagate.

### RBAC + ABAC Enforcement

**RBAC (role-permissions matrix):**
```
PlatformAdmin: read, write, delete, admin
TenantAdmin:   read, write, delete
DocManager:    read, write, delete
DocUploader:   read, write
DocReader:     read
```
Default-deny model: a role not in the matrix gets no permissions. Unknown roles have no entry and produce `undefined`, which `.includes()` treats as no match — correctly denied.

**ABAC (`assertDocumentTenantScope`):**
Correct and consistent. Applied in every `DocumentService` method that loads a document by ID. The only exception is `DocumentService.list()` which does not call `assertDocumentTenantScope` — it cannot, as no single document is loaded. The tenant filter is applied entirely at the DB layer (`WHERE tenant_id = $1`) for list operations, which is correct.

**Gap:** `assertPermission` does not audit denied access attempts. A `DocReader` attempting to `DELETE` a document gets a 403 but no `ACCESS_DENIED` audit event is emitted at the RBAC layer. Denied access at the permission level is not in the audit trail, only missing/invalid tokens are.

### Rate Limiting Effectiveness

Effective at preventing individual-user and tenant-wide burst traffic. Three dimensions provide defence against shared-IP attackers. The 10/min upload limit is appropriate for a document management system.

**Gaps:** See Phase 1 risks. Primary concern is per-process in-memory mode in multi-replica deployments. The `/access/:token` endpoint is unprotected by any rate limiter.

### File Validation Strength

**MIME whitelist:** 10 allowed types covering PDF, Word, Excel, images, text, CSV.

**Magic byte validation:** Uses `file-type` v16.5.4 (last CJS release). The library reads the file buffer's first few bytes to determine actual file type, independent of `Content-Type` header. JPEG/JPG alias is handled explicitly. Files that are not detectable by magic bytes (plain text, CSV) fall back to the declared MIME type — this is correct since text files have no magic bytes.

**Gap — extension extraction in `buildStorageKey`:** The storage key is built using `originalname.split('.').pop()`. A file named `document.pdf.exe` would get extension `exe` in the key, which is cosmetic but incorrect. More seriously, a file named `.bashrc` (no extension) would produce `bashrc` as the extension. This is a cosmetic issue (the MIME validation has already passed), not a security issue, but the key is misleading.

**Gap — `LocalStorageProvider` path traversal:** The sanitisation `key.replace(/\.\./g, '_')` blocks `../` but does not handle absolute paths (`/etc/passwd` → stored under `basePath///etc/passwd`), URL-encoded sequences (`%2e%2e`), or null bytes. This is dev-only, but the gap should be documented.

### Malware Scanning Enforcement

Correctly enforced synchronously before storage upload. `INFECTED` always blocks. `PENDING`/`FAILED` block when `REQUIRE_CLEAN_SCAN_FOR_ACCESS=true`. Double-checked at token redemption time. Audit events emitted for all outcomes.

**Gap:** `FILE_SCANNER_PROVIDER=none` is the default. A misconfigured production deployment with no scanner env var will silently pass all files with `scanStatus=SKIPPED`. An operator check on startup (logging `WARN: file scanning disabled`) would reduce this risk, but no startup warning is currently emitted.

### Access Mediation Correctness

**Direct storage URL exposure:** `DIRECT_PRESIGN_ENABLED=false` (default). In this mode, clients never receive storage URLs, bucket names, or keys. The `sanitizeDocument` function strips `storageKey`, `storageBucket`, `checksum` from all API responses.

**`DIRECT_PRESIGN_ENABLED=true`:** When enabled, `DocumentService.generateSignedUrl` is called, which produces a cloud storage presigned URL. This URL typically encodes the bucket name and key in the URL structure (S3: `https://bucket.s3.amazonaws.com/key?X-Amz-Signature=...`). In this mode, storage internals are partially exposed. This mode is documented as "legacy compat" and should be deprecated.

**Token security:**
| Property | Status |
|----------|--------|
| Expiry | ✅ Configurable `ACCESS_TOKEN_TTL_SECONDS`, default 5 minutes |
| One-time use | ✅ Default `true`; atomic Lua enforcement in Redis |
| Token entropy | ✅ `crypto.randomBytes(32)` = 64 hex chars = 256 bits |
| Storage | ✅ In-memory or Redis; never in JWT or cookie |
| Token in logs | ✅ Not logged (Pino `redact` covers `token` field) |
| IP binding on redemption | ❌ Stored but not enforced |

### Data Exposure Risk

**Storage keys in responses:** Stripped by sanitiser. ✅  
**Threat names in logs:** Only `threatCount` logged. ✅  
**JWT payload in logs:** Redacted. ✅  
**File content in logs:** Not logged. ✅  
**tenantId in error responses on cross-tenant access:** Not leaked — generic message returned. ✅  
**IP addresses in audit log:** Stored in `ip_address` column. Acceptable for audit trail but IP is PII under GDPR. No pseudonymisation applied. ⚠️

### Critical Vulnerabilities

| # | Vulnerability | Impact |
|---|---------------|--------|
| C1 | `AUTH_PROVIDER=mock` accepts any base64 JSON as a valid principal with any tenantId and any roles | Complete authentication and authorisation bypass; any attacker who sets a `Bearer <base64_json>` token with `PlatformAdmin` role gets full cross-tenant access |
| C2 | Audit insert is non-fatal — a DB outage causes audit events to be silently lost | In HIPAA context, missing audit records of PHI access violate 45 CFR §164.312(b); no alerting on audit failure |

### Medium Risks

| # | Risk | Impact |
|---|------|--------|
| M1 | IP binding not enforced on token redemption | Stolen access token usable from any IP until expiry |
| M2 | No rate limit on `/access/:token` endpoint | Concurrent token replay attempts are not throttled |
| M3 | Raw IPs stored in Redis and audit table without hashing | GDPR compliance gap; documented as hashed but not hashed |
| M4 | JWKS cache TTL 1 hour | Compromised signing key stays trusted up to 60 minutes after rotation |
| M5 | `DIRECT_PRESIGN_ENABLED=true` exposes bucket and key structure | Cloud storage internals partially revealed |

### Minor Improvements

| # | Issue |
|---|-------|
| m1 | `tenantId` claim not validated as UUID format at JWT extraction |
| m2 | RBAC denials not written to `document_audits` |
| m3 | No startup warning when `FILE_SCANNER_PROVIDER=none` |
| m4 | `AWS_BUCKET_NAME` falls back to literal `'docs-local'` when unset in S3 mode |
| m5 | `softDelete` returns `void` with no row-count check; TOCTOU window on concurrent delete |

---

## 5. Compliance & Audit Readiness

### Audit Trail Completeness

| Event | Audited | Notes |
|-------|---------|-------|
| `DOCUMENT_CREATED` | ✅ | Includes MIME type, file size, scan status |
| `DOCUMENT_UPDATED` | ✅ | Includes changed fields |
| `DOCUMENT_DELETED` | ✅ | |
| `DOCUMENT_STATUS_CHANGED` | ✅ | |
| `VERSION_UPLOADED` | ✅ | |
| `SCAN_REQUESTED` | ✅ | |
| `SCAN_COMPLETED` / `SCAN_FAILED` / `SCAN_INFECTED` | ✅ | threat count only, not names |
| `SCAN_ACCESS_DENIED` | ✅ | |
| `VIEW_URL_GENERATED` | ✅ (legacy mode only) | Not logged in default access token mode |
| `DOWNLOAD_URL_GENERATED` | ✅ (legacy mode only) | Not logged in default access token mode |
| `ACCESS_TOKEN_ISSUED` | ✅ | |
| `ACCESS_TOKEN_REDEEMED` | ✅ | Includes document access confirmation |
| `ACCESS_TOKEN_EXPIRED` / `ACCESS_TOKEN_INVALID` | ✅ | |
| `DOCUMENT_ACCESSED` (direct authenticated access) | ✅ | |
| `ACCESS_DENIED` (no/invalid token) | ✅ | |
| `ADMIN_CROSS_TENANT_ACCESS` | ✅ | |
| `TENANT_ISOLATION_VIOLATION` | ✅ | |
| RBAC denial (403 from `assertPermission`) | ❌ | Not audited |

**Critical gap:** Direct document access confirmation is only audited via `DOCUMENT_ACCESSED` (GET /content) and `ACCESS_TOKEN_REDEEMED` (token redemption). In `DIRECT_PRESIGN_ENABLED=true` mode, the audit event is `VIEW_URL_GENERATED` or `DOWNLOAD_URL_GENERATED` — these log URL generation, not actual file access. Cloud storage access logs would need to be correlated externally to confirm actual reads. In default token mode, `ACCESS_TOKEN_REDEEMED` reliably captures the moment the client receives the redirect URL, which is the closest proxy for actual file access available without storage-level logging.

### Audit Immutability

✅ PostgreSQL trigger `trg_audit_immutable` prevents `UPDATE` and `DELETE` on `document_audits`. Verified by integration test. Note: this protection only applies to the application's PostgreSQL user — a database superuser can drop the trigger. True immutability requires write-once storage (AWS CloudTrail, GCS Audit Logs, or a separate WORM log service).

### Actual Document Access Logging

In default mode (access tokens): ✅ `ACCESS_TOKEN_REDEEMED` is written when the client receives the redirect URL.

Gap: The event captures URL generation, not the subsequent HTTP GET to the storage URL. If the client receives the redirect but never fetches the file (or fetches it from cache), the log shows access that may not have occurred. This is an inherent limitation of pre-signed URL architectures and is standard in the industry.

### Legal Hold Enforcement

✅ `DocumentService.delete` checks `doc.legalHoldAt` before calling `softDelete` and throws `ForbiddenError` if set. The `LEGAL_HOLD` status is settable via `PATCH /documents/:id` with `status: LEGAL_HOLD`.

Gap: Setting `legalHoldAt` (the timestamp field) vs setting `status = 'LEGAL_HOLD'` are distinct operations. The soft-delete check uses `doc.legalHoldAt` (the timestamp), not `doc.status === 'LEGAL_HOLD'`. A document could have `status = 'LEGAL_HOLD'` without `legalHoldAt` being set (if only the status was patched) and the delete guard would not fire. These two fields need to be set atomically.

### Retention Readiness

⚠️ `retainUntil` column exists but no enforcement logic is implemented. A document with `retainUntil` in the future can still be soft-deleted via the API. The field is stored and returned but not checked before deletion. HIPAA retention requirements (6 years for PHI) cannot be enforced by this field alone.

### PHI/PII Exposure Risks

1. The `detail` JSONB column in `document_audits` stores arbitrary `input.detail` payloads. The `DOCUMENT_UPDATED` event stores `{ changes: input }`, which includes whatever fields were in the PATCH body (title, description, etc.). If document titles or descriptions contain PHI, the audit log contains PHI.
2. `userAgent` strings are logged in the audit trail. While not PHI themselves, they could contribute to re-identification in combination with other fields.
3. IP addresses in the audit trail are PII under GDPR.

---

## 6. Cloud & Infrastructure Portability

### StorageProvider Abstraction

| Provider | Upload | Signed URL | Delete | Exists |
|----------|--------|-----------|--------|--------|
| LocalStorageProvider | ✅ | ✅ (internal token) | ✅ | ✅ |
| S3StorageProvider | ✅ | ✅ (AWS SDK v3) | ✅ | ✅ |
| GCSStorageProvider | ✅ | ✅ (GCS SDK) | ✅ | ✅ |

The interface is consistent and correctly abstracted. One concern: `LocalStorageProvider.generateSignedUrl` returns a relative path (`/internal/files?token=...`) while S3/GCS return absolute HTTPS URLs. If the `redirectUrl` from token redemption is used in a redirect, a relative URL depends on the Express server returning `302` with the path — which works, but it is a behaviour difference across providers not documented in the interface.

### Redis Optionality

✅ Redis is fully optional. Both rate limiting and access token store fall back to in-memory providers when `REDIS_URL` is absent. The fallback is silent — no alert or health status change is visible at the API level.

### Secrets Management

✅ Three secrets providers: `env` (default), `aws-sm` (AWS Secrets Manager), `gcp-sm` (GCP Secret Manager). Interface is defined (`SecretsProvider`). Only the `env` provider is fully implemented; `aws-sm` and `gcp-sm` exist in the factory's switch but their implementation status is not visible in the reviewed code. Assuming they are stub/partial implementations, this must be verified before production deployment.

### Ability to Run

| Environment | Status |
|-------------|--------|
| Local (no cloud) | ✅ `STORAGE_PROVIDER=local`, `AUTH_PROVIDER=jwt` with `JWT_SECRET`, no Redis |
| AWS | ✅ `STORAGE_PROVIDER=s3`, JWKS from Cognito, `RATE_LIMIT_PROVIDER=redis`, `SECRETS_PROVIDER=aws-sm` |
| GCP | ✅ `STORAGE_PROVIDER=gcs`, JWKS from Firebase Auth, `RATE_LIMIT_PROVIDER=redis` (Cloud Memorystore), `SECRETS_PROVIDER=gcp-sm` |
| MySQL | ❌ Schema requires complete DDL rewrite |

### Cloud Lock-In Risks

- **None at the application layer.** All cloud SDK calls are isolated inside provider implementations.
- **Mild: LocalStorageProvider** references `LOCAL_STORAGE_PATH` and serves files via an internal express route `/internal/files`. This is dev/test only and correctly documented as "NOT suitable for production."

---

## 7. Data Layer & Tenant Isolation (MySQL — CRITICAL)

> **Important finding:** Despite documentation describing this as a "MySQL-based" and "MySQL-safe" implementation, the actual schema is **PostgreSQL-specific**. This section evaluates the application-level isolation strategy (which is genuinely database-agnostic) but calls out the schema incompatibility explicitly.

### How Tenant Isolation Is Enforced

#### Repository Layer

Every `DocumentRepository` method follows this pattern:

```typescript
async findById(id: string, tenantId: string): Promise<Document | null> {
  requireTenantId(tenantId, 'DocumentRepository.findById');    // 1. Guard
  return queryOne(
    `SELECT * FROM documents WHERE id = $1 AND tenant_id = $2 ...`,
    [id, tenantId],                                            // 2. SQL predicate
  );
}
```

The guard fires before SQL is built. The SQL predicate ensures the DB never returns a cross-tenant row. Both defences must fail simultaneously for a cross-tenant data leak.

#### Service Layer

Every `DocumentService` method that loads a document calls `assertDocumentTenantScope()` after the repository returns, before the document data reaches the route handler.

### Does Every Query Enforce Tenant Filtering?

| Method | `requireTenantId` | `WHERE tenant_id = ?` |
|--------|-------------------|----------------------|
| `findById` | ✅ | ✅ |
| `list` | ✅ | ✅ (first WHERE condition) |
| `create` | ✅ | ✅ (INSERT column) |
| `update` | ✅ | ✅ (`AND tenant_id = $2`) |
| `softDelete` | ✅ | ✅ (`AND tenant_id = $2`) |
| `createVersion` (SELECT) | ✅ | ✅ |
| `createVersion` (UPDATE documents) | ✅ | ✅ (`AND tenant_id = $10`) — bug fix applied |
| `listVersions` | ✅ | ✅ |
| `updateDocumentScanStatus` | ✅ | ✅ |
| `updateVersionScanStatus` | ✅ | ✅ |

**Result: All 10 repository methods enforce tenant filtering at both the guard level and the SQL level.**

### Do Repository Methods Require tenantId?

Yes. Every method signature requires `tenantId: string` as a mandatory parameter. There is no default and no optional `tenantId`. Type system prevents calling these methods without providing the parameter.

### Can Unsafe/Raw Queries Be Executed?

`db.ts` exports raw `query()` and `queryOne()` functions which do not require tenantId. A future developer could call these directly. Nothing in the language or tooling prevents this. The only safeguard is:
1. `requireTenantId()` is checked inside every existing method.
2. `tenantQuery()` / `tenantQueryOne()` helpers exist as safer alternatives.
3. Code review convention.

This remains the primary residual risk of the no-RLS model.

### Scenarios Where Cross-Tenant Data Leakage Could Occur

| Scenario | Risk Level | Current Mitigation |
|----------|------------|-------------------|
| New repository method written without `requireTenantId` | **High** | Code review only; no static analysis |
| New repository method with wrong parameter order (tenantId ends up as wrong param in SQL) | **High** | No enforcement; parameterised queries prevent injection but not wrong value |
| `withTransaction` block adds a raw `client.query()` without tenant filter | **Medium** | Fixed in `createVersion`; no automated check for future uses |
| PlatformAdmin token compromise | **Critical** | Audited; short TTL recommended; no additional protection |
| `DIRECT_PRESIGN_ENABLED=true` with a guessable storage key | **Low** | Keys include `tenantId/docId/timestamp.ext` — not guessable without the document ID |
| A future endpoint that accepts `documentId` but reads audit logs without tenant scoping | **Medium** | `AuditRepository.listForDocument` includes `AND tenant_id = ?` — currently correct |

### Recommended Fixes

1. **Add ESLint rule:** Prohibit calls to raw `query()` / `queryOne()` in `**/database/*-repository.ts` files (only `tenantQuery()`/`tenantQueryOne()` or explicit `requireTenantId()` before `query()` allowed).
2. **Switch existing repository methods to use `tenantQuery()` helpers** instead of `requireTenantId()` + raw `query()` — makes the pattern consistent and harder to accidentally omit.
3. **Add `retainUntil` enforcement** at the `softDelete` boundary: reject if `retainUntil > now`.
4. **Atomic `legalHoldAt` + `status` update:** Ensure setting `status = 'LEGAL_HOLD'` always sets `legalHoldAt = NOW()` in a single SQL statement.

---

## 8. Testing Coverage Review

### Unit Tests (161 tests, 7 suites)

| Suite | Tests | Assessment |
|-------|-------|------------|
| errors.test.ts | 28 | ✅ Complete — all error classes, HTTP codes, error codes |
| rbac.test.ts | 22 | ✅ Complete — full RBAC matrix, assertTenantScope, all roles |
| malware-scanning.test.ts | 27 | ✅ Good — all three providers, scan gate, ScanService lifecycle |
| access-mediation.test.ts | 20 | ✅ Good — issue/redeem/one-time-use, scan gate integration |
| redis-backing.test.ts | 23 | ✅ Good — RedisRateLimiter, RedisAccessTokenStore, fallback |
| tenant-isolation.test.ts | 22 | ✅ Complete — all three layers, admin cross-tenant, nil UUID, resolveEffectiveTenantId |
| rate-limiting.test.ts | 19 | ✅ Good — all three dimensions, headers, 429 responses |

**Missing unit test coverage:**
- `DocumentRepository` methods — no unit tests for the data layer
- `AuditRepository.insert` — non-fatal swallow path not unit tested
- `buildStorageKey` — extension extraction edge cases not tested
- `MockAuthProvider` — present but no guard preventing production use
- `config.ts` validation — schema rejection on invalid env vars not tested

### Integration Tests (97 tests, 7 suites)

| Suite | Tests | Assessment |
|-------|-------|------------|
| auth.test.ts | 25 | ✅ Comprehensive |
| rbac.test.ts | 22 | ✅ Complete RBAC matrix |
| tenant-isolation.test.ts | 21 | ✅ Critical — all isolation paths covered |
| upload-validation.test.ts | 14 | ✅ Good |
| access-control.test.ts | 19 | ✅ Good |
| rate-limiting.test.ts | 9 | ⚠️ Limited — memory provider only; Redis not tested |
| audit.test.ts | 28 | ✅ Comprehensive — events, immutability, ordering |

### Tenant Isolation Coverage (CRITICAL)

| Test | Covered |
|------|---------|
| Tenant A reads Tenant B document → 404 | ✅ |
| Tenant A deletes Tenant B document → 404 | ✅ |
| Tenant A updates Tenant B document → 404 | ✅ |
| Cross-tenant response is 404, not 403 | ✅ |
| Admin cross-tenant access succeeds | ✅ |
| Admin cross-tenant access is audited | ✅ |
| Non-admin `X-Admin-Target-Tenant` header is silently ignored | ✅ |
| Missing tenantId in token → 401 | ✅ |
| `TENANT_ISOLATION_VIOLATION` audit event emitted on attempt | ✅ |
| `requireTenantId` empty string → error before query | ⚠️ Unit tested, not integration tested |
| Version upload cross-tenant blocked | ❌ Not tested |
| Access token issued for Tenant A document cannot be used to access Tenant B data | ❌ Not explicitly tested |

### Missing Critical Tests

1. Version upload cross-tenant — `POST /documents/:id/versions` where document belongs to another tenant.
2. Token replay after one-time use — explicitly redeeming a used token and verifying 401.
3. Redis-backed rate limiter integration — currently only memory limiter is integration tested.
4. `DIRECT_PRESIGN_ENABLED=true` mode — presigned URL path is untested in integration.
5. `retainUntil` field — stored and returned but enforcement is absent; test would expose this.
6. Concurrent version uploads — race condition in `createVersion` transaction is not stress tested.
7. RBAC denial audit events — 403 responses not verified to produce audit records (because they don't — see Section 4).

---

## 9. Performance & Scalability Risks

### Database Query Performance

| Concern | Assessment |
|---------|------------|
| Point lookup `WHERE id = $1 AND tenant_id = $2` | ⚠️ No composite index on `(id, tenant_id)`. PK lookup by `id` alone is O(log n), but the `AND tenant_id = $2` predicate must then be verified. On a large table this is fine, but a composite index would be marginally faster. |
| `WHERE tenant_id = $1 AND is_deleted = FALSE` (list) | ✅ Covered by `idx_documents_tenant` (partial index on non-deleted rows) |
| `WHERE tenant_id = $1, product_id = $2` filter | ✅ Covered by `idx_documents_product` |
| `WHERE tenant_id = $1, reference_id = $2` filter | ✅ Covered by `idx_documents_reference` |
| `WHERE document_id = $1 AND tenant_id = $2` (versions) | ⚠️ `idx_versions_document` only covers `document_id`; tenant filter is unindexed for version queries |
| Audit log query `WHERE document_id = $1 AND tenant_id = $2` | ✅ Two indexes available (`idx_audits_document`, `idx_audits_tenant`) |
| Count query on list endpoint | ⚠️ Runs a separate `COUNT(*)` query in parallel — doubles DB round-trips on every list call |

### File Handling

- Files are buffered entirely in memory by Multer (`memoryStorage()`). A 50 MB upload with 10 concurrent requests = 500 MB of heap pressure. Under load this will trigger GC pauses or OOM.
- Scanning also operates on the full in-memory buffer. ClamAV scanning of a 50 MB file can take several seconds, blocking the event loop's async I/O queue.
- Recommendation: stream files to a temp path on disk before scanning, or enforce a lower `MAX_FILE_SIZE_MB` for the initial release.

### Rate Limiting Scalability

- Memory provider: per-process, does not scale horizontally. Must be enforced via deployment policy.
- Redis provider: scales horizontally but adds latency (typically 1-2ms per check; three checks per request = 3-6ms overhead per authenticated request).
- Tenant rate limit (`tenantMax = ipMax * 2`) allows a busy tenant to use 200 req/min even though each user is limited to 100. Under a large tenant with many users, tenant-level limits may need tuning.

### Concurrency Issues

- **`createVersion` transaction:** Uses `SELECT ... FOR UPDATE` with a `SERIALIZABLE` isolation level implicit in the explicit locking. This serialises concurrent version uploads for the same document. Under high version-upload frequency for a single document, this becomes a bottleneck.
- **`LocalStorageProvider` token map:** `this.tokens = new Map` in a singleton. Entries are never swept on expiry unless `resolveToken()` is called. Long-lived services with many one-time tokens could accumulate stale entries indefinitely. A TTL sweep or LRU eviction is absent.
- **`AccessTokenService.redeem` race with `markUsed`:** In memory mode, `store.markUsed()` is synchronous and atomic via a flag check. But if the in-memory store is ever accessed from multiple async contexts (e.g., two concurrent requests for the same token), the flag check is not atomic at the Node.js async level — there is a tiny TOCTOU window. The Redis Lua implementation has no such window.

---

## 10. Production Readiness Checklist

| Item | Status | Notes |
|------|--------|-------|
| Authentication | ✅ Ready | JWT with JWKS or symmetric secret; issuer/audience validated |
| Authorization (RBAC) | ✅ Ready | Default-deny, 5 roles, consistent enforcement |
| Authorization (ABAC) | ✅ Ready | Three-layer tenant isolation, admin audit |
| Rate limiting | ⚠️ Needs improvement | Works correctly; per-process in memory mode; `/access/:token` unprotected |
| Malware scanning | ⚠️ Needs improvement | Correct enforcement; sync blocking; default is `none` |
| Audit logging | ⚠️ Needs improvement | Non-fatal; RBAC denials not logged; IP not hashed |
| Access mediation | ✅ Ready | Token mediation by default; storage keys never exposed |
| Tenant isolation (application) | ✅ Ready | Three layers, redundant, well-tested |
| Tenant isolation safeguards | ⚠️ Needs improvement | Guards exist; not enforced by tooling; no lint rule |
| Secrets management | ⚠️ Needs improvement | `env` provider complete; `aws-sm`/`gcp-sm` partial |
| Logging | ✅ Ready | Structured Pino; PHI fields redacted; correlation IDs |
| Observability | ❌ Missing | No metrics endpoint; no health check for DB/Redis/scanner; no tracing |
| Error handling | ✅ Ready | Centralised, typed, internal details not exposed |
| Test coverage | ⚠️ Needs improvement | 258 tests; Redis/S3/GCS/JWKS not integration tested; missing concurrency tests |
| Database schema (MySQL) | ❌ Missing | Schema is PostgreSQL-specific; MySQL deployment not possible |
| `retainUntil` enforcement | ❌ Missing | Field stored but not enforced |
| Legal hold atomicity | ⚠️ Needs improvement | `legalHoldAt` and `status` updated separately; possible inconsistency |

---

## 11. Top 10 Critical Issues

### Issue 1: `AUTH_PROVIDER=mock` is a complete authentication bypass

**Impact:** Critical. Any request with a `Bearer <base64_json>` token claiming any role and any tenantId will be authenticated and fully authorised. If `AUTH_PROVIDER=mock` is accidentally set in a staging or production environment, the service is completely open. There is no config validation that blocks `mock` in non-development environments.

**Fix:** In `auth-factory.ts`, throw a startup error if `AUTH_PROVIDER=mock` and `NODE_ENV !== 'development' && NODE_ENV !== 'test'`. Add a startup log `CRITICAL: mock auth provider active` at warn level.

---

### Issue 2: Audit inserts are non-fatal — access events can be silently lost

**Impact:** Critical in HIPAA context. `AuditRepository.insert` catches all errors and logs them but continues execution. In a scenario where the audit DB is unavailable (connection pool exhausted, failover in progress), document access proceeds but is not logged. For PHI, this violates the HIPAA requirement for audit controls (45 CFR §164.312(b)).

**Fix:** Add a configurable `AUDIT_STRICT_MODE` flag. When `true`, audit failures propagate and the operation is rejected. When `false` (current default), emit a metric/alert (e.g., increment a `docs_audit_failure_total` counter) rather than silently swallowing. At minimum, add a dead-letter mechanism (write to a file or secondary store when primary DB write fails).

---

### Issue 3: Database schema is PostgreSQL-specific; MySQL deployment is impossible

**Impact:** High. All `migrate.ts` DDL uses PostgreSQL-only features (`TIMESTAMPTZ`, `JSONB`, `UUID` type, `TEXT[]`, `gen_random_uuid()`, PL/pgSQL triggers, partial indexes with `WHERE`). The analysis documentation describes the tenant isolation as "MySQL-safe" — the isolation strategy is database-agnostic, but the schema is not. Any team attempting to deploy to MySQL/Aurora MySQL will fail at migration.

**Fix:** Either: (a) remove all claims of MySQL compatibility from documentation and document PostgreSQL as the sole supported database, or (b) create a parallel MySQL migration file that uses MySQL-compatible types (`CHAR(36)` for UUID, `DATETIME(6)` for timestamps, `JSON` for JSONB, no array columns — normalise to join tables, `BINARY` stored procedures for trigger replacement).

---

### Issue 4: `retainUntil` field has no enforcement logic

**Impact:** High. In a legal/medical document service, retention periods are a regulatory requirement. The `retainUntil` column is stored and returned by the API but `DocumentService.delete()` does not check it before soft-deleting. A document can be deleted while its retention period is active.

**Fix:** Add a `retainUntil` check in `DocumentService.delete()`:
```typescript
if (doc.retainUntil && doc.retainUntil > new Date()) {
  throw new ForbiddenError('Document cannot be deleted before retention period ends');
}
```

---

### Issue 5: In-memory access token and rate limit stores are not safe for multi-replica deployment

**Impact:** High. With two service replicas, rate limits are per-replica (effectively doubled). Access tokens issued by replica A are invisible to replica B — token redemption against replica B returns "not found". This causes 401 errors for users in production behind a load balancer.

**Fix:** Make `RATE_LIMIT_PROVIDER=redis` and `ACCESS_TOKEN_STORE=redis` the required defaults for any non-local deployment. Add a health check at startup that verifies Redis connectivity when these are set. Document this requirement explicitly in the deployment guide.

---

### Issue 6: IP addresses are not hashed despite code comment claiming they are

**Impact:** Medium. The `normaliseIp()` function in `rate-limiter.ts` has a code comment that says "Hashes the result so raw IPs are not stored (GDPR / privacy consideration)" but the implementation does not hash. Raw IP addresses are stored as Redis keys and in `document_audits.ip_address`. This is a GDPR compliance gap (IP is personal data) and a documentation-code discrepancy that could mislead a privacy audit.

**Fix:** Either hash the IP (SHA-256 one-way hash, consistent across replicas) before storing in Redis, or remove the misleading comment and document that IPs are stored as-is (and ensure this is covered in the DPIA).

---

### Issue 7: `/access/:token` endpoint has no rate limiter

**Impact:** Medium. While 256-bit token entropy makes brute force infeasible, the endpoint is susceptible to:
- High-volume replay attempts against a known token (e.g., from an intercepted log line)
- DDoS via the unauthenticated endpoint (no token consumption cost to the attacker)

**Fix:** Apply a strict IP-based rate limiter (e.g., 20 requests/min per IP) to the `GET /access/:token` route. This is separate from the general limiter since the endpoint is unauthenticated.

---

### Issue 8: Legal hold check uses `legalHoldAt` timestamp but PATCH sets `status: LEGAL_HOLD` separately

**Impact:** Medium. Two state fields track legal hold: `status = 'LEGAL_HOLD'` and `legalHoldAt IS NOT NULL`. The soft-delete guard checks `doc.legalHoldAt` (the timestamp), not `doc.status`. Setting status to `LEGAL_HOLD` via PATCH does not automatically set `legalHoldAt`. A document with `status = 'LEGAL_HOLD'` and `legalHoldAt = null` can be soft-deleted.

**Fix:** In the `update` method, when `input.status === 'LEGAL_HOLD'`, also set `legalHoldAt = NOW()` in the same UPDATE statement. Treat them as a single atomic state.

---

### Issue 9: No observability — no metrics, no health check, no distributed tracing

**Impact:** Medium. The service has no `/health/ready` or `/health/live` endpoints that check DB connectivity, Redis health, or scanner availability. In production orchestration (Kubernetes, ECS), liveness/readiness probes cannot be configured. Without metrics (request duration, error rate, queue depth, scan duration), SLOs cannot be measured or alerted on.

**Fix (minimum):**
- Add `GET /health` returning `{ db: "ok"|"error", redis: "ok"|"unavailable", scanner: "ok"|"unavailable" }`.
- Emit structured log entries that can be scraped for metrics (Loki/Grafana) or add a `prom-client` middleware for Prometheus.

---

### Issue 10: RBAC permission denials are not audited

**Impact:** Medium. When `assertPermission()` throws `ForbiddenError` (e.g., a `DocReader` attempting to delete a document), the error is returned as a 403 but no audit event is written to `document_audits`. In a HIPAA context, all access attempts — including denied ones — must be auditable. An attacker probing role boundaries would leave no trace in the audit trail.

**Fix:** In `assertPermission()` or in the error handler for `ForbiddenError`, emit an `ACCESS_DENIED` audit event:
```typescript
await auditService.log({
  event: AuditEvent.ACCESS_DENIED,
  outcome: 'DENIED',
  detail: { reason: 'insufficient_permission', action, roles: principal.roles },
  ...
});
```

---

## 12. Recommended Next Backlog

### Security (Priority: Immediate)

- [ ] **Block `AUTH_PROVIDER=mock` in non-dev environments** — startup guard in `auth-factory.ts`
- [ ] **Enforce `retainUntil` in `DocumentService.delete()`** — regulatory requirement
- [ ] **Fix `legalHoldAt`/`status` atomicity** — set both in a single UPDATE
- [ ] **Add rate limiter to `/access/:token` endpoint** — 20 req/min per IP
- [ ] **Audit RBAC denials** — emit `ACCESS_DENIED` from `assertPermission`
- [ ] **IP hashing** — hash IPs before storing in Redis keys and audit log (or remove misleading comment and disclose in DPIA)
- [ ] **Configurable `AUDIT_STRICT_MODE`** — audit failure should alert, not silently continue
- [ ] **`tenantId` UUID format validation** at JWT extraction boundary

### Scalability (Priority: High for Production)

- [ ] **Stream files to temp path instead of full memory buffer** — replace `memoryStorage()` with `diskStorage()` for files > N MB
- [ ] **Document Redis as required for multi-replica** — enforce `RATE_LIMIT_PROVIDER=redis` and `ACCESS_TOKEN_STORE=redis` in non-local configs
- [ ] **Add startup health check for DB, Redis, and scanner connectivity**
- [ ] **Composite index on `(id, tenant_id)` in `documents` table** — faster point lookups with tenant co-predicate
- [ ] **Composite index on `(tenant_id, document_id)` in `document_versions` table**
- [ ] **Sweep expired tokens from `LocalStorageProvider.tokens` map** — prevent unbounded memory growth in long-running local instances

### Compliance (Priority: High for HIPAA/GDPR)

- [ ] **`retainUntil` enforcement** — see Security above
- [ ] **RBAC denial audit events** — see Security above
- [ ] **`document_audits.detail` PHI review** — ensure title/description in DOCUMENT_UPDATED events are acceptable to include in audit log
- [ ] **Write-once audit log** — back audit trail with append-only S3 / GCS bucket or CloudTrail-equivalent in addition to DB trigger
- [ ] **Document data residency** — specify which region(s) data at rest is acceptable in; enforce via storage bucket region configuration
- [ ] **JWKS cache TTL reduction** — reduce from 1 hour to 5-15 minutes for faster key rotation response

### Developer Experience (Priority: Medium)

- [ ] **ESLint rule: prohibit raw `query()` in repository files** — enforce `tenantQuery()` usage
- [ ] **Switch repository to use `tenantQuery()`/`tenantQueryOne()`** helpers instead of `requireTenantId()` + `query()`
- [ ] **Add `/health/ready` and `/health/live` endpoints** — required for Kubernetes / ECS deployment
- [ ] **Add Prometheus metrics middleware** — request duration, error rate, scan duration, audit failure count
- [ ] **Add integration tests for Redis-backed providers** — requires Redis in CI environment
- [ ] **Add integration tests for `DIRECT_PRESIGN_ENABLED=true` mode**
- [ ] **Add integration test for `retainUntil` enforcement** (once implemented)
- [ ] **Document MySQL vs PostgreSQL schema discrepancy** — either resolve or formally retire MySQL claim
- [ ] **Deployment guide** — document required env vars, Redis requirement for multi-replica, scanner setup, JWKS configuration

---

## 13. Final Verdict

### NOT production ready in its current state.

The service is **production-ready with mandatory pre-production work** if MySQL is removed as a requirement and the critical issues below are addressed first.

### Mandatory blockers before any production deployment:

1. **`AUTH_PROVIDER=mock` must be blocked in production** (Issue 1). This is a complete security bypass that invalidates all other controls.

2. **`retainUntil` must be enforced** (Issue 4). Storing retention dates without enforcing them is a compliance liability in a legal/medical document service.

3. **Redis must be required for multi-replica deployment** (Issue 5). Token and rate-limit state loss between replicas causes both security degradation and user-facing 401 errors.

4. **Audit strict mode (or equivalent alerting) must be implemented** (Issue 2). Silent audit loss in a HIPAA context is a regulatory violation.

5. **Legal hold must be atomic** (Issue 8). Two-field inconsistency creates a deletion bypass for documents on legal hold.

### Why the current implementation is nonetheless strong:

The **tenant isolation model is the best aspect of this implementation**. Three independent, redundant layers — each capable of independently preventing cross-tenant access — is a textbook defence-in-depth approach. The code is clearly written, well-commented, and all three layers are consistently applied across every data access path. The integration test suite verifies this against a real database.

The **access mediation design** is production-grade. Opaque access tokens with 256-bit entropy, one-time-use atomic enforcement, re-scan at redemption, and storage key never exposed to clients reflects a mature understanding of the threat model.

The **provider abstraction** is clean and complete. Swapping cloud providers requires only environment variables.

The **error model** is consistent, typed, and does not leak internal state to clients.

### Summary assessment:

| Dimension | Grade |
|-----------|-------|
| Tenant isolation design | A |
| Access mediation | A- |
| Authentication | B+ |
| Audit completeness | B- |
| Rate limiting | B |
| Malware scanning | B |
| Observability | D |
| MySQL compatibility claim | F (false) |
| Compliance readiness (HIPAA) | C+ |
| Test coverage | B+ |
| **Overall** | **B- (not production ready without mandatory fixes)** |

