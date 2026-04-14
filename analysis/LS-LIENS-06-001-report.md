# LS-LIENS-06-001 — Case HTTP APIs Report

## 1. Summary

Implemented the first real production-ready Liens API surface: five database-backed Case endpoints exposed through the existing v2 gateway and authorization model. These endpoints replace stub behavior with full CRUD operations backed by the existing repository/application architecture.

## 2. Existing Liens/v2 API Pattern Identified and Followed

| Concern | v2 Pattern (CareConnect/Fund/existing Liens) | Applied |
|---|---|---|
| Endpoint style | ASP.NET Core Minimal APIs with static extension methods | ✅ `CaseEndpoints.MapCaseEndpoints()` |
| Route grouping | `app.MapGroup("/api/{service}/{resource}")` | ✅ `/api/liens/cases` |
| Auth at group level | `.RequireAuthorization(Policies.AuthenticatedUser).RequireProductAccess(ProductCode)` | ✅ |
| Auth per endpoint | `.RequirePermission(PermissionCode)` | ✅ |
| Request context | `ICurrentRequestContext` injected into handlers | ✅ |
| DTOs | `Application/DTOs/` namespace, separate request/response classes | ✅ |
| Application services | Interface in `Application/Interfaces/`, impl in `Application/Services/` | ✅ |
| DI registration | `Infrastructure/DependencyInjection.cs` via `AddScoped` | ✅ |
| Endpoint handlers | Thin — delegate to application service, return `IResult` | ✅ |
| Error responses | Middleware maps `ValidationException`→400, `NotFoundException`→404, `ConflictException`→409 | ✅ |

## 3. Exact Files Created/Changed

### Created
| File | Purpose |
|---|---|
| `Liens.Application/DTOs/CaseResponse.cs` | Response DTO for Case |
| `Liens.Application/DTOs/PaginatedResult.cs` | Generic paginated result shape |
| `Liens.Application/DTOs/CreateCaseRequest.cs` | Create request DTO |
| `Liens.Application/DTOs/UpdateCaseRequest.cs` | Update request DTO |
| `Liens.Application/Interfaces/ICaseService.cs` | Application service interface |
| `Liens.Application/Services/CaseService.cs` | Application service implementation |
| `Liens.Api/Endpoints/CaseEndpoints.cs` | Minimal API endpoint definitions |

### Changed
| File | Change |
|---|---|
| `Liens.Domain/LiensPermissions.cs` | Added `CaseRead`, `CaseCreate`, `CaseUpdate` permission codes |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `ICaseService` → `CaseService` |
| `Liens.Api/Program.cs` | Added `app.MapCaseEndpoints()` |

## 4. DTOs Added

### `CaseResponse`
Full read model covering all practical UI fields: `Id`, `CaseNumber`, `ExternalReference`, `Title`, `ClientFirstName`, `ClientLastName`, `ClientDisplayName` (computed), `Status`, `DateOfIncident`, `ClientDob`, `ClientPhone`, `ClientEmail`, `ClientAddress`, `InsuranceCarrier`, `PolicyNumber`, `ClaimNumber`, `DemandAmount`, `SettlementAmount`, `Description`, `Notes`, `OpenedAtUtc`, `ClosedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`.

### `PaginatedResult<T>`
Generic paginated wrapper: `Items`, `Page`, `PageSize`, `TotalCount`.

### `CreateCaseRequest`
Client-supplied fields only: `CaseNumber`, `ClientFirstName`, `ClientLastName`, `ExternalReference`, `Title`, `ClientDob`, `ClientPhone`, `ClientEmail`, `ClientAddress`, `DateOfIncident`, `InsuranceCarrier`, `PolicyNumber`, `ClaimNumber`, `Description`, `Notes`. Excludes: `TenantId`, `OrgId`, `CreatedByUserId`, all audit timestamps.

### `UpdateCaseRequest`
Mutable fields: all from Create plus `Status`, `DemandAmount`, `SettlementAmount`. Excludes: `TenantId`, `OrgId`, `CaseNumber` (immutable after creation), all audit fields.

## 5. Application Services/Handlers Added

### `ICaseService` / `CaseService`

| Method | Purpose |
|---|---|
| `SearchAsync` | Paginated list/search with optional `search` and `status` filters |
| `GetByIdAsync` | Single case by GUID |
| `GetByCaseNumberAsync` | Single case by case number string |
| `CreateAsync` | Validates required fields, checks for duplicate case number, creates via domain factory method |
| `UpdateAsync` | Loads existing case, validates mutable fields, applies updates via domain methods, handles status transitions and financial field updates |

All methods:
- Accept `tenantId` as first parameter (from request context, never client-supplied)
- Use `BuildingBlocks.Exceptions` for error handling
- Return DTOs (never EF entities)
- Delegate to `ICaseRepository` for persistence

## 6. Endpoint List and Routes

| Method | Internal Route | External Route (via Gateway) | Permission |
|---|---|---|---|
| `GET` | `/api/liens/cases` | `/liens/api/liens/cases` | `SYNQ_LIENS.case:read` |
| `GET` | `/api/liens/cases/{id}` | `/liens/api/liens/cases/{id}` | `SYNQ_LIENS.case:read` |
| `GET` | `/api/liens/cases/by-number/{caseNumber}` | `/liens/api/liens/cases/by-number/{caseNumber}` | `SYNQ_LIENS.case:read` |
| `POST` | `/api/liens/cases` | `/liens/api/liens/cases` | `SYNQ_LIENS.case:create` |
| `PUT` | `/api/liens/cases/{id}` | `/liens/api/liens/cases/{id}` | `SYNQ_LIENS.case:update` |

### Query Parameters (List/Search)
- `search` — matches CaseNumber, ClientFirstName, ClientLastName, Title
- `status` — exact status filter
- `page` — page number (default: 1)
- `pageSize` — items per page (default: 20, max: 100)

## 7. Repository Interactions

| Repository | Methods Used | Purpose |
|---|---|---|
| `ICaseRepository` | `SearchAsync` | List/search with pagination |
| `ICaseRepository` | `GetByIdAsync` | Fetch by GUID |
| `ICaseRepository` | `GetByCaseNumberAsync` | Fetch by case number + duplicate check on create |
| `ICaseRepository` | `AddAsync` | Persist new case |
| `ICaseRepository` | `UpdateAsync` | Persist updates |

No other repositories are used. No cross-entity queries.

## 8. Authorization Approach Used

- **Group level:** `RequireAuthorization(Policies.AuthenticatedUser)` + `RequireProductAccess(LiensPermissions.ProductCode)` — all case endpoints require authenticated user with SYNQ_LIENS product access
- **Per endpoint:** `RequirePermission(...)` with new case-specific permission codes
- **Request context guards:** Helper methods (`RequireTenantId`, `RequireUserId`, `RequireOrgId`) validate that nullable context values are present, throwing `UnauthorizedAccessException` if missing

### New Permission Codes
| Code | Used By |
|---|---|
| `SYNQ_LIENS.case:read` | GET list, GET by id, GET by case number |
| `SYNQ_LIENS.case:create` | POST create |
| `SYNQ_LIENS.case:update` | PUT update |

## 9. Request-Context Usage

| Context Value | Source | Used In |
|---|---|---|
| `TenantId` | JWT `tenant_id` claim | All endpoints — scopes all queries |
| `UserId` | JWT `sub` claim | Create and Update — stamps audit fields |
| `OrgId` | JWT `org_id` claim | Create — sets case organization ownership |

All three are derived from the authenticated request context via `ICurrentRequestContext`. None are accepted from client request bodies.

## 10. Donor API Compatibility Notes

- Case number is preserved as-is from the donor model
- Status values match donor conventions: `PreDemand`, `DemandSent`, `InNegotiation`, `CaseSettled`, `Closed`
- Client fields (name, DOB, phone, email, address) are preserved from the donor data model
- Insurance/claim fields preserved
- Financial fields (DemandAmount, SettlementAmount) use domain methods with validation
- The `ExternalReference` field supports donor system cross-references
- `ClientDisplayName` is computed from first + last name in the response DTO

## 11. Validation Performed

| Check | Result |
|---|---|
| Liens.Domain builds | ✅ 0 errors, 0 warnings |
| Liens.Application builds | ✅ 0 errors, 0 warnings |
| Liens.Infrastructure builds | ✅ 0 errors, 0 warnings |
| Liens.Api builds | ✅ 0 errors, 0 warnings |
| Endpoints compile and resolve | ✅ All 5 routes mapped |
| DTOs do not expose EF entities | ✅ |
| No cross-service DB joins | ✅ |
| Auth filters applied | ✅ All endpoints protected |
| Gateway routing compatible | ✅ `/liens/{**catch-all}` with `PathRemovePrefix` |

## 12. Confirmations

- ✅ No EF entities returned directly — all responses use `CaseResponse` DTO
- ✅ No cross-service joins introduced — only `ICaseRepository` used
- ✅ No unrelated APIs added — no Lien, LienOffer, BillOfSale, or other endpoints
- ✅ No frontend code added
- ✅ No audit/notifications/documents integration added
- ✅ No ETL scripts added

## 13. Build Results

```
Liens.Domain          → Build succeeded. 0 Warning(s) 0 Error(s)
Liens.Application     → Build succeeded. 0 Warning(s) 0 Error(s)
Liens.Infrastructure  → Build succeeded. 0 Warning(s) 0 Error(s)
Liens.Api             → Build succeeded. 0 Warning(s) 0 Error(s)
```

## 14. Permission-Model Gaps / Deferred Items

| Gap | Impact | Recommendation |
|---|---|---|
| Case permissions are new and not yet seeded in Identity service | Endpoints will return 403 until permissions are provisioned | Seed `case:read`, `case:create`, `case:update` in the Identity permission catalog |
| No org-scoped filtering on list | A user sees all cases in the tenant, not just their org | Add optional `OrgId` filter to `SearchAsync` if org-level isolation is needed |
| No delete endpoint | Spec did not request deletion | Add soft-delete or archive capability if needed |
| `TransitionStatus` has no guard against invalid transitions | Any valid status can transition to any other valid status | Add transition rules if business logic requires ordered state machine |

## 15. Risks / Assumptions

| Risk | Mitigation |
|---|---|
| New permission codes not yet in Identity | Document for Identity team; endpoints will 403 gracefully until provisioned |
| CaseNumber uniqueness enforced at application layer only | Add DB unique index on `(TenantId, CaseNumber)` if not already present in migration |
| No pagination cursor for large result sets | Offset pagination sufficient for current scale; cursor-based can be added later |
| `UpdateCaseRequest` allows status + financial updates in same call | Domain methods handle each independently; no transactional conflict |

## 16. Final Readiness Statement

**Are real Case APIs established?**
Yes. Five database-backed endpoints are implemented with proper authorization, tenant isolation, request context usage, validation, and clean architecture boundaries. The endpoints follow the established v2 pattern exactly.

**Is the system ready for Lien APIs next?**
Yes. The Case API implementation establishes the complete pattern (DTOs → Application Service → Endpoints → Auth) that Lien APIs will follow. The `PaginatedResult<T>` generic DTO is reusable across all future Liens resources. The permission model is extensible for Lien-specific permissions.
