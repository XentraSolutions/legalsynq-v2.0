using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundEmailCaptureFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboundEmailCaptureFailures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantIntakeSourceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Provider = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEmailCaptureFailures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundEmailCaptureFailures_TenantIntakeSources_TenantIntake~",
                        column: x => x.TenantIntakeSourceId,
                        principalTable: "TenantIntakeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailCaptureFailures_TenantId_FailureCode_OccurredAt",
                table: "InboundEmailCaptureFailures",
                columns: new[] { "TenantId", "FailureCode", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailCaptureFailures_TenantId_OccurredAt",
                table: "InboundEmailCaptureFailures",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailCaptureFailures_TenantId_TenantIntakeSourceId_Oc~",
                table: "InboundEmailCaptureFailures",
                columns: new[] { "TenantId", "TenantIntakeSourceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailCaptureFailures_TenantIntakeSourceId",
                table: "InboundEmailCaptureFailures",
                column: "TenantIntakeSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboundEmailCaptureFailures");
        }
    }
}
