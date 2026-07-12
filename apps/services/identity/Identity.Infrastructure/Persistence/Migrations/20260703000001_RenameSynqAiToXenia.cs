using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260703000001_RenameSynqAiToXenia")]
public partial class RenameSynqAiToXenia : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE `idt_AccessGroups`
            SET `ProductCode` = 'XENIA'
            WHERE `ProductCode` IN ('SYNQ_AI', 'SYNQAI');

            UPDATE `idt_GroupProductAccess`
            SET `ProductCode` = 'XENIA'
            WHERE `ProductCode` IN ('SYNQ_AI', 'SYNQAI');

            UPDATE `idt_GroupRoleAssignments`
            SET `ProductCode` = 'XENIA'
            WHERE `ProductCode` IN ('SYNQ_AI', 'SYNQAI');

            UPDATE `idt_Policies`
            SET `ProductCode` = 'XENIA'
            WHERE `ProductCode` IN ('SYNQ_AI', 'SYNQAI');

            UPDATE `idt_TenantProductEntitlements`
            SET `ProductCode` = 'XENIA'
            WHERE `ProductCode` IN ('SYNQ_AI', 'SYNQAI');

            UPDATE `idt_UserProductAccess`
            SET `ProductCode` = 'XENIA'
            WHERE `ProductCode` IN ('SYNQ_AI', 'SYNQAI');

            UPDATE `idt_UserRoleAssignments`
            SET `ProductCode` = 'XENIA'
            WHERE `ProductCode` IN ('SYNQ_AI', 'SYNQAI');

            UPDATE `idt_Products`
            SET `Code` = 'XENIA',
                `Name` = 'Xenia',
                `Description` = 'Enterprise intelligence platform for LegalSynq products.'
            WHERE `Id` = '10000000-0000-0000-0000-000000000005';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE `idt_AccessGroups`
            SET `ProductCode` = 'SYNQ_AI'
            WHERE `ProductCode` = 'XENIA';

            UPDATE `idt_GroupProductAccess`
            SET `ProductCode` = 'SYNQ_AI'
            WHERE `ProductCode` = 'XENIA';

            UPDATE `idt_GroupRoleAssignments`
            SET `ProductCode` = 'SYNQ_AI'
            WHERE `ProductCode` = 'XENIA';

            UPDATE `idt_Policies`
            SET `ProductCode` = 'SYNQ_AI'
            WHERE `ProductCode` = 'XENIA';

            UPDATE `idt_TenantProductEntitlements`
            SET `ProductCode` = 'SYNQ_AI'
            WHERE `ProductCode` = 'XENIA';

            UPDATE `idt_UserProductAccess`
            SET `ProductCode` = 'SYNQ_AI'
            WHERE `ProductCode` = 'XENIA';

            UPDATE `idt_UserRoleAssignments`
            SET `ProductCode` = 'SYNQ_AI'
            WHERE `ProductCode` = 'XENIA';

            UPDATE `idt_Products`
            SET `Code` = 'SYNQ_AI',
                `Name` = 'SynqAI',
                `Description` = 'AI-powered legal intelligence platform'
            WHERE `Id` = '10000000-0000-0000-0000-000000000005';
            """);
    }
}
