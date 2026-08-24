using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenia.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(Xenia.Infrastructure.Persistence.XeniaDbContext))]
    [Migration("20260714000001_RemoveAssistantApiKeySecretReference")]
    public partial class RemoveAssistantApiKeySecretReference : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM xn_configuration
                WHERE namespace = 'assistant'
                  AND configuration_key = 'openAi.apiKeySecretRef';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
