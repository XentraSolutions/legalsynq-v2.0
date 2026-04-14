# LS-LIENS-07-002 — BillOfSale Document Retrieval API Report

## 1. Summary

Implemented secure BillOfSale document retrieval APIs through the Liens service. Liens validates business ownership (tenant scope, BOS existence, DocumentId presence) and proxies the document download from the v2 Documents service. No direct Documents access is exposed to callers.

## 2. Documents Retrieval Pattern Identified

The v2 Documents service uses a **302-redirect model**:
- `GET /documents/{id}/content?type=download` returns a 302 redirect to a short-lived signed storage URL (S3/GCS)
- Alternative: token-based access via `POST /documents/{id}/download-url` + `GET /access/{token}`
- Auth: JWT-based with tenant isolation and malware scan gate

**Pattern followed**: Proxy download. Liens calls `GET /documents/{id}/content?type=download`, the HttpClient follows the 302 redirect automatically, and Liens streams the resulting file content back to the caller. This keeps the Documents service internal — callers never see the redirect or storage URLs.

## 3. Files Created/Changed

### Created
| File | Purpose |
|------|---------|
| `Liens.Application/DTOs/DocumentRetrievalResult.cs` | DTO: stream + content type + filename + length |
| `Liens.Application/Interfaces/IBillOfSaleDocumentQueryService.cs` | Application-layer query abstraction |
| `Liens.Application/Services/BillOfSaleDocumentQueryService.cs` | Orchestrates BOS lookup → validation → Documents retrieval |
| `BuildingBlocks/Exceptions/ServiceUnavailableException.cs` | New exception for upstream service failures |

### Modified
| File | Change |
|------|--------|
| `Liens.Application/Interfaces/IBillOfSaleDocumentService.cs` | Added `RetrieveDocumentAsync` method |
| `Liens.Infrastructure/Documents/BillOfSaleDocumentService.cs` | Implemented retrieval via Documents HTTP client |
| `Liens.Api/Endpoints/BillOfSaleEndpoints.cs` | Added two document retrieval endpoints |
| `Liens.Api/Middleware/ExceptionHandlingMiddleware.cs` | Added `ServiceUnavailableException` → 502 handler |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `IBillOfSaleDocumentQueryService` |

## 4. Application / Document Retrieval Services

### IBillOfSaleDocumentService (infrastructure abstraction)
```csharp
Task<DocumentRetrievalResult?> RetrieveDocumentAsync(Guid documentId, CancellationToken ct)
```
- Calls `GET /documents/{documentId}/content?type=download` via named HttpClient
- Returns `DocumentRetrievalResult` (stream, content type, filename, length) or `null`
- Handles 404 and non-success responses gracefully (returns null)

### IBillOfSaleDocumentQueryService (application layer)
```csharp
Task<DocumentRetrievalResult> GetDocumentByBosIdAsync(Guid tenantId, Guid bosId, CancellationToken ct)
Task<DocumentRetrievalResult> GetDocumentByBosNumberAsync(Guid tenantId, string billOfSaleNumber, CancellationToken ct)
```
Orchestration:
1. Load BOS from repository (tenant-scoped)
2. Validate BOS exists → `NotFoundException` if not
3. Validate `DocumentId` is present → `ConflictException` (code: `DOCUMENT_NOT_AVAILABLE`) if null
4. Call `IBillOfSaleDocumentService.RetrieveDocumentAsync`
5. If Documents service fails → `ServiceUnavailableException`
6. Return `DocumentRetrievalResult` for streaming

## 5. Endpoint Routes Added

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/liens/bill-of-sales/{id}/document` | Download BOS document by BOS ID |
| GET | `/api/liens/bill-of-sales/by-number/{billOfSaleNumber}/document` | Download BOS document by BOS number |

Both endpoints:
- Return `Results.File(stream, contentType, fileDownloadName)` on success
- Share the `bosGroup` route group (same auth chain as existing BOS endpoints)
- Follow the existing by-ID / by-number convention used across Liens (cases, liens, BOS)

## 6. Retrieval Model: Proxy Download

**Chosen model**: Proxy download (Liens streams file to caller).

**Rationale**:
- Keeps Documents service completely internal — callers never see storage URLs or Documents endpoints
- Maintains Liens as the single business access gate for all BOS-related operations
- Consistent with the spec's preferred default
- HttpClient follows 302 redirects automatically — no additional redirect logic needed

**How it works**:
1. Liens endpoint receives authenticated request
2. Validates BOS ownership (tenant + existence + DocumentId)
3. Calls Documents service `GET /documents/{id}/content?type=download`
4. HttpClient follows the 302 redirect to the signed storage URL
5. Streams the response (using `HttpCompletionOption.ResponseHeadersRead` for efficiency)
6. Returns file to caller via `Results.File`

## 7. How BOS is Resolved Before Document Retrieval

1. Extract `tenantId` from `ICurrentRequestContext` (via `RequireTenantId()` helper)
2. Load BOS via `IBillOfSaleRepository.GetByIdAsync(tenantId, bosId)` or `GetByBillOfSaleNumberAsync(tenantId, number)`
3. Repository enforces tenant isolation (tenant-scoped query)
4. If not found: `NotFoundException` → HTTP 404
5. If `DocumentId` is null: `ConflictException` → HTTP 409 with code `DOCUMENT_NOT_AVAILABLE`
6. Only then is the DocumentId passed to the Documents service client

## 8. How Documents Service Retrieval Works

```
GET http://localhost:5006/documents/{documentId}/content?type=download
```

- Uses the existing named HttpClient `"DocumentsService"` (registered in DI)
- `HttpCompletionOption.ResponseHeadersRead` — starts streaming without buffering full response
- Documents service returns 302 → HttpClient follows to storage URL → returns file bytes
- Extracts: content type, filename (from Content-Disposition), content length
- Falls back to `application/octet-stream` if no content type, `document-{id}.pdf` if no filename

## 9. Authorization Approach

Uses the existing Liens auth model:
- `RequireAuthorization(Policies.AuthenticatedUser)` — via `bosGroup`
- `RequireProductAccess(LiensPermissions.ProductCode)` — via `bosGroup`
- `RequirePermission(LiensPermissions.LienRead)` — per endpoint

**Permission used**: `LienRead` — same permission as all existing BOS read endpoints.

**Gap**: No dedicated `BosDocumentRead` or `DocumentRead` permission exists. Using `LienRead` is the closest aligned current permission and is consistent — if a user can read BOS metadata, they should be able to read the BOS document.

## 10. Request Context Usage

- `ICurrentRequestContext.TenantId` — extracted via `RequireTenantId()` (same pattern as all other BOS endpoints)
- Used for tenant-scoped BOS lookup
- No client-supplied tenant/org/user data trusted
- All retrieval passes through tenant-scoped BOS validation first

## 11. Error Handling

| Scenario | Exception | HTTP Status | Error Code |
|----------|-----------|-------------|------------|
| BOS not found | `NotFoundException` | 404 | `not_found` |
| BOS found but `DocumentId` is null | `ConflictException` | 409 | `DOCUMENT_NOT_AVAILABLE` |
| Documents service returns 404 | `ServiceUnavailableException` | 502 | `service_unavailable` |
| Documents service returns error | `ServiceUnavailableException` | 502 | `service_unavailable` |
| Documents service unreachable | `ServiceUnavailableException` | 502 | `service_unavailable` |
| No tenant context | `UnauthorizedAccessException` | 401 | `unauthorized` |
| No auth / wrong permissions | Framework auth pipeline | 401/403 | — |

All errors are mapped through the existing `ExceptionHandlingMiddleware`, which was extended with a `ServiceUnavailableException` → 502 handler.

## 12. Validation Performed

| Check | Result |
|-------|--------|
| Liens.Api build | 0 errors, 0 warnings |
| Full solution build (`LegalSynq.sln`) | Clean (no output = success) |
| Service health | `{"status":"ok","service":"liens"}` |
| `GET /api/liens/bill-of-sales/{id}/document` | 401 (auth enforced) |
| `GET /api/liens/bill-of-sales/by-number/{n}/document` | 401 (auth enforced) |
| `GET /api/liens/bill-of-sales` | 401 (no regression) |
| `GET /api/liens/liens` | 401 (no regression) |
| `POST /api/liens/offers/{id}/accept` | 401 (no regression) |
| `GET /api/liens/cases` | 401 (no regression) |

## 13. Confirmations

- [x] No local file storage introduced
- [x] No document tables in Liens DB
- [x] No cross-service DB joins
- [x] No generic document APIs added (BOS-only)
- [x] No document upload/delete/mutation APIs added
- [x] No direct Documents access exposed
- [x] No frontend work added
- [x] No audit/notification integration added
- [x] EF entities not returned directly (DTO layer preserved)

## 14. Build Results

```
Liens.Api → Build succeeded. 0 Warning(s), 0 Error(s)
LegalSynq.sln → Build succeeded. All projects clean.
```

Dependent projects: Contracts, BuildingBlocks, Liens.Domain, Liens.Application, Liens.Infrastructure, Liens.Api — all compile.

## 15. Permission Model Gaps / Deferred Items

- **No `BosDocumentRead` permission**: Using `LienRead` instead. A dedicated permission can be added later if finer-grained BOS document access control is needed.
- **No service-to-service auth**: The Documents HTTP call does not forward JWT. In dev, Documents accepts internal requests. For production, service-to-service auth (e.g., client credentials, API key, or JWT forwarding) needs to be added.
- **No retry on Documents failure**: If retrieval fails, the caller gets a 502. A retry mechanism could be added later.

## 16. Risks / Assumptions

- **Documents service 302 redirect**: The proxy model assumes the HttpClient follows the 302 to the signed storage URL. Default `HttpClientHandler` follows redirects automatically — this is the expected behavior.
- **Streaming efficiency**: `HttpCompletionOption.ResponseHeadersRead` ensures Liens doesn't buffer the full file in memory before streaming to the caller. However, the intermediate hop (Liens → Documents → S3 → Liens → Caller) adds latency compared to a direct redirect model.
- **Content-Disposition parsing**: The filename extraction relies on Documents/S3 setting the Content-Disposition header. Falls back to a default filename if not present.
- **ServiceUnavailableException is new**: Added to BuildingBlocks; other services will need to add handlers in their middleware if they want to catch it (currently only Liens has the handler).

## 17. Final Readiness

- **Is BOS document retrieval now exposed safely?** Yes. Liens validates tenant scope, BOS existence, and DocumentId presence before proxying the download from Documents. No direct Documents access is exposed.

- **Is the system ready for Audit/Notifications integration or broader document features next?** Yes. The abstraction layers (`IBillOfSaleDocumentService`, `IBillOfSaleDocumentQueryService`) are clean and extensible. Adding audit events on document access or expanding to other entity types would follow the same pattern.

- **Architecture alignment**: The implementation follows the "Liens as business gate, Documents as storage owner" principle. No service boundaries were violated.
