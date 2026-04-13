# LS-LIENS-03-003: LienOffer Domain Entity

**Date:** 2026-04-13
**Scope:** Domain modeling — LienOffer entity + supporting types
**Service:** `apps/services/liens/Liens.Domain`
**Predecessor:** LS-LIENS-03-002 (Core Lien entity)

---

## 1. Summary

Created the `LienOffer` domain entity to model marketplace buyer offers against liens. This is the negotiation mechanism in the SynqLiens multi-party marketplace where buyers (lien owners) submit offers on liens listed by sellers (providers). The entity supports the full offer lifecycle: create, update, accept, reject, withdraw, and expire.

Supporting type `OfferStatus` provides the status constants with an explicit transition matrix.

No persistence, API, auth, or integration concerns were added. This is pure domain modeling.

---

## 2. Existing v2 + Liens Domain Patterns Followed

| Pattern | Source | Applied In LienOffer |
|---------|--------|----------------------|
| `AuditableEntity` base class | `BuildingBlocks.Domain` | `LienOffer : AuditableEntity` |
| Private constructor + static `Create` factory | Lien, Case, Contact, Facility | `private LienOffer()` + `static LienOffer Create(...)` |
| `Guid.Empty` guards on required IDs | All Liens entities | tenantId, lienId, buyerOrgId, sellerOrgId, createdByUserId |
| String constants for status (not C# enums) | LienStatus, CaseStatus, ContactType | `OfferStatus.Pending` etc. |
| `IReadOnlySet<string> All` validation | All existing enum classes | `OfferStatus.All`, `OfferStatus.Terminal` |
| `AllowedTransitions` matrix | LienStatus (added in LS-LIENS-03-002) | `OfferStatus.AllowedTransitions` |
| `.Trim()` on string inputs | All existing entities | All string parameters trimmed |
| `UpdatedByUserId` + `UpdatedAtUtc` on mutations | All existing entities | All domain methods |
| Named domain methods for state transitions | Lien.ListForSale, Lien.MarkSold, etc. | Accept, Reject, Withdraw, Expire |
| Private guard helper | (new pattern) | `EnsurePendingAndNotExpired()` — centralizes status + expiry check |

---

## 3. Donor SynqLiens Offer Concepts Consulted

Sources analyzed:
- `apps/web/src/types/lien.ts` — `LienOfferSummary`, `SubmitLienOfferRequest` interfaces
- `apps/web/src/lib/lien-mock-data.ts` — Mock offers (off001, off002, off003) with realistic buyer/amount/status data
- `apps/web/src/stores/lien-store.ts` — `addOffer`, `updateOffer` store methods
- `apps/web/src/app/(platform)/lien/liens/[id]/page.tsx` — Accept/Reject confirmation flow; accept triggers `updateOffer` + `MarkSold` on the lien

Key donor concepts absorbed:
- **Offer-per-buyer**: Multiple offers can exist on a single lien from different buyer orgs
- **Status lifecycle**: Pending → Accepted / Rejected / Withdrawn (donor uses 4 statuses; v2 adds Expired)
- **Accept triggers sale**: When an offer is accepted, the lien transitions to Sold and buyer becomes holder
- **Buyer identity**: `buyerOrgId` + `buyerOrgName` (name is a projection concern)
- **Offer amount**: Single amount field (no counteroffer chains in donor model)
- **Notes**: Buyer can attach notes to their offer
- **Timestamps**: `createdAtUtc`, `updatedAtUtc` tracked (v2 adds richer lifecycle timestamps)

---

## 4. Files Created/Changed

### Created
| File | Purpose |
|------|---------|
| `Liens.Domain/Entities/LienOffer.cs` | LienOffer domain entity (150 lines) |
| `Liens.Domain/Enums/OfferStatus.cs` | Offer status constants with transition matrix |

### Unchanged (verified compatible)
| File | Status |
|------|--------|
| `Liens.Domain/Entities/Lien.cs` | No changes needed — LienOffer references Lien via `LienId` FK |
| All other Liens.Domain files | No changes needed |

---

## 5. Final Entity + Supporting Type List

### Entity
- **`LienOffer`** — Marketplace buyer offer against a lien

### Supporting Types (new)
- **`OfferStatus`** — Constants: Pending, Accepted, Rejected, Withdrawn, Expired. Includes `Terminal` subset and `AllowedTransitions` matrix.

---

## 6. Key Fields and Rationale

### Identity & Scoping
| Field | Type | Rationale |
|-------|------|-----------|
| `Id` | `Guid` | Standard v2 primary key |
| `TenantId` | `Guid` | Row-level tenant isolation |
| `LienId` | `Guid` | FK to the Lien being offered on (donor: `lienId`) |

### Parties
| Field | Type | Rationale |
|-------|------|-----------|
| `BuyerOrgId` | `Guid` | Organization submitting the offer (donor: `buyerOrgId`) |
| `SellerOrgId` | `Guid` | Snapshot of selling org at offer time. Ensures historical integrity if lien ownership changes. Validated against `Lien.SellingOrgId` at creation in service layer. |

### Financial
| Field | Type | Rationale |
|-------|------|-----------|
| `OfferAmount` | `decimal` | The buyer's bid amount (donor: `offerAmount`). Positive-only guard. |

### Status
| Field | Type | Rationale |
|-------|------|-----------|
| `Status` | `string` | Validated against `OfferStatus.All`. Default: Pending. |

### Communication
| Field | Type | Rationale |
|-------|------|-----------|
| `Notes` | `string?` | Buyer's message with the offer (donor: `notes`) |
| `ResponseNotes` | `string?` | Seller's response message on accept/reject. Separated from buyer notes for clean domain semantics. |
| `ExternalReference` | `string?` | External system cross-reference (v2 pattern from Lien, Case, Facility) |

### Lifecycle Timestamps
| Field | Type | Rationale |
|-------|------|-----------|
| `OfferedAtUtc` | `DateTime` | When the offer was submitted (set on create) |
| `ExpiresAtUtc` | `DateTime?` | Optional expiration. Enforced at domain level — expired offers cannot be accepted/rejected/withdrawn. |
| `RespondedAtUtc` | `DateTime?` | When the seller accepted or rejected |
| `WithdrawnAtUtc` | `DateTime?` | When the buyer withdrew the offer |

### Computed Property
| Field | Type | Rationale |
|-------|------|-----------|
| `IsExpired` | `bool` (computed) | True when `Status == Expired` OR `(Status == Pending && ExpiresAtUtc <= now)`. Covers both explicit and clock-based expiry. |

---

## 7. Multi-Tenant / Org / Buyer-Seller Design

- **TenantId**: Required on create. Ensures offers are scoped within the same tenant marketplace.
- **BuyerOrgId**: The organization submitting the offer. Required, validated non-empty.
- **SellerOrgId**: Snapshot of the selling org at offer creation time. Required, validated non-empty. This is denormalized from `Lien.SellingOrgId` for:
  - Query efficiency (list offers by seller without joining Lien)
  - Historical integrity (if lien selling org changes, offer record preserves original seller)
  - Cross-aggregate boundary (LienOffer should not depend on Lien navigation property)
- **Invariant**: Service layer should validate `SellerOrgId == Lien.SellingOrgId` at creation time (cross-aggregate validation belongs in application/domain service, not entity).

---

## 8. Relationship Assumptions & Future Extensibility

| Future Concept | Design Support | Notes |
|----------------|----------------|-------|
| **Lien** | `LienId` FK | Direct reference. Accept triggers `Lien.MarkSold()` in service layer. |
| **BillOfSale** | Future entity references accepted `LienOffer.Id` | Sale finalization uses accepted offer's amount and buyer. |
| **Audit trail** | v2 Audit service (external) | No domain coupling. Audit hooks at API/service layer. |
| **Notifications** | v2 Notifications service (external) | Offer create/accept/reject/withdraw triggers notifications at service layer. |
| **Counteroffers** | Deferred | Donor model uses single offers, not counteroffer chains. If needed, can add `ParentOfferId` FK later. |
| **Offer revisions** | Partially supported | `UpdatePending()` allows amount/notes/expiry changes before response. |

---

## 9. Intentionally Deferred

| Concept | Reason |
|---------|--------|
| **Counteroffer chains** | Donor model uses independent offers, not linked counter-chains. Can add `ParentOfferId` later if needed. |
| **Auto-expiration background job** | `Expire()` method is ready, but scheduling belongs in application/infrastructure layer. |
| **Offer-level documents** | Document attachment is a v2 Documents service concern, not domain. |
| **Offer ranking/selection** | Business logic for choosing among multiple offers belongs in application service. |
| **Buyer org name snapshot** | Donor stores `buyerOrgName` on the offer. In v2, org names are projected from Identity service at API layer, not stored on domain entity. |
| **Navigation properties** | No EF Core navigation. FK Guids only (domain purity). |

---

## 10. Build Results

### Liens.Domain
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Liens.Api (full stack: Domain → Application → Infrastructure → Api)
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Gateway.Api
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 11. Domain Purity Confirmation

**No persistence/API/auth concerns in Domain layer:**
- No EF Core attributes
- No DbContext references
- No repository interfaces or implementations
- No migration files
- No API DTOs, controllers, or endpoint logic
- No HTTP/auth/claims parsing
- No documents/notifications/audit integration

The only dependency is `BuildingBlocks.Domain` for `AuditableEntity`.

---

## 12. No v1 Code Confirmation

- No v1 source code, assemblies, or runtime logic was introduced
- Donor SynqLiens was consulted only through frontend types and mock data
- All implementation follows v2 patterns established in LS-LIENS-03-001 and LS-LIENS-03-002

---

## 13. API & Behavior Preservation Analysis

### Donor Fields That Influenced Design

| Donor Field (TypeScript) | LienOffer Field | Mapping |
|--------------------------|-----------------|---------|
| `id` | `Id` | Direct (Guid vs string) |
| `lienId` | `LienId` | Direct |
| `buyerOrgId` | `BuyerOrgId` | Direct |
| `buyerOrgName` | N/A (projected) | Deferred to API projection layer — org names come from Identity service |
| `offerAmount` | `OfferAmount` | Direct |
| `notes` | `Notes` | Direct |
| `status` | `Status` | Direct (same values + Expired added) |
| `createdAtUtc` | `CreatedAtUtc` + `OfferedAtUtc` | Split: audit timestamp + business timestamp |
| `updatedAtUtc` | `UpdatedAtUtc` | Direct (via AuditableEntity) |

### Fields Preserved for Compatibility
All critical business fields from `LienOfferSummary` have direct equivalents: `id`, `lienId`, `buyerOrgId`, `offerAmount`, `notes`, `status`, `createdAtUtc`, `updatedAtUtc`.

### Fields Changed/Normalized

| Donor | Entity | Change | Justification |
|-------|--------|--------|---------------|
| `buyerOrgName` (string) | Not stored | Projection concern | Org names are resolved from Identity service at API layer. Storing denormalized names violates v2 pattern. |
| No `sellerOrgId` in donor | `SellerOrgId` added | Snapshot | Enables efficient querying and historical integrity without cross-aggregate navigation. |
| No expiration in donor summary | `ExpiresAtUtc` added | Forward-looking | Donor `LienDetail.offerExpiresAtUtc` exists at lien level. v2 moves expiration to the offer level for per-offer control. |
| No response notes in donor | `ResponseNotes` added | Clean separation | Buyer notes vs seller response notes. Donor had single `notes` field. |

### Fields Deferred

| Donor Field | Reason |
|-------------|--------|
| `buyerOrgName` | API projection from Identity service |

### API Mapping Assessment
Future APIs can map to the LienOffer entity with minimal transformation:
- `LienOfferSummary` response: Direct field mapping + `buyerOrgName` projected from Identity
- `SubmitLienOfferRequest`: Maps to `LienOffer.Create()` parameters (service resolves sellerOrgId from Lien)
- Accept/Reject actions: Map directly to `Accept()`/`Reject()` domain methods
- Existing frontend offer panel components will work with API responses built from this entity

---

## 14. Code Review Fixes Applied

Architecture review identified three issues, all corrected:

| Issue | Severity | Fix |
|-------|----------|-----|
| Accept/Reject/Withdraw/UpdatePending did not check clock-based expiration | Critical | Added `EnsurePendingAndNotExpired()` private guard method. All four transition methods now reject time-expired offers. |
| `IsExpired` only returned true for pending+time-expired, not for explicitly expired offers | Medium | Changed to `Status == Expired \|\| (Pending && ExpiresAtUtc <= now)`. Covers both computed and explicit expired states. |
| `Expire()` had no `updatedByUserId` for audit trail | Low | Added optional `Guid? expiredByUserId` parameter. Supports both system-triggered (null) and user-triggered expiration. |

---

## 15. Risks / Assumptions

| Risk/Assumption | Mitigation |
|-----------------|------------|
| `OfferStatus` adds `Expired` beyond donor's 4 values | Frontend uses string status fields. New status will be handled gracefully by status badge components. |
| `SellerOrgId` is a snapshot, not live-resolved | Documented as invariant. Service layer validates match at creation time. |
| No counteroffer/revision chain | Donor model uses independent offers. `UpdatePending()` supports simple revisions before response. |
| `Expire()` relies on service/job to call it | `EnsurePendingAndNotExpired()` provides real-time guard even if background job hasn't run yet. |
| Multiple offers per lien — no auto-reject on accept | Service layer responsibility: when one offer is accepted, reject all other pending offers and mark lien as Sold. |

---

## 16. Final Readiness Statement

### Is the core LienOffer domain model now established?
**Yes.** The `LienOffer` entity is fully defined with:
- 15 properties covering identity, parties, financials, status, communication, and lifecycle timestamps
- 6 domain methods: Create, UpdatePending, Accept, Reject, Withdraw, Expire
- Clock-based expiration guard preventing actions on expired offers
- Computed `IsExpired` property for query convenience
- Explicit status transition matrix in `OfferStatus`

### Is the service ready for the next feature?
**Yes.** The natural next steps are:
1. **EF Core configurations** for Lien and LienOffer persistence
2. **BillOfSale entity** — ownership transfer record (linked to accepted offer)
3. **Application service** — orchestrates accept-offer → mark-sold → reject-others → create-bill-of-sale flow
4. **API endpoints** — offer CRUD + marketplace actions
5. **Frontend integration** — wire offer panels to real APIs

All builds pass (0 warnings, 0 errors) across Liens and Gateway services. No regressions introduced.
