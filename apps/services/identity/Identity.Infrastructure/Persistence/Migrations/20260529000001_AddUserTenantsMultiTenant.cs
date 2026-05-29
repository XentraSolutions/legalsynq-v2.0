using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// DDL-only schema migration for multi-tenant account linking.
    ///
    /// 1. Creates idt_UserTenants (join table: one row per user-tenant link).
    /// 2. Adds a unique index on (UserId, TenantId).
    /// 3. Drops the old per-tenant (TenantId, Email) unique index on idt_Users.
    /// 4. Adds a global Email unique index on idt_Users.
    ///
    /// Data backfill (seeding existing users into idt_UserTenants) is handled
    /// separately by 20260529000002_BackfillUserTenants so DDL and DML can be
    /// applied and rolled back independently.
    /// </summary>
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20260529000001_AddUserTenantsMultiTenant")]
    public partial class AddUserTenantsMultiTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Create idt_UserTenants ────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "idt_UserTenants",
                columns: table => new
                {
                    Id          = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId      = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId    = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IsActive    = table.Column<bool>(nullable: false, defaultValue: true),
                    JoinedAtUtc = table.Column<DateTime>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idt_UserTenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_idt_UserTenants_idt_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "idt_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_idt_UserTenants_idt_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "idt_Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_idt_UserTenants_UserId_TenantId",
                table: "idt_UserTenants",
                columns: ["UserId", "TenantId"],
                unique: true);

            // ── 2. Drop old (TenantId, Email) composite unique index on idt_Users ──────
            // Conditional drop — the index may already be absent on databases that were
            // provisioned without it, so we guard with an information_schema check to
            // keep the migration idempotent.
            migrationBuilder.Sql("""
                SET @dbName = DATABASE();
                SET @exists = (
                    SELECT COUNT(*) FROM information_schema.statistics
                    WHERE table_schema = @dbName
                      AND table_name   = 'idt_Users'
                      AND index_name   = 'IX_idt_Users_TenantId_Email'
                );
                SET @sql = IF(@exists > 0,
                    'ALTER TABLE `idt_Users` DROP INDEX `IX_idt_Users_TenantId_Email`',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            // ── 3. Add global Email unique index on idt_Users (idempotent) ─────────────
            migrationBuilder.Sql("""
                SET @dbName = DATABASE();
                SET @exists = (
                    SELECT COUNT(*) FROM information_schema.statistics
                    WHERE table_schema = @dbName
                      AND table_name   = 'idt_Users'
                      AND index_name   = 'IX_idt_Users_Email'
                );
                SET @sql = IF(@exists = 0,
                    'CREATE UNIQUE INDEX `IX_idt_Users_Email` ON `idt_Users` (`Email`)',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOTE: Run 20260529000002_BackfillUserTenants Down first to remove seeded rows
            // before this migration drops the table.

            // ── 3. Restore global Email unique index → old (TenantId, Email) index ────
            migrationBuilder.Sql("""
                SET @dbName = DATABASE();
                SET @exists = (
                    SELECT COUNT(*) FROM information_schema.statistics
                    WHERE table_schema = @dbName
                      AND table_name   = 'idt_Users'
                      AND index_name   = 'IX_idt_Users_Email'
                );
                SET @sql = IF(@exists > 0,
                    'ALTER TABLE `idt_Users` DROP INDEX `IX_idt_Users_Email`',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @dbName = DATABASE();
                SET @exists = (
                    SELECT COUNT(*) FROM information_schema.statistics
                    WHERE table_schema = @dbName
                      AND table_name   = 'idt_Users'
                      AND index_name   = 'IX_idt_Users_TenantId_Email'
                );
                SET @sql = IF(@exists = 0,
                    'CREATE UNIQUE INDEX `IX_idt_Users_TenantId_Email` ON `idt_Users` (`TenantId`, `Email`)',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.DropTable(name: "idt_UserTenants");
        }
    }
}
