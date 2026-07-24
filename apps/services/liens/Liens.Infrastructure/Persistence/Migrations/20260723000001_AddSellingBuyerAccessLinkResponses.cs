using System;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LiensDbContext))]
    [Migration("20260723000001_AddSellingBuyerAccessLinkResponses")]
    public partial class AddSellingBuyerAccessLinkResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResponseStatus",
                table: "liens_SellingBuyerAccessLinks",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ResponseAmount",
                table: "liens_SellingBuyerAccessLinks",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseNotes",
                table: "liens_SellingBuyerAccessLinks",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAtUtc",
                table: "liens_SellingBuyerAccessLinks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseIdempotencyKey",
                table: "liens_SellingBuyerAccessLinks",
                type: "varchar(280)",
                maxLength: 280,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponseStatus",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "ResponseAmount",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "ResponseNotes",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "RespondedAtUtc",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "ResponseIdempotencyKey",
                table: "liens_SellingBuyerAccessLinks");
        }
    }
}
