using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSubdomainSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "tenant_Tenants",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceUrl",
                table: "tenant_Tenants",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            var baseDomain = NormalizeBaseDomain(
                Environment.GetEnvironmentVariable("PLATFORM_BASE_DOMAIN") ?? "legalsynq.com");

            migrationBuilder.Sql($"""
                UPDATE `tenant_Tenants`
                SET `WorkspaceUrl` = LOWER(CONCAT(`Subdomain`, '.{EscapeSqlLiteral(baseDomain)}'))
                WHERE `Subdomain` IS NOT NULL
                  AND `Subdomain` <> ''
                  AND (`WorkspaceUrl` IS NULL OR `WorkspaceUrl` = '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "tenant_Tenants");

            migrationBuilder.DropColumn(
                name: "WorkspaceUrl",
                table: "tenant_Tenants");
        }

        private static string NormalizeBaseDomain(string value) =>
            value.Trim()
                .ToLowerInvariant()
                .Replace("https://", string.Empty, StringComparison.Ordinal)
                .Replace("http://", string.Empty, StringComparison.Ordinal)
                .Trim('/')
                .Trim('.');

        private static string EscapeSqlLiteral(string value) =>
            value.Replace("'", "''", StringComparison.Ordinal);
    }
}
