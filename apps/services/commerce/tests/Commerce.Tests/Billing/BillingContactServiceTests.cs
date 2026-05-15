using Xunit;
using Commerce.Contracts.Billing;
using Commerce.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Tests.Billing;

public sealed class BillingContactServiceTests
{
    private static async Task<Guid> SeedAccountAsync(BillingTestHost host)
    {
        var a = await host.AccountService.CreateAsync(
            new CreateBillingAccountRequest("Acme", null, "USD"), default);
        return a.Id;
    }

    [Fact]
    public async Task Add_first_contact_of_type_marked_primary_remains_primary()
    {
        using var host = new BillingTestHost();
        var id = await SeedAccountAsync(host);

        var c = await host.ContactService.AddAsync(id,
            new CreateBillingContactRequest(BillingContactType.Billing, "Alice", "alice@x.com", null, true),
            default);
        Assert.True(c.IsPrimary);
    }

    [Fact]
    public async Task Adding_second_primary_of_same_type_demotes_previous()
    {
        using var host = new BillingTestHost();
        var id = await SeedAccountAsync(host);

        var first = await host.ContactService.AddAsync(id,
            new CreateBillingContactRequest(BillingContactType.Billing, "Alice", "a@x.com", null, true),
            default);
        var second = await host.ContactService.AddAsync(id,
            new CreateBillingContactRequest(BillingContactType.Billing, "Bob", "b@x.com", null, true),
            default);

        Assert.True(second.IsPrimary);
        var refreshed = await host.Db.BillingContacts.AsNoTracking().SingleAsync(c => c.Id == first.Id);
        Assert.False(refreshed.IsPrimary);
    }

    [Fact]
    public async Task Different_types_can_each_have_their_own_primary()
    {
        using var host = new BillingTestHost();
        var id = await SeedAccountAsync(host);

        var billing = await host.ContactService.AddAsync(id,
            new CreateBillingContactRequest(BillingContactType.Billing, "Alice", "a@x.com", null, true),
            default);
        var technical = await host.ContactService.AddAsync(id,
            new CreateBillingContactRequest(BillingContactType.Technical, "Bob", "b@x.com", null, true),
            default);

        Assert.True(billing.IsPrimary);
        Assert.True(technical.IsPrimary);
    }

    [Fact]
    public async Task MakePrimary_promotes_specified_contact()
    {
        using var host = new BillingTestHost();
        var id = await SeedAccountAsync(host);

        var first = await host.ContactService.AddAsync(id,
            new CreateBillingContactRequest(BillingContactType.Billing, "Alice", "a@x.com", null, true),
            default);
        var second = await host.ContactService.AddAsync(id,
            new CreateBillingContactRequest(BillingContactType.Billing, "Bob", "b@x.com", null, false),
            default);

        var promoted = await host.ContactService.MakePrimaryAsync(id, second.Id, default);
        Assert.True(promoted.IsPrimary);
        var refreshed = await host.Db.BillingContacts.AsNoTracking().SingleAsync(c => c.Id == first.Id);
        Assert.False(refreshed.IsPrimary);
    }
}
