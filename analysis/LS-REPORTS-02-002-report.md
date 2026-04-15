# LS-REPORTS-02-002 — Tenant Custom Report Override (Inheritance Model)

## Story ID
LS-REPORTS-02-002

## Objective
Introduce a tenant override model that allows a tenant to derive a tenant-specific report configuration from a published `ReportTemplate` without changing the global template itself. Establishes inheritance foundation for tenant custom report variants.

## Scope
- Tenant override domain entity + EF configuration
- Migration for `rpt_TenantReportOverrides` table
- `ITenantReportOverrideRepository` + EF/mock implementations
- `ITenantReportOverrideService` + `TenantReportOverrideService`
- Request/response DTOs
- Override management endpoints (CRUD)
- Effective tenant report resolution endpoint
- Validation, conflict handling, audit hooks

## Out of Scope
- Full drag-and-drop report builder UI
- Report execution / scheduling
- Pricing engine enforcement
- End-user UI / user-level saved views

---

## Execution Log

### Step 1 — Report created
- Created `/analysis/LS-REPORTS-02-002-report.md` FIRST before any code.

### Step 2 — Domain entity + EF configuration
- Status: PENDING

### Step 3 — Migration
- Status: PENDING

### Step 4 — Repository support
- Status: PENDING

### Step 5 — Service layer
- Status: PENDING

### Step 6 — DTOs
- Status: PENDING

### Step 7 — Override management endpoints
- Status: PENDING

### Step 8 — Effective resolution endpoint
- Status: PENDING

### Step 9 — Validation and conflict handling
- Status: PENDING

### Step 10 — Validation
- Status: PENDING

---

## Files Created
_(will be updated as implementation progresses)_

## Files Modified
_(will be updated as implementation progresses)_

## Migration Output
_(will be updated after migration)_

## Database Schema Summary
_(will be updated after migration)_

## API Validation Results
_(will be updated after testing)_

## Build / Run / Validation Status
_(will be updated after build)_

## Issues Encountered
_(none yet)_

## Decisions Made
_(will be updated as decisions are made)_

## Known Gaps / Not Yet Implemented
_(will be updated)_

## Final Summary
_(will be updated upon completion)_
