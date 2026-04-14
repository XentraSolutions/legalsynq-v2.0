# LS-LIENS-06-005 — Sale Finalization Endpoint Report

## 1. Summary

Exposed the existing `ILienSaleService.AcceptOfferAsync` workflow through a single HTTP endpoint. The endpoint is a thin handler that extracts request context, delegates to the application service, and returns the existing `SaleFinalizationResult` DTO. No workflow logic was duplicated or modified.

## 2. Existing Pattern Followed

The endpoint was added to `LienOfferEndpoints.cs` since it is an action on an offer. It follows the same patterns as `CreateOffer`:
- Route registered in `MapLienOfferEndpoints` within the existing `offersGroup`
- Auth filters via `RequireAuthorization` + `RequireProductAccess` + `RequirePermission`
- `RequireTenantId()` and `RequireUserId()` helpers for context extraction
- Service injected via DI parameter binding
- Returns `Results.Ok(result)` with the DTO

## 3. Files Changed

| File | Change |
|------|--------|
| `Liens.Api/Endpoints/LienOfferEndpoints.cs` | Added `POST /{offerId}/accept` route + `AcceptOffer` handler method |

No new files were created. No other files were modified.

## 4. Endpoint Route

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/liens/offers/{offerId:guid}/accept` | LienUpdate | Accept offer and finalize sale |

Bodyless POST — no request body required. The `offerId` route parameter identifies the target offer.

## 5. Request Context Usage

| Field | Source | Usage |
|-------|--------|-------|
| `tenantId` | `ICurrentRequestContext.TenantId` via `RequireTenantId()` | Passed to `AcceptOfferAsync` for tenant scoping |
| `userId` | `ICurrentRequestContext.UserId` via `RequireUserId()` | Passed as `actingUserId` for audit trail |

No client-supplied tenant/user/org identifiers are trusted.

## 6. Authorization

- `Policies.AuthenticatedUser` — valid JWT (group level)
- `RequireProductAccess(LiensPermissions.ProductCode)` — org entitled to SYNQ_LIENS (group level)
- `RequirePermission(LiensPermissions.LienUpdate)` — seller-side mutation permission (endpoint level)

### Permission Gap
No dedicated `SYNQ_LIENS.sale:finalize` permission exists. `LienUpdate` (`SYNQ_LIENS.lien:update`) is used as the closest aligned seller-side permission. A more granular permission can be added later without API changes.

## 7. Service Invocation

```
ILienSaleService.AcceptOfferAsync(tenantId, offerId, userId, ct)
```

Single call. The endpoint does not:
- Load repositories
- Validate offer/lien state
- Create BillOfSale
- Reject competing offers
- Update lien status

All of that remains in `LienSaleService`.

## 8. Response Model

Returns `SaleFinalizationResult` directly (already a DTO, not an EF entity):

| Field | Type |
|-------|------|
| AcceptedOfferId | Guid |
| AcceptedOfferStatus | string |
| LienId | Guid |
| FinalLienStatus | string |
| BillOfSaleId | Guid |
| BillOfSaleNumber | string |
| BillOfSaleStatus | string |
| PurchaseAmount | decimal |
| OriginalLienAmount | decimal |
| DiscountPercent | decimal? |
| CompetingOffersRejected | int |
| FinalizedAtUtc | DateTime |

## 9. Idempotent Behavior

`LienSaleService.AcceptOfferAsync` already handles idempotency:
- If the offer is already `Accepted` and a BillOfSale exists, it returns the existing result with `CompetingOffersRejected = 0`
- The endpoint simply passes through this behavior — no additional idempotency logic was added

Error scenarios handled by the service (propagated via exception middleware):
- `NotFoundException` — offer or lien not found
- `ConflictException` — offer not actionable, expired, or lien already has active BOS
- `ValidationException` — invalid input parameters

## 10. Validation Performed

| Check | Result |
|-------|--------|
| Liens.Api build | 0 errors, 0 warnings |
| All layers compile | ✅ |
| POST /api/liens/offers/{offerId}/accept | 401 (auth enforced) |
| No regression: GET /api/liens/offers | 401 ✅ |
| No regression: GET /api/liens/liens | 401 ✅ |
| No regression: GET /api/liens/cases | 401 ✅ |
| No regression: GET /api/liens/bill-of-sales | 401 ✅ |
| Service health | `{"status":"ok","service":"liens"}` |

## 11. Confirmations

- ✅ No workflow logic duplicated in endpoint
- ✅ No cross-service DB joins introduced
- ✅ No unrelated workflow endpoints added
- ✅ No generic offer accept/reject APIs added
- ✅ No BOS mutation endpoints added
- ✅ No document generation, notifications, or audit integration
- ✅ No EF entities returned directly

## 12. Build Results

```
Liens.Api → Build succeeded. 0 Warning(s), 0 Error(s)
```

All dependent projects built: Contracts, BuildingBlocks, Liens.Domain, Liens.Application, Liens.Infrastructure, Liens.Api.

## 13. Permission-Model Gaps / Deferred Items

| Item | Status |
|------|--------|
| `SYNQ_LIENS.sale:finalize` permission | Not yet in catalog; using `LienUpdate` |
| Generic offer accept/reject endpoints | Deferred |
| BOS mutation endpoints | Deferred |
| Document generation on sale | Deferred |
| Notifications on sale | Deferred |
| Audit events on sale | Deferred |
| Permission seeding in Identity | Still pending from prior features |

## 14. Risks / Assumptions

- **Permission scope**: `LienUpdate` grants broad lien mutation access. Users with `LienUpdate` can also update lien fields directly. A dedicated `sale:finalize` permission would provide tighter access control.
- **Seller identity not enforced at endpoint level**: The service validates offer/lien state but does not verify the caller's org is the seller org. This is consistent with the current pattern where authorization is permission-based, not org-ownership-based. Org-level authorization can be layered in later.
- **No rate limiting**: Repeated calls are handled idempotently by the service, but no explicit rate limiting is applied.

## 15. Final Readiness

- ✅ Sale finalization workflow is now exposed via `POST /api/liens/offers/{offerId}/accept`
- ✅ Endpoint is thin, authorized, and delegates entirely to `ILienSaleService`
- ✅ System is ready for post-sale integrations (document generation, notifications, audit events) as next features
