# LS-REPORTS-07-001 — Reporting UI & Builder (Tenant + Control Center)

## Objective
Build a complete user-facing UI layer for the Reports Service, enabling:
- Admins (Control Center) → manage templates, assignments
- Tenants (Tenant Portal) → run, export, customize, and schedule reports

## Architecture Decision
Integrating into existing Next.js apps (`apps/web` for tenant, `apps/control-center` for admin) rather than creating a separate Vite app. This preserves established auth, routing, layouts, and session management.

## Execution Log

| Step | Description | Status | Notes |
|------|------------|--------|-------|
| 1 | Create report file | ✅ Complete | This file |
| 2 | Build API client layer | Pending | |
| 3 | Build tenant report catalog | Pending | |
| 4 | Build report execution/viewer | Pending | |
| 5 | Build results DataGrid | Pending | |
| 6 | Build export modal | Pending | |
| 7 | Build report builder | Pending | |
| 8 | Build scheduling UI | Pending | |
| 9 | Enhance Control Center template UI | Pending | |
| 10 | Validate end-to-end | Pending | |
