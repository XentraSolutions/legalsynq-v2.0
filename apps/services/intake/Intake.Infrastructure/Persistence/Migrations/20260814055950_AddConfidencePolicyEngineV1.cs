using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfidencePolicyEngineV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ArtifactExtractions_TenantId_Id",
                table: "ArtifactExtractions",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ArtifactClassifications_TenantId_Id",
                table: "ArtifactClassifications",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "ArtifactPolicyEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClassificationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactExtractionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactNormalizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactMatchRunId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PolicyProfileCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PolicyProfileVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Disposition = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OverallConfidence = table.Column<decimal>(type: "decimal(6,5)", nullable: false),
                    ReviewPriority = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExecutionKey = table.Column<string>(type: "varchar(192)", maxLength: 192, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CurrentResultMarker = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactPolicyEvaluations", x => x.Id);
                    table.UniqueConstraint("AK_ArtifactPolicyEvaluations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ArtifactPolicyEvaluations_ArtifactClassifications_TenantId_C~",
                        columns: x => new { x.TenantId, x.ClassificationId },
                        principalTable: "ArtifactClassifications",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtifactPolicyEvaluations_ArtifactExtractions_TenantId_Artif~",
                        columns: x => new { x.TenantId, x.ArtifactExtractionId },
                        principalTable: "ArtifactExtractions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtifactPolicyEvaluations_ArtifactMatchRuns_TenantId_Artifac~",
                        columns: x => new { x.TenantId, x.ArtifactMatchRunId },
                        principalTable: "ArtifactMatchRuns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtifactPolicyEvaluations_ArtifactNormalizations_TenantId_Ar~",
                        columns: x => new { x.TenantId, x.ArtifactNormalizationId },
                        principalTable: "ArtifactNormalizations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtifactPolicyEvaluations_IntakeArtifacts_TenantId_ArtifactId",
                        columns: x => new { x.TenantId, x.ArtifactId },
                        principalTable: "IntakeArtifacts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PolicyProfileDefinitions",
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
                    DefinitionJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyProfileDefinitions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ArtifactPolicyFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactPolicyEvaluationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RuleCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RuleCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Severity = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Outcome = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FactCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelatedEntityMatchId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RelatedDuplicateSignalId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RelatedNormalizedFactId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Score = table.Column<decimal>(type: "decimal(6,5)", nullable: true),
                    Threshold = table.Column<decimal>(type: "decimal(6,5)", nullable: true),
                    EvidenceReferenceJson = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactPolicyFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtifactPolicyFindings_ArtifactPolicyEvaluations_TenantId_Ar~",
                        columns: x => new { x.TenantId, x.ArtifactPolicyEvaluationId },
                        principalTable: "ArtifactPolicyEvaluations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "PolicyProfileDefinitions",
                columns: new[] { "Id", "Code", "CreatedAt", "DefinitionJson", "Description", "DisplayName", "IsActive", "IsSystemDefined", "UpdatedAt", "Version" },
                values: new object[] { new Guid("d9dcf7c5-6b13-4f87-a9b9-793af934b101"), "LIEN_INTAKE_POLICY_V1", new DateTimeOffset(new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"requiredUpstreamStages\":[\"CLASSIFICATION\",\"EXTRACTION\",\"NORMALIZATION\",\"MATCHING\"],\"supportedClassifications\":[\"MEDICAL_BILL\",\"MEDICAL_RECORD\",\"LIEN_DOCUMENT\",\"LETTER_OF_PROTECTION\",\"EXPLANATION_OF_BENEFITS\",\"SETTLEMENT_DOCUMENT\",\"ATTORNEY_DOCUMENT\",\"CORRESPONDENCE\",\"INSURANCE_DOCUMENT\"],\"classificationConfidenceThreshold\":0.80,\"requiredFactConfidenceThreshold\":0.70,\"classificationPolicies\":{\"MEDICAL_BILL\":{\"requiredFacts\":[\"PATIENT_NAME\",\"PROVIDER_NAME\",\"DATE_OF_SERVICE_START\"],\"requiredEntities\":[{\"code\":\"PATIENT\",\"anyOfEntityTypes\":[\"PATIENT\"],\"required\":true},{\"code\":\"PROVIDER_OR_FACILITY\",\"anyOfEntityTypes\":[\"PROVIDER\",\"FACILITY\"],\"required\":true}]},\"MEDICAL_RECORD\":{\"requiredFacts\":[\"PATIENT_NAME\",\"PROVIDER_NAME\"],\"requiredEntities\":[{\"code\":\"PATIENT\",\"anyOfEntityTypes\":[\"PATIENT\"],\"required\":true},{\"code\":\"PROVIDER_OR_FACILITY\",\"anyOfEntityTypes\":[\"PROVIDER\",\"FACILITY\"],\"required\":true}]},\"LIEN_DOCUMENT\":{\"requiredFacts\":[\"PATIENT_NAME\",\"PROVIDER_NAME\",\"LIEN_AMOUNT\"],\"requiredEntities\":[{\"code\":\"PATIENT\",\"anyOfEntityTypes\":[\"PATIENT\"],\"required\":true},{\"code\":\"PROVIDER_OR_FACILITY\",\"anyOfEntityTypes\":[\"PROVIDER\",\"FACILITY\"],\"required\":true}]},\"LETTER_OF_PROTECTION\":{\"requiredFacts\":[\"PATIENT_NAME\",\"PROVIDER_NAME\",\"ATTORNEY_NAME\"],\"requiredEntities\":[{\"code\":\"PATIENT\",\"anyOfEntityTypes\":[\"PATIENT\"],\"required\":true},{\"code\":\"PROVIDER_OR_FACILITY\",\"anyOfEntityTypes\":[\"PROVIDER\",\"FACILITY\"],\"required\":true}]},\"EXPLANATION_OF_BENEFITS\":{\"requiredFacts\":[\"PATIENT_NAME\",\"PROVIDER_NAME\",\"CLAIM_NUMBER\"],\"requiredEntities\":[{\"code\":\"PATIENT\",\"anyOfEntityTypes\":[\"PATIENT\"],\"required\":true},{\"code\":\"PROVIDER_OR_FACILITY\",\"anyOfEntityTypes\":[\"PROVIDER\",\"FACILITY\"],\"required\":true}]},\"SETTLEMENT_DOCUMENT\":{\"requiredFacts\":[\"PATIENT_NAME\",\"SETTLEMENT_AMOUNT\"],\"requiredEntities\":[{\"code\":\"PATIENT\",\"anyOfEntityTypes\":[\"PATIENT\"],\"required\":true},{\"code\":\"PROVIDER_OR_FACILITY\",\"anyOfEntityTypes\":[\"PROVIDER\",\"FACILITY\"],\"required\":false}]},\"ATTORNEY_DOCUMENT\":{\"requiredFacts\":[\"PATIENT_NAME\",\"ATTORNEY_NAME\"],\"requiredEntities\":[{\"code\":\"PATIENT\",\"anyOfEntityTypes\":[\"PATIENT\"],\"required\":true},{\"code\":\"PROVIDER_OR_FACILITY\",\"anyOfEntityTypes\":[\"PROVIDER\",\"FACILITY\"],\"required\":false}]},\"CORRESPONDENCE\":{\"requiredFacts\":[\"PATIENT_NAME\"],\"requiredEntities\":[{\"code\":\"PATIENT\",\"anyOfEntityTypes\":[\"PATIENT\"],\"required\":true},{\"code\":\"PROVIDER_OR_FACILITY\",\"anyOfEntityTypes\":[\"PROVIDER\",\"FACILITY\"],\"required\":true}]},\"INSURANCE_DOCUMENT\":{\"requiredFacts\":[\"PATIENT_NAME\",\"INSURER_NAME\",\"CLAIM_NUMBER\"],\"requiredEntities\":[{\"code\":\"PATIENT\",\"anyOfEntityTypes\":[\"PATIENT\"],\"required\":true},{\"code\":\"PROVIDER_OR_FACILITY\",\"anyOfEntityTypes\":[\"PROVIDER\",\"FACILITY\"],\"required\":true}]}},\"matchThresholds\":{\"PATIENT\":0.85,\"PROVIDER\":0.80,\"FACILITY\":0.80,\"CASE\":0.75,\"ATTORNEY\":0.80,\"LAW_FIRM\":0.80},\"candidateMargins\":{\"PATIENT\":0.10,\"PROVIDER\":0.10,\"FACILITY\":0.10,\"CASE\":0.10,\"ATTORNEY\":0.10,\"LAW_FIRM\":0.10},\"evidenceRequiredFactCodes\":[\"PATIENT_NAME\",\"PATIENT_IDENTIFIER\",\"PROVIDER_NAME\",\"PROVIDER_IDENTIFIER\",\"LIEN_AMOUNT\"],\"hardConflictReasonCodes\":[\"DOB_CONFLICT\",\"IDENTIFIER_CONFLICT\"],\"duplicatePolicies\":{\"EXACT_ARTIFACT\":{\"disposition\":\"DUPLICATE\",\"severity\":\"REVIEW\",\"enabled\":true},\"CONTENT\":{\"disposition\":\"DUPLICATE\",\"severity\":\"REVIEW\",\"enabled\":true},\"BUSINESS_KEY\":{\"disposition\":\"DUPLICATE\",\"severity\":\"REVIEW\",\"enabled\":true}},\"confidenceWeights\":{\"classification\":0.20,\"extraction\":0.15,\"normalization\":0.15,\"patient\":0.25,\"provider\":0.15,\"evidence\":0.10},\"confidencePenalties\":{\"hard_conflict\":0.30,\"ambiguity\":0.15,\"duplicate\":0.25,\"warning\":0.05},\"defaultDisposition\":\"REVIEW_REQUIRED\"}", "Deterministic confidence, safety, duplicate, evidence, and policy disposition rules.", "Lien Intake Policy V1", true, true, new DateTimeOffset(new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPolicyEvaluations_ExecutionKey",
                table: "ArtifactPolicyEvaluations",
                column: "ExecutionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPolicyEvaluations_TenantId_ArtifactExtractionId",
                table: "ArtifactPolicyEvaluations",
                columns: new[] { "TenantId", "ArtifactExtractionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPolicyEvaluations_TenantId_ArtifactId_CreatedAt",
                table: "ArtifactPolicyEvaluations",
                columns: new[] { "TenantId", "ArtifactId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPolicyEvaluations_TenantId_ArtifactId_CurrentResultM~",
                table: "ArtifactPolicyEvaluations",
                columns: new[] { "TenantId", "ArtifactId", "CurrentResultMarker" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPolicyEvaluations_TenantId_ArtifactMatchRunId",
                table: "ArtifactPolicyEvaluations",
                columns: new[] { "TenantId", "ArtifactMatchRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPolicyEvaluations_TenantId_ArtifactNormalizationId",
                table: "ArtifactPolicyEvaluations",
                columns: new[] { "TenantId", "ArtifactNormalizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPolicyEvaluations_TenantId_ClassificationId",
                table: "ArtifactPolicyEvaluations",
                columns: new[] { "TenantId", "ClassificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPolicyFindings_TenantId_ArtifactPolicyEvaluationId_R~",
                table: "ArtifactPolicyFindings",
                columns: new[] { "TenantId", "ArtifactPolicyEvaluationId", "RuleCode", "ReasonCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyProfileDefinitions_Code_Version",
                table: "PolicyProfileDefinitions",
                columns: new[] { "Code", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtifactPolicyFindings");

            migrationBuilder.DropTable(
                name: "PolicyProfileDefinitions");

            migrationBuilder.DropTable(
                name: "ArtifactPolicyEvaluations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ArtifactExtractions_TenantId_Id",
                table: "ArtifactExtractions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ArtifactClassifications_TenantId_Id",
                table: "ArtifactClassifications");
        }
    }
}
