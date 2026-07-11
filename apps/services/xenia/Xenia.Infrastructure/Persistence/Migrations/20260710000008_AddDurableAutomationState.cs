using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenia.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(Xenia.Infrastructure.Persistence.XeniaDbContext))]
    [Migration("20260710000008_AddDurableAutomationState")]
    /// <summary>
    /// Migration 8 — Durable Automation State.
    ///
    /// Adds 9 tables to replace all process-local in-memory automation stores
    /// with MySQL-backed durable state. No cross-service FKs, no credential
    /// columns, no raw payload columns.
    ///
    /// Tables created:
    ///   xn_automation_registry      — platform-level registration per automation key
    ///   xn_automation_versions      — version history and manifest archive
    ///   xn_tenant_automations       — per-tenant enable/disable state
    ///   xn_automation_configuration — layered configuration (global + tenant-scoped)
    ///   xn_automation_runtime_state — live counters and health per (tenant, key)
    ///   xn_automation_executions    — execution audit trail with status lifecycle
    ///   xn_automation_dead_letters  — failed executions awaiting review/replay
    ///   xn_automation_schedules     — cron/interval schedule definitions
    ///   xn_automation_idempotency   — exactly-once delivery fence with TTL expiry
    ///
    /// Security:
    ///   - All tenant-scoped tables include tenant_id as first composite index key.
    ///   - safe_* columns are bounded VARCHAR — no raw stack traces or secrets.
    ///   - configuration_json / manifest_json store sanitized operator-controlled data only.
    ///   - secret_references_json holds references (keys), never secret values.
    ///   - Optimistic concurrency via row_version (uint, IsConcurrencyToken) on all mutable tables.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class AddDurableAutomationState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── xn_automation_registry ───────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_automation_registry",
                columns: table => new
                {
                    id                       = table.Column<string>(type: "char(36)", nullable: false),
                    automation_key           = table.Column<string>(maxLength: 200, nullable: false),
                    provider                 = table.Column<string>(maxLength: 200, nullable: false),
                    category                 = table.Column<string>(maxLength: 100, nullable: false),
                    current_version          = table.Column<string>(maxLength: 50, nullable: false),
                    lifecycle_status         = table.Column<int>(nullable: false),
                    globally_enabled         = table.Column<bool>(nullable: false, defaultValue: false),
                    manifest_hash            = table.Column<string>(maxLength: 64, nullable: false),
                    minimum_platform_version = table.Column<string>(maxLength: 50, nullable: true),
                    registered_at            = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_reconciled_at       = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at               = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at               = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version              = table.Column<uint>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_automation_registry", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_xn_automation_registry_key",
                table: "xn_automation_registry",
                column: "automation_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_registry_lifecycle_status",
                table: "xn_automation_registry",
                column: "lifecycle_status");

            // ── xn_automation_versions ───────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_automation_versions",
                columns: table => new
                {
                    id                      = table.Column<string>(type: "char(36)", nullable: false),
                    automation_key          = table.Column<string>(maxLength: 200, nullable: false),
                    version                 = table.Column<string>(maxLength: 50, nullable: false),
                    manifest_json           = table.Column<string>(type: "longtext", nullable: false),
                    manifest_schema_version = table.Column<string>(maxLength: 50, nullable: false),
                    compatibility_json      = table.Column<string>(type: "text", nullable: true),
                    registered_at           = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    activated_at            = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    retired_at              = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status                  = table.Column<int>(nullable: false),
                    created_at              = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at              = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version             = table.Column<uint>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_automation_versions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_xn_automation_versions_key_version",
                table: "xn_automation_versions",
                columns: new[] { "automation_key", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_versions_key",
                table: "xn_automation_versions",
                column: "automation_key");

            // ── xn_tenant_automations ────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_tenant_automations",
                columns: table => new
                {
                    id                    = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id             = table.Column<string>(type: "char(36)", nullable: false),
                    automation_key        = table.Column<string>(maxLength: 200, nullable: false),
                    enabled               = table.Column<bool>(nullable: false, defaultValue: false),
                    lifecycle_override    = table.Column<string>(maxLength: 50, nullable: true),
                    configuration_version = table.Column<string>(maxLength: 50, nullable: true),
                    last_validated_at     = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by            = table.Column<string>(maxLength: 200, nullable: true),
                    created_at            = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at            = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version           = table.Column<uint>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_tenant_automations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_xn_tenant_automations_tenant_key",
                table: "xn_tenant_automations",
                columns: new[] { "tenant_id", "automation_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xn_tenant_automations_tenant",
                table: "xn_tenant_automations",
                column: "tenant_id");

            // ── xn_automation_configuration ──────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_automation_configuration",
                columns: table => new
                {
                    id                      = table.Column<string>(type: "char(36)", nullable: false),
                    scope_type              = table.Column<int>(nullable: false),
                    tenant_id               = table.Column<string>(type: "char(36)", nullable: true),
                    automation_key          = table.Column<string>(maxLength: 200, nullable: false),
                    configuration_namespace = table.Column<string>(maxLength: 200, nullable: false),
                    configuration_json      = table.Column<string>(type: "longtext", nullable: false),
                    schema_version          = table.Column<string>(maxLength: 50, nullable: false),
                    secret_references_json  = table.Column<string>(type: "text", nullable: true),
                    updated_by              = table.Column<string>(maxLength: 200, nullable: true),
                    created_at              = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at              = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version             = table.Column<uint>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_automation_configuration", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_xn_automation_configuration_scope",
                table: "xn_automation_configuration",
                columns: new[] { "scope_type", "tenant_id", "automation_key", "configuration_namespace" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_configuration_tenant",
                table: "xn_automation_configuration",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_configuration_key",
                table: "xn_automation_configuration",
                column: "automation_key");

            // ── xn_automation_runtime_state ──────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_automation_runtime_state",
                columns: table => new
                {
                    id                             = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id                      = table.Column<string>(type: "char(36)", nullable: false),
                    automation_key                 = table.Column<string>(maxLength: 200, nullable: false),
                    automation_version             = table.Column<string>(maxLength: 50, nullable: false, defaultValue: ""),
                    global_state                   = table.Column<int>(nullable: false),
                    tenant_state                   = table.Column<int>(nullable: true),
                    lifecycle_state                = table.Column<int>(nullable: false),
                    health_state                   = table.Column<int>(nullable: false),
                    last_execution_at              = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_successful_execution_at   = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    consecutive_failure_count      = table.Column<int>(nullable: false, defaultValue: 0),
                    total_executions               = table.Column<int>(nullable: false, defaultValue: 0),
                    active_executions              = table.Column<int>(nullable: false, defaultValue: 0),
                    total_failure_count            = table.Column<int>(nullable: false, defaultValue: 0),
                    next_eligible_execution_at     = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_safe_error_category       = table.Column<string>(maxLength: 100, nullable: true),
                    last_safe_error_summary        = table.Column<string>(maxLength: 500, nullable: true),
                    worker_instance_id             = table.Column<string>(maxLength: 200, nullable: true),
                    created_at                     = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at                     = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version                    = table.Column<uint>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_automation_runtime_state", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_xn_automation_runtime_state_tenant_key",
                table: "xn_automation_runtime_state",
                columns: new[] { "tenant_id", "automation_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_runtime_state_tenant",
                table: "xn_automation_runtime_state",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_runtime_state_health",
                table: "xn_automation_runtime_state",
                column: "health_state");

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_runtime_state_next_eligible",
                table: "xn_automation_runtime_state",
                columns: new[] { "tenant_id", "next_eligible_execution_at" });

            // ── xn_automation_executions ─────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_automation_executions",
                columns: table => new
                {
                    id                   = table.Column<string>(type: "char(36)", nullable: false),
                    execution_id         = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id            = table.Column<string>(type: "char(36)", nullable: false),
                    automation_key       = table.Column<string>(maxLength: 200, nullable: false),
                    automation_version   = table.Column<string>(maxLength: 50, nullable: false),
                    trigger_type         = table.Column<int>(nullable: false),
                    status               = table.Column<int>(nullable: false),
                    idempotency_key      = table.Column<string>(maxLength: 200, nullable: true),
                    correlation_id       = table.Column<string>(type: "char(36)", nullable: true),
                    actor_id             = table.Column<string>(maxLength: 200, nullable: true),
                    queued_at            = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    started_at           = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    completed_at         = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    retry_count          = table.Column<int>(nullable: false, defaultValue: 0),
                    parent_execution_id  = table.Column<string>(type: "char(36)", nullable: true),
                    dead_letter_id       = table.Column<string>(type: "char(36)", nullable: true),
                    safe_result_summary  = table.Column<string>(maxLength: 500, nullable: true),
                    safe_error_category  = table.Column<string>(maxLength: 100, nullable: true),
                    safe_error_summary   = table.Column<string>(maxLength: 500, nullable: true),
                    worker_instance_id   = table.Column<string>(maxLength: 200, nullable: true),
                    created_at           = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at           = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version          = table.Column<uint>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_automation_executions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_xn_automation_executions_execution_id",
                table: "xn_automation_executions",
                column: "execution_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_executions_tenant",
                table: "xn_automation_executions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_executions_tenant_key",
                table: "xn_automation_executions",
                columns: new[] { "tenant_id", "automation_key" });

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_executions_tenant_status",
                table: "xn_automation_executions",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_executions_correlation",
                table: "xn_automation_executions",
                column: "correlation_id");

            // ── xn_automation_dead_letters ───────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_automation_dead_letters",
                columns: table => new
                {
                    id                     = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id              = table.Column<string>(type: "char(36)", nullable: false),
                    automation_key         = table.Column<string>(maxLength: 200, nullable: false),
                    automation_version     = table.Column<string>(maxLength: 50, nullable: false),
                    execution_id           = table.Column<string>(type: "char(36)", nullable: true),
                    trigger_type           = table.Column<int>(nullable: false),
                    failure_category       = table.Column<string>(maxLength: 100, nullable: false),
                    safe_error_summary     = table.Column<string>(maxLength: 500, nullable: true),
                    retry_count            = table.Column<int>(nullable: false, defaultValue: 0),
                    replay_count           = table.Column<int>(nullable: false, defaultValue: 0),
                    first_failed_at        = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_failed_at         = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    next_eligible_retry_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status                 = table.Column<int>(nullable: false),
                    resolution             = table.Column<string>(maxLength: 500, nullable: true),
                    correlation_id         = table.Column<string>(type: "char(36)", nullable: true),
                    created_at             = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at             = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version            = table.Column<uint>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_automation_dead_letters", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_dead_letters_tenant",
                table: "xn_automation_dead_letters",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_dead_letters_tenant_key",
                table: "xn_automation_dead_letters",
                columns: new[] { "tenant_id", "automation_key" });

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_dead_letters_next_retry",
                table: "xn_automation_dead_letters",
                columns: new[] { "tenant_id", "next_eligible_retry_at" });

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_dead_letters_tenant_status",
                table: "xn_automation_dead_letters",
                columns: new[] { "tenant_id", "status" });

            // ── xn_automation_schedules ──────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_automation_schedules",
                columns: table => new
                {
                    id                 = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id          = table.Column<string>(type: "char(36)", nullable: false),
                    automation_key     = table.Column<string>(maxLength: 200, nullable: false),
                    schedule_type      = table.Column<int>(nullable: false),
                    expression         = table.Column<string>(maxLength: 200, nullable: true),
                    interval_seconds   = table.Column<int>(nullable: true),
                    time_zone          = table.Column<string>(maxLength: 100, nullable: false),
                    enabled            = table.Column<bool>(nullable: false, defaultValue: true),
                    next_run_at        = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_run_at        = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    misfire_policy     = table.Column<int>(nullable: false),
                    concurrency_policy = table.Column<int>(nullable: false),
                    created_by         = table.Column<string>(maxLength: 200, nullable: true),
                    updated_by         = table.Column<string>(maxLength: 200, nullable: true),
                    created_at         = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at         = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version        = table.Column<uint>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_automation_schedules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_schedules_tenant",
                table: "xn_automation_schedules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_schedules_tenant_key",
                table: "xn_automation_schedules",
                columns: new[] { "tenant_id", "automation_key" });

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_schedules_enabled_next_run",
                table: "xn_automation_schedules",
                columns: new[] { "enabled", "next_run_at" });

            // ── xn_automation_idempotency ────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_automation_idempotency",
                columns: table => new
                {
                    id                  = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id           = table.Column<string>(type: "char(36)", nullable: false),
                    automation_key      = table.Column<string>(maxLength: 200, nullable: false),
                    idempotency_key     = table.Column<string>(maxLength: 200, nullable: false),
                    request_fingerprint = table.Column<string>(maxLength: 64, nullable: false),
                    execution_id        = table.Column<string>(type: "char(36)", nullable: true),
                    expires_at          = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at          = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version         = table.Column<uint>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_automation_idempotency", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_xn_automation_idempotency_tenant_key_idkey",
                table: "xn_automation_idempotency",
                columns: new[] { "tenant_id", "automation_key", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_idempotency_tenant_key",
                table: "xn_automation_idempotency",
                columns: new[] { "tenant_id", "automation_key" });

            migrationBuilder.CreateIndex(
                name: "ix_xn_automation_idempotency_expires",
                table: "xn_automation_idempotency",
                column: "expires_at");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "xn_automation_idempotency");
            migrationBuilder.DropTable(name: "xn_automation_schedules");
            migrationBuilder.DropTable(name: "xn_automation_dead_letters");
            migrationBuilder.DropTable(name: "xn_automation_executions");
            migrationBuilder.DropTable(name: "xn_automation_runtime_state");
            migrationBuilder.DropTable(name: "xn_automation_configuration");
            migrationBuilder.DropTable(name: "xn_tenant_automations");
            migrationBuilder.DropTable(name: "xn_automation_versions");
            migrationBuilder.DropTable(name: "xn_automation_registry");
        }
    }
}
