namespace Xenia.Application.Adapters;

/// <summary>
/// Manages the Xenia platform adapter registry.
///
/// The registry tracks the health and availability of all registered platform
/// adapters. It does not store credentials — those are managed externally.
///
/// Adapters are wired in DI and reflected into this registry for observability.
/// </summary>
public interface IAdapterRegistry
{
    /// <summary>Returns all registered adapters with their current status.</summary>
    Task<IReadOnlyList<AdapterDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns a single adapter by key, or null if not registered.</summary>
    Task<AdapterDto?> GetAsync(string adapterKey, CancellationToken ct = default);

    /// <summary>
    /// Records a health-check result for the given adapter.
    /// Creates the registry record if it does not already exist.
    /// </summary>
    Task RecordHealthCheckAsync(
        string adapterKey,
        bool isAvailable,
        bool isHealthy,
        string? diagnosticMessage = null,
        CancellationToken ct = default);
}
