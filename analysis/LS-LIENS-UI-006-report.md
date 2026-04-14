# LS-LIENS-UI-006: Contacts & Participants Integration — Report

## Objective
Build the full Contact backend stack (service, DTOs, endpoints) on existing entity + repository, create a 5-file frontend service layer, rewrite contacts list/detail/add-contact pages to consume the real API, and remove all mock store data reads.

---

## T001: Backend Contact Stack (DTOs + Service + Endpoints)

### Files Created
| File | Purpose |
|------|---------|
| `Liens.Application/DTOs/ContactResponse.cs` | Response DTO — all contact fields including audit timestamps |
| `Liens.Application/DTOs/CreateContactRequest.cs` | Create request DTO — required + optional fields |
| `Liens.Application/DTOs/UpdateContactRequest.cs` | Update request DTO — mirrors create structure |
| `Liens.Application/Interfaces/IContactService.cs` | Service interface — SearchAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeactivateAsync, ReactivateAsync |
| `Liens.Application/Services/ContactService.cs` | Service implementation — validation, audit publishing, entity mapping |
| `Liens.Api/Endpoints/ContactEndpoints.cs` | MinimalAPI endpoints under `/api/liens/contacts` |

### Files Modified
| File | Change |
|------|--------|
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `IContactService` → `ContactService` |
| `Liens.Api/Program.cs` | Added `app.MapContactEndpoints()` |

### Endpoint Routes
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/liens/contacts` | List/search contacts (query: `search`, `contactType`, `isActive`, `page`, `pageSize`) |
| GET | `/api/liens/contacts/{id}` | Get contact by ID |
| POST | `/api/liens/contacts` | Create new contact |
| PUT | `/api/liens/contacts/{id}` | Update contact |
| PUT | `/api/liens/contacts/{id}/deactivate` | Deactivate contact |
| PUT | `/api/liens/contacts/{id}/reactivate` | Reactivate contact |

### Pattern Compliance
- **Auth/Permissions**: `RequireAuthorization(Policies.AuthenticatedUser)` + `RequireProductAccess(LiensPermissions.ProductCode)` + `RequirePermission(LiensPermissions.LienService)` — matches `ServicingEndpoints.cs`
- **Error handling**: `NotFoundException` for missing entities, `ValidationException` with field-level error dictionaries, `ArgumentException` catch-and-wrap — matches `ServicingItemService`, `LienService`
- **Audit publishing**: Synchronous `_audit.Publish(eventType, action, description, tenantId, actorUserId, entityType, entityId)` — matches `ServicingItemService` signature exactly
- **Build status**: ✅ 0 errors, 0 warnings

---

## T002: Frontend Contacts Service Layer (5 Files)

### Files Created (`apps/web/src/lib/contacts/`)
| File | Purpose |
|------|---------|
| `contacts.types.ts` | DTOs (`ContactResponseDto`, `CreateContactRequestDto`, `UpdateContactRequestDto`), UI models (`ContactListItem`, `ContactDetail`), query/pagination types |
| `contacts.api.ts` | Raw API calls via `apiClient` — list, getById, create, update, deactivate, reactivate |
| `contacts.mapper.ts` | `mapContactToListItem()`, `mapContactToDetail()`, `mapContactPagination()` — bridges backend field names to UI-friendly models |
| `contacts.service.ts` | Service facade — `getContacts()`, `getContact()`, `createContact()`, `updateContact()`, `deactivateContact()`, `reactivateContact()` |
| `index.ts` | Barrel export — types + service |

### Field Mapping (Backend → Frontend)
| Backend | Frontend |
|---------|----------|
| `firstName` + `lastName` | `displayName` (computed by entity) |
| `addressLine1` | `addressLine1` |
| `postalCode` | `postalCode` |
| `createdAtUtc` | `createdAt` (formatted via mapper) |
| `updatedAtUtc` | `updatedAt` (formatted via mapper) |
| Nullable fields | Empty string fallback via `safeString()` |

### Gateway Path
- Frontend calls: `/lien/api/liens/contacts/...`
- Next.js rewrite: → Gateway `/liens/api/liens/contacts/...`
- Gateway strips prefix: → Liens API `/api/liens/contacts/...`

---

## T003: UI Pages Rewritten

### Files Rewritten
| File | Before | After |
|------|--------|-------|
| `contacts/page.tsx` | `useLienStore((s) => s.contacts)` mock data | `contactsService.getContacts()` with search + type filter, loading/error states, side drawer preview |
| `contacts/[id]/page.tsx` | `useLienStore((s) => s.contactDetails)` mock data | `contactsService.getContact(id)` with deactivate/reactivate actions, confirm dialog |
| `add-contact-form.tsx` | `useLienStore((s) => s.addContact)` mock mutation | `contactsService.createContact()` with field validation, `onCreated` callback for list refresh |

### Mock Store Reads Removed
| Removed Store Read | Replacement |
|-------------------|-------------|
| `useLienStore((s) => s.contacts)` | `contactsService.getContacts()` |
| `useLienStore((s) => s.contactDetails)` | `contactsService.getContact(id)` |
| `useLienStore((s) => s.cases)` (for "Related Cases") | Removed — not available from contacts API |
| `useLienStore((s) => s.addContact)` | `contactsService.createContact()` |

### Remaining Store Usage (Intentional)
- `useLienStore((s) => s.currentRole)` — role-gated actions (create, edit)
- `useLienStore((s) => s.addToast)` — toast notifications

---

## Code Review

### Issues Found & Fixed
| Severity | Issue | Fix |
|----------|-------|-----|
| **Critical** | `ContactService` called `_audit.PublishAsync(...)` — `IAuditPublisher` only has synchronous `Publish()` | Changed to `_audit.Publish(eventType, action, description, tenantId, actorUserId, entityType, entityId)` |
| **Critical** | `ValidationException` thrown with single string — constructor requires `(string, Dictionary<string,string[]>)` | Added field-level error dictionaries for all validation paths |
| **Critical** | Missing entities threw `ValidationException` — should be `NotFoundException` | Changed to `NotFoundException` matching `ServicingItemService` pattern |
| **Medium** | No `ArgumentException` catch from domain entity `Create()`/`Update()` | Added try-catch wrapping `ArgumentException` → `ValidationException` with field mapping |

### Post-Fix Verification
- `dotnet build Liens.Api/Liens.Api.csproj` → ✅ **Build succeeded, 0 warnings, 0 errors**
- Frontend compiled without errors (Next.js HMR)

---

## Deliverables Summary

| Component | Files | Status |
|-----------|-------|--------|
| Backend DTOs | 3 created | ✅ |
| Backend Service Interface | 1 created | ✅ |
| Backend Service Implementation | 1 created | ✅ |
| Backend Endpoints | 1 created | ✅ |
| Backend DI + Program.cs | 2 modified | ✅ |
| Frontend Service Layer | 5 created | ✅ |
| Frontend UI Pages | 3 rewritten | ✅ |
| Build Verification | Backend + Frontend | ✅ |
| Code Review | 4 critical/medium issues fixed | ✅ |
