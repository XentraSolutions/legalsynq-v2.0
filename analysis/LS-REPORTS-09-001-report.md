# LS-REPORTS-09-001 — Report Designer Enhancements (v2)

## Objective
Enhance the existing report designer with saved views/variants, calculated fields, column formatting, and improved filter UX/persistence.

## Scope
- Saved views (TenantReportView entity, CRUD, one-default-per-tenant-template)
- Calculated fields (safe post-query evaluation, limited expression support)
- Column formatting (presentation-level config for currency/date/number/percentage/boolean)
- Improved filter model (richer operators: equals, not_equals, contains, starts_with, ends_with, gt, lt, between, in)
- Integration with execution, export, and scheduling pipelines
- UI updates to builder and viewer

## Out of Scope
- Drag-drop visual canvas, charts/dashboards, pivot tables, AI builder, arbitrary SQL

---

## Execution Log

| Step | Files Created/Modified | Change | Status |
|------|----------------------|--------|--------|
| T001 | Domain entity, EF config, migration, DbContext | TenantReportView persistence layer | IN PROGRESS |
| T002 | Repository, service, DTOs, audit events, DI | CRUD service layer | PENDING |
| T003 | FormulaEvaluator, FormulaValidator, FormattingConfig | Calculated field engine | PENDING |
| T004 | Execution/Export/Schedule DTOs and services | ViewId integration | PENDING |
| T005 | ViewEndpoints.cs | API endpoints | PENDING |
| T006 | Frontend types, API, service | Frontend integration | PENDING |
| T007 | Builder, viewer UI components | UI updates | PENDING |
| T008 | Validation, report finalization | Final validation | PENDING |

---

## Schema Summary
*Updated as implementation progresses*

## API Changes Summary
*Updated as implementation progresses*

## UI Changes Summary
*Updated as implementation progresses*

## Validation Results
*Updated as implementation progresses*

## Issues Encountered
*Updated as implementation progresses*

## Decisions Made
*Updated as implementation progresses*

## Known Gaps
*Updated as implementation progresses*
