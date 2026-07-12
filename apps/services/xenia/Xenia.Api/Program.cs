using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.Json.Serialization;
using System.Data.Common;
using Xenia.Api.Authentication;
using Xenia.Api.Endpoints;
using Xenia.Application;
using Xenia.Infrastructure;
using Xenia.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddXeniaApplication();
builder.Services.AddXeniaInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddXeniaAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<XeniaDbContext>();
    var migrationsAssembly = db.GetService<IMigrationsAssembly>();
    if (migrationsAssembly.Migrations.Count == 0)
    {
        throw new InvalidOperationException(
            "Xenia EF migrations were not discovered. Ensure migration classes are compiled into Xenia.Infrastructure before starting Xenia.Api.");
    }

    var initialMigrationId = migrationsAssembly.Migrations.Keys.OrderBy(static key => key).First();
    if (app.Environment.IsDevelopment())
    {
        await RecoverIncompleteDevelopmentSchemaAsync(db, app.Logger, initialMigrationId);
    }

    try
    {
        await db.Database.MigrateAsync();
        await XeniaSeedData.InitializeAsync(db);
    }
    catch (Exception ex) when (app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning(ex, "Xenia database bootstrap failed in Development. Continuing with in-memory fallback services.");
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapOperationalEndpoints();
app.MapAdminEndpoints();
app.MapTenantEndpoints();
app.MapConversationEndpoints();
app.MapInternalExecutionEndpoints();

app.Run();

static async Task RecoverIncompleteDevelopmentSchemaAsync(
    XeniaDbContext db,
    ILogger logger,
    string initialMigrationId)
{
    await db.Database.OpenConnectionAsync();
    try
    {
        var connection = db.Database.GetDbConnection();

        if (!await TableExistsAsync(connection, "__EFMigrationsHistory"))
        {
            await DropPartialXeniaTablesAsync(connection, logger);
            return;
        }

        if (await MigrationExistsAsync(connection, initialMigrationId))
            return;

        await DropPartialXeniaTablesAsync(connection, logger);
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }
}

static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
{
    await using var command = connection.CreateCommand();
    command.CommandText =
        "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName LIMIT 1;";

    var parameter = command.CreateParameter();
    parameter.ParameterName = "@tableName";
    parameter.Value = tableName;
    command.Parameters.Add(parameter);

    return await command.ExecuteScalarAsync() is not null;
}

static async Task<bool> MigrationExistsAsync(DbConnection connection, string migrationId)
{
    await using var command = connection.CreateCommand();
    command.CommandText =
        "SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = @migrationId LIMIT 1;";

    var parameter = command.CreateParameter();
    parameter.ParameterName = "@migrationId";
    parameter.Value = migrationId;
    command.Parameters.Add(parameter);

    return await command.ExecuteScalarAsync() is not null;
}

static async Task DropPartialXeniaTablesAsync(DbConnection connection, ILogger logger)
{
    var tableNames = new List<string>();

    await using (var listCommand = connection.CreateCommand())
    {
        listCommand.CommandText =
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME LIKE 'xen\\_%' ESCAPE '\\\\';";

        await using var reader = await listCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }
    }

    if (tableNames.Count == 0)
        return;

    logger.LogWarning(
        "Detected partial Xenia schema in Development with no applied initial migration. Dropping {TableCount} leftover xen_* tables before rerunning migrations.",
        tableNames.Count);

    await using var disableCommand = connection.CreateCommand();
    disableCommand.CommandText = "SET FOREIGN_KEY_CHECKS = 0;";
    await disableCommand.ExecuteNonQueryAsync();

    try
    {
        foreach (var tableName in tableNames)
        {
            await using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = $"DROP TABLE IF EXISTS `{tableName.Replace("`", "``")}`;";
            await dropCommand.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        await using var enableCommand = connection.CreateCommand();
        enableCommand.CommandText = "SET FOREIGN_KEY_CHECKS = 1;";
        await enableCommand.ExecuteNonQueryAsync();
    }
}
