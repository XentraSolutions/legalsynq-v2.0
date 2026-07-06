using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Support.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCaseManagerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "case_manager_email",
                table: "support_tickets",
                type: "varchar(320)",
                maxLength: 320,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "case_manager_name",
                table: "support_tickets",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "case_manager_user_id",
                table: "support_tickets",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_support_tickets_tenant_case_manager",
                table: "support_tickets",
                columns: new[] { "tenant_id", "case_manager_user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_support_tickets_tenant_case_manager",
                table: "support_tickets");

            migrationBuilder.DropColumn(
                name: "case_manager_email",
                table: "support_tickets");

            migrationBuilder.DropColumn(
                name: "case_manager_name",
                table: "support_tickets");

            migrationBuilder.DropColumn(
                name: "case_manager_user_id",
                table: "support_tickets");
        }
    }
}
