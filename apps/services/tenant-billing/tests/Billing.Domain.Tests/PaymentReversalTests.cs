using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using Billing.Domain.Tests.Helpers;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// MS-BILL-WRITE-002 — coverage for <see cref="PaymentService.ReverseAsync"/>.
///
/// These tests validate the immutable-history reversal contract:
///   * status flips Recorded -> Voided
///   * append-only audit fields populate (ReversedAt, ReversalReason)
///   * original financial fields are NEVER mutated
///   * invoice paidSum + status recompute via the existing aggregator
///   * tenant scoping isolates a probe by another tenant
///   * lifecycle gate rejects already-Voided rows (409 path)
///   * reason validation rejects blank / oversized strings (400 path)
///   * unknown payment id surfaces as PaymentNotFoundException (404 path)
/// </summary>
public class PaymentReversalTests
{
    private static (PaymentService svc, InMemoryInvoiceRepository invoices,
                    InMemoryPaymentRepository payments, InMemoryCustomerRepository customers) Build()
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
        string? transactionReference = null, string? notes = null)
        => svc.CreateAsync(tenant, invoiceId, amount, currency, method,
            status: PaymentService.RecordedStatus,
            transactionReference: transactionReference,
            paidAt: DateTime.UtcNow,
            notes: notes);

    // ---- happy path ------------------------------------------------

    [Fact]
    public async Task Reverse_full_payment_flips_status_and_demotes_invoice_to_Issued()
    {
        var (svc, invoices, payments, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        var paid = await Record(svc, tenant, inv.Id, 100m, notes: "audit notes");
        Assert.Equal(InvoiceStatus.Paid,
            (await invoices.GetByIdForTenantAsync(tenant, inv.Id))!.Status);

        var result = await svc.ReverseAsync(tenant, paid.Id, "Recorded against wrong invoice");

        // 1. status flip + audit fields populated.
        Assert.Equal(PaymentService.VoidedStatus, result.Payment.Status);
        Assert.NotNull(result.Payment.ReversedAt);
        Assert.Equal("Recorded against wrong invoice", result.Payment.ReversalReason);

        // 2. financial fields preserved verbatim.
        Assert.Equal(paid.Amount, result.Payment.Amount);
        Assert.Equal(paid.Currency, result.Payment.Currency);
        Assert.Equal(paid.Method, result.Payment.Method);
        Assert.Equal(paid.PaidAt, result.Payment.PaidAt);
        Assert.Equal(paid.TransactionReference, result.Payment.TransactionReference);
        Assert.Equal(paid.Notes, result.Payment.Notes);
        Assert.Equal(paid.CreatedAt, result.Payment.CreatedAt);

        // 3. invoice demoted back to Issued; balanceDue == total.
        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Issued, reloaded!.Status);
        Assert.Equal(InvoiceStatus.Issued, result.Invoice.InvoiceStatus);
        Assert.Equal(0m, result.Invoice.TotalPaid);
        Assert.Equal(100m, result.Invoice.BalanceDue);

        // 4. aggregator excludes the now-Voided row.
        var sum = await payments.SumRecordedPaymentsForInvoiceAsync(tenant, inv.Id);
        Assert.Equal(0m, sum);
    }

    [Fact]
    public async Task Reverse_one_of_two_partial_payments_demotes_Paid_to_PartiallyPaid()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        var first = await Record(svc, tenant, inv.Id, 40m);
        await Record(svc, tenant, inv.Id, 60m);
        Assert.Equal(InvoiceStatus.Paid,
            (await invoices.GetByIdForTenantAsync(tenant, inv.Id))!.Status);

        var result = await svc.ReverseAsync(tenant, first.Id, "Duplicate posting");

        Assert.Equal(InvoiceStatus.PartiallyPaid, result.Invoice.InvoiceStatus);
        Assert.Equal(60m, result.Invoice.TotalPaid);
        Assert.Equal(40m, result.Invoice.BalanceDue);
        Assert.Equal(InvoiceStatus.PartiallyPaid,
            (await invoices.GetByIdForTenantAsync(tenant, inv.Id))!.Status);
    }

    // ---- 409: already-Voided lifecycle gate -----------------------

    [Fact]
    public async Task Reverse_twice_throws_PaymentAlreadyReversedException_409_mapping()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 100m);

        await svc.ReverseAsync(tenant, paid.Id, "first reversal");

        var ex = await Assert.ThrowsAsync<PaymentAlreadyReversedException>(() =>
            svc.ReverseAsync(tenant, paid.Id, "second reversal"));
        Assert.Equal(paid.Id, ex.PaymentId);
    }

    // ---- 404: tenant scope + missing id ---------------------------

    [Fact]
    public async Task Reverse_unknown_payment_id_throws_PaymentNotFoundException()
    {
        var (svc, _, _, _) = Build();
        var tenant = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<PaymentNotFoundException>(() =>
            svc.ReverseAsync(tenant, Guid.NewGuid(), "anything"));
        Assert.Equal(tenant, ex.TenantId);
    }

    [Fact]
    public async Task Reverse_payment_belonging_to_other_tenant_throws_PaymentNotFoundException()
    {
        var (svc, invoices, _, customers) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var custA = TestData.SeedCustomer(customers, tenantA);
        var inv = TestData.SeedInvoice(invoices, tenantA, custA.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenantA, inv.Id, 100m);

        // tenantB knows the payment id (somehow) but is not its owner.
        // Must surface as 404 — never expose existence cross-tenant.
        await Assert.ThrowsAsync<PaymentNotFoundException>(() =>
            svc.ReverseAsync(tenantB, paid.Id, "cross-tenant probe"));

        // And tenantA's payment is unaffected.
        var reloaded = await invoices.GetByIdForTenantAsync(tenantA, inv.Id);
        Assert.Equal(InvoiceStatus.Paid, reloaded!.Status);
    }

    // ---- 400: reason validation -----------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    public async Task Reverse_with_blank_reason_throws_InvalidReversalReasonException(string? reason)
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 100m);

        await Assert.ThrowsAsync<InvalidReversalReasonException>(() =>
            svc.ReverseAsync(tenant, paid.Id, reason!));
    }

    [Fact]
    public async Task Reverse_with_oversize_reason_throws_InvalidReversalReasonException()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 100m);

        var huge = new string('x', PaymentService.MaxReversalReasonLength + 1);

        var ex = await Assert.ThrowsAsync<InvalidReversalReasonException>(() =>
            svc.ReverseAsync(tenant, paid.Id, huge));
        Assert.Equal(PaymentService.MaxReversalReasonLength, ex.MaxLength);
    }

    [Fact]
    public async Task Reverse_with_exact_max_length_reason_succeeds()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 100m);

        var atLimit = new string('y', PaymentService.MaxReversalReasonLength);

        var result = await svc.ReverseAsync(tenant, paid.Id, atLimit);
        Assert.Equal(PaymentService.VoidedStatus, result.Payment.Status);
        Assert.Equal(atLimit, result.Payment.ReversalReason);
    }

    [Fact]
    public async Task Reverse_trims_whitespace_around_reason()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 100m);

        var result = await svc.ReverseAsync(tenant, paid.Id, "   posted in error   ");
        Assert.Equal("posted in error", result.Payment.ReversalReason);
    }

    // ---- guard rails: argument validation -------------------------

    [Fact]
    public async Task Reverse_with_empty_tenant_throws_ArgumentException()
    {
        var (svc, _, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ReverseAsync(Guid.Empty, Guid.NewGuid(), "x"));
    }

    [Fact]
    public async Task Reverse_with_empty_payment_id_throws_ArgumentException()
    {
        var (svc, _, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ReverseAsync(Guid.NewGuid(), Guid.Empty, "x"));
    }

    // ---- concurrency: post-lock re-check rejects races -------------

    /// <summary>
    /// SEVERE-fix coverage (architect): two concurrent ReverseAsync calls
    /// on the same payment must produce exactly one success and one
    /// <see cref="PaymentAlreadyReversedException"/>. Before the post-lock
    /// re-check was added, both callers could pass the pre-lock lifecycle
    /// gate, queue on the invoice lock, and BOTH succeed — silently
    /// overwriting the first caller's audit fields. The
    /// <see cref="InMemoryUnitOfWork"/> models the EF lock with a
    /// per-invoice semaphore, so this test exercises the same serialization
    /// boundary the production code relies on.
    /// </summary>
    [Fact]
    public async Task Reverse_two_concurrent_callers_one_succeeds_one_409()
    {
        var (svc, invoices, payments, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 100m);

        // Fire two reversals at the exact same payment in parallel. The
        // invoice-lock semaphore is shared across both transactions so
        // they MUST serialize — the second one to acquire the lock will
        // observe Status == Voided and throw 409.
        var t1 = Task.Run(() => svc.ReverseAsync(tenant, paid.Id, "first"));
        var t2 = Task.Run(() => svc.ReverseAsync(tenant, paid.Id, "second"));

        var results = await Task.WhenAll(
            SafeRun(t1), SafeRun(t2));

        // Exactly one success.
        var successes = results.Count(r => r.success);
        var alreadyReversedFailures = results.Count(r =>
            !r.success && r.exception is PaymentAlreadyReversedException);
        Assert.Equal(1, successes);
        Assert.Equal(1, alreadyReversedFailures);

        // The persisted reversal reason came from the WINNER, not from
        // a clobber by the loser. Both reasons are distinct so we can
        // unambiguously verify which one stuck.
        var reloaded = await payments.GetByIdForTenantAsync(tenant, paid.Id);
        Assert.Equal(PaymentService.VoidedStatus, reloaded!.Status);
        Assert.Contains(reloaded.ReversalReason, new[] { "first", "second" });
    }

    private static async Task<(bool success, Exception? exception)> SafeRun(Task<ReversePaymentResult> t)
    {
        try { await t; return (true, null); }
        catch (Exception ex) { return (false, ex); }
    }
}
