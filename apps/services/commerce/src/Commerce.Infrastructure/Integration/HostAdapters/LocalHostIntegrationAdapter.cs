using Commerce.Application.Integration.Abstractions;

namespace Commerce.Infrastructure.Integration.HostAdapters;

/// <summary>
/// Default <see cref="IHostIntegrationAdapter"/> registration used while
/// Commerce runs standalone. Bundles the no-op accessors so a host can
/// be swapped in by a future integration phase without touching call
/// sites.
/// </summary>
internal sealed class LocalHostIntegrationAdapter : IHostIntegrationAdapter
{
    public LocalHostIntegrationAdapter(
        IHostIdentityContextAccessor identityContextAccessor,
        IHostTenantResolver tenantResolver,
        IProvisioningHookPublisher provisioningHookPublisher)
    {
        IdentityContextAccessor = identityContextAccessor;
        TenantResolver = tenantResolver;
        ProvisioningHookPublisher = provisioningHookPublisher;
    }

    public string HostPlatformKey => LocalHostIdentityContextAccessor.LocalHostPlatformKey;

    public IHostIdentityContextAccessor IdentityContextAccessor { get; }

    public IHostTenantResolver TenantResolver { get; }

    public IProvisioningHookPublisher ProvisioningHookPublisher { get; }
}
