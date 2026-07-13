using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    public partial class AddXeniaEntitlementSeeds : Migration
    {
        private static readonly DateTime SeededAt = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "idt_Products",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "Name", "Description" },
                values: new object[] { "Xenia", "Tenant-aware AI assistant and agent platform" });

            migrationBuilder.InsertData(
                table: "idt_ProductRoles",
                columns: new[] { "Id", "ProductId", "Code", "Name", "Description", "IsActive", "CreatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000009"), new Guid("10000000-0000-0000-0000-000000000005"), "XENIA_USER",  "Xenia User",  "User access to the Xenia assistant", true, SeededAt },
                    { new Guid("50000000-0000-0000-0000-000000000010"), new Guid("10000000-0000-0000-0000-000000000005"), "XENIA_ADMIN", "Xenia Admin", "Administrative access to Xenia assistant configuration", true, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "idt_Capabilities",
                columns: new[] { "Id", "ProductId", "Code", "Name", "Description", "Category", "IsActive", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("60000000-0000-0000-0000-000000000063"), new Guid("10000000-0000-0000-0000-000000000005"), "SYNQ_AI.assistant:use",    "Use Xenia Assistant",    "Create and use Xenia assistant conversations", "Assistant", true, SeededAt, null, null, null },
                    { new Guid("60000000-0000-0000-0000-000000000064"), new Guid("10000000-0000-0000-0000-000000000005"), "SYNQ_AI.assistant:manage", "Manage Xenia Assistant", "Configure Xenia assistant providers, agents, and quotas", "Assistant", true, SeededAt, null, null, null },
                    { new Guid("60000000-0000-0000-0000-000000000065"), new Guid("10000000-0000-0000-0000-000000000005"), "SYNQ_AI.usage:read",       "View Xenia Usage",       "View Xenia assistant usage and cost telemetry", "Usage", true, SeededAt, null, null, null },
                });

            migrationBuilder.InsertData(
                table: "idt_RoleCapabilities",
                columns: new[] { "ProductRoleId", "CapabilityId" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000009"), new Guid("60000000-0000-0000-0000-000000000063") },
                    { new Guid("50000000-0000-0000-0000-000000000010"), new Guid("60000000-0000-0000-0000-000000000063") },
                    { new Guid("50000000-0000-0000-0000-000000000010"), new Guid("60000000-0000-0000-0000-000000000064") },
                    { new Guid("50000000-0000-0000-0000-000000000010"), new Guid("60000000-0000-0000-0000-000000000065") },
                });

            migrationBuilder.InsertData(
                table: "idt_ProductOrganizationTypeRules",
                columns: new[] { "Id", "ProductId", "ProductRoleId", "OrganizationTypeId", "IsActive", "CreatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("90000000-0000-0000-0000-000000000008"), new Guid("10000000-0000-0000-0000-000000000005"), new Guid("50000000-0000-0000-0000-000000000009"), new Guid("70000000-0000-0000-0000-000000000002"), true, SeededAt },
                    { new Guid("90000000-0000-0000-0000-000000000009"), new Guid("10000000-0000-0000-0000-000000000005"), new Guid("50000000-0000-0000-0000-000000000009"), new Guid("70000000-0000-0000-0000-000000000003"), true, SeededAt },
                    { new Guid("90000000-0000-0000-0000-000000000010"), new Guid("10000000-0000-0000-0000-000000000005"), new Guid("50000000-0000-0000-0000-000000000009"), new Guid("70000000-0000-0000-0000-000000000004"), true, SeededAt },
                    { new Guid("90000000-0000-0000-0000-000000000011"), new Guid("10000000-0000-0000-0000-000000000005"), new Guid("50000000-0000-0000-0000-000000000009"), new Guid("70000000-0000-0000-0000-000000000005"), true, SeededAt },
                    { new Guid("90000000-0000-0000-0000-000000000012"), new Guid("10000000-0000-0000-0000-000000000005"), new Guid("50000000-0000-0000-0000-000000000010"), new Guid("70000000-0000-0000-0000-000000000001"), true, SeededAt },
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData("idt_ProductOrganizationTypeRules", "Id", new Guid("90000000-0000-0000-0000-000000000008"));
            migrationBuilder.DeleteData("idt_ProductOrganizationTypeRules", "Id", new Guid("90000000-0000-0000-0000-000000000009"));
            migrationBuilder.DeleteData("idt_ProductOrganizationTypeRules", "Id", new Guid("90000000-0000-0000-0000-000000000010"));
            migrationBuilder.DeleteData("idt_ProductOrganizationTypeRules", "Id", new Guid("90000000-0000-0000-0000-000000000011"));
            migrationBuilder.DeleteData("idt_ProductOrganizationTypeRules", "Id", new Guid("90000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData("idt_RoleCapabilities", new[] { "ProductRoleId", "CapabilityId" }, new object[] { new Guid("50000000-0000-0000-0000-000000000009"), new Guid("60000000-0000-0000-0000-000000000063") });
            migrationBuilder.DeleteData("idt_RoleCapabilities", new[] { "ProductRoleId", "CapabilityId" }, new object[] { new Guid("50000000-0000-0000-0000-000000000010"), new Guid("60000000-0000-0000-0000-000000000063") });
            migrationBuilder.DeleteData("idt_RoleCapabilities", new[] { "ProductRoleId", "CapabilityId" }, new object[] { new Guid("50000000-0000-0000-0000-000000000010"), new Guid("60000000-0000-0000-0000-000000000064") });
            migrationBuilder.DeleteData("idt_RoleCapabilities", new[] { "ProductRoleId", "CapabilityId" }, new object[] { new Guid("50000000-0000-0000-0000-000000000010"), new Guid("60000000-0000-0000-0000-000000000065") });

            migrationBuilder.DeleteData("idt_Capabilities", "Id", new Guid("60000000-0000-0000-0000-000000000063"));
            migrationBuilder.DeleteData("idt_Capabilities", "Id", new Guid("60000000-0000-0000-0000-000000000064"));
            migrationBuilder.DeleteData("idt_Capabilities", "Id", new Guid("60000000-0000-0000-0000-000000000065"));

            migrationBuilder.DeleteData("idt_ProductRoles", "Id", new Guid("50000000-0000-0000-0000-000000000009"));
            migrationBuilder.DeleteData("idt_ProductRoles", "Id", new Guid("50000000-0000-0000-0000-000000000010"));

            migrationBuilder.UpdateData(
                table: "idt_Products",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "Name", "Description" },
                values: new object[] { "SynqAI", "AI-powered legal intelligence platform" });
        }
    }
}
