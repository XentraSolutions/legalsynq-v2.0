using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260601000001_AddTenantOwnerUserId")]
    public partial class AddTenantOwnerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cross-service reference to the Identity userId that owns (provisioned) this tenant.
            // No FK constraint — Identity users live in a different database.
            // The index supports "find all tenants owned by user X" queries efficiently.
            migrationBuilder.AddColumn<Guid>(
                name:     "OwnerUserId",
                table:    "tenant_Tenants",
                type:     "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name:    "IX_tenant_Tenants_OwnerUserId",
                table:   "tenant_Tenants",
                column:  "OwnerUserId",
                filter:  "`OwnerUserId` IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name:  "IX_tenant_Tenants_OwnerUserId",
                table: "tenant_Tenants");

            migrationBuilder.DropColumn(
                name:  "OwnerUserId",
                table: "tenant_Tenants");
        }
    }
}
