# LS-LIENS-04-002: LiensDbContext Wiring + Initial Migration

**Status:** COMPLETE  
**Date:** 2026-04-14  
**Predecessor:** LS-LIENS-04-001 (EF Core entity configurations)

---

## 1. Summary

Created the `LiensDbContext`, wired it into the Liens service DI container, added a design-time factory for EF CLI tooling, and generated the initial EF Core migration. The migration produces exactly the 7 approved `liens_`-prefixed tables with all FK constraints, indexes, and column types matching LS-LIENS-04-001.

---

## 2. Existing v2 DbContext Pattern Identified

Inspected all four existing v2 services:

| Service | DbContext Location | SaveChangesAsync Override | DI Pattern | Design-Time Factory |
|---|---|---|---|---|
| Fund | `Fund.Infrastructure/Data/FundDbContext.cs` | Yes (AuditableEntity timestamps) | `AddDbContext<T>` via `AddInfrastructure()` extension | `Fund.Api/DesignTimeDbContextFactory.cs` |
| CareConnect | `CareConnect.Infrastructure/Data/CareConnectDbContext.cs` | Yes (identical pattern) | Same | `CareConnect.Api/DesignTimeDbContextFactory.cs` |
| Identity | `Identity.Infrastructure/Data/IdentityDbContext.cs` | No override | Same | `Identity.Api/DesignTimeDbContextFactory.cs` |
| Audit | `audit/Data/AuditEventDbContext.cs` | No override (append-only) | `AddDbContextFactory<T>` | `audit/Data/DesignTimeDbContextFactory.cs` |

**Pattern followed:** Fund/CareConnect pattern — `AddDbContext<T>`, `SaveChangesAsync` override for `AuditableEntity` timestamps, `ApplyConfigurationsFromAssembly()`, design-time factory in Api project. Liens uses `Persistence/` folder (established in LS-LIENS-04-001) instead of `Data/`.

---

## 3. Files Created/Changed

### Created
| File | Purpose |
|---|---|
| `Liens.Infrastructure/Persistence/LiensDbContext.cs` | DbContext with 7 DbSets, OnModelCreating, SaveChangesAsync audit |
| `Liens.Api/DesignTimeDbContextFactory.cs` | Design-time factory for EF CLI migration tooling |
| `Liens.Infrastructure/Persistence/Migrations/20260414041807_InitialCreate.cs` | Initial migration (Up/Down) |
| `Liens.Infrastructure/Persistence/Migrations/20260414041807_InitialCreate.Designer.cs` | Migration metadata |
| `Liens.Infrastructure/Persistence/Migrations/LiensDbContextModelSnapshot.cs` | Model snapshot for incremental migrations |

### Modified
| File | Change |
|---|---|
| `Liens.Infrastructure/DependencyInjection.cs` | Added `LiensDbContext` registration with MySQL/Pomelo |
| `Liens.Api/Program.cs` | Added `using` directives + dev auto-migration block |
| `Liens.Api/appsettings.json` | Set placeholder connection string for `LiensDb` |

---

## 4. LiensDbContext Location and DbSets

**Location:** `apps/services/liens/Liens.Infrastructure/Persistence/LiensDbContext.cs`  
**Namespace:** `Liens.Infrastructure.Persistence`

| DbSet Property | Entity | Table |
|---|---|---|
| `Cases` | `Case` | `liens_Cases` |
| `Contacts` | `Contact` | `liens_Contacts` |
| `Facilities` | `Facility` | `liens_Facilities` |
| `LookupValues` | `LookupValue` | `liens_LookupValues` |
| `Liens` | `Lien` | `liens_Liens` |
| `LienOffers` | `LienOffer` | `liens_LienOffers` |
| `BillsOfSale` | `BillOfSale` | `liens_BillsOfSale` |

---

## 5. How Configurations Are Applied

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(LiensDbContext).Assembly);
    base.OnModelCreating(modelBuilder);
}
```

All 7 `IEntityTypeConfiguration<T>` classes in `Liens.Infrastructure.Persistence.Configurations` are discovered via assembly scanning. No manual registration needed.

---

## 6. Audit Timestamp Handling

**Added:** Yes — identical to Fund/CareConnect pattern.

The `SaveChangesAsync` override intercepts `ChangeTracker.Entries<AuditableEntity>()`:
- **Added entities:** Sets `CreatedAtUtc` (if default) and `UpdatedAtUtc` to `DateTime.UtcNow`
- **Modified entities:** Sets `UpdatedAtUtc` to `DateTime.UtcNow`

All 7 Liens entities extend `BuildingBlocks.Domain.AuditableEntity`, so all benefit from this automatic stamping.

---

## 7. DI Registration

**File:** `Liens.Infrastructure/DependencyInjection.cs`  
**Method:** `AddLiensServices(IServiceCollection, IConfiguration)`

```csharp
var connectionString = configuration.GetConnectionString("LiensDb")
    ?? throw new InvalidOperationException("Connection string 'LiensDb' is not configured.");

services.AddDbContext<LiensDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0))));
```

Called from `Program.cs` via `builder.Services.AddLiensServices(builder.Configuration)`.

---

## 8. Connection String / Config Changes

**File:** `Liens.Api/appsettings.json`

```json
"ConnectionStrings": {
    "LiensDb": "server=localhost;port=3306;database=liens_db;user=root;password=REPLACE_VIA_SECRET"
}
```

Placeholder connection string — actual credentials must be injected via secrets/environment at runtime. Follows the same pattern as Fund (`FundDb`).

---

## 9. Design-Time Factory

**Added:** Yes  
**Location:** `Liens.Api/DesignTimeDbContextFactory.cs`  
**Pattern:** Identical to `Fund.Api/DesignTimeDbContextFactory.cs`

Reads `appsettings.json` + `appsettings.Development.json`, builds `LiensDbContext` with Pomelo MySQL. Required for `dotnet ef migrations add/remove` commands.

---

## 10. Migration Name and Location

- **Migration Name:** `InitialCreate`
- **Timestamp:** `20260414041807`
- **Location:** `Liens.Infrastructure/Persistence/Migrations/`
- **Files:**
  - `20260414041807_InitialCreate.cs` (Up/Down methods)
  - `20260414041807_InitialCreate.Designer.cs` (metadata)
  - `LiensDbContextModelSnapshot.cs` (model snapshot)

---

## 11. Generated Table List

| # | Table Name | Dependent On |
|---|---|---|
| 1 | `liens_Cases` | — |
| 2 | `liens_Contacts` | — |
| 3 | `liens_Facilities` | — |
| 4 | `liens_LookupValues` | — |
| 5 | `liens_Liens` | `liens_Cases`, `liens_Facilities` |
| 6 | `liens_LienOffers` | `liens_Liens` |
| 7 | `liens_BillsOfSale` | `liens_Liens`, `liens_LienOffers` |

---

## 12. Generated FK Summary

| FK Name | From → To | Delete |
|---|---|---|
| `FK_liens_Liens_liens_Cases_CaseId` | Lien.CaseId → Case.Id | Restrict |
| `FK_liens_Liens_liens_Facilities_FacilityId` | Lien.FacilityId → Facility.Id | Restrict |
| `FK_liens_LienOffers_liens_Liens_LienId` | LienOffer.LienId → Lien.Id | Restrict |
| `FK_liens_BillsOfSale_liens_Liens_LienId` | BillOfSale.LienId → Lien.Id | Restrict |
| `FK_liens_BillsOfSale_liens_LienOffers_LienOfferId` | BillOfSale.LienOfferId → LienOffer.Id | Restrict |

All 5 FKs are within-service only. No cross-service FK constraints.

---

## 13. Generated Index Summary

### Unique Indexes
| Index Name | Table | Columns |
|---|---|---|
| `UX_Cases_TenantId_CaseNumber` | liens_Cases | (TenantId, CaseNumber) |
| `UX_Liens_TenantId_LienNumber` | liens_Liens | (TenantId, LienNumber) |
| `UX_BillsOfSale_TenantId_BillOfSaleNumber` | liens_BillsOfSale | (TenantId, BillOfSaleNumber) |
| `UX_LookupValues_TenantId_Category_Code` | liens_LookupValues | (TenantId, Category, Code) |

### Query Indexes (non-unique)
| Index Name | Table | Columns |
|---|---|---|
| `IX_Cases_TenantId_Status` | liens_Cases | (TenantId, Status) |
| `IX_Cases_TenantId_OrgId_Status` | liens_Cases | (TenantId, OrgId, Status) |
| `IX_Cases_TenantId_OrgId_CreatedAtUtc` | liens_Cases | (TenantId, OrgId, CreatedAtUtc) |
| `IX_Contacts_TenantId_OrgId_ContactType` | liens_Contacts | (TenantId, OrgId, ContactType) |
| `IX_Contacts_TenantId_OrgId_DisplayName` | liens_Contacts | (TenantId, OrgId, DisplayName) |
| `IX_Contacts_TenantId_Email` | liens_Contacts | (TenantId, Email) |
| `IX_Facilities_TenantId_OrgId_Name` | liens_Facilities | (TenantId, OrgId, Name) |
| `IX_Facilities_TenantId_Code` | liens_Facilities | (TenantId, Code) |
| `IX_Facilities_OrganizationId` | liens_Facilities | (OrganizationId) |
| `IX_Liens_TenantId_Status` | liens_Liens | (TenantId, Status) |
| `IX_Liens_TenantId_OrgId_Status` | liens_Liens | (TenantId, OrgId, Status) |
| `IX_Liens_TenantId_LienType` | liens_Liens | (TenantId, LienType) |
| `IX_Liens_TenantId_CreatedAtUtc` | liens_Liens | (TenantId, CreatedAtUtc) |
| `IX_Liens_TenantId_SellingOrgId_Status` | liens_Liens | (TenantId, SellingOrgId, Status) |
| `IX_Liens_TenantId_BuyingOrgId` | liens_Liens | (TenantId, BuyingOrgId) |
| `IX_Liens_TenantId_HoldingOrgId_Status` | liens_Liens | (TenantId, HoldingOrgId, Status) |
| `IX_Liens_CaseId` | liens_Liens | (CaseId) |
| `IX_Liens_FacilityId` | liens_Liens | (FacilityId) |
| `IX_LienOffers_TenantId_Status` | liens_LienOffers | (TenantId, Status) |
| `IX_LienOffers_TenantId_LienId_Status` | liens_LienOffers | (TenantId, LienId, Status) |
| `IX_LienOffers_TenantId_BuyerOrgId_Status` | liens_LienOffers | (TenantId, BuyerOrgId, Status) |
| `IX_LienOffers_TenantId_SellerOrgId_Status` | liens_LienOffers | (TenantId, SellerOrgId, Status) |
| `IX_liens_LienOffers_LienId` | liens_LienOffers | (LienId) — EF auto FK index |
| `IX_BillsOfSale_TenantId_Status` | liens_BillsOfSale | (TenantId, Status) |
| `IX_BillsOfSale_TenantId_SellerOrgId` | liens_BillsOfSale | (TenantId, SellerOrgId) |
| `IX_BillsOfSale_TenantId_BuyerOrgId` | liens_BillsOfSale | (TenantId, BuyerOrgId) |
| `IX_BillsOfSale_LienId` | liens_BillsOfSale | (LienId) |
| `IX_BillsOfSale_LienOfferId` | liens_BillsOfSale | (LienOfferId) |
| `IX_BillsOfSale_DocumentId` | liens_BillsOfSale | (DocumentId) |

---

## 14. Confirmation Checklist

| # | Criterion | Status |
|---|---|---|
| 1 | All tables use `liens_` prefix | ✅ |
| 2 | No legacy v1 table names used | ✅ |
| 3 | No shared-service foreign keys created | ✅ |
| 4 | No extra entities/tables introduced | ✅ |
| 5 | No ServicingTask, CaseNote, or future entities | ✅ |
| 6 | All money columns `decimal(18,2)` | ✅ |
| 7 | DiscountPercent uses `decimal(5,2)` | ✅ |
| 8 | DateOnly → `date`, DateTime → `datetime(6)` | ✅ |
| 9 | All delete behavior is `Restrict` | ✅ |
| 10 | LookupValue.TenantId is nullable | ✅ |

---

## 15. Build Results

| Project | Result |
|---|---|
| `Liens.Api` (all 4 Liens projects) | ✅ 0 errors, 0 warnings |
| `Identity.Api` | ✅ 0 errors, 0 warnings |
| `Gateway.Api` | ✅ 0 errors, 0 warnings |

---

## 16. Deviations / Fixes

- **Folder convention:** Liens uses `Persistence/` instead of `Data/` (established in LS-LIENS-04-001 for entity configurations). DbContext placed in `Persistence/` for consistency. Migrations also under `Persistence/Migrations/`.
- **DI method name:** Liens uses `AddLiensServices()` (pre-existing) instead of `AddInfrastructure()` (Fund/CareConnect pattern). Kept as-is to avoid breaking existing code.
- **Auto-generated FK index:** EF Core generated `IX_liens_LienOffers_LienId` (with `liens_` prefix in index name) in addition to the explicitly named indexes. This is the standard EF behavior for FK columns and is harmless.
- **No deviations from LS-LIENS-04-001 schema:** Migration output matches the approved configuration exactly.

---

## 17. Risks / Assumptions

1. **Connection string:** `LiensDb` uses a placeholder in `appsettings.json`. Must be configured via secrets/env vars before runtime database access works.
2. **Auto-migration in dev:** The `db.Database.Migrate()` call only runs in `Development` environment. Production requires explicit migration application.
3. **MySQL version:** Uses `MySqlServerVersion(8, 0, 0)` — same as all other services. Actual target MySQL must be 8.0+.
4. **No data seeding:** No seed data included. LookupValue seed data is deferred to a future task.

---

## 18. Final Readiness Statement

| Question | Answer |
|---|---|
| Is the DbContext established? | ✅ Yes — `LiensDbContext` created with 7 DbSets, audit timestamps, assembly-scanned configurations |
| Is the initial migration valid? | ✅ Yes — 7 tables, 5 within-service FKs, 4 unique indexes, 29 query indexes, all matching LS-LIENS-04-001 |
| Is the service ready for the next feature? | ✅ Yes — repositories, query services, and API endpoints can now be built on top of this persistence layer |
