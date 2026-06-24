using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// MS-BILL-WRITE-002 — append-only audit columns recording WHEN a
    /// payment was reversed and WHY. Both columns are nullable so existing
    /// Recorded rows remain valid (no data backfill required) and so the
    /// schema can be rolled back without data loss. The original financial
    /// columns on the row are NEVER modified by the reversal flow; only
    /// <c>Status</c> flips from <c>"Recorded"</c> to <c>"Voided"</c> and
    /// these two audit columns are populated.
    /// </remarks>
    public partial class AddPaymentReversalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAt",
                table: "payments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "payments",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "payments");
        }
    }
}
