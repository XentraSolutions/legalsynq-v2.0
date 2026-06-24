using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TenantBilling.Domain.Rendering;
using TenantBilling.Domain.Repositories;
using TenantBilling.Domain.Services;
using TenantBilling.Domain.Statements;
using TenantBilling.Domain.StatementTemplates;
using TenantBilling.Infrastructure.Data;
using TenantBilling.Infrastructure.Repositories;

namespace TenantBilling.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Register the Tenant Billing infrastructure: EF Core DbContext (MySQL via
    /// Pomelo when a connection string is configured, in-memory fallback
    /// otherwise so the host still starts in environments without MySQL),
    /// repositories, and domain services.
    /// </summary>
    public static IServiceCollection AddTenantBillingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<TenantBillingDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseInMemoryDatabase("tenant-billing-inmemory");
                return;
            }

            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                mysql => mysql.MigrationsAssembly(typeof(TenantBillingDbContext).Assembly.FullName));
        });

        // Repositories
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IInvoiceTemplateRepository, InvoiceTemplateRepository>();

        // Unit of work: wraps the scoped DbContext so domain services can
        // group multi-write flows (e.g. payment + invoice status update) into
        // a single atomic transaction with row-level locking.
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Domain services
        services.AddScoped<ICustomerService, CustomerService>();
        // Lifecycle engine is stateless and cheap to construct; register as
        // singleton so all scopes share the same allowed-transition table.
        services.AddSingleton<InvoiceLifecycleService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IPaymentService, PaymentService>();

        // Invoice template service: same concrete class implements both
        // the admin write surface (IInvoiceTemplateService) and the
        // selection-only read surface (IInvoiceTemplateSelectionService),
        // so we register one instance and forward both contracts.
        services.AddScoped<InvoiceTemplateService>();
        services.AddScoped<IInvoiceTemplateService>(sp => sp.GetRequiredService<InvoiceTemplateService>());
        services.AddScoped<IInvoiceTemplateSelectionService>(sp => sp.GetRequiredService<InvoiceTemplateService>());

        // INV-TPL-02 stamping: pure / stateless (it just copies fields
        // from a template onto an invoice in memory) so a singleton
        // suffices and avoids per-request allocation.
        services.AddSingleton<IInvoiceTemplateStampingService, InvoiceTemplateStampingService>();

        // INV-TPL-03 rendering. The HTML renderer is pure (string in,
        // string out) so a singleton is correct. The render service
        // composes scoped repositories + the payment summary, so it
        // is itself scoped. TimeProvider.System backs `GeneratedAtUtc`
        // (overridable from tests by registering a fake before this
        // call).
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IInvoiceHtmlRenderer, InvoiceHtmlRenderer>();
        services.AddScoped<IInvoiceRenderService, InvoiceRenderService>();

        // STAT-B01 customer statement engine. Mirrors the invoice
        // render pattern: HTML renderer is pure / stateless ⇒
        // singleton; the build service composes scoped repositories
        // ⇒ scoped. TimeProvider.System is already registered just
        // above by the invoice render block (TryAddSingleton means
        // the second registration here would be a no-op anyway, but
        // we omit it to keep this block declarative).
        services.AddSingleton<ICustomerStatementHtmlRenderer, CustomerStatementHtmlRenderer>();
        services.AddScoped<ICustomerStatementService, CustomerStatementService>();

        // STAT-B02 statement templates + persistence layer.
        // StatementTemplateService implements both the admin write
        // surface and the read-only selection surface, mirroring the
        // InvoiceTemplate pattern.
        services.AddScoped<IStatementTemplateRepository, StatementTemplateRepository>();
        services.AddScoped<ICustomerStatementRepository, CustomerStatementRepository>();
        services.AddScoped<StatementTemplateService>();
        services.AddScoped<IStatementTemplateService>(sp => sp.GetRequiredService<StatementTemplateService>());
        services.AddScoped<IStatementTemplateSelectionService>(sp => sp.GetRequiredService<StatementTemplateService>());
        services.AddScoped<IStatementNumberGenerator, StatementNumberGenerator>();
        services.AddScoped<ICustomerStatementPersistenceService, CustomerStatementPersistenceService>();

        return services;
    }
}
