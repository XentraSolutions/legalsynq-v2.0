namespace Xenia.Domain.Adapters;

/// <summary>
/// Classifies how a platform adapter's unavailability affects Xenia readiness.
///
/// Optional   — Xenia degrades gracefully if this adapter is unavailable.
///              /ready returns 200 with a degraded note.
///              This is the CLR default (value = 0) so EF does not require a sentinel.
/// Mandatory  — Xenia cannot serve requests without this adapter. Unavailability
///              causes /ready to return 503.
/// Disabled   — Adapter is intentionally not wired; its status is ignored entirely.
/// </summary>
public enum AdapterCriticality
{
    /// <summary>
    /// Adapter is used when available; Xenia degrades gracefully if absent.
    /// Unavailability causes /ready → 200 degraded.
    /// This is the enum CLR default (value = 0).
    /// </summary>
    Optional = 0,

    /// <summary>
    /// Adapter is required for Xenia to serve requests.
    /// Unavailability causes /ready → 503.
    /// </summary>
    Mandatory = 1,

    /// <summary>
    /// Adapter is intentionally disabled for this deployment.
    /// Its status is excluded from readiness computation entirely.
    /// </summary>
    Disabled = 2,
}
