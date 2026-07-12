using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migration 4 — Email foundation hardening:
    ///
    ///   1. Soft delete support on xn_email_sources:
    ///      +is_deleted  TINYINT(1) NOT NULL DEFAULT 0
    ///      +deleted_at  DATETIME(6)        DEFAULT NULL
    ///      +deleted_by  CHAR(36)           DEFAULT NULL
    ///      +ix_xn_email_sources_not_deleted  (tenant_id, is_deleted) — active-source reads
    ///
    ///   2. New table xn_email_settings — per-tenant Email module configuration.
    ///      One row per tenant; GetOrCreate pattern ensures initialization.
    ///      No credential columns.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class AddSoftDeleteAndSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Soft delete columns on xn_email_sources ────────────────────
            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "xn_email_sources",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "xn_email_sources",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "xn_email_sources",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_xn_email_sources_not_deleted",
                table: "xn_email_sources",
                columns: new[] { "tenant_id", "is_deleted" });

            // ── 2. New table xn_email_settings ────────────────────────────────
            migrationBuilder.CreateTable(
                name: "xn_email_settings",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    connection_timeout_seconds = table.Column<int>(nullable: false, defaultValue: 30),
                    allowed_provider_types = table.Column<string>(maxLength: 500, nullable: false,
                        defaultValue: "M365,GoogleWorkspace,Imap,Pop3,ExchangeImap"),
                    validation_retry_limit = table.Column<int>(nullable: false, defaultValue: 2),
                    validation_history_retention_days = table.Column<int>(nullable: false, defaultValue: 90),
                    allowed_ports = table.Column<string>(maxLength: 200, nullable: false,
                        defaultValue: "993,995,443"),
                    require_tls = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    allow_custom_hosts = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    ssrf_policy_mode = table.Column<string>(maxLength: 50, nullable: false,
                        defaultValue: "Strict"),
                    default_source_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    version = table.Column<int>(nullable: false, defaultValue: 0),
                    updated_by = table.Column<string>(type: "char(36)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_email_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_email_settings_tenant_id_unique",
                table: "xn_email_settings",
                column: "tenant_id",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "xn_email_settings");
            migrationBuilder.DropIndex(name: "ix_xn_email_sources_not_deleted", table: "xn_email_sources");
            migrationBuilder.DropColumn(name: "is_deleted", table: "xn_email_sources");
            migrationBuilder.DropColumn(name: "deleted_at", table: "xn_email_sources");
            migrationBuilder.DropColumn(name: "deleted_by", table: "xn_email_sources");
        }
    }
}
