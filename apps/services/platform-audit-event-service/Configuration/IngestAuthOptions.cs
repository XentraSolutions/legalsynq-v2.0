namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Authentication/authorization options for the event ingestion endpoints
/// (POST /api/auditevents and POST /api/auditevents/batch).
/// Bound from "IngestAuth" section in appsettings.
/// Environment variable override prefix: IngestAuth__
/// </summary>
public sealed class IngestAuthOptions
{
    public const string SectionName = "IngestAuth";

    /// <summary>
    /// Auth mode for ingestion endpoints.
    /// Allowed values:
    ///   "None"   — no auth required (dev/internal only — never use publicly)
    ///   "ApiKey" — require X-Api-Key header matching ApiKey value
    ///   "Bearer" — require valid JWT bearer token
    /// Environment variable: IngestAuth__Mode
    /// </summary>
    public string Mode { get; set; } = "None";

    /// <summary>
    /// Shared API key for ingest auth when Mode = "ApiKey".
    /// MUST be injected via environment variable — never hardcode.
    /// Environment variable: IngestAuth__ApiKey
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Header name to read the API key from.
    /// Default: X-Api-Key
    /// </summary>
    public string ApiKeyHeader { get; set; } = "X-Api-Key";

    /// <summary>
    /// Required JWT claim names when Mode = "Bearer".
    /// All listed claims must be present in the token.
    /// </summary>
    public List<string> RequiredClaims { get; set; } = [];

    /// <summary>
    /// Required JWT role when Mode = "Bearer".
    /// E.g. "platform-audit-ingest"
    /// </summary>
    public string? RequiredRole { get; set; }

    /// <summary>
    /// Allowed source system identifiers. When non-empty, ingested events
    /// whose Source is not in this list are rejected with 403.
    /// Empty = allow any source.
    /// </summary>
    public List<string> AllowedSources { get; set; } = [];
}
