using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedCareConnectTestData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: this provider seed data has been removed.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: this migration no longer inserts provider seed data.
        }
    }
}
