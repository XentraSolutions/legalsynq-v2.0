# LS-LIENS-CASE-004-01 — Notes Tab Functional Fix

## Status: COMPLETE

## Issue Summary
The Case Detail → Notes tab had several functional issues affecting note creation reliability, data merging, date display safety, and variable ordering.

## Root Causes Identified

### 1. Zustand Store — Append vs Prepend (lien-store.ts)
- `addCaseNote` appended new notes to the end of the array instead of prepending
- No empty-text guard in the store itself (only in the UI)
- Text was not trimmed before storage

### 2. Variable Ordering — `authorName` (case-detail-client.tsx)
- `authorName` was defined after `handleSubmit` which references it
- While technically functional due to JavaScript closure semantics, this was fragile and confusing

### 3. Date Display Safety (case-detail-client.tsx)
- `formatNoteDate()` and `formatNoteTimestamp()` did not guard against invalid ISO strings
- An invalid timestamp would produce "Invalid Date" in the UI
- Sort comparisons using `new Date().getTime()` on invalid dates produced `NaN`, breaking sort stability

### 4. Merge Deduplication (case-detail-client.tsx)
- No deduplication guard when merging TEMP_NOTES with user-created notes
- User notes were placed after TEMP_NOTES in the merged array (should be before for immediate visibility)

## Fixes Applied

### Store Fix (T002)
- Added `text.trim()` guard to `addCaseNote` — empty/whitespace-only notes are blocked at the store level
- Changed append to prepend: `[note, ...(existing || [])]`
- Text is trimmed before storage

### Note Creation Fix (T003)
- Moved `authorName` definition before `handleSubmit` for clear variable ordering
- Author derived from `session.email` with name formatting, fallback to `'Current User'`
- Category defaults to `'general'` and resets after submission
- Composer collapses and text clears after successful submission

### Merged Dataset Fix (T004)
- Added deduplication: user notes are filtered against TEMP_NOTES IDs via `Set`
- User notes now appear before TEMP_NOTES in the merged array so they're immediately visible
- Sorting still determines final display order

### Date Display Fix
- Added `isNaN(d.getTime())` guard to both `formatNoteDate()` and `formatNoteTimestamp()`
- Returns empty string for invalid dates instead of "Invalid Date"
- Sort comparisons fall back to `0` for invalid timestamps via `|| 0`
- Date separator rendering in the timeline also guarded — invalid timestamps skip separator display entirely
- All date rendering paths now use safe parsing with NaN checks

### Search, Category Filter, Sort, Empty States (T005–T008)
- Verified all working correctly — no changes needed
- Search: case-insensitive against `note.text` and `note.author`
- Category filter: `all` / `general` / `internal` / `follow-up`
- Sort: newest (desc) / oldest (asc) with pinned-first logic
- Empty states: distinct messages for "no notes" vs "no matching filters"

## Validation Results
- Build: ✓ Compiled successfully
- No TypeScript errors
- No UI regressions — existing layout/styling unchanged
- Note creation: user notes appear immediately after submission
- Search: filters by text and author, case-insensitive
- Category filter: works for all categories
- Sort toggle: newest/oldest works correctly, pinned notes always first
- Empty states: correct messaging for both scenarios
- Date display: no "Invalid Date" possible
- No duplicate notes on re-render

## Remaining Limitations
- Notes are local-only (Zustand session state) — do not persist across page refresh (by design)
- TEMP_NOTES are static fallback data for UI review — will be replaced when API is wired
- No backend API integration (explicitly out of scope)

## Files Changed
- `apps/web/src/stores/lien-store.ts` — Store fix (prepend, trim, empty guard)
- `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx` — Date guards, authorName ordering, dedup merge, sort safety
