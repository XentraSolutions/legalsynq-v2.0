using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellingPortfolios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "liens_SellingPortfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SellerOrgId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PortfolioNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LienCount = table.Column<int>(type: "int", nullable: false),
                    OriginalAmountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentBalanceTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OfferPriceTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingPortfolios", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "liens_SellingPortfolioBuyers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PortfolioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuyerOrgId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingPortfolioBuyers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_liens_SellingPortfolioBuyers_liens_SellingPortfolios_Portfol~",
                        column: x => x.PortfolioId,
                        principalTable: "liens_SellingPortfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "liens_SellingPortfolioLiens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PortfolioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LienId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LienNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LienExternalId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CaseExternalId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FacilityId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LienType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LienLifecycleStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OfferPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PayoffAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SubjectFirstName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubjectLastName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Jurisdiction = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IncidentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingPortfolioLiens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_liens_SellingPortfolioLiens_liens_Liens_LienId",
                        column: x => x.LienId,
                        principalTable: "liens_Liens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liens_SellingPortfolioLiens_liens_SellingPortfolios_Portfoli~",
                        column: x => x.PortfolioId,
                        principalTable: "liens_SellingPortfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "liens_SellingPortfolioStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PortfolioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FromStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingPortfolioStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_liens_SellingPortfolioStatusHistory_liens_SellingPortfolios_~",
                        column: x => x.PortfolioId,
                        principalTable: "liens_SellingPortfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPortfolioBuyers_PortfolioId",
                table: "liens_SellingPortfolioBuyers",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortfolioBuyers_TenantId_BuyerOrgId",
                table: "liens_SellingPortfolioBuyers",
                columns: new[] { "TenantId", "BuyerOrgId" });

            migrationBuilder.CreateIndex(
                name: "UX_SellingPortfolioBuyers_TenantId_PortfolioId_BuyerOrgId",
                table: "liens_SellingPortfolioBuyers",
                columns: new[] { "TenantId", "PortfolioId", "BuyerOrgId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPortfolioLiens_LienId",
                table: "liens_SellingPortfolioLiens",
                column: "LienId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPortfolioLiens_PortfolioId",
                table: "liens_SellingPortfolioLiens",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortfolioLiens_TenantId_CaseId",
                table: "liens_SellingPortfolioLiens",
                columns: new[] { "TenantId", "CaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortfolioLiens_TenantId_LienId",
                table: "liens_SellingPortfolioLiens",
                columns: new[] { "TenantId", "LienId" });

            migrationBuilder.CreateIndex(
                name: "UX_SellingPortfolioLiens_TenantId_PortfolioId_LienId",
                table: "liens_SellingPortfolioLiens",
                columns: new[] { "TenantId", "PortfolioId", "LienId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortfolios_TenantId_SellerOrgId_Status",
                table: "liens_SellingPortfolios",
                columns: new[] { "TenantId", "SellerOrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortfolios_TenantId_Status",
                table: "liens_SellingPortfolios",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_SellingPortfolios_TenantId_PortfolioNumber",
                table: "liens_SellingPortfolios",
                columns: new[] { "TenantId", "PortfolioNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPortfolioStatusHistory_PortfolioId",
                table: "liens_SellingPortfolioStatusHistory",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_SellingPortfolioStatusHistory_TenantId_PortfolioId_ChangedAtUtc",
                table: "liens_SellingPortfolioStatusHistory",
                columns: new[] { "TenantId", "PortfolioId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "liens_SellingPortfolioBuyers");

            migrationBuilder.DropTable(
                name: "liens_SellingPortfolioLiens");

            migrationBuilder.DropTable(
                name: "liens_SellingPortfolioStatusHistory");

            migrationBuilder.DropTable(
                name: "liens_SellingPortfolios");
        }
    }
}
