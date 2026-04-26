using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notifications.Api.Authorization;
using Notifications.Api.Endpoints;
using Notifications.Api.Middleware;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ── JWT Authentication ────────────────────────────────────────────────────────

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

// LS-NOTIF-CORE-021: service token signing key (shared platform secret).
// Preferred from FLOW_SERVICE_TOKEN_SECRET env var, then ServiceTokens:SigningKey config.
var serviceTokenKey =
    Environment.GetEnvironmentVariable(ServiceTokenAuthenticationDefaults.SecretEnvVar)
    ?? builder.Configuration[$"{ServiceTokenOptions.SectionName}:SigningKey"]
    ?? string.Empty;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    // ── Scheme 1: user JWTs from Identity ────────────────────────────────
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
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RoleClaimType            = "role",
            ClockSkew                = TimeSpan.Zero,
        };
    })
    // ── Scheme 2: service-to-service JWTs (LS-NOTIF-CORE-021) ────────────
    // Accepts tokens minted by ServiceTokenIssuer from any producer service.
    // Validates: issuer=legalsynq-service-tokens, audience=notifications-service
    // OR flow-service (for Flow's existing issuer config), subject=service:*
    .AddJwtBearer(ServiceTokenAuthenticationDefaults.Scheme, options =>
    {
        options.MapInboundClaims    = false;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(serviceTokenKey),
            RequireSignedTokens      = true,
            RequireExpirationTime    = true,
            ValidIssuer              = ServiceTokenAuthenticationDefaults.DefaultIssuer,
            // Accept notifications-service (new preferred) + flow-service
            // (Flow's existing issuer defaults) + legalsynq-services (future).
            ValidAudiences           = ["notifications-service", "flow-service", "legalsynq-services"],
            IssuerSigningKey         = string.IsNullOrWhiteSpace(serviceTokenKey)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(serviceTokenKey)),
            NameClaimType            = "sub",
            RoleClaimType            = "role",
            ClockSkew                = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var sub = ctx.Principal?.FindFirst("sub")?.Value;
                if (string.IsNullOrWhiteSpace(sub) ||
                    !sub.StartsWith("service:", StringComparison.Ordinal))
                {
                    ctx.Fail("Service token must have a subject starting with 'service:'.");
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                var log = ctx.HttpContext.RequestServices
                    .GetService<ILoggerFactory>()
                    ?.CreateLogger(ServiceTokenAuthenticationDefaults.Scheme);
                log?.LogWarning(ctx.Exception,
                    "ServiceToken authentication failed. Path={Path}",
                    ctx.HttpContext.Request.Path);
                return Task.CompletedTask;
            },
        };
    });

// ── HTTP context accessor (required by ServiceSubmissionHandler) ──────────────
builder.Services.AddHttpContextAccessor();

// ── Authorization ─────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    // LS-NOTIF-CORE-021 — service submission gate on POST /v1/notifications.
    // Tries both the user JWT scheme and the ServiceToken scheme;
    // the custom handler also allows legacy X-Tenant-Id header requests.
    options.AddPolicy(Policies.ServiceSubmission, policy =>
        policy
            .AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme,
                ServiceTokenAuthenticationDefaults.Scheme)
            .AddRequirements(new ServiceSubmissionRequirement()));
});

// Register the custom authorization handler for ServiceSubmission.
builder.Services.AddSingleton<IAuthorizationHandler, ServiceSubmissionHandler>();

// ── Application services ─────────────────────────────────────────────────────

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── Database startup ──────────────────────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

    try
    {
        await SchemaRenamer.RenameSchemaAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Schema rename step failed — tables/columns may already be renamed");
    }

    try
    {
        // If the schema already existed before EF migrations were introduced the
        // __EFMigrationsHistory table may be empty even though InitialCreate (and
        // AddRetryFields) have already been applied.  MigrateAsync() would then
        // try to re-run InitialCreate, fail with "table already exists", and
        // abort — leaving AddCategoryAndSeverity (and any future migrations) never
        // applied.  We detect this condition and seed the history so that
        // MigrateAsync() only executes the genuinely pending migrations.
        await SeedMigrationHistoryIfNeededAsync(db, app.Logger);
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Notifications database migrated successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not apply Notifications database migrations on startup — schema may be out of sync.");
    }

    // Safety net: ensure columns added by AddCategoryAndSeverity actually exist
    // in the database even if EF's history already records the migration as applied
    // (which can happen when the migration was aborted mid-run but still committed
    // to __EFMigrationsHistory).
    try
    {
        await EnsureNotificationsSchemaColumnsAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not ensure notification schema columns — queries may fail");
    }

    try
    {
        await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
    }
}

// ── Platform provider seeding ─────────────────────────────────────────────────
// On every startup, ensure the platform-level SendGrid provider config exists.
// This is stored with the sentinel TenantId 00000000-0000-0000-0000-000000000001
// so the control center can list/use it without a real tenant context.
try
{
    using var seedScope = app.Services.CreateScope();
    await SeedPlatformSendGridProviderAsync(
        seedScope.ServiceProvider,
        app.Configuration,
        app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Platform SendGrid provider seeding failed — providers page may show empty");
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
// Order matters: Authentication → Authorization → custom middleware → endpoints.
// TenantMiddleware is placed AFTER UseAuthentication so it can read context.User
// to extract tenant_id from JWT claims for authenticated requests.

app.UseMiddleware<RawBodyMiddleware>();
app.UseMiddleware<InternalTokenMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapHealthEndpoints();
app.MapNotificationEndpoints();
app.MapAdminNotificationEndpoints();
app.MapTemplateEndpoints();
app.MapGlobalTemplateEndpoints();
app.MapProviderEndpoints();
app.MapWebhookEndpoints();
app.MapBillingEndpoints();
app.MapContactEndpoints();
app.MapBrandingEndpoints();
app.MapInternalEndpoints();

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────

static async Task EnsureNotificationsSchemaColumnsAsync(NotificationsDbContext db, ILogger logger)
{
    // Use raw ADO.NET so we stay in control of the SQL and avoid EF query-wrapping quirks.
    var conn = db.Database.GetDbConnection();
    var dbName = conn.Database;
    var opened = false;

    try
    {
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
            opened = true;
        }

        // All columns that may be missing: from AddRetryFields and AddCategoryAndSeverity.
        var columnsToAdd = new[]
        {
            ("RetryCount",  "int NOT NULL DEFAULT 0"),
            ("MaxRetries",  "int NOT NULL DEFAULT 3"),
            ("NextRetryAt", "datetime(6) NULL"),
            ("Category",    "varchar(100) CHARACTER SET utf8mb4 NULL"),
            ("Severity",    "varchar(50)  CHARACTER SET utf8mb4 NULL"),
        };

        foreach (var (col, colDef) in columnsToAdd)
        {
            // Check column existence via INFORMATION_SCHEMA.
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText =
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_Notifications' AND COLUMN_NAME = '{col}'";
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (count == 0)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE `ntf_Notifications` ADD COLUMN `{col}` {colDef}";
                await alterCmd.ExecuteNonQueryAsync();
                logger.LogInformation("Added missing column ntf_Notifications.{Column}", col);
            }
            else
            {
                logger.LogDebug("Column ntf_Notifications.{Column} already exists", col);
            }
        }

        // Also ensure the retry index exists (idempotent via INFORMATION_SCHEMA check).
        using var idxCheckCmd = conn.CreateCommand();
        idxCheckCmd.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS " +
            $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_Notifications' " +
            $"AND INDEX_NAME = 'IX_Notifications_Status_NextRetryAt'";
        var idxCount = Convert.ToInt32(await idxCheckCmd.ExecuteScalarAsync());
        if (idxCount == 0)
        {
            using var idxCmd = conn.CreateCommand();
            idxCmd.CommandText =
                "CREATE INDEX `IX_Notifications_Status_NextRetryAt` ON `ntf_Notifications` (`Status`, `NextRetryAt`)";
            await idxCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Created missing index IX_Notifications_Status_NextRetryAt");
        }

        // Ensure all columns exist on ntf_TenantProviderConfigs — some may be missing on DBs
        // where the migration was pre-seeded as already-applied without actually running DDL.
        // Note: TEXT columns cannot have DEFAULT values on all MySQL versions, so use NULL for those.
        var providerColumnsToAdd = new[]
        {
            ("ntf_TenantProviderConfigs", "CredentialsJson",     "longtext CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "SettingsJson",        "longtext CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "ValidationStatus",    "varchar(30) CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "ValidationMessage",   "text CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "LastValidatedAt",     "datetime(6) NULL"),
            ("ntf_TenantProviderConfigs", "HealthStatus",        "varchar(20) CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "LastHealthCheckAt",   "datetime(6) NULL"),
            ("ntf_TenantProviderConfigs", "HealthCheckLatencyMs","int NULL"),
            ("ntf_TenantProviderConfigs", "OwnershipMode",       "varchar(20) CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "Priority",            "int NULL"),
        };

        foreach (var (table, col, colDef) in providerColumnsToAdd)
        {
            using var checkCmd2 = conn.CreateCommand();
            checkCmd2.CommandText =
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{col}'";
            var count2 = Convert.ToInt32(await checkCmd2.ExecuteScalarAsync());

            if (count2 == 0)
            {
                using var alterCmd2 = conn.CreateCommand();
                alterCmd2.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{col}` {colDef}";
                await alterCmd2.ExecuteNonQueryAsync();
                logger.LogInformation("Added missing column {Table}.{Column}", table, col);
            }
        }

        logger.LogInformation("EnsureNotificationsSchemaColumns complete");
    }
    finally
    {
        if (opened) await conn.CloseAsync();
    }
}

static async Task SeedMigrationHistoryIfNeededAsync(NotificationsDbContext db, ILogger logger)
{
    // These are the migrations whose DDL was applied to the DB before EF
    // migrations were tracking history.  If the history table exists but does
    // not contain them we insert them so MigrateAsync skips re-running them.
    var alreadyApplied = new[]
    {
        ("20260418043535_InitialCreate",   "8.0.2"),
        ("20260419000001_AddRetryFields",  "8.0.2"),
    };

    try
    {
        // Ensure the history table exists (idempotent).
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
                logger.LogInformation("Seeded migration history for {MigrationId}", id);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not seed migration history — proceeding anyway");
    }
}

// ── Platform SendGrid provider seeder ─────────────────────────────────────────
// Ensures a single platform-level SendGrid config exists so the control-center
// "Test Outbound Message" page and the providers list work for platform admins
// without any manual setup step.
static async Task SeedPlatformSendGridProviderAsync(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger logger)
{
    var sgApiKey = configuration["SENDGRID_API_KEY"];
    if (string.IsNullOrWhiteSpace(sgApiKey))
    {
        logger.LogInformation("SENDGRID_API_KEY not set — skipping platform provider seed");
        return;
    }

    var repo = services.GetRequiredService<Notifications.Application.Interfaces.ITenantProviderConfigRepository>();

    var platformId   = Notifications.Application.Constants.PlatformProvider.PlatformTenantId;
    var existing     = await repo.GetByTenantAndChannelAsync(platformId, "email");
    var alreadyHasSg = existing.Any(c => c.ProviderType.Equals("sendgrid", StringComparison.OrdinalIgnoreCase));

    if (alreadyHasSg)
    {
        logger.LogInformation("Platform SendGrid provider config already exists — skipping seed");
        return;
    }

    var fromEmail = configuration["SENDGRID_FROM_EMAIL"] ?? "noreply@legalsynq.com";
    var fromName  = configuration["SENDGRID_FROM_NAME"]  ?? "LegalSynq";

    var config = new Notifications.Domain.TenantProviderConfig
    {
        Id              = Guid.NewGuid(),
        TenantId        = platformId,
        Channel         = "email",
        ProviderType    = "sendgrid",
        DisplayName     = "SendGrid (Platform Default)",
        CredentialsJson = JsonSerializer.Serialize(new { apiKey = sgApiKey }),
        SettingsJson    = JsonSerializer.Serialize(new { fromEmail, fromName }),
        Status          = "active",
        ValidationStatus = "valid",
        HealthStatus    = "unknown",
        Priority        = 1,
    };

    await repo.CreateAsync(config);
    logger.LogInformation("Platform SendGrid provider config seeded with id={Id}", config.Id);
}
