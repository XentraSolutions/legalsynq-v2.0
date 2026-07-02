using CareConnect.Api.Endpoints;
using Xunit;

namespace CareConnect.Tests.Application;

public class EnrollmentPhoneSelectionTests
{
    [Fact]
    public void ResolveIdentityEnrollmentPhone_UsesNormalizedRegistrationPhone_WhenProvided()
    {
        var result = EnrollmentEndpoints.ResolveIdentityEnrollmentPhone("5551234567", " +15557654321 ");

        Assert.Equal("+15551234567", result);
    }

    [Fact]
    public void ResolveIdentityEnrollmentPhone_FallsBackToProviderPhone_WhenRegistrationPhoneMissing()
    {
        var result = EnrollmentEndpoints.ResolveIdentityEnrollmentPhone(null, " (555) 765-4321 ");

        Assert.Equal("+15557654321", result);
    }

    [Fact]
    public void ResolveIdentityEnrollmentPhone_ReturnsNull_WhenBothPhonesMissing()
    {
        var result = EnrollmentEndpoints.ResolveIdentityEnrollmentPhone(" ", null);

        Assert.Null(result);
    }
}
