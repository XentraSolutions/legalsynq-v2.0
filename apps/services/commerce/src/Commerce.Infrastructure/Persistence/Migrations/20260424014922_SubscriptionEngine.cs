using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BillingAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SubscriptionNumber = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CurrentPeriodStartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CurrentPeriodEndUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TrialStartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TrialEndUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CancellationReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscriptions_billing_accounts_BillingAccountId",
                        column: x => x.BillingAccountId,
                        principalTable: "billing_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "subscription_changes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SubscriptionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    FromPlanId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ToPlanId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FromPriceId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ToPriceId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    EffectiveAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ProrationBehavior = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscription_changes_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "subscription_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SubscriptionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PlanId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PriceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BillingInterval = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscription_items_catalog_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "catalog_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscription_items_catalog_prices_PriceId",
                        column: x => x.PriceId,
                        principalTable: "catalog_prices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscription_items_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_changes_subscription_created",
                table: "subscription_changes",
                columns: new[] { "SubscriptionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_items_PlanId",
                table: "subscription_items",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_items_PriceId",
                table: "subscription_items",
                column: "PriceId");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_items_subscription_id",
                table: "subscription_items",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_items_subscription_status",
                table: "subscription_items",
                columns: new[] { "SubscriptionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_billing_account_id",
                table: "subscriptions",
                column: "BillingAccountId");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_status",
                table: "subscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_subscriptions_subscription_number",
                table: "subscriptions",
                column: "SubscriptionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_changes");

            migrationBuilder.DropTable(
                name: "subscription_items");

            migrationBuilder.DropTable(
                name: "subscriptions");
        }
    }
}
