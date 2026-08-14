using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class B16RecoveryOperationsModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeRecoveryWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Stage = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ObjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DomainStatus = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecoveryStatus = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Retryable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastRecoveryAttemptAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    LastFailureCode = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastSafeMessage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureCategory = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecoverySource = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExhaustedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    StaleSince = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    ClaimToken = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRecoveryWorkItems", x => x.Id);
                    table.UniqueConstraint("AK_IntakeRecoveryWorkItems_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.UniqueConstraint("AK_IntakeRecoveryWorkItems_TenantId_Id", x => new { x.TenantId, x.Id });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IntakeRecoveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WorkItemId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureCode = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SafeMessage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureCategory = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecoverySource = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRecoveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeRecoveryAttempts_IntakeRecoveryWorkItems_WorkItemId_Te~",
                        columns: x => new { x.WorkItemId, x.TenantId },
                        principalTable: "IntakeRecoveryWorkItems",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRecoveryAttempts_TenantId_WorkItemId_AttemptNumber",
                table: "IntakeRecoveryAttempts",
                columns: new[] { "TenantId", "WorkItemId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRecoveryAttempts_WorkItemId_TenantId",
                table: "IntakeRecoveryAttempts",
                columns: new[] { "WorkItemId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRecoveryWorkItems_TenantId_RecoveryStatus_NextRetryAt_~",
                table: "IntakeRecoveryWorkItems",
                columns: new[] { "TenantId", "RecoveryStatus", "NextRetryAt", "StaleSince" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRecoveryWorkItems_TenantId_Stage_ObjectId",
                table: "IntakeRecoveryWorkItems",
                columns: new[] { "TenantId", "Stage", "ObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRecoveryWorkItems_TenantId_Stage_UpdatedAt",
                table: "IntakeRecoveryWorkItems",
                columns: new[] { "TenantId", "Stage", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeRecoveryAttempts");

            migrationBuilder.DropTable(
                name: "IntakeRecoveryWorkItems");
        }
    }
}
