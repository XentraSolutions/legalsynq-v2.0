using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantBilling.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTemplateDefaultUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultScopeKey",
                table: "invoice_templates",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                computedColumnSql: "(CASE WHEN `IsDefault` = 1 THEN CONCAT(`OwnerType`, '|', IFNULL(`BillingAccountId`, '')) ELSE NULL END)",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "UX_invoice_templates_DefaultScopeKey",
                table: "invoice_templates",
                column: "DefaultScopeKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_invoice_templates_DefaultScopeKey",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "DefaultScopeKey",
                table: "invoice_templates");
        }
    }
}
