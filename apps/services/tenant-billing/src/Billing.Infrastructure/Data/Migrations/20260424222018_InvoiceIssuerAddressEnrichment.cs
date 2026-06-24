using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceIssuerAddressEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssuerAddressLine1",
                table: "invoices",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerAddressLine2",
                table: "invoices",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerCity",
                table: "invoices",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerCountry",
                table: "invoices",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerDisplayName",
                table: "invoices",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerEmail",
                table: "invoices",
                type: "varchar(320)",
                maxLength: 320,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerLegalName",
                table: "invoices",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerPhone",
                table: "invoices",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerPostalCode",
                table: "invoices",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "IssuerStampedAtUtc",
                table: "invoices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssuerStateRegion",
                table: "invoices",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerTaxId",
                table: "invoices",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerWebsite",
                table: "invoices",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerAddressLine1",
                table: "invoice_templates",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerAddressLine2",
                table: "invoice_templates",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerCity",
                table: "invoice_templates",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerCountry",
                table: "invoice_templates",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerDisplayName",
                table: "invoice_templates",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerEmail",
                table: "invoice_templates",
                type: "varchar(320)",
                maxLength: 320,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerLegalName",
                table: "invoice_templates",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerPhone",
                table: "invoice_templates",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerPostalCode",
                table: "invoice_templates",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerStateRegion",
                table: "invoice_templates",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerTaxId",
                table: "invoice_templates",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IssuerWebsite",
                table: "invoice_templates",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine1",
                table: "customers",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine2",
                table: "customers",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BillingCity",
                table: "customers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BillingCountry",
                table: "customers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BillingPostalCode",
                table: "customers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BillingStateRegion",
                table: "customers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssuerAddressLine1",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerAddressLine2",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerCity",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerCountry",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerDisplayName",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerEmail",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerLegalName",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerPhone",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerPostalCode",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerStampedAtUtc",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerStateRegion",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerTaxId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerWebsite",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "IssuerAddressLine1",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerAddressLine2",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerCity",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerCountry",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerDisplayName",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerEmail",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerLegalName",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerPhone",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerPostalCode",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerStateRegion",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerTaxId",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "IssuerWebsite",
                table: "invoice_templates");

            migrationBuilder.DropColumn(
                name: "BillingAddressLine1",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BillingAddressLine2",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BillingCity",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BillingCountry",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BillingPostalCode",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BillingStateRegion",
                table: "customers");
        }
    }
}
