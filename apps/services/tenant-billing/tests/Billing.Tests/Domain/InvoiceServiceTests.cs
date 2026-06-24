using FluentAssertions;
using Billing.Domain.Services;
using Xunit;

namespace Billing.Tests.Domain;

public class InvoiceServiceTests
{
    private static readonly DateTime IssueDate = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DueDate = IssueDate.AddDays(30);

    private static IReadOnlyList<NewInvoiceLine> SingleLine(decimal unitPrice = 100m, int quantity = 1)
        => new[] { new NewInvoiceLine("Widget", quantity, unitPrice) };

    [Fact]
    public async Task Create_persists_invoice_for_customer_in_same_tenant()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);

        var invoice = await host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-001", IssueDate, DueDate, "USD", null,
            SingleLine(100m), taxAmount: 0m);

        invoice.TenantId.Should().Be(tenantId);
        invoice.CustomerId.Should().Be(customer.Id);
        invoice.InvoiceNumber.Should().Be("INV-001");
        invoice.Status.Should().Be("Draft");
        invoice.LineItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_rejects_customer_owned_by_other_tenant()
    {
        using var host = new DomainTestHost();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantA, "Acme", "acme@example.com", null, null, null, null);

        Func<Task> act = () => host.Invoices.CreateAsync(
            tenantB, customer.Id, "INV-001", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Customer {customer.Id} does not belong to tenant {tenantB}*");
    }

    [Fact]
    public async Task Create_rejects_unknown_customer()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var unknownCustomerId = Guid.CreateVersion7();

        Func<Task> act = () => host.Invoices.CreateAsync(
            tenantId, unknownCustomerId, "INV-001", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        // Main reworded the unknown-customer guard to also cover the
        // soft-delete case in a single message.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Customer {unknownCustomerId}*");
    }

    [Fact]
    public async Task Create_throws_DuplicateInvoiceNumberException_for_same_tenant_and_number()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);

        await host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-DUP", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        Func<Task> act = () => host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-DUP", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        var ex = await act.Should().ThrowAsync<DuplicateInvoiceNumberException>();
        ex.Which.TenantId.Should().Be(tenantId);
        ex.Which.InvoiceNumber.Should().Be("INV-DUP");
    }

    [Fact]
    public async Task Create_allows_same_InvoiceNumber_across_different_tenants()
    {
        using var host = new DomainTestHost();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var customerA = await host.Customers.CreateAsync(tenantA, "A", "a@example.com", null, null, null, null);
        var customerB = await host.Customers.CreateAsync(tenantB, "B", "b@example.com", null, null, null, null);

        var first = await host.Invoices.CreateAsync(
            tenantA, customerA.Id, "INV-100", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);
        var second = await host.Invoices.CreateAsync(
            tenantB, customerB.Id, "INV-100", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        first.InvoiceNumber.Should().Be("INV-100");
        second.InvoiceNumber.Should().Be("INV-100");
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public async Task Create_trims_invoice_number_when_checking_for_duplicates()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);

        await host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-TRIM", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        Func<Task> act = () => host.Invoices.CreateAsync(
            tenantId, customer.Id, "  INV-TRIM  ", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        await act.Should().ThrowAsync<DuplicateInvoiceNumberException>();
    }

    [Fact]
    public async Task Create_rounds_unit_price_line_total_subtotal_tax_and_total_to_two_decimal_places()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);

        // UnitPrice 1.235 * Qty 3 = 3.705 -> rounded line total 3.71 (away-from-zero)
        // Tax 0.125 -> 0.13. Subtotal 3.71. Total 3.84.
        var lines = new[] { new NewInvoiceLine("Item", 3, 1.235m) };
        var invoice = await host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-ROUND", IssueDate, DueDate, "usd", null,
            lines, taxAmount: 0.125m);

        invoice.LineItems.Single().UnitPrice.Should().Be(1.24m);
        invoice.LineItems.Single().LineTotal.Should().Be(3.71m);
        invoice.Subtotal.Should().Be(3.71m);
        invoice.TaxAmount.Should().Be(0.13m);
        invoice.TotalAmount.Should().Be(3.84m);
        invoice.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Create_rejects_DueDate_before_IssueDate()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);

        Func<Task> act = () => host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-DATES", IssueDate, IssueDate.AddDays(-1), "USD", null,
            SingleLine(), taxAmount: 0m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*DueDate must be on or after IssueDate*");
    }

    [Fact]
    public async Task Create_allows_DueDate_equal_to_IssueDate()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);

        var invoice = await host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-SAME", IssueDate, IssueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        invoice.IssueDate.Should().Be(invoice.DueDate);
    }

    [Fact]
    public async Task Create_rejects_negative_TaxAmount()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);

        Func<Task> act = () => host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-NEGTAX", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: -1m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*TaxAmount must be >= 0*");
    }

    [Fact]
    public async Task Create_rejects_empty_lines()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);

        Func<Task> act = () => host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-NOLINES", IssueDate, DueDate, "USD", null,
            Array.Empty<NewInvoiceLine>(), taxAmount: 0m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least one line item is required*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Create_rejects_line_with_non_positive_quantity(int quantity)
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);

        Func<Task> act = () => host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-QTY", IssueDate, DueDate, "USD", null,
            new[] { new NewInvoiceLine("Item", quantity, 10m) }, taxAmount: 0m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Quantity must be >= 1*");
    }

    [Fact]
    public async Task Create_rejects_line_with_negative_unit_price()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.CreateVersion7();
        var customer = await host.Customers.CreateAsync(tenantId, "Acme", "acme@example.com", null, null, null, null);

        Func<Task> act = () => host.Invoices.CreateAsync(
            tenantId, customer.Id, "INV-NEGPRICE", IssueDate, DueDate, "USD", null,
            new[] { new NewInvoiceLine("Item", 1, -1m) }, taxAmount: 0m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*UnitPrice must be >= 0*");
    }
}
