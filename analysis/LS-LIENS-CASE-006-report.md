# LS-LIENS-CASE-006 — Case Section Final Alignment & Validation

## Objective
Perform a final UI alignment pass across the entire Case Detail page (6 tabs) to ensure consistency, cohesion, and production readiness. No new features, no API changes, no business logic changes.

## Scope
- Details, Liens, Documents, Servicing, Notes, Task Manager tabs
- Spacing, padding, alignment, empty states, icon usage, tables, modals, communication panels

---

## Alignment Checklist

### T001 — Header Spacing & Alignment
- **Status**: ✅ Already consistent
- Case header: `px-6 py-4` with 4-col metadata grid
- Tab nav: `px-6`, `gap-4`, `py-2.5` tab buttons
- No changes needed

### T002 — CollapsibleSection Styling
- **Status**: ✅ Already consistent
- All use: `rounded-lg`, `px-5 py-3` header, `px-5 py-4` body, `border-t border-gray-100`
- No changes needed

### T003 — Vertical Spacing
- **Status**: ✅ Already consistent
- All left/right content wrappers use `space-y-4`
- No changes needed

### T004 — LayoutSplit Consistency
- **Status**: ✅ Already consistent
- Details, Liens, Documents, Servicing use LayoutSplit with right comm panel
- Notes, Task Manager are full-width (by design)
- No changes needed

### T005 — Table Standardization
- **Status**: 🔧 Fixed
- **Issue 1**: Task Manager list table header uses `py-2.5` while all other tables use `py-2` → normalized to `py-2`
- **Issue 2**: Task Manager list table header row has `bg-gray-50/50` which no other table header uses → removed
- **Files affected**: `case-detail-client.tsx` (Task Manager list `<thead>`)

### T006 — Modal Spacing & Button Alignment
- **Status**: ✅ Already consistent
- ConfirmDialog (status change) consistent
- Task Manager add-task inline form consistent
- No changes needed

### T007 — Empty/Loading/Error States
- **Status**: 🔧 Fixed
- **Issue 1**: Notes empty state uses `py-12` → normalized to `py-8`
- **Issue 2**: Notes empty state icon uses `text-3xl` → normalized to `text-2xl`
- **Issue 3**: Task Manager list empty state uses `py-10` → normalized to `py-8`
- **Issue 4**: Task Manager kanban column empty state uses `py-6` with no icon → normalized to `py-8` with icon
- All empty states now use: `py-8`, `text-2xl` icon, `text-sm text-gray-400 mt-2` message
- **Files affected**: `case-detail-client.tsx` (Notes empty, Task Manager list empty, Kanban column empty)

### T008 — Icon Consistency
- **Status**: ✅ Already consistent
- All icons use Remix `ri-*` prefix exclusively
- No non-Remix icons found

### T009 — Communication Panel Consistency
- **Status**: ✅ Already consistent
- All 4 LayoutSplit tabs (Details, Liens, Documents, Servicing) share identical Email/SMS/Contacts panels
- Same button styles, same contact card layout, same spacing

### T010 — Navigation Consistency
- **Status**: ✅ Already consistent
- Breadcrumb: `px-6 pt-3` with Cases → Liens Management
- Tab bar: consistent active/inactive styling across all tabs

### T011 — Dead Code Cleanup
- **Status**: 🔧 Fixed
- Removed unused `CheckboxField` component (was from pre-edit era, no longer referenced)
- Removed unused `EmptyTab` component (defined but never called)
- Removed extra blank line after `formatCurrency`

---

## Summary of Changes

| Task | Status | Changes Made |
|------|--------|-------------|
| T001 Header spacing | ✅ No change | Already aligned |
| T002 CollapsibleSection | ✅ No change | Already aligned |
| T003 Vertical spacing | ✅ No change | Already aligned |
| T004 LayoutSplit | ✅ No change | Already aligned |
| T005 Tables | 🔧 Fixed | Task Manager list header normalized (py-2, no bg) |
| T006 Modals | ✅ No change | Already aligned |
| T007 Empty states | 🔧 Fixed | Notes py-12→py-8, icon 3xl→2xl; TM list py-10→py-8; Kanban py-6→py-8 + icon |
| T008 Icons | ✅ No change | All Remix icons |
| T009 Comm panel | ✅ No change | Already aligned |
| T010 Navigation | ✅ No change | Already aligned |
| T011 Dead code | 🔧 Fixed | Removed CheckboxField, EmptyTab (unused) |

## Files Modified
- `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx`

## Validation
- TypeScript compilation: PASS
- No regressions to existing functionality
- All tabs visually cohesive
- Tables standardized across all tabs
- Empty states normalized to consistent pattern
- No dead code remaining
