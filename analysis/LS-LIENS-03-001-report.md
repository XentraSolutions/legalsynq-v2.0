# LS-LIENS-03-001 — Liens Domain Entity Foundation Report

**Date:** 2026-04-13
**Epic:** Liens Microservice — Domain Foundation
**Feature:** Core Domain Entity Definitions
**Status:** ✅ Complete — Build verified

---

## 1. Summary

Defined the foundational domain entities for the `Liens` microservice: **Case**, **Contact**, **Facility**, and **LookupValue**, along with supporting enums, value objects, and constants. These entities follow v2 domain conventions established by the Fund and CareConnect services, while preserving behavior and API shape compatibility with the v1 SynqLiens frontend types.

The domain layer remains pure — no EF attributes, DbContext, repositories, migrations, API models, HTTP logic, auth logic, or service integrations were added.

---

## 2. v2 Domain Patterns Followed

| Pattern | Implementation | Reference |
|---------|---------------|-----------|
| **Auditable base** | All entities inherit `AuditableEntity` from `BuildingBlocks.Domain` | `AuditableEntity.cs` |
| **Private constructors** | All entities have `private` parameterless constructors (EF Core compat) | Fund `Application.cs`, CareConnect `Referral.cs` |
| **Static factory methods** | `Create()` with validation guards (`ArgumentException.ThrowIfNullOrWhiteSpace`) | Fund `Application.Create()` |
| **Private setters** | All properties use `{ get; private set; }` | All v2 entities |
| **Domain update methods** | `Update()`, state transitions, `Deactivate()`/`Reactivate()` | Fund `Application.Update()`, `Submit()` |
| **String constants for statuses** | Static classes with `const string` fields + `IReadOnlySet<string> All` | CareConnect `AppointmentStatus.cs` |
| **Multi-tenant scoping** | `TenantId` on all entities, `OrgId` on org-scoped entities | CareConnect `Referral.cs`, `Party.cs` |
| **Soft FK to Identity** | `Facility.OrganizationId` for cross-service linking | CareConnect `Facility.OrganizationId` |
| **Value objects** | `Address` as `sealed record` with factory method | CareConnect inline address pattern elevated |

---

## 3. v1 Donor Concepts Used (Behavior Only)

The v1 SynqLiens domain is defined in frontend TypeScript types (`apps/web/src/types/lien.ts`) and mock data (`apps/web/src/lib/lien-mock-data.ts`). No backend C# entities existed. The following v1 concepts informed the domain design:

| v1 Concept | v2 Entity | How Used |
|------------|-----------|----------|
| `CaseSummary` / `CaseDetail` | `Case` | Case structure, status lifecycle, insurance fields, demand/settlement amounts |
| `ContactSummary` / `ContactDetail` | `Contact` | Contact types, name/org/address fields, website/fax |
| `CaseStatus` enum | `CaseStatus` constants | Preserved exact status codes: PreDemand → DemandSent → InNegotiation → CaseSettled → Closed |
| `ContactType` enum | `ContactType` constants | Preserved: LawFirm, Provider, LienHolder, CaseManager, InternalUser |
| `LienType` enum | `LienType` constants | Preserved: MedicalLien, AttorneyLien, SettlementAdvance, WorkersCompLien, PropertyLien, Other |
| `LienStatus` enum | `LienStatus` constants | Preserved: Draft, Offered, Sold, Withdrawn |
| `ServicingStatus` / `ServicingPriority` | Constants | Preserved for future ServicingItem entity |
| `medicalFacility` in CaseDetail | `Facility` | Elevated to first-class entity with v2 address/code/org-link |

---

## 4. Files Created

| File | Type | Description |
|------|------|-------------|
| `Liens.Domain/Entities/Case.cs` | Entity | Core case entity with client, insurance, demand/settlement fields |
| `Liens.Domain/Entities/Contact.cs` | Entity | Contact with type, name, org, address, active flag |
| `Liens.Domain/Entities/Facility.cs` | Entity | Medical/legal facility with address, code, Identity org link |
| `Liens.Domain/Entities/LookupValue.cs` | Entity | Tenant-scoped or global lookup values by category |
| `Liens.Domain/Enums/CaseStatus.cs` | Constants | PreDemand, DemandSent, InNegotiation, CaseSettled, Closed |
| `Liens.Domain/Enums/ContactType.cs` | Constants | LawFirm, Provider, LienHolder, CaseManager, InternalUser |
| `Liens.Domain/Enums/LienType.cs` | Constants | MedicalLien, AttorneyLien, SettlementAdvance, etc. |
| `Liens.Domain/Enums/LienStatus.cs` | Constants | Draft, Offered, Sold, Withdrawn |
| `Liens.Domain/Enums/ServicingStatus.cs` | Constants | Pending, InProgress, Completed, Escalated, OnHold |
| `Liens.Domain/Enums/ServicingPriority.cs` | Constants | Low, Normal, High, Urgent |
| `Liens.Domain/Enums/LookupCategory.cs` | Constants | Valid categories for LookupValue |
| `Liens.Domain/ValueObjects/Address.cs` | Value Object | Reusable address with factory + validation |

**Files removed:** `Liens.Domain/Entities/.gitkeep` (empty placeholder)

---

## 5. Entity Details

### Case

| Property | Type | Purpose |
|----------|------|---------|
| `Id` | `Guid` | Primary key |
| `TenantId` | `Guid` | Tenant isolation |
| `OrgId` | `Guid` | Owner organization |
| `CaseNumber` | `string` | Unique business key (v1: `caseNumber`) |
| `ExternalReference` | `string?` | External system reference for API compatibility |
| `Title` | `string?` | Optional display name |
| `ClientFirstName/LastName` | `string` | Inline client fields (v1: `clientName` split) |
| `ClientDob/Phone/Email` | various | v1: `clientDob`, `clientPhone`, `clientEmail` |
| `Status` | `string` | Lookup-driven, defaults to `PreDemand` |
| `DateOfIncident` | `DateOnly?` | v1: `dateOfIncident` |
| `OpenedAtUtc/ClosedAtUtc` | `DateTime?` | Lifecycle timestamps |
| `InsuranceCarrier/PolicyNumber/ClaimNumber` | `string?` | v1 insurance fields preserved |
| `DemandAmount/SettlementAmount` | `decimal?` | v1: `demandAmount`, `settlementAmount` |
| `Description/Notes` | `string?` | v1: `description`, `notes` |

**Methods:** `Create()`, `Update()`, `TransitionStatus()`, `SetDemandAmount()`, `SetSettlementAmount()`

### Contact

| Property | Type | Purpose |
|----------|------|---------|
| `Id` | `Guid` | Primary key |
| `TenantId/OrgId` | `Guid` | Tenant + org isolation |
| `ContactType` | `string` | v1: LawFirm, Provider, LienHolder, CaseManager, InternalUser |
| `FirstName/LastName/DisplayName` | `string` | v1 `name` split into structured fields |
| `Title/Organization` | `string?` | v1: `title`, `organization` |
| `Email/Phone/Fax/Website` | `string?` | v1 preserved |
| `AddressLine1/City/State/PostalCode` | `string?` | v1: `address`, `city`, `state`, `zipCode` |
| `Notes` | `string?` | v1: `notes` |
| `IsActive` | `bool` | Active flag |

**Methods:** `Create()`, `Update()`, `Deactivate()`, `Reactivate()`

### Facility

| Property | Type | Purpose |
|----------|------|---------|
| `Id` | `Guid` | Primary key |
| `TenantId/OrgId` | `Guid` | Tenant + org isolation |
| `Name` | `string` | Facility name |
| `Code` | `string?` | Short code for lookups |
| `ExternalReference` | `string?` | API compat |
| `AddressLine1/Line2/City/State/PostalCode` | `string?` | Inline address (matches CareConnect Facility pattern) |
| `Phone/Email/Fax` | `string?` | Contact info |
| `IsActive` | `bool` | Active flag |
| `OrganizationId` | `Guid?` | Soft FK to Identity Organization |

**Methods:** `Create()`, `Update()`, `LinkOrganization()`, `Deactivate()`, `Reactivate()`

### LookupValue

| Property | Type | Purpose |
|----------|------|---------|
| `Id` | `Guid` | Primary key |
| `TenantId` | `Guid?` | Null = global, set = tenant-specific |
| `Category` | `string` | Validated against `LookupCategory.All` |
| `Code` | `string` | Machine-readable code |
| `Name` | `string` | Human-readable label |
| `Description` | `string?` | Optional description |
| `SortOrder` | `int` | Display ordering |
| `IsActive` | `bool` | Active flag |
| `IsSystem` | `bool` | System lookups cannot be deactivated |

**Methods:** `Create()`, `Update()`, `Deactivate()` (guards against system values), `Reactivate()`

---

## 6. Key Fields and Reasoning

| Field | Reasoning |
|-------|-----------|
| `CaseNumber` | Business key matching v1 `caseNumber` — drives all case lookups and API routes |
| `ExternalReference` | Preserves v1 import/sync scenarios where external system IDs must be tracked |
| `ClientFirstName/LastName` | Split from v1 `clientName` for structured queries while keeping API-mappable |
| `Status` (string) | String constants (not C# enum) for serialization simplicity — matches CareConnect/Fund pattern |
| `OrgId` | Replaces v1 `programId` — every entity is org-scoped per v2 multi-tenant design |
| `OrganizationId` on Facility | Soft FK to Identity service — matches CareConnect `Facility.OrganizationId` pattern |
| `DisplayName` on Contact | Computed from First+Last at creation — maps cleanly to v1 `name` field |
| `IsSystem` on LookupValue | Prevents deletion of platform-defined lookup values while allowing tenant customization |

---

## 7. Multi-Tenant / Org Design Decisions

- **TenantId** is present on all four entities, ensuring tenant isolation at the data layer
- **OrgId** is present on Case, Contact, and Facility — these are org-owned resources
- **LookupValue** uses nullable `TenantId` — `null` means global/platform-level, non-null means tenant-specific customization
- No `programId` or legacy identifiers are used anywhere
- The pattern follows CareConnect's `OwnerOrganizationId` / Identity's `OrganizationId` approach

---

## 8. Relationship Assumptions (Future)

The entities are designed to support these future relationships without requiring structural changes:

| Relationship | Mechanism |
|-------------|-----------|
| Case ↔ Contact | Join entity `CaseContact` with role (e.g., Attorney, Physician) |
| Case ↔ Facility | Join entity `CaseFacility` for treatment locations |
| Case ↔ Lien | `Lien` entity with `CaseId` FK (future entity) |
| Case ↔ Task | `ServicingItem` with `LinkedCaseId` FK (future entity) |
| Case ↔ Settlement | `Settlement` entity with `CaseId` FK (future entity) |
| Facility ↔ Contact | Join entity or `Contact.FacilityId` FK |
| Case ↔ LookupValue | Status stored as string code, resolved via lookup query |
| Facility ↔ Identity Org | `Facility.OrganizationId` soft FK (already modeled) |

No heavy navigation collections were added — relationships will be expressed through separate join entities or FK properties when those features are built.

---

## 9. What Was Deferred

| Item | Reason |
|------|--------|
| **Lien entity** | Core to the product but involves complex multi-org workflow (seller/buyer/holder), offers, bill of sale. Will be its own feature. |
| **ServicingItem entity** | Operations-focused task management. Requires Lien + Case entities first. |
| **BillOfSale entity** | Legal execution entity. Depends on Lien. |
| **LienOffer entity** | Negotiation entity. Depends on Lien. |
| **CaseContact / CaseFacility join entities** | Relationship management. Deferred to repository/persistence phase. |
| **EF DbContext / configurations** | Domain must remain pure per spec. |
| **Repositories / migrations** | Infrastructure layer concern. |
| **API models / endpoints** | Application/API layer concern. |
| **Workflow rules** | Will follow CareConnect's `ReferralWorkflowRules` pattern when status transitions are formalized. |
| **Address as owned type** | `Address` value object is defined but entities use inline address fields for CareConnect consistency. Can be adopted when EF configuration is built. |

---

## 10. Build Results

```
Liens.Domain    → Build succeeded. 0 Warning(s). 0 Error(s).
Liens.Api       → Build succeeded. 0 Warning(s). 0 Error(s).
  (includes: BuildingBlocks → Liens.Domain → Liens.Application → Contracts → Liens.Infrastructure → Liens.Api)
```

Full service stack compiles cleanly. Identity, CareConnect, Fund, Documents, Notifications, and Gateway are unaffected (no shared code was modified).

---

## 11. Confirmations

| Check | Status |
|-------|--------|
| No persistence logic (EF attributes, DbContext, repositories) | ✅ Confirmed |
| No API logic (controllers, endpoints, DTOs) | ✅ Confirmed |
| No auth logic in Domain | ✅ Confirmed |
| No v1 code copied (all v2-native) | ✅ Confirmed |
| Domain depends only on `BuildingBlocks` | ✅ Confirmed |

---

## 12. API & Behavior Preservation Analysis

### v1 Fields That Influenced Design

| v1 Type | v1 Field | v2 Entity.Property | Notes |
|---------|----------|--------------------|-------|
| `CaseSummary` | `caseNumber` | `Case.CaseNumber` | Preserved exactly — primary business key |
| `CaseSummary` | `status` | `Case.Status` | Same lifecycle codes preserved |
| `CaseSummary` | `clientName` | `Case.ClientFirstName/LastName` | Split for structured queries |
| `CaseDetail` | `dateOfIncident` | `Case.DateOfIncident` | `string` → `DateOnly?` for type safety |
| `CaseDetail` | `insuranceCarrier/policyNumber/claimNumber` | `Case.*` | Preserved exactly |
| `CaseDetail` | `demandAmount/settlementAmount` | `Case.*` | Preserved exactly |
| `CaseDetail` | `clientDob/Phone/Email/Address` | `Case.Client*` | Preserved (address deferred to join entity) |
| `ContactSummary` | `contactType` | `Contact.ContactType` | Same type codes preserved |
| `ContactSummary` | `name` | `Contact.DisplayName` | Computed from FirstName + LastName |
| `ContactSummary` | `organization/email/phone/city/state` | `Contact.*` | Preserved |
| `ContactDetail` | `title/address/zipCode/notes/website/fax` | `Contact.*` | `zipCode` → `PostalCode` (v2 convention) |
| `LienType` | all values | `LienType.*` | Preserved exactly for future Lien entity |
| `LienStatus` | all values | `LienStatus.*` | Preserved exactly |
| `ServicingStatus/Priority` | all values | `ServicingStatus/Priority.*` | Preserved for future ServicingItem |

### Fields Preserved for API Compatibility
All core v1 fields are represented. Future APIs can map with minimal transformation:
- `clientName` → concat `ClientFirstName + " " + ClientLastName`
- `zipCode` → `PostalCode` (simple rename)
- `name` on Contact → `DisplayName`

### Fields Changed and Why
| v1 Field | Change | Reason |
|----------|--------|--------|
| `clientName` (string) | Split → `ClientFirstName` + `ClientLastName` | Structured queries, matches CareConnect Party pattern |
| `dateOfIncident` (string) | → `DateOnly?` | Type safety, v2 convention |
| `zipCode` | → `PostalCode` | v2 naming convention (matches CareConnect Facility) |
| `assignedTo` (string) | Deferred | Will be modeled via relationship to Identity user/org |
| `lawFirm` / `medicalFacility` (strings) | → Relationships to Contact/Facility entities | Normalized per v2 relational design |
| `totalLienAmount` / `lienCount` | Deferred | Computed aggregates — will be calculated from Lien records |

### API Mapping Feasibility
High. All preserved fields map 1:1 or with trivial transformation. The `CaseSummary` API shape can be reconstructed from `Case` entity + aggregated Lien data + Contact/Facility joins.

---

## 13. Risks / Assumptions

| Risk/Assumption | Impact | Mitigation |
|-----------------|--------|------------|
| `clientName` split may need API shim | Low | `DisplayName` pattern or concat in DTO mapping |
| `LookupValue` categories are hardcoded | Low | `LookupCategory` can be extended; validation prevents unknown categories |
| `Address` value object defined but not used by entities | None | Entities use inline address fields for CareConnect consistency; VO available for future adoption as EF owned type |
| No workflow rules yet | Expected | Will follow `ReferralWorkflowRules` pattern when formalized |
| `OrgId` is required (not nullable) on Case/Contact/Facility | Design choice | Matches v2 pattern where all user-created data is org-owned |

---

## 14. Final Readiness

| Question | Answer |
|----------|--------|
| Is the domain model established? | ✅ Yes — 4 entities, 7 enum/constant classes, 1 value object |
| Ready for next feature? | ✅ Yes — Lien entity, repositories, or API layer can build on this foundation |
| Follows v2 conventions? | ✅ Verified against Fund, CareConnect, Identity patterns |
| Preserves v1 behavior? | ✅ All core fields and status lifecycles preserved |
| Build clean? | ✅ Full Liens service stack: 0 warnings, 0 errors |
