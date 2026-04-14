using Microsoft.Extensions.DependencyInjection;
using Reports.Contracts.Adapters;
using Reports.Contracts.Guardrails;
using Reports.Contracts.Persistence;
using Reports.Contracts.Queue;
using Reports.Application.Guardrails;
using Reports.Infrastructure.Adapters;
using Reports.Infrastructure.Persistence;
using Reports.Infrastructure.Queue;

namespace Reports.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReportsInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IIdentityAdapter, MockIdentityAdapter>();
        services.AddSingleton<ITenantAdapter, MockTenantAdapter>();
        services.AddSingleton<IEntitlementAdapter, MockEntitlementAdapter>();
        services.AddSingleton<IAuditAdapter, MockAuditAdapter>();
        services.AddSingleton<IDocumentAdapter, MockDocumentAdapter>();
        services.AddSingleton<INotificationAdapter, MockNotificationAdapter>();
        services.AddSingleton<IProductDataAdapter, MockProductDataAdapter>();

        services.AddSingleton<IJobQueue, InMemoryJobQueue>();
        services.AddSingleton<IJobProcessor, MockJobProcessor>();
        services.AddSingleton<IReportRepository, MockReportRepository>();

        services.AddSingleton<IGuardrailValidator, GuardrailValidator>();

        return services;
    }
}
