using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using Billing.Domain.Tests.Helpers;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// TBS-B05: cross-tenant batch sweep
/// (<see cref="InvoiceService.MarkEligibleOverdueAsync"/>) used by both the
/// hosted scheduler (tenantId=null) and the operator API endpoint
/// (tenantId=callingTenant).
/// </summary>
public class InvoiceOverdueBatchTests
{
    private static (InvoiceService svc, InMemoryInvoiceRepository invoices, InMemoryCustomerRepository customers) Build()
    {
        var invoices = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var refunds = new InMemoryRefundRepository(invoices);
        return (new InvoiceService(invoices, customers, refunds, new InvoiceLifecycleService(), new Fakes.NoTemplateSelectionService(), new InvoiceTemplateStampingService()), invoices, customers);
    }

    [Fact]
    public async Task MarkEligibleOverdueAsync_picks_only_eligible_invoices()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);

        // Three eligible: Issued past-due, PartiallyPaid past-due, Issued
        // past-due (different invoice).
        var e1 = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: DateTime.UtcNow.AddDays(-5));
        var e2 = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.PartiallyPaid, dueDate: DateTime.UtcNow.AddDays(-2));
        var e3 = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: DateTime.UtcNow.AddDays(-1));

        // Ineligible: Draft past-due, Issued future-due, Paid past-due,
        // Voided past-due.
        TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Draft, dueDate: DateTime.UtcNow.AddDays(-10));
        TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: DateTime.UtcNow.AddDays(10));
        TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Paid, dueDate: DateTime.UtcNow.AddDays(-10));
        TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Voided, dueDate: DateTime.UtcNow.AddDays(-10));

        var result = await svc.MarkEligibleOverdueAsync(
            tenantId: null, nowUtc: DateTime.UtcNow, take: 100);

        Assert.Equal(3, result.UpdatedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Failures);

        // The three eligible invoices are now Overdue.
        foreach (var id in new[] { e1.Id, e2.Id, e3.Id })
        {
            var fresh = await invoices.GetByIdForTenantAsync(tenant, id);
            Assert.Equal(InvoiceStatus.Overdue, fresh!.Status);
        }
    }

    [Fact]
    public async Task MarkEligibleOverdueAsync_respects_take_cap_oldest_first()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);

        // Five eligible with descending due dates (oldest = -10d).
        var ids = Enumerable.Range(1, 5).Select(i =>
            TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
                status: InvoiceStatus.Issued,
                dueDate: DateTime.UtcNow.AddDays(-i * 2)).Id).ToList();

        var result = await svc.MarkEligibleOverdueAsync(
            tenantId: null, nowUtc: DateTime.UtcNow, take: 2);

        // Only the take=2 oldest get processed.
        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal(0, result.FailedCount);

        // Oldest two were ids[4] and ids[3] (-10d, -8d).
        var oldest = await invoices.GetByIdForTenantAsync(tenant, ids[4]);
        var second = await invoices.GetByIdForTenantAsync(tenant, ids[3]);
        var youngest = await invoices.GetByIdForTenantAsync(tenant, ids[0]);
        Assert.Equal(InvoiceStatus.Overdue, oldest!.Status);
        Assert.Equal(InvoiceStatus.Overdue, second!.Status);
        Assert.Equal(InvoiceStatus.Issued,  youngest!.Status);
    }

    [Fact]
    public async Task MarkEligibleOverdueAsync_scopes_to_tenant_when_supplied()
    {
        var (svc, invoices, customers) = Build();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var custA = TestData.SeedCustomer(customers, tenantA);
        var custB = TestData.SeedCustomer(customers, tenantB);

        var a = TestData.SeedInvoice(invoices, tenantA, custA.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: DateTime.UtcNow.AddDays(-3));
        var b = TestData.SeedInvoice(invoices, tenantB, custB.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: DateTime.UtcNow.AddDays(-3));

        var result = await svc.MarkEligibleOverdueAsync(
            tenantId: tenantA, nowUtc: DateTime.UtcNow, take: 100);

        Assert.Equal(1, result.UpdatedCount);

        var freshA = await invoices.GetByIdForTenantAsync(tenantA, a.Id);
        var freshB = await invoices.GetByIdForTenantAsync(tenantB, b.Id);
        Assert.Equal(InvoiceStatus.Overdue, freshA!.Status);
        // Tenant B's invoice must not have been touched.
        Assert.Equal(InvoiceStatus.Issued, freshB!.Status);
    }

    [Fact]
    public async Task MarkEligibleOverdueAsync_returns_zero_when_take_is_zero()
    {
        var (svc, _, _) = Build();
        var result = await svc.MarkEligibleOverdueAsync(
            tenantId: null, nowUtc: DateTime.UtcNow, take: 0);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
    }

    [Fact]
    public async Task MarkEligibleOverdueAsync_does_not_mark_invoices_due_today()
    {
        // Date-boundary regression: an invoice whose DueDate falls
        // anywhere within the current UTC day must NOT be considered
        // overdue, matching ComputeStatus and the single-invoice
        // MarkOverdueAsync rule. Previously the eligibility query used
        // a time-of-day comparison and would incorrectly pick up
        // invoices "due earlier today".
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);

        var nowUtc = new DateTime(2026, 4, 24, 16, 0, 0, DateTimeKind.Utc);
        var dueEarlierToday = new DateTime(2026, 4, 24, 9, 0, 0, DateTimeKind.Utc);
        var dueYesterday   = new DateTime(2026, 4, 23, 23, 0, 0, DateTimeKind.Utc);

        var todayInv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: dueEarlierToday);
        var yesterdayInv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: dueYesterday);

        var result = await svc.MarkEligibleOverdueAsync(
            tenantId: null, nowUtc: nowUtc, take: 100);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.FailedCount);

        var todayFresh = await invoices.GetByIdForTenantAsync(tenant, todayInv.Id);
        var yesterdayFresh = await invoices.GetByIdForTenantAsync(tenant, yesterdayInv.Id);

        // "Due earlier today" stays Issued — date boundary not crossed.
        Assert.Equal(InvoiceStatus.Issued,  todayFresh!.Status);
        // "Due yesterday" flips to Overdue — boundary crossed.
        Assert.Equal(InvoiceStatus.Overdue, yesterdayFresh!.Status);
    }

    [Fact]
    public async Task TryMarkOverdueAsync_returns_null_when_status_changed_concurrently()
    {
        // TOCTOU regression: a candidate that has been re-read by the
        // batch path but raced with a concurrent payment (Issued ->
        // Paid) must NOT be overwritten by the conditional update.
        // The repository's TryMarkOverdueAsync re-checks the predicate
        // at write time; here we mutate the persisted row between
        // "fetch candidates" and "TryMarkOverdueAsync" to simulate
        // the race.
        var invoices = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued, dueDate: DateTime.UtcNow.AddDays(-3));

        var nowUtc = DateTime.UtcNow;
        var candidates = await invoices.GetInvoicesEligibleForOverdueAsync(
            tenantId: null, nowUtc: nowUtc, take: 10);
        Assert.Single(candidates);

        // Simulate a concurrent payment flipping the invoice to Paid
        // between the eligibility query and the conditional update.
        await invoices.UpdateStatusAsync(tenant, inv.Id, InvoiceStatus.Paid, DateTime.UtcNow);

        var raceResult = await invoices.TryMarkOverdueAsync(tenant, inv.Id, nowUtc);
        Assert.Null(raceResult);

        var fresh = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
        Assert.Equal(InvoiceStatus.Paid, fresh!.Status);
    }

    [Fact]
    public async Task TryMarkOverdueAsync_returns_null_when_status_is_terminal()
    {
        // Defensive: terminal Voided / Refunded / Paid all fail the
        // status guard inside the conditional update. The method
        // never throws — it just no-ops and returns null so the
        // batch loop can move on.
        var invoices = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);

        foreach (var status in new[]
                 {
                     InvoiceStatus.Paid,
                     InvoiceStatus.Voided,
                     InvoiceStatus.Refunded,
                     InvoiceStatus.PartiallyRefunded,
                     InvoiceStatus.Draft,
                     InvoiceStatus.Overdue,
                 })
        {
            var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m,
                status: status, dueDate: DateTime.UtcNow.AddDays(-3));
            var result = await invoices.TryMarkOverdueAsync(tenant, inv.Id, DateTime.UtcNow);
            Assert.Null(result);
            var fresh = await invoices.GetByIdForTenantAsync(tenant, inv.Id);
            Assert.Equal(status, fresh!.Status);
        }
    }
}
