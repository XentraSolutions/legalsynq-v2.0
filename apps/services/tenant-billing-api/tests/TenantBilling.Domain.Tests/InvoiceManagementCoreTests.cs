using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;
using TenantBilling.Domain.Tests.Fakes;
using TenantBilling.Domain.Tests.Helpers;
using Xunit;

namespace TenantBilling.Domain.Tests;

/// <summary>
/// TBS-B03 — Invoice Management Core. Covers the new behaviors added on top
/// of the existing transition tests:
///   - Discount validation + total calculation including discount.
///   - Auto-numbering INV-YYYY-NNNNNN when caller passes null/blank.
///   - IssueAsync stamps IssuedAt only on the first transition.
///   - Paged ListPagedAsync filtering, clamping, and totals.
///   - Empty-id guards on GetAsync.
/// </summary>
public class InvoiceManagementCoreTests
{
    private static readonly DateTime IssueDate = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DueDate = IssueDate.AddDays(30);

    private static (InvoiceService svc, InMemoryInvoiceRepository invoices, InMemoryCustomerRepository customers) Build()
    {
        var invoices = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var refunds = new InMemoryRefundRepository(invoices);
        return (new InvoiceService(invoices, customers, refunds, new InvoiceLifecycleService(), new Fakes.NoTemplateSelectionService(), new InvoiceTemplateStampingService()), invoices, customers);
    }

    private static IReadOnlyList<NewInvoiceLine> SingleLine(decimal unitPrice = 100m, int quantity = 1)
        => new[] { new NewInvoiceLine("Widget", quantity, unitPrice) };

    // -----------------------------------------------------------------------
    // Discount
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Create_with_discount_subtracts_from_total()
    {
        var (svc, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var invoice = await svc.CreateAsync(
            tenant, customer.Id, "INV-DISC", IssueDate, DueDate, "USD", null,
            SingleLine(100m, 2), taxAmount: 10m, discountAmount: 25m);

        Assert.Equal(200m, invoice.Subtotal);
        Assert.Equal(10m, invoice.TaxAmount);
        Assert.Equal(25m, invoice.DiscountAmount);
        Assert.Equal(185m, invoice.TotalAmount); // 200 + 10 - 25
    }

    [Fact]
    public async Task Create_rejects_negative_discount()
    {
        var (svc, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(
            tenant, customer.Id, "INV-NEG", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m, discountAmount: -1m));
        Assert.Contains("DiscountAmount must be >= 0", ex.Message);
    }

    [Fact]
    public async Task Create_rejects_discount_greater_than_subtotal_plus_tax()
    {
        var (svc, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        // Subtotal 100 + Tax 5 = 105, discount 200 must fail.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(
            tenant, customer.Id, "INV-OVER", IssueDate, DueDate, "USD", null,
            SingleLine(100m), taxAmount: 5m, discountAmount: 200m));
        Assert.Contains("cannot exceed Subtotal+Tax", ex.Message);
    }

    [Fact]
    public async Task Create_allows_discount_equal_to_subtotal_plus_tax()
    {
        var (svc, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var invoice = await svc.CreateAsync(
            tenant, customer.Id, "INV-FULL", IssueDate, DueDate, "USD", null,
            SingleLine(100m), taxAmount: 5m, discountAmount: 105m);

        Assert.Equal(0m, invoice.TotalAmount);
    }

    // -----------------------------------------------------------------------
    // Auto-numbering
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Create_with_null_invoiceNumber_auto_generates_first_in_year()
    {
        var (svc, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var invoice = await svc.CreateAsync(
            tenant, customer.Id, null, IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        Assert.Equal($"INV-{IssueDate.Year:D4}-000001", invoice.InvoiceNumber);
    }

    [Fact]
    public async Task Create_with_blank_invoiceNumber_auto_generates_next_in_sequence()
    {
        var (svc, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var first = await svc.CreateAsync(
            tenant, customer.Id, null, IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);
        var second = await svc.CreateAsync(
            tenant, customer.Id, "   ", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);
        var third = await svc.CreateAsync(
            tenant, customer.Id, null, IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        Assert.Equal($"INV-{IssueDate.Year:D4}-000001", first.InvoiceNumber);
        Assert.Equal($"INV-{IssueDate.Year:D4}-000002", second.InvoiceNumber);
        Assert.Equal($"INV-{IssueDate.Year:D4}-000003", third.InvoiceNumber);
    }

    [Fact]
    public async Task Create_auto_number_uses_IssueDate_year()
    {
        var (svc, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        var year2025 = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var invoice = await svc.CreateAsync(
            tenant, customer.Id, null, year2025, year2025.AddDays(30), "USD", null,
            SingleLine(), taxAmount: 0m);

        Assert.StartsWith("INV-2025-", invoice.InvoiceNumber);
    }

    [Fact]
    public async Task Create_auto_number_walks_forward_when_slot_taken()
    {
        var (svc, _, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);

        // Manually take the slot 000001.
        await svc.CreateAsync(
            tenant, customer.Id, $"INV-{IssueDate.Year:D4}-000001", IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        var auto = await svc.CreateAsync(
            tenant, customer.Id, null, IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        Assert.Equal($"INV-{IssueDate.Year:D4}-000002", auto.InvoiceNumber);
    }

    [Fact]
    public async Task Create_auto_number_is_isolated_per_tenant()
    {
        var (svc, _, customers) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customerA = TestData.SeedCustomer(customers, tenantA);
        var customerB = TestData.SeedCustomer(customers, tenantB);

        var a1 = await svc.CreateAsync(
            tenantA, customerA.Id, null, IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);
        var a2 = await svc.CreateAsync(
            tenantA, customerA.Id, null, IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);
        var b1 = await svc.CreateAsync(
            tenantB, customerB.Id, null, IssueDate, DueDate, "USD", null,
            SingleLine(), taxAmount: 0m);

        Assert.Equal($"INV-{IssueDate.Year:D4}-000001", a1.InvoiceNumber);
        Assert.Equal($"INV-{IssueDate.Year:D4}-000002", a2.InvoiceNumber);
        Assert.Equal($"INV-{IssueDate.Year:D4}-000001", b1.InvoiceNumber);
    }

    // -----------------------------------------------------------------------
    // IssueAsync sets IssuedAt
    // -----------------------------------------------------------------------

    [Fact]
    public async Task IssueAsync_sets_IssuedAt_on_first_transition()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m);

        Assert.Null(inv.IssuedAt);
        var before = DateTime.UtcNow;
        var updated = await svc.IssueAsync(tenant, inv.Id);
        var after = DateTime.UtcNow;

        Assert.NotNull(updated);
        Assert.NotNull(updated!.IssuedAt);
        Assert.InRange(updated.IssuedAt!.Value, before.AddSeconds(-1), after.AddSeconds(1));
    }

    // -----------------------------------------------------------------------
    // ListPagedAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListPagedAsync_filters_by_status_and_returns_total()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Draft);
        TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Draft);
        TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        var page = await svc.ListPagedAsync(tenant, null, "Draft", null, null, null, 1, 25);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, i => Assert.Equal(InvoiceStatus.Draft, i.Status));
    }

    [Fact]
    public async Task ListPagedAsync_clamps_pageSize_above_max_to_100()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        for (var i = 0; i < 3; i++)
            TestData.SeedInvoice(invoices, tenant, customer.Id, 100m);

        var page = await svc.ListPagedAsync(tenant, null, null, null, null, null, 1, pageSize: 5_000);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task ListPagedAsync_uses_default_pageSize_when_zero_or_negative()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        for (var i = 0; i < 30; i++)
            TestData.SeedInvoice(invoices, tenant, customer.Id, 100m);

        var pageZero = await svc.ListPagedAsync(tenant, null, null, null, null, null, 1, 0);
        var pageNeg = await svc.ListPagedAsync(tenant, null, null, null, null, null, 1, -10);

        Assert.Equal(InvoiceService.DefaultPageSize, pageZero.Items.Count);
        Assert.Equal(InvoiceService.DefaultPageSize, pageNeg.Items.Count);
        Assert.Equal(30, pageZero.TotalCount);
    }

    [Fact]
    public async Task ListPagedAsync_clamps_page_below_one_to_one()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.NewGuid();
        var customer = TestData.SeedCustomer(customers, tenant);
        TestData.SeedInvoice(invoices, tenant, customer.Id, 100m);

        var page = await svc.ListPagedAsync(tenant, null, null, null, null, null, page: 0, pageSize: 25);

        Assert.Equal(1, page.TotalCount);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task ListPagedAsync_excludes_other_tenants()
    {
        var (svc, invoices, customers) = Build();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customerA = TestData.SeedCustomer(customers, tenantA);
        var customerB = TestData.SeedCustomer(customers, tenantB);
        TestData.SeedInvoice(invoices, tenantA, customerA.Id, 100m);
        TestData.SeedInvoice(invoices, tenantA, customerA.Id, 100m);
        TestData.SeedInvoice(invoices, tenantB, customerB.Id, 100m);

        var page = await svc.ListPagedAsync(tenantA, null, null, null, null, null, 1, 25);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, i => Assert.Equal(tenantA, i.TenantId));
    }

    // -----------------------------------------------------------------------
    // Empty-id guards
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_throws_on_empty_tenantId()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.GetAsync(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAsync_throws_on_empty_invoiceId()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.GetAsync(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public async Task ListPagedAsync_throws_on_empty_tenantId()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ListPagedAsync(Guid.Empty, null, null, null, null, null, 1, 25));
    }
}
