using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Billing.Domain.Accounting.Erp;
using Billing.Domain.Accounting.Erp.BulkImport;
using Billing.Domain.Accounting.Erp.QuickBooks;
using Billing.Domain.Rendering;
using Billing.Domain.Reporting;
using Billing.Domain.Repositories;
using Billing.Domain.Services;
using Billing.Domain.Statements;
using Billing.Domain.Statements.Delivery;
using Billing.Domain.StatementTemplates;
using Billing.Infrastructure.Accounting.Erp;
using Billing.Infrastructure.Accounting.Erp.Providers;
using Billing.Infrastructure.Accounting.Erp.Providers.QuickBooks;
using Billing.Infrastructure.Data;
using Billing.Infrastructure.Delivery;
using Billing.Infrastructure.Delivery.Ncm;
using Billing.Infrastructure.Repositories;
using Billing.Infrastructure.Reporting;

namespace Billing.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Register the Tenant Billing infrastructure: EF Core DbContext (MySQL via
    /// Pomelo when a connection string is configured, in-memory fallback
    /// otherwise so the host still starts in environments without MySQL),
    /// repositories, and domain services.
    /// </summary>
    public static IServiceCollection AddBillingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Connection-string source precedence (MS-BILL-SVC-003 contract):
        //   1. BILLING_DB_CONNECTION env var       — production / deployed
        //   2. ConnectionStrings:Billing            — typical .NET binding
        //      (also satisfied by ConnectionStrings__Billing env var)
        //   3. ConnectionStrings:DefaultConnection  — legacy donor fallback
        // Empty/whitespace at every step → InMemory fallback so the host
        // still comes up in environments without MySQL (tests, smoke).
        var connectionString =
            Environment.GetEnvironmentVariable("BILLING_DB_CONNECTION")
            ?? configuration.GetConnectionString("Billing")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<BillingDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseInMemoryDatabase("billing-inmemory");
                return;
            }

            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                mysql => mysql.MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName));
        });

        // Repositories
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        // TB-DATA-01 — Tenant ↔ BillingAccount mapping.
        services.AddScoped<ITenantBillingProfileRepository, TenantBillingProfileRepository>();
        services.AddScoped<ITenantBillingProfileService, TenantBillingProfileService>();
        services.AddScoped<ITenantBillingAccountResolver, TenantBillingAccountResolver>();

        // TB-DATA-02 — Commerce subscription/entitlement bridge.
        // Repository + service hold the local entitlement snapshot per
        // profile; resolver composes profile + snapshot into a single
        // "is tenant billing enabled?" decision. Advisory only: no
        // existing controller consults the resolver in this block.
        services.AddScoped<ITenantBillingEntitlementSnapshotRepository,
            TenantBillingEntitlementSnapshotRepository>();
        services.AddScoped<ITenantBillingEntitlementService, TenantBillingEntitlementService>();
        services.AddScoped<ITenantBillingEnablementResolver, TenantBillingEnablementResolver>();

        // TB-ENF-01 — soft-enforcement policy.
        //
        // EntitlementEnforcementOptions binds Billing:EntitlementEnforcement
        // (defaults: Enabled=false, UnknownMode=ReadOnly,
        // GraceLimitedMode=ReadOnly, AllowPaymentsInReadOnly=true,
        // AllowStatementsInReadOnly=true, AllowExportsInReadOnly=false).
        //
        // ITenantBillingAccessPolicy composes the existing
        // ITenantBillingEnablementResolver with the bound options. The
        // RequireTenantBillingAccessAttribute (Billing.Api/Security)
        // resolves the policy per-request and short-circuits with HTTP 403
        // ProblemDetails when an attributed action is denied. Reads,
        // profile-admin and entitlement-admin endpoints are never
        // attributed and remain reachable. No Commerce calls, no shared
        // DB, no Identity dependency.
        services.AddOptions<EntitlementEnforcementOptions>()
            .Bind(configuration.GetSection(EntitlementEnforcementOptions.SectionName));
        services.AddScoped<ITenantBillingAccessPolicy>(sp =>
            new TenantBillingAccessPolicy(
                sp.GetRequiredService<ITenantBillingEnablementResolver>(),
                () => sp.GetRequiredService<
                    Microsoft.Extensions.Options.IOptionsMonitor<EntitlementEnforcementOptions>>().CurrentValue));
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IInvoiceAdjustmentRepository, InvoiceAdjustmentRepository>();
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
        // MS-BILL-WRITE-005 — append-only Invoice Adjustment / Credit Memo service.
        services.AddScoped<IInvoiceAdjustmentService, InvoiceAdjustmentService>();
        // MS-BILL-WRITE-006 — read-side accounting-summary projection
        // service. Pure read composition over the existing tenant-scoped
        // invoice / adjustment / payment repositories — no new repo
        // surface, no schema change.
        services.AddScoped<IInvoiceAccountingSummaryService, InvoiceAccountingSummaryService>();

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

        // MS-BILL-INT-001 / MS-BILL-INT-002 — Statement delivery
        // (notification provider integration).
        //
        // Provider selection is config-gated:
        //
        //   Billing:Delivery:Provider
        //     == "Ncm"  AND every required Billing:Delivery:Ncm
        //                  field is non-empty
        //                  → register NcmStatementDeliveryProvider
        //                    (typed HttpClient, bound options).
        //     == "Noop" OR omitted OR Ncm config incomplete
        //                  → register NoOpStatementDeliveryProvider
        //                    (preserves the WRITE-009 deterministic
        //                    "ProviderUnavailable /
        //                    ProviderNotConfigured" UX).
        //
        // The fallback is intentionally non-throwing: a deployment
        // that ships with `Provider = "Ncm"` but a missing API key
        // MUST NOT crash on startup; it MUST surface the same
        // operator-facing "Email delivery is not configured yet"
        // banner so the failure mode is identical to "no provider
        // wired at all". Half-configured deployments never silently
        // send to the wrong place.
        services.AddOptions<NcmDeliveryOptions>()
            .Bind(configuration.GetSection(NcmDeliveryOptions.SectionName));

        var providerKind = configuration["Billing:Delivery:Provider"];
        var ncmOptionsSnapshot = new NcmDeliveryOptions();
        configuration.GetSection(NcmDeliveryOptions.SectionName).Bind(ncmOptionsSnapshot);
        var useNcm = string.Equals(providerKind, "Ncm", StringComparison.OrdinalIgnoreCase)
                     && ncmOptionsSnapshot.HasRequired();

        if (useNcm)
        {
            // Typed HttpClient gives us per-provider lifetime
            // management, automatic dispose, and centralised
            // BaseAddress / Timeout configuration inside the
            // provider's constructor.
            services.AddHttpClient<IStatementDeliveryProvider, NcmStatementDeliveryProvider>();
        }
        else
        {
            // Singleton because NoOp is stateless; the real provider
            // is registered as a typed HttpClient (transient by
            // contract) so DI manages the per-request lifetime.
            services.AddSingleton<IStatementDeliveryProvider, NoOpStatementDeliveryProvider>();
        }
        services.AddScoped<IStatementDeliveryService, StatementDeliveryService>();

        // MS-BILL-INT-003 — Resend governance + provider-health.
        //
        // StatementRetryOptions: bound options (Billing:Delivery:Retry).
        //   - MaxAttempts: hard cap on DeliveryRetryCount before
        //     resend is governance-rejected with RetryLimitReached.
        //   - CooldownSeconds: minimum spacing between consecutive
        //     attempts on the same snapshot.
        //   - ProviderHealth: rolling-window thresholds for the
        //     in-memory provider-health monitor.
        // Defaults are safe; deployments can override via
        // appsettings or environment variables (the bound class is
        // re-read via IOptionsMonitor so a tunable change does not
        // require a restart).
        services.AddOptions<StatementRetryOptions>()
            .Bind(configuration.GetSection(StatementRetryOptions.SectionName));
        // ProviderHealthMonitor: in-memory, single-process, lock-
        // protected rolling window. Singleton on purpose — its
        // whole job is process-local visibility ("how is the
        // upstream behaving for THIS instance?"). It records every
        // send outcome and exposes Healthy / Degraded / Unavailable
        // for the read projection.
        services.AddSingleton<IProviderHealthMonitor, ProviderHealthMonitor>();

        // MS-BILL-WRITE-007 — read-only accounting / reconciliation
        // reporting surface. The repository owns the SQL projections;
        // the service is a stable seam for future cross-row composition.
        // Both are tenant-scoped via the controller and rely on the
        // already-registered TimeProvider for the aging "now" anchor.
        services.AddScoped<IBillingReportingRepository, BillingReportingRepository>();
        services.AddScoped<IBillingReportingService, BillingReportingService>();

        // MS-BILL-OPS-002 — read-only delivery analytics projection.
        // Tenant-scoped at the SQL level, AsNoTracking everywhere,
        // no mutations. Backs the four GET routes on
        // /api/analytics/delivery (admin-only at the BFF).
        services.AddScoped<
            Billing.Domain.Statements.Analytics.IBillingDeliveryAnalyticsRepository,
            Billing.Infrastructure.Reporting.BillingDeliveryAnalyticsRepository>();

        // MS-BILL-ERP-001 — External Accounting Integration Foundation.
        //
        // Tenant-scoped repository (lifecycle persistence + read-only
        // projection-window loaders) and the orchestrator service.
        // Both providers (NoOp + Json) are registered as singletons
        // so the orchestrator's `IEnumerable<IAccountingExportProvider>`
        // can resolve by name at request time — same selection model
        // as a future QuickBooks / NetSuite registration.
        //
        // No bi-directional sync, no queue/outbox, no scheduled
        // export. The controller drives the entire lifecycle inside
        // the request that triggered it.
        services.AddScoped<IAccountingExportRepository, AccountingExportRepository>();
        services.AddSingleton<IAccountingExportProvider, NoOpAccountingExportProvider>();
        services.AddSingleton<IAccountingExportProvider, JsonAccountingExportProvider>();

        // MS-BILL-ERP-002 — QuickBooks Online provider.
        //
        // The provider is ALWAYS registered, regardless of whether
        // the credentials are wired. A half-configured deployment
        // does NOT crash at startup, does NOT silently send to the
        // wrong realm, and does NOT cause the orchestrator to throw
        // "Unknown ERP export provider 'quickbooks'" — instead, the
        // provider's `IsConfigured` is `false` and `ExportAsync`
        // collapses to the deterministic `ProviderUnavailable`
        // outcome (same UI banner the NoOp default produces).
        //
        // Lifetimes:
        //   - QuickBooksTokenProvider is a SINGLETON so the in-
        //     memory access-token cache and the refresh-
        //     serialisation SemaphoreSlim are process-shared. The
        //     transport is built per refresh from
        //     IHttpClientFactory using a named client so handler
        //     pooling stays under the framework.
        //   - QuickBooksAccountingExportProvider is registered via
        //     AddHttpClient (typed) so DI manages a per-request
        //     HttpClient lifetime; the provider holds no per-process
        //     cache state of its own.
        //
        // No bi-directional sync, no QB→Billing mutation, no queue/
        // outbox/event-bus, no scheduled export. Every export remains
        // operator-triggered through the existing controller flow.
        services.AddOptions<QuickBooksOptions>()
            .Bind(configuration.GetSection(QuickBooksOptions.SectionName));

        services.AddHttpClient(QuickBooksHttpClients.TokenClient);
        services.AddSingleton<IQuickBooksTokenProvider, QuickBooksTokenProvider>();

        // MS-BILL-ERP-003 — operator-curated Billing↔QBO customer
        // mapping. Repository + service are scoped (tied to the
        // request-scoped DbContext); the service is consumed by the
        // QB provider and by the admin CRUD controller.
        services.AddScoped<IQuickBooksCustomerMappingRepository, QuickBooksCustomerMappingRepository>();
        services.AddScoped<IQuickBooksCustomerMappingService, QuickBooksCustomerMappingService>();

        services.AddHttpClient<IAccountingExportProvider, QuickBooksAccountingExportProvider>();

        services.AddScoped<IAccountingExportService, AccountingExportService>();

        // MS-BILL-ERP-004 — read-only ERP reconciliation /
        // diagnostics layer. Pure projections over the existing
        // append-only `accounting_exports` and
        // `quickbooks_customer_mappings` tables; no mutation, no
        // provider call, no scheduler. Repository + service are
        // scoped (tied to the request-scoped DbContext).
        services.AddScoped<
            Billing.Domain.Accounting.Erp.Reconciliation.IErpReconciliationRepository,
            Billing.Infrastructure.Accounting.Erp.Reconciliation.ErpReconciliationRepository>();
        services.AddScoped<
            Billing.Domain.Accounting.Erp.Reconciliation.IErpReconciliationService,
            Billing.Domain.Accounting.Erp.Reconciliation.ErpReconciliationService>();

        // MS-BILL-ERP-005 — Mapping-remediation surface.
        //
        // - Repository is scoped (tied to the request-scoped DbContext);
        //   pure read projections over `customers`,
        //   `quickbooks_customer_mappings`, `accounting_exports`, and
        //   `invoices`.
        // - Lookup adapter is registered as a typed HttpClient so DI
        //   manages the per-request HttpClient lifetime; the adapter
        //   itself holds no per-process cache state and reuses the
        //   already-registered singleton `IQuickBooksTokenProvider`
        //   for token acquisition (in-process cache, never echoed).
        // - Service composes both plus the existing ERP-003 mapping
        //   repository (READ probe only — no mutation). The actual
        //   persistence on confirmation REUSES the ERP-003 POST.
        services.AddScoped<
            Billing.Domain.Accounting.Erp.Remediation.IErpRemediationRepository,
            Billing.Infrastructure.Accounting.Erp.Remediation.ErpRemediationRepository>();
        services.AddHttpClient<
            Billing.Domain.Accounting.Erp.Remediation.IQuickBooksCustomerLookup,
            QuickBooksCustomerLookupService>();
        services.AddScoped<
            Billing.Domain.Accounting.Erp.Remediation.IErpRemediationService,
            Billing.Domain.Accounting.Erp.Remediation.ErpRemediationService>();

        // MS-BILL-ERP-006 — Bulk customer-mapping import / export /
        // history surface.
        //
        // - Parser is stateless ⇒ singleton.
        // - History repo is scoped (tied to the request-scoped
        //   DbContext); writes append-only.
        // - Service composes the parser + ERP-003 mapping repo +
        //   ERP-005 remediation repo + the new history repo. Every
        //   per-row commit funnels through ERP-003's AddAsync so
        //   the unique-index 409 backstop applies on every row;
        //   the service never opens its own SQL transaction or
        //   bypasses the existing write contract.
        services.AddSingleton<IBulkMappingImportParser,
            Billing.Infrastructure.Accounting.Erp.BulkImport.CsvBulkMappingImportParser>();
        services.AddScoped<IBulkMappingImportHistoryRepository,
            Billing.Infrastructure.Accounting.Erp.BulkImport.BulkMappingImportHistoryRepository>();
        services.AddScoped<IBulkMappingImportService, BulkMappingImportService>();

        // ---- MS-BILL-ERP-007 — governance analytics ----
        // Read-only tenant-admin analytics over the immutable
        // accounting_exports / quickbooks_customer_mappings /
        // bulk_mapping_import_history rows. The repo is
        // request-scoped (tied to the BillingDbContext); the
        // service is scoped because it composes multiple repo
        // calls within a single request. NEITHER mutates state,
        // contacts QBO, schedules work, or fans out events.
        services.AddScoped<
            Billing.Domain.Accounting.Erp.Governance.IErpGovernanceAnalyticsRepository,
            Billing.Infrastructure.Accounting.Erp.Governance.ErpGovernanceAnalyticsRepository>();
        services.AddScoped<
            Billing.Domain.Accounting.Erp.Governance.IErpGovernanceAnalyticsService,
            Billing.Domain.Accounting.Erp.Governance.ErpGovernanceAnalyticsService>();

        // ---- MS-BILL-ERP-008 — governance evidence export ----
        // Read-only export composer over the ERP-007 governance
        // service. NO new persisted state, NO mutation, NO QBO
        // call, NO retry/replay execution. Serialises the
        // existing projections into RFC 4180 CSV (via the shared
        // Billing.Domain.Csv.CsvWriter) or a JSON envelope.
        services.AddScoped<
            Billing.Domain.Accounting.Erp.Governance.Export.IGovernanceExportService,
            Billing.Domain.Accounting.Erp.Governance.Export.GovernanceExportService>();

        return services;
    }
}
