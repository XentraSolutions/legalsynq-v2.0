using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenia.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(Xenia.Infrastructure.Persistence.XeniaDbContext))]
    [Migration("20260710000007_AddOperationsDomain")]
    /// <summary>
    /// Migration 7 — Email Operations Domain.
    ///
    /// Adds:
    ///   xn_email_operational_alerts   — tenant-scoped operational alerts with deduplication
    ///   xn_email_operational_settings — per-tenant operational settings (one row per tenant)
    ///   xn_email_retention_runs       — audit trail for retention execution runs
    ///
    /// Updates:
    ///   xn_email_source_sync_locks    — adds fencing_token (BIGINT) and renewal_failure_count (INT)
    ///   xn_email_ingestion_runs       — adds retry_of_run_id (char(36) nullable)
    ///
    /// Security:
    ///   - All tables scoped to tenant_id.
    ///   - No credential columns anywhere.
    ///   - safe_description / safe_error_summary fields are bounded (VARCHAR).
    ///   - Deduplication key index (non-unique — enforced at app level for partial-index semantics).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class AddOperationsDomain : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── xn_email_operational_alerts ──────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_email_operational_alerts",
                columns: table => new
                {
                    id                = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id         = table.Column<string>(type: "char(36)", nullable: false),
                    email_source_id   = table.Column<string>(type: "char(36)", nullable: true),
                    provider_type     = table.Column<string>(maxLength: 32, nullable: true),
                    alert_type        = table.Column<string>(maxLength: 64, nullable: false),
                    severity          = table.Column<string>(maxLength: 32, nullable: false),
                    status            = table.Column<string>(maxLength: 32, nullable: false),
                    deduplication_key = table.Column<string>(maxLength: 300, nullable: false),
                    title             = table.Column<string>(maxLength: 200, nullable: false),
                    safe_description  = table.Column<string>(maxLength: 1000, nullable: false),
                    first_observed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_observed_at  = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    occurrence_count  = table.Column<int>(nullable: false),
                    acknowledged_at   = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    acknowledged_by   = table.Column<string>(type: "char(36)", nullable: true),
                    resolved_at       = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    resolved_by       = table.Column<string>(type: "char(36)", nullable: true),
                    resolution_reason = table.Column<string>(maxLength: 500, nullable: true),
                    suppressed_until  = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    correlation_id    = table.Column<string>(maxLength: 200, nullable: true),
                    created_at        = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at        = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    version           = table.Column<int>(nullable: false, defaultValue: 1),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_email_operational_alerts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_op_alerts_tenant",
                table: "xn_email_operational_alerts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_op_alerts_source",
                table: "xn_email_operational_alerts",
                columns: new[] { "tenant_id", "email_source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_op_alerts_type",
                table: "xn_email_operational_alerts",
                columns: new[] { "tenant_id", "alert_type" });

            migrationBuilder.CreateIndex(
                name: "ix_op_alerts_severity",
                table: "xn_email_operational_alerts",
                columns: new[] { "tenant_id", "severity" });

            migrationBuilder.CreateIndex(
                name: "ix_op_alerts_status",
                table: "xn_email_operational_alerts",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_op_alerts_first_observed",
                table: "xn_email_operational_alerts",
                column: "first_observed_at");

            migrationBuilder.CreateIndex(
                name: "ix_op_alerts_last_observed",
                table: "xn_email_operational_alerts",
                column: "last_observed_at");

            migrationBuilder.CreateIndex(
                name: "ix_op_alerts_dedup_key",
                table: "xn_email_operational_alerts",
                columns: new[] { "tenant_id", "deduplication_key" });

            // ── xn_email_operational_settings ────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_email_operational_settings",
                columns: table => new
                {
                    id                                  = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id                           = table.Column<string>(type: "char(36)", nullable: false),
                    default_dashboard_range_days        = table.Column<int>(nullable: false, defaultValue: 7),
                    source_failure_alert_threshold      = table.Column<int>(nullable: false, defaultValue: 3),
                    stale_sync_threshold_minutes        = table.Column<int>(nullable: false, defaultValue: 120),
                    lock_warning_threshold_minutes      = table.Column<int>(nullable: false, defaultValue: 30),
                    maximum_retry_count                 = table.Column<int>(nullable: false, defaultValue: 5),
                    cancellation_timeout_seconds        = table.Column<int>(nullable: false, defaultValue: 60),
                    metrics_enabled                     = table.Column<bool>(nullable: false, defaultValue: true),
                    notification_alerts_enabled         = table.Column<bool>(nullable: false, defaultValue: false),
                    default_run_page_size               = table.Column<int>(nullable: false, defaultValue: 50),
                    default_message_page_size           = table.Column<int>(nullable: false, defaultValue: 50),
                    operational_polling_interval_seconds= table.Column<int>(nullable: false, defaultValue: 30),
                    message_metadata_retention_days     = table.Column<int>(nullable: false, defaultValue: 365),
                    message_body_retention_days         = table.Column<int>(nullable: false, defaultValue: 90),
                    validation_history_retention_days   = table.Column<int>(nullable: false, defaultValue: 90),
                    ingestion_run_retention_days        = table.Column<int>(nullable: false, defaultValue: 180),
                    alert_retention_days                = table.Column<int>(nullable: false, defaultValue: 90),
                    attachment_reference_retention_days = table.Column<int>(nullable: false, defaultValue: 365),
                    purge_batch_size                    = table.Column<int>(nullable: false, defaultValue: 500),
                    retention_dry_run_default           = table.Column<bool>(nullable: false, defaultValue: true),
                    legal_hold_enabled                  = table.Column<bool>(nullable: false, defaultValue: false),
                    retention_enabled                   = table.Column<bool>(nullable: false, defaultValue: false),
                    created_at                          = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at                          = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by                          = table.Column<string>(maxLength: 200, nullable: true),
                    version                             = table.Column<int>(nullable: false, defaultValue: 1),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_email_operational_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_op_settings_tenant",
                table: "xn_email_operational_settings",
                column: "tenant_id",
                unique: true);

            // ── xn_email_retention_runs ──────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_email_retention_runs",
                columns: table => new
                {
                    id                            = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id                     = table.Column<string>(type: "char(36)", nullable: false),
                    mode                          = table.Column<string>(maxLength: 32, nullable: false),
                    status                        = table.Column<string>(maxLength: 32, nullable: false),
                    started_at                    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at                  = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    messages_eligible             = table.Column<int>(nullable: false),
                    messages_deleted              = table.Column<int>(nullable: false),
                    bodies_cleared                = table.Column<int>(nullable: false),
                    runs_deleted                  = table.Column<int>(nullable: false),
                    alerts_deleted                = table.Column<int>(nullable: false),
                    attachment_references_deleted  = table.Column<int>(nullable: false),
                    failures                      = table.Column<int>(nullable: false),
                    safe_error_summary            = table.Column<string>(maxLength: 500, nullable: true),
                    correlation_id                = table.Column<string>(maxLength: 200, nullable: true),
                    actor_id                      = table.Column<string>(type: "char(36)", nullable: true),
                    created_at                    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at                    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_email_retention_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_retention_runs_tenant",
                table: "xn_email_retention_runs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_retention_runs_status",
                table: "xn_email_retention_runs",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_retention_runs_started",
                table: "xn_email_retention_runs",
                columns: new[] { "tenant_id", "started_at" });

            // ── Alter xn_email_source_sync_locks: add fencing_token, renewal_failure_count ──
            migrationBuilder.AddColumn<long>(
                name: "fencing_token",
                table: "xn_email_source_sync_locks",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<int>(
                name: "renewal_failure_count",
                table: "xn_email_source_sync_locks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_email_source_sync_locks_fencing_token",
                table: "xn_email_source_sync_locks",
                column: "fencing_token");

            // ── Alter xn_email_ingestion_runs: add retry_of_run_id ───────────
            migrationBuilder.AddColumn<string>(
                name: "retry_of_run_id",
                table: "xn_email_ingestion_runs",
                type: "char(36)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "xn_email_operational_alerts");
            migrationBuilder.DropTable(name: "xn_email_operational_settings");
            migrationBuilder.DropTable(name: "xn_email_retention_runs");

            migrationBuilder.DropIndex(
                name: "ix_email_source_sync_locks_fencing_token",
                table: "xn_email_source_sync_locks");

            migrationBuilder.DropColumn(
                name: "fencing_token",
                table: "xn_email_source_sync_locks");

            migrationBuilder.DropColumn(
                name: "renewal_failure_count",
                table: "xn_email_source_sync_locks");

            migrationBuilder.DropColumn(
                name: "retry_of_run_id",
                table: "xn_email_ingestion_runs");
        }
    }
}
