using Xunit;
using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Billing;
using Commerce.Domain.Billing;
using Commerce.Domain.Billing.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Tests.Billing;

public sealed class BillingAccountServiceTests
{
    [Fact]
    public async Task Create_assigns_account_number_and_creates_profile_and_audit()
    {
        using var host = new BillingTestHost();
        var resp = await host.AccountService.CreateAsync(
            new CreateBillingAccountRequest("Acme Co", "Acme, Inc.", "USD"), default);

        Assert.Equal("COM-BA-000001", resp.AccountNumber);
        Assert.Equal(BillingAccountStatus.Draft, resp.Status);

        var profile = await host.Db.BillingProfiles.SingleAsync(p => p.BillingAccountId == resp.Id);
        Assert.NotNull(profile);

        var audits = await host.Db.BillingAccountAuditEvents
            .Where(a => a.BillingAccountId == resp.Id).ToListAsync();
        Assert.Contains(audits, a => a.EventType == BillingAccountAuditEventTypes.AccountCreated);
    }

    [Fact]
    public async Task Create_increments_account_number_sequentially()
    {
        using var host = new BillingTestHost();
        var a = await host.AccountService.CreateAsync(new CreateBillingAccountRequest("A", null, "USD"), default);
        var b = await host.AccountService.CreateAsync(new CreateBillingAccountRequest("B", null, "EUR"), default);
        Assert.Equal("COM-BA-000001", a.AccountNumber);
        Assert.Equal("COM-BA-000002", b.AccountNumber);
    }

    [Fact]
    public async Task Create_validates_required_fields()
    {
        using var host = new BillingTestHost();
        await Assert.ThrowsAsync<ValidationException>(() =>
            host.AccountService.CreateAsync(new CreateBillingAccountRequest("", null, "USD"), default));
    }

    [Fact]
    public async Task Update_changes_fields_and_writes_audit()
    {
        using var host = new BillingTestHost();
        var a = await host.AccountService.CreateAsync(new CreateBillingAccountRequest("A", null, "USD"), default);
        var updated = await host.AccountService.UpdateAsync(a.Id,
            new UpdateBillingAccountRequest("A2", "Legal A", "EUR"), default);
        Assert.Equal("A2", updated.DisplayName);
        Assert.Equal("EUR", updated.DefaultCurrency);

        var audits = await host.Db.BillingAccountAuditEvents
            .Where(e => e.BillingAccountId == a.Id).ToListAsync();
        Assert.Contains(audits, e => e.EventType == BillingAccountAuditEventTypes.AccountUpdated);
    }

    [Fact]
    public async Task Get_throws_NotFound_for_unknown()
    {
        using var host = new BillingTestHost();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            host.AccountService.GetAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Lifecycle_Draft_to_Active_to_Suspended_to_Active_to_Closed()
    {
        using var host = new BillingTestHost();
        var a = await host.AccountService.CreateAsync(new CreateBillingAccountRequest("A", null, "USD"), default);

        var act = await host.AccountService.ActivateAsync(a.Id, default);
        Assert.Equal(BillingAccountStatus.Active, act.Status);

        var sus = await host.AccountService.SuspendAsync(a.Id, default);
        Assert.Equal(BillingAccountStatus.Suspended, sus.Status);

        var resumed = await host.AccountService.ActivateAsync(a.Id, default);
        Assert.Equal(BillingAccountStatus.Active, resumed.Status);

        var closed = await host.AccountService.CloseAsync(a.Id, default);
        Assert.Equal(BillingAccountStatus.Closed, closed.Status);
    }

    [Fact]
    public async Task Closed_account_cannot_be_reactivated()
    {
        using var host = new BillingTestHost();
        var a = await host.AccountService.CreateAsync(new CreateBillingAccountRequest("A", null, "USD"), default);
        await host.AccountService.ActivateAsync(a.Id, default);
        await host.AccountService.CloseAsync(a.Id, default);

        await Assert.ThrowsAsync<InvalidStateTransitionException>(() =>
            host.AccountService.ActivateAsync(a.Id, default));
    }

    [Fact]
    public async Task Cannot_suspend_a_Draft_account()
    {
        using var host = new BillingTestHost();
        var a = await host.AccountService.CreateAsync(new CreateBillingAccountRequest("A", null, "USD"), default);
        await Assert.ThrowsAsync<InvalidStateTransitionException>(() =>
            host.AccountService.SuspendAsync(a.Id, default));
    }

    [Fact]
    public async Task Cannot_close_a_Draft_account()
    {
        using var host = new BillingTestHost();
        var a = await host.AccountService.CreateAsync(new CreateBillingAccountRequest("A", null, "USD"), default);
        await Assert.ThrowsAsync<InvalidStateTransitionException>(() =>
            host.AccountService.CloseAsync(a.Id, default));
    }
}
