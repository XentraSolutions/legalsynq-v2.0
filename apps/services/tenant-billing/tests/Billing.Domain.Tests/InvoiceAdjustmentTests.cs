using Billing.Domain.Entities;
using Billing.Domain.Exceptions;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using Billing.Domain.Tests.Helpers;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// MS-BILL-WRITE-005 — append-only Invoice Adjustment / Credit Memo
/// service tests. Mirrors the InvoiceRefundTests shape:
/// in-memory fakes, deterministic seed helpers, no EF dependency.
/// </summary>
public class InvoiceAdjustmentTests
{
    private static (
        InvoiceAdjustmentService svc,
        InMemoryInvoiceRepository invoices,
        InMemoryCustomerRepository customers,
        InMemoryPaymentRepository payments,
        InMemoryInvoiceAdjustmentRepository adjustments) Build()
    {
        var invoices = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var payments = new InMemoryPaymentRepository(invoices);
        var adjustments = new InMemoryInvoiceAdjustmentRepository(invoices);
        var svc = new InvoiceAdjustmentService(adjustments, invoices, payments);
        return (svc, invoices, customers, payments, adjustments);
    }

    private static Invoice SeedIssued(
        InMemoryInvoiceRepository invoices, Guid tenant, Guid customer,
        decimal total = 100m)
        => TestData.SeedInvoice(invoices, tenant, customer, total, status: InvoiceStatus.Issued);

    private static void SeedPayment(
        InMemoryPaymentRepository payments, InMemoryInvoiceRepository invoices,
        Invoice inv, decimal amount)
    {
        var pay = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = inv.TenantId,
            InvoiceId = inv.Id,
            Amount = amount,
            Currency = inv.Currency,
            Method = "card",
            Status = "Recorded",
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        payments.AddAsync(pay).GetAwaiter().GetResult();
        invoices.AttachPayment(inv.Id, pay);
    }

    // ---- happy paths -----------------------------------------------

    [Fact]
    public async Task Credit_within_remaining_balance_appends_and_returns_effective_totals()
    {
        var (svc, invoices, customers, payments, repo) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 200m);
        SeedPayment(payments, invoices, inv, 50m); // partial: 150 outstanding

        var result = await svc.CreateAsync(
            tenant, inv.Id, "Credit", 100m, "Goodwill credit", referenceNumber: "CM-1", createdBy: null);

        Assert.NotNull(result);
        Assert.Equal("Credit", result!.Adjustment.Type);
        Assert.Equal(100m, result.Adjustment.Amount);
        Assert.Equal("USD", result.Adjustment.Currency);
        Assert.Equal(customer.Id, result.Adjustment.CustomerId);
        Assert.Equal("CM-1", result.Adjustment.ReferenceNumber);
        Assert.Equal(50m, result.PaidSum);
        Assert.Equal(100m, result.AdjustmentSumCredit);
        Assert.Equal(0m, result.AdjustmentSumDebit);
        Assert.Equal(100m, result.EffectiveTotal);        // 200 - 100
        Assert.Equal(50m, result.EffectiveOutstanding);   // 100 - 50
        // Original invoice immutable.
        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(200m, reloaded!.TotalAmount);
        Assert.Equal(InvoiceStatus.Issued, reloaded.Status);
        Assert.Single(await repo.GetByInvoiceAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task Debit_on_Issued_invoice_increases_effective_total()
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);

        var result = await svc.CreateAsync(
            tenant, inv.Id, "Debit", 25m, "Late fee", null, null);

        Assert.NotNull(result);
        Assert.Equal("Debit", result!.Adjustment.Type);
        Assert.Equal(25m, result.AdjustmentSumDebit);
        Assert.Equal(0m, result.AdjustmentSumCredit);
        Assert.Equal(125m, result.EffectiveTotal);
        Assert.Equal(125m, result.EffectiveOutstanding);
    }

    [Fact]
    public async Task Multiple_adjustments_accumulate_across_calls()
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);

        await svc.CreateAsync(tenant, inv.Id, "Debit", 30m, "Late fee", null, null);
        await svc.CreateAsync(tenant, inv.Id, "Credit", 10m, "Goodwill", null, null);
        var result = await svc.CreateAsync(tenant, inv.Id, "Debit", 5m, "Reissue charge", null, null);

        Assert.NotNull(result);
        Assert.Equal(35m, result!.AdjustmentSumDebit);   // 30 + 5
        Assert.Equal(10m, result.AdjustmentSumCredit);
        Assert.Equal(125m, result.EffectiveTotal);       // 100 + 35 - 10
    }

    [Theory]
    [InlineData("credit")]
    [InlineData("Credit")]
    [InlineData("CREDIT")]
    [InlineData(" Debit ")]
    public async Task Type_is_case_insensitive_and_trimmed(string type)
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);

        var result = await svc.CreateAsync(tenant, inv.Id, type, 5m, "ok", null, null);
        Assert.NotNull(result);
        Assert.Contains(result!.Adjustment.Type, new[] { "Credit", "Debit" });
    }

    // ---- over-credit guard -----------------------------------------

    [Fact]
    public async Task Credit_that_exceeds_effective_owed_throws_OverCredit_and_does_not_persist()
    {
        var (svc, invoices, customers, payments, repo) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);
        SeedPayment(payments, invoices, inv, 60m); // 40 outstanding

        var ex = await Assert.ThrowsAsync<OverCreditException>(() =>
            svc.CreateAsync(tenant, inv.Id, "Credit", 50m, "too much", null, null));

        Assert.Equal(100m, ex.EffectiveOwed);
        Assert.Equal(50m, ex.RequestedCredit);
        Assert.Equal(60m, ex.PaidSum);
        Assert.Empty(await repo.GetByInvoiceAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task Credit_exactly_equal_to_outstanding_is_allowed()
    {
        var (svc, invoices, customers, payments, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);
        SeedPayment(payments, invoices, inv, 30m); // 70 outstanding

        var result = await svc.CreateAsync(tenant, inv.Id, "Credit", 70m, "Match outstanding", null, null);

        Assert.NotNull(result);
        Assert.Equal(0m, result!.EffectiveOutstanding);
    }

    // ---- terminal-state guard --------------------------------------

    [Theory]
    [InlineData(InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.Refunded)]
    [InlineData(InvoiceStatus.PartiallyRefunded)]
    public async Task Adjustment_blocked_on_terminal_or_refund_status(string status)
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: status);

        var ex = await Assert.ThrowsAsync<InvoiceNotAdjustableException>(() =>
            svc.CreateAsync(tenant, inv.Id, "Credit", 1m, "anything", null, null));

        Assert.Equal(status, ex.Status);
    }

    // ---- cross-tenant probe ----------------------------------------

    [Fact]
    public async Task Cross_tenant_probe_returns_null_with_no_existence_leak()
    {
        var (svc, invoices, customers, _, _) = Build();
        var ownerTenant = Guid.NewGuid();
        var probeTenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, ownerTenant);
        var inv = SeedIssued(invoices, ownerTenant, customer.Id, 100m);

        var result = await svc.CreateAsync(probeTenant, inv.Id, "Credit", 1m, "x", null, null);
        Assert.Null(result);
    }

    [Fact]
    public async Task Missing_invoice_id_returns_null()
    {
        var (svc, _, _, _, _) = Build();
        var tenant = Guid.NewGuid();
        var result = await svc.CreateAsync(tenant, Guid.NewGuid(), "Credit", 1m, "x", null, null);
        Assert.Null(result);
    }

    // ---- argument guards -------------------------------------------

    [Fact]
    public async Task Empty_tenant_id_throws()
    {
        var (svc, _, _, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Guid.Empty, Guid.NewGuid(), "Credit", 1m, "x", null, null));
    }

    [Fact]
    public async Task Empty_invoice_id_throws()
    {
        var (svc, _, _, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Guid.NewGuid(), Guid.Empty, "Credit", 1m, "x", null, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Writeoff")]
    [InlineData("voided")]
    public async Task Blank_or_unknown_type_throws_InvalidAdjustmentType(string type)
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);

        await Assert.ThrowsAsync<InvalidAdjustmentTypeException>(() =>
            svc.CreateAsync(tenant, inv.Id, type, 1m, "x", null, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_amount_throws(decimal amount)
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(tenant, inv.Id, "Credit", amount, "x", null, null));
    }

    [Fact]
    public async Task Amount_above_decimal_18_2_cap_throws()
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(tenant, inv.Id, "Debit", 100_000_000m, "x", null, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_reason_throws(string reason)
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(tenant, inv.Id, "Credit", 1m, reason, null, null));
    }

    [Fact]
    public async Task Oversize_reason_throws()
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);
        var huge = new string('r', InvoiceAdjustmentService.ReasonMaxLength + 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(tenant, inv.Id, "Credit", 1m, huge, null, null));
    }

    [Fact]
    public async Task Oversize_reference_number_throws()
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = SeedIssued(invoices, tenant, customer.Id, 100m);
        var huge = new string('x', InvoiceAdjustmentService.ReferenceNumberMaxLength + 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(tenant, inv.Id, "Credit", 1m, "ok", huge, null));
    }

    [Fact]
    public async Task Currency_is_inherited_from_parent_invoice()
    {
        var (svc, invoices, customers, _, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, currency: "EUR");

        var result = await svc.CreateAsync(tenant, inv.Id, "Credit", 5m, "ok", null, null);
        Assert.NotNull(result);
        Assert.Equal("EUR", result!.Adjustment.Currency);
    }
}
