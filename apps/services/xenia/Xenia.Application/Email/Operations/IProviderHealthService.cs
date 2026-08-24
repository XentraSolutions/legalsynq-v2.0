using Xenia.Domain.Email;

namespace Xenia.Application.Email.Operations;

/// <summary>
/// Returns per-provider operational health and aggregate statistics for a tenant.
///
/// Classification must accurately reflect actual operational capability:
/// - Operational: connector is live-tested and known to work in production.
/// - ProtocolCompleteUnverified: full protocol implemented, not yet tested live.
/// - ValidationOnly: can only validate connectivity, not ingest.
/// - Stub: placeholder implementation.
/// - Blocked: disabled or deprecated.
/// </summary>
public interface IProviderHealthService
{
    Task<IReadOnlyList<ProviderHealthSnapshot>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
}

public enum ProviderClassification
{
    Operational                = 1,
    ProtocolCompleteUnverified = 2,
    ValidationOnly             = 3,
    Stub                       = 4,
    Blocked                    = 5,
}

public sealed record ProviderHealthSnapshot(
    EmailProviderType Provider,
    string ProviderDisplayName,
    ProviderClassification Classification,
    string OperationalStatus,
    bool SupportsAuthentication,
    bool SupportsIngestion,
    bool SupportsIncrementalCursor,
    bool SupportsAttachments,
    bool SupportsRateLimitHandling,
    int ConfiguredSourceCount,
    int HealthySourceCount,
    int FailedSourceCount,
    double RecentErrorRate,
    double? AverageConnectionLatencyMs,
    DateTime? LastSuccessfulOperationAt,
    double? AvailabilityPercent,
    string? SafeDiagnostics);
