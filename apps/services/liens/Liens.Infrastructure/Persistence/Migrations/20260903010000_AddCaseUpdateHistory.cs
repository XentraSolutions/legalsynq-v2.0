using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260903010000_AddCaseUpdateHistory")]
public partial class AddCaseUpdateHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "liens_CaseUpdateHistory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                Action = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                ActorUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_liens_CaseUpdateHistory", item => item.Id));

        migrationBuilder.CreateIndex(
            name: "IX_CaseUpdateHistory_TenantId_CaseId_OccurredAtUtc",
            table: "liens_CaseUpdateHistory",
            columns: new[] { "TenantId", "CaseId", "OccurredAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "Case update history is retained audit evidence and cannot be safely removed by migration rollback.");
    }
}
