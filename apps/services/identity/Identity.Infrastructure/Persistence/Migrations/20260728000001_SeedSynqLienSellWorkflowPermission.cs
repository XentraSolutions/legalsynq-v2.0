using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds the explicit Flow capability for SynqLien sellers. The pre-existing
/// sale-portfolio capabilities are already seeded by 20260627000002; this
/// migration intentionally adds only the missing <c>lien:sell</c> capability.
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260728000001_SeedSynqLienSellWorkflowPermission")]
public partial class SeedSynqLienSellWorkflowPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A fixed, non-sequential identifier lets Down remove only rows created
        // by this migration. Do not reuse the historic 0063-0065 range: raw
        // migrations already collide there.
        migrationBuilder.Sql("""
            INSERT INTO `idt_Capabilities`
                (`Id`, `ProductId`, `Code`, `Name`, `Description`, `Category`, `IsActive`,
                 `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`)
            SELECT 'c8f18b6e-21d1-4fd8-931c-417006f1aed0', p.`Id`, 'SYNQ_LIENS.lien:sell', 'Start Lien Selling Workflow',
                   'Start Flow workflows for lien selling', 'Workflow', 1,
                   UTC_TIMESTAMP(6), NULL, NULL, NULL
            FROM `idt_Products` p
            WHERE p.`Code` = 'SYNQ_LIENS'
              AND NOT EXISTS (
                  SELECT 1
                  FROM `idt_Capabilities` c
                  WHERE c.`Code` = 'SYNQ_LIENS.lien:sell'
              );
            """);

        migrationBuilder.Sql("""
            INSERT INTO `idt_RoleCapabilities` (`ProductRoleId`, `CapabilityId`)
            SELECT pr.`Id`, c.`Id`
            FROM `idt_ProductRoles` pr
            INNER JOIN `idt_Products` p
                ON p.`Id` = pr.`ProductId`
            INNER JOIN `idt_Capabilities` c
                ON c.`ProductId` = p.`Id`
               AND c.`Id` = 'c8f18b6e-21d1-4fd8-931c-417006f1aed0'
            WHERE p.`Code` = 'SYNQ_LIENS'
              AND pr.`Code` = 'SYNQLIEN_SELLER'
              AND NOT EXISTS (
                  SELECT 1
                  FROM `idt_RoleCapabilities` rc
                  WHERE rc.`ProductRoleId` = pr.`Id`
                    AND rc.`CapabilityId` = c.`Id`
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE rc
            FROM `idt_RoleCapabilities` rc
            WHERE rc.`CapabilityId` = 'c8f18b6e-21d1-4fd8-931c-417006f1aed0';
            """);

        migrationBuilder.Sql("""
            DELETE FROM `idt_Capabilities`
            WHERE `Id` = 'c8f18b6e-21d1-4fd8-931c-417006f1aed0';
            """);
    }
}
