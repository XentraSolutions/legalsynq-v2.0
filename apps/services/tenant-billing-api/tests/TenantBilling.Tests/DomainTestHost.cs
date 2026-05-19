using Microsoft.EntityFrameworkCore;
using TenantBilling.Domain.Repositories;
using TenantBilling.Domain.Services;
using TenantBilling.Infrastructure.Data;
using TenantBilling.Infrastructure.Repositories;

// INV-TPL-02: this test host now constructs the InvoiceService with
// the two new template-related collaborators (selection + stamping)
// so existing domain tests keep their real-repository behaviour while
// the new behaviour is exercisable from the same fixture.

namespace TenantBilling.Tests;

/// <summary>
/// Wires up the real EF Core repositories against a per-instance InMemory
/// database and constructs the domain services exactly the way the runtime
/// composition root does. Using the real repositories (rather than mocks)
/// keeps the validation rules under test honest about how they interact
/// with persistence (uniqueness, cross-aggregate lookups, etc).
/// </summary>
internal sealed class DomainTestHost : IDisposable
{
    public TenantBillingDbContext Db { get; }
    public ICustomerRepository CustomerRepo { get; }
    public IInvoiceRepository InvoiceRepo { get; }
    public IPaymentRepository PaymentRepo { get; }
    public IRefundRepository RefundRepo { get; }
    public IUnitOfWork UnitOfWork { get; }
    public CustomerService Customers { get; }
    public InvoiceService Invoices { get; }
    public PaymentService Payments { get; }

    public DomainTestHost()
    {
        var opts = new DbContextOptionsBuilder<TenantBillingDbContext>()
            .UseInMemoryDatabase($"tenant-billing-domain-tests-{Guid.CreateVersion7():N}")
            .Options;
        Db = new TenantBillingDbContext(opts);

        // INV-TPL-02: a single InvoiceTemplateStampingService instance is
        // shared by both the InvoiceRepository (which uses it on the
        // ApplyStampAsync path) and the InvoiceService (which uses it on
        // create). It's pure / stateless so sharing is safe and matches
        // the singleton DI registration in production.
        var stamping = new InvoiceTemplateStampingService();

        CustomerRepo = new CustomerRepository(Db);
        InvoiceRepo = new InvoiceRepository(Db, stamping);
        PaymentRepo = new PaymentRepository(Db);
        RefundRepo = new RefundRepository(Db);
        // Main introduced an IUnitOfWork dependency on PaymentService for the
        // atomic payment+status update. The EF-backed implementation degrades
        // to a no-op transaction on the InMemory provider used here, which
        // keeps these domain tests focused on validation/lifecycle logic.
        UnitOfWork = new EfUnitOfWork(Db);

        // The same concrete InvoiceTemplateService backs both the admin
        // surface and the selection surface, so we hand the same instance
        // to InvoiceService as the selection collaborator.
        TemplateRepo = new InvoiceTemplateRepository(Db);
        var templateService = new InvoiceTemplateService(TemplateRepo, UnitOfWork);
        Templates = templateService;
        TemplateSelection = templateService;

        Customers = new CustomerService(CustomerRepo);
        Invoices = new InvoiceService(
            InvoiceRepo, CustomerRepo, RefundRepo,
            new InvoiceLifecycleService(),
            TemplateSelection,
            stamping);
        Payments = new PaymentService(PaymentRepo, InvoiceRepo, UnitOfWork);
    }

    public IInvoiceTemplateRepository TemplateRepo { get; }
    public IInvoiceTemplateService Templates { get; }
    public IInvoiceTemplateSelectionService TemplateSelection { get; }

    public void Dispose() => Db.Dispose();
}
