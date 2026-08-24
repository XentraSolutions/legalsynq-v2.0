using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantMatchingAndDuplicatesV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_IntakeArtifacts_TenantId_Id",
                table: "IntakeArtifacts",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ArtifactNormalizations_TenantId_Id",
                table: "ArtifactNormalizations",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "ArtifactMatchRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IntakeArtifactId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactNormalizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MatchingProfileCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MatchingProfileVersion = table.Column<int>(type: "int", nullable: false),
                    ScoringVersion = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExecutionKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CurrentResultMarker = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BusinessKeyFingerprint = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BusinessDuplicateRuleCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactMatchRuns", x => x.Id);
                    table.UniqueConstraint("AK_ArtifactMatchRuns_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ArtifactMatchRuns_ArtifactNormalizations_TenantId_ArtifactNo~",
                        columns: x => new { x.TenantId, x.ArtifactNormalizationId },
                        principalTable: "ArtifactNormalizations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtifactMatchRuns_IntakeArtifacts_TenantId_IntakeArtifactId",
                        columns: x => new { x.TenantId, x.IntakeArtifactId },
                        principalTable: "IntakeArtifacts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MatchingProfileDefinitions",
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
                    ScoringVersion = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsSystemDefined = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefinitionJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchingProfileDefinitions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ArtifactDuplicateSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactMatchRunId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DuplicateType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelatedArtifactId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RelatedBusinessEntityType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelatedBusinessEntityId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Score = table.Column<decimal>(type: "decimal(6,5)", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactDuplicateSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtifactDuplicateSignals_ArtifactMatchRuns_TenantId_Artifact~",
                        columns: x => new { x.TenantId, x.ArtifactMatchRunId },
                        principalTable: "ArtifactMatchRuns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ArtifactEntityMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactMatchRunId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EntityType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CandidateEntityId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CandidateDisplayLabel = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Score = table.Column<decimal>(type: "decimal(6,5)", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    MatchStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsTopCandidate = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MatchedFieldCount = table.Column<int>(type: "int", nullable: false),
                    ConflictingFieldCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactEntityMatches", x => x.Id);
                    table.UniqueConstraint("AK_ArtifactEntityMatches_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ArtifactEntityMatches_ArtifactMatchRuns_TenantId_ArtifactMat~",
                        columns: x => new { x.TenantId, x.ArtifactMatchRunId },
                        principalTable: "ArtifactMatchRuns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ArtifactMatchFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactEntityMatchId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceNormalizedFactId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FactCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CandidateFieldName = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ComparisonMethod = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FieldScore = table.Column<decimal>(type: "decimal(6,5)", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(6,5)", nullable: false),
                    EffectiveWeight = table.Column<decimal>(type: "decimal(6,5)", nullable: false),
                    WeightedScore = table.Column<decimal>(type: "decimal(8,5)", nullable: false),
                    MatchOutcome = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonCode = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactMatchFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtifactMatchFields_ArtifactEntityMatches_TenantId_ArtifactE~",
                        columns: x => new { x.TenantId, x.ArtifactEntityMatchId },
                        principalTable: "ArtifactEntityMatches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "MatchingProfileDefinitions",
                columns: new[] { "Id", "Code", "CreatedAt", "DefinitionJson", "Description", "DisplayName", "IsActive", "IsSystemDefined", "ScoringVersion", "UpdatedAt", "Version" },
                values: new object[] { new Guid("5a54cc3e-748d-4f3d-b10b-000000000001"), "LIEN_INTAKE_MATCHING_V1", new DateTimeOffset(new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\n  \"entityTypes\":[\"PATIENT\",\"PROVIDER\",\"FACILITY\",\"ATTORNEY\",\"LAW_FIRM\",\"CASE\"],\n  \"entityRules\":{\n    \"PATIENT\":{\n      \"fields\":[\n        {\"factCode\":\"PATIENT_NAME\",\"candidateFieldName\":\"PATIENT_NAME\",\"comparisonMethod\":\"PERSON_NAME\",\"weight\":0.25,\"conflictPenalty\":0.20,\"hardConflict\":false},\n        {\"factCode\":\"DATE_OF_BIRTH\",\"candidateFieldName\":\"DATE_OF_BIRTH\",\"comparisonMethod\":\"EXACT\",\"weight\":0.30,\"conflictPenalty\":0.45,\"hardConflict\":true},\n        {\"factCode\":\"PATIENT_IDENTIFIER\",\"candidateFieldName\":\"PATIENT_IDENTIFIER\",\"comparisonMethod\":\"EXACT\",\"weight\":0.30,\"conflictPenalty\":0.45,\"hardConflict\":true},\n        {\"factCode\":\"ACCOUNT_NUMBER\",\"candidateFieldName\":\"ACCOUNT_NUMBER\",\"comparisonMethod\":\"EXACT\",\"weight\":0.15,\"conflictPenalty\":0.25,\"hardConflict\":false}\n      ],\n      \"strongThreshold\":0.80,\n      \"possibleThreshold\":0.50,\n      \"strongMinimumMatchedFields\":2,\n      \"strongRequiresHardIdentifier\":true,\n      \"hardConflictMaximumScore\":0.49\n    },\n    \"PROVIDER\":{\n      \"fields\":[\n        {\"factCode\":\"PROVIDER_NAME\",\"candidateFieldName\":\"PROVIDER_NAME\",\"comparisonMethod\":\"ORGANIZATION\",\"weight\":0.50,\"conflictPenalty\":0.25,\"hardConflict\":false},\n        {\"factCode\":\"PROVIDER_PHONE\",\"candidateFieldName\":\"PROVIDER_PHONE\",\"comparisonMethod\":\"EXACT\",\"weight\":0.25,\"conflictPenalty\":0.25,\"hardConflict\":false},\n        {\"factCode\":\"FACILITY_ADDRESS\",\"candidateFieldName\":\"PROVIDER_ADDRESS\",\"comparisonMethod\":\"ADDRESS\",\"weight\":0.25,\"conflictPenalty\":0.20,\"hardConflict\":false}\n      ],\n      \"strongThreshold\":0.80,\n      \"possibleThreshold\":0.50,\n      \"strongMinimumMatchedFields\":2,\n      \"strongRequiresHardIdentifier\":false,\n      \"hardConflictMaximumScore\":0.59\n    },\n    \"FACILITY\":{\n      \"fields\":[\n        {\"factCode\":\"PROVIDER_NAME\",\"candidateFieldName\":\"FACILITY_NAME\",\"comparisonMethod\":\"ORGANIZATION\",\"weight\":0.50,\"conflictPenalty\":0.25,\"hardConflict\":false},\n        {\"factCode\":\"PROVIDER_PHONE\",\"candidateFieldName\":\"FACILITY_PHONE\",\"comparisonMethod\":\"EXACT\",\"weight\":0.25,\"conflictPenalty\":0.25,\"hardConflict\":false},\n        {\"factCode\":\"FACILITY_ADDRESS\",\"candidateFieldName\":\"FACILITY_ADDRESS\",\"comparisonMethod\":\"ADDRESS\",\"weight\":0.25,\"conflictPenalty\":0.20,\"hardConflict\":false}\n      ],\n      \"strongThreshold\":0.80,\n      \"possibleThreshold\":0.50,\n      \"strongMinimumMatchedFields\":2,\n      \"strongRequiresHardIdentifier\":false,\n      \"hardConflictMaximumScore\":0.59\n    },\n    \"ATTORNEY\":{\n      \"fields\":[\n        {\"factCode\":\"ATTORNEY_EMAIL\",\"candidateFieldName\":\"ATTORNEY_EMAIL\",\"comparisonMethod\":\"EXACT\",\"weight\":0.45,\"conflictPenalty\":0.45,\"hardConflict\":true},\n        {\"factCode\":\"ATTORNEY_NAME\",\"candidateFieldName\":\"ATTORNEY_NAME\",\"comparisonMethod\":\"PERSON_NAME\",\"weight\":0.30,\"conflictPenalty\":0.20,\"hardConflict\":false},\n        {\"factCode\":\"LAW_FIRM_NAME\",\"candidateFieldName\":\"LAW_FIRM_NAME\",\"comparisonMethod\":\"ORGANIZATION\",\"weight\":0.15,\"conflictPenalty\":0.15,\"hardConflict\":false},\n        {\"factCode\":\"ATTORNEY_PHONE\",\"candidateFieldName\":\"ATTORNEY_PHONE\",\"comparisonMethod\":\"EXACT\",\"weight\":0.10,\"conflictPenalty\":0.10,\"hardConflict\":false}\n      ],\n      \"strongThreshold\":0.80,\n      \"possibleThreshold\":0.50,\n      \"strongMinimumMatchedFields\":2,\n      \"strongRequiresHardIdentifier\":true,\n      \"hardConflictMaximumScore\":0.49\n    },\n    \"LAW_FIRM\":{\n      \"fields\":[\n        {\"factCode\":\"LAW_FIRM_NAME\",\"candidateFieldName\":\"LAW_FIRM_NAME\",\"comparisonMethod\":\"ORGANIZATION\",\"weight\":0.75,\"conflictPenalty\":0.35,\"hardConflict\":false},\n        {\"factCode\":\"ATTORNEY_EMAIL\",\"candidateFieldName\":\"ATTORNEY_EMAIL\",\"comparisonMethod\":\"EXACT\",\"weight\":0.125,\"conflictPenalty\":0.10,\"hardConflict\":false},\n        {\"factCode\":\"ATTORNEY_PHONE\",\"candidateFieldName\":\"ATTORNEY_PHONE\",\"comparisonMethod\":\"EXACT\",\"weight\":0.125,\"conflictPenalty\":0.10,\"hardConflict\":false}\n      ],\n      \"strongThreshold\":0.80,\n      \"possibleThreshold\":0.50,\n      \"strongMinimumMatchedFields\":1,\n      \"strongRequiresHardIdentifier\":false,\n      \"hardConflictMaximumScore\":0.59\n    },\n    \"CASE\":{\n      \"fields\":[\n        {\"factCode\":\"CASE_NUMBER\",\"candidateFieldName\":\"CASE_NUMBER\",\"comparisonMethod\":\"EXACT\",\"weight\":0.45,\"conflictPenalty\":0.45,\"hardConflict\":true},\n        {\"factCode\":\"PATIENT_NAME\",\"candidateFieldName\":\"PATIENT_NAME\",\"comparisonMethod\":\"PERSON_NAME\",\"weight\":0.20,\"conflictPenalty\":0.15,\"hardConflict\":false},\n        {\"factCode\":\"CLAIM_NUMBER\",\"candidateFieldName\":\"CLAIM_NUMBER\",\"comparisonMethod\":\"EXACT\",\"weight\":0.15,\"conflictPenalty\":0.25,\"hardConflict\":false},\n        {\"factCode\":\"DATE_OF_ACCIDENT\",\"candidateFieldName\":\"DATE_OF_ACCIDENT\",\"comparisonMethod\":\"EXACT\",\"weight\":0.10,\"conflictPenalty\":0.15,\"hardConflict\":false},\n        {\"factCode\":\"ATTORNEY_NAME\",\"candidateFieldName\":\"ATTORNEY_NAME\",\"comparisonMethod\":\"PERSON_NAME\",\"weight\":0.10,\"conflictPenalty\":0.10,\"hardConflict\":false}\n      ],\n      \"strongThreshold\":0.80,\n      \"possibleThreshold\":0.50,\n      \"strongMinimumMatchedFields\":2,\n      \"strongRequiresHardIdentifier\":true,\n      \"hardConflictMaximumScore\":0.49\n    }\n  },\n  \"primaryDuplicateRule\":{\n    \"code\":\"PATIENT_PROVIDER_ACCOUNT_SERVICE_DATE\",\n    \"duplicateType\":\"BUSINESS_KEY_DUPLICATE\",\n    \"requiredFactCodes\":[\"PATIENT_NAME\",\"PROVIDER_NAME\",\"ACCOUNT_NUMBER\",\"DATE_OF_SERVICE_START\"],\n    \"requiredEntityTypes\":[\"PATIENT\",\"PROVIDER\"],\n    \"score\":0.90,\n    \"status\":\"POSSIBLE\"\n  }\n}", "Deterministic tenant-scoped candidate matching and duplicate signals.", "Lien Intake Matching V1", true, true, "B10-SCORE-1", new DateTimeOffset(new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactDuplicateSignals_ArtifactMatchRunId_DuplicateType_Re~",
                table: "ArtifactDuplicateSignals",
                columns: new[] { "ArtifactMatchRunId", "DuplicateType", "RelatedArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactDuplicateSignals_TenantId_ArtifactMatchRunId",
                table: "ArtifactDuplicateSignals",
                columns: new[] { "TenantId", "ArtifactMatchRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactEntityMatches_ArtifactMatchRunId_EntityType_Candidat~",
                table: "ArtifactEntityMatches",
                columns: new[] { "ArtifactMatchRunId", "EntityType", "CandidateEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactEntityMatches_ArtifactMatchRunId_EntityType_Rank",
                table: "ArtifactEntityMatches",
                columns: new[] { "ArtifactMatchRunId", "EntityType", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactEntityMatches_TenantId_ArtifactMatchRunId",
                table: "ArtifactEntityMatches",
                columns: new[] { "TenantId", "ArtifactMatchRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactMatchFields_ArtifactEntityMatchId_SourceNormalizedF~1",
                table: "ArtifactMatchFields",
                columns: new[] { "ArtifactEntityMatchId", "SourceNormalizedFactId", "FactCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactMatchFields_ArtifactEntityMatchId_SourceNormalizedFa~",
                table: "ArtifactMatchFields",
                columns: new[] { "ArtifactEntityMatchId", "SourceNormalizedFactId", "CandidateFieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactMatchFields_TenantId_ArtifactEntityMatchId",
                table: "ArtifactMatchFields",
                columns: new[] { "TenantId", "ArtifactEntityMatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactMatchRuns_ExecutionKey",
                table: "ArtifactMatchRuns",
                column: "ExecutionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactMatchRuns_TenantId_ArtifactNormalizationId_Status",
                table: "ArtifactMatchRuns",
                columns: new[] { "TenantId", "ArtifactNormalizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactMatchRuns_TenantId_BusinessKeyFingerprint_Status",
                table: "ArtifactMatchRuns",
                columns: new[] { "TenantId", "BusinessKeyFingerprint", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactMatchRuns_TenantId_IntakeArtifactId_CurrentResultMar~",
                table: "ArtifactMatchRuns",
                columns: new[] { "TenantId", "IntakeArtifactId", "CurrentResultMarker" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchingProfileDefinitions_Code_Version",
                table: "MatchingProfileDefinitions",
                columns: new[] { "Code", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtifactDuplicateSignals");

            migrationBuilder.DropTable(
                name: "ArtifactMatchFields");

            migrationBuilder.DropTable(
                name: "MatchingProfileDefinitions");

            migrationBuilder.DropTable(
                name: "ArtifactEntityMatches");

            migrationBuilder.DropTable(
                name: "ArtifactMatchRuns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_IntakeArtifacts_TenantId_Id",
                table: "IntakeArtifacts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ArtifactNormalizations_TenantId_Id",
                table: "ArtifactNormalizations");
        }
    }
}
