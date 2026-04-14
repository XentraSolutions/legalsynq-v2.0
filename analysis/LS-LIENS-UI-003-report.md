# LS-LIENS-UI-003: Liens UI → Real API Integration

**Date:** 2026-04-14
**Status:** Complete
**Build:** Clean (0 errors, 0 warnings)
**Scope:** Wire Liens list page, detail page, and create modal to real backend APIs; replace Zustand mock store data reads with layered service pattern matching Cases.

---

## Objective

Replace all direct Zustand mock store reads (`useLienStore`) for lien data in the three primary Liens UI surfaces with real API calls through a new layered service module (`apps/web/src/lib/liens/`), following the identical 5-file architecture established by the Cases service layer (LS-LIENS-UI-002).

---

## Files Created

| File | Lines | Purpose |
|------|------:|---------|
| `apps/web/src/lib/liens/liens.types.ts` | 187 | DTOs matching backend C# classes + UI model interfaces |
| `apps/web/src/lib/liens/liens.api.ts` | 61 | Raw HTTP client wrapping `apiClient` for all lien endpoints |
| `apps/web/src/lib/liens/liens.mapper.ts` | 136 | DTO → UI model transformations with safe defaults |
| `apps/web/src/lib/liens/liens.service.ts` | 69 | Business-facing service methods consumed by pages |
| `apps/web/src/lib/liens/index.ts` | 13 | Barrel exports for clean imports |

## Files Modified

| File | Lines | Changes |
|------|------:|---------|
| `apps/web/src/app/(platform)/lien/liens/page.tsx` | 180 | Full rewrite: API fetch, loading/error/pagination, server-side filtering |
| `apps/web/src/app/(platform)/lien/liens/[id]/page.tsx` | 250 | Full rewrite: API fetch, offers from backend, cross-entity case lookup |
| `apps/web/src/components/lien/forms/create-lien-modal.tsx` | 134 | Full rewrite: calls `liensService.createLien()`, lienNumber field added |
| `replit.md` | — | Added liens service layer documentation + LS-LIENS-UI-003 section |

**Total:** 1,030 lines across 8 files (5 new, 3 rewritten)

---

## Architecture

### Service Layer Pattern (5-file stack)

```
liens.types.ts          DTO interfaces (backend shape) + UI model interfaces (display shape)
       ↓
liens.api.ts            apiClient.get/post/put wrappers for each endpoint
       ↓
liens.mapper.ts         DTO → UI model transformations (date formatting, label lookup, null coalescing)
       ↓
liens.service.ts        Business methods: getLiens, getLien, createLien, updateLien, getLienOffers, createOffer, acceptOffer
       ↓
index.ts                Barrel exports (types + service)
```

This mirrors the `apps/web/src/lib/cases/` structure exactly.

### Backend Endpoints Covered

| Method | Endpoint | Service Method | Used By |
|--------|----------|----------------|---------|
| `GET` | `/lien/api/liens/liens?search&status&lienType&caseId&page&pageSize` | `getLiens()` | List page |
| `GET` | `/lien/api/liens/liens/{id}` | `getLien()` | Detail page |
| `GET` | `/lien/api/liens/liens/by-number/{number}` | (exposed, not yet consumed) | — |
| `POST` | `/lien/api/liens/liens` | `createLien()` | Create modal |
| `PUT` | `/lien/api/liens/liens/{id}` | `updateLien()` | (exposed, not yet consumed) |
| `GET` | `/lien/api/liens/liens/{id}/offers` | `getLienOffers()` | Detail page |
| `POST` | `/lien/api/liens/offers` | `createOffer()` | Detail page (Submit Offer modal) |
| `POST` | `/lien/api/liens/offers/{id}/accept` | `acceptOffer()` | Detail page (Accept button) |

### DTO Parity

| Frontend Type | Backend C# Class | Match |
|--------------|-------------------|-------|
| `LienResponseDto` | `LienResponse` | ✅ All 22 fields |
| `CreateLienRequestDto` | `CreateLienRequest` | ✅ All 12 fields |
| `UpdateLienRequestDto` | `UpdateLienRequest` | ✅ All 11 fields |
| `LienOfferResponseDto` | `LienOfferResponse` | ✅ All 17 fields |
| `CreateLienOfferRequestDto` | `CreateLienOfferRequest` | ✅ All 4 fields |
| `SaleFinalizationResultDto` | `SaleFinalizationResult` | ✅ All 12 fields |

---

## Page Changes

### Liens List Page (`liens/page.tsx`)

**Before:** Read `useLienStore((s) => s.liens)` for mock data; client-side filtering; no pagination; no loading/error states; inline `updateLien` for status changes.

**After:**
- `liensService.getLiens(query)` with server-side `?search`, `?status`, `?lienType` query params
- Pagination controls (Previous / Next) with `page` / `pageSize` params
- Loading spinner during fetch
- Error banner with retry button (preserves active filters on retry)
- `onCreated` callback refreshes list after modal creation
- Side drawer preview still works with in-memory filtered data
- Count display shows `pagination.totalCount` from server response

### Liens Detail Page (`liens/[id]/page.tsx`)

**Before:** Read `useLienStore((s) => s.getLienDetail(id))` for mock data; inline `addOffer` / `updateOffer` / `updateLien` mutations; `addActivity` for local timeline; all offer management client-side.

**After:**
- `liensService.getLien(id)` + `liensService.getLienOffers(id)` via `Promise.all`
- Loading spinner and not-found/error fallback UI
- Submit Offer calls `liensService.createOffer()` then re-fetches data
- Accept Offer calls `liensService.acceptOffer()` which triggers backend sale finalization (Bill of Sale creation, competing offer rejection)
- Cross-entity case lookup: `casesService.getCase(caseId)` fetches linked case details; displays case number + client name with navigation link to `/lien/cases/{caseId}`
- Stale `linkedCase` state explicitly cleared when navigating between liens (code review finding)

### Create Lien Modal (`create-lien-modal.tsx`)

**Before:** `useLienStore((s) => s.addLien)` with client-generated ID and lien number; no API call; no error handling.

**After:**
- Builds `CreateLienRequestDto` with user-provided `lienNumber` (required by backend — was previously auto-generated client-side)
- Calls `liensService.createLien(request)` via API
- Shows inline form-level error on API failure (`errors._form`)
- Loading state on submit button (`Creating...`)
- `onCreated` prop callback for parent page to refresh list

---

## Cross-Entity Integration

| Direction | Implementation | Status |
|-----------|---------------|--------|
| Case → Lien | `cases/[id]/page.tsx` calls `casesService.getCaseLiens(id)`, renders lien rows with links to `/lien/liens/{id}` | ✅ Already working (LS-LIENS-UI-002) |
| Lien → Case | `liens/[id]/page.tsx` calls `casesService.getCase(caseId)`, displays case number + client name in Lien Summary section with link to `/lien/cases/{caseId}` | ✅ New in this task |

---

## Remaining Zustand Store Usage

The following `useLienStore` selectors remain in the rewritten pages — these are intentional and do not read lien/case data from mock stores:

| Selector | Purpose | Pages |
|----------|---------|-------|
| `currentRole` | RBAC role gating (`canPerformAction`) for UI button visibility | List, Detail |
| `addToast` | Toast notification display for success/error feedback | List, Detail, Create |

All lien data, offer data, and case cross-references now come exclusively from API calls.

---

## Code Review

**Reviewer:** Architect subagent
**Result:** Pass (after 2 fixes applied)

| Finding | Severity | Resolution |
|---------|----------|------------|
| Stale `linkedCase` on client-side navigation between liens | Critical | Added `else { setLinkedCase(null); }` in `fetchLien` |
| Retry button resets filters | Minor | Retry now passes current `search`, `statusFilter`, `typeFilter` values |
| `meta` field type mismatch (JSX in string-only field) | Build error | Changed to string-only case number display in `meta`; JSX link preserved in `DetailSection` |

---

## Build Validation

```
✓ Compiled successfully
✓ Linting and checking validity of types
✓ 0 errors, 0 warnings
```

---

## What's Next

Potential follow-up tasks for remaining Liens UI surfaces not yet wired to APIs:
- **Bill of Sale pages** — currently using `useLienStore` mock data
- **Servicing pages** — currently using `useLienStore` mock data
- **Contacts pages** — currently using `useLienStore` mock data
- **Documents pages** — currently using `useLienStore` mock data
- **Lien edit form** — `updateLien` service method is exposed but not yet consumed by any UI
- **Offer rejection** — backend `POST /offers/{id}/reject` endpoint not yet wired (only accept is connected)
