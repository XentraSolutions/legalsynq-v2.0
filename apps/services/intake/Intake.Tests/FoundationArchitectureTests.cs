using Intake.Application;
using Intake.Domain;
using Xunit;

namespace Intake.Tests;

public sealed class FoundationArchitectureTests
{
    [Fact]
    public void Domain_does_not_reference_service_layers()
    {
        var references = typeof(IntakeDomainAssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Intake.Api", references);
        Assert.DoesNotContain("Intake.Application", references);
        Assert.DoesNotContain("Intake.Infrastructure", references);
    }

    [Fact]
    public void Application_does_not_reference_infrastructure_or_api()
    {
        var references = typeof(IntakeFoundationService)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Intake.Api", references);
        Assert.DoesNotContain("Intake.Infrastructure", references);
    }

    [Fact]
    public void Foundation_metadata_is_product_neutral()
    {
        var info = new IntakeFoundationService().GetServiceInfo();

        Assert.Equal("intake", info.Service);
        Assert.Equal("Synq Intake", info.DisplayName);
        Assert.DoesNotContain("Lien", info.DisplayName, StringComparison.OrdinalIgnoreCase);
    }
}