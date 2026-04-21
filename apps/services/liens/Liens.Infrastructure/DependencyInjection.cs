using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Context;
using BuildingBlocks.Notifications;
using LegalSynq.AuditClient;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Application.Services;
using Liens.Infrastructure.Audit;
using Liens.Infrastructure.Documents;
using Liens.Infrastructure.Notifications;
using Liens.Infrastructure.Persistence;
using Liens.Infrastructure.Repositories;
using Liens.Infrastructure.TaskService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Liens.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLiensServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LiensDb")
            ?? throw new InvalidOperationException("Connection string 'LiensDb' is not configured.");

        services.AddDbContext<LiensDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

        services.AddScoped<ICaseRepository, CaseRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IFacilityRepository, FacilityRepository>();
        services.AddScoped<ILookupValueRepository, LookupValueRepository>();
        services.AddScoped<ILienRepository, LienRepository>();
        services.AddScoped<ILienOfferRepository, LienOfferRepository>();
        services.AddScoped<IBillOfSaleRepository, BillOfSaleRepository>();
        services.AddScoped<IServicingItemRepository, ServicingItemRepository>();
        services.AddScoped<ILienTaskRepository, LienTaskRepository>();
        services.AddScoped<ILienWorkflowConfigRepository, LienWorkflowConfigRepository>();
        services.AddScoped<ILienTaskTemplateRepository, LienTaskTemplateRepository>();
        services.AddScoped<ILienTaskGenerationRuleRepository, LienTaskGenerationRuleRepository>();
        services.AddScoped<ILienTaskNoteRepository, LienTaskNoteRepository>();
        services.AddScoped<ILienCaseNoteRepository, LienCaseNoteRepository>();
        // LS-LIENS-FLOW-006 — Task governance
        services.AddScoped<ILienTaskGovernanceSettingsRepository, LienTaskGovernanceSettingsRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IBillOfSalePdfGenerator, BillOfSalePdfGenerator>();
        services.AddScoped<IBillOfSaleDocumentService, BillOfSaleDocumentService>();
        services.AddScoped<ILienSaleService, LienSaleService>();
        services.AddScoped<ILienService, LienService>();
        services.AddScoped<ILienOfferService, LienOfferService>();
        services.AddScoped<IBillOfSaleService, BillOfSaleService>();
        services.AddScoped<IBillOfSaleDocumentQueryService, BillOfSaleDocumentQueryService>();
        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<IServicingItemService, ServicingItemService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<ILienTaskService, LienTaskService>();
        services.AddScoped<ILienWorkflowConfigService, LienWorkflowConfigService>();
        services.AddScoped<IWorkflowTransitionValidationService, WorkflowTransitionValidationService>();
        services.AddScoped<ILienTaskTemplateService, LienTaskTemplateService>();
        services.AddScoped<ILienTaskGenerationRuleService, LienTaskGenerationRuleService>();
        services.AddScoped<ILienTaskGenerationEngine, LienTaskGenerationEngine>();
        services.AddScoped<ILienTaskNoteService, LienTaskNoteService>();
        services.AddScoped<ILienCaseNoteService, LienCaseNoteService>();
        // LS-LIENS-FLOW-006 — Task governance
        services.AddScoped<ILienTaskGovernanceService, LienTaskGovernanceService>();
        // LS-LIENS-FLOW-007 — Flow instance linkage resolver
        services.AddScoped<IFlowInstanceResolver, FlowInstanceResolver>();
        // LS-LIENS-FLOW-009 — Flow event consumption (delegates to Task service)
        services.AddScoped<IFlowEventHandler, FlowEventHandler>();
        // TASK-B04 — backfill service
        services.AddScoped<ILienTaskBackfillService, LienTaskBackfillService>();

        // TASK-MIG-01 — startup governance sync: copies liens_TaskGovernanceSettings
        // rows into tasks_GovernanceSettings on every startup. Idempotent; best-effort.
        services.AddSingleton<LiensGovernanceSyncService>();
        services.AddHostedService(sp => sp.GetRequiredService<LiensGovernanceSyncService>());

        var docsBaseUrl = configuration["Services:DocumentsUrl"] ?? "http://localhost:5006";
        services.AddHttpClient("DocumentsService", client =>
        {
            client.BaseAddress = new Uri(docsBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddAuditEventClient(configuration);
        services.AddScoped<IAuditPublisher, AuditPublisher>();

        // LS-NOTIF-CORE-021 — service token issuer for Notifications calls.
        // Mints HS256 JWTs (audience: notifications-service) so POST /v1/notifications
        // is authenticated rather than relying on the legacy X-Tenant-Id header.
        services.AddServiceTokenIssuer(configuration, "liens-service");
        services.AddTransient<NotificationsAuthDelegatingHandler>();

        var notifBaseUrl = configuration["Services:NotificationsUrl"] ?? "http://localhost:5008";
        services.AddHttpClient("NotificationsService", client =>
        {
            client.BaseAddress = new Uri(notifBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddHttpMessageHandler<NotificationsAuthDelegatingHandler>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();

        // TASK-B04 — Task service HTTP client with shared service-token auth handler.
        services.AddTransient<TaskServiceAuthDelegatingHandler>();

        var taskBaseUrl = configuration["ExternalServices:Task:BaseUrl"] ?? "http://localhost:5016";
        services.AddHttpClient<ILiensTaskServiceClient, LiensTaskServiceClient>(client =>
        {
            client.BaseAddress = new Uri(taskBaseUrl);
            client.Timeout     = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<TaskServiceAuthDelegatingHandler>();

        return services;
    }
}
