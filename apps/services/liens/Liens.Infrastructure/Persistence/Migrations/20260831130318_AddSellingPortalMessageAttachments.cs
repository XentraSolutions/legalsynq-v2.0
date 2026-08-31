using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellingPortalMessageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "liens_SellingPortalMessageAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LienId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SellerOrgId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuyerOrgId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuyerContactId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AccessLinkId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MessageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DocumentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingPortalMessageAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_liens_SellingPortalMessageAttachments_liens_Liens_LienId",
                        column: x => x.LienId,
                        principalTable: "liens_Liens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liens_SellingPortalMessageAttachments_liens_SellingBuyerAcce~",
                        column: x => x.AccessLinkId,
                        principalTable: "liens_SellingBuyerAccessLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liens_SellingPortalMessageAttachments_liens_SellingPortalMes~",
                        column: x => x.MessageId,
                        principalTable: "liens_SellingPortalMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPortalMessageAttachments_AccessLinkId",
                table: "liens_SellingPortalMessageAttachments",
                column: "AccessLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPortalMessageAttachments_LienId",
                table: "liens_SellingPortalMessageAttachments",
                column: "LienId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPortalMessageAttachments_MessageId",
                table: "liens_SellingPortalMessageAttachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortalMessageAttachments_Tenant_Document",
                table: "liens_SellingPortalMessageAttachments",
                columns: new[] { "TenantId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortalMessageAttachments_Tenant_Lien_Participants",
                table: "liens_SellingPortalMessageAttachments",
                columns: new[] { "TenantId", "LienId", "SellerOrgId", "BuyerOrgId", "BuyerContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortalMessageAttachments_Tenant_Message_Created",
                table: "liens_SellingPortalMessageAttachments",
                columns: new[] { "TenantId", "MessageId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "liens_SellingPortalMessageAttachments");
        }
    }
}
