using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundEmailRepository : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboundEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrgId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TenantIntakeSourceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceConfigurationVersion = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessingProfileCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantConfigurationVersion = table.Column<int>(type: "int", nullable: true),
                    TenantProfileConfigurationVersion = table.Column<int>(type: "int", nullable: true),
                    Provider = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderMessageId = table.Column<string>(type: "varchar(768)", maxLength: 768, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderThreadId = table.Column<string>(type: "varchar(768)", maxLength: 768, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InternetMessageId = table.Column<string>(type: "varchar(768)", maxLength: 768, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InReplyToMessageId = table.Column<string>(type: "varchar(768)", maxLength: 768, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReferencesJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    ProviderCreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    FromAddress = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromDisplayName = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SenderAddress = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SenderDisplayName = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReplyToAddress = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReplyToDisplayName = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Subject = table.Column<string>(type: "varchar(998)", maxLength: 998, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TextBody = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HtmlBody = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HeadersJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawMessageContent = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawMessageHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawMessageSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    HasAttachments = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AttachmentCount = table.Column<int>(type: "int", nullable: false),
                    CaptureStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessingStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdempotencyKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DuplicateCaptureCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundEmails_TenantIntakeSources_TenantIntakeSourceId",
                        column: x => x.TenantIntakeSourceId,
                        principalTable: "TenantIntakeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InboundEmailAttachmentMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    InboundEmailId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProviderAttachmentId = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileName = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentDisposition = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentId = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsInline = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEmailAttachmentMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundEmailAttachmentMetadata_InboundEmails_InboundEmailId",
                        column: x => x.InboundEmailId,
                        principalTable: "InboundEmails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InboundEmailRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    InboundEmailId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RecipientType = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmailAddress = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedEmailAddress = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ordinal = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEmailRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundEmailRecipients_InboundEmails_InboundEmailId",
                        column: x => x.InboundEmailId,
                        principalTable: "InboundEmails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailAttachmentMetadata_InboundEmailId_Ordinal",
                table: "InboundEmailAttachmentMetadata",
                columns: new[] { "InboundEmailId", "Ordinal" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailRecipients_InboundEmailId_RecipientType",
                table: "InboundEmailRecipients",
                columns: new[] { "InboundEmailId", "RecipientType" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmails_IdempotencyKey",
                table: "InboundEmails",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmails_TenantId_CaptureStatus_ReceivedAt",
                table: "InboundEmails",
                columns: new[] { "TenantId", "CaptureStatus", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmails_TenantId_Provider_ReceivedAt",
                table: "InboundEmails",
                columns: new[] { "TenantId", "Provider", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmails_TenantId_Purpose_ReceivedAt",
                table: "InboundEmails",
                columns: new[] { "TenantId", "Purpose", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmails_TenantId_ReceivedAt",
                table: "InboundEmails",
                columns: new[] { "TenantId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmails_TenantId_TenantIntakeSourceId_ReceivedAt",
                table: "InboundEmails",
                columns: new[] { "TenantId", "TenantIntakeSourceId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmails_TenantIntakeSourceId",
                table: "InboundEmails",
                column: "TenantIntakeSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboundEmailAttachmentMetadata");

            migrationBuilder.DropTable(
                name: "InboundEmailRecipients");

            migrationBuilder.DropTable(
                name: "InboundEmails");
        }
    }
}
