using System.ComponentModel.DataAnnotations;

namespace Monitoring.Infrastructure.Http;

/// <summary>
/// Bound from the <c>Monitoring:HttpCheck</c> configuration section.
/// Validated at startup via <c>ValidateDataAnnotations</c> +
/// <c>ValidateOnStart</c> so misconfiguration fails fast with a clear
/// message rather than producing surprising behavior at runtime.
/// </summary>
public sealed class HttpCheckOptions
{
    public const string SectionName = "Monitoring:HttpCheck";

    /// <summary>
    /// Per-request bound (in seconds) for an HTTP check. Enforced via a
    /// <see cref="CancellationTokenSource"/> linked to the host's stopping
    /// token, so timeouts never survive shutdown and shutdowns never wait
    /// on a slow target.
    /// </summary>
    [Range(1, 300, ErrorMessage = "Monitoring:HttpCheck:TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 10;
}
