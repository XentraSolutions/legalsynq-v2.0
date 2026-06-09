using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Data.Migrations
{
    /// <summary>
    /// MS-BILL-ERP-001 — Append-safe accounting export lifecycle
    /// table. One row per ERP export attempt; the row is INSERTed
    /// in <c>Pending</c> state and UPDATEd exactly once with the
    /// terminal status + payload JSON.
    ///
    /// <para>
    /// Composite unique index on
    /// <c>(TenantId, Provider, ExportType, WindowFromUtc, WindowToUtc, Fingerprint)</c>
    /// is intentionally NOT unique at the SQL level — duplicate
    /// prevention happens at the application level
    /// (<c>AccountingExportService.RunAsync</c>) so a previous
    /// <c>Failed</c> / <c>ProviderUnavailable</c> row does not
    /// block a re-attempt. Replacing this with a unique index in
    /// the future would require also clearing failed rows on
    /// retry, which is explicitly out of scope for ERP-001.
    /// </para>
    ///
    /// <para>
    /// Per WRITE-001..005 precedent: this migration .cs file is the
    /// authoritative DDL applied at runtime. The companion
    /// Designer.cs is a minimal placeholder; the maintainer must
    /// regenerate the snapshot via <c>dotnet ef migrations add</c>
    /// in a dotnet-SDK-equipped environment before the next
    /// schema-touching prompt.
    /// </para>
    /// </summary>
    public partial class AddAccountingExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_exports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Provider = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExportType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WindowFromUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WindowToUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExternalReferenceId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FailureReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdempotencyKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Fingerprint = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InvoiceCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PaymentCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AdjustmentCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    JournalEntryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayloadJson = table.Column<string>(type: "LONGTEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_exports", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_exports_TenantId",
                table: "accounting_exports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_exports_TenantId_Fingerprint",
                table: "accounting_exports",
                columns: new[] { "TenantId", "Fingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_exports_TenantId_RequestedAtUtc",
                table: "accounting_exports",
                columns: new[] { "TenantId", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_exports");
        }
    }
}
