using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantBilling.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTemplateStampToInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceTemplateId",
                table: "invoices",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "TemplateAccentColor",
                table: "invoices",
                type: "varchar(7)",
                maxLength: 7,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "TemplateDisplayBillingAddress",
                table: "invoices",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TemplateDisplayPaymentInstructions",
                table: "invoices",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TemplateDisplayTerms",
                table: "invoices",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateFooterText",
                table: "invoices",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TemplateHeaderText",
                table: "invoices",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TemplateLogoUrl",
                table: "invoices",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TemplateMemoPlaceholder",
                table: "invoices",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "invoices",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TemplateOwnerType",
                table: "invoices",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TemplatePaymentInstructions",
                table: "invoices",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "TemplateStampedAtUtc",
                table: "invoices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateTermsText",
                table: "invoices",
                type: "varchar(8000)",
                maxLength: 8000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_InvoiceTemplateId",
                table: "invoices",
                column: "InvoiceTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_InvoiceTemplateId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceTemplateId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateAccentColor",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateDisplayBillingAddress",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateDisplayPaymentInstructions",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateDisplayTerms",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateFooterText",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateHeaderText",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateLogoUrl",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateMemoPlaceholder",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateOwnerType",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplatePaymentInstructions",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateStampedAtUtc",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateTermsText",
                table: "invoices");
        }
    }
}
