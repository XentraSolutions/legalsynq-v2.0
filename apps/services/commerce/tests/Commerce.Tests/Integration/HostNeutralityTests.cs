using System.Reflection;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Commerce.Infrastructure.Integration.HostAdapters;
using Commerce.Infrastructure.Integration.Services;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Integration;

/// <summary>
/// Structural checks that prove COM-B08 contracts and infrastructure
/// services do not depend on any concrete host platform (LegalSynq, a
/// Tenant service, an Identity service, etc.). These tests fail fast if
/// anyone smuggles in a host-specific reference.
/// </summary>
public class HostNeutralityTests
{
    private static readonly Assembly[] InspectedAssemblies =
    {
        typeof(HostTenantRef).Assembly,                          // Commerce.Contracts
        typeof(IHostIntegrationAdapter).Assembly,                // Commerce.Application
        typeof(LocalHostIdentityContextAccessor).Assembly,       // Commerce.Infrastructure
    };

    private static readonly string[] BannedTypeNameFragments =
    {
        "LegalSynq",
        "Yarp",
        "Identity.Client",          // MSAL / azure identity
        "TenantServiceClient",
        "IdentityServiceClient",
    };

    [Fact]
    public void Contract_and_infrastructure_assemblies_have_no_host_specific_types()
    {
        foreach (var asm in InspectedAssemblies)
        {
            foreach (var type in asm.GetTypes())
            {
                foreach (var fragment in BannedTypeNameFragments)
                {
                    type.FullName.Should().NotContain(fragment,
                        $"COM-B08 must remain host-neutral; '{type.FullName}' violated this in {asm.GetName().Name}.");
                }
            }
        }
    }

    [Fact]
    public void Contract_assembly_only_references_base_class_libraries()
    {
        var refs = typeof(HostTenantRef).Assembly.GetReferencedAssemblies();
        refs.Should().NotContain(r => r.Name!.Contains("LegalSynq", StringComparison.OrdinalIgnoreCase));
        refs.Should().NotContain(r => r.Name!.Contains("Yarp", StringComparison.OrdinalIgnoreCase));
        refs.Should().NotContain(r => r.Name!.Contains("Tenant", StringComparison.OrdinalIgnoreCase));
        refs.Should().NotContain(r => r.Name!.Contains("Identity.Client", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Default_adapters_are_local_no_op_implementations()
    {
        var idType = typeof(LocalHostIdentityContextAccessor);
        var tenantType = typeof(NoopHostTenantResolver);
        var hookType = typeof(NoopProvisioningHookPublisher);

        idType.GetInterfaces().Should().Contain(typeof(IHostIdentityContextAccessor));
        tenantType.GetInterfaces().Should().Contain(typeof(IHostTenantResolver));
        hookType.GetInterfaces().Should().Contain(typeof(IProvisioningHookPublisher));
    }

    [Fact]
    public void No_yarp_reverseproxy_or_identity_host_packages_are_referenced_by_api()
    {
        var apiRefs = typeof(Program).Assembly.GetReferencedAssemblies()
            .Select(r => r.Name ?? string.Empty)
            .ToArray();
        apiRefs.Should().NotContain(r => r.Contains("Yarp", StringComparison.OrdinalIgnoreCase));
        apiRefs.Should().NotContain(r => r.Contains("LegalSynq", StringComparison.OrdinalIgnoreCase));
        apiRefs.Should().NotContain(r => r.Equals("Microsoft.AspNetCore.Authentication.JwtBearer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Local_adapter_returns_anonymous_unauthenticated_context()
    {
        IHostIdentityContextAccessor accessor = new LocalHostIdentityContextAccessor();
        var ctx = accessor.Current;
        ctx.IsAuthenticated.Should().BeFalse();
        ctx.HostPlatformKey.Should().Be(LocalHostIdentityContextAccessor.LocalHostPlatformKey);
        ctx.Roles.Should().BeEmpty();
        ctx.Scopes.Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Noop_provisioning_hook_publisher_accepts_without_delivering()
    {
        IProvisioningHookPublisher pub = new NoopProvisioningHookPublisher(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NoopProvisioningHookPublisher>.Instance);
        var result = await pub.PublishAsync(new ProvisioningHookRequest(
            HostTenantRef: new HostTenantRef("local", "tnt-x"),
            BillingAccountId: Guid.NewGuid(),
            SubscriptionId: null,
            ProductKey: "k-prod",
            PlanKey: "k-plan",
            RequestedAction: ProvisioningAction.Provision), default);
        result.Accepted.Should().BeTrue();
        result.Delivered.Should().BeFalse();
    }
}
