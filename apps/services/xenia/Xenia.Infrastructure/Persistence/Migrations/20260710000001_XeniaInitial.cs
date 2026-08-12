using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class XeniaInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "xn_modules",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    module_key = table.Column<string>(maxLength: 100, nullable: false),
                    name = table.Column<string>(maxLength: 200, nullable: false),
                    version = table.Column<string>(maxLength: 50, nullable: false),
                    description = table.Column<string>(maxLength: 1000, nullable: true),
                    global_enabled = table.Column<bool>(nullable: false),
                    status = table.Column<int>(nullable: false),
                    configuration_namespace = table.Column<string>(maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_modules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_modules_module_key",
                table: "xn_modules",
                column: "module_key",
                unique: true);

            migrationBuilder.CreateTable(
                name: "xn_tenant_modules",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    module_key = table.Column<string>(maxLength: 100, nullable: false),
                    enabled = table.Column<bool>(nullable: false),
                    module_configuration = table.Column<string>(maxLength: 8000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_tenant_modules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_tenant_modules_tenant_id",
                table: "xn_tenant_modules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_xn_tenant_modules_tenant_module",
                table: "xn_tenant_modules",
                columns: new[] { "tenant_id", "module_key" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "xn_platform_adapters",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    adapter_key = table.Column<string>(maxLength: 100, nullable: false),
                    adapter_type = table.Column<int>(nullable: false),
                    name = table.Column<string>(maxLength: 200, nullable: false),
                    version = table.Column<string>(maxLength: 50, nullable: false),
                    configuration_status = table.Column<int>(nullable: false),
                    availability_status = table.Column<int>(nullable: false),
                    health_status = table.Column<int>(nullable: false),
                    last_health_check_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    diagnostic_message = table.Column<string>(maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_platform_adapters", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_platform_adapters_key",
                table: "xn_platform_adapters",
                column: "adapter_key",
                unique: true);

            migrationBuilder.CreateTable(
                name: "xn_configuration",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    scope_type = table.Column<int>(nullable: false),
                    scope_id = table.Column<string>(maxLength: 300, nullable: true),
                    @namespace = table.Column<string>(name: "namespace", maxLength: 200, nullable: false),
                    configuration_key = table.Column<string>(maxLength: 200, nullable: false),
                    configuration_value = table.Column<string>(maxLength: 4000, nullable: true),
                    value_type = table.Column<string>(maxLength: 50, nullable: true),
                    is_secret = table.Column<bool>(nullable: false),
                    version = table.Column<int>(nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_configuration", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_configuration_scope_key",
                table: "xn_configuration",
                columns: new[] { "scope_type", "scope_id", "namespace", "configuration_key" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "xn_tenant_settings",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    enabled = table.Column<bool>(nullable: false),
                    settings = table.Column<string>(maxLength: 8000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_tenant_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_xn_tenant_settings_tenant_id",
                table: "xn_tenant_settings",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "xn_tenant_settings");
            migrationBuilder.DropTable(name: "xn_configuration");
            migrationBuilder.DropTable(name: "xn_platform_adapters");
            migrationBuilder.DropTable(name: "xn_tenant_modules");
            migrationBuilder.DropTable(name: "xn_modules");
        }
    }
}
