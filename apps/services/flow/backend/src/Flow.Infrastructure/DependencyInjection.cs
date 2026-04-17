using Flow.Application.Engines.WorkflowEngine;
using Flow.Application.Interfaces;
using Flow.Application.Services;
using Flow.Domain.Interfaces;
using Flow.Infrastructure.Persistence;
using Flow.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOW_DB_CONNECTION_STRING")
            ?? configuration.GetConnectionString("FlowDb")
            ?? "Server=localhost;Database=flow_db;User=root;Password=;";

        services.AddDbContext<FlowDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)),
                mysqlOptions => mysqlOptions.EnableRetryOnFailure(3)));

        services.AddScoped<IFlowDbContext>(provider => provider.GetRequiredService<FlowDbContext>());
        services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));

        return services;
    }

    public static IServiceCollection AddTenantProvider<TTenantProvider>(this IServiceCollection services)
        where TTenantProvider : class, ITenantProvider
    {
        services.AddScoped<ITenantProvider, TTenantProvider>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IAutomationExecutor, AutomationExecutor>();
        services.AddScoped<INotificationService, NotificationService>();
        // LS-FLOW-MERGE-P3 — product-facing service for SynqLien/CareConnect/SynqFund.
        services.AddScoped<IProductWorkflowService, ProductWorkflowService>();
        // LS-FLOW-MERGE-P5 — execution authority for WorkflowInstance.
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        // LS-FLOW-E11.2 — auto-creates WorkflowTask rows from workflow
        // transitions. Stateless service; one per scoped DbContext.
        services.AddScoped<IWorkflowTaskFromWorkflowFactory, WorkflowTaskFromWorkflowFactory>();

        return services;
    }
}
