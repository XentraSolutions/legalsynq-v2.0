using Billing.Domain.Entities;
using Billing.Domain.Statements;
using Billing.Domain.Tests.Fakes;
using Billing.Domain.Tests.Helpers;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// STAT-B01 — Domain-level tests for the customer statement engine.
/// All tests build the service over the in-memory repository fakes
/// so the assertions cover the pure calculation logic without any
/// EF / HTTP overhead.
/// </summary>
public class CustomerStatementServiceTests
{
    /// <summary>
    /// Constructs the service with a fixed-clock TimeProvider so
    /// the "DaysPastDue" assertions are deterministic. Returns the
    /// repos so individual tests can seed entities directly.
    /// </summary>
    private static (
        CustomerStatementService svc,
        InMemoryInvoiceRepository invoices,
        InMemoryPaymentRepository payments,
        InMemoryCustomerRepository customers,
        DateTime nowUtc) Build(DateTime? clock = null)
    {
        var now = clock ?? new DateTime(2026, 04, 29, 12, 0, 0, DateTimeKind.Utc);
        var time = new FakeTimeProvider(now);

        var customers = new InMemoryCustomerRepository();
        var invoices = new InMemoryInvoiceRepository();
        var payments = new InMemoryPaymentRepository(invoices);
        var renderer = new CustomerStatementHtmlRenderer();
        var svc = new CustomerStatementService(customers, invoices, payments, renderer, time);
        return (svc, invoices, payments, customers, now);
    }

    /// <summary>
    /// Helper for seeding payments tied to a specific tenant + invoice
    /// so the join in the in-memory fake matches the EF behaviour.
    /// </summary>
    private static Payment SeedPayment(
        InMemoryPaymentRepository payments,
        Guid tenantId,
        Guid invoiceId,
        decimal amount,
        DateTime paidAt,
        string status = "Recorded",
        string currency = "USD",
        string method = "Card")
    {
        var p = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoiceId,
            Amount = amount,
            Currency = currency,
            Method = method,
            Status = status,
            PaidAt = paidAt,
            CreatedAt = paidAt,
        };
        payments.AddAsync(p).GetAwaiter().GetResult();
        return p;
    }

    [Fact]
    public async Task ZeroActivity_ReturnsZeroBalances()
    {
        var (svc, _, _, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var doc = await svc.BuildStatementAsync(
            tenant, customer.Id,
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));

        Assert.NotNull(doc);
        Assert.Equal(0m, doc!.OpeningBalance);
        Assert.Equal(0m, doc.TotalInvoiced);
        Assert.Equal(0m, doc.TotalPaid);
        Assert.Equal(0m, doc.TotalAdjustments);
        Assert.Equal(0m, doc.ClosingBalance);
        Assert.Equal(0m, doc.OutstandingBalance);
        Assert.Empty(doc.Transactions);
        Assert.Empty(doc.OutstandingInvoices);
        Assert.Equal("USD", doc.Currency);
    }

    [Fact]
    public async Task OpeningBalance_IncludesPrePeriodInvoicesAndPayments()
    {
        var (svc, invoices, payments, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        // Pre-period: invoice $300 issued, $100 paid -> opening = 200.
        var prior = TestData.SeedInvoice(invoices, tenant, customer.Id, 300m,
            status: InvoiceStatus.Issued, dueDate: new DateTime(2026, 03, 15));
        prior.IssueDate = new DateTime(2026, 03, 01);
        SeedPayment(payments, tenant, prior.Id, 100m, new DateTime(2026, 03, 20));

        var doc = await svc.BuildStatementAsync(tenant, customer.Id,
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));

        Assert.Equal(200m, doc!.OpeningBalance);
        Assert.Equal(0m, doc.TotalInvoiced);
        Assert.Equal(0m, doc.TotalPaid);
        Assert.Equal(200m, doc.ClosingBalance);
    }

    [Fact]
    public async Task PeriodInvoicesAndPayments_DriveTotals_AndClosing()
    {
        var (svc, invoices, payments, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var inv1 = TestData.SeedInvoice(invoices, tenant, customer.Id, 500m,
            status: InvoiceStatus.Issued);
        inv1.IssueDate = new DateTime(2026, 04, 05);
        var inv2 = TestData.SeedInvoice(invoices, tenant, customer.Id, 250m,
            status: InvoiceStatus.PartiallyPaid);
        inv2.IssueDate = new DateTime(2026, 04, 15);
        SeedPayment(payments, tenant, inv2.Id, 100m, new DateTime(2026, 04, 20));

        var doc = await svc.BuildStatementAsync(tenant, customer.Id,
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));

        Assert.Equal(0m, doc!.OpeningBalance);
        Assert.Equal(750m, doc.TotalInvoiced);
        Assert.Equal(100m, doc.TotalPaid);
        Assert.Equal(650m, doc.ClosingBalance);
        Assert.Equal(3, doc.Transactions.Count);
    }

    [Fact]
    public async Task Transactions_OrderedChronologically_InvoicesBeforePaymentsOnSameDay()
    {
        var (svc, invoices, payments, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var sameDay = new DateTime(2026, 04, 10, 9, 0, 0, DateTimeKind.Utc);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued);
        inv.IssueDate = sameDay;
        // Payment occurs *earlier* in wall-clock on the same day,
        // but the engine must still place the invoice row first per the
        // accounting convention (TypePriority = 0 invoice, 1 payment).
        SeedPayment(payments, tenant, inv.Id, 30m, sameDay.AddHours(-2));

        var doc = await svc.BuildStatementAsync(tenant, customer.Id,
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));

        Assert.Equal(2, doc!.Transactions.Count);
        Assert.Equal(CustomerStatementTransactionType.Invoice, doc.Transactions[0].Type);
        Assert.Equal(CustomerStatementTransactionType.Payment, doc.Transactions[1].Type);
        Assert.Equal(100m, doc.Transactions[0].RunningBalance);
        Assert.Equal(70m, doc.Transactions[1].RunningBalance);
    }

    [Fact]
    public async Task OutstandingInvoices_ExcludeFullyPaid_AndExcludeVoided_AndIncludeStaleUnpaid()
    {
        var (svc, invoices, payments, customers, now) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        // Fully paid (excluded).
        var paid = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Paid);
        paid.IssueDate = new DateTime(2026, 02, 01);
        SeedPayment(payments, tenant, paid.Id, 100m, new DateTime(2026, 02, 02));

        // Voided (excluded even with no payments).
        var voided = TestData.SeedInvoice(invoices, tenant, customer.Id, 75m,
            status: InvoiceStatus.Voided);
        voided.IssueDate = new DateTime(2026, 02, 10);

        // Stale unpaid (issued well before period; included).
        var stale = TestData.SeedInvoice(invoices, tenant, customer.Id, 200m,
            status: InvoiceStatus.Overdue, dueDate: new DateTime(2026, 02, 25));
        stale.IssueDate = new DateTime(2026, 02, 05);

        // Partially paid (50 of 80 paid; balance 30).
        var partial = TestData.SeedInvoice(invoices, tenant, customer.Id, 80m,
            status: InvoiceStatus.PartiallyPaid, dueDate: new DateTime(2026, 04, 30));
        partial.IssueDate = new DateTime(2026, 04, 05);
        SeedPayment(payments, tenant, partial.Id, 50m, new DateTime(2026, 04, 06));

        var doc = await svc.BuildStatementAsync(tenant, customer.Id,
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));

        Assert.Equal(2, doc!.OutstandingInvoices.Count);
        Assert.Equal(230m, doc.OutstandingBalance);
        Assert.DoesNotContain(doc.OutstandingInvoices, o => o.InvoiceId == paid.Id);
        Assert.DoesNotContain(doc.OutstandingInvoices, o => o.InvoiceId == voided.Id);

        var staleRow = doc.OutstandingInvoices.Single(o => o.InvoiceId == stale.Id);
        Assert.Equal(200m, staleRow.AmountDue);
        // generation date = 2026-04-29, due 2026-02-25 → 63 days past due
        Assert.Equal((now.Date - stale.DueDate.Date).Days, staleRow.DaysPastDue);
        Assert.True(staleRow.DaysPastDue > 0);

        var partialRow = doc.OutstandingInvoices.Single(o => o.InvoiceId == partial.Id);
        Assert.Equal(30m, partialRow.AmountDue);
    }

    [Fact]
    public async Task DaysPastDue_ZeroForFutureDueDate()
    {
        var (svc, invoices, _, customers, now) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var future = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: now.AddDays(60));
        future.IssueDate = now.AddDays(-1);

        var doc = await svc.BuildStatementAsync(tenant, customer.Id,
            now.Date.AddDays(-30), now.Date);

        var row = doc!.OutstandingInvoices.Single();
        Assert.Equal(0, row.DaysPastDue);
    }

    [Fact]
    public async Task MultiCurrency_ThrowsValidationException()
    {
        var (svc, invoices, _, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var usd = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, currency: "USD");
        usd.IssueDate = new DateTime(2026, 04, 10);
        var eur = TestData.SeedInvoice(invoices, tenant, customer.Id, 50m,
            status: InvoiceStatus.Issued, currency: "EUR");
        eur.IssueDate = new DateTime(2026, 04, 12);

        await Assert.ThrowsAsync<StatementValidationException>(() =>
            svc.BuildStatementAsync(tenant, customer.Id,
                new DateTime(2026, 04, 01), new DateTime(2026, 04, 30)));
    }

    [Fact]
    public async Task CrossTenantCustomer_ReturnsNull()
    {
        var (svc, _, _, customers, _) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenantA);

        // Same id, but probed under tenantB.
        var doc = await svc.BuildStatementAsync(tenantB, customer.Id,
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));

        Assert.Null(doc);
    }

    [Fact]
    public async Task CrossTenantInvoicesAndPayments_AreExcluded()
    {
        var (svc, invoices, payments, customers, _) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var customerA = TestData.SeedCustomer(customers, tenantA);
        // Seed an invoice for the SAME customer id under tenantB. Real
        // production would never share ids, but this guards against an
        // engine that joins solely on CustomerId without re-checking
        // tenant.
        var foreign = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            CustomerId = customerA.Id, // same guid, wrong tenant
            InvoiceNumber = "INV-999",
            IssueDate = new DateTime(2026, 04, 10),
            DueDate = new DateTime(2026, 05, 10),
            Status = InvoiceStatus.Issued,
            Subtotal = 999m,
            TotalAmount = 999m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        invoices.AddAsync(foreign).GetAwaiter().GetResult();
        SeedPayment(payments, tenantB, foreign.Id, 999m, new DateTime(2026, 04, 11));

        var doc = await svc.BuildStatementAsync(tenantA, customerA.Id,
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));

        Assert.NotNull(doc);
        Assert.Equal(0m, doc!.TotalInvoiced);
        Assert.Equal(0m, doc.TotalPaid);
        Assert.Equal(0m, doc.OutstandingBalance);
    }

    [Fact]
    public async Task InvalidDateRange_ThrowsValidationException()
    {
        var (svc, _, _, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        await Assert.ThrowsAsync<StatementValidationException>(() =>
            svc.BuildStatementAsync(tenant, customer.Id,
                new DateTime(2026, 04, 30), new DateTime(2026, 04, 01)));
    }

    [Fact]
    public async Task RangeOver366Days_ThrowsValidationException()
    {
        var (svc, _, _, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        await Assert.ThrowsAsync<StatementValidationException>(() =>
            svc.BuildStatementAsync(tenant, customer.Id,
                new DateTime(2024, 01, 01), new DateTime(2025, 06, 01)));
    }

    [Fact]
    public async Task EmptyCustomerId_ThrowsArgumentException()
    {
        var (svc, _, _, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.BuildStatementAsync(Guid.NewGuid(), Guid.Empty,
                new DateTime(2026, 04, 01), new DateTime(2026, 04, 30)));
    }

    [Fact]
    public async Task RenderHtml_ReturnsNull_WhenCustomerMissing()
    {
        var (svc, _, _, _, _) = Build();
        var html = await svc.RenderHtmlAsync(Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));
        Assert.Null(html);
    }

    [Fact]
    public async Task RenderHtml_EscapesUnsafeCustomerName_AndOmitsScriptTags()
    {
        var (svc, _, _, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        customer.Name = "<script>alert('x')</script>";
        await customers.UpdateAsync(customer);

        var html = await svc.RenderHtmlAsync(tenant, customer.Id,
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));

        Assert.NotNull(html);
        Assert.DoesNotContain("<script>alert", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;alert", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SoftDeletedCustomer_IsTreatedAsMissing()
    {
        var (svc, _, _, customers, _) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        customer.IsDeleted = true;
        await customers.UpdateAsync(customer);

        var doc = await svc.BuildStatementAsync(
            tenant, customer.Id,
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));
        Assert.Null(doc);

        var html = await svc.RenderHtmlAsync(
            tenant, customer.Id,
            new DateTime(2026, 04, 01), new DateTime(2026, 04, 30));
        Assert.Null(html);
    }

    /// <summary>
    /// Minimal TimeProvider stub for deterministic generation
    /// timestamps. Inlined here because the existing test project does
    /// not yet expose a shared FakeTimeProvider.
    /// </summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTime utc) => _now = new DateTimeOffset(utc, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
