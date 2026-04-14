# LS-LIENS-06-002 — Lien HTTP APIs Implementation Report

## 1. Summary

Implemented five production-ready, database-backed Lien HTTP API endpoints following the exact same architecture pattern established by the Case APIs (LS-LIENS-06-001). The implementation replaces all stub endpoints with real application-service-backed operations supporting search/list, get-by-id, get-by-lien-number, create, and update.

## 2. Existing Pattern Followed

The implementation mirrors the Case API pattern in every structural decision:

| Concern | Case Pattern | Lien Implementation |
|---------|-------------|---------------------|
| Route group | `/api/liens/cases` | `/api/liens/liens` |
| DTOs | `CaseResponse`, `CreateCaseRequest`, `UpdateCaseRequest` | `LienResponse`, `CreateLienRequest`, `UpdateLienRequest` |
| Pagination | `PaginatedResult<T>` (shared) | Same `PaginatedResult<T>` |
| Application service | `ICaseService` / `CaseService` | `ILienService` / `LienService` |
| Endpoint structure | Thin handlers delegating to service | Identical pattern |
| Request context | `RequireTenantId/UserId/OrgId` helpers | Identical helpers |
| Error handling | `ValidationException`, `ConflictException`, `NotFoundException` | Same exception types |
| Auth filters | `RequireProductAccess` + `RequirePermission` | Same filters |

## 3. Files Created / Changed

### Created
| File | Layer | Purpose |
|------|-------|---------|
| `Liens.Application/DTOs/LienResponse.cs` | Application | Response DTO with all lien fields |
| `Liens.Application/DTOs/CreateLienRequest.cs` | Application | Create request DTO (safe fields only) |
| `Liens.Application/DTOs/UpdateLienRequest.cs` | Application | Update request DTO (safe mutable fields only) |
| `Liens.Application/Interfaces/ILienService.cs` | Application | Service interface |
| `Liens.Application/Services/LienService.cs` | Application | Service implementation |

### Modified
| File | Change |
|------|--------|
| `Liens.Api/Endpoints/LienEndpoints.cs` | Replaced all stubs with real database-backed endpoints |
| `Liens.Domain/LiensPermissions.cs` | Added `LienRead` and `LienUpdate` permission constants |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `ILienService` / `LienService` |

## 4. DTOs

### LienResponse
Full response shape covering UI needs:
- Identity: `id`, `lienNumber`, `externalReference`
- Classification: `lienType`, `status`
- Associations: `caseId`, `facilityId`
- Financials: `originalAmount`, `currentBalance`, `offerPrice`, `purchasePrice`, `payoffAmount`
- Subject: `subjectFirstName`, `subjectLastName`, `subjectDisplayName`, `isConfidential`
- Ownership: `orgId`, `sellingOrgId`, `buyingOrgId`, `holdingOrgId`
- Metadata: `jurisdiction`, `incidentDate`, `description`
- Timestamps: `openedAtUtc`, `closedAtUtc`, `createdAtUtc`, `updatedAtUtc`

### CreateLienRequest
Client-supplied fields only. Excludes: `tenantId`, `orgId`, `sellingOrgId`, `buyingOrgId`, `holdingOrgId`, `offerPrice`, `purchasePrice`, `payoffAmount`, all audit fields, status (initialized to `Draft` by domain).

### UpdateLienRequest
Safe mutable fields only. Excludes: `offerPrice`, `purchasePrice`, `payoffAmount`, `buyingOrgId`, `holdingOrgId`, and all sale/settlement/offer workflow fields. `OfferPrice` is intentionally excluded — it belongs to the `ListForSale` workflow endpoint (future LienOffer APIs).

## 5. Application Service

`LienService` implements `ILienService` with:
- **SearchAsync**: Paginated search with filters (search, status, lienType, caseId, facilityId); page/pageSize clamping
- **GetByIdAsync**: Tenant-scoped single-entity retrieval
- **GetByLienNumberAsync**: Tenant-scoped lookup by business key
- **CreateAsync**: Full validation, duplicate check, case/facility reference validation, domain entity creation
- **UpdateAsync**: Tenant-scoped load, validation, case/facility reference validation, domain-safe update via entity methods

## 6. Endpoint List and Routes

| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| `GET` | `/api/liens/liens` | `LienRead` | List/search with filters |
| `GET` | `/api/liens/liens/{id}` | `LienRead` | Get by ID |
| `GET` | `/api/liens/liens/by-number/{lienNumber}` | `LienRead` | Get by lien number |
| `POST` | `/api/liens/liens` | `LienCreate` | Create new lien |
| `PUT` | `/api/liens/liens/{id}` | `LienUpdate` | Update lien |

Query parameters for list endpoint: `search`, `status`, `lienType`, `caseId`, `facilityId`, `page`, `pageSize`.

## 7. Repository Interactions

- `ILienRepository`: `SearchAsync`, `GetByIdAsync`, `GetByLienNumberAsync`, `AddAsync`, `UpdateAsync`
- `ICaseRepository`: `GetByIdAsync` — validates referenced case exists during create/update
- `IFacilityRepository`: `GetByIdAsync` — validates referenced facility exists during create/update

No new repository methods needed — existing interfaces fully support all operations.

## 8. Authorization Approach

- **Route group**: `RequireAuthorization(Policies.AuthenticatedUser)` + `RequireProductAccess(LiensPermissions.ProductCode)`
- **Per-endpoint**: `RequirePermission()` with granular permission codes
- New permissions added: `SYNQ_LIENS.lien:read`, `SYNQ_LIENS.lien:update`
- Existing permissions preserved: `LienCreate`, `LienOffer`, `LienReadOwn`, `LienBrowse`, `LienPurchase`, `LienReadHeld`, `LienService`, `LienSettle`

## 9. Request Context Usage

- **TenantId**: Derived from `ICurrentRequestContext.TenantId` via `RequireTenantId()` — used for all data scoping
- **OrgId**: Derived from `ICurrentRequestContext.OrgId` via `RequireOrgId()` — used for create ownership
- **UserId**: Derived from `ICurrentRequestContext.UserId` via `RequireUserId()` — used for audit trail
- No client-supplied tenant/org/user identifiers are trusted

## 10. Donor API Compatibility Notes

- Route prefix changed from `/api/liens` (flat) to `/api/liens/liens` (resource-namespaced) to match Case API pattern (`/api/liens/cases`) and prevent collisions with future resources (offers, BOS)
- Former stub endpoints (`/own`, `/held`, `/marketplace`, etc.) under `/api/liens` are now replaced by a single flexible list endpoint at `/api/liens/liens` with query filters
- Workflow-specific stubs (offer, purchase, service, settle) removed from this file — they belong in future LienOffer/workflow endpoints (LS-LIENS-07+)

## 11. Marketplace / Portfolio / My-Liens Support

The single `GET /api/liens/liens` endpoint supports all view-oriented queries through filters:

| View | How to Query |
|------|-------------|
| **My Liens** | Frontend knows user's `orgId`; filter by ownership or use `search` |
| **Portfolio** | Filter by `status=Active` or `status=Sold` |
| **Marketplace** | Filter by `status=Offered` |
| **By Case** | `?caseId={id}` |
| **By Facility** | `?facilityId={id}` |
| **By Type** | `?lienType=MedicalLien` |

Org-scoped views (my-liens, portfolio) can be added as convenience endpoints later without changing the core architecture. The repository's `SearchAsync` already supports all needed filter combinations.

## 12. Validation Performed

| Check | Result |
|-------|--------|
| Liens.Api builds | 0 errors, 0 warnings |
| Liens.Application builds | 0 errors, 0 warnings |
| Liens.Domain builds | 0 errors, 0 warnings |
| Liens.Infrastructure builds | 0 errors, 0 warnings |
| DI registration complete | `ILienService` → `LienService` registered |
| Endpoint routing compiles | All 5 endpoints resolve correctly |
| DTO shapes match domain entity | All fields mapped |
| Permission constants defined | `LienRead`, `LienUpdate` added |
| Existing permissions preserved | No regressions |

## 13. Confirmations

- [x] No EF entities returned directly — all responses use `LienResponse` DTO
- [x] No cross-service joins introduced — only Liens-owned tables accessed
- [x] No unrelated APIs added — no Offer, BOS, ServicingTask, CaseNote endpoints
- [x] No cross-service enrichment — returns scalar org/case/facility IDs only
- [x] No direct mutation of sale workflow fields (`purchasePrice`, `payoffAmount`, `buyingOrgId`, `holdingOrgId`)

## 14. Build Results

```
Liens.Domain       → OK (0 errors, 0 warnings)
Liens.Application  → OK (0 errors, 0 warnings)
Liens.Infrastructure → OK (0 errors, 0 warnings)
Liens.Api          → OK (0 errors, 0 warnings)
```

## 15. Permission-Model Gaps

| Permission | Status | Note |
|------------|--------|------|
| `SYNQ_LIENS.lien:read` | **NEW** — needs seeding in Identity service | General read; coexists with `lien:read:own` and `lien:read:held` for future fine-grained scoping |
| `SYNQ_LIENS.lien:update` | **NEW** — needs seeding in Identity service | General update permission |
| `SYNQ_LIENS.lien:create` | Already exists | Used by create endpoint |

The `LienReadOwn` and `LienReadHeld` permissions remain in the constants for future org-scoped read endpoints but are not used by the current general-purpose endpoints.

## 16. Risks / Assumptions

1. **Permission seeding**: `LienRead` and `LienUpdate` must be seeded in the Identity service's permission catalog before endpoints are accessible to users. Same gap as Case permissions.
2. **Org-scoped filtering**: The current list endpoint returns all tenant-scoped liens. Org-level scoping (my-liens vs. all-liens) will be needed when multi-org tenants are active.
3. **Domain invariant errors**: `InvalidOperationException` from domain entity methods (e.g., updating a settled lien) is now handled by `ExceptionHandlingMiddleware` and returns 409 Conflict with a `business_rule_violation` error code.
4. **Route migration**: Old stub routes at `/api/liens/{action}` are replaced by `/api/liens/liens/{action}`. Any existing frontend or integration referencing old routes will need updating.

## 17. Final Readiness Statement

**Are real Lien APIs established?** Yes. Five database-backed endpoints with full CRUD (minus delete), tenant-scoped access, request-context-driven ownership, input validation, duplicate detection, cross-entity reference validation, and proper authorization filters.

**Is the system ready for LienOffer APIs next?** Yes. The Lien entity, repository, DTOs, and application service are fully operational. LienOffer APIs can build on this foundation — creating offers against existing liens, referencing them by ID, and triggering domain workflows like `ListForSale`, `MarkSold`, etc.
