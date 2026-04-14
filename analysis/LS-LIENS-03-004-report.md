# LS-LIENS-03-004 — BillOfSale Domain Entity Report

**Date:** 2026-04-14  
**Scope:** Domain modeling only — `BillOfSale` entity for the Liens microservice  
**Status:** Complete

---

## 1. Summary

Created the `BillOfSale` domain entity as the legal and financial transfer record produced when a lien sale is completed through an accepted `LienOffer`. The entity captures seller/buyer snapshots, purchase financials, execution timestamps, and supports the full Draft → Pending → Executed lifecycle with optional cancellation. One supporting status constants class was added.

---

## 2. Existing Patterns Identified and Followed

| Pattern | Source | Applied in BillOfSale |
|---------|--------|-----------------------|
| `AuditableEntity` base class | `BuildingBlocks.Domain` | ✓ Inherits `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId` |
| Private constructor + static `Create` factory | `Lien`, `LienOffer`, `Case`, `Contact` | ✓ `CreateFromAcceptedOffer(...)` factory method |
| `private set` on all properties | All Liens entities | ✓ All properties use `private set` |
| `string` status with `const` class | `LienStatus`, `OfferStatus`, `CaseStatus` | ✓ `BillOfSaleStatus` static class with `All`, `Open`, `Terminal`, `AllowedTransitions` |
| Guard clauses in factory and mutation methods | `Lien.Create()`, `LienOffer.Create()` | ✓ `ArgumentException` / `InvalidOperationException` guards |
| `Guid TenantId` + org scoping | `Lien` (TenantId + OrgId + SellerOrgId/BuyerOrgId) | ✓ `TenantId` + `SellerOrgId` + `BuyerOrgId` |
| `ExternalReference` field | `Lien`, `LienOffer` | ✓ Included |
| `DateTime?` nullable timestamps for lifecycle | `LienOffer.RespondedAtUtc`, `Lien.ClosedAtUtc` | ✓ `ExecutedAtUtc`, `EffectiveAtUtc`, `CancelledAtUtc` |
| Domain methods for state transitions | `Lien.MarkSold()`, `LienOffer.Accept()` | ✓ `SubmitForExecution()`, `MarkExecuted()`, `Cancel()` |
| `Notes` / `string?` optional text fields | All entities | ✓ `Notes`, `Terms` |

---

## 3. Donor SynqLiens Concepts Consulted

### Frontend Types (`apps/web/src/types/lien.ts`)
- `BillOfSaleSummary`: `id`, `bosNumber`, `status`, `lienId`, `lienNumber`, `caseNumber`, `sellerOrg`, `buyerOrg`, `saleAmount`, `executionDate`, `createdAtUtc`
- `BillOfSaleDetail`: extends summary with `originalLienAmount`, `discountPercent`, `sellerContact`, `buyerContact`, `terms`, `notes`
- `BillOfSaleStatus`: `Draft`, `Pending`, `Executed`, `Cancelled`

### Mock Data (`apps/web/src/lib/lien-mock-data.ts`)
- BOS numbers: `BOS-2024-NNNN` format
- Status lifecycle: Draft → Pending → Executed (with Cancelled as exit)
- Execution dates tracked separately from creation
- Discount percentage calculated from original lien amount vs sale amount
- Seller/buyer contact names captured alongside org names
- Terms field captures payment instructions (e.g., "Net 30 from execution date")

### Store Behavior (`apps/web/src/stores/lien-store.ts`)
- `addBos` / `updateBos` manage bill of sale state
- BOS creation flows from the lien sale flow (not standalone)

### UI Workflow (`apps/web/src/app/(platform)/lien/bill-of-sales/[id]/page.tsx`)
- BOS Workflow steps: Draft → Pending → Executed
- Detail view shows: BOS number, linked lien, seller/buyer orgs, sale amount, original lien amount, discount %, execution date, terms, notes
- Actions: Submit (Draft→Pending), Execute (Pending→Executed), Cancel

---

## 4. Files Created/Changed

### Created
| File | Type |
|------|------|
| `apps/services/liens/Liens.Domain/Entities/BillOfSale.cs` | Entity |
| `apps/services/liens/Liens.Domain/Enums/BillOfSaleStatus.cs` | Status constants |

### Changed
None. No existing files were modified.

---

## 5. Entity List

| Entity | Namespace | Base Class |
|--------|-----------|------------|
| `BillOfSale` | `Liens.Domain.Entities` | `AuditableEntity` |
| `BillOfSaleStatus` | `Liens.Domain.Enums` | Static class (constants) |

---

## 6. Key Fields and Rationale

| Field | Type | Rationale |
|-------|------|-----------|
| `Id` | `Guid` | Standard v2 PK pattern |
| `TenantId` | `Guid` | Multi-tenant scoping (required) |
| `LienId` | `Guid` | Links to the lien being transferred |
| `LienOfferId` | `Guid` | Links to the accepted offer that triggered this BOS |
| `BillOfSaleNumber` | `string` | Business-visible identifier (`BOS-2024-NNNN`), maps to donor `bosNumber` |
| `ExternalReference` | `string?` | External system reference, consistent with `Lien`/`LienOffer` pattern |
| `Status` | `string` | `Draft`→`Pending`→`Executed` lifecycle, matches donor `BillOfSaleStatus` exactly |
| `SellerOrgId` | `Guid` | Snapshot of selling organization at time of sale |
| `BuyerOrgId` | `Guid` | Snapshot of buying organization at time of sale |
| `PurchaseAmount` | `decimal` | Agreed sale price — maps to donor `saleAmount` |
| `OriginalLienAmount` | `decimal` | Original lien value for discount calculation — maps to donor `originalLienAmount` |
| `DiscountPercent` | `decimal?` | Auto-calculated `(1 - purchaseAmount/originalLienAmount) * 100` — matches donor `discountPercent` |
| `SellerContactName` | `string?` | Seller contact snapshot — maps to donor `sellerContact` |
| `BuyerContactName` | `string?` | Buyer contact snapshot — maps to donor `buyerContact` |
| `Terms` | `string?` | Payment/transfer terms — maps to donor `terms` |
| `Notes` | `string?` | Free-text notes — maps to donor `notes` |
| `DocumentId` | `Guid?` | Future link to v2 Documents service for generated BOS PDF |
| `IssuedAtUtc` | `DateTime` | When the BOS record was created (issuance timestamp) |
| `ExecutedAtUtc` | `DateTime?` | When the BOS was formally executed (signed/completed) |
| `EffectiveAtUtc` | `DateTime?` | When the ownership transfer takes legal effect |
| `CancelledAtUtc` | `DateTime?` | When the BOS was cancelled, if applicable |

---

## 7. Multi-Tenant / Org / Seller-Buyer / Transfer Design

- **TenantId**: Required. Scopes the BOS to a platform tenant. Both seller and buyer must be within the same tenant for a marketplace transaction.
- **SellerOrgId / BuyerOrgId**: Required. Captures the selling and buying organizations at time of sale. These are snapshot IDs — they do not change after creation, even if the lien later transfers to another holder.
- **No OrgId field**: Unlike `Lien` (which has a creation-origin `OrgId`), `BillOfSale` is a bilateral record between two orgs. The `SellerOrgId`/`BuyerOrgId` pair replaces a single `OrgId`.
- **LienOfferId**: Required. Every BOS originates from an accepted offer, ensuring traceability to the negotiation.

---

## 8. Relationship Assumptions and Extensibility

| Relationship | Design | Future Notes |
|-------------|--------|--------------|
| `BillOfSale` → `Lien` | Via `LienId` (Guid FK) | Future EF mapping: `HasOne<Lien>` |
| `BillOfSale` → `LienOffer` | Via `LienOfferId` (Guid FK) | Future EF mapping: `HasOne<LienOffer>` |
| `BillOfSale` → Seller Org | Via `SellerOrgId` (cross-service reference) | Org lives in Identity service; no nav property |
| `BillOfSale` → Buyer Org | Via `BuyerOrgId` (cross-service reference) | Org lives in Identity service; no nav property |
| `BillOfSale` → Document | Via `DocumentId` (Guid?, v2 Documents service) | `AttachDocument()` method ready |
| `BillOfSale` → Audit trail | Via audit service integration | Future: application service publishes audit events |
| `BillOfSale` → Settlement/Disbursement | Via future entity linking `BillOfSaleId` | BOS does not own settlement logic |
| `BillOfSale` → E-signature | Via future workflow status extensions | `ExecutedAtUtc` timestamp ready for signature capture |

---

## 9. Intentionally Deferred

| Concern | Reason |
|---------|--------|
| EF Core configuration / DbContext | Task scope: domain modeling only |
| Repository interface | Task scope: domain modeling only |
| Database migration | Task scope: domain modeling only |
| API endpoints / DTOs | Task scope: domain modeling only |
| Document generation (PDF) | Future feature; `DocumentId` placeholder ready |
| Notifications integration | Application layer concern |
| Audit event publishing | Application layer concern |
| E-signature workflow | Future feature; `ExecutedAtUtc` timestamp ready |
| Settlement/disbursement entity | Separate domain entity (future) |
| `VoidedAtUtc` field | Consolidated into `CancelledAtUtc` — donor uses `Cancelled` not `Voided`; if void semantics differ from cancel in the future, can be added without breaking changes |

---

## 10. Build Results

| Project | Result |
|---------|--------|
| `Liens.Domain` | ✅ 0 errors, 0 warnings |
| `Liens.Api` | ✅ 0 errors, 0 warnings |
| `Gateway.Api` | ✅ 0 errors, 0 warnings |
| `Identity.Api` | ✅ 0 errors, 0 warnings |

---

## 11. Domain Purity Confirmation

✅ No EF Core attributes or `DbContext` references in Domain layer  
✅ No repository interfaces or implementations  
✅ No API DTOs, HTTP logic, or auth concerns  
✅ No Documents, Notifications, or Audit integration code  
✅ No `using` statements for infrastructure or API namespaces  

---

## 12. No v1 Code Confirmation

✅ No v1 runtime code was referenced, copied, or introduced  
✅ Donor SynqLiens was consulted only for behavior/API shape understanding  
✅ All code uses v2 patterns exclusively  

---

## 13. API & Behavior Preservation Analysis

### Donor Fields → v2 BillOfSale Mapping

| Donor Field | v2 Field | Disposition |
|-------------|----------|-------------|
| `bosNumber` | `BillOfSaleNumber` | **Preserved** — renamed to v2 naming convention; maps 1:1 |
| `status` | `Status` | **Preserved** — identical values: Draft, Pending, Executed, Cancelled |
| `lienId` | `LienId` | **Preserved** — Guid instead of string; maps 1:1 |
| `sellerOrg` (name) | `SellerOrgId` (Guid) | **Normalized** — v2 stores org ID, name resolved at API layer |
| `buyerOrg` (name) | `BuyerOrgId` (Guid) | **Normalized** — v2 stores org ID, name resolved at API layer |
| `saleAmount` | `PurchaseAmount` | **Renamed** — aligns with `Lien.PurchasePrice` naming; same meaning |
| `originalLienAmount` | `OriginalLienAmount` | **Preserved** — exact match |
| `discountPercent` | `DiscountPercent` | **Preserved** — auto-calculated in factory |
| `executionDate` | `ExecutedAtUtc` | **Preserved** — upgraded from date string to DateTime with UTC |
| `sellerContact` | `SellerContactName` | **Preserved** — renamed for clarity |
| `buyerContact` | `BuyerContactName` | **Preserved** — renamed for clarity |
| `terms` | `Terms` | **Preserved** — exact match |
| `notes` | `Notes` | **Preserved** — exact match |
| `createdAtUtc` | `CreatedAtUtc` | **Preserved** — inherited from `AuditableEntity` |

### Fields Added Beyond Donor

| Field | Justification |
|-------|--------------|
| `TenantId` | v2 multi-tenant requirement |
| `LienOfferId` | Traceability to the accepted offer |
| `ExternalReference` | Consistent with all Liens entities |
| `DocumentId` | Future v2 Documents integration |
| `IssuedAtUtc` | Distinct from `CreatedAtUtc` — represents business issuance |
| `EffectiveAtUtc` | Legal effective date may differ from execution date |
| `CancelledAtUtc` | Timestamp for cancel events |

### API Mapping Ease

Future APIs can map to the donor `BillOfSaleSummary` / `BillOfSaleDetail` contracts with minimal transformation:
- `bosNumber` ← `BillOfSaleNumber` (direct)
- `sellerOrg` ← resolve org name from `SellerOrgId` via Identity service
- `buyerOrg` ← resolve org name from `BuyerOrgId` via Identity service
- `saleAmount` ← `PurchaseAmount` (direct)
- `executionDate` ← format `ExecutedAtUtc` as date string
- All other fields map directly

---

## 14. Risks / Assumptions

| Risk/Assumption | Mitigation |
|-----------------|------------|
| BOS always originates from an accepted `LienOffer` | Factory requires `LienOfferId`; direct-purchase BOS would need a synthetic offer or a separate factory (future) |
| Seller/buyer org names not stored on entity | API layer resolves from Identity service; adds one cross-service call but keeps domain clean |
| Single `DocumentId` for the BOS document | Sufficient for MVP; if multiple documents needed, can add a separate `BillOfSaleDocument` join entity |
| `VoidedAtUtc` deferred | Donor uses `Cancelled`, not `Voided`. If void semantics differ later, field can be added without breaking |
| Discount percent calculation assumes `originalLienAmount > 0` | Null-safe: returns `null` if original is zero |

---

## 15. Readiness Statement

**Is the core BillOfSale domain model now established?**  
✅ Yes. The `BillOfSale` entity captures the full transfer record lifecycle (Draft → Pending → Executed), preserves all donor SynqLiens bill-of-sale concepts, and follows established v2 Liens domain conventions.

**Is the service ready for the next feature?**  
✅ Yes. The entity is ready for:
- EF Core configuration and migration (next step)
- Repository interface definition
- API endpoint creation with DTO mapping to donor contracts
- Integration with the `LienOffer.Accept()` → `Lien.MarkSold()` → `BillOfSale.CreateFromAcceptedOffer()` workflow
- Document generation attachment via `AttachDocument()`
- Audit trail publishing at the application service layer
