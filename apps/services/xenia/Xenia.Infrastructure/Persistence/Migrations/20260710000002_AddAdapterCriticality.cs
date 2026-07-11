using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the criticality column to xn_platform_adapters.
    ///
    /// Criticality classifies how each adapter's unavailability affects /ready:
    ///   Mandatory — unavailability → 503
    ///   Optional  — unavailability → degraded 200
    ///   Disabled  — excluded from readiness entirely
    ///
    /// Default is 'Optional' so existing rows are not accidentally promoted to mandatory.
    /// Tenant and Identity adapters are updated to Mandatory after migration via seeding.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class AddAdapterCriticality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "criticality",
                table: "xn_platform_adapters",
                nullable: false,
                defaultValue: "Optional");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "criticality",
                table: "xn_platform_adapters");
        }
    }
}
