# Documents Service

Secure document storage with virus scanning, versioning, and opaque access tokens.

**Port:** 5006

## Responsibilities

- Document upload (multipart, S3-backed storage)
- Metadata management (name, tags, tenant scoping)
- Virus scanning via ClamAV (circuit breaker, large-file policy)
- Document versioning
- Opaque access token issuance (short-lived view/download URLs)
- Public logo serving (`/public/logos/{id}`) for tenant branding
- Signature freshness monitoring

## Layer Structure

```
Documents.Api/            Endpoints, middleware, Program.cs (port 5006)
Documents.Application/    Interfaces, DTOs, DocumentService, AccessTokenService
Documents.Domain/         Document, DocumentVersion, AccessToken
Documents.Infrastructure/ DbContext (DocsDb), S3 adapter, ClamAV adapter, EF migrations
```

## Key Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/documents` | Bearer | Upload document |
| `GET` | `/api/documents` | Bearer | List documents (tenant-scoped) |
| `GET` | `/api/documents/{id}` | Bearer | Document metadata |
| `POST` | `/api/documents/{id}/view-token` | Bearer | Request short-lived view URL |
| `POST` | `/api/documents/{id}/download-token` | Bearer | Request download URL |
| `GET` | `/access/{token}` | Anonymous | Redeem short-lived view/download token |
| `GET` | `/api/documents/{id}/versions` | Bearer | Version history |
| `GET` | `/public/logos/{id}` | Anonymous | Public tenant logo |

Authenticated Documents endpoints accept either a standard Identity user JWT or a shared platform service JWT. The preferred service-token audience is `documents-service`.
SynqLien sellers can read and upload supporting documents. Document deletion remains limited to document managers and tenant/platform administrators; other SynqLien roles do not gain direct Documents-service access.
For local storage, redeemed file responses infer `Content-Type` from the stored filename so browser view links can render supported files such as PNGs and PDFs.

## Storage

AWS S3 (configured via `AWS_S3_BUCKET_NAME`, `AWS_S3_REGION`, `AWS_S3_ACCESS_KEY_ID`, `AWS_S3_SECRET_ACCESS_KEY` secrets).

## Database

`DocsDb` (MySQL).

## Security Notes

- All document access goes through opaque tokens — no direct S3 URL exposure
- ClamAV integration with circuit breaker (bypasses scan on ClamAV failure, logs warning)
- Large files (>50MB) skip AV scan with policy flag
- Public logo endpoint is intentionally anonymous but serves only registered logo document IDs

## Timestamp Contract

- Document API response timestamps are serialized in Pacific time with an explicit offset (`-08:00` or `-07:00`), including document `createdAt` / `updatedAt` and version `uploadedAt`.
- The service computes Pacific offsets even when the host runtime is missing OS timezone metadata, so QA and production stay aligned with the application timezone.
