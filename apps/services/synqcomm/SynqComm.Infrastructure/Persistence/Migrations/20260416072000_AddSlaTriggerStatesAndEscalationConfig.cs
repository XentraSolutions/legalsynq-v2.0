using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SynqComm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaTriggerStatesAndEscalationConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comms_ConversationSlaTriggerStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FirstResponseWarningSentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FirstResponseBreachSentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolutionWarningSentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolutionBreachSentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastEscalatedToUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LastEscalatedQueueId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    WarningThresholdSnapshotMinutes = table.Column<int>(type: "int", nullable: true),
                    EvaluationVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comms_ConversationSlaTriggerStates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "comms_QueueEscalationConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    QueueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FallbackUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comms_QueueEscalationConfigs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SlaTriggerState_TenantId",
                table: "comms_ConversationSlaTriggerStates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaTriggerState_TenantId_ConversationId",
                table: "comms_ConversationSlaTriggerStates",
                columns: new[] { "TenantId", "ConversationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaTriggerState_TenantId_FirstResponseBreachSentAtUtc",
                table: "comms_ConversationSlaTriggerStates",
                columns: new[] { "TenantId", "FirstResponseBreachSentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaTriggerState_TenantId_ResolutionBreachSentAtUtc",
                table: "comms_ConversationSlaTriggerStates",
                columns: new[] { "TenantId", "ResolutionBreachSentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QueueEscalationConfig_TenantId_QueueId",
                table: "comms_QueueEscalationConfigs",
                columns: new[] { "TenantId", "QueueId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comms_ConversationSlaTriggerStates");

            migrationBuilder.DropTable(
                name: "comms_QueueEscalationConfigs");
        }
    }
}
