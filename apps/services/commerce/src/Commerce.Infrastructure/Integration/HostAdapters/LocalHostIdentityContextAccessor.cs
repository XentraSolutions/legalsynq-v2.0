using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;

namespace Commerce.Infrastructure.Integration.HostAdapters;

/// <summary>
/// Default no-op accessor used when Commerce runs standalone (no host
/// integration registered). Returns an anonymous identity context whose
/// <see cref="HostIdentityContext.IsAuthenticated"/> is false. The real
/// JWT/OIDC flow is intentionally NOT implemented in COM-B08.
/// </summary>
internal sealed class LocalHostIdentityContextAccessor : IHostIdentityContextAccessor
{
    public const string LocalHostPlatformKey = "local";

    public HostIdentityContext Current { get; } =
        HostIdentityContext.Anonymous(LocalHostPlatformKey);
}
