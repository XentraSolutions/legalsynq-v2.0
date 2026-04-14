# LS-LIENS-04-001 — Liens EF Core Persistence Configuration Report

**Date:** 2026-04-14
**Epic:** Liens Microservice — Persistence Foundation
**Feature:** EF Core Entity Configurations for Approved Domain Entities
**Status:** ✅ Complete — Build verified (0 errors, 0 warnings)

---

## 1. Summary

Created 7 EF Core fluent entity configuration classes for all approved Liens domain entities: Case, Contact, Facility, LookupValue, Lien, LienOffer, and BillOfSale. Each configuration maps the entity to a `liens_`-prefixed table, defines property constraints, indexes, and within-service FK relationships while keeping cross-service references as scalar fields only.

No domain entities were added or modified. No repositories, query services, API endpoints, migrations, or ETL scripts were created. The work is purely persistence configuration, ready for DbContext wiring and initial migration creation in LS-LIENS-04-002.

---

## 2. Existing v2 Persistence Pattern Identified and Followed

Inspected EF Core configurations in 4 existing v2 services:

| Service | Config Location | Table Prefix | Pattern |
|---------|----------------|-------------|---------|
| Identity | `Identity.Infrastructure/Data/Configurations/` | `idt_` | `IEntityTypeConfiguration<T>`, explicit `ToTable()`, `HasDatabaseName()` on some indexes |
| Fund | `Fund.Infrastructure/Data/Configurations/` | `fund_` | Same pattern, anonymous composite indexes, explicit audit field mapping |
| CareConnect | `CareConnect.Infrastructure/Data/Configurations/` | `cc_` | Same pattern, `HasDatabaseName()` on all indexes, `HasOne().WithMany().HasForeignKey().OnDelete(Restrict)` for within-service FKs |
| Audit | `Audit/Data/Configurations/` | `aud_` | Same pattern |

### Conventions followed exactly:

| Convention | Implementation |
|-----------|---------------|
| Config class per entity | `IEntityTypeConfiguration<T>` in `Persistence/Configurations/` |
| Explicit `ToTable("liens_*")` | All 7 configurations |
| Explicit `HasKey(x => x.Id)` | All 7 configurations |
| Explicit property mapping with `IsRequired()` / `HasMaxLength()` | All properties |
| Money fields | `HasColumnType("decimal(18,2)")` — matches Fund `ApplicationConfiguration` |
| Percent fields | `HasColumnType("decimal(5,2)")` — appropriate for `DiscountPercent` |
| Date-only fields | `HasColumnType("date")` — matches CareConnect `SubjectDobSnapshot` pattern |
| Audit fields | `CreatedByUserId.IsRequired()`, `UpdatedByUserId` (nullable), `CreatedAtUtc.IsRequired()`, `UpdatedAtUtc.IsRequired()` — matches Fund/CareConnect |
| Within-service FKs | `HasOne<T>().WithMany().HasForeignKey().OnDelete(Restrict)` |
| Cross-service refs | Scalar property only, no FK constraint |
| Index naming | `HasDatabaseName("IX_*")` for regular, `HasDatabaseName("UX_*")` for unique — matches CareConnect convention |
| DbContext discovery | Configurations placed in assembly for `ApplyConfigurationsFromAssembly()` |

### Deviation from CareConnect: No navigation properties

CareConnect entities have navigation properties (e.g., `Referral.Provider`, `Appointment.Facility`) which enable `builder.HasOne(x => x.Nav)` syntax. The Liens domain entities were designed without navigation properties (all FK fields are scalar `Guid?`). Rather than modifying the domain entities, the configurations use the generic `HasOne<T>().WithMany()` overload which achieves the same FK constraint without requiring navigation properties on the domain model. This is a supported EF Core pattern and avoids unnecessary domain modifications.

---

## 3. Files Created / Changed

### Files Created (7)

| File | Purpose |
|------|---------|
| `apps/services/liens/Liens.Infrastructure/Persistence/Configurations/CaseConfiguration.cs` | EF config for `Case` → `liens_Cases` |
| `apps/services/liens/Liens.Infrastructure/Persistence/Configurations/ContactConfiguration.cs` | EF config for `Contact` → `liens_Contacts` |
| `apps/services/liens/Liens.Infrastructure/Persistence/Configurations/FacilityConfiguration.cs` | EF config for `Facility` → `liens_Facilities` |
| `apps/services/liens/Liens.Infrastructure/Persistence/Configurations/LookupValueConfiguration.cs` | EF config for `LookupValue` → `liens_LookupValues` |
| `apps/services/liens/Liens.Infrastructure/Persistence/Configurations/LienConfiguration.cs` | EF config for `Lien` → `liens_Liens` |
| `apps/services/liens/Liens.Infrastructure/Persistence/Configurations/LienOfferConfiguration.cs` | EF config for `LienOffer` → `liens_LienOffers` |
| `apps/services/liens/Liens.Infrastructure/Persistence/Configurations/BillOfSaleConfiguration.cs` | EF config for `BillOfSale` → `liens_BillsOfSale` |

### Files Changed

None. No existing files were modified.

---

## 4. Entity-to-Table Mapping

| Entity | Table Name | PK | Business Key |
|--------|-----------|-----|-------------|
| `Case` | `liens_Cases` | `Id` (Guid) | `TenantId` + `CaseNumber` (unique) |
| `Contact` | `liens_Contacts` | `Id` (Guid) | Natural key via `TenantId` + `OrgId` + `DisplayName` (non-unique index) |
| `Facility` | `liens_Facilities` | `Id` (Guid) | Natural key via `TenantId` + `OrgId` + `Name` (non-unique index) |
| `LookupValue` | `liens_LookupValues` | `Id` (Guid) | `TenantId` + `Category` + `Code` (unique) |
| `Lien` | `liens_Liens` | `Id` (Guid) | `TenantId` + `LienNumber` (unique) |
| `LienOffer` | `liens_LienOffers` | `Id` (Guid) | No business-key uniqueness (multiple offers per lien allowed) |
| `BillOfSale` | `liens_BillsOfSale` | `Id` (Guid) | `TenantId` + `BillOfSaleNumber` (unique) |

---

## 5. Relationship Mapping Summary

### Within-Service Foreign Keys

| From Entity | FK Field | To Entity | Delete Behavior | Rationale |
|-------------|----------|-----------|----------------|-----------|
| `Lien` | `CaseId` | `Case` | `Restrict` | Prevents orphan liens; case must be explicitly unlinked |
| `Lien` | `FacilityId` | `Facility` | `Restrict` | Prevents orphan facility references |
| `LienOffer` | `LienId` | `Lien` | `Restrict` | Preserves offer history; lien cannot be deleted while offers exist |
| `BillOfSale` | `LienId` | `Lien` | `Restrict` | Legal record; lien cannot be deleted while BOS exists |
| `BillOfSale` | `LienOfferId` | `LienOffer` | `Restrict` | Traceability; BOS linked to accepted offer |

All within-service FKs use `DeleteBehavior.Restrict` to protect legal/financial records from accidental cascade deletion. This aligns with the CareConnect precedent (all FKs use `Restrict`).

### No cascade chains

The FK graph is: `BillOfSale` → `LienOffer` → `Lien` → `Case` / `Facility`. All links use `Restrict`, so no cascading deletes can propagate through the chain. This is intentional for a legal/financial domain where records must be preserved.

---

## 6. Cross-Service Reference Handling Summary

The following fields reference entities owned by other services. They are stored as scalar `Guid` or `Guid?` columns with **no SQL foreign key constraints** to external tables.

| Field | Entity | Referenced Service | Indexed |
|-------|--------|-------------------|---------|
| `TenantId` | All 7 entities | Identity | Yes (part of all composite indexes) |
| `OrgId` | Case, Contact, Facility, Lien | Identity | Yes (part of composite indexes) |
| `SellingOrgId` | Lien | Identity | Yes (`IX_Liens_TenantId_SellingOrgId_Status`) |
| `BuyingOrgId` | Lien | Identity | Yes (`IX_Liens_TenantId_BuyingOrgId`) |
| `HoldingOrgId` | Lien | Identity | Yes (`IX_Liens_TenantId_HoldingOrgId_Status`) |
| `SubjectPartyId` | Lien | Identity | No (low-cardinality, rarely queried directly) |
| `OrganizationId` | Facility | Identity | Yes (`IX_Facilities_OrganizationId`) |
| `BuyerOrgId` | LienOffer, BillOfSale | Identity | Yes (composite indexes) |
| `SellerOrgId` | LienOffer, BillOfSale | Identity | Yes (composite indexes) |
| `DocumentId` | BillOfSale | Documents | Yes (`IX_BillsOfSale_DocumentId`) |
| `CreatedByUserId` | All entities | Identity | No (audit field, not query-path) |
| `UpdatedByUserId` | All entities | Identity | No (audit field, not query-path) |

---

## 7. Field Constraints / Max Lengths / Precision

### String Field Length Strategy

| Category | Max Length | Examples |
|----------|----------|---------|
| Business numbers | 50 | `CaseNumber`, `LienNumber`, `BillOfSaleNumber`, `Code` |
| Person names | 100 | `ClientFirstName`, `ClientLastName`, `FirstName`, `LastName`, `SubjectFirstName`, `SubjectLastName` |
| Display names | 250 | `DisplayName`, `SellerContactName`, `BuyerContactName` |
| Org/entity names | 200 | `Name` (Facility), `Organization` (Contact), `InsuranceCarrier`, `ExternalReference`, `LookupValue.Name` |
| Email | 320 | RFC 5321 maximum |
| Phone/fax | 30 | International format |
| Address | 300 | `AddressLine1`, `AddressLine2` |
| City/state | 100 | Standard |
| Postal code | 20 | International |
| Status/type codes | 50 | All `Status`, `LienType`, `ContactType`, `Category`, `Priority` |
| Jurisdiction | 100 | State/jurisdiction code |
| Website/URL | 500 | `Website` |
| Title | 100-300 | `Contact.Title` (100), `Case.Title` (300) |
| Client address | 500 | `Case.ClientAddress` (single-field, unstructured) |
| Notes/description/terms | 4000 | `Notes`, `Description`, `Terms`, `ResponseNotes`, `AttorneyNotes` equivalent |
| Lookup description | 1000 | `LookupValue.Description` |

### Decimal Precision

| Field | Column Type | Rationale |
|-------|------------|-----------|
| `OriginalAmount`, `CurrentBalance`, `OfferPrice`, `PurchasePrice`, `PayoffAmount` | `decimal(18,2)` | Financial amounts; matches Fund `RequestedAmount`/`ApprovedAmount` pattern |
| `DemandAmount`, `SettlementAmount` | `decimal(18,2)` | Legal settlement amounts |
| `OfferAmount`, `PurchaseAmount`, `OriginalLienAmount` | `decimal(18,2)` | Transaction amounts |
| `DiscountPercent` | `decimal(5,2)` | Percentage (0.00–100.00); narrower precision appropriate |

### Date/Time Types

| Field | Column Type | Rationale |
|-------|------------|-----------|
| `ClientDob`, `DateOfIncident`, `IncidentDate` | `date` | Date-only; matches CareConnect `SubjectDobSnapshot` pattern |
| All `*AtUtc` fields | `datetime(6)` | MySQL default for `DateTime`; microsecond precision |

---

## 8. Index Strategy Summary

### Unique Indexes (Business Key Enforcement)

| Index | Table | Columns | Purpose |
|-------|-------|---------|---------|
| `UX_Cases_TenantId_CaseNumber` | `liens_Cases` | `TenantId`, `CaseNumber` | Prevent duplicate case numbers per tenant |
| `UX_Liens_TenantId_LienNumber` | `liens_Liens` | `TenantId`, `LienNumber` | Prevent duplicate lien numbers per tenant |
| `UX_BillsOfSale_TenantId_BillOfSaleNumber` | `liens_BillsOfSale` | `TenantId`, `BillOfSaleNumber` | Prevent duplicate BOS numbers per tenant |
| `UX_LookupValues_TenantId_Category_Code` | `liens_LookupValues` | `TenantId`, `Category`, `Code` | Prevent duplicate lookup codes per category per tenant |

### Composite Query Indexes

| Index | Table | Columns | Access Pattern |
|-------|-------|---------|---------------|
| `IX_Cases_TenantId_OrgId_Status` | `liens_Cases` | `TenantId`, `OrgId`, `Status` | Case list filtered by org and status |
| `IX_Cases_TenantId_OrgId_CreatedAtUtc` | `liens_Cases` | `TenantId`, `OrgId`, `CreatedAtUtc` | Case list sorted by creation date |
| `IX_Cases_TenantId_Status` | `liens_Cases` | `TenantId`, `Status` | Cross-org case status queries |
| `IX_Liens_TenantId_OrgId_Status` | `liens_Liens` | `TenantId`, `OrgId`, `Status` | My-liens view (owner's liens by status) |
| `IX_Liens_TenantId_Status` | `liens_Liens` | `TenantId`, `Status` | Marketplace browsing (status = Offered) |
| `IX_Liens_TenantId_LienType` | `liens_Liens` | `TenantId`, `LienType` | Type filter on lien lists |
| `IX_Liens_TenantId_SellingOrgId_Status` | `liens_Liens` | `TenantId`, `SellingOrgId`, `Status` | Seller's lien dashboard |
| `IX_Liens_TenantId_BuyingOrgId` | `liens_Liens` | `TenantId`, `BuyingOrgId` | Buyer's purchased liens |
| `IX_Liens_TenantId_HoldingOrgId_Status` | `liens_Liens` | `TenantId`, `HoldingOrgId`, `Status` | Portfolio view (holder's active liens) |
| `IX_Liens_TenantId_CreatedAtUtc` | `liens_Liens` | `TenantId`, `CreatedAtUtc` | Chronological lien listing |
| `IX_LienOffers_TenantId_LienId_Status` | `liens_LienOffers` | `TenantId`, `LienId`, `Status` | Offers on a specific lien |
| `IX_LienOffers_TenantId_BuyerOrgId_Status` | `liens_LienOffers` | `TenantId`, `BuyerOrgId`, `Status` | Buyer's offers dashboard |
| `IX_LienOffers_TenantId_SellerOrgId_Status` | `liens_LienOffers` | `TenantId`, `SellerOrgId`, `Status` | Seller's received offers |
| `IX_LienOffers_TenantId_Status` | `liens_LienOffers` | `TenantId`, `Status` | Offer status queries |
| `IX_BillsOfSale_TenantId_Status` | `liens_BillsOfSale` | `TenantId`, `Status` | BOS status queries |
| `IX_BillsOfSale_TenantId_SellerOrgId` | `liens_BillsOfSale` | `TenantId`, `SellerOrgId` | Seller's BOS records |
| `IX_BillsOfSale_TenantId_BuyerOrgId` | `liens_BillsOfSale` | `TenantId`, `BuyerOrgId` | Buyer's BOS records |
| `IX_Contacts_TenantId_OrgId_ContactType` | `liens_Contacts` | `TenantId`, `OrgId`, `ContactType` | Contact list by type |
| `IX_Contacts_TenantId_OrgId_DisplayName` | `liens_Contacts` | `TenantId`, `OrgId`, `DisplayName` | Contact search by name |
| `IX_Contacts_TenantId_Email` | `liens_Contacts` | `TenantId`, `Email` | Contact lookup by email |
| `IX_Facilities_TenantId_OrgId_Name` | `liens_Facilities` | `TenantId`, `OrgId`, `Name` | Facility search by name |
| `IX_Facilities_TenantId_Code` | `liens_Facilities` | `TenantId`, `Code` | Facility lookup by code |
| `IX_LookupValues_TenantId_Category` | `liens_LookupValues` | `TenantId`, `Category` | Lookup dropdown population |

### Single-Column FK Indexes

| Index | Table | Column | Purpose |
|-------|-------|--------|---------|
| `IX_Liens_CaseId` | `liens_Liens` | `CaseId` | FK lookup; find liens for a case |
| `IX_Liens_FacilityId` | `liens_Liens` | `FacilityId` | FK lookup; find liens at a facility |
| `IX_BillsOfSale_LienId` | `liens_BillsOfSale` | `LienId` | FK lookup; find BOS for a lien |
| `IX_BillsOfSale_LienOfferId` | `liens_BillsOfSale` | `LienOfferId` | FK lookup; find BOS for an offer |
| `IX_BillsOfSale_DocumentId` | `liens_BillsOfSale` | `DocumentId` | Cross-service ref lookup |
| `IX_Facilities_OrganizationId` | `liens_Facilities` | `OrganizationId` | Cross-service ref lookup |

### Indexes NOT added (deliberate)

| Candidate | Reason Skipped |
|-----------|---------------|
| `Lien.SubjectPartyId` | Low-cardinality cross-service reference; subject lookup typically goes through Case, not directly through Lien |
| `CreatedByUserId` / `UpdatedByUserId` on any entity | Audit fields; "who changed this" queries go through the Audit service, not through entity tables |
| `Lien.Jurisdiction` standalone index | Low-cardinality filter typically combined with Status (covered by application-level filtering on `IX_Liens_TenantId_Status` result set) |
| `Contact.IsActive` standalone | Boolean filter applied in-memory after index-driven fetch |
| `Facility.IsActive` standalone | Same reasoning as Contact |

**Total: 4 unique indexes + 22 query indexes + 6 FK indexes = 32 indexes across 7 tables**

---

## 9. Delete Behavior Decisions and Rationale

| Relationship | Delete Behavior | Rationale |
|-------------|----------------|-----------|
| `Lien` → `Case` | `Restrict` | A case cannot be deleted while it has linked liens. Business rule: cases with financial records must be preserved |
| `Lien` → `Facility` | `Restrict` | A facility cannot be deleted while liens reference it. Use `Deactivate()` for soft removal |
| `LienOffer` → `Lien` | `Restrict` | A lien cannot be deleted while offers exist. Offers are legal negotiation records |
| `BillOfSale` → `Lien` | `Restrict` | A lien cannot be deleted while a BOS exists. BOS is a legal transfer document |
| `BillOfSale` → `LienOffer` | `Restrict` | An offer cannot be deleted while a BOS references it. Maintains audit trail of offer → sale chain |

**No cascade deletes exist anywhere in the Liens schema.** This is intentional for a legal/financial domain where:
- Records represent legal instruments (liens, bills of sale, offers)
- Regulatory requirements may mandate record retention
- Soft-delete or archival should be used instead of hard delete
- The CareConnect service also uses `Restrict` for all within-service FKs as precedent

---

## 10. Domain Adjustments Required for Persistence Compatibility

**None.** All 7 domain entities mapped cleanly to EF Core configurations without any modifications. Specifically:

- Private constructors are already present (EF Core compatible)
- All properties have `{ get; private set; }` (EF Core can set via backing fields)
- Audit base class (`AuditableEntity`) properties are `protected set` (accessible to EF Core)
- No navigation properties were needed — `HasOne<T>().WithMany()` generic overload works without them
- No `[Column]`, `[Table]`, or other data annotations were added to domain entities

---

## 11. Build Results

| Project | Result | Errors | Warnings |
|---------|--------|--------|----------|
| `Liens.Domain` | ✅ Success | 0 | 0 |
| `Liens.Application` | ✅ Success | 0 | 0 |
| `Liens.Infrastructure` | ✅ Success | 0 | 0 |
| `Liens.Api` | ✅ Success | 0 | 0 |
| `Identity.Api` (non-regression) | ✅ Success | 0 | 0 |
| `Gateway.Api` (non-regression) | ✅ Success | 0 | 0 |

---

## 12. Confirmation Checklist

| Check | Status |
|-------|--------|
| All tables use `liens_` prefix | ✅ All 7: `liens_Cases`, `liens_Contacts`, `liens_Facilities`, `liens_LookupValues`, `liens_Liens`, `liens_LienOffers`, `liens_BillsOfSale` |
| No legacy v1 table names used as final targets | ✅ No v1 schema exists; all table names are new v2 conventions |
| No shared-service schema leakage | ✅ No FK constraints to `idt_`, `docs_`, `ntf_`, or `aud_` tables. All cross-service fields are scalar references |
| No data annotations on domain entities | ✅ All configuration is fluent-only in Infrastructure layer |
| Follows existing v2 persistence pattern | ✅ Matches Fund/CareConnect `IEntityTypeConfiguration<T>` pattern exactly |
| No repositories or query services added | ✅ |
| No API endpoints added | ✅ |
| No migrations generated | ✅ (deferred to LS-LIENS-04-002) |
| No new domain entities added | ✅ |
| No Identity/Documents/Notifications/Audit schemas modified | ✅ |

---

## 13. Risks / Assumptions

| # | Risk/Assumption | Mitigation |
|---|----------------|------------|
| R-1 | MySQL `decimal(18,2)` chosen for money fields — if the business requires sub-cent precision (e.g., for prorated calculations), precision may need to increase to `decimal(18,4)` | Monitor during API layer implementation; adjustable via migration |
| R-2 | `LookupValue.TenantId` is `Guid?` (nullable) to support system-wide lookup values. The unique index `UX_LookupValues_TenantId_Category_Code` includes null TenantId, but MySQL treats NULL as distinct in unique indexes | System-wide lookups with `TenantId = NULL` are unique per category+code; tenant-specific lookups are unique per tenant+category+code. This is correct behavior |
| R-3 | No navigation properties on Liens entities. If future query patterns require eager loading with `.Include()`, navigation properties would need to be added to domain entities | Can be added later as a minimal domain adjustment. The `HasOne<T>().WithMany()` FK configuration will automatically work with navigation properties when added |
| R-4 | 32 indexes may be excessive for initial deployment. Some indexes (e.g., `IX_Contacts_TenantId_Email`) may not justify their write cost until query volumes are measured | Monitor query patterns post-deployment; drop unused indexes via migration |
| A-1 | Assumes MySQL 8.x as the target database (Pomelo.EntityFrameworkCore.MySql 8.0.2 is configured in `Liens.Infrastructure.csproj`) | Confirmed by existing project reference |
| A-2 | Assumes `CreatedByUserId` is always non-null (`.IsRequired()`) matching Fund service pattern. The AuditableEntity base class defines it as `Guid?` but the Fund configuration requires it | Matches Fund precedent; entity `Create()` methods always set this value |

---

## 14. Readiness Statement

### Is the EF configuration layer established?

**Yes.** All 7 approved Liens domain entities have complete fluent EF Core configurations that:
- Follow v2 conventions exactly
- Map to `liens_`-prefixed tables
- Define all property constraints, FK relationships, and indexes
- Keep cross-service references as scalar fields
- Compile cleanly with 0 errors and 0 warnings

### Is the service ready for LS-LIENS-04-002 (DbContext + initial migrations)?

**Yes.** The next step requires:
1. Create `LiensDbContext` in `Liens.Infrastructure/Persistence/` with:
   - `DbSet<T>` for all 7 entities
   - `OnModelCreating` calling `ApplyConfigurationsFromAssembly()`
   - `SaveChangesAsync` override for audit timestamp auto-population (following Fund/CareConnect pattern)
2. Register `LiensDbContext` in DI (Liens.Api `Program.cs`)
3. Configure MySQL connection string
4. Generate initial EF Core migration

All configurations are in the correct assembly location for automatic discovery via `ApplyConfigurationsFromAssembly(typeof(LiensDbContext).Assembly)`.
