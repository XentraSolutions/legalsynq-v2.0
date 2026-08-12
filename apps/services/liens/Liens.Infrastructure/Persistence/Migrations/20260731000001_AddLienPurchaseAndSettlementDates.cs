using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260731000001_AddLienPurchaseAndSettlementDates")]
public partial class AddLienPurchaseAndSettlementDates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddColumnIfMissing(
            migrationBuilder,
            "liens_Liens",
            "PurchaseDate",
            "date NULL");

        AddColumnIfMissing(
            migrationBuilder,
            "liens_LienSettlements",
            "SettlementDate",
            "date NULL");

        CreateIndexIfMissing(
            migrationBuilder,
            "liens_Liens",
            "IX_Liens_TenantId_PurchaseDate",
            "CREATE INDEX `IX_Liens_TenantId_PurchaseDate` ON `liens_Liens` (`TenantId`, `PurchaseDate`)");

        CreateIndexIfMissing(
            migrationBuilder,
            "liens_LienSettlements",
            "IX_LienSettlements_TenantId_SettlementDate",
            "CREATE INDEX `IX_LienSettlements_TenantId_SettlementDate` ON `liens_LienSettlements` (`TenantId`, `SettlementDate`)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Liens_TenantId_PurchaseDate",
            table: "liens_Liens");

        migrationBuilder.DropIndex(
            name: "IX_LienSettlements_TenantId_SettlementDate",
            table: "liens_LienSettlements");

        migrationBuilder.DropColumn(
            name: "PurchaseDate",
            table: "liens_Liens");

        migrationBuilder.DropColumn(
            name: "SettlementDate",
            table: "liens_LienSettlements");
    }

    private static void AddColumnIfMissing(
        MigrationBuilder migrationBuilder,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        migrationBuilder.Sql($"""
            SET @db = DATABASE();
            SET @ddl = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='{tableName}' AND COLUMN_NAME='{columnName}')=0,
                'ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {columnDefinition}',
                'SELECT 1');
            PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """);
    }

    private static void CreateIndexIfMissing(
        MigrationBuilder migrationBuilder,
        string tableName,
        string indexName,
        string createIndexSql)
    {
        migrationBuilder.Sql($"""
            SET @db = DATABASE();
            SET @ddl = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='{tableName}' AND INDEX_NAME='{indexName}')=0,
                '{createIndexSql}',
                'SELECT 1');
            PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """);
    }
}
