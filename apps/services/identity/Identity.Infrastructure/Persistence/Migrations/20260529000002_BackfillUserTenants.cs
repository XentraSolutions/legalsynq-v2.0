using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Backfills one idt_UserTenants row per existing user using their current
    /// TenantId from idt_Users. Separated from the schema migration
    /// (20260529000001_AddUserTenantsMultiTenant) so that DDL and DML are applied
    /// independently and can be re-run or rolled back in isolation.
    /// </summary>
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20260529000002_BackfillUserTenants")]
    public partial class BackfillUserTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO idt_UserTenants (Id, UserId, TenantId, IsActive, JoinedAtUtc)
                SELECT
                    UUID(),
                    u.Id,
                    u.TenantId,
                    1,
                    u.CreatedAtUtc
                FROM idt_Users AS u
                ON DUPLICATE KEY UPDATE idt_UserTenants.IsActive = idt_UserTenants.IsActive;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the backfilled rows. Rows created after this migration
            // (e.g. by UserMembershipService) are left untouched.
            migrationBuilder.Sql("""
                DELETE ut
                FROM idt_UserTenants AS ut
                INNER JOIN idt_Users AS u
                    ON ut.UserId = u.Id AND ut.TenantId = u.TenantId;
                """);
        }
    }
}
