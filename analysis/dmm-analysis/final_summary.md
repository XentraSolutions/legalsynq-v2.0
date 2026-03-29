# Final Summary — LegalSynq Docs Service

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         API Layer                               │
│  Express + Helmet + CORS + Correlation ID + Error Handler       │
│  Routes: /health, /documents                                    │
│  Middleware: requireAuth, upload, validateFileContent           │
└────────────────────┬────────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────────┐
│                    Application Layer                            │
│  DocumentService  ─  AuditService  ─  RBAC (assertPermission)  │
└────┬─────────────────────┬───────────────────────┬─────────────┘
     │                     │                       │
┌────▼──────┐   ┌──────────▼──────────┐   ┌───────▼──────────────┐
│  Domain   │   │   Infrastructure     │   │  Infrastructure      │
│  Entities │   │   Storage Provider  │   │  Database            │
│  Interfaces│  │  (S3/Local/GCS)     │   │  (PostgreSQL)        │
└───────────┘   └─────────────────────┘   └──────────────────────┘
```

## Provider Abstraction Design

All external dependencies hidden behind interfaces:

```typescript
// Switching from S3 to GCS:
// 1. STORAGE_PROVIDER=gcs in environment
// 2. Implement GCSStorageProvider (scaffold exists)
// 3. Zero changes to domain, application, or API layers
```

## Security Model

| Layer | Control |
|---|---|
| Transport | HTTPS assumed; HSTS headers enforced |
| Authentication | JWT Bearer (JWKS RS256/ES256 or HS256 dev) |
| Authorisation | RBAC (5 roles) + ABAC (tenant scope) |
| Default posture | DENY — explicit grant required |
| File input | MIME allowlist + size limit + magic-byte detection |
| Storage | Private only — no public ACL; signed URLs with expiry |
| Audit | Immutable INSERT-only table; DB trigger prevents tampering |
| Logs | Pino with redact config; no secrets, no file content |
| Secrets | Environment variables (dev); pluggable for AWS SM / GCP SM |

## Compliance Features

| Feature | Implementation |
|---|---|
| Audit trail | `document_audits` — all 14 critical events |
| Tamper-resistant audit | PostgreSQL trigger blocks UPDATE/DELETE |
| Tenant isolation | JWT claim + ABAC + DB query filter (3 layers) |
| Soft delete | `is_deleted + deleted_at + deleted_by` |
| Retention policy | `retain_until` field — enforcement hook ready |
| Legal hold | `legal_hold_at` — deletion blocked when set |
| File integrity | SHA-256 checksum on every document and version |
| No PHI in logs | Pino redact config + structured log discipline |

## Switching AWS → GCP

1. Set `STORAGE_PROVIDER=gcs`, `GCS_BUCKET_NAME`, `GCS_PROJECT_ID`, `GCS_KEY_FILE_PATH`
2. Implement `GCSStorageProvider` (scaffold at `infrastructure/storage/gcs-storage-provider.ts`):
   - Install `@google-cloud/storage`
   - Implement `upload()`, `generateSignedUrl()`, `delete()`, `exists()`
   - All GCS SDK calls confined to that single file
3. Optionally set `SECRETS_PROVIDER=gcp-sm` and implement `GcpSmSecretsProvider`
4. No other files change

## Deployment Instructions

### Local Development
```bash
cp .env.example .env
# Edit DATABASE_URL, STORAGE_PROVIDER=local, AUTH_PROVIDER=mock
npm install
npm run db:migrate
npm run dev
```

### Docker
```bash
docker build -t legalsynq/docs-service .
docker run -p 5005:5005 \
  -e DATABASE_URL=postgres://... \
  -e STORAGE_PROVIDER=s3 \
  -e AWS_BUCKET_NAME=my-docs-bucket \
  -e AUTH_PROVIDER=jwt \
  -e JWT_JWKS_URI=https://auth.legalsynq.com/.well-known/jwks.json \
  legalsynq/docs-service
```

### Environment Variables (required for production)
```
DATABASE_URL
STORAGE_PROVIDER=s3
AWS_REGION
AWS_BUCKET_NAME
AUTH_PROVIDER=jwt
JWT_JWKS_URI
JWT_ISSUER
JWT_AUDIENCE
SECRETS_PROVIDER
```

## Remaining Risks

| Risk | Mitigation |
|---|---|
| JWKS endpoint unavailable at startup | JwtAuthProvider retries via jwks-rsa caching; but service can't validate tokens until JWKS is reachable |
| Local storage token store is in-memory | Lost on restart; use Redis or DB for production signed-URL token store |
| GCS and AWS SM providers are scaffolds | Must be implemented before production use |
| No rate limiting | Add `express-rate-limit` middleware per IP/tenantId |
| No malware scanning | Implement `FileScannerProvider` before handling untrusted uploads |
| DB connection pooling under load | Max pool size is 10 — tune via `DATABASE_POOL_MAX` env var |

## Next Backlog

- [ ] Implement `GCSStorageProvider` fully
- [ ] Implement `AwsSmSecretsProvider` and `GcpSmSecretsProvider`
- [ ] Implement `ClamAvFileScannerProvider` hook in upload flow
- [ ] Add rate limiting middleware
- [ ] Add Prometheus `/metrics` endpoint
- [ ] Add Redis-backed signed URL token store (for local provider)
- [ ] Write integration test suite (supertest + test DB)
- [ ] Add OpenAPI / Swagger spec generation
- [ ] Add legal hold management API endpoints (`PUT /documents/:id/legal-hold`)
- [ ] Add retention policy management endpoints
- [ ] Add bulk operations endpoint (`POST /documents/bulk`)
- [ ] CI pipeline: lint + test + Docker build + SAST scan
