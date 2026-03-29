# Phase 8 — Legacy .NET DMM Mapping

## Feature Mapping

| Legacy .NET DMM Feature | New Docs Service Equivalent | Notes |
|---|---|---|
| Document upload | `POST /documents` | Adds MIME validation, SHA-256 checksum, RBAC |
| Document list | `GET /documents` | Adds tenant isolation, filtering, pagination |
| Document download | `POST /documents/:id/download-url` | Short-lived signed URL replaces direct S3 |
| Document view | `POST /documents/:id/view-url` | Short-lived signed URL, audit-logged |
| Document metadata update | `PATCH /documents/:id` | Adds status transitions, legal hold check |
| Document delete | `DELETE /documents/:id` | Soft delete only; legal hold enforcement |
| Versioning | `POST /documents/:id/versions` | Atomic version counter with row lock |
| Document types | `document_types` table | Supports global + per-tenant types |
| Audit log | `document_audits` table | Immutable (DB trigger), covers 14 event types |
| Basic Auth | Removed | JWT Bearer (JWKS / symmetric) |
| MD5 checksum | Removed | SHA-256 |
| S3 direct access | Removed | Signed URLs only |
| TPP dependency | Removed | Service is fully independent |

## Fields Intentionally NOT Carried Over

| Legacy Field | Reason |
|---|---|
| `tppCaseId` (raw foreign key to TPP) | Replaced by generic `referenceId` + `referenceType` |
| `s3PublicUrl` | No public URLs; signed URLs only |
| `md5Hash` | Replaced by SHA-256 `checksum` |
| `basicAuthToken` | Replaced by JWT |
| Direct TPP DB joins | Removed — service owns its own data |

## Migration Path for Existing Data

If migrating data from the legacy DMM:

1. Export existing document records (id, title, mimeType, s3Key, tenantId, etc.)
2. Map `tppCaseId` → `referenceId` with `referenceType = 'CASE'`
3. Recompute checksums from existing files (SHA-256)
4. Insert into `documents` table with `status = 'ACTIVE'`
5. Create initial `document_versions` entries pointing to existing S3 keys
6. Seed `document_types` from legacy classification codes
7. Generate synthetic audit entries for `DOCUMENT_CREATED` (retroactive compliance)

**Do NOT** import old MD5 hashes or basic auth tokens.

## Coupling NOT Reintroduced

- No TPP/LegalSynq service HTTP calls
- No shared database reads
- No AWS SDK outside `infrastructure/storage/s3-storage-provider.ts`
- No hardcoded tenant IDs or product IDs
