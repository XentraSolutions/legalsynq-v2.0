using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedApprovedSnapshotSchemaV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ApprovedSnapshotSchemaDefinitions",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "DisplayName", "IsActive", "IsSystemDefined", "UpdatedAt", "Version" },
                values: new object[] { new Guid("019b0b22-7f4d-7e25-9c6c-3b2f6f5a4d11"), "LIEN_INTAKE_APPROVED_SNAPSHOT_V1", new DateTimeOffset(new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Product-neutral approved projection contract for downstream adapters.", "Lien Intake Approved Snapshot V1", true, true, new DateTimeOffset(new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ApprovedSnapshotSchemaDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("019b0b22-7f4d-7e25-9c6c-3b2f6f5a4d11"));
        }
    }
}
