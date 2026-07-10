using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds email module tables:
    ///   xn_email_sources         — tenant-scoped email source configurations
    ///   xn_email_provider_settings — per-source provider metadata (no secrets)
    ///   xn_email_validation_history — immutable connectivity validation records
    ///
    /// No plaintext credential columns are present. All credential material is stored
    /// by reference (secret_reference_id / oauth_connection_ref) and resolved at runtime
    /// by ISecretReferenceService.
    ///
    /// No email message, attachment, sync cursor, or delta token columns are included.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class AddEmailModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "xn_email_sources",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    module_key = table.Column<string>(maxLength: 100, nullable: false),
                    display_name = table.Column<string>(maxLength: 200, nullable: false),
                    description = table.Column<string>(maxLength: 1000, nullable: true),
                    provider_type = table.Column<string>(maxLength: 32, nullable: false),
                    auth_type = table.Column<string>(maxLength: 32, nullable: false),
                    email_address = table.Column<string>(maxLength: 320, nullable: false),
                    username = table.Column<string>(maxLength: 255, nullable: true),
                    incoming_host = table.Column<string>(maxLength: 255, nullable: true),
                    incoming_port = table.Column<int>(nullable: true),
                    use_tls = table.Column<bool>(nullable: false),
                    mailbox_folder = table.Column<string>(maxLength: 255, nullable: true),
                    secret_reference_id = table.Column<string>(maxLength: 500, nullable: true),
                    oauth_connection_ref = table.Column<string>(maxLength: 500, nullable: true),
                    enabled = table.Column<bool>(nullable: false),
                    status = table.Column<string>(maxLength: 32, nullable: false),
                    health_status = table.Column<string>(maxLength: 32, nullable: false),
                    validation_status = table.Column<string>(maxLength: 32, nullable: false),
                    last_validated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_successful_validation_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_validation_latency_ms = table.Column<int>(nullable: true),
                    last_connection_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_error_code = table.Column<string>(maxLength: 100, nullable: true),
                    last_error_summary = table.Column<string>(maxLength: 500, nullable: true),
                    created_by = table.Column<string>(type: "char(36)", nullable: true),
                    updated_by = table.Column<string>(type: "char(36)", nullable: true),
                    row_version = table.Column<int>(nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_email_sources", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_email_sources_tenant_id",
                table: "xn_email_sources",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_xn_email_sources_tenant_provider",
                table: "xn_email_sources",
                columns: new[] { "tenant_id", "provider_type" });

            migrationBuilder.CreateIndex(
                name: "ix_xn_email_sources_tenant_status",
                table: "xn_email_sources",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateTable(
                name: "xn_email_provider_settings",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    email_source_id = table.Column<string>(type: "char(36)", nullable: false),
                    provider_type = table.Column<string>(maxLength: 32, nullable: false),
                    configuration_json = table.Column<string>(maxLength: 8000, nullable: true),
                    configuration_version = table.Column<int>(nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_email_provider_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_email_prov_settings_source",
                table: "xn_email_provider_settings",
                column: "email_source_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xn_email_prov_settings_tenant",
                table: "xn_email_provider_settings",
                column: "tenant_id");

            migrationBuilder.CreateTable(
                name: "xn_email_validation_history",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    email_source_id = table.Column<string>(type: "char(36)", nullable: false),
                    provider_type = table.Column<string>(maxLength: 32, nullable: false),
                    validation_type = table.Column<string>(maxLength: 50, nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    duration_ms = table.Column<int>(nullable: true),
                    result = table.Column<string>(maxLength: 32, nullable: false),
                    error_code = table.Column<string>(maxLength: 100, nullable: true),
                    error_summary = table.Column<string>(maxLength: 500, nullable: true),
                    correlation_id = table.Column<string>(maxLength: 200, nullable: true),
                    actor_id = table.Column<string>(type: "char(36)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_email_validation_history", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_email_val_history_tenant",
                table: "xn_email_validation_history",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_xn_email_val_history_source",
                table: "xn_email_validation_history",
                column: "email_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_xn_email_val_history_started",
                table: "xn_email_validation_history",
                column: "started_at");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "xn_email_validation_history");
            migrationBuilder.DropTable(name: "xn_email_provider_settings");
            migrationBuilder.DropTable(name: "xn_email_sources");
        }
    }
}
