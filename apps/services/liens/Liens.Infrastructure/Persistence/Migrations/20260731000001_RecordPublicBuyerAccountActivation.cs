using System;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LiensDbContext))]
    [Migration("20260731000001_RecordPublicBuyerAccountActivation")]
    public partial class RecordPublicBuyerAccountActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccountActivatedAtUtc",
                table: "liens_SellingBuyerAccessLinks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountActivatedEmail",
                table: "liens_SellingBuyerAccessLinks",
                type: "varchar(320)",
                maxLength: 320,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountActivatedUserId",
                table: "liens_SellingBuyerAccessLinks",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountActivatedAtUtc",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "AccountActivatedEmail",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "AccountActivatedUserId",
                table: "liens_SellingBuyerAccessLinks");
        }
    }
}
