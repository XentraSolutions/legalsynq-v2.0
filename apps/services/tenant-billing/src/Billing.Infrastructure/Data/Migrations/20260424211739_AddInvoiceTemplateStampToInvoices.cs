using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTemplateStampToInvoices : Migration
    {
        private static void AddColumnIfMissing(MigrationBuilder migrationBuilder, string table, string column, string definition)
        {
            migrationBuilder.Sql($"""
                SET @ddl = IF(
                    EXISTS(
                        SELECT 1
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE()
                          AND TABLE_NAME = '{table}'
                          AND COLUMN_NAME = '{column}'),
                    'SELECT 1',
                    'ALTER TABLE `{table}` ADD COLUMN `{column}` {definition}');
                PREPARE stmt FROM @ddl;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        private static void CreateIndexIfMissing(MigrationBuilder migrationBuilder, string table, string index, string columns, bool unique = false)
        {
            var createStatement = unique
                ? $"CREATE UNIQUE INDEX `{index}` ON `{table}` ({columns})"
                : $"CREATE INDEX `{index}` ON `{table}` ({columns})";

            migrationBuilder.Sql($"""
                SET @ddl = IF(
                    EXISTS(
                        SELECT 1
                        FROM INFORMATION_SCHEMA.STATISTICS
                        WHERE TABLE_SCHEMA = DATABASE()
                          AND TABLE_NAME = '{table}'
                          AND INDEX_NAME = '{index}'),
                    'SELECT 1',
                    '{createStatement}');
                PREPARE stmt FROM @ddl;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        private static void ModifyColumnIfNeeded(MigrationBuilder migrationBuilder, string table, string column, string definition, string expectedDataType)
        {
            migrationBuilder.Sql($"""
                SET @ddl = IF(
                    EXISTS(
                        SELECT 1
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE()
                          AND TABLE_NAME = '{table}'
                          AND COLUMN_NAME = '{column}'
                          AND DATA_TYPE <> '{expectedDataType}'),
                    'ALTER TABLE `{table}` MODIFY COLUMN `{column}` {definition}',
                    'SELECT 1');
                PREPARE stmt FROM @ddl;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddColumnIfMissing(migrationBuilder, "invoices", "InvoiceTemplateId", "char(36) COLLATE ascii_general_ci NULL");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateAccentColor", "varchar(7) CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateDisplayBillingAddress", "tinyint(1) NOT NULL DEFAULT 0");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateDisplayPaymentInstructions", "tinyint(1) NOT NULL DEFAULT 0");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateDisplayTerms", "tinyint(1) NOT NULL DEFAULT 0");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateFooterText", "varchar(4000) CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateHeaderText", "varchar(2000) CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateLogoUrl", "varchar(1000) CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateMemoPlaceholder", "varchar(2000) CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateName", "varchar(200) CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateOwnerType", "varchar(16) CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplatePaymentInstructions", "varchar(4000) CHARACTER SET utf8mb4 NULL");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateStampedAtUtc", "datetime(6) NULL");
            AddColumnIfMissing(migrationBuilder, "invoices", "TemplateTermsText", "longtext CHARACTER SET utf8mb4 NULL");

            ModifyColumnIfNeeded(migrationBuilder, "invoices", "TemplateHeaderText", "longtext CHARACTER SET utf8mb4 NULL", "longtext");
            ModifyColumnIfNeeded(migrationBuilder, "invoices", "TemplateFooterText", "longtext CHARACTER SET utf8mb4 NULL", "longtext");
            ModifyColumnIfNeeded(migrationBuilder, "invoices", "TemplatePaymentInstructions", "longtext CHARACTER SET utf8mb4 NULL", "longtext");
            ModifyColumnIfNeeded(migrationBuilder, "invoices", "TemplateMemoPlaceholder", "longtext CHARACTER SET utf8mb4 NULL", "longtext");
            ModifyColumnIfNeeded(migrationBuilder, "invoices", "TemplateTermsText", "longtext CHARACTER SET utf8mb4 NULL", "longtext");

            CreateIndexIfMissing(migrationBuilder, "invoices", "IX_invoices_InvoiceTemplateId", "`InvoiceTemplateId`");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_InvoiceTemplateId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceTemplateId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateAccentColor",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateDisplayBillingAddress",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateDisplayPaymentInstructions",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateDisplayTerms",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateFooterText",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateHeaderText",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateLogoUrl",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateMemoPlaceholder",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateOwnerType",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplatePaymentInstructions",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateStampedAtUtc",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TemplateTermsText",
                table: "invoices");
        }
    }
}
