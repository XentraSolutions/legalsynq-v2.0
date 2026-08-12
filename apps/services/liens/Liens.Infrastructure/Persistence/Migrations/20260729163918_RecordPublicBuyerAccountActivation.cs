using System;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LiensDbContext))]
    [Migration("20260729163918_RecordPublicBuyerAccountActivation")]
    public partial class RecordPublicBuyerAccountActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddColumnIfMissing(
                migrationBuilder,
                "liens_SellingBuyerAccessLinks",
                "AccountActivatedAtUtc",
                "datetime(6) NULL");

            AddColumnIfMissing(
                migrationBuilder,
                "liens_SellingBuyerAccessLinks",
                "AccountActivatedEmail",
                "varchar(320) CHARACTER SET utf8mb4 NULL");

            AddColumnIfMissing(
                migrationBuilder,
                "liens_SellingBuyerAccessLinks",
                "AccountActivatedUserId",
                "char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountActivatedAtUtc",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "AccountActivatedEmail",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "AccountActivatedUserId",
                table: "liens_SellingBuyerAccessLinks");
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
    }
}
