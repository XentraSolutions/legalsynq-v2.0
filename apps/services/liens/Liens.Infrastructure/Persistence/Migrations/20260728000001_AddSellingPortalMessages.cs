using System;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LiensDbContext))]
    [Migration("20260728000001_AddSellingPortalMessages")]
    public partial class AddSellingPortalMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "liens_SellingPortalMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LienId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SellerOrgId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuyerOrgId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuyerContactId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AccessLinkId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SenderType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SenderName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SenderEmail = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Message = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingPortalMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_liens_SellingPortalMessages_liens_Liens_LienId",
                        column: x => x.LienId,
                        principalTable: "liens_Liens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liens_SellingPortalMessages_liens_SellingBuyerAccessLinks_AccessLinkId",
                        column: x => x.AccessLinkId,
                        principalTable: "liens_SellingBuyerAccessLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortalMessages_Tenant_AccessLink_Created",
                table: "liens_SellingPortalMessages",
                columns: new[] { "TenantId", "AccessLinkId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortalMessages_Tenant_Lien_Participants_Created",
                table: "liens_SellingPortalMessages",
                columns: new[] { "TenantId", "LienId", "SellerOrgId", "BuyerOrgId", "BuyerContactId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPortalMessages_AccessLinkId",
                table: "liens_SellingPortalMessages",
                column: "AccessLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPortalMessages_LienId",
                table: "liens_SellingPortalMessages",
                column: "LienId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "liens_SellingPortalMessages");
        }
    }
}
