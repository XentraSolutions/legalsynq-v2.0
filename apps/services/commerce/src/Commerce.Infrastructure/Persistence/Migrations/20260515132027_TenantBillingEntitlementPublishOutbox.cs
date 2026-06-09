using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantBillingEntitlementPublishOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_billing_entitlement_publish_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BillingAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TriggerSource = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastOutcome = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastReason = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastHttpStatus = table.Column<int>(type: "int", nullable: true),
                    LastErrorSummary = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LockedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LockId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_billing_entitlement_publish_outbox", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_tb_entitlement_outbox_billing_account",
                table: "tenant_billing_entitlement_publish_outbox",
                column: "BillingAccountId");

            migrationBuilder.CreateIndex(
                name: "ix_tb_entitlement_outbox_created_at",
                table: "tenant_billing_entitlement_publish_outbox",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "ix_tb_entitlement_outbox_next_attempt",
                table: "tenant_billing_entitlement_publish_outbox",
                column: "NextAttemptAtUtc");

            migrationBuilder.CreateIndex(
                name: "ix_tb_entitlement_outbox_status",
                table: "tenant_billing_entitlement_publish_outbox",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_tb_entitlement_outbox_status_next_attempt",
                table: "tenant_billing_entitlement_publish_outbox",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_tb_entitlement_outbox_trigger_source",
                table: "tenant_billing_entitlement_publish_outbox",
                column: "TriggerSource");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_billing_entitlement_publish_outbox");
        }
    }
}
