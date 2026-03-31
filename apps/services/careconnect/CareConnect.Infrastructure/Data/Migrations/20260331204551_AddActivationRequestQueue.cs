using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivationRequestQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReferralNotes_ReferralId",
                table: "ReferralNotes");

            migrationBuilder.DropIndex(
                name: "IX_CareConnectNotifications_Status_NextRetryAfterUtc",
                table: "CareConnectNotifications");

            migrationBuilder.AlterColumn<int>(
                name: "TokenVersion",
                table: "Referrals",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "ReferrerName",
                table: "Referrals",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReferrerEmail",
                table: "Referrals",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(320)",
                oldMaxLength: 320,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationRelationshipId",
                table: "Referrals",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Providers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AlterColumn<string>(
                name: "SsnLast4",
                table: "Parties",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(4)",
                oldMaxLength: 4,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PartyType",
                table: "Parties",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "INDIVIDUAL")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Facilities",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AlterColumn<string>(
                name: "TriggerSource",
                table: "CareConnectNotifications",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Initial")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "AttemptCount",
                table: "CareConnectNotifications",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationRelationshipId",
                table: "Appointments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "ActivationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReferralId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProviderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProviderName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderEmail = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequesterName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequesterEmail = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClientName = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReferringFirmName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedService = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LinkedOrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivationRequests_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivationRequests_Referrals_ReferralId",
                        column: x => x.ReferralId,
                        principalTable: "Referrals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_OrganizationRelationshipId",
                table: "Referrals",
                column: "OrganizationRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_Providers_OrganizationId",
                table: "Providers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Providers_TenantId_City_State",
                table: "Providers",
                columns: new[] { "TenantId", "City", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_OrganizationId",
                table: "Facilities",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_OrganizationRelationshipId",
                table: "Appointments",
                column: "OrganizationRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivationRequests_ProviderId",
                table: "ActivationRequests",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivationRequests_ReferralId_ProviderId",
                table: "ActivationRequests",
                columns: new[] { "ReferralId", "ProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivationRequests_Status_CreatedAt",
                table: "ActivationRequests",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivationRequests");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_OrganizationRelationshipId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Providers_OrganizationId",
                table: "Providers");

            migrationBuilder.DropIndex(
                name: "IX_Providers_TenantId_City_State",
                table: "Providers");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_OrganizationId",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_OrganizationRelationshipId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "OrganizationRelationshipId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "OrganizationRelationshipId",
                table: "Appointments");

            migrationBuilder.AlterColumn<int>(
                name: "TokenVersion",
                table: "Referrals",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ReferrerName",
                table: "Referrals",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReferrerEmail",
                table: "Referrals",
                type: "varchar(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SsnLast4",
                table: "Parties",
                type: "char(4)",
                maxLength: 4,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(4)",
                oldMaxLength: 4,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PartyType",
                table: "Parties",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "INDIVIDUAL",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TriggerSource",
                table: "CareConnectNotifications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Initial",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "AttemptCount",
                table: "CareConnectNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralNotes_ReferralId",
                table: "ReferralNotes",
                column: "ReferralId");

            migrationBuilder.CreateIndex(
                name: "IX_CareConnectNotifications_Status_NextRetryAfterUtc",
                table: "CareConnectNotifications",
                columns: new[] { "Status", "NextRetryAfterUtc" });
        }
    }
}
