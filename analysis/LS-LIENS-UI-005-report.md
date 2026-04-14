# LS-LIENS-UI-005: Documents Integration with v2 Shared Documents Service

## Status: COMPLETE

## Summary
Built a 5-file frontend service layer to integrate with the existing v2 shared Documents microservice, and rewrote the document-handling list page, detail page, and upload form to use real API calls instead of mock Zustand store data. All mock store reads for documents (`useLienStore.documents`, `useLienStore.documentDetails`, `useLienStore.addDocument`, `useLienStore.updateDocument`) are fully removed from the affected pages.

## Backend (No Changes — Pre-existing)
The v2 Documents service (`apps/services/documents/`) was already fully built. This task only consumed its API from the frontend. Key backend capabilities used:

### API Endpoints Consumed
| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/documents` | List documents with filtering (productId, referenceId, referenceType, status, limit, offset) |
| GET | `/documents/{id}` | Get document by ID |
| POST | `/documents` | Upload new document (multipart/form-data) |
| PATCH | `/documents/{id}` | Update document metadata (title, description, status) |
| DELETE | `/documents/{id}` | Soft-delete a document |
| POST | `/documents/{id}/view-url` | Issue opaque view token |
| POST | `/documents/{id}/download-url` | Issue opaque download token |
| GET | `/documents/{id}/versions` | List all versions of a document |

### Gateway Routing
- Frontend: `/api/documents/documents/...`
- Gateway YARP: strips `/documents` prefix → Documents service at `localhost:5006`
- Access tokens: `/api/documents/access/{token}` → anonymous gateway route → docs service `/access/{token}`

### Response Shapes
- **DocumentResponse**: id, tenantId, productId, referenceId, referenceType, documentTypeId, title, description, status, mimeType, fileSizeBytes, currentVersionId, versionCount, scanStatus, scanCompletedAt, scanThreats, isDeleted, createdAt, createdBy, updatedAt, updatedBy
- **DocumentListResponse**: { data: DocumentResponse[], total, limit, offset }
- **IssuedTokenResponse**: { accessToken, redeemUrl, expiresInSeconds, type }
- **DocumentVersionResponse**: id, documentId, versionNumber, mimeType, fileSizeBytes, scanStatus, label, uploadedAt, uploadedBy

## Frontend Changes

### New Files (Service Layer)
| File | Purpose |
|------|---------|
| `lib/documents/documents.types.ts` | TypeScript DTOs matching backend shapes + UI view models (DocumentListItem, DocumentDetail, DocumentVersion) + UploadDocumentParams |
| `lib/documents/documents.api.ts` | API client: list, getById, upload (raw fetch with FormData), update, delete, requestViewUrl, requestDownloadUrl, listVersions |
| `lib/documents/documents.mapper.ts` | DTO → UI model mappers: mapDocumentToListItem, mapDocumentToDetail, mapDocumentVersion, mapDocumentPagination; formatFileSize helper |
| `lib/documents/documents.service.ts` | Business service: list, getById, upload, update, delete, getViewUrl, getDownloadUrl, listVersions |
| `lib/documents/index.ts` | Barrel export |

### Rewritten Files
| File | Changes |
|------|---------|
| `document-handling/page.tsx` | Replaced `useLienStore((s) => s.documents)` + `useLienStore((s) => s.updateDocument)` with `documentsService.list()`. Added async loading state, server-side status filtering via query params, real download via opaque token flow, archive via `documentsService.update()` PATCH. Removed `DOCUMENT_CATEGORY_LABELS` (backend uses referenceType instead of category). Added refresh-on-upload callback. |
| `document-handling/[id]/page.tsx` | Replaced `useLienStore((s) => s.documents)` + `useLienStore((s) => s.documentDetails)` with `documentsService.getById()` + `documentsService.listVersions()` (parallel fetch). Added version history table, scan threat alert panel (red box with threat list), real preview via `getViewUrl()`, real download via `getDownloadUrl()`. Removed tags section and processingNotes (not in v2 API). |
| `upload-document-form.tsx` | Replaced `simulateFileSelect()` mock with real `<input type="file">` + drag-and-drop. Sends actual `multipart/form-data` via `documentsService.upload()`. Auto-populates title from filename. Shows file size. Accepts `tenantId` and `documentTypeId` props for production context injection (defaults provided for dev). Added `onUploaded` callback. Replaced `useLienStore((s) => s.addDocument)` entirely. |

### Mock Store Reads Removed
| Store Selector | Removed From |
|----------------|-------------|
| `useLienStore((s) => s.documents)` | `document-handling/page.tsx`, `document-handling/[id]/page.tsx` |
| `useLienStore((s) => s.documentDetails)` | `document-handling/[id]/page.tsx` |
| `useLienStore((s) => s.updateDocument)` | `document-handling/page.tsx`, `document-handling/[id]/page.tsx` |
| `useLienStore((s) => s.addDocument)` | `upload-document-form.tsx` |

### Store Selectors Still Used (UI-only, not data)
| Store Selector | Used For |
|----------------|----------|
| `useLienStore((s) => s.addToast)` | Toast notifications for success/error feedback |
| `useLienStore((s) => s.currentRole)` | Role-based permission checks (`canPerformAction`) |

## Key Design Decisions

### Upload uses raw fetch instead of apiClient
The `apiClient` always sets `Content-Type: application/json`. Document upload requires `multipart/form-data`, so `documentsApi.upload()` uses raw `fetch()` with `FormData` (browser auto-sets the correct Content-Type with boundary).

### Opaque token flow for preview/download
The Documents service uses a two-step access pattern: (1) POST to get a short-lived opaque token, (2) redirect browser to `/api/documents/access/{token}` which 302-redirects to the actual storage URL. This avoids exposing JWTs in URLs and storage keys to the client.

### URL normalization
The backend `redeemUrl` field includes a leading slash (e.g., `/access/abc123`). The service layer strips leading slashes before concatenating with the `/api/documents/` prefix to avoid double-slash bugs.

### Tenant and DocumentType IDs
The upload form accepts `tenantId` and `documentTypeId` as optional props with dev-friendly defaults. In production, these will be injected from the authenticated user's context (session/JWT claims).

## Code Review Fixes Applied
1. **Double-slash URL bug** — `redeemUrl` starting with `/` caused `/api/documents//access/...`. Fixed by stripping leading slashes before concatenation.
2. **Hardcoded tenant ID** — Moved from hardcoded constants to injectable props (`tenantId`, `documentTypeId`) on `UploadDocumentForm`, enabling production context injection while keeping dev defaults.

## Files Changed
```
apps/web/src/lib/documents/documents.types.ts     (new)
apps/web/src/lib/documents/documents.api.ts        (new)
apps/web/src/lib/documents/documents.mapper.ts     (new)
apps/web/src/lib/documents/documents.service.ts    (new)
apps/web/src/lib/documents/index.ts                (new)
apps/web/src/app/(platform)/lien/document-handling/page.tsx       (rewritten)
apps/web/src/app/(platform)/lien/document-handling/[id]/page.tsx  (rewritten)
apps/web/src/components/lien/forms/upload-document-form.tsx       (rewritten)
replit.md                                                          (updated)
```
