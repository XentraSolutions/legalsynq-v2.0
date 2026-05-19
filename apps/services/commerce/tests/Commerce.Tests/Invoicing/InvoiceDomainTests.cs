using Commerce.Domain.Invoicing;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Invoicing;

public class InvoiceDomainTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Invoice_Create_normalizes_currency_and_initial_status()
    {
        var inv = Invoice.Create(Guid.CreateVersion7(), null, "INV-1", "usd", Now, Now.AddDays(7),
            InvoiceStatus.Open, Now);
        inv.Currency.Should().Be("USD");
        inv.Status.Should().Be(InvoiceStatus.Open);
        inv.InvoiceNumber.Should().Be("INV-1");
    }

    [Fact]
    public void Invoice_Create_rejects_invalid_currency()
    {
        Action a = () => Invoice.Create(Guid.CreateVersion7(), null, "INV-1", "USDX", Now, null,
            InvoiceStatus.Open, Now);
        a.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Invoice_Create_rejects_initial_status_other_than_draft_or_open()
    {
        Action a = () => Invoice.Create(Guid.CreateVersion7(), null, "INV-1", "USD", Now, null,
            InvoiceStatus.Paid, Now);
        a.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InvoiceLine_Create_computes_line_amount()
    {
        var line = InvoiceLine.Create(Guid.CreateVersion7(), null, "Seat", 3, 1500, "USD", null, null, Now);
        line.LineAmountMinor.Should().Be(4500);
    }

    [Fact]
    public void InvoiceLine_Create_rejects_inverted_service_period()
    {
        Action a = () => InvoiceLine.Create(Guid.CreateVersion7(), null, "Seat", 1, 1000, "USD",
            Now, Now.AddSeconds(-1), Now);
        a.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Invoice_Recalculate_sums_lines_and_computes_due()
    {
        var inv = Invoice.Create(Guid.CreateVersion7(), null, "INV-1", "USD", Now, null,
            InvoiceStatus.Open, Now);
        var l1 = InvoiceLine.Create(inv.Id, null, "A", 2, 1000, "USD", null, null, Now);
        var l2 = InvoiceLine.Create(inv.Id, null, "B", 1, 500, "USD", null, null, Now);
        inv.Recalculate(new[] { l1, l2 }, Now);
        inv.SubtotalAmountMinor.Should().Be(2500);
        inv.TotalAmountMinor.Should().Be(2500);
        inv.AmountDueMinor.Should().Be(2500);
    }

    [Fact]
    public void Invoice_Recalculate_rejects_currency_mismatch()
    {
        var inv = Invoice.Create(Guid.CreateVersion7(), null, "INV-1", "USD", Now, null,
            InvoiceStatus.Open, Now);
        var bad = InvoiceLine.Create(inv.Id, null, "X", 1, 100, "EUR", null, null, Now);
        Action a = () => inv.Recalculate(new[] { bad }, Now);
        a.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Invoice_RegisterPayment_transitions_to_paid_when_due_reaches_zero()
    {
        var inv = Invoice.Create(Guid.CreateVersion7(), null, "INV-1", "USD", Now, null,
            InvoiceStatus.Open, Now);
        var line = InvoiceLine.Create(inv.Id, null, "X", 1, 1000, "USD", null, null, Now);
        inv.Recalculate(new[] { line }, Now);

        inv.RegisterPayment(400, Now);
        inv.Status.Should().Be(InvoiceStatus.Open);
        inv.AmountDueMinor.Should().Be(600);

        inv.RegisterPayment(600, Now);
        inv.Status.Should().Be(InvoiceStatus.Paid);
        inv.PaidAtUtc.Should().NotBeNull();
        inv.AmountDueMinor.Should().Be(0);
    }

    [Fact]
    public void Invoice_AttachProviderInvoice_records_provider_link()
    {
        var inv = Invoice.Create(Guid.CreateVersion7(), null, "INV-1", "USD", Now, null,
            InvoiceStatus.Open, Now);
        inv.AttachProviderInvoice(PaymentProviderType.Stripe, "in_xxx", Now);
        inv.Provider.Should().Be(PaymentProviderType.Stripe);
        inv.ProviderInvoiceId.Should().Be("in_xxx");
    }

    [Fact]
    public void Invoice_Void_rejects_paid()
    {
        var inv = Invoice.Create(Guid.CreateVersion7(), null, "INV-1", "USD", Now, null,
            InvoiceStatus.Open, Now);
        var line = InvoiceLine.Create(inv.Id, null, "X", 1, 100, "USD", null, null, Now);
        inv.Recalculate(new[] { line }, Now);
        inv.RegisterPayment(100, Now);
        Action a = () => inv.Void(Now);
        a.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InvoiceNumber_format_is_padded()
    {
        var n = InvoiceNumber.Format(7);
        n.Should().Be("COM-INV-000007");
        InvoiceNumber.TryParseSequence(n, out var seq).Should().BeTrue();
        seq.Should().Be(7);
    }
}
