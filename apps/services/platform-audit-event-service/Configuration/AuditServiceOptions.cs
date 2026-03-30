namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Strongly-typed configuration options for the Platform Audit/Event Service.
/// Bound from the "AuditService" section of appsettings.
/// </summary>
public sealed class AuditServiceOptions
{
    public const string SectionName = "AuditService";

    /// <summary>
    /// Base64-encoded 256-bit HMAC secret for computing tamper-evident integrity hashes.
    /// Must be set to a cryptographically random value in production.
    /// </summary>
    public string? IntegrityHmacKeyBase64 { get; set; }

    /// <summary>
    /// Persistence provider: "InMemory" (default, dev only) | "SqlServer" | "PostgreSQL" | "CosmosDb".
    /// </summary>
    public string PersistenceProvider { get; set; } = "InMemory";

    /// <summary>
    /// Maximum allowed PageSize on query requests.
    /// </summary>
    public int MaxPageSize { get; set; } = 500;
}
