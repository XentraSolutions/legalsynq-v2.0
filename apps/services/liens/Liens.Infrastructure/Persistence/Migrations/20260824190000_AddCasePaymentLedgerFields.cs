using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260824190000_AddCasePaymentLedgerFields")]
public partial class AddCasePaymentLedgerFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder, "liens_SettlementPaymentDetails", "ReceiptId", "char(36) COLLATE ascii_general_ci NULL");
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder, "liens_SettlementPaymentDetails", "PaymentMethod", "varchar(50) CHARACTER SET utf8mb4 NULL");
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder, "liens_SettlementPaymentDetails", "SettlementType", "varchar(80) CHARACTER SET utf8mb4 NULL");
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder, "liens_SettlementPaymentDetails", "SettlementStatus", "varchar(80) CHARACTER SET utf8mb4 NULL");
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder, "liens_SettlementPaymentDetails", "DetailsContext", "varchar(300) CHARACTER SET utf8mb4 NULL");
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder, "liens_SettlementPaymentDetails", "PostingStatus", "varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Posted'");
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder, "liens_SettlementPaymentDetails", "VoidedAtUtc", "datetime(6) NULL");
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder, "liens_SettlementPaymentDetails", "VoidedByUserId", "char(36) COLLATE ascii_general_ci NULL");
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder, "liens_SettlementPaymentDetails", "VoidReason", "varchar(500) CHARACTER SET utf8mb4 NULL");

        SellingSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "liens_SettlementPaymentDetails",
            "IX_SettlementPayments_Tenant_Case_Status_Date",
            "(`TenantId`, `CaseId`, `PostingStatus`, `PaymentDate`)");
        SellingSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "liens_SettlementPaymentDetails",
            "IX_SettlementPayments_Tenant_Receipt",
            "(`TenantId`, `ReceiptId`)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SettlementPayments_Tenant_Case_Status_Date",
            table: "liens_SettlementPaymentDetails");
        migrationBuilder.DropIndex(
            name: "IX_SettlementPayments_Tenant_Receipt",
            table: "liens_SettlementPaymentDetails");
        migrationBuilder.DropColumn(name: "ReceiptId", table: "liens_SettlementPaymentDetails");
        migrationBuilder.DropColumn(name: "PaymentMethod", table: "liens_SettlementPaymentDetails");
        migrationBuilder.DropColumn(name: "SettlementType", table: "liens_SettlementPaymentDetails");
        migrationBuilder.DropColumn(name: "SettlementStatus", table: "liens_SettlementPaymentDetails");
        migrationBuilder.DropColumn(name: "DetailsContext", table: "liens_SettlementPaymentDetails");
        migrationBuilder.DropColumn(name: "PostingStatus", table: "liens_SettlementPaymentDetails");
        migrationBuilder.DropColumn(name: "VoidedAtUtc", table: "liens_SettlementPaymentDetails");
        migrationBuilder.DropColumn(name: "VoidedByUserId", table: "liens_SettlementPaymentDetails");
        migrationBuilder.DropColumn(name: "VoidReason", table: "liens_SettlementPaymentDetails");
    }
}
