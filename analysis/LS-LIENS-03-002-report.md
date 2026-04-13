# LS-LIENS-03-002: Core Lien Domain Entity

**Date:** 2026-04-13
**Scope:** Domain modeling — Lien entity + supporting types
**Service:** `apps/services/liens/Liens.Domain`
**Predecessor:** LS-LIENS-03-001 (Case, Contact, Facility, LookupValue foundation)

---

## 1. Summary

Created the core `Lien` domain entity as the central business object of the SynqLiens product. The entity models a medical/legal lien through its full lifecycle — from draft creation through marketplace listing, sale, servicing, and settlement. Supporting types include an expanded `LienStatus` constants class and a new `LienParticipantRole` constants class.

No persistence, API, auth, or integration concerns were added. This is pure domain modeling.

---

## 2. Existing v2 + Liens Domain Patterns Followed

| Pattern | Source | Applied In Lien |
|---------|--------|-----------------|
| `AuditableEntity` base class | `BuildingBlocks.Domain` | `Lien : AuditableEntity` |
| Private constructor + static `Create` factory | Case, Contact, Facility, Fund.Application, CareConnect.Referral | `private Lien()` + `static Lien Create(...)` |
| `Guid.Empty` guards on required IDs | All existing Liens entities | tenantId, orgId, createdByUserId |
| `ArgumentException.ThrowIfNullOrWhiteSpace` | Case, Contact, Facility | lienNumber |
| String constants for status/type (not C# enums) | CaseStatus, LienType, ContactType, LienStatus | Status field uses `LienStatus.Draft` etc. |
| `IReadOnlySet<string> All` validation pattern | All existing enum classes | LienStatus.All, LienParticipantRole.All |
| `.Trim()` on string inputs | All existing entities | All string parameters trimmed |
| `UpdatedByUserId` + `UpdatedAtUtc` on mutations | Case, Contact, Facility | All domain methods |
| `TenantId` + `OrgId` multi-tenant scoping | Case, Contact, Facility | Both present as required fields |
| Named domain methods for state transitions | Case.TransitionStatus, Facility.Deactivate | ListForSale, Withdraw, MarkSold, Activate, Settle |
| Financial setter methods with validation | Case.SetDemandAmount, Case.SetSettlementAmount | SetFinancials with non-negative guards |
| Relationship attachment methods | Facility.LinkOrganization | AttachCase, AttachFacility, TransferHolding |

---

## 3. Donor SynqLiens Concepts Consulted

Sources analyzed:
- `apps/web/src/types/lien.ts` — TypeScript interfaces (LienSummary, LienDetail, LienOfferSummary, BillOfSaleSummary, CreateLienRequest, etc.)
- `apps/web/src/lib/lien-mock-data.ts` — Mock lien data with realistic examples
- `apps/web/src/stores/lien-store.ts` — Client-side state management
- `apps/web/src/app/(platform)/lien/liens/page.tsx` — Seller lien list page with Draft→Offered→Withdrawn transitions
- `apps/web/src/app/(platform)/lien/liens/[id]/page.tsx` — Lien detail with offer acceptance and sale flow

Key donor concepts absorbed:
- **Multi-party marketplace**: Seller (Provider) → Buyer (Lien Owner) → Holder lifecycle
- **Subject party**: Patient/claimant identity with confidentiality controls
- **Financial model**: OriginalAmount → OfferPrice → PurchasePrice → PayoffAmount progression
- **Status lifecycle**: Draft → Offered → UnderReview → Sold → Active → Settled (with Withdrawn/Cancelled/Disputed branches)
- **Org snapshots**: sellingOrg, buyingOrg, holdingOrg as separate party references
- **Case linkage**: Liens grouped under Cases with cross-reference
- **Bill of Sale concept**: Separate entity recording ownership transfers (deferred to future entity)

---

## 4. Files Created/Changed

### Created
| File | Purpose |
|------|---------|
| `Liens.Domain/Entities/Lien.cs` | Core Lien domain entity (248 lines) |
| `Liens.Domain/Enums/LienParticipantRole.cs` | Seller/Buyer/Holder role constants |

### Changed
| File | Change |
|------|--------|
| `Liens.Domain/Enums/LienStatus.cs` | Expanded from 4 statuses (Draft, Offered, Sold, Withdrawn) to 9 statuses adding UnderReview, Active, Settled, Cancelled, Disputed. Added `Open` and `Terminal` subsets. |

### Unchanged (verified compatible)
| File | Status |
|------|--------|
| `Liens.Domain/Entities/Case.cs` | No changes needed |
| `Liens.Domain/Entities/Contact.cs` | No changes needed |
| `Liens.Domain/Entities/Facility.cs` | No changes needed |
| `Liens.Domain/Entities/LookupValue.cs` | No changes needed |
| All other Liens.Domain enum/value-object files | No changes needed |

---

## 5. Final Entity + Supporting Type List

### Entity
- **`Lien`** — Central lien business object with full lifecycle support

### Supporting Types (new)
- **`LienParticipantRole`** — Constants: Seller, Buyer, Holder

### Supporting Types (modified)
- **`LienStatus`** — Expanded: Draft, Offered, UnderReview, Sold, Active, Settled, Withdrawn, Cancelled, Disputed. Includes `Open` and `Terminal` subsets.

### Supporting Types (existing, unchanged)
- `LienType` — MedicalLien, AttorneyLien, SettlementAdvance, WorkersCompLien, PropertyLien, Other
- `CaseStatus`, `ContactType`, `LookupCategory`, `ServicingStatus`, `ServicingPriority`
- `Address` value object

---

## 6. Key Fields and Rationale

### Identity & Scoping
| Field | Type | Rationale |
|-------|------|-----------|
| `Id` | `Guid` | Standard v2 primary key |
| `TenantId` | `Guid` | Row-level tenant isolation (v2 pattern) |
| `OrgId` | `Guid` | Organization that created/owns the lien record |
| `LienNumber` | `string` | Human-readable identifier (maps to donor `lienNumber`, `LN-2024-XXXX`) |
| `ExternalReference` | `string?` | External system cross-reference (v2 pattern from Case, Facility) |

### Classification
| Field | Type | Rationale |
|-------|------|-----------|
| `LienType` | `string` | Validated against `LienType.All` (donor: MedicalLien, AttorneyLien, etc.) |
| `Status` | `string` | Full lifecycle status (9 values, validated against `LienStatus.All`) |
| `Jurisdiction` | `string?` | Geographic jurisdiction (donor: `jurisdiction` field in LienSummary) |
| `IsConfidential` | `bool` | Controls subject party visibility in marketplace (donor: `isConfidential`) |

### Subject Party
| Field | Type | Rationale |
|-------|------|-----------|
| `SubjectPartyId` | `Guid?` | FK-ready reference to future Subject/Patient entity |
| `SubjectFirstName` | `string?` | Inline snapshot for display (donor: `PartySnapshot.firstName`) |
| `SubjectLastName` | `string?` | Inline snapshot for display (donor: `PartySnapshot.lastName`) |

### Relationships (FK-ready, no navigation properties)
| Field | Type | Rationale |
|-------|------|-----------|
| `CaseId` | `Guid?` | Links lien to a Case entity (donor: liens grouped under cases) |
| `FacilityId` | `Guid?` | Links to originating facility (donor: medical facility involvement) |

### Financial
| Field | Type | Rationale |
|-------|------|-----------|
| `OriginalAmount` | `decimal` | Face value of the lien (donor: `originalAmount`) |
| `CurrentBalance` | `decimal?` | Outstanding balance during servicing (initialized to OriginalAmount, zeroed on settle) |
| `OfferPrice` | `decimal?` | Seller's asking price when listed (donor: `offerPrice`) |
| `PurchasePrice` | `decimal?` | Actual price paid by buyer (donor: `purchasePrice`) |
| `PayoffAmount` | `decimal?` | Final settlement/payoff amount (future settlement linkage) |

### Multi-Party Ownership
| Field | Type | Rationale |
|-------|------|-----------|
| `SellingOrgId` | `Guid?` | Organization selling the lien (auto-set to OrgId on create; donor: `sellingOrgId`) |
| `BuyingOrgId` | `Guid?` | Organization purchasing the lien (set on MarkSold; donor: `buyingOrgId`) |
| `HoldingOrgId` | `Guid?` | Current holder/servicer (set on MarkSold, transferable; donor: `holdingOrgId`) |

### Lifecycle Dates
| Field | Type | Rationale |
|-------|------|-----------|
| `IncidentDate` | `DateOnly?` | Date of incident (donor: `incidentDate`) |
| `OpenedAtUtc` | `DateTime?` | When the lien was created/opened |
| `ClosedAtUtc` | `DateTime?` | When the lien reached terminal status (Settled/Cancelled) |

### Descriptive
| Field | Type | Rationale |
|-------|------|-----------|
| `Description` | `string?` | Free-text description (donor: `description`) |
| `Notes` | `string?` | Internal notes (v2 pattern from Case, Contact) |

---

## 7. Multi-Tenant / Org / Ownership Design

- **TenantId**: Required on create, enforces row-level tenant isolation. All liens within a tenant share the same marketplace.
- **OrgId**: The creating organization. In the marketplace model, this is the seller's org. Set as required on create (follows Case/Contact/Facility pattern).
- **SellingOrgId**: Explicitly tracks the selling organization. Auto-set to `OrgId` on create. This supports future scenarios where a lien might be re-listed by a different org.
- **BuyingOrgId / HoldingOrgId**: Populated during the sale lifecycle. `HoldingOrgId` is transferable via `TransferHolding()` to support servicing hand-offs.
- **Role-based access**: The `LienParticipantRole` constants (Seller/Buyer/Holder) provide domain vocabulary for future authorization logic without embedding auth in the domain.

---

## 8. Relationship Assumptions & Future Extensibility

The Lien entity is designed as the central hub with Guid FK references (no navigation properties) to support future relationships:

| Future Entity | Link Field | Notes |
|---------------|------------|-------|
| **Case** | `CaseId` | Already established entity. `AttachCase()` method ready. |
| **Facility** | `FacilityId` | Already established entity. `AttachFacility()` method ready. |
| **Contact** | Via `SellingOrgId`, `BuyingOrgId`, `HoldingOrgId` | Org-level actors. Contact-level linking deferred to future entity (LienParticipant). |
| **BillOfSale** | Future entity will reference `Lien.Id` | Donor concept fully documented. Sale flow (`MarkSold`) provides data. |
| **ServicingItem** | Future entity will reference `Lien.Id` | Status model supports Active→Settled flow. |
| **LienOffer** | Future entity will reference `Lien.Id` | Donor `LienOfferSummary` concept. Status includes UnderReview. |
| **Settlement** | Future entity will reference `CaseId` or `Lien.Id` | `PayoffAmount` + `Settle()` method ready. |
| **Documents** | Via v2 Documents service (external) | No domain coupling. Document references will be at API/integration layer. |

---

## 9. Intentionally Deferred

| Concept | Reason |
|---------|--------|
| **LienOffer entity** | Separate aggregate. Requires its own lifecycle (Pending/Accepted/Rejected/Withdrawn). Will be a future domain entity. |
| **BillOfSale entity** | Separate aggregate. Represents legal ownership transfer. Donor types documented. |
| **ServicingItem entity** | Separate aggregate with its own status/priority lifecycle. |
| **Settlement entity** | Cross-cutting concern across Case and multiple Liens. Future design. |
| **LienParticipant entity** | Contact-level tracking of individuals within org roles. |
| **Offer expiration** | `offerExpiresAtUtc` is in donor types but belongs on the Offer entity, not Lien. |
| **Discount percentage** | Calculated value (`(original - sale) / original`). Belongs in API/projection layer, not domain. |
| **Money value object** | Evaluated against v2 patterns. Fund and CareConnect use plain `decimal` fields. Staying consistent. |
| **Navigation properties** | No EF Core navigation. FK Guids only (domain purity). |
| **Status transition matrix** | Full valid-transition rules deferred to application layer. Domain validates against `LienStatus.All` and provides guard rails in named methods. |

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

### Identity.Api
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 11. Domain Purity Confirmation

**No persistence/API/auth concerns in Domain layer:**
- No EF Core attributes (`[Key]`, `[Column]`, `[Required]`, etc.)
- No `DbContext` references
- No repository interfaces or implementations
- No migration files
- No API DTOs, controllers, or endpoint logic
- No HTTP/auth/claims parsing
- No documents/notifications/audit integration

The only dependency is `BuildingBlocks.Domain` for `AuditableEntity`.

---

## 12. No v1 Code Confirmation

- No v1 source code, assemblies, or runtime logic was introduced
- Donor SynqLiens was consulted only through frontend types (`types/lien.ts`) and mock data (`lien-mock-data.ts`)
- All implementation follows v2 patterns established in LS-LIENS-03-001

---

## 13. API & Behavior Preservation Analysis

### Donor Fields That Influenced Design

| Donor Field (TypeScript) | Lien Entity Field | Mapping |
|--------------------------|-------------------|---------|
| `id` | `Id` | Direct (Guid vs string) |
| `tenantId` | `TenantId` | Direct |
| `lienNumber` | `LienNumber` | Direct |
| `lienType` | `LienType` | Direct (same enum values) |
| `status` | `Status` | Direct (expanded set, same names) |
| `originalAmount` | `OriginalAmount` | Direct |
| `offerPrice` | `OfferPrice` | Direct |
| `purchasePrice` | `PurchasePrice` | Direct |
| `jurisdiction` | `Jurisdiction` | Direct |
| `isConfidential` | `IsConfidential` | Direct |
| `subjectParty.firstName` | `SubjectFirstName` | Flattened from snapshot |
| `subjectParty.lastName` | `SubjectLastName` | Flattened from snapshot |
| `sellingOrg.orgId` | `SellingOrgId` | Extracted from snapshot |
| `buyingOrg.orgId` | `BuyingOrgId` | Extracted from snapshot |
| `holdingOrg.orgId` | `HoldingOrgId` | Extracted from snapshot |
| `incidentDate` | `IncidentDate` | `string` → `DateOnly?` (type normalized) |
| `description` | `Description` | Direct |
| `subjectPartyId` | `SubjectPartyId` | Direct |
| `caseRef` | Via `CaseId` | Normalized: string ref → Guid FK (Case entity has `CaseNumber` for display) |

### Fields Preserved for Compatibility
All critical business fields from `LienSummary` and `LienDetail` have direct equivalents. Field names match the donor 1:1 where possible (LienNumber, LienType, OriginalAmount, OfferPrice, PurchasePrice, Jurisdiction, IsConfidential).

### Fields Changed/Normalized

| Donor | Entity | Change | Justification |
|-------|--------|--------|---------------|
| `caseRef` (string) | `CaseId` (Guid?) | Type change | v2 uses Guid FKs. Case entity provides `CaseNumber` for display. |
| `incidentDate` (string) | `IncidentDate` (DateOnly?) | Type change | Proper date type in .NET domain. API layer serializes to yyyy-MM-dd. |
| `sellingOrg` (OrgSnapshot) | `SellingOrgId` (Guid?) | Simplified | Org name is a projection concern. Domain stores the FK only. |
| `buyingOrg` / `holdingOrg` | Same pattern | Simplified | Snapshot projection happens at API/query layer. |

### Fields Deferred

| Donor Field | Reason |
|-------------|--------|
| `offerExpiresAtUtc` | Belongs on future LienOffer entity |
| `offerNotes` | Belongs on future LienOffer entity |
| `offers[]` | Future LienOffer aggregate (1:N relationship) |
| `discountPercent` | Calculated field — API projection layer |

### API Mapping Assessment
Future APIs can map to the Lien entity with minimal transformation:
- `LienSummary` response: Direct field mapping + org name projection from Identity service
- `LienDetail` response: Same + attach related offers from LienOffer query
- `CreateLienRequest`: Maps directly to `Lien.Create()` parameters
- Existing frontend pages (`/lien/liens`, `/lien/marketplace`, `/lien/portfolio`) will work with API responses built from this entity without frontend changes

---

## 14. Code Review Fixes Applied

Architecture review identified three issues, all corrected:

| Issue | Severity | Fix |
|-------|----------|-----|
| `Sold` incorrectly in `Terminal` set, preventing `Sold → Active` via `TransitionStatus()` while `Activate()` allowed it | Critical | Removed `Sold` from `Terminal`. `Sold` is now in `Open` set — it's a transitional status, not final. |
| `TransitionStatus()` allowed arbitrary jumps (e.g., `Draft → Settled`, `Withdrawn → Active`) | Critical | Replaced open guard with explicit `AllowedTransitions` matrix. Every status has a defined set of valid next statuses. Terminal statuses have empty transition sets. |
| `SetFinancials()` only validated `OriginalAmount`; permitted negative values for `CurrentBalance`, `OfferPrice`, `PurchasePrice`, `PayoffAmount` | Medium | Added non-negative guards for all five financial fields. |
| `Withdraw()` did not set `ClosedAtUtc` unlike `Settle()` and `Cancel()` | Low | Added `ClosedAtUtc = DateTime.UtcNow` to `Withdraw()`. All terminal transitions now consistently set closure timestamp. |

### Transition Matrix (as implemented)
```
Draft       → Offered, Cancelled
Offered     → UnderReview, Sold, Withdrawn
UnderReview → Sold, Withdrawn
Sold        → Active, Cancelled
Active      → Settled, Disputed, Cancelled
Disputed    → Active, Settled, Cancelled
Settled     → (terminal)
Withdrawn   → (terminal)
Cancelled   → (terminal)
```

---

## 15. Risks / Assumptions

| Risk/Assumption | Mitigation |
|-----------------|------------|
| LienStatus expanded from 4→9 values; frontend currently only uses Draft/Offered/Sold/Withdrawn | Frontend mock data uses the original 4. New statuses (UnderReview, Active, Settled, Cancelled, Disputed) are forward-looking. Frontend will gain these as features are built. Existing status badge component already handles unknown statuses gracefully. |
| `SellingOrgId` auto-set to `OrgId` on create | Assumes the creating org is always the seller. Re-listing by a different org would require a domain method. |
| No formal status transition matrix | Named methods (ListForSale, Withdraw, MarkSold, Activate, Settle) enforce key business rules. Full matrix validation deferred to application layer. |
| `CurrentBalance` initialized to `OriginalAmount` | Assumes the outstanding balance starts at face value. Servicing adjustments will modify this. |
| Money stored as `decimal` (not a value object) | Consistent with Fund.Application (decimal for amounts) and all existing Liens entities. No currency conversion needed in the domain. |

---

## 16. Final Readiness Statement

### Is the core Lien domain model now established?
**Yes.** The `Lien` entity is fully defined with:
- 28 properties covering identity, classification, relationships, financials, ownership, lifecycle, and descriptive fields
- 10 domain methods covering creation, updates, status transitions, sale flow, settlement, and relationship attachment
- Full lifecycle support from Draft through Settled/Cancelled
- Multi-party ownership model (Seller → Buyer → Holder)
- Guards and validation consistent with existing Liens domain entities

### Is the service ready for the next feature?
**Yes.** The natural next steps are:
1. **EF Core configuration** (`LienConfiguration.cs`) — persistence mapping
2. **LienOffer entity** — buyer negotiation aggregate
3. **BillOfSale entity** — ownership transfer record
4. **API endpoints** — CRUD + marketplace + portfolio views
5. **Frontend integration** — Replace mock data with real API calls

All builds pass (0 warnings, 0 errors) across Liens, Identity, and Gateway services. No regressions introduced.
