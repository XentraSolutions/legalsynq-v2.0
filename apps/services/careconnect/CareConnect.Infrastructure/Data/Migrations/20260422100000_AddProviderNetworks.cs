using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// CC2-INT-B06 — Adds provider network management tables.
///
/// Tables:
///   cc_ProviderNetworks  — tenant-scoped named networks (soft-delete via IsDeleted)
///   cc_NetworkProviders  — join table: network ↔ provider (unique per network+provider)
///
/// Indexes:
///   cc_ProviderNetworks (TenantId, Name) WHERE IsDeleted = 0   — unique active name per tenant
///   cc_NetworkProviders (ProviderNetworkId, ProviderId)         — unique membership
///   cc_NetworkProviders (TenantId)                             — tenant list queries
/// </summary>
[Migration("20260422100000_AddProviderNetworks")]
public partial class AddProviderNetworks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "cc_ProviderNetworks",
            columns: table => new
            {
                Id            = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId      = table.Column<Guid>(type: "char(36)", nullable: false),
                Name          = table.Column<string>(maxLength: 200, nullable: false),
                Description   = table.Column<string>(maxLength: 1000, nullable: false, defaultValue: ""),
                IsDeleted     = table.Column<bool>(nullable: false, defaultValue: false),
                CreatedAtUtc  = table.Column<DateTime>(nullable: false),
                UpdatedAtUtc  = table.Column<DateTime>(nullable: false),
                CreatedByUserId = table.Column<string>(nullable: true),
                UpdatedByUserId = table.Column<string>(nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cc_ProviderNetworks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "cc_NetworkProviders",
            columns: table => new
            {
                Id               = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId         = table.Column<Guid>(type: "char(36)", nullable: false),
                ProviderNetworkId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProviderId       = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc     = table.Column<DateTime>(nullable: false),
                UpdatedAtUtc     = table.Column<DateTime>(nullable: false),
                CreatedByUserId  = table.Column<string>(nullable: true),
                UpdatedByUserId  = table.Column<string>(nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cc_NetworkProviders", x => x.Id);

                table.ForeignKey(
                    name: "FK_cc_NetworkProviders_cc_ProviderNetworks_ProviderNetworkId",
                    column: x => x.ProviderNetworkId,
                    principalTable: "cc_ProviderNetworks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);

                table.ForeignKey(
                    name: "FK_cc_NetworkProviders_cc_Providers_ProviderId",
                    column: x => x.ProviderId,
                    principalTable: "cc_Providers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // MySQL 8.0 does not support partial/filtered indexes — omit filter here.
        // Uniqueness is enforced at the application layer (NetworkService.NameExistsAsync).
        migrationBuilder.CreateIndex(
            name: "IX_cc_ProviderNetworks_TenantId_Name",
            table: "cc_ProviderNetworks",
            columns: new[] { "TenantId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_cc_NetworkProviders_ProviderNetworkId_ProviderId",
            table: "cc_NetworkProviders",
            columns: new[] { "ProviderNetworkId", "ProviderId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_cc_NetworkProviders_TenantId",
            table: "cc_NetworkProviders",
            column: "TenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "cc_NetworkProviders");
        migrationBuilder.DropTable(name: "cc_ProviderNetworks");
    }
}
