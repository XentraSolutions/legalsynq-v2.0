using Xunit;
using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Billing;
using Commerce.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Tests.Billing;

public sealed class BillingProfileAndAuditTests
{
    [Fact]
    public async Task Profile_is_auto_created_on_account_create_and_can_be_fetched()
    {
        using var host = new BillingTestHost();
        var a = await host.AccountService.CreateAsync(
            new CreateBillingAccountRequest("Acme", null, "USD"), default);

        var p = await host.ProfileService.GetAsync(a.Id, default);
        Assert.Equal(a.Id, p.BillingAccountId);
        Assert.False(p.TaxExempt);
    }

    [Fact]
    public async Task Profile_update_persists_address_fields_and_writes_audit()
    {
        using var host = new BillingTestHost();
        var a = await host.AccountService.CreateAsync(
            new CreateBillingAccountRequest("Acme", null, "USD"), default);

        var p = await host.ProfileService.UpdateAsync(a.Id,
            new UpdateBillingProfileRequest("123 Main", null, "Boston", "MA", "02101", "US", "TAX-1", true),
            default);
        Assert.Equal("123 Main", p.AddressLine1);
        Assert.True(p.TaxExempt);

        var events = await host.AuditService.ListAsync(a.Id, default);
        Assert.Contains(events, e => e.EventType == BillingAccountAuditEventTypes.BillingProfileUpdated);
    }

    [Fact]
    public async Task Audit_history_contains_created_and_updated_for_account_lifecycle()
    {
        using var host = new BillingTestHost();
        var a = await host.AccountService.CreateAsync(
            new CreateBillingAccountRequest("Acme", null, "USD"), default);
        await host.AccountService.ActivateAsync(a.Id, default);
        await host.AccountService.SuspendAsync(a.Id, default);

        var events = await host.AuditService.ListAsync(a.Id, default);
        Assert.Contains(events, e => e.EventType == BillingAccountAuditEventTypes.AccountCreated);
        Assert.Contains(events, e => e.EventType == BillingAccountAuditEventTypes.AccountActivated);
        Assert.Contains(events, e => e.EventType == BillingAccountAuditEventTypes.AccountSuspended);
    }

    [Fact]
    public async Task Profile_get_does_not_self_heal_a_missing_profile()
    {
        using var host = new BillingTestHost();
        var a = await host.AccountService.CreateAsync(
            new CreateBillingAccountRequest("Acme", null, "USD"), default);

        // Forcibly remove the auto-created profile to simulate legacy data,
        // then verify that GetAsync surfaces NotFound rather than re-creating.
        var existing = await host.Db.BillingProfiles.SingleAsync(p => p.BillingAccountId == a.Id);
        host.Db.BillingProfiles.Remove(existing);
        await host.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => host.ProfileService.GetAsync(a.Id, default));
        Assert.False(await host.Db.BillingProfiles.AnyAsync(p => p.BillingAccountId == a.Id));
    }
}
