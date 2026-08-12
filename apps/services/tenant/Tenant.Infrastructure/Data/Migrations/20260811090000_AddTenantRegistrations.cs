using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Tenant.Infrastructure.Data.Migrations;

[DbContext(typeof(TenantDbContext))]
[Migration("20260811090000_AddTenantRegistrations")]
public partial class AddTenantRegistrations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_Registrations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                TenantCode = table.Column<string>(type: "varchar(63)", maxLength: 63, nullable: false),
                OrganizationType = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                StreetAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                AdminFirstName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                AdminLastName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                AdminEmail = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                RegistrationStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                ProvisioningStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: true),
                ProvisioningHostname = table.Column<string>(type: "varchar(253)", maxLength: 253, nullable: true),
                ProvisioningError = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                ProvisioningFailureStage = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                DecisionReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                ReviewedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                ReviewedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ProvisioningStartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ProvisionedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Version = table.Column<uint>(type: "int unsigned", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_tenant_Registrations", x => x.Id));
        migrationBuilder.CreateIndex("IX_tenant_Registrations_AdminEmail", "tenant_Registrations", "AdminEmail");
        migrationBuilder.CreateIndex("IX_tenant_Registrations_CreatedAtUtc", "tenant_Registrations", "CreatedAtUtc");
        migrationBuilder.CreateIndex("IX_tenant_Registrations_ProvisioningStatus", "tenant_Registrations", "ProvisioningStatus");
        migrationBuilder.CreateIndex("IX_tenant_Registrations_RegistrationStatus", "tenant_Registrations", "RegistrationStatus");
        migrationBuilder.CreateIndex("IX_tenant_Registrations_TenantCode", "tenant_Registrations", "TenantCode");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("tenant_Registrations");
}
