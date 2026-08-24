using Tenant.Application.Services;
using Tenant.Domain;
using Xunit;

namespace Tenant.Application.Tests;

public sealed class TenantRegistrationTests
{
    [Fact]
    public void New_registration_is_pending_and_not_started()
    {
        var item = Create();
        Assert.Equal(RegistrationStatus.PendingReview, item.RegistrationStatus);
        Assert.Equal(RegistrationProvisioningStatus.NotStarted, item.ProvisioningStatus);
        Assert.Null(item.TenantId);
    }

    [Theory]
    [InlineData(" Example Legal ", "example-legal")]
    [InlineData("EXAMPLE__Legal", "example-legal")]
    [InlineData("example.nonprod", "example-nonprod")]
    public void Tenant_code_is_normalized_to_dns_slug(string input, string expected) =>
        Assert.Equal(expected, TenantRegistrationService.NormalizeCode(input));

    [Fact]
    public void Decline_requires_a_reason() => Assert.Throws<ArgumentException>(() => Create().Decline(Guid.NewGuid(), " "));

    [Fact]
    public void Dns_failure_preserves_approved_decision()
    {
        var item = Create(); item.BeginApproval(Guid.NewGuid());
        item.CompleteApproval(Guid.NewGuid(), "example.nonprod.legalsynq.com", false, "provider failed", "DnsRecord");
        Assert.Equal(RegistrationStatus.Approved, item.RegistrationStatus);
        Assert.Equal(RegistrationProvisioningStatus.Failed, item.ProvisioningStatus);
    }

    [Fact]
    public void Provisioned_registration_cannot_be_retried()
    {
        var item = Create(); item.BeginApproval(Guid.NewGuid()); item.CompleteApproval(Guid.NewGuid(), "example.legalsynq.com", true, null, null);
        Assert.Throws<InvalidOperationException>(item.BeginProvisioningRetry);
    }

    [Fact]
    public void Approval_reservation_cannot_be_taken_twice()
    {
        var item = Create(); item.BeginApproval(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => item.BeginApproval(Guid.NewGuid()));
    }

    private static TenantRegistration Create() => TenantRegistration.Create("Example", "example", "LAW_FIRM", null, "Jane", "Doe", "jane@example.com");
}
