using Commerce.Application.Integration.Abstractions;

namespace Commerce.Infrastructure.Integration.HostAdapters;

/// <summary>
/// LS-INT-01 — LegalSynq host integration adapter for Commerce.
/// Implements <see cref="IHostIntegrationAdapter"/> with
/// <see cref="LegalSynqJwtHostIdentityContextAccessor"/> and
/// <see cref="LegalSynqJwtHostTenantResolver"/>.
///
/// Replaces <see cref="LocalHostIntegrationAdapter"/> when
/// <c>LegalSynq:Identity:Enabled = true</c>.
/// </summary>
internal sealed class LegalSynqCommerceHostIntegrationAdapter : IHostIntegrationAdapter
{
    public LegalSynqCommerceHostIntegrationAdapter(
        LegalSynqJwtHostIdentityContextAccessor identityContextAccessor,
        LegalSynqJwtHostTenantResolver tenantResolver,
        IProvisioningHookPublisher provisioningHookPublisher)
    {
        IdentityContextAccessor = identityContextAccessor;
        TenantResolver = tenantResolver;
        ProvisioningHookPublisher = provisioningHookPublisher;
    }

    public string HostPlatformKey => "legalsynq";
    public IHostIdentityContextAccessor IdentityContextAccessor { get; }
    public IHostTenantResolver TenantResolver { get; }
    public IProvisioningHookPublisher ProvisioningHookPublisher { get; }
}
