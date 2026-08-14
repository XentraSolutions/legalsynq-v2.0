using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedSnapshotAdapterFrameworkV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovedIntakeSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReviewId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PolicyEvaluationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClassificationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactExtractionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactNormalizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactMatchRunId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ProcessingProfileCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SchemaCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    SnapshotVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SnapshotHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExecutionKey = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ActiveCurrentKey = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    SupersedesSnapshotId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedIntakeSnapshots", x => x.Id);
                    table.UniqueConstraint("AK_ApprovedIntakeSnapshots_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ApprovedIntakeSnapshots_IntakeArtifacts_TenantId_ArtifactId",
                        columns: x => new { x.TenantId, x.ArtifactId },
                        principalTable: "IntakeArtifacts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovedIntakeSnapshots_IntakeReviews_TenantId_ReviewId",
                        columns: x => new { x.TenantId, x.ReviewId },
                        principalTable: "IntakeReviews",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ApprovedSnapshotSchemaDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
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
                    table.PrimaryKey("PK_ApprovedSnapshotSchemaDefinitions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IntakeAdapterExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SnapshotId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AdapterCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdapterVersion = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExecutionKey = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdempotencyKey = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
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
                    ResultJson = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAdapterExecutions", x => x.Id);
                    table.UniqueConstraint("AK_IntakeAdapterExecutions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_IntakeAdapterExecutions_ApprovedIntakeSnapshots_TenantId_Sna~",
                        columns: x => new { x.TenantId, x.SnapshotId },
                        principalTable: "ApprovedIntakeSnapshots",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IntakeAdapterExecutionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AdapterExecutionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    FailureCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAdapterExecutionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeAdapterExecutionAttempts_IntakeAdapterExecutions_Tenan~",
                        columns: x => new { x.TenantId, x.AdapterExecutionId },
                        principalTable: "IntakeAdapterExecutions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IntakeAdapterExternalReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AdapterExecutionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReferenceType = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReferenceId = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAdapterExternalReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeAdapterExternalReferences_IntakeAdapterExecutions_Tena~",
                        columns: x => new { x.TenantId, x.AdapterExecutionId },
                        principalTable: "IntakeAdapterExecutions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedIntakeSnapshots_TenantId_ActiveCurrentKey",
                table: "ApprovedIntakeSnapshots",
                columns: new[] { "TenantId", "ActiveCurrentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedIntakeSnapshots_TenantId_ArtifactId_IsCurrent",
                table: "ApprovedIntakeSnapshots",
                columns: new[] { "TenantId", "ArtifactId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedIntakeSnapshots_TenantId_ArtifactId_SnapshotVersion",
                table: "ApprovedIntakeSnapshots",
                columns: new[] { "TenantId", "ArtifactId", "SnapshotVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedIntakeSnapshots_TenantId_ExecutionKey",
                table: "ApprovedIntakeSnapshots",
                columns: new[] { "TenantId", "ExecutionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedIntakeSnapshots_TenantId_ReviewId",
                table: "ApprovedIntakeSnapshots",
                columns: new[] { "TenantId", "ReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSnapshotSchemaDefinitions_Code_IsActive",
                table: "ApprovedSnapshotSchemaDefinitions",
                columns: new[] { "Code", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedSnapshotSchemaDefinitions_Code_Version",
                table: "ApprovedSnapshotSchemaDefinitions",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAdapterExecutionAttempts_TenantId_AdapterExecutionId_A~",
                table: "IntakeAdapterExecutionAttempts",
                columns: new[] { "TenantId", "AdapterExecutionId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAdapterExecutions_TenantId_ExecutionKey",
                table: "IntakeAdapterExecutions",
                columns: new[] { "TenantId", "ExecutionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAdapterExecutions_TenantId_SnapshotId_AdapterCode",
                table: "IntakeAdapterExecutions",
                columns: new[] { "TenantId", "SnapshotId", "AdapterCode" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAdapterExternalReferences_TenantId_AdapterExecutionId",
                table: "IntakeAdapterExternalReferences",
                columns: new[] { "TenantId", "AdapterExecutionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovedSnapshotSchemaDefinitions");

            migrationBuilder.DropTable(
                name: "IntakeAdapterExecutionAttempts");

            migrationBuilder.DropTable(
                name: "IntakeAdapterExternalReferences");

            migrationBuilder.DropTable(
                name: "IntakeAdapterExecutions");

            migrationBuilder.DropTable(
                name: "ApprovedIntakeSnapshots");
        }
    }
}
