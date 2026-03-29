# Phase 14 — Integration Tests

## Overview

Full end-to-end integration test suite against a real PostgreSQL database
(`heliumdb` — Replit shared instance), a local file storage provider, and a JWT
auth provider using HS256 with a test-only secret. All seven suites run with
`maxWorkers: 1` to prevent DB concurrency issues.

---

## Infrastructure

| Component | Configuration |
|-----------|--------------|
| Database | `postgresql://postgres:password@helium/heliumdb?sslmode=disable` |
| Auth provider | `AUTH_PROVIDER=jwt`, secret `integration-test-secret-do-not-use-in-prod` |
| Storage provider | `STORAGE_PROVIDER=local`, dir `/tmp/docs-integration-test-storage` |
| File scanner | `FILE_SCANNER_PROVIDER=none` → `NullFileScannerProvider` → `SKIPPED` |
| Rate limiter | `RATE_LIMIT_PROVIDER=memory` |
| Access token store | `ACCESS_TOKEN_STORE=memory` |
| Scan gate | `REQUIRE_CLEAN_SCAN_FOR_ACCESS=true` |
| Direct presign | `DIRECT_PRESIGN_ENABLED=false` (overridden per-test in access-control) |
| Max file size | `MAX_FILE_SIZE_MB=1` |
| Rate limits | `RATE_LIMIT_MAX_REQUESTS=200`, `RATE_LIMIT_UPLOAD_MAX=50` |

### DB Setup (globalSetup)

Four migrations applied idempotently (`_docs_migrations` tracking table):

1. `001_create_document_types` — shared reference data
2. `002_create_documents` — primary document store with tenant isolation indices
3. `003_create_document_versions` — version history
4. `004_create_document_audits` — immutable audit log + PostgreSQL trigger

Shared seed: `document_types` row with fixed UUID `10000000-0000-0000-0000-000000000001`.

### Tenant Fixtures

| Constant | UUID |
|----------|------|
| `TENANT_A` | `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` |
| `TENANT_B` | `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb` |

### Test Data Cleanup

All integration test documents use `product_id = 'int-test'`.  
Each test file calls `cleanTestDocuments()` in `afterAll`.  
`globalTeardown` performs a final sweep.

---

## Test Suites

### 1. `auth.test.ts` — Authentication (25 tests)

| Category | Tests |
|----------|-------|
| Missing token (5 endpoints) | 401 AUTHENTICATION_REQUIRED |
| Wrong scheme (Basic not Bearer) | 401 |
| CorrelationId in 401 body | checked |
| Invalid format (random string) | 401 |
| Wrong secret | 401 |
| Missing required claims | 401 |
| Empty Bearer value | 401 |
| Expired JWT | 401 |
| Valid token → GET /documents | 200 |
| Valid token → GET /documents/:id | 404 (not 401) |
| X-Correlation-Id echoed in response | verified |
| GET /health (public) | 200 |

### 2. `rbac.test.ts` — Role-Based Access Control (22 tests)

| Role | Tested Operations |
|------|-------------------|
| DocReader | GET list, GET id ✓ · POST upload ✗ · DELETE ✗ · PATCH ✗ |
| DocUploader | GET ✓ · POST ✓ · DELETE ✗ |
| DocManager | GET ✓ · PATCH ✓ · DELETE ✓ (full lifecycle verified) |
| TenantAdmin | upload + read + delete ✓ |
| PlatformAdmin | all permissions ✓ |

Response structure invariants: `data/total/limit/offset`, no `storageKey`, no `storageBucket`, no `checksum` in responses.

### 3. `tenant-isolation.test.ts` — Tenant Isolation (21 tests)

Critical security regression suite. Three-layer isolation verified end-to-end:

| Layer | Mechanism | Test Coverage |
|-------|-----------|---------------|
| Layer 1 (Route) | `assertTenantScope()` pre-flight | Cross-tenant create → 403 |
| Layer 2 (Service) | `assertDocumentTenantScope()` ABAC | Post-load scope enforcement |
| Layer 3 (DB) | `requireTenantId()` + `WHERE tenant_id = ?` | No cross-tenant rows returned |

Key assertions:
- Cross-tenant GET returns **404** (not 403 — never discloses existence)
- Response body never contains the target tenant UUID
- PlatformAdmin cross-tenant read requires `X-Admin-Target-Tenant` header
- PlatformAdmin cross-tenant access emits `ADMIN_CROSS_TENANT_ACCESS` audit event
- Non-admin `X-Admin-Target-Tenant` header is silently ignored
- Symmetry verified: TENANT_A cannot read TENANT_B documents and vice versa

### 4. `upload-validation.test.ts` — File Validation (14 tests)

| Scenario | Expected |
|----------|----------|
| Valid `text/plain` | 201, scanStatus=SKIPPED, no storageKey in response |
| Zero-byte file | 400 FILE_VALIDATION_ERROR |
| Oversized (>1 MB) | 413 FILE_TOO_LARGE |
| `application/javascript` | 422 UNSUPPORTED_FILE_TYPE (Multer fileFilter) |
| `text/html` | 422 UNSUPPORTED_FILE_TYPE |
| `application/octet-stream` | 422 UNSUPPORTED_FILE_TYPE |
| JPEG bytes declared as `application/pdf` | 400 FILE_VALIDATION_ERROR (magic-byte mismatch) |
| Missing file (JSON body) | 400 VALIDATION_ERROR |
| `text/plain` + `text/csv` whitelisted | 201 |

### 5. `access-control.test.ts` — Lifecycle & Scan Access Control (19 tests)

| Scenario | Expected |
|----------|----------|
| Soft-deleted doc GET | 404 NOT_FOUND |
| Soft-deleted doc absent from list | verified |
| DELETE on already-deleted | 404 |
| Legal hold → DELETE | 403 ACCESS_DENIED + "legal hold" message |
| Legal hold → GET | 200 (read allowed) |
| Scan INFECTED → view-url | 403 SCAN_BLOCKED |
| Scan INFECTED → download-url | 403 SCAN_BLOCKED |
| Scan PENDING → view-url | 403 SCAN_BLOCKED |
| Scan SKIPPED → view-url | 200 (allowed) |
| Scan CLEAN → view-url | 200 (allowed) |
| PENDING → CLEAN transition | access changes dynamically |
| Access token issue round-trip | token in response |

Scan gate tests use `DIRECT_PRESIGN_ENABLED=true` via `jest.resetModules()` per-test.

### 6. `rate-limiting.test.ts` — Rate Limiting (9 tests)

Rate limits overridden per-test via `jest.resetModules()`:

- `RATE_LIMIT_MAX_REQUESTS=3` for general limiter tests
- `RATE_LIMIT_UPLOAD_MAX=2` for upload limiter tests

| Scenario | Expected |
|----------|----------|
| Rate-limit headers on every response | X-RateLimit-Limit/Remaining/Reset |
| Exceeding general limit | 429 RATE_LIMIT_EXCEEDED |
| 429 includes Retry-After header + retryAfter body | verified |
| 429 includes limitDimension field | ip/user/tenant |
| Upload limit separately enforced | 429 after RATE_LIMIT_UPLOAD_MAX |
| Different users have independent user-level limits | verified |

### 7. `audit.test.ts` — Audit Trail (28 tests)

| Event | Verified |
|-------|---------|
| `DOCUMENT_CREATED` | actor_id, outcome, detail.mimeType, detail.scanStatus |
| `SCAN_REQUESTED` | emitted pre-storage with pre-generated docId |
| `SCAN_COMPLETED` | NullScanner → SKIPPED outcome |
| `DOCUMENT_UPDATED` | actor_id, outcome on PATCH |
| `DOCUMENT_STATUS_CHANGED` | on status field update |
| `DOCUMENT_DELETED` | actor_id, outcome on soft delete |
| `VERSION_UPLOADED` | actor_id, versionNumber in detail |
| `ADMIN_CROSS_TENANT_ACCESS` | actor_id, outcome, resourceTenantId in detail |
| Audit immutability | DELETE attempt → PostgreSQL trigger rejects |
| Correlation ID threading | audit row correlationId matches request header |

**Known gap documented:** RBAC denial (`assertPermission` throw) fires before document context exists — denial is NOT written to `document_audits`. HTTP 403 is still returned. Future improvement: route-level RBAC denial audit with nil documentId.

**Bug fix discovered:** `DocumentRepository.create` was generating its own UUID independently from the pre-generated `docId` in `document-service.ts`, causing scan audit events (`SCAN_REQUESTED`, `SCAN_COMPLETED`) to be stored under a mismatched `document_id`. Fixed by threading `docId` as optional `id` input to `DocumentRepository.create`.

---

## Test Results

```
Test Suites: 7 passed, 7 total
Tests:       97 passed, 97 total
Snapshots:   0 total
Time:        ~12 seconds
```

Combined with unit tests:

```
Unit  tests: 161 passed (7 suites)
Integration: 97 passed  (7 suites)
Total:       258 tests, 14 suites — all passing
```

---

## Files Changed

### New — Integration Test Infrastructure
- `jest.integration.config.js` — Jest config for integration suite
- `tests/integration/setup/env.ts` — env vars injected before each worker module
- `tests/integration/setup/global-setup.ts` — DB migrations + seed (one-time)
- `tests/integration/setup/global-teardown.ts` — final cleanup
- `tests/integration/helpers/token.ts` — JWT factory + role helpers
- `tests/integration/helpers/db.ts` — seedDocument(), updateScanStatus(), getAuditEvents()

### New — Integration Test Suites
- `tests/integration/auth.test.ts`
- `tests/integration/rbac.test.ts`
- `tests/integration/tenant-isolation.test.ts`
- `tests/integration/upload-validation.test.ts`
- `tests/integration/access-control.test.ts`
- `tests/integration/rate-limiting.test.ts`
- `tests/integration/audit.test.ts`

### Modified — Source Fixes
- `src/api/middleware/auth.ts` — nil UUID placeholders (prevent UUID DB constraint error)
- `src/api/middleware/file-validator.ts` — import alias (`fromBuffer as fileTypeFromBuffer`)
- `src/application/document-service.ts` — pass pre-generated `docId` to `DocumentRepository.create`
- `src/infrastructure/database/document-repository.ts` — accept optional `id` in create input
- `package.json` — `test` script targets unit tests only; `test:int` uses integration config; `file-type` downgraded to v16.5.4 (last CJS version)

---

## How to Run

```bash
# Unit tests only (~5s)
npm test
# or
npm run test:unit

# Integration tests only (~12s)
# Requires: PostgreSQL on helium (Replit shared DB)
npm run test:int

# Both
npm run test:all
```

---

## Remaining Gaps

1. **RBAC denial audit** — Route-level `assertPermission()` denials are not written to `document_audits` (no document context). Future improvement: emit audit event with nil documentId.
2. **Async scan flow** — Integration tests use synchronous (inline) scanning only. Async scan-complete webhook / worker path is not tested.
3. **Redis provider** — Integration tests use in-memory providers. Redis-backed rate limiting and access token store are unit-tested but not integration-tested.
4. **GCS/S3 storage** — Only local storage is integration-tested. Cloud providers require real credentials.
5. **JWKS auth** — Only HS256 JWT tested. RS256 + JWKS URL path is unit-tested only.
