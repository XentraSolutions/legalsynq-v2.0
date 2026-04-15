using LegalSynq.AuditClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reports.Contracts.Adapters;
using Reports.Contracts.Delivery;
using Reports.Contracts.Export;
using Reports.Contracts.Persistence;
using Reports.Contracts.Queue;
using Reports.Infrastructure.Adapters;
using Reports.Infrastructure.Exporters;
using Reports.Infrastructure.Persistence;
using Reports.Infrastructure.Queue;

namespace Reports.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReportsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ReportsDb");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ReportsDbContext>(options =>
                options.UseMySql(
                    connectionString,
                    new MySqlServerVersion(new Version(8, 0, 0))));

            services.AddScoped<IReportRepository, EfReportRepository>();
            services.AddScoped<ITemplateRepository, EfTemplateRepository>();
            services.AddScoped<ITemplateAssignmentRepository, EfTemplateAssignmentRepository>();
            services.AddScoped<ITenantReportOverrideRepository, EfTenantReportOverrideRepository>();
            services.AddScoped<IReportScheduleRepository, EfReportScheduleRepository>();
        }
        else
        {
            services.AddSingleton<IReportRepository, MockReportRepository>();
            services.AddSingleton<ITemplateRepository, MockTemplateRepository>();
            services.AddSingleton<ITemplateAssignmentRepository, MockTemplateAssignmentRepository>();
            services.AddSingleton<ITenantReportOverrideRepository, MockTenantReportOverrideRepository>();
            services.AddSingleton<IReportScheduleRepository, MockReportScheduleRepository>();
        }

        var auditEnabled = configuration.GetValue<bool>("AuditService:Enabled");
        var auditBaseUrl = configuration["AuditService:BaseUrl"];

        if (auditEnabled && !string.IsNullOrWhiteSpace(auditBaseUrl))
        {
            services.AddAuditEventClient(configuration.GetSection("AuditClient"), auditBaseUrl, configuration);
            services.AddSingleton<IAuditAdapter, SharedAuditAdapter>();
        }
        else
        {
            services.AddSingleton<IAuditAdapter, MockAuditAdapter>();
        }

        services.AddSingleton<IIdentityAdapter, MockIdentityAdapter>();
        services.AddSingleton<ITenantAdapter, MockTenantAdapter>();
        services.AddSingleton<IEntitlementAdapter, MockEntitlementAdapter>();
        services.AddSingleton<IDocumentAdapter, MockDocumentAdapter>();
        services.AddSingleton<INotificationAdapter, MockNotificationAdapter>();
        services.AddSingleton<IProductDataAdapter, MockProductDataAdapter>();
        services.AddSingleton<IReportDataQueryAdapter, MockReportDataQueryAdapter>();

        services.AddSingleton<IJobQueue, InMemoryJobQueue>();
        services.AddSingleton<IJobProcessor, MockJobProcessor>();

        services.AddSingleton<IReportExporter, CsvReportExporter>();
        services.AddSingleton<IReportExporter, XlsxReportExporter>();
        services.AddSingleton<IReportExporter, PdfReportExporter>();

        services.AddSingleton<IReportDeliveryAdapter, OnScreenReportDeliveryAdapter>();
        services.AddSingleton<IReportDeliveryAdapter, EmailReportDeliveryAdapter>();
        services.AddSingleton<IReportDeliveryAdapter, SftpReportDeliveryAdapter>();

        return services;
    }

    private static void AddAuditEventClient(
        this IServiceCollection services,
        IConfigurationSection auditClientSection,
        string baseUrl,
        IConfiguration configuration)
    {
        var timeoutSeconds = configuration.GetValue<int?>("AuditService:TimeoutSeconds") ?? 5;
        var serviceToken = configuration["AuditService:ServiceToken"] ?? string.Empty;

        services.Configure<AuditClientOptions>(opts =>
        {
            opts.BaseUrl = baseUrl;
            opts.TimeoutSeconds = timeoutSeconds;
            opts.ServiceToken = serviceToken;
            opts.SourceSystem = "legalsynq-platform";
            opts.SourceService = "reports-service";
        });

        services.AddHttpClient<IAuditEventClient, HttpAuditEventClient>("AuditEventClient");
    }
}
