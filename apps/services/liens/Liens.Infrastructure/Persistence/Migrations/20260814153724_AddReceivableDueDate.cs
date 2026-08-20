using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivableDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL DDL auto-commits. Guard every operation so an interrupted
            // deployment can safely resume without guessed schema state.
            SellingSchemaMigrationGuards.AddColumnIfMissing(
                migrationBuilder,
                "liens_Liens",
                "ReceivableDueDate",
                "date NULL");
            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_SettlementPaymentDetails",
                "IX_SettlementPayments_Tenant_Date_Deleted",
                "(`TenantId`, `PaymentDate`, `IsDeleted`)");
            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_SettlementPaymentDetails",
                "IX_SettlementPayments_Tenant_Lien_Deleted",
                "(`TenantId`, `LienId`, `IsDeleted`)");
            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_Liens",
                "IX_Liens_Tenant_Seller_FundingCompanyCompanyId",
                "(`TenantId`, `SellingOrgId`, `FundingCompanyCompanyId`)");
            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_Liens",
                "IX_Liens_Tenant_Seller_ReceivableDueDate",
                "(`TenantId`, `SellingOrgId`, `ReceivableDueDate`)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettlementPayments_Tenant_Date_Deleted",
                table: "liens_SettlementPaymentDetails");

            migrationBuilder.DropIndex(
                name: "IX_SettlementPayments_Tenant_Lien_Deleted",
                table: "liens_SettlementPaymentDetails");

            migrationBuilder.DropIndex(
                name: "IX_Liens_Tenant_Seller_FundingCompanyCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_Liens_Tenant_Seller_ReceivableDueDate",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "ReceivableDueDate",
                table: "liens_Liens");
        }
    }
}
