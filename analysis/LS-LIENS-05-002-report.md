# LS-LIENS-05-002 — Sale Finalization Application Service Report

## 1. Summary

Implemented the core transactional business workflow that finalizes a lien sale by:
- Accepting a target offer
- Creating a Bill of Sale
- Updating the lien to "Sold" status
- Rejecting all competing pending offers

This is the first multi-entity orchestrated workflow in the Liens microservice. All four mutations occur within a single database transaction — if any step fails, the entire operation rolls back.

## 2. Existing v2 Application Service Pattern Identified

Inspected CareConnect, Fund, and existing Liens structure:

| Concern | v2 Pattern | Followed |
|---|---|---|
| Interface placement | `{Service}.Application/Interfaces/I{Name}Service.cs` | ✅ |
| Implementation placement | `{Service}.Application/Services/{Name}Service.cs` | ✅ |
| DI registration | `{Service}.Infrastructure/DependencyInjection.cs` via `AddScoped` | ✅ |
| Exception handling | `BuildingBlocks.Exceptions` (`NotFoundException`, `ConflictException`) | ✅ |
| Transaction handling | EF Core `DbContext` transaction (via `IUnitOfWork` abstraction) | ✅ |
| Return model | Application-layer DTO (not EF entities) | ✅ |

## 3. Exact Files Created/Changed

### Created
| File | Purpose |
|---|---|
| `Liens.Application/Interfaces/ILienSaleService.cs` | Service interface |
| `Liens.Application/Interfaces/IUnitOfWork.cs` | Transaction abstraction (IUnitOfWork + ITransactionScope) |
| `Liens.Application/Services/LienSaleService.cs` | Workflow implementation |
| `Liens.Application/DTOs/SaleFinalizationResult.cs` | Result model |
| `Liens.Infrastructure/Persistence/UnitOfWork.cs` | EF Core UoW implementation |

### Changed
| File | Change |
|---|---|
| `Liens.Application/Liens.Application.csproj` | Added BuildingBlocks + Logging references |
| `Liens.Infrastructure/DependencyInjection.cs` | Added `IUnitOfWork` + `ILienSaleService` registrations |

## 4. Service Interface

```csharp
// Liens.Application/Interfaces/ILienSaleService.cs
public interface ILienSaleService
{
    Task<SaleFinalizationResult> AcceptOfferAsync(
        Guid tenantId,
        Guid lienOfferId,
        Guid actingUserId,
        CancellationToken ct = default);
}
```

## 5. Method Signature

`AcceptOfferAsync(Guid tenantId, Guid lienOfferId, Guid actingUserId, CancellationToken ct)`

- **tenantId** — multi-tenant isolation scope
- **lienOfferId** — target offer to accept
- **actingUserId** — user performing the action (stamps all audit fields)
- **ct** — cancellation token for async operations

## 6. Workflow Step Breakdown

| Step | Action | Domain Method |
|---|---|---|
| 1 | Validate input parameters | Guard clauses |
| 2 | Load target offer by ID + tenant | `ILienOfferRepository.GetByIdAsync` |
| 3 | Check idempotency (already accepted?) | Returns existing result if BOS exists |
| 4 | Validate offer is Pending + not expired | `OfferStatus.Pending`, `IsExpired` |
| 5 | Load associated lien | `ILienRepository.GetByIdAsync` |
| 6 | Validate lien is Offered/UnderReview | Status check |
| 7 | Check no active BOS exists for lien | `IBillOfSaleRepository.GetByLienIdAsync` |
| 8 | Begin transaction | `IUnitOfWork.BeginTransactionAsync` |
| 9 | Accept the target offer | `LienOffer.Accept()` |
| 10 | Create Bill of Sale | `BillOfSale.CreateFromAcceptedOffer()` |
| 11 | Reject competing pending offers | `LienOffer.Reject()` per competitor |
| 12 | Mark lien as sold | `Lien.MarkSold()` |
| 13 | Commit transaction | `ITransactionScope.CommitAsync` |
| 14 | Return structured result | `SaleFinalizationResult` |

## 7. Transaction Handling

- **Abstraction:** `IUnitOfWork` / `ITransactionScope` defined in Application layer
- **Implementation:** `UnitOfWork` in Infrastructure wraps `LiensDbContext.Database.BeginTransactionAsync()`
- **Scope:** All four mutations (accept offer, create BOS, reject competitors, mark lien sold) occur within a single `IDbContextTransaction`
- **Rollback:** Explicit `RollbackAsync` in catch block; transaction is also `IAsyncDisposable` for safety
- **No partial state:** If any repository call or domain validation fails mid-transaction, all changes are rolled back

## 8. Idempotency Handling

**Strategy: Safe return of existing result**

If `AcceptOfferAsync` is called and the offer is already in `Accepted` status with an existing BillOfSale:
- The service returns the existing `SaleFinalizationResult` without re-executing the workflow
- `CompetingOffersRejected` is reported as 0 (already processed)
- No transaction is opened
- Logged as "idempotent return"

If the offer is in any other terminal status (Rejected, Withdrawn, Expired):
- Throws `ConflictException` with error code `OFFER_NOT_ACTIONABLE`

## 9. Repository Interactions

| Repository | Methods Used | Purpose |
|---|---|---|
| `ILienOfferRepository` | `GetByIdAsync`, `GetByLienIdAsync`, `UpdateAsync` | Load target, load competitors, persist status changes |
| `ILienRepository` | `GetByIdAsync`, `UpdateAsync` | Load and update lien |
| `IBillOfSaleRepository` | `GetByLienOfferIdAsync`, `GetByLienIdAsync`, `AddAsync` | Idempotency check, active BOS check, persist new BOS |

## 10. Competing Offers Handling

1. After accepting the target offer, load all offers for the same lien via `GetByLienIdAsync`
2. Skip the accepted offer (by ID comparison)
3. Skip offers already in terminal status (Accepted, Rejected, Withdrawn, Expired)
4. For each remaining `Pending` offer: call `LienOffer.Reject()` with response notes indicating supersession
5. Persist each rejection via `UpdateAsync`
6. Count of rejected offers is included in the result

## 11. Lien Ownership/State Update

Uses `Lien.MarkSold(purchasePrice, buyingOrgId, actingUserId)` which:
- Sets `PurchasePrice` to the offer amount
- Sets `BuyingOrgId` and `HoldingOrgId` to the buyer's org
- Transitions status from `Offered`/`UnderReview` → `Sold`
- Stamps `UpdatedByUserId` and `UpdatedAtUtc`

## 12. Result Model

```csharp
public sealed class SaleFinalizationResult
{
    public Guid AcceptedOfferId { get; init; }
    public string AcceptedOfferStatus { get; init; }
    public Guid LienId { get; init; }
    public string FinalLienStatus { get; init; }
    public Guid BillOfSaleId { get; init; }
    public string BillOfSaleNumber { get; init; }
    public string BillOfSaleStatus { get; init; }
    public decimal PurchaseAmount { get; init; }
    public decimal OriginalLienAmount { get; init; }
    public decimal? DiscountPercent { get; init; }
    public int CompetingOffersRejected { get; init; }
    public DateTime FinalizedAtUtc { get; init; }
}
```

## 13. DI Registration Changes

In `Liens.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddScoped<ILienSaleService, LienSaleService>();
```

Both registered as `Scoped` — consistent with the v2 pattern. Shares the same `LiensDbContext` lifetime as the repositories.

## 14. Validation Performed

- **Build:** All four Liens projects (Domain, Application, Infrastructure, Api) build with 0 errors, 0 warnings
- **Architecture:** Clean Architecture respected — Application layer depends only on Domain + BuildingBlocks; no Infrastructure references from Application
- **Transaction abstraction:** `IUnitOfWork` in Application, `UnitOfWork` in Infrastructure — no EF Core types leak into the Application layer
- **Domain method usage:** All state transitions use existing domain methods (`Accept`, `Reject`, `MarkSold`, `CreateFromAcceptedOffer`) — no direct field mutation

## 15. Confirmations

- ✅ No API endpoints added
- ✅ No cross-service joins introduced
- ✅ No documents/notifications/audit integration added yet
- ✅ No new domain entities created
- ✅ No repositories rewritten
- ✅ No frontend code added
- ✅ Service is structured for future cross-cutting concerns (audit events, notifications, document generation) via additional method calls after the transaction commit

## 16. Build Results

```
Liens.Domain        → Build succeeded. 0 Warning(s) 0 Error(s)
Liens.Application   → Build succeeded. 0 Warning(s) 0 Error(s)
Liens.Infrastructure → Build succeeded. 0 Warning(s) 0 Error(s)
Liens.Api           → Build succeeded. 0 Warning(s) 0 Error(s)
```

## 17. Risks / Assumptions

| Risk | Mitigation |
|---|---|
| BillOfSaleNumber collision under high concurrency | Timestamp-based generation (`BOS-{LienNumber}-{yyyyMMddHHmmss}`) is sufficient for current scale; can add sequence/GUID suffix if needed |
| Competing offer load is unbounded | In practice, lien offers are low-cardinality per lien; can add pagination if needed |
| MySQL transaction isolation | Default `REPEATABLE READ` for InnoDB is appropriate for this workflow |
| No retry logic for transient DB failures | EF Core retry-on-failure is configured at the DbContext level; application-level retry is deferred |

## 18. Final Readiness Statement

**Is the sale finalization application service established?**
Yes. The `ILienSaleService` / `LienSaleService` implements the complete Accept Offer → Create BillOfSale → Reject Competitors → Mark Sold workflow with full transactional safety, idempotency handling, and structured error reporting.

**Is the system ready for API exposure of the workflow?**
Yes. The next step is to create a minimal API endpoint or controller that:
1. Extracts `tenantId` and `actingUserId` from the authenticated request context
2. Calls `ILienSaleService.AcceptOfferAsync`
3. Maps `SaleFinalizationResult` to an HTTP response

No workflow restructuring is needed — the service is fully consumable via DI.
