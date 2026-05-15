using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Tests.Fakes;

namespace TenantBilling.Domain.Tests.Helpers;

internal static class TestData
{
    public static Customer SeedCustomer(InMemoryCustomerRepository customers, Guid tenantId)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Acme Corp",
            Email = "billing@acme.test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        customers.AddAsync(customer).GetAwaiter().GetResult();
        return customer;
    }

    public static Invoice SeedInvoice(
        InMemoryInvoiceRepository invoices,
        Guid tenantId,
        Guid customerId,
        decimal totalAmount,
        string status = InvoiceStatus.Draft,
        DateTime? dueDate = null,
        string currency = "USD")
    {
        var now = DateTime.UtcNow;
        var due = dueDate ?? now.AddDays(30);
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}".Substring(0, 12),
            IssueDate = now,
            DueDate = due,
            Status = status,
            Subtotal = totalAmount,
            TaxAmount = 0m,
            TotalAmount = totalAmount,
            Currency = currency,
            CreatedAt = now,
            UpdatedAt = now,
        };
        invoices.AddAsync(invoice).GetAwaiter().GetResult();
        return invoice;
    }
}
