using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using Billing.Domain.Tests.Helpers;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// TBS-B05: single-invoice mark-overdue flow on
/// <see cref="InvoiceService.MarkOverdueAsync"/>.
/// </summary>
public class InvoiceOverdueTests
{
    private static (InvoiceService svc, InMemoryInvoiceRepository invoices, InMemoryCustomerRepository customers) Build()
    {
        var invoices = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var refunds = new InMemoryRefundRepository(invoices);
        return (new InvoiceService(invoices, customers, refunds, new InvoiceLifecycleService(), new Fakes.NoTemplateSelectionService(), new InvoiceTemplateStampingService()), invoices, customers);
    }

    [Fact]
    public async Task MarkOverdueAsync_moves_Issued_past_due_to_Overdue()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: DateTime.UtcNow.AddDays(-3));

        var updated = await svc.MarkOverdueAsync(tenant, inv.Id);

        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.Overdue, updated!.Status);
    }

    [Fact]
    public async Task MarkOverdueAsync_moves_PartiallyPaid_past_due_to_Overdue()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.PartiallyPaid, dueDate: DateTime.UtcNow.AddDays(-1));

        var updated = await svc.MarkOverdueAsync(tenant, inv.Id);

        Assert.Equal(InvoiceStatus.Overdue, updated!.Status);
    }

    [Fact]
    public async Task MarkOverdueAsync_rejects_when_due_date_in_future()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: DateTime.UtcNow.AddDays(5));

        await Assert.ThrowsAsync<InvalidInvoiceStateException>(
            () => svc.MarkOverdueAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task MarkOverdueAsync_rejects_Draft()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Draft, dueDate: DateTime.UtcNow.AddDays(-3));

        await Assert.ThrowsAsync<InvalidInvoiceTransitionException>(
            () => svc.MarkOverdueAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task MarkOverdueAsync_rejects_Paid()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Paid, dueDate: DateTime.UtcNow.AddDays(-3));

        await Assert.ThrowsAsync<InvalidInvoiceTransitionException>(
            () => svc.MarkOverdueAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task MarkOverdueAsync_rejects_Voided()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Voided, dueDate: DateTime.UtcNow.AddDays(-3));

        await Assert.ThrowsAsync<InvalidInvoiceTransitionException>(
            () => svc.MarkOverdueAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task MarkOverdueAsync_rejects_already_Overdue()
    {
        // The lifecycle graph has no Overdue → Overdue self-loop, so a
        // second mark on an already-overdue invoice is rejected at the
        // structural gate.
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Overdue, dueDate: DateTime.UtcNow.AddDays(-3));

        await Assert.ThrowsAsync<InvalidInvoiceTransitionException>(
            () => svc.MarkOverdueAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task MarkOverdueAsync_returns_null_when_invoice_missing()
    {
        var (svc, _, _) = Build();
        var updated = await svc.MarkOverdueAsync(Guid.CreateVersion7(), Guid.CreateVersion7());
        Assert.Null(updated);
    }

    [Fact]
    public async Task MarkOverdueAsync_returns_null_for_cross_tenant_invoice()
    {
        // Cross-tenant id is indistinguishable from "missing" by design:
        // the service must not leak existence to a non-owner tenant.
        var (svc, invoices, customers) = Build();
        var ownerTenant = Guid.CreateVersion7();
        var otherTenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, ownerTenant);
        var inv = TestData.SeedInvoice(invoices, ownerTenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: DateTime.UtcNow.AddDays(-3));

        var updated = await svc.MarkOverdueAsync(otherTenant, inv.Id);

        Assert.Null(updated);
        // Original invoice unchanged.
        var stillThere = await invoices.GetByIdForTenantAsync(ownerTenant, inv.Id);
        Assert.Equal(InvoiceStatus.Issued, stillThere!.Status);
    }

    [Fact]
    public async Task MarkOverdueAsync_throws_when_status_changes_between_pre_read_and_CAS()
    {
        // Race regression: a concurrent payment moves the invoice to
        // Paid AFTER the pre-read for diagnostics but BEFORE the
        // conditional update. The service must NOT overwrite the
        // newer Paid status; instead it surfaces the actual current
        // status as a typed transition exception so the API returns
        // 400 with the truthful diagnostic.
        var underlying = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var refunds = new InMemoryRefundRepository(underlying);
        var racing = new RacingInvoiceRepository(underlying);
        var svc = new InvoiceService(racing, customers, refunds, new InvoiceLifecycleService(), new Fakes.NoTemplateSelectionService(), new InvoiceTemplateStampingService());

        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(underlying, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: DateTime.UtcNow.AddDays(-3));

        // Arm the wrapper: when MarkOverdueAsync calls
        // TryMarkOverdueAsync, the wrapper first flips Status to Paid
        // (simulating a concurrent payment landing) and then delegates.
        // The eligibility predicate inside TryMarkOverdueAsync will
        // miss; the service should re-read and throw.
        racing.OnTryMarkOverdueAsync = async (tid, iid, now, ct) =>
        {
            await underlying.UpdateStatusAsync(tid, iid, InvoiceStatus.Paid, DateTime.UtcNow, null, ct);
        };

        var ex = await Assert.ThrowsAsync<InvalidInvoiceTransitionException>(
            () => svc.MarkOverdueAsync(tenant, inv.Id));
        Assert.Contains("Paid", ex.Message);

        // The persisted state stayed Paid — never reverted to Overdue.
        var fresh = await underlying.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Paid, fresh!.Status);
    }

    /// <summary>
    /// Test-only repository wrapper that lets a single test fire a
    /// callback right before <see cref="TryMarkOverdueAsync"/> delegates
    /// to the underlying store. Used to simulate a concurrent writer
    /// landing between the service's pre-read and its conditional
    /// update — i.e. the TOCTOU window.
    /// </summary>
    private sealed class RacingInvoiceRepository : Domain.Repositories.IInvoiceRepository
    {
        private readonly InMemoryInvoiceRepository _inner;
        public Func<Guid, Guid, DateTime, CancellationToken, Task>? OnTryMarkOverdueAsync { get; set; }
        public RacingInvoiceRepository(InMemoryInvoiceRepository inner) => _inner = inner;
        public Task<Invoice> AddAsync(Invoice invoice, CancellationToken ct = default) => _inner.AddAsync(invoice, ct);
        public Task<Invoice?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default) => _inner.GetByIdForTenantAsync(tenantId, id, ct);
        public Task<IReadOnlyList<Invoice>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default) => _inner.GetAllForTenantAsync(tenantId, ct);
        public Task<IReadOnlyList<Invoice>> ListAsync(Guid tenantId, string? search, string? status, Guid? customerId, DateTime? fromDate, DateTime? toDate, int page, int pageSize, CancellationToken ct = default) => _inner.ListAsync(tenantId, search, status, customerId, fromDate, toDate, page, pageSize, ct);
        public Task<int> CountAsync(Guid tenantId, string? search, string? status, Guid? customerId, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default) => _inner.CountAsync(tenantId, search, status, customerId, fromDate, toDate, ct);
        public Task<bool> ExistsByTenantAndNumberAsync(Guid tenantId, string invoiceNumber, Guid? excludingInvoiceId = null, CancellationToken ct = default) => _inner.ExistsByTenantAndNumberAsync(tenantId, invoiceNumber, excludingInvoiceId, ct);
        public Task<string?> GetLatestInvoiceNumberAsync(Guid tenantId, int year, CancellationToken ct = default) => _inner.GetLatestInvoiceNumberAsync(tenantId, year, ct);
        public Task<Invoice?> UpdateStatusAsync(Guid tenantId, Guid invoiceId, string status, DateTime updatedAt, DateTime? issuedAt = null, CancellationToken ct = default) => _inner.UpdateStatusAsync(tenantId, invoiceId, status, updatedAt, issuedAt, ct);
        public Task<IReadOnlyList<Invoice>> GetInvoicesEligibleForOverdueAsync(Guid? tenantId, DateTime nowUtc, int take, CancellationToken ct = default) => _inner.GetInvoicesEligibleForOverdueAsync(tenantId, nowUtc, take, ct);
        public async Task<Invoice?> TryMarkOverdueAsync(Guid tenantId, Guid invoiceId, DateTime nowUtc, CancellationToken ct = default)
        {
            if (OnTryMarkOverdueAsync is not null)
                await OnTryMarkOverdueAsync(tenantId, invoiceId, nowUtc, ct);
            return await _inner.TryMarkOverdueAsync(tenantId, invoiceId, nowUtc, ct);
        }
        // INV-TPL-02: pass-through; this fixture only races the
        // overdue path, but we still need to satisfy the interface.
        public Task<Invoice?> ApplyStampAsync(Guid tenantId, Guid invoiceId, Domain.Entities.InvoiceTemplate template, DateTime stampedAtUtc, CancellationToken ct = default)
            => _inner.ApplyStampAsync(tenantId, invoiceId, template, stampedAtUtc, ct);
        // STAT-B01: pass-through; this fixture races the overdue path
        // only, but the interface now requires the customer-scoped
        // invoice query, so forward to the underlying in-memory store.
        public Task<IReadOnlyList<Invoice>> GetInvoicesForCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
            => _inner.GetInvoicesForCustomerAsync(tenantId, customerId, ct);
    }
}
