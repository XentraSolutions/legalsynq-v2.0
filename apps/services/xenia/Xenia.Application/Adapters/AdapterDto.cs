using Xenia.Domain.Adapters;

namespace Xenia.Application.Adapters;

/// <summary>
/// Read model for a platform adapter. Safe to return from APIs.
/// Does not contain credentials, secrets, or internal connection details.
/// </summary>
public sealed record AdapterDto
{
    public required Guid Id { get; init; }
    public required string AdapterKey { get; init; }
    public required string AdapterType { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string ConfigurationStatus { get; init; }
    public required string AvailabilityStatus { get; init; }
    public required string HealthStatus { get; init; }
    public required DateTime? LastHealthCheckAt { get; init; }

    /// <summary>
    /// Safe diagnostic message for administrators.
    /// Must not contain secrets, credentials, or tokens.
    /// </summary>
    public required string? DiagnosticMessage { get; init; }

    public static AdapterDto FromEntity(PlatformAdapter a) => new()
    {
        Id = a.Id,
        AdapterKey = a.AdapterKey,
        AdapterType = a.AdapterType.ToString(),
        Name = a.Name,
        Version = a.Version,
        ConfigurationStatus = a.ConfigurationStatus.ToString(),
        AvailabilityStatus = a.AvailabilityStatus.ToString(),
        HealthStatus = a.HealthStatus.ToString(),
        LastHealthCheckAt = a.LastHealthCheckAt,
        DiagnosticMessage = a.DiagnosticMessage,
    };
}
