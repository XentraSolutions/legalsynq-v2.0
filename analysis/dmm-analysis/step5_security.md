# Phase 5 — Security Hardening

## JWT Authentication

### JwtAuthProvider
- Validates RS256 / ES256 tokens via **JWKS** (preferred) or HS256 via static secret (dev only)
- JWKS client caches public keys (1-hour TTL, 10-key max) to avoid key endpoint hammering
- Rate-limits JWKS requests: 10/min
- Extracts: `sub` (userId), `tenantId`, `email`, `roles`, `productId`
- Rejects tokens missing `sub` or `tenantId`
- `MockAuthProvider` blocked in `NODE_ENV=production` via factory guard

### Token Flow
```
Client → Bearer <JWT> → requireAuth middleware → JwtAuthProvider.validateToken()
                                ↓
                        AuthPrincipal { userId, tenantId, roles }
                                ↓
                        assertPermission() + assertTenantScope()
                                ↓
                        Service layer
```

## RBAC

| Role | read | write | delete | admin |
|---|---|---|---|---|
| PlatformAdmin | ✓ | ✓ | ✓ | ✓ |
| TenantAdmin | ✓ | ✓ | ✓ | — |
| DocManager | ✓ | ✓ | ✓ | — |
| DocUploader | ✓ | ✓ | — | — |
| DocReader | ✓ | — | — | — |

**Default DENY** — any role not in the table or any unknown role gets no permissions.

## ABAC (Tenant Scope)

- `assertTenantScope(principal, resourceTenantId)` compares JWT `tenantId` against the document's `tenantId`
- Only `PlatformAdmin` can cross tenant boundaries
- All DB queries additionally filter by `tenant_id` as a database-level safety net (defence in depth)

## File Security

| Check | Implementation |
|---|---|
| Max file size | `multer.limits.fileSize` + `FileTooLargeError` |
| MIME allowlist | `multer.fileFilter` + `ALLOWED_MIME_TYPES` |
| Magic byte detection | `file-type` library reads first bytes |
| MIME mismatch | Declared vs detected MIME compared; mismatch rejected |
| Empty file | `buffer.byteLength === 0` check |
| Path traversal (local) | `path.basename(bucket)` + `key.replace(/\.\./)` |

## Signed URL Security

- Expiry: configurable, default 300 seconds
- S3: AWS pre-signed GET URL, HTTPS only, IAM-scoped bucket policy
- Local: random 256-bit token, server-side expiry checked on resolution
- Audit log entry written for every URL generation

## Secrets Management

- No hardcoded credentials anywhere
- All secrets from environment via `EnvSecretsProvider`
- AWS/GCP SDK credentials read from environment (IAM role recommended for production)
- `config.ts` validates all required env vars at startup; service refuses to start on misconfiguration

## Security Headers (helmet)

- `Content-Security-Policy`
- `X-Content-Type-Options: nosniff`
- `X-XSS-Protection`
- `Strict-Transport-Security` (1 year, includeSubDomains, preload)
- `Cross-Origin-Embedder-Policy`
- `Referrer-Policy: no-referrer`
- `X-Powered-By` header removed

## Malware Scanning Hook

`FileScannerProvider` interface defined in `domain/interfaces/`. All version uploads include `scanStatus: 'PENDING'` (or `'SKIPPED'` if no scanner configured). Implement `ClamAvFileScannerProvider` or `AwsGuardDutyScanner` and wire in `document-service.ts` `uploadVersion()` path.
