# LS-LIENS-UI-007: Bill of Sale & Settlement Flow — Report

## Feature ID
LS-LIENS-UI-007

## Objective
Wire the Bill of Sale and Settlement UI to real backend APIs, complete the financial lifecycle of the system, and remove all mock data.

---

## T001: Initial Analysis

### Backend Findings
| Component | Status | Details |
|-----------|--------|---------|
| `BillOfSale` entity | EXISTS | Domain entity with `SubmitForExecution()`, `MarkExecuted()`, `Cancel()`, `UpdateDraft()`, `AttachDocument()` |
| `BillOfSaleStatus` enum | EXISTS | `Draft`, `Pending`, `Executed`, `Cancelled` with state machine transitions |
| `IBillOfSaleRepository` | EXISTS | `GetByIdAsync`, `SearchAsync`, `GetByLienIdAsync`, `AddAsync`, `UpdateAsync` |
| `BillOfSaleService` | EXISTS (read-only) | `GetByIdAsync`, `GetByBillOfSaleNumberAsync`, `SearchAsync`, `GetByLienIdAsync` |
| `BillOfSaleEndpoints` | EXISTS (read-only) | `GET /`, `GET /{id}`, `GET /by-number/{num}`, `GET /liens/{lienId}/bill-of-sales`, `GET /{id}/document` |
| Status transition endpoints | **MISSING** | No submit/execute/cancel endpoints — needed for settlement lifecycle |
| Settlement-specific service | N/A | Settlement flow is managed via `LienSaleService.AcceptOfferAsync` (creates BOS from accepted offer) |

### Frontend Findings
| Component | Status | Details |
|-----------|--------|---------|
| List page (`bill-of-sales/page.tsx`) | MOCK | `useLienStore((s) => s.billsOfSale)`, `updateBos` for status changes |
| Detail page (`[id]/page.tsx`) | MOCK | `useLienStore((s) => s.billsOfSale)` + `useLienStore((s) => s.bosDetails)` |
| Service layer | **MISSING** | No `lib/billofsale/` directory |
| `formatCurrency`/`formatDate` | MOCK IMPORT | Imported from `lien-mock-data.ts` |

### Key Decisions
- **No separate "settlement" API needed** — settlement is BOS status lifecycle (Draft → Pending → Executed)
- **No "payout" API exists** — financial breakdown is BOS fields: `purchaseAmount`, `originalLienAmount`, `discountPercent`
- **BOS creation via lien sale flow** — `LienSaleService.AcceptOfferAsync` creates BOS automatically; no standalone create endpoint needed

---

## T002: Backend Status Transition Endpoints

### Files Modified
| File | Change |
|------|--------|
| `Liens.Application/Interfaces/IBillOfSaleService.cs` | Added `SubmitForExecutionAsync`, `ExecuteAsync`, `CancelAsync` |
| `Liens.Application/Services/BillOfSaleService.cs` | Implemented 3 transition methods with `NotFoundException`, `ValidationException`, audit publishing. Added `IAuditPublisher` dependency. |
| `Liens.Api/Endpoints/BillOfSaleEndpoints.cs` | Added 3 PUT endpoints: `/{id}/submit`, `/{id}/execute`, `/{id}/cancel` |

### New Endpoints
| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| PUT | `/api/liens/bill-of-sales/{id}/submit` | `LienService` | Draft → Pending |
| PUT | `/api/liens/bill-of-sales/{id}/execute` | `LienService` | Pending → Executed |
| PUT | `/api/liens/bill-of-sales/{id}/cancel` | `LienService` | Draft/Pending → Cancelled |

### Pattern Compliance
- `NotFoundException` for missing entities (matches `ContactService`, `ServicingItemService`)
- `InvalidOperationException` from domain entity caught and wrapped to `ValidationException` with `status` error key
- `_audit.Publish(...)` (sync) with correct parameter signature
- `RequireUserId` helper added for mutation endpoints

### Build Status
- ✅ `dotnet build` — 0 errors, 0 warnings

---

## T003: Frontend Service Layer (5 Files)

### Files Created (`apps/web/src/lib/billofsale/`)
| File | Purpose |
|------|---------|
| `billofsale.types.ts` | DTOs (`BillOfSaleResponseDto`), UI models (`BillOfSaleListItem`, `BillOfSaleDetail`), query types, status labels, workflow steps |
| `billofsale.api.ts` | Raw API calls — list, getById, getByNumber, getByLienId, submitForExecution, execute, cancel, getDocumentUrl |
| `billofsale.mapper.ts` | `mapBosToListItem()`, `mapBosToDetail()`, `mapBosPagination()`, `formatCurrency()`, `formatDate()` |
| `billofsale.service.ts` | Service facade — `getBillOfSales()`, `getBillOfSale()`, `getBillOfSaleByNumber()`, `getBillOfSalesByLien()`, `submitForExecution()`, `execute()`, `cancel()`, `getDocumentUrl()` |
| `index.ts` | Barrel export — service, types, utilities |

### Field Mapping (Backend → Frontend)
| Backend DTO | Frontend UI Model |
|-------------|-------------------|
| `billOfSaleNumber` | `bosNumber` |
| `purchaseAmount` | `purchaseAmount` |
| `originalLienAmount` | `originalLienAmount` |
| `discountPercent` | `discountPercent` |
| `sellerContactName` / `buyerContactName` | `sellerContactName` / `buyerContactName` |
| `issuedAtUtc` | `issuedAt` (formatted) |
| `executedAtUtc` | `executedAt` (formatted) |
| `documentId` | `hasDocument` (boolean) + `documentId` (string) |

### Gateway Path
- API calls: `/lien/api/liens/bill-of-sales/...` (via `apiClient` which uses BFF `/api/lien/[...path]`)
- Document download: `/api/lien/api/liens/bill-of-sales/{id}/document` (direct browser link through BFF proxy)

---

## T004: Rewrite UI Pages

### Bill of Sale List Page (`bill-of-sales/page.tsx`)

| Before (Mock) | After (Real API) |
|---------------|-----------------|
| `useLienStore((s) => s.billsOfSale)` | `billOfSaleService.getBillOfSales()` |
| `useLienStore((s) => s.updateBos)` for status changes | `billOfSaleService.submitForExecution/execute/cancel()` |
| `formatCurrency` from `lien-mock-data` | `formatCurrency` from `billofsale` service layer |
| Client-side filtering | Server-side search + status filter via API query params |
| KPI computed from mock array | KPI computed from API response |

**Features preserved**: KPI cards (Total BOS, Executed, Pending, Volume), search toolbar, status filter, action menu (Submit/Execute/Cancel per status), confirm dialog, loading/empty states.

### Bill of Sale Detail Page (`bill-of-sales/[id]/page.tsx`)

| Before (Mock) | After (Real API) |
|---------------|-----------------|
| `useLienStore((s) => s.billsOfSale).find(...)` + `useLienStore((s) => s.bosDetails)[id]` | `billOfSaleService.getBillOfSale(id)` |
| `updateBos(id, { status })` for transitions | `billOfSaleService.submitForExecution/execute/cancel(id)` — returns updated BOS detail |
| "Print" button (simulated toast) | "Download PDF" link via BFF-proxied document URL (only shown when `hasDocument`) |
| `formatCurrency` from `lien-mock-data` | `formatCurrency` from `billofsale` service layer |
| `as any` type assertion | Properly typed `BillOfSaleDetail` |

**Features preserved**: Workflow stepper (Draft → Pending → Executed), financial breakdown cards (Sale Amount, Original Lien Amount, Discount %), transaction details with lien link, parties section, terms/notes sections, status action buttons, confirm dialog.

---

## T005–T013: Cross-Entity, Settlement, Payout, Mock Removal

### Settlement Lifecycle (T009)
- **Status display**: Real `status` field from API, rendered via `StatusBadge`
- **Status transitions**: Real PUT endpoints — Submit (Draft→Pending), Execute (Pending→Executed), Cancel (Draft/Pending→Cancelled)
- **No separate "settlement" entity** — settlement is the BOS lifecycle itself

### Payout Display (T010)
- **Sale amount**: `purchaseAmount` from API (real financial data)
- **Original lien amount**: `originalLienAmount` from API
- **Discount**: `discountPercent` computed by backend on creation (`(1 - purchase/original) * 100`)
- **No fake calculations** — all values come from backend

### Cross-Entity Navigation (T011)
- **BOS → Lien**: Link on detail page via `lienId`
- **Lien → BOS**: Already supported via `GET /api/liens/liens/{lienId}/bill-of-sales` endpoint
- **BOS → Document**: Download PDF via `/{id}/document`

### Mock Data Removed (T012)
| Removed | Replacement |
|---------|-------------|
| `useLienStore((s) => s.billsOfSale)` | `billOfSaleService.getBillOfSales()` |
| `useLienStore((s) => s.bosDetails)` | `billOfSaleService.getBillOfSale()` |
| `useLienStore((s) => s.updateBos)` | `billOfSaleService.submitForExecution/execute/cancel()` |
| `import { formatCurrency, formatDate } from '@/lib/lien-mock-data'` | `import { formatCurrency } from '@/lib/billofsale'` |

### Unsupported Actions (T013)
| Action | Status | Note |
|--------|--------|------|
| Create BOS directly | Disabled | BOS is created via lien sale flow (accept offer) |
| Edit BOS fields | Not available | Backend has `UpdateDraft()` but no endpoint exposed — could add later |
| Payout breakdown (fees) | Not available | No fee structure in backend entity |

---

## T014: Final Validation

### Build Verification
- ✅ Backend: `dotnet build` — 0 errors, 0 warnings
- ✅ Frontend: Next.js compiled without errors (HMR + page load)

### Code Review
| Finding | Severity | Resolution |
|---------|----------|------------|
| Document download URL bypassed BFF proxy | Medium | Fixed: Changed from `/lien/api/liens/...` to `/api/lien/api/liens/...` for proper BFF routing |
| Backend audit publisher uses correct sync `Publish()` | Pass | Verified against `IAuditPublisher` interface |
| `NotFoundException`/`ValidationException` patterns correct | Pass | Matches `ContactService`, `ServicingItemService` |
| No double-slash URL bugs | Pass | All path concatenations verified |

### Architecture Validation
| Rule | Status |
|------|--------|
| API → Service → Mapper → UI | ✅ Enforced |
| No direct HTTP in components | ✅ All via `billOfSaleService` |
| No mock data | ✅ All reads from API |
| No fake financial calculations | ✅ All from backend |
| UI layout preserved | ✅ Same structure, styling, and interactions |
| Service layer only | ✅ 5-file pattern followed |

---

## Executive Summary

### Before
- List and detail pages read from Zustand mock store (`billsOfSale`, `bosDetails`)
- Status changes were local store mutations (no backend persistence)
- Financial data was static mock data
- No backend endpoints for status transitions
- `formatCurrency`/`formatDate` imported from mock data module

### After
- Full API integration through 5-file service layer (`lib/billofsale/`)
- 3 new backend PUT endpoints for settlement lifecycle (submit/execute/cancel)
- Real financial data from backend (purchase amount, original lien amount, discount)
- Document download through BFF-proxied URL
- All formatting utilities self-contained in service layer

### APIs Used
| Endpoint | Method | Frontend Method |
|----------|--------|-----------------|
| `/api/liens/bill-of-sales` | GET | `getBillOfSales()` |
| `/api/liens/bill-of-sales/{id}` | GET | `getBillOfSale()` |
| `/api/liens/bill-of-sales/by-number/{num}` | GET | `getBillOfSaleByNumber()` |
| `/api/liens/liens/{lienId}/bill-of-sales` | GET | `getBillOfSalesByLien()` |
| `/api/liens/bill-of-sales/{id}/submit` | PUT | `submitForExecution()` |
| `/api/liens/bill-of-sales/{id}/execute` | PUT | `execute()` |
| `/api/liens/bill-of-sales/{id}/cancel` | PUT | `cancel()` |
| `/api/liens/bill-of-sales/{id}/document` | GET | `getDocumentUrl()` |

### Deliverables
| Component | Files | Status |
|-----------|-------|--------|
| Backend service transitions | 2 modified | ✅ |
| Backend endpoints | 1 modified (3 new routes) | ✅ |
| Frontend service layer | 5 created | ✅ |
| List page | 1 rewritten | ✅ |
| Detail page | 1 rewritten | ✅ |
| Build verification | Backend + Frontend | ✅ |
| Code review | 1 issue found + fixed | ✅ |

### Risks/Blockers
- **No BOS edit endpoint**: Backend entity has `UpdateDraft()` but no endpoint is exposed. Can be added later if needed.
- **No fee/payout breakdown**: Backend doesn't model fees or multi-party payouts. Only has `purchaseAmount`, `originalLienAmount`, `discountPercent`.
- **Mock store still contains BOS data**: `lien-store.ts` still has `billsOfSale`/`bosDetails` slices — could be cleaned up in a future pass, but they are no longer read by the UI pages.
