namespace Xenia.Domain.Email;

/// <summary>Health status of an email source based on most recent validation.</summary>
public enum EmailHealthStatus
{
    /// <summary>No health information is available yet.</summary>
    Unknown,

    /// <summary>Source is reachable and credentials are valid.</summary>
    Healthy,

    /// <summary>Source is reachable but with warnings (e.g. slow latency, near-quota).</summary>
    Degraded,

    /// <summary>Source is unreachable or credentials are invalid.</summary>
    Unavailable,
}
