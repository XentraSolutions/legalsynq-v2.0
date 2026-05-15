using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Data.Migrations
{
    /// <summary>
    /// MS-BILL-INT-001 — append-only delivery-lifecycle columns on
    /// <c>customer_statements</c>. Tracks the most-recent send
    /// attempt's deterministic outcome plus an aggregate retry
    /// counter. Snapshot content columns
    /// (<c>StatementSnapshotJson</c>, <c>HtmlSnapshot</c>) and
    /// totals are NOT touched — immutability of the snapshot is
    /// preserved at the migration level too.
    ///
    /// All columns are nullable except <c>DeliveryRetryCount</c>
    /// (default 0) so existing rows backfill without a data step.
    ///
    /// Per WRITE-001..005 precedent: this migration .cs is the
    /// authoritative DDL applied at runtime. The companion
    /// Designer.cs is a minimal placeholder; the maintainer must
    /// regenerate the snapshot via <c>dotnet ef migrations add</c>
    /// in a dotnet-SDK-equipped environment before the next
    /// schema-touching prompt. See the INT-001 report's "Known
    /// gaps" section for context.
    /// </summary>
    public partial class AddStatementDeliveryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryProvider",
                table: "customer_statements",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "customer_statements",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryId",
                table: "customer_statements",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCorrelationId",
                table: "customer_statements",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryRecipientEmail",
                table: "customer_statements",
                type: "varchar(320)",
                maxLength: 320,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeliverySentBy",
                table: "customer_statements",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryAttemptedAtUtc",
                table: "customer_statements",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryLastSentAtUtc",
                table: "customer_statements",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryFailureReason",
                table: "customer_statements",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryRetryCount",
                table: "customer_statements",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DeliveryRetryCount", table: "customer_statements");
            migrationBuilder.DropColumn(name: "DeliveryFailureReason", table: "customer_statements");
            migrationBuilder.DropColumn(name: "DeliveryLastSentAtUtc", table: "customer_statements");
            migrationBuilder.DropColumn(name: "DeliveryAttemptedAtUtc", table: "customer_statements");
            migrationBuilder.DropColumn(name: "DeliverySentBy", table: "customer_statements");
            migrationBuilder.DropColumn(name: "DeliveryRecipientEmail", table: "customer_statements");
            migrationBuilder.DropColumn(name: "DeliveryCorrelationId", table: "customer_statements");
            migrationBuilder.DropColumn(name: "DeliveryId", table: "customer_statements");
            migrationBuilder.DropColumn(name: "DeliveryStatus", table: "customer_statements");
            migrationBuilder.DropColumn(name: "DeliveryProvider", table: "customer_statements");
        }
    }
}
