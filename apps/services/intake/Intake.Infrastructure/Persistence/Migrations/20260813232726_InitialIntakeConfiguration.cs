using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIntakeConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProcessingProfileDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsSystemDefined = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingProfileDefinitions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TenantIntakeConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrgId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultProcessingProfileCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequireHumanReviewByDefault = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AutoProcessingEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ConfigurationVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantIntakeConfigurations", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TenantProcessingProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProcessingProfileDefinitionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDefault = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultTenantKey = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfigurationJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfigurationVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantProcessingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantProcessingProfiles_ProcessingProfileDefinitions_Proces~",
                        column: x => x.ProcessingProfileDefinitionId,
                        principalTable: "ProcessingProfileDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "ProcessingProfileDefinitions",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "DisplayName", "IsActive", "IsSystemDefined", "UpdatedAt", "Version" },
                values: new object[] { new Guid("019b0a9c-3f2d-7a11-9b72-5c8e2f1d4a60"), "LIEN_INTAKE_V1", new DateTimeOffset(new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Conservative configuration contract for future lien-intake processing.", "Lien Intake V1", true, true, new DateTimeOffset(new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingProfileDefinitions_Code",
                table: "ProcessingProfileDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntakeConfigurations_DefaultProcessingProfileCode",
                table: "TenantIntakeConfigurations",
                column: "DefaultProcessingProfileCode");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntakeConfigurations_TenantId",
                table: "TenantIntakeConfigurations",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantProcessingProfiles_DefaultTenantKey",
                table: "TenantProcessingProfiles",
                column: "DefaultTenantKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantProcessingProfiles_ProcessingProfileDefinitionId",
                table: "TenantProcessingProfiles",
                column: "ProcessingProfileDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantProcessingProfiles_TenantId_IsDefault",
                table: "TenantProcessingProfiles",
                columns: new[] { "TenantId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantProcessingProfiles_TenantId_IsEnabled",
                table: "TenantProcessingProfiles",
                columns: new[] { "TenantId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantProcessingProfiles_TenantId_ProcessingProfileDefinitio~",
                table: "TenantProcessingProfiles",
                columns: new[] { "TenantId", "ProcessingProfileDefinitionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantIntakeConfigurations");

            migrationBuilder.DropTable(
                name: "TenantProcessingProfiles");

            migrationBuilder.DropTable(
                name: "ProcessingProfileDefinitions");
        }
    }
}
