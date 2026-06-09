using Commerce.Contracts.Integration;

namespace Commerce.Application.Integration.Abstractions;

/// <summary>
/// Provides the host-supplied identity context for the current request,
/// if any. Hosts implement this in their integration adapter to surface
/// authenticated identity to Commerce. The default
/// <c>LocalHostIdentityContextAccessor</c> always returns an anonymous
/// "local" context so the standalone Commerce service still runs.
/// </summary>
public interface IHostIdentityContextAccessor
{
    /// <summary>
    /// The identity context for the current request. Never null.
    /// Returns an anonymous context when no host identity is present.
    /// </summary>
    HostIdentityContext Current { get; }
}
