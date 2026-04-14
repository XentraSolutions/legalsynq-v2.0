# LS-LIENS-05-001 — Core Repository Interfaces & EF Core Implementations

**Service:** Liens  
**Date:** 2026-04-14  
**Status:** Complete  
**Depends on:** LS-LIENS-04-002 (LiensDbContext + Initial Migration)

---

## 1. Summary

Implemented 7 repository interfaces in `Liens.Application` and 7 corresponding EF Core implementations in `Liens.Infrastructure` for the Liens microservice. This establishes the complete data-access layer for all domain entities defined in LS-LIENS-03-001 through LS-LIENS-03-003 and persisted by the DbContext from LS-LIENS-04-002. All repositories are registered in the DI container and the full Liens stack builds with 0 errors and 0 warnings.

No new domain entities were introduced. No API endpoints were added. No permission or authorization logic was mixed into the persistence layer. The scope is strictly data-access abstractions and their EF Core implementations.

---

## 2. Existing v2 Data Access Pattern Identified and Followed

The established v2 repository pattern was derived from analysis of **CareConnect** (17 repository pairs) and **Fund** (1 repository pair):

| Aspect | v2 Convention | Liens Implementation |
|---|---|---|
| Interface location | `{Service}.Application/Repositories/I{Entity}Repository.cs` | Followed exactly |
| Implementation location | `{Service}.Infrastructure/Repositories/{Entity}Repository.cs` | Followed exactly |
| DbContext injection | Constructor-injected, private readonly field `_db` | Followed exactly |
| Save semantics | Repository-level `SaveChangesAsync()` per mutating method | Followed exactly |
| Unit of Work | None — no `IUnitOfWork` abstraction | Followed exactly |
| DI lifetime | `AddScoped<IXRepository, XRepository>()` | Followed exactly |
| DI registration site | `Infrastructure/DependencyInjection.cs` | Followed exactly |
| Tenant isolation | All read queries filter by `TenantId == tenantId` | Followed exactly |
| CancellationToken | Optional parameter with `default` on all async methods | Followed exactly |
| Search return type | `Task<(List<T> Items, int TotalCount)>` tuple for paginated results | Followed exactly |
| Pagination | `Skip((page - 1) * pageSize).Take(pageSize)` | Followed exactly |
| Entity references | Domain entities returned directly (no DTO mapping at repository layer) | Followed exactly |

**Reference implementations studied:**
- `CareConnect.Application/Repositories/IProviderRepository.cs` (27 lines, 7 methods)
- `CareConnect.Infrastructure/Repositories/ProviderRepository.cs` (177 lines, full search + CRUD)
- `CareConnect.Infrastructure/DependencyInjection.cs` (107 lines, 17 repository registrations)
- `Fund.Application/IApplicationRepository.cs` + `Fund.Infrastructure/Repositories/ApplicationRepository.cs`

---

## 3. Exact Files Created / Changed

### Created (14 files)

**Interfaces (7):**
| File | Lines |
|---|---|
| `apps/services/liens/Liens.Application/Repositories/ICaseRepository.cs` | 12 |
| `apps/services/liens/Liens.Application/Repositories/IContactRepository.cs` | 12 |
| `apps/services/liens/Liens.Application/Repositories/IFacilityRepository.cs` | 12 |
| `apps/services/liens/Liens.Application/Repositories/ILookupValueRepository.cs` | 13 |
| `apps/services/liens/Liens.Application/Repositories/ILienRepository.cs` | 15 |
| `apps/services/liens/Liens.Application/Repositories/ILienOfferRepository.cs` | 13 |
| `apps/services/liens/Liens.Application/Repositories/IBillOfSaleRepository.cs` | 14 |

**Implementations (7):**
| File | Lines |
|---|---|
| `apps/services/liens/Liens.Infrastructure/Repositories/CaseRepository.cs` | 72 |
| `apps/services/liens/Liens.Infrastructure/Repositories/ContactRepository.cs` | 69 |
| `apps/services/liens/Liens.Infrastructure/Repositories/FacilityRepository.cs` | 64 |
| `apps/services/liens/Liens.Infrastructure/Repositories/LookupValueRepository.cs` | 51 |
| `apps/services/liens/Liens.Infrastructure/Repositories/LienRepository.cs` | 98 |
| `apps/services/liens/Liens.Infrastructure/Repositories/LienOfferRepository.cs` | 66 |
| `apps/services/liens/Liens.Infrastructure/Repositories/BillOfSaleRepository.cs` | 73 |

### Changed (1 file)

| File | Change |
|---|---|
| `apps/services/liens/Liens.Infrastructure/DependencyInjection.cs` | Added `using Liens.Application.Repositories;` and `using Liens.Infrastructure.Repositories;` imports. Added 7 `AddScoped` registrations for all repository interface→implementation pairs. |

### Unchanged

- `Liens.Domain/` — No entities, enums, or value objects added or modified.
- `Liens.Api/` — No endpoints, middleware, or startup changes.
- `Liens.Application/Liens.Application.csproj` — No new package references needed (already references `Liens.Domain`).
- `Liens.Infrastructure/Liens.Infrastructure.csproj` — No new package references needed (already references `Liens.Application`, `Pomelo.EntityFrameworkCore.MySql`, `BuildingBlocks`).

---

## 4. Repository / Query Abstractions Added

All 7 interfaces follow a consistent contract shape:

- **Single-entity retrieval** — `GetByIdAsync(Guid tenantId, Guid id)` returns `T?`
- **Business-key retrieval** — Where applicable: `GetByCaseNumberAsync`, `GetByLienNumberAsync`, `GetByCodeAsync`
- **Paginated search** — `SearchAsync(...)` returns `(List<T> Items, int TotalCount)`
- **Relationship traversal** — `GetByCaseIdAsync`, `GetByFacilityIdAsync`, `GetByLienIdAsync`, `GetByLienOfferIdAsync`
- **Write operations** — `AddAsync(T entity)` and `UpdateAsync(T entity)`

No `DeleteAsync` methods were added. Liens domain entities use soft-state transitions (status-based lifecycle) rather than hard deletes, consistent with the v2 pattern.

---

## 5. Where Interfaces Live

```
apps/services/liens/Liens.Application/Repositories/
├── ICaseRepository.cs
├── IContactRepository.cs
├── IFacilityRepository.cs
├── ILookupValueRepository.cs
├── ILienRepository.cs
├── ILienOfferRepository.cs
└── IBillOfSaleRepository.cs
```

**Namespace:** `Liens.Application.Repositories`

All interfaces depend only on `Liens.Domain.Entities` — no infrastructure, EF Core, or framework references leak into the Application layer.

---

## 6. Where Implementations Live

```
apps/services/liens/Liens.Infrastructure/Repositories/
├── CaseRepository.cs
├── ContactRepository.cs
├── FacilityRepository.cs
├── LookupValueRepository.cs
├── LienRepository.cs
├── LienOfferRepository.cs
└── BillOfSaleRepository.cs
```

**Namespace:** `Liens.Infrastructure.Repositories`

All implementations depend on:
- `Liens.Application.Repositories` (interface contract)
- `Liens.Domain.Entities` (entity types)
- `Liens.Infrastructure.Persistence` (`LiensDbContext`)
- `Microsoft.EntityFrameworkCore` (LINQ extensions, `FirstOrDefaultAsync`, `ToListAsync`, etc.)

---

## 7. Method Inventory by Entity

### ICaseRepository / CaseRepository

| Method | Signature | Tenant-Scoped | Notes |
|---|---|---|---|
| `GetByIdAsync` | `(Guid tenantId, Guid id, CancellationToken)` → `Case?` | Yes | |
| `GetByCaseNumberAsync` | `(Guid tenantId, string caseNumber, CancellationToken)` → `Case?` | Yes | Business-key lookup for duplicate detection |
| `SearchAsync` | `(Guid tenantId, string? search, string? status, int page, int pageSize, CancellationToken)` → `(List<Case>, int)` | Yes | Searches: CaseNumber, ClientFirstName, ClientLastName, Title. Filters: Status. Order: CreatedAtUtc DESC |
| `AddAsync` | `(Case entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |
| `UpdateAsync` | `(Case entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |

### IContactRepository / ContactRepository

| Method | Signature | Tenant-Scoped | Notes |
|---|---|---|---|
| `GetByIdAsync` | `(Guid tenantId, Guid id, CancellationToken)` → `Contact?` | Yes | |
| `SearchAsync` | `(Guid tenantId, string? search, string? contactType, bool? isActive, int page, int pageSize, CancellationToken)` → `(List<Contact>, int)` | Yes | Searches: FirstName, LastName, DisplayName, Email, Organization. Filters: ContactType, IsActive. Order: DisplayName ASC |
| `AddAsync` | `(Contact entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |
| `UpdateAsync` | `(Contact entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |

### IFacilityRepository / FacilityRepository

| Method | Signature | Tenant-Scoped | Notes |
|---|---|---|---|
| `GetByIdAsync` | `(Guid tenantId, Guid id, CancellationToken)` → `Facility?` | Yes | |
| `SearchAsync` | `(Guid tenantId, string? search, bool? isActive, int page, int pageSize, CancellationToken)` → `(List<Facility>, int)` | Yes | Searches: Name, Code, City. Filters: IsActive. Order: Name ASC |
| `AddAsync` | `(Facility entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |
| `UpdateAsync` | `(Facility entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |

### ILookupValueRepository / LookupValueRepository

| Method | Signature | Tenant-Scoped | Notes |
|---|---|---|---|
| `GetByIdAsync` | `(Guid? tenantId, Guid id, CancellationToken)` → `LookupValue?` | Yes (with system fallback) | Filter: `TenantId == null OR TenantId == tenantId`. Nullable tenantId because LookupValue.TenantId is nullable (null = system-wide) |
| `GetByCategoryAsync` | `(Guid? tenantId, string category, CancellationToken)` → `List<LookupValue>` | Yes (with system fallback) | Returns active values only. Order: SortOrder ASC, Name ASC |
| `GetByCodeAsync` | `(Guid? tenantId, string category, string code, CancellationToken)` → `LookupValue?` | Yes (with system fallback) | Returns active values only. Resolves unique (TenantId, Category, Code) triple |
| `AddAsync` | `(LookupValue entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |
| `UpdateAsync` | `(LookupValue entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |

### ILienRepository / LienRepository

| Method | Signature | Tenant-Scoped | Notes |
|---|---|---|---|
| `GetByIdAsync` | `(Guid tenantId, Guid id, CancellationToken)` → `Lien?` | Yes | |
| `GetByLienNumberAsync` | `(Guid tenantId, string lienNumber, CancellationToken)` → `Lien?` | Yes | Business-key lookup for duplicate detection |
| `SearchAsync` | `(Guid tenantId, string? search, string? status, string? lienType, Guid? caseId, Guid? facilityId, int page, int pageSize, CancellationToken)` → `(List<Lien>, int)` | Yes | Searches: LienNumber, SubjectFirstName, SubjectLastName, Description. Filters: Status, LienType, CaseId, FacilityId. Order: CreatedAtUtc DESC |
| `GetByCaseIdAsync` | `(Guid tenantId, Guid caseId, CancellationToken)` → `List<Lien>` | Yes | Relationship traversal: all liens attached to a case. Order: CreatedAtUtc DESC |
| `GetByFacilityIdAsync` | `(Guid tenantId, Guid facilityId, CancellationToken)` → `List<Lien>` | Yes | Relationship traversal: all liens for a facility. Order: CreatedAtUtc DESC |
| `AddAsync` | `(Lien entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |
| `UpdateAsync` | `(Lien entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |

### ILienOfferRepository / LienOfferRepository

| Method | Signature | Tenant-Scoped | Notes |
|---|---|---|---|
| `GetByIdAsync` | `(Guid tenantId, Guid id, CancellationToken)` → `LienOffer?` | Yes | |
| `GetByLienIdAsync` | `(Guid tenantId, Guid lienId, CancellationToken)` → `List<LienOffer>` | Yes | All offers for a lien. Order: OfferedAtUtc DESC |
| `SearchAsync` | `(Guid tenantId, Guid? lienId, string? status, int page, int pageSize, CancellationToken)` → `(List<LienOffer>, int)` | Yes | Filters: LienId, Status. Order: OfferedAtUtc DESC |
| `AddAsync` | `(LienOffer entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |
| `UpdateAsync` | `(LienOffer entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |

### IBillOfSaleRepository / BillOfSaleRepository

| Method | Signature | Tenant-Scoped | Notes |
|---|---|---|---|
| `GetByIdAsync` | `(Guid tenantId, Guid id, CancellationToken)` → `BillOfSale?` | Yes | |
| `GetByLienOfferIdAsync` | `(Guid tenantId, Guid lienOfferId, CancellationToken)` → `BillOfSale?` | Yes | Unique lookup: one BOS per accepted offer |
| `GetByLienIdAsync` | `(Guid tenantId, Guid lienId, CancellationToken)` → `List<BillOfSale>` | Yes | All BOS documents for a lien. Order: IssuedAtUtc DESC |
| `SearchAsync` | `(Guid tenantId, Guid? lienId, string? status, int page, int pageSize, CancellationToken)` → `(List<BillOfSale>, int)` | Yes | Filters: LienId, Status. Order: IssuedAtUtc DESC |
| `AddAsync` | `(BillOfSale entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |
| `UpdateAsync` | `(BillOfSale entity, CancellationToken)` → `Task` | N/A | Calls `SaveChangesAsync` |

**Total: 7 interfaces, 7 implementations, 37 methods.**

---

## 8. Supported Workflow Query Patterns

The repository layer supports the following Liens domain workflows through its query surface:

| Workflow | Repository Methods Used | Pattern |
|---|---|---|
| **Case management** | `CaseRepo.SearchAsync` (list view), `GetByIdAsync` (detail), `GetByCaseNumberAsync` (duplicate check), `LienRepo.GetByCaseIdAsync` (case→liens drilldown) | Paginated list → detail → child aggregates |
| **Lien lifecycle (create → list → offer → sell → settle)** | `LienRepo.SearchAsync` (list/marketplace), `GetByIdAsync` (detail), `GetByLienNumberAsync` (duplicate check), `AddAsync` (create), `UpdateAsync` (status transitions via domain methods) | CRUD + status machine |
| **Offer negotiation** | `LienOfferRepo.GetByLienIdAsync` (offers on a lien), `SearchAsync` (offer dashboard), `GetByIdAsync` (respond to specific offer), `AddAsync` (place offer), `UpdateAsync` (accept/reject/withdraw) | Parent→child traversal + status updates |
| **Bill of Sale execution** | `BillOfSaleRepo.GetByLienOfferIdAsync` (find BOS for accepted offer — enforces 1:1), `GetByLienIdAsync` (all BOS for a lien), `SearchAsync` (BOS dashboard), `AddAsync` (create from accepted offer), `UpdateAsync` (submit/execute/cancel) | Offer→BOS 1:1 link + status machine |
| **Facility lookup** | `FacilityRepo.SearchAsync` (picker/autocomplete), `GetByIdAsync` (detail), `LienRepo.GetByFacilityIdAsync` (facility→liens drilldown) | Reference data + relationship traversal |
| **Contact directory** | `ContactRepo.SearchAsync` (filterable directory), `GetByIdAsync` (detail) | Filtered paginated list |
| **Lookup value resolution** | `LookupValueRepo.GetByCategoryAsync` (dropdown population), `GetByCodeAsync` (single value resolution), `GetByIdAsync` (admin edit) | System-wide + tenant-overlay hierarchy |

---

## 9. Save / Persistence Coordination Model

**Model: Repository-Level Save (no Unit of Work)**

Every write method (`AddAsync`, `UpdateAsync`) calls `_db.SaveChangesAsync(ct)` immediately after the EF Core operation. This matches the v2 convention established by CareConnect and Fund.

```
AddAsync:    _db.{DbSet}.AddAsync(entity, ct) → _db.SaveChangesAsync(ct)
UpdateAsync: _db.{DbSet}.Update(entity)       → _db.SaveChangesAsync(ct)
```

**Implications:**
- Each repository operation is an atomic database transaction.
- Multi-entity workflows (e.g., accept offer → create BOS → update lien status) will require the Application service layer to orchestrate multiple repository calls. Each call commits independently.
- If cross-repository atomicity is needed in the future, the service layer can wrap multiple calls in an explicit `DbContext` transaction. The current repository signatures do not preclude this — `LiensDbContext` is scoped per request, so all repositories in a single request share the same context instance.
- `LiensDbContext.SaveChangesAsync` includes the `AuditableEntity` interceptor that auto-stamps `CreatedAtUtc`/`UpdatedAtUtc` on tracked entities.

---

## 10. DI Registration Changes

**File:** `apps/services/liens/Liens.Infrastructure/DependencyInjection.cs`

**Added imports:**
```csharp
using Liens.Application.Repositories;
using Liens.Infrastructure.Repositories;
```

**Added registrations (7):**
```csharp
services.AddScoped<ICaseRepository, CaseRepository>();
services.AddScoped<IContactRepository, ContactRepository>();
services.AddScoped<IFacilityRepository, FacilityRepository>();
services.AddScoped<ILookupValueRepository, LookupValueRepository>();
services.AddScoped<ILienRepository, LienRepository>();
services.AddScoped<ILienOfferRepository, LienOfferRepository>();
services.AddScoped<IBillOfSaleRepository, BillOfSaleRepository>();
```

**Lifetime:** All `Scoped` — one instance per HTTP request, matching the EF Core `DbContext` lifetime. This ensures all repositories in a request share the same `LiensDbContext` instance and its change tracker.

**Pre-existing registrations (unchanged):**
- `LiensDbContext` — `AddDbContext` (Scoped, MySQL via Pomelo)
- `IHttpContextAccessor` — `AddHttpContextAccessor` (Singleton)
- `ICurrentRequestContext` → `CurrentRequestContext` — `AddScoped`

---

## 11. Validation Performed

### Compilation
- `dotnet build` for all 4 Liens projects: **0 errors, 0 warnings** (see Section 13).

### Architecture Review
An automated code review was executed post-implementation. Findings and resolutions:

| Finding | Severity | Resolution |
|---|---|---|
| `LookupValueRepository.GetByIdAsync` was not tenant-scoped (queried by `Id` only) | Critical (broken access control) | Fixed: signature changed to `GetByIdAsync(Guid? tenantId, Guid id)`, implementation now filters `(TenantId == null \|\| TenantId == tenantId)` |
| `LookupValueRepository.GetByCodeAsync` did not filter by `IsActive` (inconsistent with `GetByCategoryAsync`) | Medium (semantic inconsistency) | Fixed: added `&& lv.IsActive` to the WHERE clause |
| No unit-of-work for multi-step workflows | Informational | By design — matches v2 convention. Service layer can wrap in explicit transaction if needed. |

### Pattern Conformance
- All 7 implementations follow identical constructor injection pattern: `private readonly LiensDbContext _db;`
- All paginated search methods use `CountAsync` before `Skip/Take` (count reflects pre-pagination total)
- All search methods apply tenant filter as the base query before any optional filters
- No `AsNoTracking()` used — entities may be mutated after retrieval (consistent with CareConnect pattern)

---

## 12. Confirmation

### No cross-service joins introduced
- All queries reference only `LiensDbContext` DbSets (`Cases`, `Contacts`, `Facilities`, `LookupValues`, `Liens`, `LienOffers`, `BillsOfSale`).
- No references to Identity, CareConnect, Fund, Audit, Documents, or Notifications DbContexts.
- Organization resolution (`SellingOrgId`, `BuyingOrgId`, `HoldingOrgId`) is deferred to the service/API layer via cross-service HTTP calls — not database joins. This matches the CareConnect pattern where `OrganizationId` is resolved via `HttpIdentityOrganizationService`.

### No permission / capability logic mixed into persistence
- Repository methods accept raw parameter values (tenantId, status, search terms). They do not check `LiensPermissions`, `LiensCapabilities`, `ICurrentRequestContext.Roles`, or any authorization policies.
- Permission enforcement is the responsibility of the API endpoint filters (`RequireProductAccess`, `RequirePermission`) and future Application service layer methods.
- The repositories are pure data-access — they translate queries to EF Core LINQ and persist entity state.

### No new domain entities added
- All 7 entities (`Case`, `Contact`, `Facility`, `LookupValue`, `Lien`, `LienOffer`, `BillOfSale`) were defined in LS-LIENS-03-001 through LS-LIENS-03-003.
- All 7 DbSets were mapped in LS-LIENS-04-002 (`LiensDbContext`).
- This task created only Application-layer interfaces and Infrastructure-layer implementations. Zero changes to `Liens.Domain/`.

---

## 13. Build Results

### Liens Service (all 4 projects)

```
Project                    Errors  Warnings
─────────────────────────  ──────  ────────
Liens.Domain               0       0
Liens.Application           0       0
Liens.Infrastructure        0       0
Liens.Api                   0       0
```

All projects build successfully with `dotnet build --nologo -v q`.

### Relevant Workspace Context

- No solution-level build performed (monorepo has 30+ projects). The Liens service is self-contained via its project references:
  - `Liens.Api` → `Liens.Infrastructure` → `Liens.Application` → `Liens.Domain`
  - `Liens.Infrastructure` → `BuildingBlocks` (shared library)
- Gateway and Identity (the two other services that interact with Liens at runtime) were not rebuilt — no changes were made to those projects.

---

## 14. Risks / Assumptions

| Risk | Severity | Mitigation |
|---|---|---|
| **No integration tests** — Repository queries are untested against a real MySQL instance | Medium | EF Core LINQ-to-SQL translation for the patterns used (Where, Contains, OrderBy, Skip, Take, Count) is well-established. Real integration tests should be added in LS-LIENS-06 or a dedicated test task. |
| **`Contains()` string matching** — MySQL `LIKE '%term%'` cannot use indexes | Low | Acceptable for v1 with expected data volumes. Full-text search or indexed prefix matching can be added later if performance requires it. |
| **No `AsNoTracking()` on read-only queries** — Slight memory overhead for tracked entities | Low | Matches v2 convention. Entities fetched for display-only will be GC'd at request end. Can be optimized per-query if profiling shows issues. |
| **Repository-level save on multi-step workflows** — `Accept offer → Create BOS → Update lien` is not atomic | Medium | By design (v2 convention). Service layer can wrap in `_db.Database.BeginTransactionAsync()` for atomicity. All repositories share the same scoped `LiensDbContext`. |
| **LookupValue tenant resolution** — `(TenantId == null \|\| TenantId == tenantId)` returns system values for all tenants | Low | Intentional. System-wide lookups (`TenantId = null`) are shared reference data. Tenant-specific overrides take precedence at the Application layer. |
| **No `DeleteAsync` methods** — Entities cannot be hard-deleted | Low | By design. Liens domain uses status-based lifecycle (Draft → Cancelled, etc.) and soft deactivation (`IsActive = false`). Hard deletes are not part of the domain model. |

---

## 15. Final Readiness Statement

### Is the core repository / query layer established?

**Yes.** All 7 domain entities have complete data-access abstractions covering:
- Single-entity retrieval by primary key and business keys
- Paginated, filterable search with tenant isolation
- Relationship traversal across the entity graph (Case → Liens → Offers → BillOfSale, Facility → Liens)
- Write operations (Add, Update) with immediate persistence
- LookupValue resolution with system-wide + tenant-specific hierarchy

The layer is architecturally clean: interfaces in Application, implementations in Infrastructure, no domain contamination, no cross-service coupling, no authorization leakage.

### Is the service ready for the next feature?

**Yes.** The next natural steps are:

1. **LS-LIENS-06: Application Services** — Service classes in `Liens.Application/Services/` that orchestrate repository calls, enforce business rules, and map to DTOs. These will inject `I{Entity}Repository` interfaces.
2. **LS-LIENS-07: API Endpoints** — Replace the current stub endpoints in `Liens.Api/Endpoints/LienEndpoints.cs` with real CRUD endpoints backed by Application services.
3. **Integration Tests** — Repository-level tests against a MySQL test database to validate query correctness and tenant isolation.

The repository layer provides the complete persistence foundation for all of these.
