using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using Billing.Domain.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// MS-BILL-WRITE-004 — coverage for the unified
/// <see cref="InvoiceService.TransitionAsync"/> dispatcher.
///
/// These tests exercise the dispatch + accounting-safety contract
/// described on <see cref="IInvoiceService.TransitionAsync"/> — they
/// do NOT re-prove the per-action methods' guards (those are owned
/// by <c>InvoiceServiceLifecycleTests</c> / <c>InvoiceLifecycleServiceTests</c>).
/// What we DO prove here:
///   - happy path for each accepted target (Issued, Voided, Overdue, Paid)
///   - illegal-edge rejection routes through the lifecycle engine
///   - target=Paid pre-check refuses balance > 0 BEFORE mutating
///   - argument guards (empty tenant id / invoice id / target / reason)
///   - cross-tenant probe surfaces as null (treat as 404 at the API)
/// </summary>
public sealed class InvoiceTransitionTests
{
    private static InvoiceService NewService(out InMemoryInvoiceRepository invoices, out InMemoryCustomerRepository customers)
    {
        invoices = new InMemoryInvoiceRepository();
        customers = new InMemoryCustomerRepository();
        var refunds = new InMemoryRefundRepository();
        return new InvoiceService(
            invoices,
            customers,
            refunds,
            new InvoiceLifecycleService(),
            new Fakes.NoTemplateSelectionService(),
            new InvoiceTemplateStampingService());
    }

    // ---------------- happy paths --------------------------------

    [Fact]
    public async Task Draft_to_Issued_dispatches_to_IssueAsync_and_returns_previous_status()
    {
        var tenantId = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantId);
        var invoice = TestData.SeedInvoice(invoices, tenantId, customer.Id, totalAmount: 100m);

        var result = await svc.TransitionAsync(tenantId, invoice.Id, InvoiceStatus.Issued, "Send to client");

        result.Should().NotBeNull();
        result!.PreviousStatus.Should().Be(InvoiceStatus.Draft);
        result.Invoice.Status.Should().Be(InvoiceStatus.Issued);
    }

    [Fact]
    public async Task Issued_to_Voided_dispatches_to_VoidAsync()
    {
        var tenantId = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantId);
        var invoice = TestData.SeedInvoice(invoices, tenantId, customer.Id, 100m, status: InvoiceStatus.Issued);

        var result = await svc.TransitionAsync(tenantId, invoice.Id, InvoiceStatus.Voided, "Issued in error");

        result.Should().NotBeNull();
        result!.PreviousStatus.Should().Be(InvoiceStatus.Issued);
        result.Invoice.Status.Should().Be(InvoiceStatus.Voided);
    }

    [Fact]
    public async Task Issued_to_Overdue_when_due_date_passed()
    {
        var tenantId = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantId);
        var pastDue = DateTime.UtcNow.AddDays(-10);
        var invoice = TestData.SeedInvoice(
            invoices, tenantId, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: pastDue);

        var result = await svc.TransitionAsync(tenantId, invoice.Id, InvoiceStatus.Overdue, "Past due 10 days");

        result.Should().NotBeNull();
        result!.PreviousStatus.Should().Be(InvoiceStatus.Issued);
        result.Invoice.Status.Should().Be(InvoiceStatus.Overdue);
    }

    [Fact]
    public async Task Target_Paid_succeeds_when_payments_cover_total()
    {
        var tenantId = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantId);
        var invoice = TestData.SeedInvoice(invoices, tenantId, customer.Id, 100m, status: InvoiceStatus.Issued);
        // Seed a covering payment directly so ReevaluateAsync lands on Paid.
        invoice.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            Amount = 100m,
            Currency = invoice.Currency,
            Method = "wire",
            PaidAt = DateTime.UtcNow,
            Status = "Recorded",
            CreatedAt = DateTime.UtcNow,
            // TB-MERGE-01 import fix: archive's Payment entity has no
            // UpdatedAt column, so the test initialiser cannot set it.
        });

        var result = await svc.TransitionAsync(tenantId, invoice.Id, InvoiceStatus.Paid, "Wire received");

        result.Should().NotBeNull();
        result!.Invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    // ---------------- illegal edges ------------------------------

    [Fact]
    public async Task Paid_to_Issued_rejected_by_lifecycle_engine()
    {
        var tenantId = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantId);
        var invoice = TestData.SeedInvoice(invoices, tenantId, customer.Id, 100m, status: InvoiceStatus.Paid);

        var act = async () => await svc.TransitionAsync(tenantId, invoice.Id, InvoiceStatus.Issued, "rollback please");

        await act.Should().ThrowAsync<InvalidInvoiceTransitionException>();
        // No mutation.
        var after = await invoices.GetByIdForTenantAsync(tenantId, invoice.Id);
        after!.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Voided_to_anything_rejected_by_lifecycle_engine()
    {
        var tenantId = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantId);
        var invoice = TestData.SeedInvoice(invoices, tenantId, customer.Id, 100m, status: InvoiceStatus.Voided);

        var act = async () => await svc.TransitionAsync(tenantId, invoice.Id, InvoiceStatus.Issued, "undo void");

        await act.Should().ThrowAsync<InvalidInvoiceTransitionException>();
    }

    [Fact]
    public async Task Target_Paid_with_balance_above_zero_throws_without_mutating()
    {
        var tenantId = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantId);
        var invoice = TestData.SeedInvoice(invoices, tenantId, customer.Id, 100m, status: InvoiceStatus.Issued);
        // No payments seeded — paidSum = 0 < 100.

        var act = async () => await svc.TransitionAsync(tenantId, invoice.Id, InvoiceStatus.Paid, "force flip");

        var ex = await act.Should().ThrowAsync<InvalidInvoiceStateException>();
        ex.Which.Message.Should().Contain("do not cover");

        var after = await invoices.GetByIdForTenantAsync(tenantId, invoice.Id);
        after!.Status.Should().Be(InvoiceStatus.Issued);
    }

    [Fact]
    public async Task Target_outside_unified_matrix_rejected_as_illegal()
    {
        // Refunded / PartiallyRefunded are NOT exposed via this endpoint;
        // either the engine refuses the edge from the source state, or the
        // dispatcher's default branch refuses with InvalidInvoiceTransitionException.
        var tenantId = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantId);
        var invoice = TestData.SeedInvoice(invoices, tenantId, customer.Id, 100m, status: InvoiceStatus.Issued);

        var act = async () => await svc.TransitionAsync(tenantId, invoice.Id, "Refunded", "no refund here");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------------- argument guards ----------------------------

    [Fact]
    public async Task Empty_tenant_id_throws_ArgumentException()
    {
        var svc = NewService(out _, out _);
        var act = async () => await svc.TransitionAsync(Guid.Empty, Guid.NewGuid(), InvoiceStatus.Issued, "x");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Empty_invoice_id_throws_ArgumentException()
    {
        var svc = NewService(out _, out _);
        var act = async () => await svc.TransitionAsync(Guid.NewGuid(), Guid.Empty, InvoiceStatus.Issued, "x");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_target_throws_ArgumentException(string? target)
    {
        var svc = NewService(out _, out _);
        var act = async () => await svc.TransitionAsync(Guid.NewGuid(), Guid.NewGuid(), target!, "reason");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_reason_throws_ArgumentException(string? reason)
    {
        var tenantId = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantId);
        var invoice = TestData.SeedInvoice(invoices, tenantId, customer.Id, 100m);

        var act = async () => await svc.TransitionAsync(tenantId, invoice.Id, InvoiceStatus.Issued, reason!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Reason_over_1000_chars_throws_ArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantId);
        var invoice = TestData.SeedInvoice(invoices, tenantId, customer.Id, 100m);

        var oversize = new string('x', 1001);
        var act = async () => await svc.TransitionAsync(tenantId, invoice.Id, InvoiceStatus.Issued, oversize);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---------------- cross-tenant -------------------------------

    [Fact]
    public async Task Cross_tenant_probe_returns_null()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var svc = NewService(out var invoices, out var customers);
        var customer = TestData.SeedCustomer(customers, tenantA);
        var invoice = TestData.SeedInvoice(invoices, tenantA, customer.Id, 100m);

        var result = await svc.TransitionAsync(tenantB, invoice.Id, InvoiceStatus.Issued, "wrong tenant");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Missing_invoice_returns_null()
    {
        var svc = NewService(out _, out _);
        var result = await svc.TransitionAsync(Guid.NewGuid(), Guid.NewGuid(), InvoiceStatus.Issued, "ghost");
        result.Should().BeNull();
    }
}
