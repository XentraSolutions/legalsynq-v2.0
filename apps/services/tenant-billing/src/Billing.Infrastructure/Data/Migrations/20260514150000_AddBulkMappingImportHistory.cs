using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Data.Migrations
{
    /// <summary>
    /// MS-BILL-ERP-006 — Per-import audit row for the bulk customer
    /// mapping import. Append-only from the application layer; no
    /// updates, no deletes. One composite index on
    /// (TenantId, StartedAtUtc) backs the newest-first list view.
    ///
    /// <para>
    /// Per WRITE-001..005 / ERP-001 / ERP-003 precedent: this
    /// migration .cs file is the authoritative DDL applied at
    /// runtime. The companion Designer.cs is a minimal placeholder;
    /// the maintainer must regenerate the snapshot via
    /// <c>dotnet ef migrations add</c> in a dotnet-SDK-equipped
    /// environment before the next schema-touching prompt.
    /// </para>
    /// </summary>
    public partial class AddBulkMappingImportHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bulk_mapping_import_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    OperatorDisplayName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    AcceptedRows = table.Column<int>(type: "int", nullable: false),
                    WarningRows = table.Column<int>(type: "int", nullable: false),
                    RejectedRows = table.Column<int>(type: "int", nullable: false),
                    SummaryJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdempotencyKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bulk_mapping_import_history", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bulk_mapping_import_history_TenantId",
                table: "bulk_mapping_import_history",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bulk_mapping_import_history_TenantId_StartedAtUtc",
                table: "bulk_mapping_import_history",
                columns: new[] { "TenantId", "StartedAtUtc" });

            // Replay protection: enforce one persisted commit per
            // (tenant, operator-supplied Idempotency-Key) at the DB
            // level so the unique-violation surfaces even if two
            // requests race past the service-level lookup.
            migrationBuilder.CreateIndex(
                name: "UX_bulk_mapping_import_history_TenantId_IdempotencyKey",
                table: "bulk_mapping_import_history",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bulk_mapping_import_history");
        }
    }
}
