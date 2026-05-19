using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using Billing.Domain.Tests.Helpers;
using Xunit;

namespace Billing.Domain.Tests;

public class PaymentServiceTests
{
    private static (PaymentService svc, InMemoryInvoiceRepository invoices, InMemoryPaymentRepository payments, InMemoryCustomerRepository customers) Build()
    {
        var invoices = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var payments = new InMemoryPaymentRepository(invoices);
        var uow = new InMemoryUnitOfWork();
        return (new PaymentService(payments, invoices, uow), invoices, payments, customers);
    }

    private static Task<Payment> Pay(PaymentService svc, Guid tenant, Guid invoiceId, decimal amount, string currency = "USD")
        => svc.CreateAsync(tenant, invoiceId, amount, currency, method: "card", status: "Succeeded",
            transactionReference: null, paidAt: DateTime.UtcNow);

    [Fact]
    public async Task Partial_payment_marks_invoice_PartiallyPaid()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await Pay(svc, tenant, inv.Id, 40m);

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.PartiallyPaid, reloaded!.Status);
    }

    [Fact]
    public async Task Full_payment_marks_invoice_Paid()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await Pay(svc, tenant, inv.Id, 100m);

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Paid, reloaded!.Status);
    }

    [Fact]
    public async Task Sequential_payments_progress_PartiallyPaid_then_Paid()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await Pay(svc, tenant, inv.Id, 30m);
        var afterFirst = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.PartiallyPaid, afterFirst!.Status);

        await Pay(svc, tenant, inv.Id, 70m);
        var afterSecond = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Paid, afterSecond!.Status);
    }

    [Fact]
    public async Task Payment_against_voided_invoice_is_rejected()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Voided);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Pay(svc, tenant, inv.Id, 10m));
    }

    [Fact]
    public async Task Payment_against_draft_invoice_is_rejected()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Draft);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Pay(svc, tenant, inv.Id, 10m));
    }

    [Fact]
    public async Task Payment_against_paid_invoice_is_rejected()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Paid);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Pay(svc, tenant, inv.Id, 10m));
    }

    [Fact]
    public async Task Overpayment_is_rejected_and_invoice_status_unchanged()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await Pay(svc, tenant, inv.Id, 60m);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Pay(svc, tenant, inv.Id, 50m));

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.PartiallyPaid, reloaded!.Status);
        Assert.Single(reloaded.Payments);
    }

    [Fact]
    public async Task Currency_mismatch_is_rejected()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued, currency: "USD");

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Pay(svc, tenant, inv.Id, 50m, currency: "EUR"));
    }

    [Fact]
    public async Task Cross_tenant_payment_is_rejected()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var otherTenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Pay(svc, otherTenant, inv.Id, 10m));
    }

    [Fact]
    public async Task Partial_payment_to_overdue_invoice_keeps_Overdue()
    {
        // TBS-B05: tighter ComputeStatus — past-due dominates partial,
        // so an overdue invoice must remain Overdue after a partial
        // payment instead of silently rolling back to PartiallyPaid.
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(
            invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Overdue,
            dueDate: DateTime.UtcNow.AddDays(-5));

        await Pay(svc, tenant, inv.Id, 40m);

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Overdue, reloaded!.Status);
    }

    [Fact]
    public async Task Full_payment_to_overdue_invoice_marks_Paid()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(
            invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Overdue,
            dueDate: DateTime.UtcNow.AddDays(-5));

        await Pay(svc, tenant, inv.Id, 100m);

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Paid, reloaded!.Status);
    }

    [Fact]
    public async Task Concurrent_payments_cannot_collectively_overpay_invoice()
    {
        // Ten concurrent payment attempts of $30 each against a $100 invoice.
        // Without per-invoice locking the overpayment guard would race —
        // every attempt would read existingPaid = 0 and pass the check, and
        // we'd end up with 10 successful payments totalling $300. The lock
        // acquired by BeginTransactionAsync + LockInvoiceForUpdateAsync must
        // serialize the attempts so the running paid sum is observed
        // correctly and only the first three succeed (3 * $30 = $90 ≤ $100).
        var (svc, invoices, payments, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        const int attemptCount = 10;
        const decimal perAttempt = 30m;

        var tasks = Enumerable.Range(0, attemptCount)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    await Pay(svc, tenant, inv.Id, perAttempt);
                    return (success: true, error: (string?)null);
                }
                catch (InvalidOperationException ex)
                {
                    return (success: false, error: (string?)ex.Message);
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.success);
        var failureCount = results.Count(r => !r.success);

        // Exactly three attempts should win the race (3 * $30 = $90); the
        // remaining seven must be rejected as overpayments.
        Assert.Equal(3, successCount);
        Assert.Equal(7, failureCount);

        var allPayments = await payments.GetAllForTenantAsync(tenant);
        Assert.Equal(3, allPayments.Count);
        Assert.Equal(90m, allPayments.Sum(p => p.Amount));

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.Payments.Sum(p => p.Amount) <= reloaded.TotalAmount,
            "Concurrent payments must never push the paid sum above the invoice total.");
        Assert.Equal(InvoiceStatus.PartiallyPaid, reloaded.Status);
    }

    [Fact]
    public async Task Duplicate_TransactionReference_is_rejected_and_invoice_unchanged()
    {
        // Models a webhook being delivered twice: the first call records the
        // payment and flips the invoice to PartiallyPaid; the second call
        // (same Stripe charge id) must be rejected so the invoice's paid
        // total and status are unchanged and only one Payment row exists.
        var (svc, invoices, payments, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);
        const string chargeId = "ch_stripe_123";

        await svc.CreateAsync(tenant, inv.Id, 40m, "USD", "card", "Succeeded",
            transactionReference: chargeId, paidAt: DateTime.UtcNow);

        var dup = await Assert.ThrowsAsync<DuplicatePaymentReferenceException>(() =>
            svc.CreateAsync(tenant, inv.Id, 40m, "USD", "card", "Succeeded",
                transactionReference: chargeId, paidAt: DateTime.UtcNow));
        Assert.Equal(tenant, dup.TenantId);
        Assert.Equal(chargeId, dup.TransactionReference);

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.PartiallyPaid, reloaded!.Status);
        Assert.Single(reloaded.Payments);
        Assert.Equal(40m, reloaded.Payments.Sum(p => p.Amount));
    }

    [Fact]
    public async Task Duplicate_TransactionReference_check_trims_and_treats_whitespace_padding_as_same()
    {
        // The service trims TransactionReference before persisting, so the
        // dedupe check must apply to the trimmed value too — otherwise a
        // retried webhook with stray whitespace would slip through.
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await svc.CreateAsync(tenant, inv.Id, 25m, "USD", "card", "Succeeded",
            transactionReference: "ch_abc", paidAt: DateTime.UtcNow);

        await Assert.ThrowsAsync<DuplicatePaymentReferenceException>(() =>
            svc.CreateAsync(tenant, inv.Id, 25m, "USD", "card", "Succeeded",
                transactionReference: "  ch_abc  ", paidAt: DateTime.UtcNow));
    }

    [Fact]
    public async Task Same_TransactionReference_is_allowed_for_different_tenants()
    {
        // The uniqueness scope is (TenantId, TransactionReference). Two
        // different tenants happening to have the same provider id (very
        // unlikely in practice but possible across providers/sandboxes)
        // must not collide.
        var (svc, invoices, _, customers) = Build();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var custA = TestData.SeedCustomer(customers, tenantA);
        var custB = TestData.SeedCustomer(customers, tenantB);
        var invA = TestData.SeedInvoice(invoices, tenantA, custA.Id, 100m, status: InvoiceStatus.Issued);
        var invB = TestData.SeedInvoice(invoices, tenantB, custB.Id, 100m, status: InvoiceStatus.Issued);

        await svc.CreateAsync(tenantA, invA.Id, 10m, "USD", "card", "Succeeded",
            transactionReference: "shared_ref", paidAt: DateTime.UtcNow);
        await svc.CreateAsync(tenantB, invB.Id, 10m, "USD", "card", "Succeeded",
            transactionReference: "shared_ref", paidAt: DateTime.UtcNow);

        Assert.Single((await invoices.GetByIdForTenantAsync(tenantA, invA.Id))!.Payments);
        Assert.Single((await invoices.GetByIdForTenantAsync(tenantB, invB.Id))!.Payments);
    }

    [Fact]
    public async Task Multiple_payments_without_TransactionReference_are_allowed()
    {
        // Null TransactionReference must not be deduplicated — it is the
        // common case for manually recorded payments and they are not
        // idempotent on a provider id.
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await Pay(svc, tenant, inv.Id, 30m);
        await Pay(svc, tenant, inv.Id, 30m);

        var reloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(2, reloaded!.Payments.Count);
        Assert.Equal(60m, reloaded.Payments.Sum(p => p.Amount));
    }
}
