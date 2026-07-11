namespace Xenia.Domain.Automation;

/// <summary>
/// Provider-neutral description of a registered automation.
/// All automation providers must expose a manifest so the platform can
/// discover, display, and manage them without knowing provider internals.
///
/// No email-specific or LegalSynq-specific fields belong here.
/// </summary>
public sealed record AutomationManifest
{
    public required string AutomationKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Version { get; init; }
    public required string Category { get; init; }
    public required string Provider { get; init; }
    public required AutomationLifecycleState Status { get; init; }
    public required AutomationCapability Capabilities { get; init; }
    public required IReadOnlyList<AutomationDependency> Dependencies { get; init; }
    public required IReadOnlyList<string> Permissions { get; init; }
    public required string ConfigurationNamespace { get; init; }
    public required IReadOnlyList<AutomationTriggerType> SupportedTriggers { get; init; }
    public required IReadOnlyList<string> SupportedExecutionModes { get; init; }
    public required bool TenantEnablementSupported { get; init; }
    public required bool SchedulingSupported { get; init; }
    public required bool DiagnosticsSupported { get; init; }
    public required bool HealthSupported { get; init; }
    public required string MinimumPlatformVersion { get; init; }
    public required int MetadataVersion { get; init; }
}
