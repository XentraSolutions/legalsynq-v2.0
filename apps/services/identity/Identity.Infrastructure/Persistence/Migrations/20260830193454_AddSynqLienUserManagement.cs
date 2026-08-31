using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSynqLienUserManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "idt_UserInvitations",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "idt_UserInvitationRoleGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    InvitationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RoleCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idt_UserInvitationRoleGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_idt_UserInvitationRoleGrants_idt_UserInvitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "idt_UserInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "idt_Capabilities",
                columns: new[] { "Id", "Category", "Code", "CreatedAtUtc", "CreatedBy", "Description", "IsActive", "Name", "ProductId", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("60000000-0000-0000-0000-000000000073"), "User Management", "SYNQ_LIENS.user:read", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "View tenant users with SynqLien access or invitations", true, "Read SynqLien Users", new Guid("10000000-0000-0000-0000-000000000002"), null, null },
                    { new Guid("60000000-0000-0000-0000-000000000074"), "User Management", "SYNQ_LIENS.user:invite", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Invite users into SynqLien", true, "Invite SynqLien Users", new Guid("10000000-0000-0000-0000-000000000002"), null, null },
                    { new Guid("60000000-0000-0000-0000-000000000075"), "User Management", "SYNQ_LIENS.user_access:manage", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Grant or revoke SynqLien product access", true, "Manage SynqLien User Access", new Guid("10000000-0000-0000-0000-000000000002"), null, null },
                    { new Guid("60000000-0000-0000-0000-000000000076"), "User Management", "SYNQ_LIENS.user_role:assign", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Replace direct SynqLien role assignments", true, "Assign SynqLien User Roles", new Guid("10000000-0000-0000-0000-000000000002"), null, null },
                    { new Guid("60000000-0000-0000-0000-000000000077"), "User Management", "SYNQ_LIENS.user_audit:read", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "View SynqLien user-management audit events", true, "Read SynqLien User Audit", new Guid("10000000-0000-0000-0000-000000000002"), null, null }
                });

            migrationBuilder.InsertData(
                table: "idt_ProductRoles",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "Description", "IsActive", "Name", "ProductId" },
                values: new object[] { new Guid("50000000-0000-0000-0000-000000000013"), "SYNQLIEN_USER_ADMIN", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Tenant-scoped administration of SynqLien users, invitations, access, and roles", true, "SynqLien User Administrator", new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.InsertData(
                table: "idt_RoleCapabilities",
                columns: new[] { "CapabilityId", "ProductRoleId" },
                values: new object[,]
                {
                    { new Guid("60000000-0000-0000-0000-000000000073"), new Guid("50000000-0000-0000-0000-000000000013") },
                    { new Guid("60000000-0000-0000-0000-000000000074"), new Guid("50000000-0000-0000-0000-000000000013") },
                    { new Guid("60000000-0000-0000-0000-000000000075"), new Guid("50000000-0000-0000-0000-000000000013") },
                    { new Guid("60000000-0000-0000-0000-000000000076"), new Guid("50000000-0000-0000-0000-000000000013") },
                    { new Guid("60000000-0000-0000-0000-000000000077"), new Guid("50000000-0000-0000-0000-000000000013") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_idt_UserInvitations_TenantId_ProductCode_Status_CreatedAtUtc",
                table: "idt_UserInvitations",
                columns: new[] { "TenantId", "ProductCode", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_Tenant_Product_Status_User",
                table: "idt_UserRoleAssignments",
                columns: new[] { "TenantId", "ProductCode", "AssignmentStatus", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_idt_UserInvitationRoleGrants_InvitationId_ProductCode_RoleCo~",
                table: "idt_UserInvitationRoleGrants",
                columns: new[] { "InvitationId", "ProductCode", "RoleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idt_UserInvitationRoleGrants_TenantId_ProductCode_RoleCode",
                table: "idt_UserInvitationRoleGrants",
                columns: new[] { "TenantId", "ProductCode", "RoleCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idt_UserInvitationRoleGrants");

            migrationBuilder.DropIndex(
                name: "IX_idt_UserInvitations_TenantId_ProductCode_Status_CreatedAtUtc",
                table: "idt_UserInvitations");

            migrationBuilder.DropIndex(
                name: "IX_UserRoleAssignments_Tenant_Product_Status_User",
                table: "idt_UserRoleAssignments");

            migrationBuilder.DeleteData(
                table: "idt_RoleCapabilities",
                keyColumns: new[] { "CapabilityId", "ProductRoleId" },
                keyValues: new object[] { new Guid("60000000-0000-0000-0000-000000000073"), new Guid("50000000-0000-0000-0000-000000000013") });

            migrationBuilder.DeleteData(
                table: "idt_RoleCapabilities",
                keyColumns: new[] { "CapabilityId", "ProductRoleId" },
                keyValues: new object[] { new Guid("60000000-0000-0000-0000-000000000074"), new Guid("50000000-0000-0000-0000-000000000013") });

            migrationBuilder.DeleteData(
                table: "idt_RoleCapabilities",
                keyColumns: new[] { "CapabilityId", "ProductRoleId" },
                keyValues: new object[] { new Guid("60000000-0000-0000-0000-000000000075"), new Guid("50000000-0000-0000-0000-000000000013") });

            migrationBuilder.DeleteData(
                table: "idt_RoleCapabilities",
                keyColumns: new[] { "CapabilityId", "ProductRoleId" },
                keyValues: new object[] { new Guid("60000000-0000-0000-0000-000000000076"), new Guid("50000000-0000-0000-0000-000000000013") });

            migrationBuilder.DeleteData(
                table: "idt_RoleCapabilities",
                keyColumns: new[] { "CapabilityId", "ProductRoleId" },
                keyValues: new object[] { new Guid("60000000-0000-0000-0000-000000000077"), new Guid("50000000-0000-0000-0000-000000000013") });

            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000073"));

            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000074"));

            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000075"));

            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000076"));

            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000077"));

            migrationBuilder.DeleteData(
                table: "idt_ProductRoles",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000013"));

            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "idt_UserInvitations");
        }
    }
}
