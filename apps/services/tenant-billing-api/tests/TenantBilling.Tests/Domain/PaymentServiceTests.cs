using FluentAssertions;
using TenantBilling.Domain.Services;
using Xunit;

namespace TenantBilling.Tests.Domain;

public class PaymentServiceTests
{
    private static readonly DateTime IssueDate = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DueDate = IssueDate.AddDays(15);

    private static async Task<(Guid TenantId, Guid InvoiceId)> SeedInvoiceAsync(DomainTestHost host)
    {
        var tenantId = Guid.NewGuid();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);
        var invoice = await host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-PAY", IssueDate, DueDate, "USD", null,
            new[] { new NewInvoiceLine("Widget", 1, 100m) },
            taxAmount: 0m);
        // Main added a lifecycle gate that rejects payments unless the invoice
        // has been Issued. Move the seeded invoice past Draft so the payment
        // tests can exercise the post-validation behavior they care about.
        await host.Invoices.IssueAsync(tenantId, invoice.Id);
        return (tenantId, invoice.Id);
    }

    [Fact]
    public async Task Create_persists_payment_for_invoice_in_same_tenant()
    {
        using var host = new DomainTestHost();
        var (tenantId, invoiceId) = await SeedInvoiceAsync(host);

        var payment = await host.Payments.CreateAsync(
            tenantId, invoiceId, amount: 50m, currency: "usd", method: "card",
            status: "succeeded", transactionReference: " ref-123 ", paidAt: null);

        payment.TenantId.Should().Be(tenantId);
        payment.InvoiceId.Should().Be(invoiceId);
        payment.Amount.Should().Be(50m);
        payment.Currency.Should().Be("USD");
        payment.Method.Should().Be("card");
        payment.Status.Should().Be("succeeded");
        payment.TransactionReference.Should().Be("ref-123");
    }

    [Fact]
    public async Task Create_rejects_invoice_owned_by_other_tenant()
    {
        using var host = new DomainTestHost();
        var (tenantA, invoiceId) = await SeedInvoiceAsync(host);
        var tenantB = Guid.NewGuid();
        tenantB.Should().NotBe(tenantA);

        Func<Task> act = () => host.Payments.CreateAsync(
            tenantB, invoiceId, amount: 25m, currency: "USD", method: "card",
            status: "Pending", transactionReference: null, paidAt: null);

        // The PaymentService deliberately surfaces a generic "not found"
        // for cross-tenant invoice access so it cannot leak the existence
        // of an invoice belonging to another tenant.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Invoice {invoiceId} not found*");
    }

    [Fact]
    public async Task Create_rejects_unknown_invoice()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.NewGuid();
        var unknownInvoiceId = Guid.NewGuid();

        Func<Task> act = () => host.Payments.CreateAsync(
            tenantId, unknownInvoiceId, amount: 10m, currency: "USD", method: "card",
            status: "Pending", transactionReference: null, paidAt: null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Invoice {unknownInvoiceId} not found*");
    }

    [Fact]
    public async Task Create_rounds_amount_to_two_decimal_places()
    {
        using var host = new DomainTestHost();
        var (tenantId, invoiceId) = await SeedInvoiceAsync(host);

        var payment = await host.Payments.CreateAsync(
            tenantId, invoiceId, amount: 12.345m, currency: "USD", method: "card",
            status: "Pending", transactionReference: null, paidAt: null);

        payment.Amount.Should().Be(12.35m);
    }

    [Fact]
    public async Task Create_rejects_non_positive_amount()
    {
        using var host = new DomainTestHost();
        var (tenantId, invoiceId) = await SeedInvoiceAsync(host);

        Func<Task> act = () => host.Payments.CreateAsync(
            tenantId, invoiceId, amount: 0m, currency: "USD", method: "card",
            status: "Pending", transactionReference: null, paidAt: null);

        // TBS-B04 introduced typed payment exceptions. The amount validation
        // surfaces as InvalidPaymentAmountException, which derives from
        // InvalidOperationException for back-compat with older catch sites.
        await act.Should().ThrowAsync<InvalidPaymentAmountException>()
            .WithMessage("*must be greater than zero*");
    }

    [Fact]
    public async Task Create_defaults_blank_status_to_Recorded()
    {
        using var host = new DomainTestHost();
        var (tenantId, invoiceId) = await SeedInvoiceAsync(host);

        // TBS-B04: payment lifecycle is server-controlled (Recorded → Voided).
        // A blank/whitespace status from a legacy caller now collapses to
        // the canonical "Recorded" instead of the older "Pending" placeholder.
        var payment = await host.Payments.CreateAsync(
            tenantId, invoiceId, amount: 1m, currency: "USD", method: "card",
            status: "   ", transactionReference: null, paidAt: null);

        payment.Status.Should().Be(PaymentService.RecordedStatus);
    }
}
