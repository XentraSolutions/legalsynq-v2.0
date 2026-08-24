using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedContactPersonTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL DDL auto-commits, so every operation must tolerate a retry after
            // an interrupted deployment or schema recovery pass.
            SellingSchemaMigrationGuards.AddColumnIfMissing(
                migrationBuilder,
                "liens_ContactPersonTypes",
                "OrgId",
                "char(36) COLLATE ascii_general_ci NULL");
            SellingSchemaMigrationGuards.AddColumnIfMissing(
                migrationBuilder,
                "liens_ContactPersonTypes",
                "TenantId",
                "char(36) COLLATE ascii_general_ci NULL");
            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_ContactPersonTypes",
                "UX_ContactPersonTypes_Scope_CompanyTypeId_Code",
                "(`TenantId`, `OrgId`, `CompanyTypeId`, `Code`)",
                unique: true);
            SellingSchemaMigrationGuards.DropIndexIfExists(
                migrationBuilder,
                "liens_ContactPersonTypes",
                "UX_ContactPersonTypes_CompanyTypeId_Code");
            SellingSchemaMigrationGuards.AddCheckConstraintIfMissing(
                migrationBuilder,
                "liens_ContactPersonTypes",
                "CK_ContactPersonTypes_Scope",
                "(`TenantId` IS NULL AND `OrgId` IS NULL) OR (`TenantId` IS NOT NULL AND `OrgId` IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ContactPersonTypes_Scope_CompanyTypeId_Code",
                table: "liens_ContactPersonTypes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContactPersonTypes_Scope",
                table: "liens_ContactPersonTypes");

            migrationBuilder.DropColumn(
                name: "OrgId",
                table: "liens_ContactPersonTypes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "liens_ContactPersonTypes");

            migrationBuilder.CreateIndex(
                name: "UX_ContactPersonTypes_CompanyTypeId_Code",
                table: "liens_ContactPersonTypes",
                columns: new[] { "CompanyTypeId", "Code" },
                unique: true);
        }
    }
}
