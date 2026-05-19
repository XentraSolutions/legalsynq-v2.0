using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Invoicing;
using Commerce.Domain.Invoicing.Enums;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Commerce.Tests.Invoicing;

public class InvoiceServiceTests
{
    [Fact]
    public async Task Create_returns_invoice_with_total_and_lines()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();

        var req = new CreateInvoiceRequest(
            account.Id, "USD",
            new[] {
                new CreateInvoiceLineRequest("Seats", 3, 1500),
                new CreateInvoiceLineRequest("Setup", 1, 500)
            },
            null, host.Clock.UtcNow.AddDays(7));

        var resp = await host.Invoices.CreateAsync(req, default);

        resp.BillingAccountId.Should().Be(account.Id);
        resp.Status.Should().Be(InvoiceStatus.Open);
        resp.SubtotalAmountMinor.Should().Be(5000);
        resp.TotalAmountMinor.Should().Be(5000);
        resp.AmountDueMinor.Should().Be(5000);
        resp.InvoiceNumber.Should().StartWith("COM-INV-");
        resp.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_for_unknown_account_returns_404()
    {
        using var host = new InvoicingTestHost();
        var req = new CreateInvoiceRequest(
            Guid.CreateVersion7(), "USD",
            new[] { new CreateInvoiceLineRequest("X", 1, 100) });
        Func<Task> a = () => host.Invoices.CreateAsync(req, default);
        await a.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_with_subscription_from_other_account_throws()
    {
        using var host = new InvoicingTestHost();
        var a = host.AddActiveAccount("COM-ACC-INV-A");
        var b = host.AddActiveAccount("COM-ACC-INV-B");
        var sub = host.AddActiveSubscription(b);

        var req = new CreateInvoiceRequest(
            a.Id, "USD",
            new[] { new CreateInvoiceLineRequest("X", 1, 100) },
            SubscriptionId: sub.Id);

        Func<Task> act = () => host.Invoices.CreateAsync(req, default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Create_with_subscription_item_without_parent_subscription_throws()
    {
        using var host = new InvoicingTestHost();
        var a = host.AddActiveAccount();

        var req = new CreateInvoiceRequest(
            a.Id, "USD",
            new[] {
                new CreateInvoiceLineRequest("X", 1, 100, SubscriptionItemId: Guid.CreateVersion7())
            });

        Func<Task> act = () => host.Invoices.CreateAsync(req, default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Create_invalid_currency_yields_validation()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();

        var req = new CreateInvoiceRequest(
            account.Id, "BADCUR",
            new[] { new CreateInvoiceLineRequest("X", 1, 100) });

        Func<Task> a = () => host.Invoices.CreateAsync(req, default);
        await a.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Get_returns_invoice_with_lines()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();
        var created = await host.Invoices.CreateAsync(
            new CreateInvoiceRequest(account.Id, "USD",
                new[] { new CreateInvoiceLineRequest("L", 1, 250) }), default);

        var got = await host.Invoices.GetAsync(created.Id, default);
        got.Id.Should().Be(created.Id);
        got.Lines.Should().HaveCount(1);
        got.Lines[0].LineAmountMinor.Should().Be(250);
    }

    [Fact]
    public async Task ListForBillingAccount_returns_only_that_account_invoices()
    {
        using var host = new InvoicingTestHost();
        var a = host.AddActiveAccount("COM-ACC-INV-AA");
        var b = host.AddActiveAccount("COM-ACC-INV-BB");
        await host.Invoices.CreateAsync(new CreateInvoiceRequest(a.Id, "USD",
            new[] { new CreateInvoiceLineRequest("X", 1, 100) }), default);
        await host.Invoices.CreateAsync(new CreateInvoiceRequest(b.Id, "USD",
            new[] { new CreateInvoiceLineRequest("Y", 1, 200) }), default);

        var listA = await host.Invoices.ListForBillingAccountAsync(a.Id, default);
        listA.Should().HaveCount(1);
        listA[0].BillingAccountId.Should().Be(a.Id);
    }

    [Fact]
    public async Task InvoiceNumberGenerator_returns_well_formed_number_when_no_invoices()
    {
        using var host = new InvoicingTestHost();
        var n = await host.Numbers.AllocateAsync(default);
        n.Should().StartWith("COM-INV-");
    }

    [Fact]
    public async Task InvoiceNumberGenerator_advances_after_invoice_persisted()
    {
        using var host = new InvoicingTestHost();
        var account = host.AddActiveAccount();
        await host.Invoices.CreateAsync(new CreateInvoiceRequest(account.Id, "USD",
            new[] { new CreateInvoiceLineRequest("X", 1, 100) }), default);
        var next = await host.Numbers.AllocateAsync(default);
        next.Should().StartWith("COM-INV-");
        var existing = await host.Db.Invoices.AsNoTracking().Select(i => i.InvoiceNumber).ToListAsync();
        existing.Should().NotContain(next);
    }
}
