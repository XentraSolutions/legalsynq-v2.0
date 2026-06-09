namespace Commerce.Application.Integration.Abstractions;

/// <summary>
/// Composite contract a concrete host platform implements to integrate
/// with Commerce. A future LegalSynq adapter (separate phase, not
/// COM-B08) would implement this interface and register replacement
/// implementations of <see cref="IHostIdentityContextAccessor"/>,
/// <see cref="IHostTenantResolver"/>, and
/// <see cref="IProvisioningHookPublisher"/>.
///
/// The default registration is a "local" adapter that surfaces the
/// no-op accessors so Commerce continues to run standalone.
/// </summary>
public interface IHostIntegrationAdapter
{
    /// <summary>
    /// Stable identifier for this host platform (e.g. <c>"legalsynq"</c>,
    /// <c>"local"</c>).
    /// </summary>
    string HostPlatformKey { get; }

    IHostIdentityContextAccessor IdentityContextAccessor { get; }

    IHostTenantResolver TenantResolver { get; }

    IProvisioningHookPublisher ProvisioningHookPublisher { get; }
}
