# LS-LIENS-UI-002 Implementation Report

## Objective
Wire the Cases UI to real backend APIs using an enforced layered API service pattern (API client → service → mapper → UI pages).

## Status: COMPLETE

## Changes Made

### 1. BFF Proxy Routing Fix
**File:** `apps/web/src/app/api/lien/[...path]/route.ts`
- Fixed gateway routing prefix: `/lien/` → `/liens/` (plural)
- Now matches the gateway YARP route `"Path": "/liens/{**catch-all}"` with `PathRemovePrefix: /liens`
- Aligned with CareConnect BFF proxy convention (`/careconnect/` matches `/careconnect/{**catch-all}`)

### 2. Layered Service Pattern (New Files)
All under `apps/web/src/lib/cases/`:

| Layer | File | Purpose |
|-------|------|---------|
| Types | `cases.types.ts` | DTOs matching backend (`CaseResponseDto`, `CreateCaseRequestDto`, `UpdateCaseRequestDto`, `LienResponseDto`, `PaginatedResultDto`), UI models (`CaseListItem`, `CaseDetail`, `CaseLienItem`), `PaginationMeta` |
| API Client | `cases.api.ts` | Raw HTTP calls via `apiClient`: `list`, `getById`, `getByNumber`, `create`, `update`, `listLiensByCase` |
| Mapper | `cases.mapper.ts` | DTO→UI transformations: `mapCaseToListItem`, `mapCaseToDetail`, `mapDtoToUpdateRequest` (non-destructive updates), `mapLienToListItem`, `mapPagination` |
| Service | `cases.service.ts` | Business API: `getCases`, `getCase`, `createCase`, `updateCase`, `updateCaseStatus`, `getCaseLiens` |
| Barrel | `index.ts` | Re-exports service + types |

### 3. Cases List Page Rewrite
**File:** `apps/web/src/app/(platform)/lien/cases/page.tsx`
- Removed: `useLienStore` mock data reads, `formatCurrency`/`formatDate` from mock helpers
- Added: `casesService.getCases()` with loading spinner, error banner with retry, empty state
- Added: Server-side pagination controls (Previous/Next)
- Table columns adapted: Case #, Client, Title/Ref, Insurance, Demand, Incident, Status (removed mock-only: Law Firm, Liens count, Amount, Assigned To)
- Status advance: calls `casesService.updateCaseStatus()` via real API
- Removed: Reassign action (no backend support)
- CreateCaseForm now receives `onCreated` callback to refresh list

### 4. Case Detail Page Rewrite
**File:** `apps/web/src/app/(platform)/lien/cases/[id]/page.tsx`
- Removed: `useLienStore.getCaseDetail()`, mock liens/documents from store
- Added: `casesService.getCase()` + `casesService.getCaseLiens()` with loading/error states
- Related liens fetched from real API via `listLiensByCase`
- KPI cards: Related Liens count (from API), Demand Amount, Settlement Amount
- Detail sections adapted to real backend fields (removed: Law Firm, Medical Facility)
- Status advance: calls `casesService.updateCaseStatus()` (non-destructive, re-fetches fresh DTO)
- Removed: Add Lien, Add Document, Assign Task buttons (no corresponding backend endpoints for case-scoped creation)
- NotesPanel: kept but set to read-only (no backend notes API exists yet)

### 5. CreateCaseForm Rewrite
**File:** `apps/web/src/components/lien/forms/create-case-form.tsx`
- Removed: `useLienStore.addCase()` mock mutation
- Added: `casesService.createCase()` with proper error handling
- Fields aligned to backend `CreateCaseRequest`: Case Number (required), Client First/Last Name (required), Title, Date of Incident, Insurance Carrier, Description
- Removed: Law Firm, Medical Facility, Assigned To fields (not in backend)
- Added: Submitting state, conflict error handling (duplicate case number)

## Schema Gap Analysis

| UI Mock Field | Backend Status | Resolution |
|---|---|---|
| `clientName` | `ClientDisplayName` (computed) | Mapped via `clientDisplayName` |
| `lawFirm` | Not in CaseResponse | Removed from UI; replaced with `title`/`externalReference` |
| `medicalFacility` | Not in CaseResponse | Removed from UI |
| `assignedTo` | Not in CaseResponse | Removed from UI; Reassign action removed |
| `lienCount` | Not in CaseResponse | Detail page fetches liens via separate API call |
| `totalLienAmount` | Not in CaseResponse | Removed from list; detail shows lien count instead |
| `DateOnly` dates | Serialized as strings | Mapped via `formatDateField()` |

## Non-Destructive Status Update Pattern
The `updateCaseStatus()` service method:
1. Fetches fresh `CaseResponseDto` from `GET /api/liens/cases/{id}`
2. Maps it to full `UpdateCaseRequestDto` via `mapDtoToUpdateRequest()` (preserves all fields)
3. Overrides only the `status` field
4. Sends complete payload to `PUT /api/liens/cases/{id}`

This prevents the backend's full-replacement update from nulling out existing fields.

## Build Validation
- Next.js compilation: 0 errors, 0 warnings
- All Fast Refresh cycles completed successfully
