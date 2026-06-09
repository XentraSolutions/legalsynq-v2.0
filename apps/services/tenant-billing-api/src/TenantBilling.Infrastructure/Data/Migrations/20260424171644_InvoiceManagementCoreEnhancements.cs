using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantBilling.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceManagementCoreEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedAt",
                table: "invoices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_DueDate",
                table: "invoices",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Status",
                table: "invoices",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_DueDate",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_Status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuedAt",
                table: "invoices");
        }
    }
}
