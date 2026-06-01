using System;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20260601000001_AddTenantOwnerUserId")]
    public partial class AddTenantOwnerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Write-through mirror of Tenant service OwnerUserId.
            // No FK constraint — users and tenants are co-located in this DB but the
            // column is authoritative in the Tenant service; Identity stores a copy
            // so enrollment endpoints can check owner status without a cross-service call.
            migrationBuilder.AddColumn<Guid>(
                name:     "OwnerUserId",
                table:    "idt_Tenants",
                type:     "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name:   "IX_idt_Tenants_OwnerUserId",
                table:  "idt_Tenants",
                column: "OwnerUserId",
                filter: "`OwnerUserId` IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name:  "IX_idt_Tenants_OwnerUserId",
                table: "idt_Tenants");

            migrationBuilder.DropColumn(
                name:  "OwnerUserId",
                table: "idt_Tenants");
        }
    }
}
