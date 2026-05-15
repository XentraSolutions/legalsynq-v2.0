using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using Billing.Domain.Tests.Helpers;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// MS-BILL-WRITE-003 — coverage for <see cref="PaymentService.UpdateNotesAsync"/>.
///
/// These tests validate the metadata-only edit contract:
///   * notes can be set, replaced, or cleared (null/empty/whitespace)
///   * notes are trimmed; oversized inputs reject with InvalidPaymentNotesException
///   * NO financial field (amount, currency, method, paidAt,
///     transactionReference) ever mutates
///   * NO lifecycle field (status, createdAt) ever mutates
///   * NO reversal audit field (reversedAt, reversalReason) ever mutates
///   * notes editable on BOTH Recorded and Voided payments
///   * tenant scoping isolates a probe by another tenant (404 path)
///   * unknown payment id surfaces as PaymentNotFoundException (404 path)
///   * empty-guid argument guards (400 path)
///   * invoice paidSum / status are NEVER recomputed (no balance side-effects)
/// </summary>
public class PaymentNotesUpdateTests
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

    // ---- happy paths ------------------------------------------------

    [Fact]
    public async Task Update_notes_sets_value_on_Recorded_payment_and_preserves_all_other_fields()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 100m,
            transactionReference: "WIRE-1", notes: "old note");

        var updated = await svc.UpdateNotesAsync(tenant, paid.Id, "wire reconciled with PO 9876");

        Assert.Equal("wire reconciled with PO 9876", updated.Notes);

        // Financial fields preserved verbatim.
        Assert.Equal(paid.Amount, updated.Amount);
        Assert.Equal(paid.Currency, updated.Currency);
        Assert.Equal(paid.Method, updated.Method);
        Assert.Equal(paid.TransactionReference, updated.TransactionReference);
        Assert.Equal(paid.PaidAt, updated.PaidAt);
        Assert.Equal(paid.CreatedAt, updated.CreatedAt);
        Assert.Equal(paid.Status, updated.Status);
        Assert.Equal(PaymentService.RecordedStatus, updated.Status);
        Assert.Null(updated.ReversedAt);
        Assert.Null(updated.ReversalReason);

        // Invoice balance/status NOT touched (no recompute path).
        var invReloaded = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Paid, invReloaded!.Status);
    }

    [Fact]
    public async Task Update_notes_trims_whitespace_around_value()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 50m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 50m);

        var updated = await svc.UpdateNotesAsync(tenant, paid.Id, "   trimmed   ");
        Assert.Equal("trimmed", updated.Notes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n  ")]
    public async Task Update_notes_clears_value_for_null_empty_or_whitespace_input(string? input)
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 50m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 50m, notes: "existing");

        var updated = await svc.UpdateNotesAsync(tenant, paid.Id, input);

        Assert.Null(updated.Notes);
    }

    [Fact]
    public async Task Update_notes_at_max_length_succeeds()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 50m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 50m);

        var atLimit = new string('n', PaymentService.MaxNotesLength);
        var updated = await svc.UpdateNotesAsync(tenant, paid.Id, atLimit);

        Assert.Equal(atLimit, updated.Notes);
        Assert.Equal(PaymentService.MaxNotesLength, updated.Notes!.Length);
    }

    [Fact]
    public async Task Update_notes_editable_on_Voided_payment()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 100m, notes: "original");

        var reversed = await svc.ReverseAsync(tenant, paid.Id, "wrong invoice");
        Assert.Equal(PaymentService.VoidedStatus, reversed.Payment.Status);

        var updated = await svc.UpdateNotesAsync(tenant, paid.Id, "after-the-fact clarification");

        // Notes mutated, but reversal audit + status preserved.
        Assert.Equal("after-the-fact clarification", updated.Notes);
        Assert.Equal(PaymentService.VoidedStatus, updated.Status);
        Assert.Equal(reversed.Payment.ReversedAt, updated.ReversedAt);
        Assert.Equal(reversed.Payment.ReversalReason, updated.ReversalReason);
    }

    // ---- 400: validation -------------------------------------------

    [Fact]
    public async Task Update_notes_oversize_throws_InvalidPaymentNotesException()
    {
        var (svc, invoices, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 50m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 50m);

        var oversized = new string('x', PaymentService.MaxNotesLength + 1);
        var ex = await Assert.ThrowsAsync<InvalidPaymentNotesException>(
            () => svc.UpdateNotesAsync(tenant, paid.Id, oversized));

        Assert.Equal(PaymentService.MaxNotesLength, ex.MaxLength);
    }

    [Fact]
    public async Task Update_notes_empty_tenant_id_throws_ArgumentException()
    {
        var (svc, _, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.UpdateNotesAsync(Guid.Empty, Guid.NewGuid(), "x"));
    }

    [Fact]
    public async Task Update_notes_empty_payment_id_throws_ArgumentException()
    {
        var (svc, _, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.UpdateNotesAsync(Guid.NewGuid(), Guid.Empty, "x"));
    }

    // ---- 404: not found / tenant isolation -------------------------

    [Fact]
    public async Task Update_notes_unknown_payment_id_throws_PaymentNotFoundException()
    {
        var (svc, _, _, _) = Build();
        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => svc.UpdateNotesAsync(Guid.NewGuid(), Guid.NewGuid(), "x"));
    }

    [Fact]
    public async Task Update_notes_cross_tenant_probe_surfaces_as_PaymentNotFoundException()
    {
        var (svc, invoices, _, customers) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customerA = TestData.SeedCustomer(customers, tenantA);
        var inv = TestData.SeedInvoice(invoices, tenantA, customerA.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenantA, inv.Id, 100m, notes: "tenant-A note");

        // Tenant B probing tenant A's payment id MUST surface as the
        // same exception as a truly-missing id — no existence leak.
        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => svc.UpdateNotesAsync(tenantB, paid.Id, "leaked"));

        // And tenant A's notes are untouched.
        var still = await svc.GetAsync(tenantA, paid.Id);
        Assert.Equal("tenant-A note", still!.Notes);
    }

    // ---- side-effect proof: no aggregator recompute ----------------

    [Fact]
    public async Task Update_notes_does_not_change_invoice_paidSum_or_status()
    {
        var (svc, invoices, payments, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);
        var paid = await Record(svc, tenant, inv.Id, 60m);

        var preStatus = (await invoices.GetByIdForTenantAsync(tenant, inv.Id))!.Status;
        var preSum = await payments.SumRecordedPaymentsForInvoiceAsync(tenant, inv.Id);

        await svc.UpdateNotesAsync(tenant, paid.Id, "metadata only");

        var postStatus = (await invoices.GetByIdForTenantAsync(tenant, inv.Id))!.Status;
        var postSum = await payments.SumRecordedPaymentsForInvoiceAsync(tenant, inv.Id);

        Assert.Equal(preStatus, postStatus);
        Assert.Equal(preSum, postSum);
    }
}
