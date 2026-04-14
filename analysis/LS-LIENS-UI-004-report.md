# LS-LIENS-UI-004: Wire Servicing + Task Manager UI to Real Backend APIs

## Status: COMPLETE

## Summary
Built the complete backend stack (entity → repository → service → DTO → endpoint) for ServicingItem and wired the Servicing page, detail page, Task Manager page, and AssignTaskForm to use real API calls instead of mock store data.

## Backend Changes

### New Files
| File | Purpose |
|------|---------|
| `Liens.Domain/Entities/ServicingItem.cs` | Domain entity with Create/Update/TransitionStatus/Reassign methods |
| `Liens.Application/DTOs/ServicingItemResponse.cs` | Response DTO |
| `Liens.Application/DTOs/CreateServicingItemRequest.cs` | Create request DTO |
| `Liens.Application/DTOs/UpdateServicingItemRequest.cs` | Update request DTO |
| `Liens.Application/Repositories/IServicingItemRepository.cs` | Repository interface |
| `Liens.Application/Interfaces/IServicingItemService.cs` | Service interface |
| `Liens.Application/Services/ServicingItemService.cs` | Service implementation with audit, logging, validation |
| `Liens.Infrastructure/Repositories/ServicingItemRepository.cs` | EF Core repository |
| `Liens.Infrastructure/Persistence/Configurations/ServicingItemConfiguration.cs` | Table `liens_ServicingItems` with indexes |
| `Liens.Api/Endpoints/ServicingEndpoints.cs` | 5 endpoints: GET list, GET by-id, POST, PUT, PUT status |
| `Liens.Infrastructure/Persistence/Migrations/20260414144025_AddServicingItem.cs` | EF migration |

### Modified Files
| File | Change |
|------|--------|
| `LiensDbContext.cs` | Added `DbSet<ServicingItem>` |
| `DependencyInjection.cs` | Registered `IServicingItemRepository` and `IServicingItemService` |
| `Program.cs` | Added `app.MapServicingEndpoints()` |

### API Endpoints
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/liens/servicing` | `LienService` |
| GET | `/api/liens/servicing/{id}` | `LienService` |
| POST | `/api/liens/servicing` | `LienService` |
| PUT | `/api/liens/servicing/{id}` | `LienService` |
| PUT | `/api/liens/servicing/{id}/status` | `LienService` |

### Database
- Table: `liens_ServicingItems`
- Indexes: `UX_ServicingItems_TenantId_TaskNumber` (unique), `IX_ServicingItems_TenantId_Status`, `IX_ServicingItems_TenantId_Priority`, `IX_ServicingItems_TenantId_AssignedTo`, `IX_ServicingItems_TenantId_CaseId`, `IX_ServicingItems_TenantId_LienId`

## Frontend Changes

### New Files (Service Layer)
| File | Purpose |
|------|---------|
| `lib/servicing/servicing.types.ts` | TypeScript types matching backend DTOs |
| `lib/servicing/servicing.api.ts` | API client with toQs helper |
| `lib/servicing/servicing.mapper.ts` | DTO → view model mappers |
| `lib/servicing/servicing.service.ts` | High-level service (getItems, getItem, createItem, updateItem, updateStatus) |
| `lib/servicing/index.ts` | Barrel export |

### Rewritten Files
| File | Changes |
|------|---------|
| `servicing/page.tsx` | Replaced mock store reads with `servicingService.getItems()`, server-side filtering/pagination, loading/error states, retry, View Details routing |
| `servicing/[id]/page.tsx` | Replaced mock store with `servicingService.getItem()`, real status actions via API, loading/error/retry, timeline and linked entities |
| `task-manager/page.tsx` | Replaced mock store with `servicingService.getItems()`, real status actions, board/list views with API data, KPI counts from API response |
| `assign-task-form.tsx` | Replaced `addServicingTask` store call with `servicingService.createItem()`, async submission with loading state, `onCreated` callback for list refresh, text input for assignee (no hardcoded names) |

## Code Review Fixes Applied
1. **EF Migration added** — `AddServicingItem` migration ensures `Database.Migrate()` creates the table
2. **ArgumentException wrapping** — Domain `ArgumentException` caught and re-thrown as `ValidationException` (400) in both `CreateAsync` and `UpdateAsync`
3. **View Details no-op fixed** — ActionMenu "View Details" now routes to detail page via `router.push`

## Build Status
- Backend: 0 errors, 0 warnings
- Frontend: 0 TypeScript errors
