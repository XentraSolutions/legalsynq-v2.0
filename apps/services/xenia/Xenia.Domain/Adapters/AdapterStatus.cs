namespace Xenia.Domain.Adapters;

/// <summary>Health / availability status of a platform adapter.</summary>
public enum AdapterStatus
{
    /// <summary>Status not yet checked.</summary>
    Unknown,

    /// <summary>Adapter is reachable and responding normally.</summary>
    Healthy,

    /// <summary>Adapter is reachable but responding with errors or latency.</summary>
    Degraded,

    /// <summary>Adapter is unreachable or returning failures.</summary>
    Unavailable,

    /// <summary>Adapter has not been configured for this environment.</summary>
    Unconfigured,
}
