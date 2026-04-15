using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reports.Contracts.Adapters;
using Reports.Contracts.Persistence;
using Reports.Contracts.Queue;
using Reports.Infrastructure.Adapters;
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
        }
        else
        {
            services.AddSingleton<IReportRepository, MockReportRepository>();
            services.AddSingleton<ITemplateRepository, MockTemplateRepository>();
            services.AddSingleton<ITemplateAssignmentRepository, MockTemplateAssignmentRepository>();
            services.AddSingleton<ITenantReportOverrideRepository, MockTenantReportOverrideRepository>();
        }

        services.AddSingleton<IIdentityAdapter, MockIdentityAdapter>();
        services.AddSingleton<ITenantAdapter, MockTenantAdapter>();
        services.AddSingleton<IEntitlementAdapter, MockEntitlementAdapter>();
        services.AddSingleton<IAuditAdapter, MockAuditAdapter>();
        services.AddSingleton<IDocumentAdapter, MockDocumentAdapter>();
        services.AddSingleton<INotificationAdapter, MockNotificationAdapter>();
        services.AddSingleton<IProductDataAdapter, MockProductDataAdapter>();

        services.AddSingleton<IJobQueue, InMemoryJobQueue>();
        services.AddSingleton<IJobProcessor, MockJobProcessor>();

        return services;
    }
}
