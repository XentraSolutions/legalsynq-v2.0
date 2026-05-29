using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
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

            // ── 2. Backfill one row per existing user from User.TenantId ─────────────
            migrationBuilder.Sql("""
                INSERT INTO idt_UserTenants (Id, UserId, TenantId, IsActive, JoinedAtUtc)
                SELECT
                    UUID(),
                    Id,
                    TenantId,
                    1,
                    CreatedAtUtc
                FROM idt_Users
                ON DUPLICATE KEY UPDATE IsActive = IsActive;
                """);

            // ── 3. Drop old (TenantId, Email) composite unique index ─────────────────
            migrationBuilder.DropIndex(
                name: "IX_idt_Users_TenantId_Email",
                table: "idt_Users");

            // ── 4. Add global Email unique index ─────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_idt_Users_Email",
                table: "idt_Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the old (TenantId, Email) unique index
            migrationBuilder.DropIndex(
                name: "IX_idt_Users_Email",
                table: "idt_Users");

            migrationBuilder.CreateIndex(
                name: "IX_idt_Users_TenantId_Email",
                table: "idt_Users",
                columns: ["TenantId", "Email"],
                unique: true);

            migrationBuilder.DropTable(name: "idt_UserTenants");
        }
    }
}
