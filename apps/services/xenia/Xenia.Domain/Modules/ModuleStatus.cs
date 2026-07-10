namespace Xenia.Domain.Modules;

/// <summary>
/// Runtime health status of a registered Xenia module.
/// </summary>
public enum ModuleStatus
{
    /// <summary>Status has not been determined yet.</summary>
    Unknown,

    /// <summary>Module is operating normally.</summary>
    Healthy,

    /// <summary>Module is operational but degraded.</summary>
    Degraded,

    /// <summary>Module is not operational.</summary>
    Unavailable,
}
