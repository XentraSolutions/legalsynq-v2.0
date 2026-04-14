# LS-LIENS-06-004 — BillOfSale HTTP APIs Report

## 1. Summary

Implemented read-only BillOfSale HTTP APIs exposing retrieval and listing operations through the existing Liens v2 architecture. Four endpoints support get-by-id, get-by-number, paginated search with filters, and lien-scoped listing. No mutation endpoints are exposed.

## 2. Existing Pattern Followed

All implementation follows the established Case/Lien/LienOffer API patterns:
- Response DTO in `Liens.Application.DTOs`
- Application service interface in `Liens.Application.Interfaces`
- Application service implementation in `Liens.Application.Services`
- Endpoint class as static extension on `WebApplication`
- `RequireTenantId()` helper for tenant-scoped context extraction
- `PaginatedResult<T>` for list/search responses
- Auth via `RequireAuthorization` + `RequireProductAccess` + `RequirePermission` filters
- 404 responses use `{ error: { code, message } }` shape
- Private `MapToResponse()` for entity-to-DTO mapping
- Repository-backed queries, no EF entities returned directly

## 3. Files Created/Changed

### Created
| File | Purpose |
|------|---------|
| `Liens.Application/DTOs/BillOfSaleResponse.cs` | Response DTO |
| `Liens.Application/Interfaces/IBillOfSaleService.cs` | Application service interface |
| `Liens.Application/Services/BillOfSaleService.cs` | Application service implementation |
| `Liens.Api/Endpoints/BillOfSaleEndpoints.cs` | Endpoint definitions |

### Changed
| File | Change |
|------|--------|
| `Liens.Application/Repositories/IBillOfSaleRepository.cs` | Added `GetByBillOfSaleNumberAsync`; extended `SearchAsync` with `buyerOrgId`, `sellerOrgId`, `search` params |
| `Liens.Infrastructure/Repositories/BillOfSaleRepository.cs` | Implemented new repository methods |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `IBillOfSaleService` → `BillOfSaleService` |
| `Liens.Api/Program.cs` | Added `app.MapBillOfSaleEndpoints()` |

## 4. DTOs

### BillOfSaleResponse
| Field | Type |
|-------|------|
| Id | Guid |
| BillOfSaleNumber | string |
| ExternalReference | string? |
| Status | string |
| LienId | Guid |
| LienOfferId | Guid |
| SellerOrgId | Guid |
| BuyerOrgId | Guid |
| PurchaseAmount | decimal |
| OriginalLienAmount | decimal |
| DiscountPercent | decimal? |
| SellerContactName | string? |
| BuyerContactName | string? |
| Terms | string? |
| Notes | string? |
| DocumentId | Guid? |
| IssuedAtUtc | DateTime |
| ExecutedAtUtc | DateTime? |
| EffectiveAtUtc | DateTime? |
| CancelledAtUtc | DateTime? |
| CreatedAtUtc | DateTime |
| UpdatedAtUtc | DateTime |

No create/update request DTOs — this feature is read-only.

## 5. Application Service

### IBillOfSaleService / BillOfSaleService
| Method | Description |
|--------|-------------|
| `GetByIdAsync(tenantId, id)` | Tenant-scoped retrieval by primary key |
| `GetByBillOfSaleNumberAsync(tenantId, billOfSaleNumber)` | Tenant-scoped retrieval by business key |
| `SearchAsync(tenantId, lienId?, status?, buyerOrgId?, sellerOrgId?, search?, page, pageSize)` | Paginated search with filters; pageSize clamped 1–100 |
| `GetByLienIdAsync(tenantId, lienId)` | All BOS records for a lien, ordered by IssuedAtUtc desc |

## 6. Endpoint Routes

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/liens/bill-of-sales` | LienRead | Paginated search with filters |
| GET | `/api/liens/bill-of-sales/{id}` | LienRead | Get by id |
| GET | `/api/liens/bill-of-sales/by-number/{billOfSaleNumber}` | LienRead | Get by BOS number |
| GET | `/api/liens/liens/{lienId}/bill-of-sales` | LienRead | All BOS for a lien |

### Search Query Parameters
- `search` — matches against BillOfSaleNumber
- `status` — exact match
- `lienId` — filter by lien
- `sellerOrgId` — filter by seller org
- `buyerOrgId` — filter by buyer org
- `page` — default 1
- `pageSize` — default 20, max 100

### Design Choice: Dedicated by-number route
A dedicated `/by-number/{billOfSaleNumber}` route is provided (matching the Case and Lien pattern of `/by-number/{caseNumber}` and `/by-number/{lienNumber}`) rather than folding BOS number lookup into the search endpoint. This keeps the established API convention consistent.

## 7. Repository Interactions

| Method | Source |
|--------|--------|
| `GetByIdAsync` | Existing |
| `GetByBillOfSaleNumberAsync` | New — simple tenant + number query |
| `GetByLienIdAsync` | Existing |
| `SearchAsync` | Extended — added `buyerOrgId`, `sellerOrgId`, `search` filter params |
| `AddAsync` / `UpdateAsync` | Existing, not called by this feature |

## 8. Authorization

All endpoints require:
1. `Policies.AuthenticatedUser` — valid JWT
2. `RequireProductAccess(LiensPermissions.ProductCode)` — org entitled to SYNQ_LIENS product
3. `RequirePermission(LiensPermissions.LienRead)` — fine-grained read permission

### Permission Gap
No dedicated `BillOfSaleRead` permission exists in the permission catalog. `LienRead` (`SYNQ_LIENS.lien:read`) is used as the closest aligned permission. If BOS-specific granularity is needed later, a `SYNQ_LIENS.bos:read` permission can be added and swapped in without API changes.

## 9. Request Context Usage

- `ICurrentRequestContext.TenantId` is extracted via `RequireTenantId()` in every endpoint handler
- All repository queries are tenant-scoped as the first filter
- Client-supplied tenant/org identifiers are never trusted for scoping
- `buyerOrgId`/`sellerOrgId` query params are openly filterable within tenant scope (same pattern as LienOffer)

## 10. Donor API Compatibility

- BillOfSale entity and factory method (`CreateFromAcceptedOffer`) unchanged
- Mutation methods (`SubmitForExecution`, `MarkExecuted`, `Cancel`, `UpdateDraft`, `AttachDocument`) untouched
- `LienSaleService` workflow unchanged — no callers of the old `SearchAsync` signature existed outside `BillOfSaleService`
- Repository `AddAsync`/`UpdateAsync` preserved for workflow use

## 11. Buyer/Seller/Lien-Scoped Views

Supported via search filters:
- **By lien**: `GET /api/liens/liens/{lienId}/bill-of-sales` or `?lienId=` on search
- **By seller org**: `?sellerOrgId=` on search
- **By buyer org**: `?buyerOrgId=` on search
- **By status**: `?status=` on search

All return scalar IDs only — no cross-service enrichment for org/user/document names.

## 12. Validation Performed

| Check | Result |
|-------|--------|
| Liens.Api build | 0 errors, 0 warnings |
| All layers compile (Domain, Application, Infrastructure, Api) | ✅ |
| Endpoints resolve (HTTP 401 without auth) | ✅ |
| GET /api/liens/bill-of-sales | 401 (auth enforced) |
| GET /api/liens/bill-of-sales/{id} | 401 (auth enforced) |
| GET /api/liens/bill-of-sales/by-number/{num} | 401 (auth enforced) |
| GET /api/liens/liens/{id}/bill-of-sales | 401 (auth enforced) |
| No regression: offers endpoint | 401 (unchanged) |
| No regression: liens endpoint | 401 (unchanged) |
| No regression: cases endpoint | 401 (unchanged) |
| Service health | `{"status":"ok","service":"liens"}` |

## 13. Confirmations

- ✅ No EF entities returned directly — all mapped through `MapToResponse`
- ✅ No cross-service DB joins
- ✅ No unrelated APIs added
- ✅ No BOS workflow mutation exposed (no create/update/execute/cancel endpoints)
- ✅ No sale-finalization endpoint exposure
- ✅ No Identity/Documents/Audit/Notifications integration
- ✅ No frontend work

## 14. Build Results

```
Liens.Api → Build succeeded. 0 Warning(s), 0 Error(s)
```

All dependent projects built: Contracts, BuildingBlocks, Liens.Domain, Liens.Application, Liens.Infrastructure, Liens.Api.

## 15. Permission-Model Gaps / Deferred Items

| Item | Status |
|------|--------|
| `SYNQ_LIENS.bos:read` permission | Not yet in catalog; using `LienRead` |
| BOS create/update/execute/cancel endpoints | Deferred to workflow feature |
| Document generation/attachment | Deferred |
| Cross-service org/user name enrichment | Deferred |
| Audit trail integration | Deferred |
| `LienRead`/`LienUpdate`/`LienOffer` permission seeding in Identity | Still pending from LS-LIENS-06-002/003 |

## 16. Risks / Assumptions

- **BOS number uniqueness**: `GetByBillOfSaleNumberAsync` assumes BOS numbers are unique per tenant. The domain factory uses `Guid.NewGuid()`-derived patterns but the DB schema should enforce a unique index on `(TenantId, BillOfSaleNumber)` — verify with DB schema.
- **Search by BOS number**: Uses `LIKE`/`Contains` for the `search` parameter. For large datasets, consider adding a database index on `BillOfSaleNumber`.
- **No data yet**: No BOS records exist in the database since the sale-finalization workflow hasn't been exposed. Endpoints will return empty results until that workflow is implemented.

## 17. Final Readiness

- ✅ Real BillOfSale APIs are established and database-backed
- ✅ All four endpoint routes resolve, compile, and enforce authorization
- ✅ System is ready for sale workflow endpoint exposure as the next feature
- ✅ Remaining domain gaps: BOS mutation endpoints, sale finalization, document generation, permission seeding
