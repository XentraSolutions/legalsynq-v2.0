using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Data.Migrations
{
    /// <summary>
    /// MS-BILL-WRITE-005 — append-only invoice adjustment ledger.
    /// New <c>invoice_adjustments</c> table; FK to <c>invoices</c>
    /// is Restrict (matches refunds). Indexes on TenantId,
    /// InvoiceId, CustomerId mirror the read paths the service /
    /// repository expects.
    ///
    /// Per WRITE-001..004 precedent: this migration .cs file is the
    /// authoritative DDL applied at runtime. The companion
    /// Designer.cs is a minimal placeholder; the maintainer must
    /// regenerate the snapshot via <c>dotnet ef migrations add</c>
    /// in a dotnet-SDK-equipped environment before the next
    /// schema-touching prompt. See the WRITE-005 report's
    /// "Known gaps" section for context.
    /// </summary>
    public partial class AddInvoiceAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice_adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    InvoiceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CustomerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReferenceNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoice_adjustments_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_adjustments_CustomerId",
                table: "invoice_adjustments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_adjustments_InvoiceId",
                table: "invoice_adjustments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_adjustments_TenantId",
                table: "invoice_adjustments",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_adjustments");
        }
    }
}
