using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeHumanReviewV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClassificationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactExtractionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactNormalizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactMatchRunId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactPolicyEvaluationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Priority = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewOutcome = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    B11Disposition = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClassificationCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignedToUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CompletionReasonCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompletionComment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    SupersedesReviewId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ActiveContextKey = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReviews", x => x.Id);
                    table.UniqueConstraint("AK_IntakeReviews_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_IntakeReviews_ArtifactClassifications_TenantId_Classificatio~",
                        columns: x => new { x.TenantId, x.ClassificationId },
                        principalTable: "ArtifactClassifications",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeReviews_ArtifactExtractions_TenantId_ArtifactExtractio~",
                        columns: x => new { x.TenantId, x.ArtifactExtractionId },
                        principalTable: "ArtifactExtractions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeReviews_ArtifactMatchRuns_TenantId_ArtifactMatchRunId",
                        columns: x => new { x.TenantId, x.ArtifactMatchRunId },
                        principalTable: "ArtifactMatchRuns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeReviews_ArtifactNormalizations_TenantId_ArtifactNormal~",
                        columns: x => new { x.TenantId, x.ArtifactNormalizationId },
                        principalTable: "ArtifactNormalizations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeReviews_ArtifactPolicyEvaluations_TenantId_ArtifactPol~",
                        columns: x => new { x.TenantId, x.ArtifactPolicyEvaluationId },
                        principalTable: "ArtifactPolicyEvaluations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeReviews_IntakeArtifacts_TenantId_ArtifactId",
                        columns: x => new { x.TenantId, x.ArtifactId },
                        principalTable: "IntakeArtifacts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IntakeReviewActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IntakeReviewId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ActivityType = table.Column<string>(type: "varchar(48)", maxLength: 48, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SafeMetadataJson = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReviewActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeReviewActivities_IntakeReviews_TenantId_IntakeReviewId",
                        columns: x => new { x.TenantId, x.IntakeReviewId },
                        principalTable: "IntakeReviews",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IntakeReviewCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IntakeReviewId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TargetType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FactCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalExtractedFactId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    OriginalNormalizedFactId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CorrectionType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrectedValue = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrectedJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedValue = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValidationStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceType = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HumanVerified = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ReasonCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Comment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    SupersedesCorrectionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReviewCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeReviewCorrections_IntakeReviews_TenantId_IntakeReviewId",
                        columns: x => new { x.TenantId, x.IntakeReviewId },
                        principalTable: "IntakeReviews",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IntakeReviewDuplicateDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IntakeReviewId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactDuplicateSignalId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Decision = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelatedArtifactId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ReasonCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Comment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    SupersedesDecisionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReviewDuplicateDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeReviewDuplicateDecisions_IntakeReviews_TenantId_Intake~",
                        columns: x => new { x.TenantId, x.IntakeReviewId },
                        principalTable: "IntakeReviews",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IntakeReviewFindingDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IntakeReviewId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactPolicyFindingId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Decision = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Comment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    SupersedesDecisionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReviewFindingDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeReviewFindingDecisions_IntakeReviews_TenantId_IntakeRe~",
                        columns: x => new { x.TenantId, x.IntakeReviewId },
                        principalTable: "IntakeReviews",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IntakeReviewMatchDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IntakeReviewId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EntityType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ArtifactEntityMatchId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CandidateEntityId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Decision = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsManualSelection = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ReasonCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Comment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    SupersedesDecisionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReviewMatchDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeReviewMatchDecisions_IntakeReviews_TenantId_IntakeRevi~",
                        columns: x => new { x.TenantId, x.IntakeReviewId },
                        principalTable: "IntakeReviews",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviewActivities_CreatedAt",
                table: "IntakeReviewActivities",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviewActivities_TenantId_IntakeReviewId",
                table: "IntakeReviewActivities",
                columns: new[] { "TenantId", "IntakeReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviewCorrections_TenantId_IntakeReviewId",
                table: "IntakeReviewCorrections",
                columns: new[] { "TenantId", "IntakeReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviewDuplicateDecisions_TenantId_IntakeReviewId",
                table: "IntakeReviewDuplicateDecisions",
                columns: new[] { "TenantId", "IntakeReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviewFindingDecisions_TenantId_IntakeReviewId",
                table: "IntakeReviewFindingDecisions",
                columns: new[] { "TenantId", "IntakeReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviewMatchDecisions_TenantId_IntakeReviewId",
                table: "IntakeReviewMatchDecisions",
                columns: new[] { "TenantId", "IntakeReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_ArtifactId",
                table: "IntakeReviews",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_ArtifactPolicyEvaluationId",
                table: "IntakeReviews",
                column: "ArtifactPolicyEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_ActiveContextKey",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "ActiveContextKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_ArtifactExtractionId",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "ArtifactExtractionId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_ArtifactId",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "ArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_ArtifactMatchRunId",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "ArtifactMatchRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_ArtifactNormalizationId",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "ArtifactNormalizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_ArtifactPolicyEvaluationId",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "ArtifactPolicyEvaluationId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_AssignedToUserId_Status",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_ClassificationId",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "ClassificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_CreatedAt",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_Priority",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReviews_TenantId_Status",
                table: "IntakeReviews",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeReviewActivities");

            migrationBuilder.DropTable(
                name: "IntakeReviewCorrections");

            migrationBuilder.DropTable(
                name: "IntakeReviewDuplicateDecisions");

            migrationBuilder.DropTable(
                name: "IntakeReviewFindingDecisions");

            migrationBuilder.DropTable(
                name: "IntakeReviewMatchDecisions");

            migrationBuilder.DropTable(
                name: "IntakeReviews");
        }
    }
}
