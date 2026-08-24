using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityLinkedContactSubtype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactSubtype",
                table: "liens_Contacts",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "liens_Contacts",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "LawFirmId",
                table: "liens_Contacts",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_TenantId_FacilityId_ContactSubtype",
                table: "liens_Contacts",
                columns: new[] { "TenantId", "FacilityId", "ContactSubtype" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_TenantId_LawFirmId_ContactSubtype",
                table: "liens_Contacts",
                columns: new[] { "TenantId", "LawFirmId", "ContactSubtype" });

            migrationBuilder.Sql(
                """
                INSERT INTO `liens_Contacts`
                (
                    `Id`,
                    `TenantId`,
                    `OrgId`,
                    `FacilityId`,
                    `ContactType`,
                    `ContactSubtype`,
                    `FirstName`,
                    `LastName`,
                    `DisplayName`,
                    `Title`,
                    `Organization`,
                    `Email`,
                    `Phone`,
                    `Fax`,
                    `Website`,
                    `AddressLine1`,
                    `City`,
                    `State`,
                    `PostalCode`,
                    `Notes`,
                    `IsActive`,
                    `CreatedByUserId`,
                    `UpdatedByUserId`,
                    `CreatedAtUtc`,
                    `UpdatedAtUtc`
                )
                SELECT
                    p.`Id`,
                    p.`TenantId`,
                    f.`OrgId`,
                    p.`FacilityId`,
                    'Facility',
                    'FacilityContactPerson',
                    p.`FirstName`,
                    p.`LastName`,
                    CONCAT(p.`FirstName`, ' ', p.`LastName`),
                    p.`Position`,
                    f.`Name`,
                    p.`Email`,
                    p.`Phone`,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    p.`IsActive`,
                    p.`CreatedByUserId`,
                    p.`UpdatedByUserId`,
                    p.`CreatedAtUtc`,
                    p.`UpdatedAtUtc`
                FROM `liens_FacilityContactPersons` p
                INNER JOIN `liens_Facilities` f ON f.`Id` = p.`FacilityId`
                LEFT JOIN `liens_Contacts` c ON c.`Id` = p.`Id`
                WHERE c.`Id` IS NULL;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO `liens_LookupValues`
                (
                    `Id`,
                    `TenantId`,
                    `Category`,
                    `Code`,
                    `Name`,
                    `Description`,
                    `IsActive`,
                    `SortOrder`,
                    `IsSystem`,
                    `CreatedByUserId`,
                    `UpdatedByUserId`,
                    `CreatedAtUtc`,
                    `UpdatedAtUtc`
                )
                SELECT
                    UUID(),
                    NULL,
                    'ContactType',
                    'Facility',
                    'Facility',
                    'Facility contact',
                    1,
                    9,
                    1,
                    '00000000-0000-0000-0000-000000000000',
                    NULL,
                    UTC_TIMESTAMP(),
                    UTC_TIMESTAMP()
                FROM DUAL
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM `liens_LookupValues`
                    WHERE `Category` = 'ContactType'
                      AND `Code` = 'Facility'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_TenantId_FacilityId_ContactSubtype",
                table: "liens_Contacts");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_TenantId_LawFirmId_ContactSubtype",
                table: "liens_Contacts");

            migrationBuilder.DropColumn(
                name: "ContactSubtype",
                table: "liens_Contacts");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "liens_Contacts");

            migrationBuilder.DropColumn(
                name: "LawFirmId",
                table: "liens_Contacts");
        }
    }
}
