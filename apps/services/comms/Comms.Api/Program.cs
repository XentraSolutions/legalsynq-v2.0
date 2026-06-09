using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Contracts;
using Comms.Api.Endpoints;
using Comms.Api.Middleware;
using Comms.Infrastructure;
using Comms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "comms";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidAudience            = jwtSection["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)) { KeyId = ServiceTokenAuthenticationDefaults.UserTokenKeyId },
            RoleClaimType            = "role",
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));
});

builder.Services.AddCommsServices(builder.Configuration);

var app = builder.Build();

var env = app.Environment.EnvironmentName;
app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

// ── Migration coverage self-test ─────────────────────────────────────────
// Compares every EF-mapped column against information_schema. If a model
// property has no backing column on the live database, log an ERROR so the
// regression is loud at boot. Catches the class of bug behind Task #58:
// a migration committed without its [Migration] attribute (or otherwise
// un-applied) leaves the EF model and the live schema out of sync, which
// previously surfaced only as runtime "Unknown column" SQL errors.

// Auto-migrate — apply pending EF Core migrations in all environments.
// __EFMigrationsHistory tracks applied migrations; idempotent across restarts.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CommsDbContext>();
    await SeedCommsMigrationHistoryIfNeededAsync(db, app.Logger);
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Comms database migrations applied successfully.");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not apply Comms database migrations on startup — schema may be out of sync.");
}

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CommsDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<InternalServiceTokenMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)))
    .AllowAnonymous();

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)))
    .AllowAnonymous();

app.MapGet("/context", (ICurrentRequestContext ctx) =>
{
    if (!ctx.IsAuthenticated)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        authenticated = true,
        userId        = ctx.UserId,
        tenantId      = ctx.TenantId,
        tenantCode    = ctx.TenantCode,
        email         = ctx.Email,
        orgId         = ctx.OrgId,
        orgType       = ctx.OrgType,
        orgTypeId     = ctx.OrgTypeId,
        roles         = ctx.Roles,
        productRoles  = ctx.ProductRoles,
        permissions   = ctx.Permissions,
        isPlatformAdmin = ctx.IsPlatformAdmin,
    });
})
.RequireAuthorization(Policies.AuthenticatedUser);

app.MapConversationEndpoints();
app.MapMessageEndpoints();
app.MapParticipantEndpoints();
app.MapAttachmentEndpoints();
app.MapEmailIntakeEndpoints();
app.MapOutboundEmailEndpoints();
app.MapSenderConfigEndpoints();
app.MapEmailTemplateEndpoints();
app.MapQueueEndpoints();
app.MapOperationalEndpoints();
app.MapSlaTriggersEndpoints();
app.MapTimelineEndpoints();

app.Run();

// ── Migration-history seed ────────────────────────────────────────────────────
// The comms_Conversations table (and its siblings) were created before EF
// migration tracking was in place.  On a database that already has the schema
// but has an empty __EFMigrationsHistory, MigrateAsync fails with
// "Table 'comms_Conversations' already exists".  This function seeds the
// history entries for every migration whose DDL has already been applied so
// that MigrateAsync skips them and only runs genuinely pending migrations.
static async Task SeedCommsMigrationHistoryIfNeededAsync(CommsDbContext db, ILogger logger)
{
    var alreadyApplied = new[]
    {
        ("20260416031628_InitialCreateWithBLK002",                 "8.0.2"),
        ("20260416034845_AddMessageAttachments",                   "8.0.2"),
        ("20260416041337_AddEmailIntakeTables",                    "8.0.2"),
        ("20260416043524_AddOutboundEmailDelivery",                "8.0.2"),
        ("20260416050314_AddEmailRecipientRecords",                "8.0.2"),
        ("20260416053646_AddSenderConfigsAndTemplates",            "8.0.2"),
        ("20260416060135_HardenE2ENotificationsIntegration",       "8.0.2"),
        ("20260416132304_AddSlaTriggerStatesEscalationAndTimeline","8.0.2"),
        ("20260416142646_AddMessageMentions",                      "8.0.2"),
        ("20260416152313_AddOperationalViewIndexes",               "8.0.2"),
    };

    // Guard: only seed when the base schema already exists.
    // On a fresh database comms_Conversations does not exist yet — seeding
    // would mark InitialCreate as applied without running it, causing
    // MigrateAsync to skip table creation and then crash on missing tables.
    try
    {
        var conn   = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync();
        try
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText =
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = 'comms_Conversations'";
            var schemaParam = checkCmd.CreateParameter();
            schemaParam.ParameterName = "@schema";
            schemaParam.Value = conn.Database;
            checkCmd.Parameters.Add(schemaParam);
            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
            if (!exists)
            {
                logger.LogInformation("Comms: fresh database detected — skipping migration history seed");
                return;
            }
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Comms: could not check for existing schema — skipping migration history seed");
        return;
    }

    try
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (" +
            "`MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL," +
            "`ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL," +
            "PRIMARY KEY (`MigrationId`)) CHARACTER SET=utf8mb4;");

        foreach (var (id, ver) in alreadyApplied)
        {
            var inserted = await db.Database.ExecuteSqlRawAsync(
                "INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ({0}, {1})",
                id, ver);
            if (inserted > 0)
                logger.LogInformation("Comms: seeded migration history for {MigrationId}", id);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Comms: could not seed migration history — proceeding anyway");
    }
}
