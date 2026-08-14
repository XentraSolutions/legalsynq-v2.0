using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class B15DocumentAssociationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeDocumentAssociationExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SnapshotId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AdapterExecutionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PolicyCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    ExecutionKey = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdempotencyKey = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    FailureCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResultJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeDocumentAssociationExecutions", x => x.Id);
                    table.UniqueConstraint("AK_IntakeDocumentAssociationExecutions_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.UniqueConstraint("AK_IntakeDocumentAssociationExecutions_TenantId_Id", x => new { x.TenantId, x.Id });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IntakeDocumentAssociationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ExecutionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DocumentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DocumentReference = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DocumentRole = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetType = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RelatedCaseId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ItemKey = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Required = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    FailureCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DestinationReference = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeDocumentAssociationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeDocumentAssociationItems_IntakeDocumentAssociationExec~",
                        columns: x => new { x.ExecutionId, x.TenantId },
                        principalTable: "IntakeDocumentAssociationExecutions",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDocumentAssociationExecutions_TenantId_ExecutionKey",
                table: "IntakeDocumentAssociationExecutions",
                columns: new[] { "TenantId", "ExecutionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDocumentAssociationExecutions_TenantId_SnapshotId_Crea~",
                table: "IntakeDocumentAssociationExecutions",
                columns: new[] { "TenantId", "SnapshotId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDocumentAssociationItems_ExecutionId_TenantId",
                table: "IntakeDocumentAssociationItems",
                columns: new[] { "ExecutionId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDocumentAssociationItems_TenantId_ArtifactId",
                table: "IntakeDocumentAssociationItems",
                columns: new[] { "TenantId", "ArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDocumentAssociationItems_TenantId_ExecutionId_ItemKey",
                table: "IntakeDocumentAssociationItems",
                columns: new[] { "TenantId", "ExecutionId", "ItemKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeDocumentAssociationItems");

            migrationBuilder.DropTable(
                name: "IntakeDocumentAssociationExecutions");
        }
    }
}
