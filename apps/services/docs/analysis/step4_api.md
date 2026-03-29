# Phase 4 — Secure API

## Endpoints

| Method | Path | Auth | Permission | Description |
|---|---|---|---|---|
| `GET`  | `/health` | None | — | Liveness probe |
| `GET`  | `/health/ready` | None | — | Readiness (DB + storage check) |
| `POST` | `/documents` | Bearer | write | Upload document + file |
| `GET`  | `/documents` | Bearer | read | List documents (tenant-scoped) |
| `GET`  | `/documents/:id` | Bearer | read | Get document metadata |
| `PATCH`| `/documents/:id` | Bearer | write | Update metadata / status |
| `DELETE`| `/documents/:id` | Bearer | delete | Soft delete |
| `POST` | `/documents/:id/versions` | Bearer | write | Upload new version |
| `GET`  | `/documents/:id/versions` | Bearer | read | List version history |
| `POST` | `/documents/:id/view-url` | Bearer | read | Generate short-lived view URL |
| `POST` | `/documents/:id/download-url` | Bearer | read | Generate short-lived download URL |

## Request Validation

- All JSON bodies validated with **Zod schemas** before reaching service layer
- File uploads validated at two layers:
  1. **Multer** — enforces `MAX_FILE_SIZE_MB` and MIME allowlist
  2. **`validateFileContent()`** — reads magic bytes, detects MIME spoofing

## Tenant Isolation

- All list/get/update/delete operations filter by `principal.tenantId`
- PATCH body accepts `tenantId` only for PlatformAdmin — regular users cannot cross tenants
- `assertTenantScope()` blocks cross-tenant access at the ABAC layer

## Secure URL Generation

- Signed URLs expire in `SIGNED_URL_EXPIRY_SECONDS` (default 300s)
- URLs never contain storage keys or bucket names
- Every signed URL generation is audit-logged
- Local provider serves files through the service itself (token-gated)
- S3 provider uses AWS pre-signed URLs (short-lived, HTTPS only)

## Response Sanitisation

`sanitizeDocument()` strips `storageKey`, `storageBucket`, and `checksum` from all API responses. Clients never see internal storage references.

## Error Responses

All errors follow the shape:
```json
{
  "error": "MACHINE_READABLE_CODE",
  "message": "Human readable message",
  "correlationId": "uuid",
  "details": {}
}
```
