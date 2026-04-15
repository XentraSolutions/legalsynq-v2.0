# LS-LIENS-UI-013-02 — Panel Expand/Collapse + Header Balance Correction

## Objective
Restore the expand/collapse interaction between middle panels on both Case Detail and Lien Detail pages, and balance the header layout visually.

## Current Issue Summary

### Panel Expand/Collapse
- **Case Detail**: No panel divider or expand/collapse controls existed — the right column (Email section) was rendered in a static `grid-cols-[7fr_3fr]` with no way to expand or collapse either panel.
- **Lien Detail**: Panel divider existed but needed consistent spacing (`mx-1`) to match case detail.

### Header Imbalance
- **Left side (title block)**: Title was `text-lg font-semibold` — too light, lacked visual weight. Subtitle had only `mt-0.5` spacing.
- **Right side (metadata)**: Used `flex items-center gap-5 flex-wrap` — unstructured horizontal wrap causing jagged layout at different widths, uneven column spacing, no consistent grid structure.
- **Action button**: Floated inline with metadata items, no consistent vertical alignment.

## Expected Expand/Collapse Behavior
- **State 1 (split)**: Left and right panels both visible (default)
- **State 2 (left-expanded)**: Left panel expands full width, right panel hidden
- **State 3 (right-expanded)**: Right panel expands full width, left panel hidden
- Always able to return to split from any state via divider controls

## Files Inspected
- `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx`
- `apps/web/src/app/(platform)/lien/liens/[id]/lien-detail-client.tsx`

---

## Implementation Log

| Step | Files Modified | Issue Found | Fix Applied | Status |
|------|---------------|-------------|-------------|--------|
| S1 | `case-detail-client.tsx` | No PanelMode state, no PanelDivider, DetailsTab used static 2-col grid | Added `PanelMode` type + `panelMode` state, `PanelDivider` component, refactored DetailsTab to 3-state grid with divider controls | ✅ Complete |
| S2 | `case-detail-client.tsx` | Header used `flex items-start justify-between gap-6` with unstructured flex-wrap metadata | Replaced with `flex items-center gap-8` layout: left title block (`text-xl font-bold`, `min-w-[160px]`, `mt-1.5` subtitle gap) + right `grid grid-cols-4 gap-x-6 gap-y-3` metadata grid | ✅ Complete |
| S3 | `case-detail-client.tsx` | HeaderMeta was `text-center` (centered in flex, uneven) | Changed to left-aligned (`min-w-0`, no text-center) for clean grid flow | ✅ Complete |
| S4 | `case-detail-client.tsx` | Action button floated inline with metadata, no alignment | Placed in grid cell with `flex items-end` wrapper for bottom-alignment with adjacent metadata values | ✅ Complete |
| S5 | `lien-detail-client.tsx` | Same header layout issues as Case Detail | Applied identical header refactor: `text-xl font-bold` title, `grid grid-cols-4 gap-x-6 gap-y-3` metadata, left-aligned HeaderMeta, action button in grid cell | ✅ Complete |
| S6 | `lien-detail-client.tsx` | PanelDivider lacked horizontal margin for visual breathing room | Added `mx-1` to divider container matching Case Detail | ✅ Complete |
| S7 | Both files | Verified no logic/routing/data/role changes | No business logic modified — all changes are className-only | ✅ Complete |

---

## Changes Summary

### Header Balance (Both Pages)

| Element | Before | After |
|---------|--------|-------|
| Title | `text-lg font-semibold` | `text-xl font-bold leading-tight` |
| Title block | `min-w-0 shrink-0` | `shrink-0 min-w-[160px]` (ensures visual weight) |
| Title-subtitle gap | `mt-0.5` | `mt-1.5` |
| Subtitle | `text-xs text-gray-400` | `text-xs text-gray-400 font-medium` |
| Title-to-metadata gap | `gap-6` | `gap-8` |
| Metadata layout | `flex items-center gap-5 flex-wrap` | `grid grid-cols-4 gap-x-6 gap-y-3` |
| Metadata alignment | `text-center` per item | Left-aligned per item |
| Action button | Inline in flex-wrap | Grid cell with `flex items-end` |
| Overall layout | `flex items-start justify-between` | `flex items-center gap-8` |

### Panel Expand/Collapse (Case Detail — New)

| Component | Added |
|-----------|-------|
| `PanelMode` type | `'split' \| 'left' \| 'right'` |
| `panelMode` state | `useState<PanelMode>('split')` in CaseDetailClient |
| `PanelDivider` component | Two directional buttons + vertical connector, hover/active states |
| DetailsTab refactored | From static `grid-cols-[7fr_3fr]` to dynamic 3-state grid with divider |

### Panel Divider Polish (Both Pages)

| Element | Spec |
|---------|------|
| Size | `w-7 h-7` per button |
| Shape | `rounded-md` |
| Border | `border-gray-200` default, `border-primary` active |
| Hover | `hover:bg-gray-50 hover:border-gray-300 hover:text-gray-600` |
| Active state | `bg-primary/10 text-primary border-primary` |
| Connector | `w-px h-6 bg-gray-200` between buttons |
| Centering | `flex flex-col items-center justify-center self-stretch` |
| Spacing | `mx-1` horizontal margin |

---

## Validation Results

### Header
- [x] Title and subtitle properly spaced (`mt-1.5` gap)
- [x] Title has dominant visual weight (`text-xl font-bold`)
- [x] Metadata aligned in clean 4-column grid
- [x] Action button aligned in grid cell (bottom-aligned with values)
- [x] Left/right visually balanced (title block has `min-w-[160px]`)
- [x] Consistent horizontal flow across both pages

### Panels
- [x] Expand/collapse works on Case Detail (new)
- [x] Expand/collapse continues working on Lien Detail (preserved)
- [x] All 3 states functional: split, left-expanded, right-expanded
- [x] Controls visible and vertically centered
- [x] No overflow issues
- [x] Layout alignment preserved

### No Regressions
- [x] No functionality changes
- [x] No routing changes
- [x] No data field changes
- [x] No role-access changes
- [x] No service/API changes
- [x] Build compiles without errors
- [x] Fast Refresh confirms clean compilation

## Remaining UI Gaps
- None identified for this correction scope.
