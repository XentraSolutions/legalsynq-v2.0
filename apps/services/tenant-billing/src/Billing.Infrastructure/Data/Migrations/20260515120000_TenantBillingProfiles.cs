using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Data.Migrations
{
    /// <summary>
    /// TB-DATA-01 — TenantId ↔ Commerce BillingAccountId mapping
    /// (<c>tenant_billing_profiles</c>). One row per
    /// <c>(TenantId, BillingAccountId)</c> binding, with the lifecycle
    /// described on <c>TenantBillingProfileStatus</c>.
    ///
    /// <para>
    /// At-most-one-Open invariants (per tenant AND per billing account)
    /// are enforced at the SQL level by two STORED generated scope-key
    /// columns that are non-null only for non-Closed rows, plus a UNIQUE
    /// index on each. Closed rows have NULL scope keys and never collide,
    /// preserving an audit trail of historical mappings.
    /// </para>
    ///
    /// <para>
    /// Per WRITE-001..005 / ERP-001 / ERP-003 precedent: this migration
    /// .cs file is the authoritative DDL applied at runtime. The companion
    /// Designer.cs is a minimal placeholder; the maintainer must
    /// regenerate the project-level model snapshot via
    /// <c>dotnet ef migrations add</c> in a dotnet-SDK-equipped
    /// environment before the next schema-touching prompt.
    /// </para>
    /// </summary>
    public partial class TenantBillingProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_billing_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BillingAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    HostPlatformKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExternalTenantId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Mode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantOpenScopeKey = table.Column<string>(
                            type: "varchar(36)",
                            maxLength: 36,
                            nullable: true,
                            computedColumnSql:
                                "(CASE WHEN `Status` <> 'Closed' THEN `TenantId` ELSE NULL END)",
                            stored: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BillingAccountOpenScopeKey = table.Column<string>(
                            type: "varchar(36)",
                            maxLength: 36,
                            nullable: true,
                            computedColumnSql:
                                "(CASE WHEN `Status` <> 'Closed' THEN `BillingAccountId` ELSE NULL END)",
                            stored: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_billing_profiles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_billing_profiles_TenantId",
                table: "tenant_billing_profiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_billing_profiles_BillingAccountId",
                table: "tenant_billing_profiles",
                column: "BillingAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_tenant_billing_profiles_TenantOpenScopeKey",
                table: "tenant_billing_profiles",
                column: "TenantOpenScopeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_tenant_billing_profiles_BillingAccountOpenScopeKey",
                table: "tenant_billing_profiles",
                column: "BillingAccountOpenScopeKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tenant_billing_profiles");
        }
    }
}
