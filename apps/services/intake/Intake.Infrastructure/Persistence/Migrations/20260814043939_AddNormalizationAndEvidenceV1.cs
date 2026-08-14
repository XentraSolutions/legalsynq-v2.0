using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizationAndEvidenceV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArtifactNormalizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IntakeArtifactId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactExtractionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NormalizationProfileCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizationProfileVersion = table.Column<int>(type: "int", nullable: false),
                    NormalizationVersion = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExecutionKey = table.Column<string>(type: "varchar(192)", maxLength: 192, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CurrentResultMarker = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
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
                    table.PrimaryKey("PK_ArtifactNormalizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtifactNormalizations_ArtifactExtractions_ArtifactExtractio~",
                        column: x => x.ArtifactExtractionId,
                        principalTable: "ArtifactExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtifactNormalizations_IntakeArtifacts_IntakeArtifactId",
                        column: x => x.IntakeArtifactId,
                        principalTable: "IntakeArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NormalizationProfileDefinitions",
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
                    SupportedFactCodesJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizerVersion = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnicodeForm = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ComparisonKeyStrategy = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultDateCulture = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultCountryCode = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultCurrencyCode = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NormalizationProfileDefinitions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ArtifactNormalizedFacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactNormalizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ArtifactExtractedFactId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FactCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DataType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawValue = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedValue = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ComparisonKey = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizationStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValidationStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizationMethod = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizationVersion = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceConfidence = table.Column<double>(type: "double", nullable: false),
                    WarningCodesJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvidenceReferenceJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactNormalizedFacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtifactNormalizedFacts_ArtifactExtractedFacts_ArtifactExtra~",
                        column: x => x.ArtifactExtractedFactId,
                        principalTable: "ArtifactExtractedFacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtifactNormalizedFacts_ArtifactNormalizations_ArtifactNorma~",
                        column: x => x.ArtifactNormalizationId,
                        principalTable: "ArtifactNormalizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "NormalizationProfileDefinitions",
                columns: new[] { "Id", "Code", "ComparisonKeyStrategy", "CreatedAt", "DefaultCountryCode", "DefaultCurrencyCode", "DefaultDateCulture", "Description", "DisplayName", "IsActive", "IsSystemDefined", "NormalizerVersion", "SupportedFactCodesJson", "UnicodeForm", "UpdatedAt", "Version" },
                values: new object[] { new Guid("11111111-1111-4111-8111-111111113001"), "LIEN_INTAKE_NORMALIZATION_V1", "UPPER_ASCII_ALNUM", new DateTimeOffset(new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "US", "USD", "en-US", "Deterministic comparison candidates and structural validation over B08 source facts.", "Lien Intake Normalization V1", true, true, "1", "[{\"code\":\"PATIENT_NAME\",\"dataType\":\"NAME\"},{\"code\":\"PATIENT_IDENTIFIER\",\"dataType\":\"IDENTIFIER\"},{\"code\":\"DATE_OF_BIRTH\",\"dataType\":\"DATE\"},{\"code\":\"PROVIDER_NAME\",\"dataType\":\"NAME\"},{\"code\":\"PROVIDER_IDENTIFIER\",\"dataType\":\"IDENTIFIER\"},{\"code\":\"DATE_OF_SERVICE_START\",\"dataType\":\"DATE\"},{\"code\":\"DATE_OF_SERVICE_END\",\"dataType\":\"DATE\"},{\"code\":\"INVOICE_NUMBER\",\"dataType\":\"IDENTIFIER\"},{\"code\":\"ACCOUNT_NUMBER\",\"dataType\":\"IDENTIFIER\"},{\"code\":\"LIEN_AMOUNT\",\"dataType\":\"MONEY\"},{\"code\":\"BILLED_AMOUNT\",\"dataType\":\"MONEY\"},{\"code\":\"PAID_AMOUNT\",\"dataType\":\"MONEY\"},{\"code\":\"BALANCE_AMOUNT\",\"dataType\":\"MONEY\"},{\"code\":\"SETTLEMENT_AMOUNT\",\"dataType\":\"MONEY\"},{\"code\":\"INSURER_NAME\",\"dataType\":\"NAME\"},{\"code\":\"CLAIM_NUMBER\",\"dataType\":\"IDENTIFIER\"},{\"code\":\"POLICY_NUMBER\",\"dataType\":\"IDENTIFIER\"},{\"code\":\"ATTORNEY_NAME\",\"dataType\":\"NAME\"},{\"code\":\"LAW_FIRM_NAME\",\"dataType\":\"NAME\"},{\"code\":\"LETTER_DATE\",\"dataType\":\"DATE\"},{\"code\":\"DOCUMENT_DATE\",\"dataType\":\"DATE\"},{\"code\":\"DOCUMENT_TITLE\",\"dataType\":\"TEXT\"},{\"code\":\"FACILITY_ADDRESS\",\"dataType\":\"ADDRESS\"},{\"code\":\"EFFECTIVE_DATE\",\"dataType\":\"DATE\"},{\"code\":\"EXPIRATION_DATE\",\"dataType\":\"DATE\"}]", "NFKC", new DateTimeOffset(new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactNormalizations_ArtifactExtractionId",
                table: "ArtifactNormalizations",
                column: "ArtifactExtractionId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactNormalizations_ExecutionKey",
                table: "ArtifactNormalizations",
                column: "ExecutionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactNormalizations_IntakeArtifactId",
                table: "ArtifactNormalizations",
                column: "IntakeArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactNormalizations_TenantId_IntakeArtifactId_ArtifactExt~",
                table: "ArtifactNormalizations",
                columns: new[] { "TenantId", "IntakeArtifactId", "ArtifactExtractionId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactNormalizations_TenantId_IntakeArtifactId_CurrentResu~",
                table: "ArtifactNormalizations",
                columns: new[] { "TenantId", "IntakeArtifactId", "CurrentResultMarker" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactNormalizedFacts_ArtifactExtractedFactId",
                table: "ArtifactNormalizedFacts",
                column: "ArtifactExtractedFactId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactNormalizedFacts_ArtifactNormalizationId_ArtifactExtr~",
                table: "ArtifactNormalizedFacts",
                columns: new[] { "ArtifactNormalizationId", "ArtifactExtractedFactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactNormalizedFacts_TenantId_ArtifactNormalizationId_Fac~",
                table: "ArtifactNormalizedFacts",
                columns: new[] { "TenantId", "ArtifactNormalizationId", "FactCode" });

            migrationBuilder.CreateIndex(
                name: "IX_NormalizationProfileDefinitions_Code_IsActive",
                table: "NormalizationProfileDefinitions",
                columns: new[] { "Code", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_NormalizationProfileDefinitions_Code_Version",
                table: "NormalizationProfileDefinitions",
                columns: new[] { "Code", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtifactNormalizedFacts");

            migrationBuilder.DropTable(
                name: "NormalizationProfileDefinitions");

            migrationBuilder.DropTable(
                name: "ArtifactNormalizations");
        }
    }
}
