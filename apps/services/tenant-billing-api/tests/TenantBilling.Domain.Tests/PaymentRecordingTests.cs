using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;
using TenantBilling.Domain.Tests.Fakes;
using TenantBilling.Domain.Tests.Helpers;
using Xunit;

namespace TenantBilling.Domain.Tests;

/// <summary>
/// TBS-B04 hardening tests: validates the new typed exceptions, money
/// rounding/normalization, server-controlled payment status, notes trimming,
/// payment summary aggregation, and paged listing.
/// </summary>
public class PaymentRecordingTests
{
    private static (PaymentService svc, InMemoryInvoiceRepository invoices, InMemoryPaymentRepository payments, InMemoryCustomerRepository customers) Build()
    {
        var invoices = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var payments = new InMemoryPaymentRepository(invoices);
        var uow = new InMemoryUnitOfWork();
        return (new PaymentService(payments, invoices, uow), invoices, payments, customers);
    }

    private static Task<Payment> Record(
        PaymentService svc, Guid tenant, Guid invoiceId, decimal amount,
        string currency = "USD", string method = "card",
        string? transactionReference = null, string? notes = null,
        DateTime? paidAt = null)
        => svc.CreateAsync(tenant, invoiceId, amount, currency, method,
            status: PaymentService.RecordedStatus,
            transactionReference: transactionReference,
            paidAt: paidAt ?? DateTime.UtcNow,
            notes: notes);

    [Fact]
    public async Task Default_status_is_Recorded_when_status_omitted()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        // Pass empty string to mimic a caller (controller) that did not
        // override the lifecycle-controlled status.
        var p = await svc.CreateAsync(tenant, inv.Id, 25m, "USD", "card",
            status: string.Empty, transactionReference: null, paidAt: null);

        Assert.Equal(PaymentService.RecordedStatus, p.Status);
    }

    [Fact]
    public async Task Negative_amount_throws_InvalidPaymentAmountException()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        var ex = await Assert.ThrowsAsync<InvalidPaymentAmountException>(() =>
            Record(svc, tenant, inv.Id, -10m));
        Assert.Equal(-10m, ex.Amount);
    }

    [Fact]
    public async Task Zero_amount_throws_InvalidPaymentAmountException()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await Assert.ThrowsAsync<InvalidPaymentAmountException>(() =>
            Record(svc, tenant, inv.Id, 0m));
    }

    [Fact]
    public async Task Unknown_invoice_throws_InvoiceNotFoundException_404_mapping()
    {
        var (svc, _, _, _) = Build();
        var tenant = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvoiceNotFoundException>(() =>
            Record(svc, tenant, Guid.NewGuid(), 10m));
        Assert.Equal(tenant, ex.TenantId);
        // Sanity: typed exception still derives from InvalidOperationException
        // for back-compat with older test patterns.
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public async Task Cross_tenant_invoice_throws_InvoiceNotFoundException()
    {
        var (svc, invoices, _, customers) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var custA = TestData.SeedCustomer(customers, tenantA);
        var inv = TestData.SeedInvoice(invoices, tenantA, custA.Id, 100m, status: InvoiceStatus.Issued);

        await Assert.ThrowsAsync<InvoiceNotFoundException>(() =>
            Record(svc, tenantB, inv.Id, 10m));
    }

    [Fact]
    public async Task Currency_mismatch_throws_typed_CurrencyMismatchException()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued, currency: "USD");

        var ex = await Assert.ThrowsAsync<CurrencyMismatchException>(() =>
            Record(svc, tenant, inv.Id, 10m, currency: "EUR"));
        Assert.Equal("EUR", ex.PaymentCurrency);
        Assert.Equal("USD", ex.InvoiceCurrency);
    }

    [Fact]
    public async Task Currency_normalized_to_uppercase_before_comparison()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued, currency: "USD");

        var p = await Record(svc, tenant, inv.Id, 10m, currency: "usd");
        Assert.Equal("USD", p.Currency);
    }

    [Fact]
    public async Task Overpayment_throws_typed_OverpaymentException_with_remaining()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await Record(svc, tenant, inv.Id, 60m);
        var ex = await Assert.ThrowsAsync<OverpaymentException>(() =>
            Record(svc, tenant, inv.Id, 50m));
        Assert.Equal(50m, ex.AttemptedAmount);
        Assert.Equal(40m, ex.RemainingBalance);
    }

    [Fact]
    public async Task Voided_invoice_throws_typed_InvalidInvoicePaymentStateException()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Voided);

        var ex = await Assert.ThrowsAsync<InvalidInvoicePaymentStateException>(() =>
            Record(svc, tenant, inv.Id, 10m));
        Assert.Equal(InvoiceStatus.Voided, ex.CurrentStatus);
    }

    [Fact]
    public async Task Amount_rounded_to_two_decimals_away_from_zero()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        var p = await Record(svc, tenant, inv.Id, 10.005m);
        // 10.005 rounded away-from-zero to 2dp = 10.01
        Assert.Equal(10.01m, p.Amount);
    }

    [Fact]
    public async Task Notes_trimmed_and_blank_normalized_to_null()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        var withNotes = await Record(svc, tenant, inv.Id, 10m, notes: "   wire from AP   ");
        Assert.Equal("wire from AP", withNotes.Notes);

        var blankNotes = await Record(svc, tenant, inv.Id, 10m, notes: "   ");
        Assert.Null(blankNotes.Notes);
    }

    [Fact]
    public async Task Partial_payment_on_past_due_invoice_keeps_status_Overdue()
    {
        // TBS-B05 regression: tighter ComputeStatus must keep an
        // already-overdue invoice in Overdue when only a partial
        // payment lands. Previously it would silently roll back to
        // PartiallyPaid, hiding collection risk.
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Overdue,
            dueDate: DateTime.UtcNow.AddDays(-5));

        await Record(svc, tenant, inv.Id, 25m);

        var fresh = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Overdue, fresh!.Status);
    }

    [Fact]
    public async Task Full_payment_on_past_due_invoice_moves_to_Paid()
    {
        // Companion to the regression above — full settlement always
        // wins, regardless of due date.
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Overdue,
            dueDate: DateTime.UtcNow.AddDays(-5));

        await Record(svc, tenant, inv.Id, 100m);

        var fresh = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Paid, fresh!.Status);
    }

    [Fact]
    public async Task GetInvoicePaymentSummary_reflects_recorded_payments_and_balance()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await Record(svc, tenant, inv.Id, 30m);
        await Record(svc, tenant, inv.Id, 20m);

        var summary = await svc.GetInvoicePaymentSummaryAsync(tenant, inv.Id);
        Assert.NotNull(summary);
        Assert.Equal(100m, summary!.InvoiceTotal);
        Assert.Equal(50m, summary.TotalPaid);
        Assert.Equal(50m, summary.BalanceDue);
        Assert.Equal(InvoiceStatus.PartiallyPaid, summary.InvoiceStatus);
        Assert.Equal("USD", summary.Currency);
    }

    [Fact]
    public async Task GetInvoicePaymentSummary_for_unknown_invoice_returns_null()
    {
        var (svc, _, _, _) = Build();
        var summary = await svc.GetInvoicePaymentSummaryAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(summary);
    }

    [Fact]
    public async Task GetInvoicePaymentSummary_for_cross_tenant_invoice_returns_null()
    {
        var (svc, invoices, _, customers) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var custA = TestData.SeedCustomer(customers, tenantA);
        var inv = TestData.SeedInvoice(invoices, tenantA, custA.Id, 100m, status: InvoiceStatus.Issued);

        var summary = await svc.GetInvoicePaymentSummaryAsync(tenantB, inv.Id);
        Assert.Null(summary);
    }

    [Fact]
    public async Task GetByInvoice_returns_null_for_unknown_invoice()
    {
        var (svc, _, _, _) = Build();
        var result = await svc.GetByInvoiceAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByInvoice_returns_payments_ordered_newest_first()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        var older = DateTime.UtcNow.AddHours(-2);
        var newer = DateTime.UtcNow.AddMinutes(-5);
        await Record(svc, tenant, inv.Id, 20m, paidAt: older);
        await Record(svc, tenant, inv.Id, 30m, paidAt: newer);

        var list = await svc.GetByInvoiceAsync(tenant, inv.Id);
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Equal(30m, list[0].Amount);
        Assert.Equal(20m, list[1].Amount);
    }

    [Fact]
    public async Task ListPaged_clamps_pageSize_and_returns_correct_totals()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 1000m, status: InvoiceStatus.Issued);

        for (var i = 0; i < 7; i++)
            await Record(svc, tenant, inv.Id, 10m, paidAt: DateTime.UtcNow.AddMinutes(-i));

        var page1 = await svc.ListPagedAsync(tenant, null, null, null, null, null, page: 1, pageSize: 3);
        Assert.Equal(7, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);

        var page2 = await svc.ListPagedAsync(tenant, null, null, null, null, null, page: 2, pageSize: 3);
        Assert.Equal(3, page2.Items.Count);

        var page3 = await svc.ListPagedAsync(tenant, null, null, null, null, null, page: 3, pageSize: 3);
        Assert.Single(page3.Items);

        // pageSize <= 0 falls back to default
        var defaulted = await svc.ListPagedAsync(tenant, null, null, null, null, null, page: 1, pageSize: 0);
        Assert.Equal(7, defaulted.Items.Count);

        // pageSize > 100 clamps to 100
        var clamped = await svc.ListPagedAsync(tenant, null, null, null, null, null, page: 1, pageSize: 5000);
        Assert.Equal(7, clamped.Items.Count);
    }

    [Fact]
    public async Task ListPaged_filters_by_invoiceId_method_and_paid_date_range()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var invA = TestData.SeedInvoice(invoices, tenant, customer.Id, 500m, status: InvoiceStatus.Issued);
        var invB = TestData.SeedInvoice(invoices, tenant, customer.Id, 500m, status: InvoiceStatus.Issued);

        var t0 = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        await Record(svc, tenant, invA.Id, 10m, method: "card", paidAt: t0);
        await Record(svc, tenant, invA.Id, 20m, method: "wire", paidAt: t0.AddDays(2));
        await Record(svc, tenant, invB.Id, 30m, method: "card", paidAt: t0.AddDays(4));

        var byInvoice = await svc.ListPagedAsync(tenant, invA.Id, null, null, null, null, 1, 25);
        Assert.Equal(2, byInvoice.TotalCount);

        var byMethod = await svc.ListPagedAsync(tenant, null, null, "card", null, null, 1, 25);
        Assert.Equal(2, byMethod.TotalCount);

        var byRange = await svc.ListPagedAsync(tenant, null, null, null, t0.AddDays(1), t0.AddDays(3), 1, 25);
        Assert.Single(byRange.Items);
        Assert.Equal(20m, byRange.Items[0].Amount);
    }

    [Fact]
    public async Task ListPaged_is_tenant_scoped()
    {
        var (svc, invoices, _, customers) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var custA = TestData.SeedCustomer(customers, tenantA);
        var custB = TestData.SeedCustomer(customers, tenantB);
        var invA = TestData.SeedInvoice(invoices, tenantA, custA.Id, 100m, status: InvoiceStatus.Issued);
        var invB = TestData.SeedInvoice(invoices, tenantB, custB.Id, 100m, status: InvoiceStatus.Issued);

        await Record(svc, tenantA, invA.Id, 10m);
        await Record(svc, tenantB, invB.Id, 10m);

        var listA = await svc.ListPagedAsync(tenantA, null, null, null, null, null, 1, 25);
        Assert.Single(listA.Items);
        Assert.Equal(invA.Id, listA.Items[0].InvoiceId);
    }

    [Fact]
    public async Task Notes_longer_than_max_throws_ArgumentException()
    {
        // Defense-in-depth: the API DTO already caps notes at 2000 via
        // [StringLength], but a non-HTTP caller composing PaymentService
        // directly would otherwise bypass that and only fail at the EF
        // column-width boundary with an opaque DbUpdateException. The
        // service-level guard must reject it cleanly.
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        var tooLong = new string('x', PaymentService.MaxNotesLength + 1);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            Record(svc, tenant, inv.Id, 10m, notes: tooLong));
        Assert.Contains("at most", ex.Message);
        Assert.Equal("notes", ex.ParamName);
    }

    [Fact]
    public async Task Notes_at_exact_max_length_is_accepted()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        var atMax = new string('y', PaymentService.MaxNotesLength);

        var p = await Record(svc, tenant, inv.Id, 10m, notes: atMax);

        Assert.Equal(PaymentService.MaxNotesLength, p.Notes!.Length);
    }

    [Fact]
    public async Task GetByInvoiceAsync_returns_null_for_other_tenants_invoice()
    {
        // Tenant isolation on the new GET /invoices/{id}/payments surface:
        // an invoice id known to belong to tenant A must look exactly like a
        // missing invoice id when queried by tenant B (the controller turns
        // null into 404).
        var (svc, invoices, _, customers) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var custA = TestData.SeedCustomer(customers, tenantA);
        var invA = TestData.SeedInvoice(invoices, tenantA, custA.Id, 100m, status: InvoiceStatus.Issued);
        await Record(svc, tenantA, invA.Id, 25m);

        var crossTenant = await svc.GetByInvoiceAsync(tenantB, invA.Id);
        Assert.Null(crossTenant);

        // Sanity: the same call from the owning tenant returns the payment.
        var owned = await svc.GetByInvoiceAsync(tenantA, invA.Id);
        Assert.NotNull(owned);
        Assert.Single(owned!);
    }

    [Fact]
    public async Task GetInvoicePaymentSummaryAsync_returns_null_for_other_tenants_invoice()
    {
        var (svc, invoices, _, customers) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var custA = TestData.SeedCustomer(customers, tenantA);
        var invA = TestData.SeedInvoice(invoices, tenantA, custA.Id, 100m, status: InvoiceStatus.Issued);
        await Record(svc, tenantA, invA.Id, 25m);

        var crossTenant = await svc.GetInvoicePaymentSummaryAsync(tenantB, invA.Id);
        Assert.Null(crossTenant);

        var owned = await svc.GetInvoicePaymentSummaryAsync(tenantA, invA.Id);
        Assert.NotNull(owned);
        Assert.Equal(25m, owned!.TotalPaid);
    }
}
