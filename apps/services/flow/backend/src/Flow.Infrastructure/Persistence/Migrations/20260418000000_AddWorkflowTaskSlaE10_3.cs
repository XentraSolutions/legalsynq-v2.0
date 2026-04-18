using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// LS-FLOW-E10.3 (task slice) — extends the existing workflow-level
    /// SLA / timer engine to <c>flow_workflow_tasks</c>.
    ///
    /// Adds the additive SLA surface to the work-item grain:
    ///   - <c>DueAt</c>              (datetime(6),  NULL)
    ///   - <c>SlaStatus</c>          (varchar(16),  NOT NULL, default 'OnTrack')
    ///   - <c>SlaBreachedAt</c>      (datetime(6),  NULL)
    ///   - <c>LastSlaEvaluatedAt</c> (datetime(6),  NULL)
    ///   - <c>SlaPolicyKey</c>       (varchar(64),  NULL — reserved)
    /// plus two composite indexes:
    ///   - <c>(TenantId, Status, SlaStatus)</c> for admin "show overdue"
    ///     scans.
    ///   - <c>(TenantId, Status, DueAt)</c> for the evaluator's batching
    ///     window (active tasks ordered by deadline).
    ///
    /// No backfill: existing rows keep <c>DueAt = NULL</c>, the
    /// evaluator skips them, and they read as the <c>OnTrack</c>
    /// default in operator surfaces. The factory will stamp
    /// <c>DueAt</c> on every newly-created task going forward.
    /// </summary>
    public partial class AddWorkflowTaskSlaE10_3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Columns -------------------------------------------------
            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "flow_workflow_tasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaStatus",
                table: "flow_workflow_tasks",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "OnTrack")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaBreachedAt",
                table: "flow_workflow_tasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSlaEvaluatedAt",
                table: "flow_workflow_tasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaPolicyKey",
                table: "flow_workflow_tasks",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // ---- Indexes -------------------------------------------------
            migrationBuilder.CreateIndex(
                name: "ix_flow_workflow_tasks_tenant_status_slastatus",
                table: "flow_workflow_tasks",
                columns: new[] { "TenantId", "Status", "SlaStatus" });

            migrationBuilder.CreateIndex(
                name: "ix_flow_workflow_tasks_tenant_status_dueat",
                table: "flow_workflow_tasks",
                columns: new[] { "TenantId", "Status", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_status_dueat",
                table: "flow_workflow_tasks");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_status_slastatus",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "SlaPolicyKey",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "LastSlaEvaluatedAt",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "SlaBreachedAt",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "SlaStatus",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "flow_workflow_tasks");
        }
    }
}
