using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TenantIntakeSourceId",
                table: "IntakeArtifacts",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AlterColumn<Guid>(
                name: "InboundEmailId",
                table: "IntakeArtifacts",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "ArtifactSourceType",
                table: "IntakeArtifacts",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "EMAIL")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "ManualIntakeSubmissionId",
                table: "IntakeArtifacts",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "ManualIntakeSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrgId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TenantIntakeSourceId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SourceType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Purpose = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessingProfileCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExternalReference = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClientRequestId = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubmittedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfigurationVersion = table.Column<int>(type: "int", nullable: false),
                    ProfileConfigurationVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualIntakeSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualIntakeSubmissions_TenantIntakeSources_TenantIntakeSour~",
                        column: x => x.TenantIntakeSourceId,
                        principalTable: "TenantIntakeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeArtifacts_ManualIntakeSubmissionId_ArtifactKey",
                table: "IntakeArtifacts",
                columns: new[] { "ManualIntakeSubmissionId", "ArtifactKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeArtifacts_TenantId_ManualIntakeSubmissionId_ArtifactOr~",
                table: "IntakeArtifacts",
                columns: new[] { "TenantId", "ManualIntakeSubmissionId", "ArtifactOrdinal" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_IntakeArtifacts_ExactlyOneParent",
                table: "IntakeArtifacts",
                sql: "((InboundEmailId IS NOT NULL AND ManualIntakeSubmissionId IS NULL) OR (InboundEmailId IS NULL AND ManualIntakeSubmissionId IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_ManualIntakeSubmissions_TenantId_ClientRequestId",
                table: "ManualIntakeSubmissions",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManualIntakeSubmissions_TenantId_CreatedAt",
                table: "ManualIntakeSubmissions",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualIntakeSubmissions_TenantId_Purpose_CreatedAt",
                table: "ManualIntakeSubmissions",
                columns: new[] { "TenantId", "Purpose", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualIntakeSubmissions_TenantId_Status_UpdatedAt",
                table: "ManualIntakeSubmissions",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualIntakeSubmissions_TenantIntakeSourceId",
                table: "ManualIntakeSubmissions",
                column: "TenantIntakeSourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeArtifacts_ManualIntakeSubmissions_ManualIntakeSubmissi~",
                table: "IntakeArtifacts",
                column: "ManualIntakeSubmissionId",
                principalTable: "ManualIntakeSubmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntakeArtifacts_ManualIntakeSubmissions_ManualIntakeSubmissi~",
                table: "IntakeArtifacts");

            migrationBuilder.DropTable(
                name: "ManualIntakeSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_IntakeArtifacts_ManualIntakeSubmissionId_ArtifactKey",
                table: "IntakeArtifacts");

            migrationBuilder.DropIndex(
                name: "IX_IntakeArtifacts_TenantId_ManualIntakeSubmissionId_ArtifactOr~",
                table: "IntakeArtifacts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IntakeArtifacts_ExactlyOneParent",
                table: "IntakeArtifacts");

            migrationBuilder.DropColumn(
                name: "ArtifactSourceType",
                table: "IntakeArtifacts");

            migrationBuilder.DropColumn(
                name: "ManualIntakeSubmissionId",
                table: "IntakeArtifacts");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantIntakeSourceId",
                table: "IntakeArtifacts",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AlterColumn<Guid>(
                name: "InboundEmailId",
                table: "IntakeArtifacts",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");
        }
    }
}
