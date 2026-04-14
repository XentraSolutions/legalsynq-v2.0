# LS-LIENS-03-005 — v1 SynqLiens → v2 Liens Migration Matrix

**Date:** 2026-04-14
**Scope:** Analysis and design only — mapping v1 SynqLiens data structures to v2 Liens service and retained shared services
**Status:** Complete

---

## 1. Executive Summary

This report provides the authoritative migration matrix for transitioning v1 SynqLiens data structures into the v2 LegalSynq platform. The v1 SynqLiens product has no independent backend database schema of its own — the "schema" is defined entirely through frontend TypeScript types, mock data, and Zustand store operations which collectively represent the donor behavioral contract.

The v2 target is a .NET 8 microservices architecture where:
- **Liens service** owns all lien-specific business data (`liens_` prefix)
- **Identity service** owns users, orgs, tenants, roles, permissions (`idt_` prefix)
- **Documents service** owns file storage, versioning, audit trails (`docs_` prefix)
- **Notifications service** owns communication channels and delivery (`ntf_` prefix)
- **Audit service** owns platform-wide event history (`aud_` prefix)

The v2 Liens domain already has 7 entities defined (Case, Contact, Facility, LookupValue, Lien, LienOffer, BillOfSale) plus 10 enum/constant classes. This matrix identifies what remains to be built, what maps directly, and what must be redesigned.

---

## 2. Migration Strategy Summary

| Strategy | Count | Description |
|----------|-------|-------------|
| **Direct reuse** | 7 | v2 entity already exists and maps cleanly to v1 concept |
| **Redesign required** | 3 | v1 concept exists but needs a new v2 entity or significant restructuring |
| **Delegate to shared service** | 4 | v1 concept now owned by Identity, Documents, Notifications, or Audit |
| **Retire / archive** | 2 | v1 concept superseded by v2 architecture; no runtime equivalent needed |

---

## 3. Source Artifacts Inspected

| Artifact | Location | Type | Purpose |
|----------|----------|------|---------|
| Frontend type definitions | `apps/web/src/types/lien.ts` | TypeScript interfaces/constants | Authoritative v1 data shape and API contract |
| Mock data | `apps/web/src/lib/lien-mock-data.ts` | TypeScript data | v1 behavioral examples, field values, relationships |
| Frontend store | `apps/web/src/stores/lien-store.ts` | Zustand store | v1 CRUD operations, state management patterns |
| Frontend pages | `apps/web/src/app/(platform)/lien/**` | Next.js pages | 23 pages covering all v1 UI workflows |
| v2 Liens domain entities | `apps/services/liens/Liens.Domain/Entities/*.cs` | C# domain | Current v2 target model (7 entities) |
| v2 Liens enums | `apps/services/liens/Liens.Domain/Enums/*.cs` | C# constants | Status/type code sets (10 files) |
| v2 Identity schema export | `exports/identity_db_schema.sql` | SQL DDL | Production identity_db structure |
| v2 Documents schema | `apps/services/documents/Documents.Infrastructure/Database/schema.sql` | SQL DDL | docs_ prefixed tables |
| v2 Notifications schema | `attached_assets/notifications-schema_1774999634019.sql` | SQL DDL | ntf_ prefixed tables |
| v2 Audit migrations | `apps/services/audit/Data/Migrations/*.cs` | EF Core | aud_ prefixed tables |
| Capability system | `apps/services/liens/Liens.Domain/LiensCapabilities.cs` | C# | LIENS_SELL, LIENS_MANAGE_INTERNAL |
| Role-permission mappings | `apps/services/identity/Identity.Infrastructure/Data/Configurations/RolePermissionMappingConfiguration.cs` | C# seed | SynqLienSeller, SynqLienHolder, SynqLienBuyer roles |
| Previous analysis reports | `analysis/LS-LIENS-03-001-report.md` through `LS-LIENS-03-004-report.md` | Markdown | Domain entity design decisions and rationale |

---

## 4. Current v2 Target Model Summary

### 4.1 Liens Domain Entities (Existing)

| Entity | Key Fields | Status Lifecycle | Notes |
|--------|-----------|-----------------|-------|
| `Case` | CaseNumber, ClientFirstName/LastName, DateOfIncident, DemandAmount, SettlementAmount | PreDemand → DemandSent → InNegotiation → CaseSettled → Closed | Org-scoped, insurance fields, parent to liens |
| `Lien` | LienNumber, LienType, OriginalAmount, CurrentBalance, OfferPrice, PurchasePrice, PayoffAmount | Draft → Offered → UnderReview → Sold → Active → Settled (with Withdrawn, Cancelled, Disputed exits) | Core entity; SellingOrgId/BuyingOrgId/HoldingOrgId |
| `LienOffer` | LienId, BuyerOrgId, SellerOrgId, OfferAmount | Pending → Accepted/Rejected/Withdrawn/Expired | Bilateral negotiation record |
| `BillOfSale` | LienId, LienOfferId, BillOfSaleNumber, PurchaseAmount, OriginalLienAmount | Draft → Pending → Executed (with Cancelled exit) | Transfer instrument; auto-calculates DiscountPercent |
| `Facility` | Name, Code, Address, Phone/Email/Fax, OrganizationId | IsActive flag | Medical/service provider location |
| `Contact` | ContactType, FirstName/LastName, Organization, Address | IsActive flag | LawFirm, Provider, LienHolder, CaseManager, InternalUser |
| `LookupValue` | Category, Code, Name, SortOrder | IsActive/IsSystem flags | Dynamic dropdown values; system vs tenant-custom |

### 4.2 Liens Domain Enums (Existing)

| Enum | Values |
|------|--------|
| `CaseStatus` | PreDemand, DemandSent, InNegotiation, CaseSettled, Closed |
| `LienStatus` | Draft, Offered, UnderReview, Sold, Active, Settled, Withdrawn, Cancelled, Disputed |
| `LienType` | MedicalLien, AttorneyLien, SettlementAdvance, WorkersCompLien, PropertyLien, Other |
| `OfferStatus` | Pending, Accepted, Rejected, Withdrawn, Expired |
| `BillOfSaleStatus` | Draft, Pending, Executed, Cancelled |
| `ContactType` | LawFirm, Provider, LienHolder, CaseManager, InternalUser |
| `ServicingStatus` | Pending, InProgress, Completed, Escalated, OnHold |
| `ServicingPriority` | Low, Normal, High, Urgent |
| `LienParticipantRole` | Seller, Buyer, Holder |
| `LookupCategory` | CaseStatus, LienStatus, LienType, ContactType, ServicingStatus, ServicingPriority, DocumentCategory |

### 4.3 Shared Service Ownership

| Service | Prefix | Tables | Replaces v1 |
|---------|--------|--------|-------------|
| Identity | `idt_` | 33+ tables (tenants, orgs, users, roles, permissions, etc.) | v1 user management, org management, role/permission assignment |
| Documents | `docs_` | 3 tables (documents, versions, audits) | v1 document handling, file upload, versioning |
| Notifications | `ntf_` | 18 tables (notifications, templates, delivery, billing) | v1 notification delivery (if any) |
| Audit | `aud_` | 7 tables (events, exports, checkpoints, legal holds) | v1 activity logs, status history, update tracking |

---

## 5. Table-by-Table Migration Matrix

### 5.1 Core Liens Business Tables

#### 5.1.1 Cases

| Attribute | Value |
|-----------|-------|
| **v1 source** | `CaseSummary` / `CaseDetail` (frontend types) |
| **Source owner** | SynqLiens frontend |
| **Business purpose** | Legal case containing one or more liens; tracks client, incident, insurance, and settlement |
| **Destination service** | Liens |
| **Target v2 entity** | `Case` → `liens_Cases` |
| **Action** | ✅ Direct reuse — entity exists |
| **Key field mappings** | |

| v1 Field | v2 Field | Type Change | Notes |
|----------|----------|-------------|-------|
| `id` | `Id` | string → Guid | Standard v2 PK |
| `caseNumber` | `CaseNumber` | — | Preserved; `CASE-YYYY-NNNN` format |
| `status` | `Status` | — | Identical values: PreDemand, DemandSent, InNegotiation, CaseSettled, Closed |
| `clientName` | `ClientFirstName` + `ClientLastName` | string → split | v1 stores combined; v2 splits for structured queries |
| `lawFirm` | *Resolved via Contact/Org* | string → cross-entity | v2 does not store law firm name inline; resolve via Contact or org relationship |
| `medicalFacility` | *Resolved via Facility* | string → FK | v2 links via `FacilityId` on linked Lien |
| `dateOfIncident` | `DateOfIncident` | string → DateOnly | Type upgrade |
| `totalLienAmount` | *Computed* | — | Not stored; computed from linked Liens at query time |
| `lienCount` | *Computed* | — | Not stored; computed from linked Liens at query time |
| `assignedTo` | *Future: ServicingTask or CaseAssignment* | string → entity | See §5.1.7 |
| `description` | `Description` | — | Preserved |
| `clientDob` | `ClientDob` | string → DateOnly | Type upgrade |
| `clientPhone` | `ClientPhone` | — | Preserved |
| `clientEmail` | `ClientEmail` | — | Preserved |
| `clientAddress` | `ClientAddress` | — | Preserved (single field; future normalization possible) |
| `insuranceCarrier` | `InsuranceCarrier` | — | Preserved |
| `policyNumber` | `PolicyNumber` | — | Preserved |
| `claimNumber` | `ClaimNumber` | — | Preserved |
| `demandAmount` | `DemandAmount` | number → decimal | Preserved |
| `settlementAmount` | `SettlementAmount` | number → decimal | Preserved |
| `notes` | `Notes` | — | Preserved |
| `createdAtUtc` | `CreatedAtUtc` | — | Inherited from AuditableEntity |
| `updatedAtUtc` | `UpdatedAtUtc` | — | Inherited from AuditableEntity |
| *(none)* | `TenantId` | — | v2 multi-tenant addition |
| *(none)* | `OrgId` | — | v2 org scoping addition |
| *(none)* | `ExternalReference` | — | v2 convention for cross-system linking |
| *(none)* | `Title` | — | v2 addition for display title |
| *(none)* | `OpenedAtUtc` / `ClosedAtUtc` | — | v2 lifecycle timestamps |

| **Tenant/org mapping** | `TenantId` + `OrgId` required; v1 has no multi-tenant concept |
| **Behavior preservation** | Case number format, status lifecycle, client/insurance/settlement fields all preserved |
| **API preservation** | `CaseSummary` shape can be reconstructed; `totalLienAmount` and `lienCount` become computed fields; `lawFirm` and `medicalFacility` need cross-entity resolution at API layer |
| **Risks** | `assignedTo` has no v2 entity yet — requires future ServicingTask or CaseAssignment entity |

---

#### 5.1.2 Liens

| Attribute | Value |
|-----------|-------|
| **v1 source** | `LienSummary` / `LienDetail` / `CreateLienRequest` (frontend types) |
| **Source owner** | SynqLiens frontend |
| **Business purpose** | Financial claim against a settlement; core tradeable asset |
| **Destination service** | Liens |
| **Target v2 entity** | `Lien` → `liens_Liens` |
| **Action** | ✅ Direct reuse — entity exists |
| **Key field mappings** | |

| v1 Field | v2 Field | Type Change | Notes |
|----------|----------|-------------|-------|
| `id` | `Id` | string → Guid | Standard v2 PK |
| `tenantId` | `TenantId` | string → Guid | Preserved |
| `lienNumber` | `LienNumber` | — | Preserved; `LN-YYYY-NNNN` format |
| `lienType` | `LienType` | — | Identical values |
| `status` | `Status` | — | v2 expands: adds UnderReview, Active, Settled, Cancelled, Disputed beyond v1's Draft/Offered/Sold/Withdrawn |
| `originalAmount` | `OriginalAmount` | number → decimal | Type upgrade |
| `offerPrice` | `OfferPrice` | number? → decimal? | Preserved |
| `purchasePrice` | `PurchasePrice` | number? → decimal? | Preserved |
| `jurisdiction` | `Jurisdiction` | — | Preserved |
| `caseRef` | `CaseId` | string → Guid FK | v1 stores case number; v2 stores Guid FK |
| `isConfidential` | `IsConfidential` | — | Preserved |
| `subjectParty.firstName` | `SubjectFirstName` | — | Denormalized snapshot preserved |
| `subjectParty.lastName` | `SubjectLastName` | — | Denormalized snapshot preserved |
| `sellingOrg.orgId` | `SellingOrgId` | string → Guid | Preserved |
| `sellingOrg.orgName` | *Resolved via Identity* | — | Org name no longer stored on Lien; resolved at API layer |
| `buyingOrg.orgId` | `BuyingOrgId` | string → Guid | Preserved |
| `holdingOrg.orgId` | `HoldingOrgId` | string → Guid | Preserved |
| `incidentDate` | `IncidentDate` | string → DateOnly | Type upgrade |
| `description` | `Description` | — | Preserved |
| `offerExpiresAtUtc` | *On LienOffer entity* | — | Moved to LienOffer.ExpiresAtUtc |
| `offerNotes` | *On LienOffer entity* | — | Moved to LienOffer.Notes |
| `offers[]` | *LienOffer entities* | — | Promoted to first-class entity |
| *(none)* | `CurrentBalance` | — | v2 addition; tracks balance through servicing |
| *(none)* | `PayoffAmount` | — | v2 addition; recorded on settlement |
| *(none)* | `FacilityId` | — | v2 addition; links to Facility entity |
| *(none)* | `SubjectPartyId` | — | v2 addition; future structured contact reference |
| *(none)* | `ExternalReference` | — | v2 convention |
| *(none)* | `OpenedAtUtc` / `ClosedAtUtc` | — | v2 lifecycle timestamps |

| **Tenant/org mapping** | `TenantId` + `OrgId` required; `SellingOrgId` defaults to `OrgId` on creation |
| **Behavior preservation** | Lien numbers, marketplace listing (`ListForSale`), sale completion (`MarkSold`), settlement (`Settle`) all preserved. Status machine expanded for post-sale lifecycle |
| **API preservation** | `LienSummary` shape directly reconstructible; `sellingOrg.orgName` / `buyingOrg.orgName` resolved via Identity at API layer; `offers[]` becomes separate query |
| **Risks** | v2 status machine is a superset of v1 — v1 frontends must handle new statuses gracefully |

---

#### 5.1.3 Lien Offers

| Attribute | Value |
|-----------|-------|
| **v1 source** | `LienOfferSummary` / `SubmitLienOfferRequest` (frontend types) |
| **Source owner** | SynqLiens frontend |
| **Business purpose** | Buyer-to-seller purchase offer on a lien |
| **Destination service** | Liens |
| **Target v2 entity** | `LienOffer` → `liens_LienOffers` |
| **Action** | ✅ Direct reuse — entity exists |
| **Key field mappings** | |

| v1 Field | v2 Field | Type Change | Notes |
|----------|----------|-------------|-------|
| `id` | `Id` | string → Guid | Standard v2 PK |
| `lienId` | `LienId` | string → Guid | Preserved |
| `buyerOrgId` | `BuyerOrgId` | string → Guid | Preserved |
| `buyerOrgName` | *Resolved via Identity* | — | Org name resolved at API layer |
| `offerAmount` | `OfferAmount` | number → decimal | Preserved |
| `notes` | `Notes` | — | Preserved |
| `status` | `Status` | — | v2 adds Expired beyond v1's Pending/Accepted/Rejected/Withdrawn |
| *(none)* | `SellerOrgId` | — | v2 addition; captures seller side for bilateral clarity |
| *(none)* | `TenantId` | — | v2 multi-tenant addition |
| *(none)* | `ExternalReference` | — | v2 convention |
| *(none)* | `ResponseNotes` | — | v2 addition; seller's response text |
| *(none)* | `OfferedAtUtc` | — | v2 lifecycle timestamp |
| *(none)* | `ExpiresAtUtc` | — | Moved from Lien to LienOffer |
| *(none)* | `RespondedAtUtc` / `WithdrawnAtUtc` | — | v2 lifecycle timestamps |

| **Tenant/org mapping** | `TenantId` + `BuyerOrgId` + `SellerOrgId` |
| **Behavior preservation** | Offer submission, acceptance, rejection, withdrawal all preserved |
| **API preservation** | `LienOfferSummary` shape directly reconstructible |
| **Risks** | None significant |

---

#### 5.1.4 Bill of Sale

| Attribute | Value |
|-----------|-------|
| **v1 source** | `BillOfSaleSummary` / `BillOfSaleDetail` (frontend types) |
| **Source owner** | SynqLiens frontend |
| **Business purpose** | Legal transfer instrument recording ownership change |
| **Destination service** | Liens |
| **Target v2 entity** | `BillOfSale` → `liens_BillsOfSale` |
| **Action** | ✅ Direct reuse — entity exists |
| **Key field mappings** | See `LS-LIENS-03-004-report.md` §13 for complete mapping |

| v1 Field | v2 Field | Notes |
|----------|----------|-------|
| `bosNumber` | `BillOfSaleNumber` | Renamed to v2 convention |
| `status` | `Status` | Identical: Draft, Pending, Executed, Cancelled |
| `lienId` | `LienId` | Preserved |
| `sellerOrg` | `SellerOrgId` | Name resolved via Identity |
| `buyerOrg` | `BuyerOrgId` | Name resolved via Identity |
| `saleAmount` | `PurchaseAmount` | Renamed; same meaning |
| `originalLienAmount` | `OriginalLienAmount` | Preserved |
| `discountPercent` | `DiscountPercent` | Auto-calculated |
| `executionDate` | `ExecutedAtUtc` | Upgraded from date string to DateTime |
| `sellerContact` | `SellerContactName` | Preserved |
| `buyerContact` | `BuyerContactName` | Preserved |
| `terms` | `Terms` | Preserved |
| `notes` | `Notes` | Preserved |

| **Risks** | None — fully detailed in LS-LIENS-03-004-report.md |

---

#### 5.1.5 Contacts

| Attribute | Value |
|-----------|-------|
| **v1 source** | `ContactSummary` / `ContactDetail` (frontend types) |
| **Source owner** | SynqLiens frontend |
| **Business purpose** | Lawyers, providers, lien holders, case managers involved in lien workflows |
| **Destination service** | Liens |
| **Target v2 entity** | `Contact` → `liens_Contacts` |
| **Action** | ✅ Direct reuse — entity exists |
| **Key field mappings** | |

| v1 Field | v2 Field | Type Change | Notes |
|----------|----------|-------------|-------|
| `id` | `Id` | string → Guid | Standard v2 PK |
| `contactType` | `ContactType` | — | Identical values |
| `name` | `FirstName` + `LastName` → `DisplayName` | string → split | v1 combined; v2 structured + computed DisplayName |
| `organization` | `Organization` | — | Preserved |
| `email` | `Email` | — | Preserved |
| `phone` | `Phone` | — | Preserved |
| `city` | `City` | — | Preserved |
| `state` | `State` | — | Preserved |
| `activeCases` | *Computed* | — | Not stored; computed at query time |
| `title` | `Title` | — | Preserved |
| `address` | `AddressLine1` | — | Renamed for clarity |
| `zipCode` | `PostalCode` | — | Renamed |
| `notes` | `Notes` | — | Preserved |
| `website` | `Website` | — | Preserved |
| `fax` | `Fax` | — | Preserved |
| *(none)* | `TenantId` / `OrgId` | — | v2 multi-tenant/org scoping |

| **Behavior preservation** | Contact types, all fields preserved; `activeCases` becomes computed |
| **API preservation** | `ContactSummary` fully reconstructible |
| **Risks** | None |

---

#### 5.1.6 Facilities

| Attribute | Value |
|-----------|-------|
| **v1 source** | `medicalFacility` field on `CaseSummary` + `CaseDetail`; provider names in mock data |
| **Source owner** | SynqLiens frontend |
| **Business purpose** | Medical/service provider location where care was rendered and lien originated |
| **Destination service** | Liens |
| **Target v2 entity** | `Facility` → `liens_Facilities` |
| **Action** | ✅ Direct reuse — entity exists |

| **v1 representation** | Inline string field (`medicalFacility: 'Desert Springs Medical'`) |
| **v2 representation** | First-class entity with Name, Code, full address, Phone/Email/Fax, OrganizationId link |
| **Migration note** | v1 facility names must be ETL'd into Facility records; linked via `Lien.FacilityId` |
| **Risks** | v1 may have duplicate facility name strings for the same physical location — deduplication needed during ETL |

---

#### 5.1.7 Servicing Tasks

| Attribute | Value |
|-----------|-------|
| **v1 source** | `ServicingItem` / `ServicingDetail` (frontend types) |
| **Source owner** | SynqLiens frontend |
| **Business purpose** | Internal work items: lien verification, document collection, payment processing, negotiation, settlement distribution |
| **Destination service** | Liens |
| **Target v2 entity** | ⚠️ **`ServicingTask`** → `liens_ServicingTasks` — **NOT YET CREATED** |
| **Action** | 🔧 **Redesign required** — new entity needed |
| **Key field mappings** | |

| v1 Field | Proposed v2 Field | Type | Notes |
|----------|-------------------|------|-------|
| `id` | `Id` | Guid | Standard v2 PK |
| `taskNumber` | `TaskNumber` | string | `SVC-YYYY-NNNN` format |
| `taskType` | `TaskType` | string | Lien Verification, Document Collection, Payment Processing, etc. |
| `status` | `Status` | string | Uses existing `ServicingStatus` enum |
| `priority` | `Priority` | string | Uses existing `ServicingPriority` enum |
| `caseNumber` | `CaseId` | Guid? | FK to Case (not inline string) |
| `lienNumber` | `LienId` | Guid? | FK to Lien (not inline string) |
| `assignedTo` | `AssignedToUserId` | Guid | FK to Identity User |
| `description` | `Description` | string | Task description |
| `dueDate` | `DueAtUtc` | DateTime | Deadline |
| `notes` | `Notes` | string? | Working notes |
| `resolution` | `Resolution` | string? | Completion notes |
| `linkedContactId` | `ContactId` | Guid? | FK to Contact |
| `history[]` | *Replaced by Audit service* | — | Task history events published to Audit service |
| *(none)* | `TenantId` | Guid | v2 multi-tenant |
| *(none)* | `OrgId` | Guid | v2 org scoping |
| *(none)* | `CompletedAtUtc` | DateTime? | Lifecycle timestamp |
| *(none)* | `EscalatedAtUtc` | DateTime? | Lifecycle timestamp |

| **Tenant/org mapping** | `TenantId` + `OrgId` required; `AssignedToUserId` resolved via Identity |
| **Behavior preservation** | Task types, status lifecycle, priority, assignment, linked case/lien/contact all preserved |
| **API preservation** | `ServicingItem` / `ServicingDetail` shapes fully reconstructible |
| **Provider capability impact** | Servicing tasks are primarily used in **LIENS_MANAGE_INTERNAL** mode (lien holder operations). Sellers may also have tasks for pre-sale preparation |
| **Risks** | `history[]` array in v1 must be migrated to Audit service events during ETL; `assignedTo` is a display name in v1 that must be mapped to a user Guid |

---

#### 5.1.8 Lien Status History

| Attribute | Value |
|-----------|-------|
| **v1 source** | `LienStatusHistoryItem` (frontend type) |
| **Source owner** | SynqLiens frontend |
| **Business purpose** | Chronological record of lien status transitions |
| **Destination service** | **Audit** (primary) / Liens (optional) |
| **Target v2 entity** | `aud_AuditEventRecords` — Audit service |
| **Action** | 🔧 **Redesign** — delegated to Audit service |

| v1 Field | v2 Mapping | Notes |
|----------|-----------|-------|
| `status` | `AuditEventRecord.Detail` (JSON field) | Status value captured in event detail |
| `occurredAtUtc` | `AuditEventRecord.OccurredAtUtc` | Preserved |
| `label` | `AuditEventRecord.EventType` | e.g., `liens.lien.status_changed` |
| `actorOrgName` | Resolved from `ActorId` via Identity | Org name not stored in audit |

| **Alternative** | A lightweight `liens_LienStatusHistory` table could also be created within the Liens service for fast lien-local timeline queries, with the Audit service as the system of record. This is an implementation-time decision |
| **Risks** | If Liens-local history table is skipped, timeline queries require cross-service calls to Audit |

---

#### 5.1.9 Case Notes

| Attribute | Value |
|-----------|-------|
| **v1 source** | `caseNotes` in Zustand store (`addCaseNote` action) |
| **Source owner** | SynqLiens frontend |
| **Business purpose** | Free-text notes attached to cases by users |
| **Destination service** | Liens |
| **Target v2 entity** | ⚠️ **`CaseNote`** → `liens_CaseNotes` — **NOT YET CREATED** |
| **Action** | 🔧 **Redesign required** — new entity needed |
| **Proposed fields** | |

| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | PK |
| `TenantId` | Guid | Multi-tenant scoping |
| `CaseId` | Guid | FK to Case |
| `Text` | string | Note content |
| `AuthorUserId` | Guid | FK to Identity user |
| `CreatedAtUtc` | DateTime | When note was added |
| `UpdatedAtUtc` | DateTime | If edited |

| **Risks** | Minimal — straightforward entity |

---

#### 5.1.10 Activity Feed

| Attribute | Value |
|-----------|-------|
| **v1 source** | `MOCK_RECENT_ACTIVITY` / `ActivityEntry` in store |
| **Source owner** | SynqLiens frontend |
| **Business purpose** | Dashboard activity feed showing recent platform events |
| **Destination service** | **Audit** |
| **Target v2 entity** | `aud_AuditEventRecords` — queried/filtered for dashboard display |
| **Action** | ✅ **Delegate to Audit** — no Liens-owned entity needed |

| v1 Field | v2 Mapping | Notes |
|----------|-----------|-------|
| `type` | `AuditEventRecord.Category` | Event category |
| `description` | `AuditEventRecord.Description` | Event description |
| `actor` | Resolved from `ActorId` | User name from Identity |
| `timestamp` | `AuditEventRecord.OccurredAtUtc` | Preserved |
| `icon` / `color` | *Frontend-only concern* | Not stored; derived from event type at render time |

| **Risks** | None — natural fit for Audit service |

---

### 5.2 Lookup/Reference Tables

#### 5.2.1 Status/Type Code Sets

| v1 Concept | v2 Implementation | Strategy |
|------------|-------------------|----------|
| `LienStatus` values | `LienStatus` static class + `LookupValue` seed data | **Preserved as code constants** — LookupValue provides tenant-customizable display names |
| `CaseStatus` values | `CaseStatus` static class + `LookupValue` seed data | **Preserved as code constants** |
| `LienType` values | `LienType` static class + `LookupValue` seed data | **Preserved as code constants** |
| `ContactType` values | `ContactType` static class + `LookupValue` seed data | **Preserved as code constants** |
| `BillOfSaleStatus` values | `BillOfSaleStatus` static class | **Preserved as code constants** |
| `ServicingStatus` values | `ServicingStatus` static class | **Preserved as code constants** |
| `ServicingPriority` values | `ServicingPriority` static class | **Preserved as code constants** |
| `OfferStatus` values | `OfferStatus` static class | **Preserved as code constants** |
| `DocumentCategory` values | `DocumentCategory` (frontend only for now) | Maps to Documents service document type |
| `DocumentStatus` values | `DocumentStatus` (frontend only) | Maps to `docs_documents.status` |
| `UserStatus` values | Identity service `idt_Users.Status` | **Delegated to Identity** |
| `LienParticipantRole` values | `LienParticipantRole` static class | **Preserved as code constants** |

**LookupValue entity** (`liens_LookupValues`) serves as the tenant-customizable registry for display names, sort orders, and active/inactive flags for all Liens-owned code sets. System-seeded values are immutable; tenants can add custom values.

---

#### 5.2.2 State/Jurisdiction Lookups

| v1 Concept | v2 Implementation | Strategy |
|------------|-------------------|----------|
| `jurisdiction` on LienSummary | String field on `Lien.Jurisdiction` | **Preserved as free-text** — no separate states table needed; standard US state codes used by convention |

---

### 5.3 Identity-Related Data

| v1 Concept | v1 Source | v2 Owner | Action |
|------------|----------|----------|--------|
| **Users** | `LienUser` / `LienUserDetail` | Identity (`idt_Users`) | **Delegate** — all user CRUD, status, permissions owned by Identity |
| **User roles/permissions** | `LienUserDetail.permissions[]`, `role` field | Identity (`idt_Roles`, `idt_RolePermissionMappings`) | **Delegate** — SynqLienSeller/Holder/Buyer product roles seeded in Identity |
| **Organizations** | `OrgSnapshot` (orgId, orgName) | Identity (`idt_Organizations`) | **Delegate** — Liens stores Guid references; names resolved via Identity API |
| **User activity log** | `LienUserDetail.activityLog[]` | Audit (`aud_AuditEventRecords`) | **Delegate** — user actions published to Audit service |
| **Tenant management** | Implicit in v1 | Identity (`idt_Tenants`) | **Delegate** — already managed |
| **Department assignment** | `LienUser.department` | Identity (future org structure) | **Delegate** — maps to org unit in Identity |

**Key migration decision:** The v1 `LienUser` / `LienUserDetail` types represent a SynqLiens-specific projection of Identity data. In v2, the frontend will call Identity API directly (via Gateway) for user management, role assignment, and permission checks. No user data is stored in the Liens service.

---

### 5.4 Documents-Related Data

| v1 Concept | v1 Source | v2 Owner | Action |
|------------|----------|----------|--------|
| **Document metadata** | `DocumentSummary` / `DocumentDetail` | Documents (`docs_documents`) | **Delegate** — all document CRUD owned by Documents service |
| **Document categories** | `DocumentCategory` constants | Documents (`docs_documents.reference_type` or custom field) | **Delegate** — category as metadata; Liens-specific categories (MedicalRecord, LegalFiling, Financial, Contract) registered as document types |
| **Document versioning** | `DocumentDetail.version` | Documents (`docs_document_versions`) | **Delegate** — versioning handled by Documents service |
| **Document linkage** | `linkedEntity` / `linkedEntityId` | Documents (`docs_documents.reference_id` + `reference_type`) | **Delegate** — Documents stores reference; product_id = "SYNQ_LIENS" |
| **Document processing** | `DocumentDetail.processingNotes` | Documents service | **Delegate** |
| **BOS document attachment** | `BillOfSale.DocumentId` | Liens → Documents cross-reference | **Hybrid** — Liens stores `DocumentId` Guid; Documents owns the file |

**Key migration decision:** The v1 `DocumentSummary` / `DocumentDetail` types are a SynqLiens-specific view. In v2, document upload/retrieval flows through the Documents service API. The Liens service creates document references with `reference_type = 'Case'/'Lien'/'BillOfSale'` and `product_id = 'SYNQ_LIENS'`.

---

### 5.5 Notifications-Related Data

| v1 Concept | v1 Source | v2 Owner | Action |
|------------|----------|----------|--------|
| **Notification delivery** | Implicit (no v1 type) | Notifications (`ntf_notifications`) | **Delegate** — Liens publishes events; Notifications handles delivery |
| **Notification templates** | Implicit | Notifications (`ntf_templates`) | **Delegate** — Liens-specific templates (offer received, BOS executed, etc.) registered in Notifications service |
| **Contact preferences** | Implicit | Notifications (`ntf_contact_suppressions`, `ntf_tenant_contact_policies`) | **Delegate** |

**Key migration decision:** The v1 SynqLiens has no explicit notification infrastructure. V2 Liens publishes domain events (offer received, lien sold, BOS executed, task assigned, etc.) to the Notifications service for delivery via configured channels.

---

### 5.6 Audit/History-Related Data

| v1 Concept | v1 Source | v2 Owner | Action |
|------------|----------|----------|--------|
| **Status history** | `LienStatusHistoryItem` | Audit (`aud_AuditEventRecords`) | **Delegate** — status transitions published as audit events |
| **Activity feed** | `ActivityEntry` / `MOCK_RECENT_ACTIVITY` | Audit (`aud_AuditEventRecords`) | **Delegate** — dashboard queries Audit service |
| **User activity log** | `LienUserDetail.activityLog[]` | Audit (`aud_AuditEventRecords`) | **Delegate** — user actions published as audit events |
| **Servicing task history** | `ServicingDetail.history[]` | Audit (`aud_AuditEventRecords`) | **Delegate** — task actions published as audit events |
| **Case/Lien change tracking** | Implicit (via `updatedAtUtc`) | Audit (`aud_AuditEventRecords`) | **Delegate** — change events published with before/after payloads |

**Key migration decision:** All history/audit concerns centralized in the Audit service. The Liens service application layer publishes structured audit events for every state change. The Audit service provides the query API for timeline/history views.

---

### 5.7 Marketplace/Portfolio/Batch Data

| v1 Concept | v1 Source (UI Page) | v2 Owner | Action |
|------------|---------------------|----------|--------|
| **Marketplace listings** | `/lien/marketplace` | Liens | **Reuse** — marketplace is a filtered view of Liens where `Status = Offered` and the querying org is not the seller. Capability: `LIENS_SELL` (seller side) or `LienBrowse`/`LienPurchase` (buyer side) |
| **My Liens** | `/lien/my-liens` | Liens | **Reuse** — filtered view where `SellingOrgId` or `OrgId` matches the current user's org. Capability: `LIENS_SELL` |
| **Portfolio** | `/lien/portfolio` | Liens | **Reuse** — filtered view where `HoldingOrgId` matches current org. Capability: `LIENS_MANAGE_INTERNAL` |
| **Batch entry** | `/lien/batch-entry` | Liens | **Reuse** — bulk creation endpoint for `Lien.Create()`. No separate entity needed; future API accepts array of `CreateLienRequest` |
| **Task manager** | `/lien/task-manager` | Liens | **Requires ServicingTask entity** (§5.1.7) |
| **Dashboard** | `/lien/dashboard` | Liens + Audit | **Composite view** — metrics from Liens, activity from Audit |

---

## 6. Top Reuse Candidates

These v1 concepts map directly to existing v2 entities with no or minimal changes:

| Rank | v1 Concept | v2 Entity | Confidence |
|------|------------|-----------|------------|
| 1 | `LienSummary`/`LienDetail` | `Lien` | 95% — only API layer differences (computed fields, org name resolution) |
| 2 | `CaseSummary`/`CaseDetail` | `Case` | 90% — `totalLienAmount`/`lienCount`/`assignedTo` become computed |
| 3 | `LienOfferSummary` | `LienOffer` | 95% — direct match |
| 4 | `BillOfSaleSummary`/`BillOfSaleDetail` | `BillOfSale` | 95% — field renames only |
| 5 | `ContactSummary`/`ContactDetail` | `Contact` | 90% — name splitting; `activeCases` computed |
| 6 | Facility references | `Facility` | 85% — promoted from inline string to entity |
| 7 | All status/type code sets | Existing enum classes + `LookupValue` | 100% — identical values |

---

## 7. Top Redesign Candidates

These v1 concepts need new v2 entities or significant restructuring:

| Rank | v1 Concept | Required v2 Entity | Complexity | Reason |
|------|------------|-------------------|------------|--------|
| 1 | `ServicingItem`/`ServicingDetail` | `ServicingTask` | Medium | New entity; task assignment, status, priority, linked case/lien/contact. Enums already exist |
| 2 | `caseNotes` store action | `CaseNote` | Low | Simple note entity; minimal fields |
| 3 | `LienStatusHistoryItem` | Audit events + optional `LienStatusHistory` | Low-Medium | Primary delegation to Audit; optional Liens-local table for fast timeline queries |

---

## 8. Retire / Archive Candidates

| v1 Concept | Reason for Retirement |
|------------|----------------------|
| **User management UI data** (`LienUser`, `LienUserDetail`) | Runtime concern now owned by Identity service. Frontend calls Identity API directly. No Liens-side user storage |
| **Document handling UI data** (`DocumentSummary`, `DocumentDetail`) | Runtime concern now owned by Documents service. Frontend calls Documents API directly. Liens stores only cross-reference Guids |

---

## 9. Tenant/Org Normalization Notes

### 9.1 v1 Patterns Identified

| Pattern | v1 Usage | v2 Mapping |
|---------|----------|------------|
| **No multi-tenancy** | v1 has `tenantId` on `LienSummary` but no enforcement infrastructure | v2: `TenantId` required on all entities; enforced at DB, service, and Gateway levels |
| **Org as name string** | `sellingOrg.orgName`, `lawFirm`, `medicalFacility` are display names | v2: `OrgId` (Guid FK to Identity); names resolved at API layer |
| **User as name string** | `assignedTo: 'Sarah Chen'` | v2: `AssignedToUserId` (Guid FK to Identity) |
| **Implicit org ownership** | v1 liens belong to whoever created them | v2: `OrgId` explicit on every entity; `SellingOrgId` / `BuyingOrgId` / `HoldingOrgId` track transfer chain |

### 9.2 Migration Rules

1. **TenantId**: All v1 data receives a TenantId during ETL. Single-tenant v1 deployments map to one v2 tenant
2. **OrgId**: v1 org name strings must be matched to `idt_Organizations` records. Create orgs if they don't exist
3. **UserId**: v1 `assignedTo` and `actor` name strings must be matched to `idt_Users` records
4. **lawFirm → Contact or Org**: v1 `lawFirm` strings on CaseSummary map to Contact entities of type `LawFirm` or to org references
5. **medicalFacility → Facility**: v1 `medicalFacility` strings map to `Facility` entities; deduplicate by name

---

## 10. Data Type Correction Notes

| v1 Pattern | v1 Type | v2 Correction | Affected Fields |
|------------|---------|---------------|-----------------|
| ISO date strings | `string` (`"2024-03-15"`) | `DateOnly` | `dateOfIncident`, `clientDob`, `dueDate` |
| ISO datetime strings | `string` (`"2024-10-20T10:00:00Z"`) | `DateTime` (UTC) | All `*AtUtc` fields |
| Money as number | `number` (JS) | `decimal` (C#) | `originalAmount`, `offerAmount`, `purchasePrice`, `saleAmount`, `demandAmount`, `settlementAmount` |
| String IDs | `string` (`"l001"`) | `Guid` | All `id`, `*Id` fields |
| Combined name | `string` (`"Maria Gonzalez"`) | Split `FirstName` + `LastName` | `clientName`, `name` on Contact |
| Inline org name | `string` (`"Desert Springs Medical"`) | `Guid` FK + API resolution | `sellingOrg.orgName`, `buyerOrg.orgName`, `lawFirm`, `medicalFacility` |
| Inline assignee name | `string` (`"Sarah Chen"`) | `Guid` FK to Identity User | `assignedTo` |
| File size as string | `string` (`"2.4 MB"`) | `long` (bytes) | `fileSize` — Documents service |
| Status as loose string | `string` | Constrained `string` with enum validation | All status fields |

---

## 11. Behavior Preservation Notes

### 11.1 Critical Business Behaviors to Preserve

| Behavior | v1 Pattern | v2 Implementation | Status |
|----------|-----------|-------------------|--------|
| **Case number format** | `CASE-YYYY-NNNN` | `Case.CaseNumber` string | ✅ Preserved |
| **Lien number format** | `LN-YYYY-NNNN` | `Lien.LienNumber` string | ✅ Preserved |
| **BOS number format** | `BOS-YYYY-NNNN` | `BillOfSale.BillOfSaleNumber` string | ✅ Preserved |
| **Task number format** | `SVC-YYYY-NNNN` | Future `ServicingTask.TaskNumber` | ⚠️ Pending entity creation |
| **Marketplace listing** | Seller lists lien with offer price | `Lien.ListForSale()` transition | ✅ Preserved |
| **Offer negotiation** | Buyer submits offer on listed lien | `LienOffer.Create()` → `Accept()/Reject()` | ✅ Preserved |
| **Sale completion** | Offer accepted → lien marked sold | `LienOffer.Accept()` → `Lien.MarkSold()` → `BillOfSale.CreateFromAcceptedOffer()` | ✅ Preserved |
| **Post-sale activation** | Sold lien activated by holder | `Lien.Activate()` | ✅ Preserved |
| **Settlement** | Active lien settled with payoff amount | `Lien.Settle()` | ✅ Preserved |
| **Holder tracking** | Lien ownership transfer tracked | `Lien.HoldingOrgId` + `TransferHolding()` | ✅ Preserved |
| **Confidential liens** | Flag to restrict visibility | `Lien.IsConfidential` | ✅ Preserved |
| **Case settlement** | Case-level settlement amount | `Case.SetSettlementAmount()` | ✅ Preserved |
| **Servicing tasks** | Internal work management | ⚠️ Requires `ServicingTask` entity | ⚠️ Pending |
| **Role-based access** | Admin, Case Manager, Analyst, Viewer | Identity product roles + Liens capabilities | ✅ Preserved via Identity |

### 11.2 Behavior Gaps (v2 Additions)

| Behavior | v1 | v2 | Impact |
|----------|-----|-----|--------|
| **Multi-offer competition** | Single offer per lien | Multiple offers; UnderReview status allows comparison | Enhancement; backward compatible |
| **Dispute handling** | Not in v1 | `Disputed` status with transition to Active/Settled/Cancelled | Enhancement |
| **Current balance tracking** | Not in v1 | `Lien.CurrentBalance` updated through servicing | Enhancement |
| **Payoff amount** | Not in v1 | `Lien.PayoffAmount` recorded on settlement | Enhancement |
| **Document attachment on BOS** | Not in v1 | `BillOfSale.AttachDocument()` links to Documents service | Enhancement |
| **Expiry management** | Implicit | `LienOffer.ExpiresAtUtc` + `Expire()` with auto-check | Enhancement |

---

## 12. API Preservation Notes

### 12.1 Lien API Shape Preservation

| Frontend Type | Preserved | Changes Required |
|--------------|-----------|-----------------|
| `LienSummary` | ✅ All fields mappable | `sellingOrg.orgName` / `buyingOrg.orgName` / `holdingOrg.orgName` resolved from Identity via OrgIds |
| `LienDetail` | ✅ All fields mappable | `offers[]` becomes separate endpoint or included via `?include=offers` |
| `CreateLienRequest` | ✅ Directly mappable | Maps 1:1 to `Lien.Create()` parameters |
| `OfferLienRequest` | ✅ Directly mappable | Maps to `LienOffer.Create()` |
| `LienSearchParams` | ✅ Directly mappable | All filter fields exist on `Lien` entity |

### 12.2 Case API Shape Preservation

| Frontend Type | Preserved | Changes Required |
|--------------|-----------|-----------------|
| `CaseSummary` | ⚠️ Mostly | `totalLienAmount` and `lienCount` become computed (aggregate query); `lawFirm` and `medicalFacility` resolved from linked entities; `assignedTo` resolved from Identity user |
| `CaseDetail` | ✅ All fields mappable | Direct mapping for all detail fields |

### 12.3 Other API Shapes

| Frontend Type | Preserved | Changes Required |
|--------------|-----------|-----------------|
| `BillOfSaleSummary` / `BillOfSaleDetail` | ✅ Fully mappable | `sellerOrg` / `buyerOrg` become org name resolution; `lienNumber` / `caseNumber` resolved from linked entities |
| `ContactSummary` / `ContactDetail` | ✅ Fully mappable | `name` becomes `DisplayName`; `activeCases` computed |
| `ServicingItem` / `ServicingDetail` | ⚠️ Pending | Requires `ServicingTask` entity; `history[]` delegated to Audit |
| `DocumentSummary` / `DocumentDetail` | ✅ Delegated | Documents service API returns this shape; Liens provides cross-references |
| `LienUser` / `LienUserDetail` | ✅ Delegated | Identity service API returns this shape |

---

## 13. Provider Capability Mode Implications

### 13.1 Capability Summary

| Mode | Capability | Product Role | Key Operations |
|------|------------|-------------|----------------|
| **Sell Liens** | `LIENS_SELL` | `SynqLienSeller` | Create lien, list for sale, manage offers, view own liens |
| **Manage Own Liens** | `LIENS_MANAGE_INTERNAL` | `SynqLienHolder` | View held liens, service liens, settle liens |
| **Buy Liens** | *(implicit)* | `SynqLienBuyer` | Browse marketplace, submit purchase offers, view held liens |
| **Both** | `LIENS_SELL` + `LIENS_MANAGE_INTERNAL` | Both roles assigned | Full lifecycle |

### 13.2 Data Model Impact by Mode

| Data Area | Sell Liens | Manage Own | Both | Notes |
|-----------|-----------|------------|------|-------|
| **Cases** | Full CRUD | Read (linked to held liens) | Full CRUD | Sellers create cases; holders view linked cases |
| **Liens** | Create, list, sell | View held, service, settle | Full lifecycle | Core entity spans both modes |
| **LienOffers** | Receive, accept/reject | Submit (as buyer) | Both directions | Offer direction depends on role |
| **BillOfSale** | Seller side | Buyer/holder side | Both sides | Transfer instrument is bilateral |
| **Contacts** | CRUD for sellers | CRUD for holders | Full CRUD | Shared entity; both modes need contacts |
| **Facilities** | CRUD | Read | Full CRUD | Sellers manage facilities; holders view |
| **ServicingTasks** | Pre-sale tasks | Post-sale servicing | All task types | Task types differ by mode |
| **CaseNotes** | Full CRUD | Read (linked cases) | Full CRUD | Follows case access |

### 13.3 v1 Tables Requiring Mode-Aware Interpretation

| v1 Table/Concept | Mode Impact | Migration Note |
|-----------------|-------------|----------------|
| `assignedTo` on cases/tasks | Sellers assign internally; holders assign to their team | `AssignedToUserId` must be within the holder's org scope |
| `sellingOrg` / `buyingOrg` on liens | Determines which side the user is on | API layer uses capability check to determine view |
| Marketplace view | Sellers list; buyers browse | `Lien.Status = Offered` + org exclusion filter |
| Portfolio view | Holders view their held liens | `Lien.HoldingOrgId = current org` filter |
| My Liens view | Sellers view their created liens | `Lien.OrgId = current org` OR `Lien.SellingOrgId = current org` filter |

### 13.4 No Workflow Changes Required

The capability modes affect API authorization and query filtering only. The domain entities are mode-agnostic — they support both selling and holding workflows natively through their existing fields (OrgId, SellingOrgId, BuyingOrgId, HoldingOrgId).

---

## 14. Prefix / Service Ownership Alignment

### 14.1 Liens Service Tables (Future `liens_` Prefix)

| Proposed Table | Entity | Purpose |
|---------------|--------|---------|
| `liens_Cases` | `Case` | Legal cases |
| `liens_Liens` | `Lien` | Lien records (core asset) |
| `liens_LienOffers` | `LienOffer` | Purchase offers |
| `liens_BillsOfSale` | `BillOfSale` | Transfer instruments |
| `liens_Contacts` | `Contact` | Business contacts |
| `liens_Facilities` | `Facility` | Service provider locations |
| `liens_LookupValues` | `LookupValue` | Configurable code sets |
| `liens_ServicingTasks` | `ServicingTask` *(new)* | Internal work items |
| `liens_CaseNotes` | `CaseNote` *(new)* | Case notes |

### 14.2 Shared Service Tables (Existing, No Changes)

| Service | Prefix | Tables Used by Liens | Cross-Reference Pattern |
|---------|--------|---------------------|------------------------|
| Identity | `idt_` | `idt_Users`, `idt_Organizations`, `idt_Tenants`, `idt_Roles`, `idt_RolePermissionMappings` | Liens stores Guid references; resolves names via Identity API |
| Documents | `docs_` | `docs_documents`, `docs_document_versions` | Liens stores `DocumentId` on BillOfSale; Documents uses `reference_id` + `product_id='SYNQ_LIENS'` for reverse linkage |
| Notifications | `ntf_` | `ntf_notifications`, `ntf_templates` | Liens publishes domain events; Notifications delivers |
| Audit | `aud_` | `aud_AuditEventRecords` | Liens publishes audit events; Audit stores and queries |

### 14.3 No v1 Shared-Service Schema Treated as Final Target

Confirmed: No v1 schema (from archived Node.js services or legacy SQL) is used as a runtime target. All shared services use their established v2 schemas.

---

## 15. Unresolved Questions / Blocking Mismatches

### 15.1 Unresolved Questions

| # | Question | Impact | Recommendation |
|---|----------|--------|----------------|
| UQ-1 | Should `liens_LienStatusHistory` exist as a Liens-local table, or should all history queries go through the Audit service? | Performance of timeline queries | Create a lightweight Liens-local table AND publish to Audit. The local table is the fast-path; Audit is the system of record |
| UQ-2 | How should `CaseSummary.lawFirm` be represented in v2? Is it a Contact reference, an Org reference, or a denormalized string? | API shape preservation | Recommend a `LawFirmContactId` Guid? field on Case linking to a Contact of type LawFirm; API resolves display name |
| UQ-3 | Should `ServicingTask.taskType` be a constrained enum or a free-text + LookupValue pattern? | Extensibility vs data quality | Recommend LookupValue pattern with system-seeded values (Lien Verification, Document Collection, Payment Processing, Lien Negotiation, Settlement Distribution) and tenant-custom values |
| UQ-4 | How should `CaseSummary.assignedTo` be modeled? Single user? Or multiple assignees? | Case assignment flexibility | Recommend `PrimaryAssignedToUserId` on Case entity; if multiple assignees needed, create `liens_CaseAssignments` join table in a future phase |
| UQ-5 | Should batch entry create a `liens_BatchImports` tracking table for import status/error logging? | Operational visibility | Recommend creating `liens_BatchImports` with status, row count, error log for traceability. Low priority |

### 15.2 Blocking Mismatches

**None found.** All v1 concepts map cleanly to existing or proposed v2 entities. No domain entity changes are required to proceed with persistence implementation.

---

## 16. Recommended Next Implementation Sequence

### Phase 1: Core Persistence (Immediate)

| Step | Task | Dependencies |
|------|------|-------------|
| 1.1 | Create `LiensDbContext` with all 7 existing entities | None |
| 1.2 | Add EF Core entity configurations (table names, indexes, relationships) | 1.1 |
| 1.3 | Generate initial migration with `liens_` prefixed tables | 1.2 |
| 1.4 | Add repository interfaces and implementations | 1.1 |
| 1.5 | Seed `LookupValue` with system code sets | 1.3 |

### Phase 2: New Domain Entities

| Step | Task | Dependencies |
|------|------|-------------|
| 2.1 | Create `ServicingTask` domain entity | None (enums exist) |
| 2.2 | Create `CaseNote` domain entity | None |
| 2.3 | Add to DbContext and generate migration | 2.1, 2.2 |

### Phase 3: API Layer

| Step | Task | Dependencies |
|------|------|-------------|
| 3.1 | Define DTOs matching frontend types (`LienSummary`, etc.) | Phase 1 |
| 3.2 | Implement CRUD endpoints for Cases, Liens, Contacts, Facilities | Phase 1 |
| 3.3 | Implement offer/BOS workflow endpoints | Phase 1 |
| 3.4 | Implement servicing task endpoints | Phase 2 |
| 3.5 | Implement computed fields (totalLienAmount, lienCount, activeCases) | Phase 1 |

### Phase 4: Cross-Service Integration

| Step | Task | Dependencies |
|------|------|-------------|
| 4.1 | Integrate with Identity for org/user name resolution | Phase 3 |
| 4.2 | Integrate with Documents for document upload/attachment | Phase 3 |
| 4.3 | Integrate with Audit for event publishing | Phase 3 |
| 4.4 | Integrate with Notifications for delivery | Phase 3 |

### Phase 5: Migration/ETL (Future)

| Step | Task | Dependencies |
|------|------|-------------|
| 5.1 | Design ETL pipeline for v1 → v2 data migration | Phases 1-4 |
| 5.2 | Build org/user/facility deduplication logic | Phase 4 |
| 5.3 | Execute data migration with validation | 5.1, 5.2 |

---

## Appendix A: Complete Entity Inventory

| # | Entity | Namespace | Exists | Table | Service Owner |
|---|--------|-----------|--------|-------|---------------|
| 1 | `Case` | `Liens.Domain.Entities` | ✅ | `liens_Cases` | Liens |
| 2 | `Lien` | `Liens.Domain.Entities` | ✅ | `liens_Liens` | Liens |
| 3 | `LienOffer` | `Liens.Domain.Entities` | ✅ | `liens_LienOffers` | Liens |
| 4 | `BillOfSale` | `Liens.Domain.Entities` | ✅ | `liens_BillsOfSale` | Liens |
| 5 | `Contact` | `Liens.Domain.Entities` | ✅ | `liens_Contacts` | Liens |
| 6 | `Facility` | `Liens.Domain.Entities` | ✅ | `liens_Facilities` | Liens |
| 7 | `LookupValue` | `Liens.Domain.Entities` | ✅ | `liens_LookupValues` | Liens |
| 8 | `ServicingTask` | `Liens.Domain.Entities` | ⚠️ Pending | `liens_ServicingTasks` | Liens |
| 9 | `CaseNote` | `Liens.Domain.Entities` | ⚠️ Pending | `liens_CaseNotes` | Liens |

---

## Appendix B: Enum/Constants Inventory

| # | Enum | Exists | Values |
|---|------|--------|--------|
| 1 | `CaseStatus` | ✅ | PreDemand, DemandSent, InNegotiation, CaseSettled, Closed |
| 2 | `LienStatus` | ✅ | Draft, Offered, UnderReview, Sold, Active, Settled, Withdrawn, Cancelled, Disputed |
| 3 | `LienType` | ✅ | MedicalLien, AttorneyLien, SettlementAdvance, WorkersCompLien, PropertyLien, Other |
| 4 | `OfferStatus` | ✅ | Pending, Accepted, Rejected, Withdrawn, Expired |
| 5 | `BillOfSaleStatus` | ✅ | Draft, Pending, Executed, Cancelled |
| 6 | `ContactType` | ✅ | LawFirm, Provider, LienHolder, CaseManager, InternalUser |
| 7 | `ServicingStatus` | ✅ | Pending, InProgress, Completed, Escalated, OnHold |
| 8 | `ServicingPriority` | ✅ | Low, Normal, High, Urgent |
| 9 | `LienParticipantRole` | ✅ | Seller, Buyer, Holder |
| 10 | `LookupCategory` | ✅ | CaseStatus, LienStatus, LienType, ContactType, ServicingStatus, ServicingPriority, DocumentCategory |
| 11 | `ServicingTaskType` | ⚠️ Pending | LienVerification, DocumentCollection, PaymentProcessing, LienNegotiation, SettlementDistribution |

---

## Appendix C: Cross-Reference Summary

| From Entity | To Entity/Service | Field | Direction |
|-------------|-------------------|-------|-----------|
| `Lien` → `Case` | `CaseId` | FK within Liens |
| `Lien` → `Facility` | `FacilityId` | FK within Liens |
| `Lien` → Identity Org | `OrgId`, `SellingOrgId`, `BuyingOrgId`, `HoldingOrgId` | Cross-service ref |
| `Lien` → Identity User | `SubjectPartyId` | Cross-service ref |
| `LienOffer` → `Lien` | `LienId` | FK within Liens |
| `LienOffer` → Identity Org | `BuyerOrgId`, `SellerOrgId` | Cross-service ref |
| `BillOfSale` → `Lien` | `LienId` | FK within Liens |
| `BillOfSale` → `LienOffer` | `LienOfferId` | FK within Liens |
| `BillOfSale` → Documents | `DocumentId` | Cross-service ref |
| `BillOfSale` → Identity Org | `SellerOrgId`, `BuyerOrgId` | Cross-service ref |
| `Case` → Identity Org | `OrgId` | Cross-service ref |
| `Contact` → Identity Org | `OrgId` | Cross-service ref |
| `Facility` → Identity Org | `OrgId`, `OrganizationId` | Cross-service ref |
| `ServicingTask` → `Case` | `CaseId` | FK within Liens |
| `ServicingTask` → `Lien` | `LienId` | FK within Liens |
| `ServicingTask` → `Contact` | `ContactId` | FK within Liens |
| `ServicingTask` → Identity User | `AssignedToUserId` | Cross-service ref |
| `CaseNote` → `Case` | `CaseId` | FK within Liens |
| `CaseNote` → Identity User | `AuthorUserId` | Cross-service ref |
| All entities → Audit | Published events | Cross-service integration |
| All entities → Identity | `TenantId`, `CreatedByUserId`, `UpdatedByUserId` | Cross-service ref |

---

## Quality Check Verification

| Check | Status |
|-------|--------|
| Matrix clearly separates Liens-owned data from shared-service-owned data | ✅ |
| No v1 shared-service schema treated as final runtime target | ✅ |
| Future Liens tables identified as `liens_*` | ✅ |
| Donor behavior and API compatibility preserved | ✅ |
| Report grounded in actual inspected source artifacts | ✅ |
| Provider capability modes analyzed | ✅ |
| Data type corrections documented | ✅ |
| Tenant/org normalization documented | ✅ |
| Implementation sequence defined | ✅ |
| Unresolved questions documented without silent changes | ✅ |
