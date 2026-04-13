using Microsoft.EntityFrameworkCore.Migrations;

  #nullable disable

  namespace Fund.Infrastructure.Data;

  public partial class AddTablePrefixes : Migration
  {
      protected override void Up(MigrationBuilder migrationBuilder)
      {
              migrationBuilder.RenameTable(name: "Applications", newName: "fund_Applications");
      }

      protected override void Down(MigrationBuilder migrationBuilder)
      {
              migrationBuilder.RenameTable(name: "fund_Applications", newName: "Applications");
      }
  }
  