# LS-LIENS-06-003: LienOffer HTTP APIs — Implementation Report

## 1. Summary

Implemented four database-backed LienOffer HTTP endpoints that expose the buyer/seller offer interaction layer of the marketplace. The feature supports offer creation with full validation, single-offer retrieval, lien-scoped offer listing, and paginated search with buyer/seller/status filtering. No accept/reject/withdraw/finalize operations are exposed — those belong to separate workflow features.

## 2. Existing Pattern Followed

The implementation mirrors the Lien/Case API pattern established in LS-LIENS-06-001 and LS-LIENS-06-002:

- **DTOs**: Sealed classes in `Liens.Application.DTOs`, response uses `init` properties, request uses only client-supplied fields
- **Application service**: Interface in `Liens.Application.Interfaces`, implementation in `Liens.Application.Services`
- **Endpoint structure**: Static extension method `MapLienOfferEndpoints` with `MapGroup`, `RequireAuthorization`, `RequireProductAccess`, `RequirePermission`
- **Request context**: `RequireTenantId`/`RequireUserId`/`RequireOrgId` helper pattern (same as `LienEndpoints`)
- **Response shape**: DTOs returned via `Results.Ok`/`Results.Created`/`Results.NotFound` with consistent error shape
- **Pagination**: `PaginatedResult<T>` with `Items`/`Page`/`PageSize`/`TotalCount`
- **Error handling**: `ValidationException` (→400), `NotFoundException` (→404), `InvalidOperationException` (→409) via existing middleware
- **Mapping**: Private static `MapToResponse` method in application service (never returns EF entities)

## 3. Files Created/Changed

### Created
| File | Layer |
|------|-------|
| `Liens.Application/DTOs/LienOfferResponse.cs` | Application |
| `Liens.Application/DTOs/CreateLienOfferRequest.cs` | Application |
| `Liens.Application/Interfaces/ILienOfferService.cs` | Application |
| `Liens.Application/Services/LienOfferService.cs` | Application |
| `Liens.Api/Endpoints/LienOfferEndpoints.cs` | API |

### Modified
| File | Change |
|------|--------|
| `Liens.Application/Repositories/ILienOfferRepository.cs` | Added `buyerOrgId`/`sellerOrgId` params to `SearchAsync`; added `HasActiveOfferAsync` |
| `Liens.Infrastructure/Repositories/LienOfferRepository.cs` | Implemented extended `SearchAsync` and `HasActiveOfferAsync` |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `ILienOfferService → LienOfferService` |
| `Liens.Api/Program.cs` | Added `app.MapLienOfferEndpoints()` |

## 4. DTOs Added

### `LienOfferResponse`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | |
| `LienId` | `Guid` | |
| `OfferAmount` | `decimal` | |
| `Status` | `string` | Pending/Accepted/Rejected/Withdrawn/Expired |
| `BuyerOrgId` | `Guid` | Scalar ID only |
| `SellerOrgId` | `Guid` | Scalar ID only |
| `Notes` | `string?` | |
| `ResponseNotes` | `string?` | |
| `ExternalReference` | `string?` | |
| `OfferedAtUtc` | `DateTime` | |
| `ExpiresAtUtc` | `DateTime?` | |
| `RespondedAtUtc` | `DateTime?` | |
| `WithdrawnAtUtc` | `DateTime?` | |
| `IsExpired` | `bool` | Computed: status=Expired OR (Pending + past expiry) |
| `CreatedAtUtc` | `DateTime` | |
| `UpdatedAtUtc` | `DateTime` | |

### `CreateLienOfferRequest`
| Field | Type | Notes |
|-------|------|-------|
| `LienId` | `Guid` | Required |
| `OfferAmount` | `decimal` | Required, must be > 0 |
| `Notes` | `string?` | Optional |
| `ExpiresAtUtc` | `DateTime?` | Optional, must be future |

**Excluded from request** (derived from context/domain): `buyerOrgId`, `sellerOrgId`, `status`, `tenantId`, `actingUserId`, all audit fields.

## 5. Application Service

### `ILienOfferService` / `LienOfferService`

| Method | Description |
|--------|-------------|
| `SearchAsync` | Paginated tenant-scoped search with lienId/status/buyerOrgId/sellerOrgId filters |
| `GetByIdAsync` | Tenant-scoped single offer retrieval |
| `GetByLienIdAsync` | All offers for a lien (tenant-scoped), ordered by most recent |
| `CreateAsync` | Full validation + persistence (see validation rules below) |

### CreateAsync Validation Rules
1. `lienId` required (non-empty GUID)
2. `offerAmount` must be positive
3. `expiresAtUtc` must be in the future if provided
4. Lien must exist within tenant
5. Lien must be in offerable state (`Offered` or `UnderReview`)
6. Seller org derived from `lien.SellingOrgId ?? lien.OrgId`
7. Buyer org (from request context) must differ from seller org
8. One active (Pending) offer per buyer org per lien enforced

## 6. Endpoint List

| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| `GET` | `/api/liens/offers` | `LienRead` | Search/list offers with filters |
| `GET` | `/api/liens/offers/{id}` | `LienRead` | Get offer by ID |
| `POST` | `/api/liens/offers` | `LienOffer` | Create new offer |
| `GET` | `/api/liens/liens/{lienId}/offers` | `LienRead` | List offers for a lien |

Query parameters for search: `lienId`, `status`, `buyerOrgId`, `sellerOrgId`, `page`, `pageSize`.

## 7. Repository Interactions

| Repository | Methods Used | Purpose |
|------------|-------------|---------|
| `ILienOfferRepository` | `SearchAsync`, `GetByIdAsync`, `GetByLienIdAsync`, `AddAsync`, `HasActiveOfferAsync` | Core CRUD + duplicate check |
| `ILienRepository` | `GetByIdAsync` | Validate target lien exists, get status, derive seller org |

No cross-service repositories or DB joins introduced.

## 8. Authorization Approach

- All endpoints require `Policies.AuthenticatedUser` + `RequireProductAccess(SYNQ_LIENS)`
- GET endpoints use `LienRead` permission (consistent with Lien/Case APIs)
- POST uses `LienOffer` permission (already defined in `LiensPermissions.cs`)
- No new permissions were created — existing permission catalog is sufficient

### Permission Gap Notes
- `LienOffer` (`SYNQ_LIENS.lien:offer`) is used for create — this permission needs to be seeded in Identity for buyer roles
- No separate `LienOfferRead` permission exists; `LienRead` is used for read operations, which is consistent with the architectural pattern where lien data visibility is tenant-scoped
- Future: If fine-grained offer visibility is needed (e.g., buyers can only see their own offers), a `LienOfferReadOwn` permission + org-scoping logic can be added

## 9. Request Context Usage

| Operation | TenantId | UserId | OrgId |
|-----------|----------|--------|-------|
| Search | ✅ Required | — | — |
| GetById | ✅ Required | — | — |
| GetByLienId | ✅ Required | — | — |
| Create | ✅ Required | ✅ Required (audit) | ✅ Required (buyer org) |

- `buyerOrgId` for create is **always** derived from `ctx.OrgId` — never client-supplied
- `sellerOrgId` is **always** derived from the lien entity — never client-supplied
- `tenantId` is **always** from request context — never client-supplied

### Search Filter Design Decision
The `buyerOrgId` and `sellerOrgId` query parameters on `GET /api/liens/offers` are openly filterable. This is intentional for the marketplace use case:
- Within a tenant, offer visibility across orgs is expected (sellers need to see incoming offers, buyers need to see offers on liens they're interested in)
- Access is already gated by `LienRead` permission at the tenant level
- This is consistent with the Lien API itself which returns all liens within a tenant without org-scoping
- If org-level isolation is needed in the future, it can be added via `LienOfferReadOwn` permission + automatic `buyerOrgId` constraint

## 10. Donor API Compatibility Notes

- The LienOffer entity's `Create` factory method is preserved unchanged
- `OfferStatus` enum values match donor behavior (Pending → Accepted/Rejected/Withdrawn/Expired)
- One-active-offer-per-buyer-per-lien enforcement aligns with marketplace best practices
- Offerable states (`Offered`/`UnderReview`) match the donor lien lifecycle where liens are listed on the marketplace
- `ExternalReference` field preserved in response DTO for donor system cross-referencing

## 11. Buyer/Seller/Lien-Scoped Offer Views

| View | How Supported |
|------|---------------|
| All offers for a lien | `GET /api/liens/liens/{lienId}/offers` (convenience route) or `GET /api/liens/offers?lienId=...` |
| Offers submitted by a buyer org | `GET /api/liens/offers?buyerOrgId=...` |
| Offers received by a seller org | `GET /api/liens/offers?sellerOrgId=...` |
| Offers by status | `GET /api/liens/offers?status=Pending` |
| Combined filters | All query params are composable |

## 12. Validation Performed

| Check | Result |
|-------|--------|
| Liens service builds | ✅ 0 errors, 0 warnings |
| Full solution builds | ✅ (all dependent projects compile) |
| Endpoints compile and resolve | ✅ (verified via build) |
| No regression to Case APIs | ✅ (CaseEndpoints unchanged) |
| No regression to Lien APIs | ✅ (LienEndpoints unchanged) |
| No regression to LienSaleService | ✅ (does not call modified `SearchAsync`) |
| Auth filters applied | ✅ (RequireAuthorization + RequireProductAccess + RequirePermission) |
| Repository interface backward-compatible | ⚠️ Signature changed in-place (no other callers; build clean) |

## 13. Architecture Confirmations

- ✅ No EF entities returned directly — all responses use DTOs via `MapToResponse`
- ✅ No cross-service DB joins — only `ILienOfferRepository` and `ILienRepository` (same service)
- ✅ No unrelated APIs added — feature is strictly LienOffer creation + retrieval
- ✅ No sale-finalization logic exposed — no accept/reject/withdraw/finalize endpoints
- ✅ No BillOfSale APIs added
- ✅ No Identity/Documents/Audit/Notifications integration calls
- ✅ No frontend work added

## 14. Build Results

```
Liens.Api → Build succeeded. 0 Warning(s), 0 Error(s)
  - Contracts.dll ✅
  - BuildingBlocks.dll ✅
  - Liens.Domain.dll ✅
  - Liens.Application.dll ✅
  - Liens.Infrastructure.dll ✅
  - Liens.Api.dll ✅
```

## 15. Permission Model Gaps / Deferred Items

| Item | Status |
|------|--------|
| `LienOffer` permission needs seeding in Identity | Deferred — same as `LienRead`/`LienUpdate` from LS-LIENS-06-002 |
| Org-level access control on search | Documented as design decision; future `LienOfferReadOwn` possible |
| DB-level uniqueness constraint for one-active-offer-per-buyer-per-lien | Application-level check only; DB constraint recommended for production |

## 16. Risks / Assumptions

1. **Offerable states**: Assumed `Offered` and `UnderReview` are the correct lien states for receiving offers. If additional states should be offerable, the `OfferableStatuses` set in `LienOfferService` can be extended.
2. **Seller org derivation**: Uses `lien.SellingOrgId ?? lien.OrgId` — assumes `OrgId` is the owning/selling org when `SellingOrgId` is not explicitly set.
3. **Race condition**: The duplicate-offer check (`HasActiveOfferAsync`) is application-level only. Under high concurrency, a DB unique index on `(TenantId, LienId, BuyerOrgId)` with a partial filter on `Status = 'Pending'` is recommended.
4. **Permission seeding**: `LienOffer` permission must be seeded in Identity service before buyers can create offers in production.

## 17. Final Readiness Statement

### Are real LienOffer APIs established?
**Yes.** Four database-backed endpoints are implemented with full validation, tenant-scoped access, proper authorization, and clean architectural boundaries. The APIs follow the established v2 pattern exactly.

### Is the system ready for BillOfSale APIs or sale workflow exposure next?
**Yes.** The offer interaction layer is cleanly separated from sale-finalization. The `LienSaleService` (LS-LIENS-05-002) remains untouched and can be exposed through dedicated workflow endpoints. Accept/reject/withdraw operations can be added as separate endpoint groups that invoke the existing domain methods on `LienOffer`.
