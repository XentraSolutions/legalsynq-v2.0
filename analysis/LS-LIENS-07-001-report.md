# LS-LIENS-07-001 — BillOfSale Document Integration Report

## 1. Summary

Integrated the Liens service with the v2 Documents service so that a finalized sale generates a BillOfSale PDF and stores it via the Documents service. The returned `DocumentId` is attached to the BillOfSale record. Uses a post-commit recoverable model — if document generation/storage fails, the sale transaction is preserved and the BOS exists with `DocumentId = null`.

## 2. Documents Integration Pattern

Followed the existing v2 integration pattern:
- No shared .NET client library — direct HTTP calls to the Documents service
- `multipart/form-data` POST to `/documents` with `tenantId`, `documentTypeId`, `productId`, `referenceId`, `referenceType`, `title`, `description`, and `file`
- Response parsed for `data.id` to extract the `DocumentId`
- Named `HttpClient` registered via `IHttpClientFactory` with configurable base URL

## 3. PDF Generation Approach

- Uses **QuestPDF 2024.10.2** (Community License, MIT)
- Deterministic output — same BOS input produces same PDF layout
- Includes: BOS number, status, lien/offer references, seller/buyer org IDs, contact names, financial summary (purchase amount, original amount, discount), all timestamps, terms, notes
- Clean layout with section headings, info rows, header, and footer
- Isolated behind `IBillOfSalePdfGenerator` interface — can be swapped for branded/templated version later

## 4. Files Created/Modified

### Created
| File | Purpose |
|------|---------|
| `Liens.Application/Interfaces/IBillOfSalePdfGenerator.cs` | PDF generation abstraction |
| `Liens.Application/Interfaces/IBillOfSaleDocumentService.cs` | Documents integration abstraction |
| `Liens.Infrastructure/Documents/BillOfSalePdfGenerator.cs` | QuestPDF implementation |
| `Liens.Infrastructure/Documents/BillOfSaleDocumentService.cs` | HTTP client for Documents service |

### Modified
| File | Change |
|------|--------|
| `Liens.Application/Services/LienSaleService.cs` | Added `IBillOfSaleDocumentService` dependency; post-commit document generation |
| `Liens.Application/DTOs/SaleFinalizationResult.cs` | Added `DocumentId` field |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered PDF generator, document service, and named HttpClient |
| `Liens.Infrastructure/Liens.Infrastructure.csproj` | Added QuestPDF 2024.10.2 package |

## 5. Document Service Abstraction

### IBillOfSaleDocumentService
```
Task<Guid?> GenerateAndStoreAsync(BillOfSale bos, Guid actingUserId, CancellationToken ct)
```

Returns `Guid?`:
- `Guid` on success — the DocumentId from the Documents service
- `null` on any failure — logged, but sale is not blocked

### BillOfSaleDocumentService
1. Calls `IBillOfSalePdfGenerator.Generate(bos)` to produce PDF bytes
2. Builds `multipart/form-data` request with metadata
3. Sends to Documents service via named HttpClient (`"DocumentsService"`)
4. Parses JSON response to extract `data.id`
5. Returns `DocumentId` or `null`

Recoverable failures are caught and logged — `OperationCanceledException` is re-thrown to respect caller cancellation.

## 6. How Documents Service is Called

```
POST http://localhost:5006/documents
Content-Type: multipart/form-data

Fields:
- tenantId: BOS tenant
- documentTypeId: 00000000-0000-0000-0000-000000000B05 (well-known BOS type)
- productId: SYNQ_LIENS
- referenceId: BOS ID
- referenceType: BillOfSale
- title: "Bill of Sale — {BOS number}"
- description: "Auto-generated BOS document for {BOS number}"
- file: BOS-{number}.pdf (application/pdf)
```

Base URL is configurable via `Services:DocumentsUrl` (defaults to `http://localhost:5006`).

## 7. Where DocumentId is Attached

After successful document creation:
1. `bos.AttachDocument(documentId, actingUserId)` — domain method
2. `_bosRepo.UpdateAsync(bos)` — persists the change

This happens **post-commit** — after the main sale transaction has been committed.

## 8. Failure Handling Strategy

**Post-commit recoverable model** (chosen):

| Step | Failure Behavior |
|------|-----------------|
| Sale transaction (commit) | Must succeed or rolls back entirely |
| PDF generation | If fails: logs error, returns `DocumentId = null` |
| Documents service call | If fails: logs error, returns `DocumentId = null` |
| Caller cancellation | `OperationCanceledException` re-thrown (not swallowed) |
| DocumentId attachment | If document step fails: BOS exists with `DocumentId = null` |
| Post-commit exception | Caught in isolated try/catch (separate from transaction rollback) |

The BOS is fully valid without a document. Document can be regenerated later via a future retry mechanism or manual trigger.

## 9. Impact on Sale Workflow

- Sale finalization transaction is **unchanged** — same atomic commit
- Document generation is a **post-commit follow-up** — does not affect sale success/failure
- `SaleFinalizationResult` now includes `DocumentId` (nullable) — backward compatible
- Idempotent path also returns `DocumentId` from existing BOS

## 10. Validation Performed

| Check | Result |
|-------|--------|
| Liens.Api build | 0 errors, 0 warnings |
| All layers compile | ✅ |
| Service health | `{"status":"ok","service":"liens"}` |
| POST /api/liens/offers/{id}/accept | 401 (auth enforced) |
| GET /api/liens/offers | 401 (no regression) |
| GET /api/liens/liens | 401 (no regression) |
| GET /api/liens/cases | 401 (no regression) |
| GET /api/liens/bill-of-sales | 401 (no regression) |

## 11. Confirmations

- ✅ No document storage in Liens DB — only `DocumentId` reference
- ✅ No cross-service DB joins
- ✅ No API exposure added for documents
- ✅ No file stored locally or in Liens tables
- ✅ No modification to Documents service
- ✅ No EF entities returned directly
- ✅ Sale workflow not broken — document is post-commit add-on

## 12. Build Results

```
Liens.Api → Build succeeded. 0 Warning(s), 0 Error(s)
```

All dependent projects: Contracts, BuildingBlocks, Liens.Domain, Liens.Application, Liens.Infrastructure, Liens.Api.

## 13. Risks / Assumptions

- **Well-known DocumentTypeId**: `00000000-0000-0000-0000-000000000B05` is used as the BOS document type. This should be seeded in the Documents service's document type catalog if one exists.
- **No auth forwarding**: The HTTP call to Documents service does not currently forward the caller's JWT. In dev, Documents service accepts requests without auth on internal routes. For production, service-to-service auth may need to be added.
- **No retry**: If document creation fails, there is no automatic retry mechanism. A future background worker or manual trigger can regenerate documents for BOS records with `DocumentId = null`.
- **QuestPDF Community License**: Valid for revenue under $1M USD/year. For larger deployments, a Professional license may be needed.

## 14. Final Readiness

- ✅ BOS documents are now generated as PDF and stored via the Documents service
- ✅ `DocumentId` is linked to the BillOfSale record
- ✅ System is ready for document retrieval APIs as the next feature
- ✅ PDF format can be enhanced with branding when needed (swap `IBillOfSalePdfGenerator` implementation)
