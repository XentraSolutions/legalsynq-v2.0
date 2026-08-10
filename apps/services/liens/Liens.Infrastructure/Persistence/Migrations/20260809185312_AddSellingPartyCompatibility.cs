using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellingPartyCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BuyerCompanyId",
                table: "liens_SellingPortfolioBuyers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "BuyerCompanyContactPersonId",
                table: "liens_SellingBuyerAccessLinks",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "BuyerCompanyId",
                table: "liens_SellingBuyerAccessLinks",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "FundingCompanyCompanyId",
                table: "liens_Liens",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "FundingCompanyContactPersonId",
                table: "liens_Liens",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "MedicalFacilityCompanyId",
                table: "liens_Liens",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "MedicalProviderCompanyId",
                table: "liens_Liens",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "BuyerCompanyId",
                table: "liens_LienOffers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "CaseManagerContactPersonId",
                table: "liens_Cases",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "HandlingLawFirmCompanyId",
                table: "liens_Cases",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "liens_SellingPartyAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ScopeKind = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScopeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Namespace = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WorkflowProvenance = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExternalId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CompanyId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CompanyContactPersonId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsPreferred = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PreferredCompanyKey = table.Column<Guid>(type: "char(36)", nullable: true, computedColumnSql: "CASE WHEN `IsPreferred` = 1 THEN `CompanyId` ELSE NULL END", stored: true, collation: "ascii_general_ci"),
                    PreferredContactPersonKey = table.Column<Guid>(type: "char(36)", nullable: true, computedColumnSql: "CASE WHEN `IsPreferred` = 1 THEN `CompanyContactPersonId` ELSE NULL END", stored: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingPartyAliases", x => x.Id);
                    table.CheckConstraint("CK_SellingPartyAliases_ExactlyOneTarget", "(`CompanyId` IS NOT NULL AND `CompanyContactPersonId` IS NULL) OR (`CompanyId` IS NULL AND `CompanyContactPersonId` IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_liens_SellingPartyAliases_liens_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "liens_Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liens_SellingPartyAliases_liens_CompanyContactPersons_Compan~",
                        column: x => x.CompanyContactPersonId,
                        principalTable: "liens_CompanyContactPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "liens_SellingPartyBackfillCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Workflow = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastExternalId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessedCount = table.Column<int>(type: "int", nullable: false),
                    QuarantinedCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingPartyBackfillCheckpoints", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "liens_SellingPartyBackfillQuarantines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Namespace = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WorkflowProvenance = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExternalId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReasonCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Details = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingPartyBackfillQuarantines", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPortfolioBuyers_BuyerCompanyId",
                table: "liens_SellingPortfolioBuyers",
                column: "BuyerCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingBuyerAccessLinks_BuyerCompanyContactPersonId",
                table: "liens_SellingBuyerAccessLinks",
                column: "BuyerCompanyContactPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingBuyerAccessLinks_BuyerCompanyId",
                table: "liens_SellingBuyerAccessLinks",
                column: "BuyerCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_Liens_FundingCompanyCompanyId",
                table: "liens_Liens",
                column: "FundingCompanyCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_Liens_FundingCompanyContactPersonId",
                table: "liens_Liens",
                column: "FundingCompanyContactPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_Liens_MedicalFacilityCompanyId",
                table: "liens_Liens",
                column: "MedicalFacilityCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_Liens_MedicalProviderCompanyId",
                table: "liens_Liens",
                column: "MedicalProviderCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_LienOffers_BuyerCompanyId",
                table: "liens_LienOffers",
                column: "BuyerCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_Cases_CaseManagerContactPersonId",
                table: "liens_Cases",
                column: "CaseManagerContactPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_Cases_HandlingLawFirmCompanyId",
                table: "liens_Cases",
                column: "HandlingLawFirmCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPartyAliases_CompanyContactPersonId",
                table: "liens_SellingPartyAliases",
                column: "CompanyContactPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingPartyAliases_CompanyId",
                table: "liens_SellingPartyAliases",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UX_SellingPartyAliases_ExternalScope",
                table: "liens_SellingPartyAliases",
                columns: new[] { "TenantId", "ScopeKind", "ScopeId", "Namespace", "WorkflowProvenance", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SellingPartyAliases_PreferredCompany",
                table: "liens_SellingPartyAliases",
                columns: new[] { "TenantId", "ScopeKind", "ScopeId", "Namespace", "WorkflowProvenance", "PreferredCompanyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SellingPartyAliases_PreferredContact",
                table: "liens_SellingPartyAliases",
                columns: new[] { "TenantId", "ScopeKind", "ScopeId", "Namespace", "WorkflowProvenance", "PreferredContactPersonKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SellingPartyBackfillCheckpoints_Tenant_Workflow",
                table: "liens_SellingPartyBackfillCheckpoints",
                columns: new[] { "TenantId", "Workflow" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SellingPartyBackfillQuarantines_SourceReason",
                table: "liens_SellingPartyBackfillQuarantines",
                columns: new[] { "TenantId", "Namespace", "WorkflowProvenance", "ExternalId", "ReasonCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_liens_Cases_liens_Companies_HandlingLawFirmCompanyId",
                table: "liens_Cases",
                column: "HandlingLawFirmCompanyId",
                principalTable: "liens_Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_liens_Cases_liens_CompanyContactPersons_CaseManagerContactPe~",
                table: "liens_Cases",
                column: "CaseManagerContactPersonId",
                principalTable: "liens_CompanyContactPersons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_liens_LienOffers_liens_Companies_BuyerCompanyId",
                table: "liens_LienOffers",
                column: "BuyerCompanyId",
                principalTable: "liens_Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_liens_Liens_liens_Companies_FundingCompanyCompanyId",
                table: "liens_Liens",
                column: "FundingCompanyCompanyId",
                principalTable: "liens_Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_liens_Liens_liens_Companies_MedicalFacilityCompanyId",
                table: "liens_Liens",
                column: "MedicalFacilityCompanyId",
                principalTable: "liens_Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_liens_Liens_liens_Companies_MedicalProviderCompanyId",
                table: "liens_Liens",
                column: "MedicalProviderCompanyId",
                principalTable: "liens_Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_liens_Liens_liens_CompanyContactPersons_FundingCompanyContac~",
                table: "liens_Liens",
                column: "FundingCompanyContactPersonId",
                principalTable: "liens_CompanyContactPersons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_liens_SellingBuyerAccessLinks_liens_Companies_BuyerCompanyId",
                table: "liens_SellingBuyerAccessLinks",
                column: "BuyerCompanyId",
                principalTable: "liens_Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_liens_SellingBuyerAccessLinks_liens_CompanyContactPersons_Bu~",
                table: "liens_SellingBuyerAccessLinks",
                column: "BuyerCompanyContactPersonId",
                principalTable: "liens_CompanyContactPersons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_liens_SellingPortfolioBuyers_liens_Companies_BuyerCompanyId",
                table: "liens_SellingPortfolioBuyers",
                column: "BuyerCompanyId",
                principalTable: "liens_Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_liens_Cases_liens_Companies_HandlingLawFirmCompanyId",
                table: "liens_Cases");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_Cases_liens_CompanyContactPersons_CaseManagerContactPe~",
                table: "liens_Cases");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_LienOffers_liens_Companies_BuyerCompanyId",
                table: "liens_LienOffers");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_Liens_liens_Companies_FundingCompanyCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_Liens_liens_Companies_MedicalFacilityCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_Liens_liens_Companies_MedicalProviderCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_Liens_liens_CompanyContactPersons_FundingCompanyContac~",
                table: "liens_Liens");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_SellingBuyerAccessLinks_liens_Companies_BuyerCompanyId",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_SellingBuyerAccessLinks_liens_CompanyContactPersons_Bu~",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_SellingPortfolioBuyers_liens_Companies_BuyerCompanyId",
                table: "liens_SellingPortfolioBuyers");

            migrationBuilder.DropTable(
                name: "liens_SellingPartyAliases");

            migrationBuilder.DropTable(
                name: "liens_SellingPartyBackfillCheckpoints");

            migrationBuilder.DropTable(
                name: "liens_SellingPartyBackfillQuarantines");

            migrationBuilder.DropIndex(
                name: "IX_liens_SellingPortfolioBuyers_BuyerCompanyId",
                table: "liens_SellingPortfolioBuyers");

            migrationBuilder.DropIndex(
                name: "IX_liens_SellingBuyerAccessLinks_BuyerCompanyContactPersonId",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropIndex(
                name: "IX_liens_SellingBuyerAccessLinks_BuyerCompanyId",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropIndex(
                name: "IX_liens_Liens_FundingCompanyCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_liens_Liens_FundingCompanyContactPersonId",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_liens_Liens_MedicalFacilityCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_liens_Liens_MedicalProviderCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_liens_LienOffers_BuyerCompanyId",
                table: "liens_LienOffers");

            migrationBuilder.DropIndex(
                name: "IX_liens_Cases_CaseManagerContactPersonId",
                table: "liens_Cases");

            migrationBuilder.DropIndex(
                name: "IX_liens_Cases_HandlingLawFirmCompanyId",
                table: "liens_Cases");

            migrationBuilder.DropColumn(
                name: "BuyerCompanyId",
                table: "liens_SellingPortfolioBuyers");

            migrationBuilder.DropColumn(
                name: "BuyerCompanyContactPersonId",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "BuyerCompanyId",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "FundingCompanyCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "FundingCompanyContactPersonId",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "MedicalFacilityCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "MedicalProviderCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "BuyerCompanyId",
                table: "liens_LienOffers");

            migrationBuilder.DropColumn(
                name: "CaseManagerContactPersonId",
                table: "liens_Cases");

            migrationBuilder.DropColumn(
                name: "HandlingLawFirmCompanyId",
                table: "liens_Cases");
        }
    }
}
