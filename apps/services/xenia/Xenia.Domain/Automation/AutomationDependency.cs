namespace Xenia.Domain.Automation;

public enum DependencyCriticality { Optional = 0, Recommended = 1, Mandatory = 2 }
public enum DependencyAvailabilityState { Unknown = 0, Available = 1, Degraded = 2, Unavailable = 3, Disabled = 4 }

/// <summary>
/// Declares a runtime dependency for an automation provider.
/// Dependencies affect health state and readiness — Mandatory unavailability = Unavailable.
/// </summary>
public sealed record AutomationDependency
{
    public required string Key { get; init; }
    public required string DependencyType { get; init; }
    public required DependencyCriticality Criticality { get; init; }
    public string? MinimumVersion { get; init; }
    public bool IsOptional => Criticality == DependencyCriticality.Optional;
    public required DependencyAvailabilityState AvailabilityState { get; init; }
    public string? ConfigurationState { get; init; }
    public string? HealthImpact { get; init; }
}
