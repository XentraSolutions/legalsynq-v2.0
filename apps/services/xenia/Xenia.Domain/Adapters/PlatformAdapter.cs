using Xenia.Domain.Common;

namespace Xenia.Domain.Adapters;

/// <summary>
/// Represents the registry record of a platform adapter within Xenia.
/// This entity tracks health and availability status — it does not store credentials.
///
/// Adapter implementations are registered in the DI container; this record
/// mirrors their status for observability via the /adapters endpoint.
/// </summary>
public sealed class PlatformAdapter : AuditableEntityBase
{
    public const int KeyMaxLength = 100;
    public const int NameMaxLength = 200;
    public const int VersionMaxLength = 50;
    public const int DiagnosticMaxLength = 500;

    public Guid Id { get; private set; }

    /// <summary>Unique stable key, e.g. <c>tenant</c>, <c>identity</c>.</summary>
    public string AdapterKey { get; private set; } = string.Empty;

    public AdapterType AdapterType { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;

    public AdapterStatus ConfigurationStatus { get; private set; }
    public AdapterStatus AvailabilityStatus { get; private set; }
    public AdapterStatus HealthStatus { get; private set; }

    public DateTime? LastHealthCheckAt { get; private set; }

    /// <summary>
    /// Optional safe diagnostic message surfaced to administrators.
    /// Must not contain credentials, tokens, or secrets.
    /// </summary>
    public string? DiagnosticMessage { get; private set; }

    /// <summary>EF Core constructor. Do not use from application code.</summary>
    private PlatformAdapter() { }

    public PlatformAdapter(
        Guid id,
        string adapterKey,
        AdapterType adapterType,
        string name,
        string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        AdapterKey = adapterKey.Trim();
        AdapterType = adapterType;
        Name = name.Trim();
        Version = version?.Trim() ?? "0.0.0";
        ConfigurationStatus = AdapterStatus.Unconfigured;
        AvailabilityStatus = AdapterStatus.Unknown;
        HealthStatus = AdapterStatus.Unknown;
    }

    public void RecordHealthCheck(
        AdapterStatus health,
        AdapterStatus availability,
        string? diagnosticMessage = null)
    {
        HealthStatus = health;
        AvailabilityStatus = availability;
        LastHealthCheckAt = DateTime.UtcNow;
        DiagnosticMessage = diagnosticMessage is { Length: > DiagnosticMaxLength }
            ? diagnosticMessage[..DiagnosticMaxLength]
            : diagnosticMessage;
    }

    public void SetConfigured() => ConfigurationStatus = AdapterStatus.Healthy;
    public void SetUnconfigured() => ConfigurationStatus = AdapterStatus.Unconfigured;
}
