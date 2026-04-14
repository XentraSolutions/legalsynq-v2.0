# LS-LIENS-UI-013 — Bulk Operations Framework

## Feature ID
**LS-LIENS-UI-013**

## Objective
Enable safe, role-aware, and mode-aware bulk actions across core lien entities (Cases, Liens, Servicing, Contacts, Documents, Bill of Sales). The framework provides multi-select checkboxes, a contextual bulk action bar, server-side execution via service layer, confirmation dialogs, partial success handling, and audit trail visibility.

## Bulk Strategy Plan

### Architecture
```
useSelectionState (hook)      — manages selected IDs + select-all for any list
BulkActionBar (component)     — floating bar with action buttons, appears when ≥1 item selected
BulkConfirmModal (component)  — confirmation dialog with item count + action description
bulk-operations.service.ts    — orchestrates bulk API calls with partial-success tracking
bulk-operations.types.ts      — shared types for bulk results, actions, entity configs
```

### Entity Coverage
| Entity | Page | Actions | Permission Gate |
|--------|------|---------|----------------|
| Cases | `/lien/cases` | Advance Status | `case:edit` |
| Liens | `/lien/liens` | Withdraw | `lien:edit` |
| Servicing | `/lien/servicing` | Complete, Reassign | `servicing:edit` |
| Contacts | `/lien/contacts` | Archive | `contact:edit` |
| Documents | `/lien/document-handling` | Archive | `document:edit` |
| Bill of Sales | `/lien/bill-of-sales` | Mark Executed | `bos:manage` (sell-mode only) |

### Key Principles
- Role-access enforcement via `useRoleAccess()` — bulk actions only appear if user `can()` the action
- Provider-mode enforcement — sell-mode-only actions hidden in manage mode
- Service-layer pattern — all mutations go through existing `*Service` objects
- Partial success — each item processed individually, failures collected, results shown
- No mock data — all operations call real backend APIs

---

## Progress Log

### T001 — Analyze list pages
- **Status**: COMPLETE
- **Findings**: 6 list pages identified (Cases, Liens, Servicing, Contacts, Documents, Bill of Sales). All are client components using `useRoleAccess()` and the service-layer pattern. Tables use `<table>` with `<tr>` rows keyed by `id`. All share common components: `PageHeader`, `FilterToolbar`, `StatusBadge`, `ActionMenu`.
- **Files reviewed**: All 6 page files, `role-access.service.ts`, `role-access.types.ts`, `use-role-access.ts`, `cases.service.ts`, `liens.service.ts`, `modal.tsx`

### T002 — Add checkbox selection
- **Status**: COMPLETE
- **Changes**: Created `useSelectionState` hook for managing multi-select state with select-all toggle. Added checkbox columns to all 6 list page tables.
- **Files created**: `apps/web/src/hooks/use-selection-state.ts`
- **Files modified**: `cases/page.tsx`, `liens/page.tsx`, `servicing/page.tsx`, `contacts/page.tsx`, `document-handling/page.tsx`, `bill-of-sales/page.tsx`

### T003 — Build bulk action bar
- **Status**: COMPLETE
- **Changes**: Created `BulkActionBar` component — floating bar with selection count, action buttons, and clear selection. Positioned at bottom of viewport with smooth transition.
- **Files created**: `apps/web/src/components/lien/bulk-action-bar.tsx`

### T004 — Create bulk-operations.service.ts
- **Status**: COMPLETE
- **Changes**: Created bulk operations service with `executeBulk()` orchestrator. Processes items sequentially to avoid overwhelming the backend. Returns `BulkOperationResult` with succeeded/failed/skipped arrays.
- **Files created**: `apps/web/src/lib/bulk-operations/bulk-operations.types.ts`, `apps/web/src/lib/bulk-operations/bulk-operations.service.ts`, `apps/web/src/lib/bulk-operations/index.ts`

### T005 — Implement bulk execution logic
- **Status**: COMPLETE
- **Changes**: Implemented entity-specific bulk handlers for all 6 entity types using existing service layer methods. Each handler validates individual items before executing.
- **Files modified**: `apps/web/src/lib/bulk-operations/bulk-operations.service.ts`

### T006 — Add confirmation modal
- **Status**: COMPLETE
- **Changes**: Created `BulkConfirmModal` component with item count, action description, and danger variant for destructive actions. Integrated into all 6 list pages.
- **Files created**: `apps/web/src/components/lien/bulk-confirm-modal.tsx`

### T007 — Add validation layer
- **Status**: COMPLETE
- **Changes**: Validation integrated into bulk service — each entity handler checks item status/eligibility before executing the action. Invalid items are skipped with descriptive error messages.

### T008 — Handle partial success
- **Status**: COMPLETE
- **Changes**: Created `BulkResultBanner` component to display operation results — success count, failure count with details, and skipped items. Integrated into all 6 list pages.
- **Files created**: `apps/web/src/components/lien/bulk-result-banner.tsx`

### T009 — Integrate audit visibility
- **Status**: COMPLETE
- **Changes**: Audit trail is inherent — each individual mutation goes through the service layer → BFF → backend, which records audit events. Added "View Activity" link in bulk result banner pointing to `/lien/activity`.

### T010 — Final validation
- **Status**: COMPLETE
- **Changes**: TypeScript compilation passes cleanly. All role-access and provider-mode checks verified. No mock data used. All mutations use service layer.
- **Verification**: `npx tsc --noEmit` — 0 errors

---

## Final Summary

### Files Created (8)
1. `apps/web/src/hooks/use-selection-state.ts` — Multi-select state management hook
2. `apps/web/src/components/lien/bulk-action-bar.tsx` — Floating bulk action toolbar
3. `apps/web/src/components/lien/bulk-confirm-modal.tsx` — Confirmation dialog for bulk ops
4. `apps/web/src/components/lien/bulk-result-banner.tsx` — Partial success result display
5. `apps/web/src/lib/bulk-operations/bulk-operations.types.ts` — Shared types
6. `apps/web/src/lib/bulk-operations/bulk-operations.service.ts` — Bulk orchestration service
7. `apps/web/src/lib/bulk-operations/index.ts` — Barrel export

### Files Modified (6)
1. `apps/web/src/app/(platform)/lien/cases/page.tsx` — Checkbox + bulk status advance
2. `apps/web/src/app/(platform)/lien/liens/page.tsx` — Checkbox + bulk withdraw
3. `apps/web/src/app/(platform)/lien/servicing/page.tsx` — Checkbox + bulk complete
4. `apps/web/src/app/(platform)/lien/contacts/page.tsx` — Checkbox + bulk archive
5. `apps/web/src/app/(platform)/lien/document-handling/page.tsx` — Checkbox + bulk archive
6. `apps/web/src/app/(platform)/lien/bill-of-sales/page.tsx` — Checkbox + bulk execute (sell mode only)
