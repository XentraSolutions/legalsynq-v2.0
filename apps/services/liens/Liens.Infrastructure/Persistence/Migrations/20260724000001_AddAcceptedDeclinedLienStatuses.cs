using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(LiensDbContext))]
    [Migration("20260724000001_AddAcceptedDeclinedLienStatuses")]
    public partial class AddAcceptedDeclinedLienStatuses : Migration
    {
        private const string SystemUserId = "00000000-0000-0000-0000-000000000001";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'LienStatus', s.Code, s.Name, s.Description, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'Accepted' AS Code, 'Accepted' AS Name, 'Buyer accepted the offered lien' AS Description, 10 AS Sort UNION ALL
                    SELECT 'Declined',         'Declined',         'Buyer declined the offered lien',                  11
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'LienStatus' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);

            migrationBuilder.Sql("""
                UPDATE `liens_Liens` l
                SET l.`Status` = 'Accepted',
                    l.`SellerStatus` = 'Accepted',
                    l.`UpdatedAtUtc` = NOW()
                WHERE l.`Status` IN ('Offered', 'UnderReview')
                  AND EXISTS (
                    SELECT 1
                    FROM `liens_SellingBuyerAccessLinks` link
                    WHERE link.`TenantId` = l.`TenantId`
                      AND link.`LienId` = l.`Id`
                      AND link.`ResponseStatus` = 'Accepted'
                  );
                """);

            migrationBuilder.Sql("""
                UPDATE `liens_Liens` l
                SET l.`Status` = 'Declined',
                    l.`SellerStatus` = 'Declined',
                    l.`ClosedAtUtc` = COALESCE(l.`ClosedAtUtc`, NOW()),
                    l.`UpdatedAtUtc` = NOW()
                WHERE l.`Status` IN ('Offered', 'UnderReview', 'Withdrawn')
                  AND EXISTS (
                    SELECT 1
                    FROM `liens_SellingBuyerAccessLinks` link
                    WHERE link.`TenantId` = l.`TenantId`
                      AND link.`LienId` = l.`Id`
                      AND link.`ResponseStatus` = 'Declined'
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE `liens_Liens` l
                SET l.`Status` = 'UnderReview',
                    l.`SellerStatus` = 'SubmittedForSale',
                    l.`UpdatedAtUtc` = NOW()
                WHERE l.`Status` = 'Accepted'
                  AND EXISTS (
                    SELECT 1
                    FROM `liens_SellingBuyerAccessLinks` link
                    WHERE link.`TenantId` = l.`TenantId`
                      AND link.`LienId` = l.`Id`
                      AND link.`ResponseStatus` = 'Accepted'
                  );
                """);

            migrationBuilder.Sql("""
                UPDATE `liens_Liens` l
                SET l.`Status` = 'Withdrawn',
                    l.`SellerStatus` = 'Withdrawn',
                    l.`WithdrawnAtUtc` = COALESCE(l.`WithdrawnAtUtc`, l.`ClosedAtUtc`, NOW()),
                    l.`UpdatedAtUtc` = NOW()
                WHERE l.`Status` = 'Declined'
                  AND EXISTS (
                    SELECT 1
                    FROM `liens_SellingBuyerAccessLinks` link
                    WHERE link.`TenantId` = l.`TenantId`
                      AND link.`LienId` = l.`Id`
                      AND link.`ResponseStatus` = 'Declined'
                  );
                """);

            migrationBuilder.Sql("""
                DELETE FROM `liens_LookupValues`
                WHERE `TenantId` IS NULL
                  AND `Category` = 'LienStatus'
                  AND `Code` IN ('Accepted', 'Declined');
                """);
        }
    }
}
