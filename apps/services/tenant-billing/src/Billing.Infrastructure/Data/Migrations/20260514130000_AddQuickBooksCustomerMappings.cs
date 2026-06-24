using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Data.Migrations
{
    /// <summary>
    /// MS-BILL-ERP-003 — Operator-curated Billing↔QuickBooks
    /// customer mapping table. One row per
    /// (TenantId, BillingCustomerId) AND per
    /// (TenantId, QuickBooksCustomerId), enforced at the SQL level
    /// by two unique indexes so a Billing customer cannot be
    /// silently double-mapped and a QBO customer cannot be linked
    /// to two distinct Billing customers within the same tenant.
    ///
    /// <para>
    /// Per WRITE-001..005 / ERP-001 precedent: this migration .cs
    /// file is the authoritative DDL applied at runtime. The
    /// companion Designer.cs is a minimal placeholder; the
    /// maintainer must regenerate the snapshot via
    /// <c>dotnet ef migrations add</c> in a dotnet-SDK-equipped
    /// environment before the next schema-touching prompt.
    /// </para>
    /// </summary>
    public partial class AddQuickBooksCustomerMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quickbooks_customer_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BillingCustomerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    QuickBooksCustomerId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QuickBooksDisplayName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MappingStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExportMode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastExportedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quickbooks_customer_mappings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_quickbooks_customer_mappings_TenantId",
                table: "quickbooks_customer_mappings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_quickbooks_customer_mappings_TenantId_BillingCustomerId",
                table: "quickbooks_customer_mappings",
                columns: new[] { "TenantId", "BillingCustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_quickbooks_customer_mappings_TenantId_QuickBooksCustomerId",
                table: "quickbooks_customer_mappings",
                columns: new[] { "TenantId", "QuickBooksCustomerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quickbooks_customer_mappings");
        }
    }
}
