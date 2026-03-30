namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Database connectivity and behavior options.
/// Bound from "Database" section in appsettings.
/// Environment variable override prefix: Database__
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Persistence provider.
    /// Allowed values: "InMemory" (dev/test only) | "MySQL"
    /// Environment variable: Database__Provider
    /// </summary>
    public string Provider { get; set; } = "InMemory";

    /// <summary>
    /// MySQL connection string.
    /// Recommended: inject via environment variable Database__ConnectionString or
    /// ConnectionStrings__AuditEventDb (standard ASP.NET Core convention).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// MySQL server version string for Pomelo (e.g. "8.0.0-mysql").
    /// Used only when Provider = "MySQL".
    /// </summary>
    public string ServerVersion { get; set; } = "8.0.0-mysql";

    /// <summary>
    /// Maximum number of connections in the connection pool.
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Minimum number of connections in the connection pool.
    /// </summary>
    public int MinPoolSize { get; set; } = 5;

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Command (query) timeout in seconds.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// When true, EF Core will run pending migrations on startup.
    /// Only applies when Provider = "MySQL".
    /// Keep false in production unless you own the migration window.
    /// </summary>
    public bool MigrateOnStartup { get; set; } = false;

    /// <summary>
    /// When true, a safe DB connectivity check runs at startup.
    /// Failure is logged as a warning but does NOT abort startup,
    /// allowing the service to remain up for health-check reporting.
    /// </summary>
    public bool VerifyConnectionOnStartup { get; set; } = true;

    /// <summary>
    /// Timeout in seconds for the startup connectivity probe.
    /// </summary>
    public int StartupProbeTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// When true, enables EF Core sensitive data logging (logs parameter values).
    /// NEVER enable in production — exposes PII.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;

    /// <summary>
    /// When true, enables EF Core detailed errors (full SQL in exceptions).
    /// Safe to enable in Development; disable in production.
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;
}
