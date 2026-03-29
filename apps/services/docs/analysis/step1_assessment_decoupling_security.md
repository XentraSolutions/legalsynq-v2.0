# Phase 1 — Assessment, Decoupling & Security Baseline

## Source Situation

No existing DMM TypeScript service was found in the repository. The legacy DMM lived as a .NET service referenced in `LegalSynq.sln`. This phase is therefore a **greenfield assessment + new baseline** rather than a legacy code audit, which actually improves the outcome: no technical debt to carry forward.

---

## Identified Coupling & Security Gaps (Legacy Reference)

| Concern | Legacy Pattern | New Design Decision |
|---|---|---|
| AWS coupling | `new S3Client()` scattered across files | `StorageProvider` interface + `S3StorageProvider` isolated to `infrastructure/storage/` |
| Shared DB | Reads TPP tables directly | Owns private `docs_*` tables only |
| TPP dependency | HTTP calls to TPP for auth + metadata | `AuthProvider` interface; zero inter-service HTTP calls |
| Auth mechanism | MD5 + Basic Auth | JWT bearer via JWKS (RS256/ES256) |
| No audit trail | Absent | Immutable `document_audits` table with DB trigger preventing mutation |
| Public file ACL | `ACL: 'public-read'` | No ACL set; bucket policy enforces private; signed URLs only |
| Raw SQL spread | Queries in route handlers | Centralised `DocumentRepository` + transaction helpers |
| No error hierarchy | `throw new Error(...)` everywhere | `DocsError` base class with typed subclasses (400–500) |
| No input validation | No MIME / size checks | `validateFileContent()` checks magic bytes + MIME mismatch |
| No correlation ID | Absent | `correlationIdMiddleware` generates or forwards X-Correlation-Id |

---

## Implemented in Phase 1

### Decoupling
- `StorageProvider` interface in `domain/interfaces/`
- `S3StorageProvider` — all AWS SDK usage confined here
- `LocalStorageProvider` — dev/test without cloud dependency
- `GCSStorageProvider` — scaffold for future GCP support
- `storage-factory.ts` — selects provider via `STORAGE_PROVIDER` env var
- `AuthProvider` interface + `JwtAuthProvider`
- `SecretsProvider` interface + `EnvSecretsProvider`

### Security Baseline
- All endpoints behind `requireAuth` middleware (default deny)
- JWT validation with JWKS or symmetric secret
- RBAC via `assertPermission()` + ABAC via `assertTenantScope()`
- `helmet` security headers on all responses
- CORS allowlist only
- File validation: size limit, MIME allowlist, magic-byte mismatch detection
- Storage keys never returned to clients (`sanitizeDocument`)
- Sensitive fields redacted from structured logs

### Transactions
- `withTransaction()` helper in `db.ts`
- Version upload uses `FOR UPDATE` row lock + atomic counter increment

### Error Handling
- Centralised `errorHandler` Express middleware
- DocsError hierarchy with HTTP status codes
- ZodError and MulterError mapped cleanly

### Correlation ID
- All requests receive a UUID correlation ID (from gateway or generated)
- Echoed in response header; present in every log line and audit record

---

## Result

The service runs locally with `LOCAL_STORAGE_PATH` and a PostgreSQL connection string. No AWS credentials, no other LegalSynq services, no TPP dependency required.
