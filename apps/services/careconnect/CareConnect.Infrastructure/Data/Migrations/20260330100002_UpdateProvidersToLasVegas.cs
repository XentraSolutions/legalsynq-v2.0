using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProvidersToLasVegas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: the provider rows this migration updated have been removed.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: this migration no longer mutates provider seed data.
        }
    }
}
