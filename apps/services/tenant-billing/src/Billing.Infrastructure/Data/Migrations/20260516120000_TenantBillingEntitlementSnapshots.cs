using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Data.Migrations
{
    /// <summary>
    /// TB-DATA-02 — local mirror of Commerce-side entitlement decisions per
    /// <see cref="Billing.Domain.Entities.TenantBillingProfile"/>. One
    /// current row per profile (UNIQUE on <c>TenantBillingProfileId</c>);
    /// updated in place.
    ///
    /// <para>
    /// Per WRITE-005 / ERP-001 / ERP-003 / TB-DATA-01 precedent: this
    /// migration .cs file is the authoritative DDL applied at runtime.
    /// The companion Designer.cs is a minimal placeholder; the maintainer
    /// must regenerate the project-level model snapshot via
    /// <c>dotnet ef migrations add</c> in a dotnet-SDK-equipped
    /// environment before the next schema-touching migration.
    /// </para>
    /// </summary>
    public partial class TenantBillingEntitlementSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_billing_entitlement_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantBillingProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BillingAccountId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceSystem = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceSnapshotId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceSubscriptionId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourcePlanKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceProductKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntitlementStatus = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccessRecommendation = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RawSnapshotJson = table.Column<string>(type: "LONGTEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_billing_entitlement_snapshots", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_billing_entitlement_snapshots_TenantId",
                table: "tenant_billing_entitlement_snapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_billing_entitlement_snapshots_BillingAccountId",
                table: "tenant_billing_entitlement_snapshots",
                column: "BillingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_billing_entitlement_snapshots_EntitlementStatus",
                table: "tenant_billing_entitlement_snapshots",
                column: "EntitlementStatus");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_billing_entitlement_snapshots_AccessRecommendation",
                table: "tenant_billing_entitlement_snapshots",
                column: "AccessRecommendation");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_billing_entitlement_snapshots_LastSyncedAtUtc",
                table: "tenant_billing_entitlement_snapshots",
                column: "LastSyncedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_tenant_billing_entitlement_snapshots_ProfileId",
                table: "tenant_billing_entitlement_snapshots",
                column: "TenantBillingProfileId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tenant_billing_entitlement_snapshots");
        }
    }
}
