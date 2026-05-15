using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;
using TenantBilling.Domain.Tests.Fakes;
using TenantBilling.Domain.Tests.Helpers;
using Xunit;

namespace TenantBilling.Domain.Tests;

public class InvoiceRefundTests
{
    private static (InvoiceService svc, InMemoryInvoiceRepository invoices, InMemoryCustomerRepository customers, InMemoryRefundRepository refunds) Build()
    {
        var invoices = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var refunds = new InMemoryRefundRepository(invoices);
        return (new InvoiceService(invoices, customers, refunds, new InvoiceLifecycleService(), new Fakes.NoTemplateSelectionService(), new InvoiceTemplateStampingService()), invoices, customers, refunds);
    }

    /// <summary>
    /// Seed an invoice in <see cref="InvoiceStatus.Paid"/> with a single
    /// payment that fully covers the invoice total. This mirrors what the
    /// PaymentService would have produced after a successful settlement.
    /// </summary>
    private static Invoice SeedPaidInvoice(
        InMemoryInvoiceRepository invoices,
        Guid tenantId,
        Guid customerId,
        decimal totalAmount,
        string currency = "USD")
    {
        var inv = TestData.SeedInvoice(invoices, tenantId, customerId, totalAmount,
            status: InvoiceStatus.Paid, currency: currency);
        invoices.AttachPayment(inv.Id, new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = inv.Id,
            Amount = totalAmount,
            Currency = currency,
            Method = "card",
            Status = "Succeeded",
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        return inv;
    }

    [Fact]
    public async Task Full_refund_of_paid_invoice_marks_Refunded()
    {
        var (svc, invoices, customers, refunds) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        var result = await svc.RefundAsync(tenant, inv.Id, 100m, currency: null, reason: "Customer return", refundedAt: null);

        Assert.NotNull(result);
        Assert.Equal(100m, result!.Refund.Amount);
        Assert.Equal(InvoiceStatus.Refunded, result.Invoice.Status);

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Refunded, reloaded!.Status);
        Assert.Single(await refunds.GetByInvoiceAsync(inv.Id));
    }

    [Fact]
    public async Task Partial_refund_of_paid_invoice_marks_PartiallyRefunded()
    {
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        var result = await svc.RefundAsync(tenant, inv.Id, 30m, null, null, null);

        Assert.NotNull(result);
        Assert.Equal(InvoiceStatus.PartiallyRefunded, result!.Invoice.Status);

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.PartiallyRefunded, reloaded!.Status);
    }

    [Fact]
    public async Task Sequential_refunds_progress_PartiallyRefunded_then_Refunded()
    {
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        await svc.RefundAsync(tenant, inv.Id, 25m, null, null, null);
        var afterFirst = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.PartiallyRefunded, afterFirst!.Status);

        await svc.RefundAsync(tenant, inv.Id, 25m, null, null, null);
        var afterSecond = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.PartiallyRefunded, afterSecond!.Status);

        await svc.RefundAsync(tenant, inv.Id, 50m, null, null, null);
        var afterThird = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Refunded, afterThird!.Status);
    }

    [Fact]
    public async Task Refund_exceeding_paid_amount_is_rejected_and_status_unchanged()
    {
        var (svc, invoices, customers, refunds) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        await svc.RefundAsync(tenant, inv.Id, 60m, null, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RefundAsync(tenant, inv.Id, 50m, null, null, null));

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.PartiallyRefunded, reloaded!.Status);
        Assert.Single(await refunds.GetByInvoiceAsync(inv.Id));
    }

    [Fact]
    public async Task Refund_exceeding_paid_amount_in_single_call_is_rejected()
    {
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RefundAsync(tenant, inv.Id, 100.01m, null, null, null));

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Paid, reloaded!.Status);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.PartiallyPaid)]
    [InlineData(InvoiceStatus.Overdue)]
    [InlineData(InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.Refunded)]
    public async Task Refund_against_non_refundable_status_is_rejected(string status)
    {
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: status);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RefundAsync(tenant, inv.Id, 10m, null, null, null));
    }

    [Fact]
    public async Task Refund_returns_null_when_invoice_missing()
    {
        var (svc, _, _, _) = Build();
        var result = await svc.RefundAsync(Guid.NewGuid(), Guid.NewGuid(), 10m, null, null, null);
        Assert.Null(result);
    }

    [Fact]
    public async Task Refund_with_zero_amount_is_rejected()
    {
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.RefundAsync(tenant, inv.Id, 0m, null, null, null));
    }

    [Fact]
    public async Task Refund_with_negative_amount_is_rejected()
    {
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.RefundAsync(tenant, inv.Id, -5m, null, null, null));
    }

    [Fact]
    public async Task Refund_with_mismatched_currency_is_rejected()
    {
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m, currency: "USD");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RefundAsync(tenant, inv.Id, 10m, currency: "EUR", reason: null, refundedAt: null));
    }

    [Fact]
    public async Task Refund_currency_defaults_to_invoice_currency()
    {
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m, currency: "EUR");

        var result = await svc.RefundAsync(tenant, inv.Id, 10m, currency: null, reason: null, refundedAt: null);

        Assert.NotNull(result);
        Assert.Equal("EUR", result!.Refund.Currency);
    }

    [Fact]
    public async Task Refund_for_other_tenant_returns_null_no_existence_leak()
    {
        // Per TBS-B02: cross-tenant access must surface as the same generic
        // "not found" response as a truly missing id (null here, mapped to
        // 404 at the API layer). Throwing would leak the fact that the
        // invoice exists under another tenant.
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        var result = await svc.RefundAsync(otherTenant, inv.Id, 10m, null, null, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task Refund_amount_is_rounded_to_two_decimals()
    {
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        var result = await svc.RefundAsync(tenant, inv.Id, 10.555m, null, null, null);

        Assert.NotNull(result);
        Assert.Equal(10.56m, result!.Refund.Amount);
    }

    [Fact]
    public async Task Refund_persists_reason_and_refundedAt()
    {
        var (svc, invoices, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);
        var when = new DateTime(2026, 4, 1, 9, 30, 0, DateTimeKind.Utc);

        var result = await svc.RefundAsync(tenant, inv.Id, 25m, currency: null, reason: "  duplicate charge  ", refundedAt: when);

        Assert.NotNull(result);
        Assert.Equal("duplicate charge", result!.Refund.Reason);
        Assert.Equal(when, result.Refund.RefundedAt);
    }

    [Fact]
    public async Task Cannot_record_payment_after_full_refund()
    {
        var (svc, invoices, customers, _) = Build();
        var payments = new InMemoryPaymentRepository(invoices);
        var paymentSvc = new PaymentService(payments, invoices, new InMemoryUnitOfWork());
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        await svc.RefundAsync(tenant, inv.Id, 100m, null, null, null);

        // TBS-B04: PaymentService now throws typed exceptions deriving from
        // InvalidOperationException (e.g. InvalidInvoicePaymentStateException).
        // Use ThrowsAnyAsync so the assertion matches the derived type too.
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => paymentSvc.CreateAsync(
            tenant, inv.Id, 10m, "USD", "card", "Succeeded", null, DateTime.UtcNow));
    }

    [Fact]
    public async Task Cannot_record_payment_after_partial_refund()
    {
        var (svc, invoices, customers, _) = Build();
        var payments = new InMemoryPaymentRepository(invoices);
        var paymentSvc = new PaymentService(payments, invoices, new InMemoryUnitOfWork());
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedPaidInvoice(invoices, tenant, customer.Id, 100m);

        await svc.RefundAsync(tenant, inv.Id, 25m, null, null, null);

        // TBS-B04: PaymentService now throws typed exceptions deriving from
        // InvalidOperationException (e.g. InvalidInvoicePaymentStateException).
        // Use ThrowsAnyAsync so the assertion matches the derived type too.
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => paymentSvc.CreateAsync(
            tenant, inv.Id, 10m, "USD", "card", "Succeeded", null, DateTime.UtcNow));
    }
}
