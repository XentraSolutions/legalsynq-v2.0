using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;
using TenantBilling.Domain.Tests.Fakes;
using TenantBilling.Domain.Tests.Helpers;
using Xunit;

namespace TenantBilling.Domain.Tests;

public class InvoiceServiceTransitionTests
{
    private static (InvoiceService svc, InMemoryInvoiceRepository invoices, InMemoryCustomerRepository customers) Build()
    {
        var invoices = new InMemoryInvoiceRepository();
        var customers = new InMemoryCustomerRepository();
        var refunds = new InMemoryRefundRepository(invoices);
        return (new InvoiceService(invoices, customers, refunds, new InvoiceLifecycleService(), new Fakes.NoTemplateSelectionService(), new InvoiceTemplateStampingService()), invoices, customers);
    }

    [Fact]
    public async Task IssueAsync_moves_Draft_to_Issued()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m);

        var updated = await svc.IssueAsync(tenant, inv.Id);

        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.Issued, updated!.Status);
    }

    [Fact]
    public async Task IssueAsync_returns_null_when_invoice_missing()
    {
        var (svc, _, _) = Build();
        var updated = await svc.IssueAsync(Guid.CreateVersion7(), Guid.CreateVersion7());
        Assert.Null(updated);
    }

    [Fact]
    public async Task IssueAsync_rejects_when_not_Draft()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => svc.IssueAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task VoidAsync_moves_Draft_to_Voided()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m);

        var updated = await svc.VoidAsync(tenant, inv.Id);

        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.Voided, updated!.Status);
    }

    [Fact]
    public async Task VoidAsync_moves_Issued_to_Voided_when_no_payments()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Issued);

        var updated = await svc.VoidAsync(tenant, inv.Id);

        Assert.Equal(InvoiceStatus.Voided, updated!.Status);
    }

    [Fact]
    public async Task VoidAsync_rejects_when_payments_exist()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.PartiallyPaid);
        invoices.AttachPayment(inv.Id, new Payment
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant,
            InvoiceId = inv.Id,
            Amount = 50m,
            Currency = "USD",
            Method = "card",
            Status = "Succeeded",
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => svc.VoidAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task VoidAsync_rejects_when_already_terminal()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Voided);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => svc.VoidAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task VoidAsync_rejects_when_Paid()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(invoices, tenant, customer.Id, 100m, status: InvoiceStatus.Paid);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => svc.VoidAsync(tenant, inv.Id));
    }

    [Fact]
    public async Task ReevaluateAsync_marks_overdue_when_past_due_and_unpaid()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(
            invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Issued,
            dueDate: DateTime.UtcNow.AddDays(-2));

        var updated = await svc.ReevaluateAsync(tenant, inv.Id);

        Assert.Equal(InvoiceStatus.Overdue, updated!.Status);
    }

    [Fact]
    public async Task ReevaluateAsync_is_noop_for_voided()
    {
        var (svc, invoices, customers) = Build();
        var tenant = Guid.CreateVersion7();
        var customer = TestData.SeedCustomer(customers, tenant);
        var inv = TestData.SeedInvoice(
            invoices, tenant, customer.Id, 100m,
            status: InvoiceStatus.Voided,
            dueDate: DateTime.UtcNow.AddDays(-2));

        var updated = await svc.ReevaluateAsync(tenant, inv.Id);

        Assert.Equal(InvoiceStatus.Voided, updated!.Status);
    }

    [Fact]
    public async Task ReevaluateAsync_returns_null_when_invoice_missing()
    {
        var (svc, _, _) = Build();
        var updated = await svc.ReevaluateAsync(Guid.CreateVersion7(), Guid.CreateVersion7());
        Assert.Null(updated);
    }
}
