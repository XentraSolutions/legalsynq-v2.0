namespace Xenia.Application.Automation;

/// <summary>
/// Safe diagnostic snapshot for authorized support users.
/// No secrets, no raw payloads, no credentials, no message bodies, no cursors.
/// </summary>
public interface IAutomationDiagnosticsService
{
    Task<AutomationDiagnosticsSnapshot> GetSnapshotAsync(Guid? tenantId, CancellationToken ct = default);
    Task<AutomationSupportBundle> GenerateSupportBundleAsync(Guid? tenantId, CancellationToken ct = default);
}

public sealed record AutomationDiagnosticsSnapshot
{
    public required DateTime GeneratedAt { get; init; }
    public required string ServiceVersion { get; init; }
    public required string Environment { get; init; }
    public required IReadOnlyList<AutomationRegistryEntry> Registrations { get; init; }
    public required IReadOnlyList<AutomationWorkerStatus> Workers { get; init; }
    public required IReadOnlyList<AutomationDependencyStatus> Dependencies { get; init; }
    public required int ActiveExecutions { get; init; }
    public required int DeadLetterCount { get; init; }
}

public sealed record AutomationRegistryEntry
{
    public required string AutomationKey { get; init; }
    public required string Version { get; init; }
    public required string Provider { get; init; }
    public required string EffectiveState { get; init; }
    public required int ActiveExecutions { get; init; }
    public required int TotalExecutions { get; init; }
    public required int FailedExecutions { get; init; }
    public required DateTime? LastExecutedAt { get; init; }
    public required string? LastSafeError { get; init; }
}

public sealed record AutomationWorkerStatus
{
    public required string Name { get; init; }
    public required bool IsRunning { get; init; }
    public required DateTime? LastRunAt { get; init; }
    public required string? SafeStatus { get; init; }
}

public sealed record AutomationDependencyStatus
{
    public required string Key { get; init; }
    public required string DependencyType { get; init; }
    public required string Criticality { get; init; }
    public required string AvailabilityState { get; init; }
    public required bool IsConfigured { get; init; }
}

public sealed record AutomationSupportBundle
{
    public required DateTime GeneratedAt { get; init; }
    public required string ServiceVersion { get; init; }
    public required string Environment { get; init; }
    public required AutomationDiagnosticsSnapshot Diagnostics { get; init; }
    public required IReadOnlyList<string> ConfigurationKeyNames { get; init; }
    public required IReadOnlyList<string> MigrationIds { get; init; }
    public required string SafeSummary { get; init; }
    public int BundleSizeEstimateBytes { get; init; }
}
