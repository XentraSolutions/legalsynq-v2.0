# Reports Service

Report template management, execution, export, scheduling, and tenant customization.

**Port:** 5029

## Responsibilities

- Report template catalog (global + tenant-assigned)
- Template versioning (Draft → Published lifecycle)
- Template assignment to tenants (global or targeted scope)
- Tenant report overrides (inherit + customize without modifying the global template)
- Synchronous report execution (query adapter → normalized tabular results)
- Export to CSV, XLSX, PDF
- Scheduled report execution and delivery (Daily/Weekly/Monthly, email/SFTP/OnScreen)
- Formatted output (currency, number, date, boolean, percentage display types)
- Audit event publication for all template, execution, export, and schedule lifecycle events

## Layer Structure

```
Reports.Api/            Endpoints, middleware, Program.cs (port 5029)
Reports.Application/    Execution, export, scheduling, formatting, override services
Reports.Domain/         ReportTemplate, ReportTemplateVersion, ReportTemplateAssignment,
                         TenantReportOverride, ReportExecution, ReportSchedule, ReportScheduleRun
Reports.Infrastructure/ DbContext (ReportsDb), EF migrations, query adapters, exporters
Reports.Shared/         Shared DTOs within the Reports bounded context
Reports.Contracts/      Public-facing contracts (for API consumers)
Reports.Worker/         Background ScheduleWorkerService (polls every 60s, max 10/cycle)
```

## Key Endpoint Groups

| Prefix | Description |
|---|---|
| `/api/v1/templates` | Template CRUD + versioning |
| `/api/v1/templates/{id}/assignments` | Assignment management (global / tenant-targeted) |
| `/api/v1/tenant-templates` | Tenant template catalog resolution |
| `/api/v1/tenant-templates/{id}/overrides` | Tenant override CRUD |
| `/api/v1/tenant-templates/{id}/effective` | Effective template resolution |
| `/api/v1/report-executions` | Execute report + status |
| `/api/v1/report-exports` | Export report to file |
| `/api/v1/report-schedules` | Schedule CRUD + run-now + history |
| `/api/v1/health` | Health probe |
| `/api/v1/ready` | Readiness probe (includes audit adapter mode) |

## Guardrails

- 500-row cap per execution (enforced in `ReportExecutionService`)
- 10MB file size cap per export (checked post-generation)
- 10 schedules processed per background poll cycle

## Export Formats

| Format | Library |
|---|---|
| CSV | `System.Text`, UTF-8 BOM, proper field escaping |
| XLSX | ClosedXML 0.102.3 — typed cells, auto-width columns |
| PDF | QuestPDF 2024.3.0 Community — landscape A4, table layout, page numbers |

When a tenant view supplies column configuration, report execution applies its visible, recognized columns before rendering or export. CSV, XLSX, and PDF therefore use the same configured order and labels; unknown, duplicate, and malformed entries are safely ignored.

## Scheduling

`ScheduleWorkerService` runs as a hosted service inside the Reports API process. Frequencies: Daily, Weekly, Monthly with timezone-aware next-run calculation. Delivery: OnScreen (pass-through), Email (via Notifications), SFTP (stub).

## Audit Integration

`IAuditAdapter` / `SharedAuditAdapter` wired to `LegalSynq.AuditClient`. 26 factory methods covering all template, execution, export, and schedule events. Readiness probe reflects audit mode (`ok` / `mock` / `fail`). All audit calls non-blocking (`TryAuditAsync` wrapper).

## Database

`ReportsDb` (MySQL). Key tables: `rpt_ReportTemplates`, `rpt_ReportTemplateVersions`, `rpt_ReportTemplateAssignments`, `rpt_ReportTemplateAssignmentTenants`, `rpt_TenantReportOverrides`, `rpt_ReportExecutions`, `rpt_ReportSchedules`, `rpt_ReportScheduleRuns`.

## Query Adapter

`IReportDataQueryAdapter` — current implementation is a mock with product-specific canned data for `LIENS`, `FUND`, `CARECONNECT`. Replace with real SQL/API adapters per product for production.
