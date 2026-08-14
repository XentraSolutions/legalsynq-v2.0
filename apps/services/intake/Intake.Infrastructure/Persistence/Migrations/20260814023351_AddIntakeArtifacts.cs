using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrgId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    InboundEmailId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantIntakeSourceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceAttachmentMetadataId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ArtifactKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ArtifactType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ArtifactRole = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ArtifactOrdinal = table.Column<int>(type: "int", nullable: false),
                    SourceContentId = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalFileName = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EffectiveFileName = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeclaredContentType = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetectedContentType = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsInline = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsRetryable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    DocumentsServiceDocumentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DocumentsServiceVersionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DocumentsServiceReference = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeArtifacts_InboundEmailAttachmentMetadata_SourceAttachm~",
                        column: x => x.SourceAttachmentMetadataId,
                        principalTable: "InboundEmailAttachmentMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeArtifacts_InboundEmails_InboundEmailId",
                        column: x => x.InboundEmailId,
                        principalTable: "InboundEmails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeArtifacts_TenantIntakeSources_TenantIntakeSourceId",
                        column: x => x.TenantIntakeSourceId,
                        principalTable: "TenantIntakeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeArtifacts_DocumentsServiceDocumentId",
                table: "IntakeArtifacts",
                column: "DocumentsServiceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeArtifacts_InboundEmailId_ArtifactKey",
                table: "IntakeArtifacts",
                columns: new[] { "InboundEmailId", "ArtifactKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeArtifacts_SourceAttachmentMetadataId",
                table: "IntakeArtifacts",
                column: "SourceAttachmentMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeArtifacts_TenantId_InboundEmailId_ArtifactOrdinal",
                table: "IntakeArtifacts",
                columns: new[] { "TenantId", "InboundEmailId", "ArtifactOrdinal" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeArtifacts_TenantId_ProcessingStatus_UpdatedAt",
                table: "IntakeArtifacts",
                columns: new[] { "TenantId", "ProcessingStatus", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeArtifacts_TenantIntakeSourceId",
                table: "IntakeArtifacts",
                column: "TenantIntakeSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeArtifacts");
        }
    }
}
