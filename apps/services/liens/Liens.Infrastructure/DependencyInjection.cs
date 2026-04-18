using BuildingBlocks.Context;
using LegalSynq.AuditClient;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Application.Services;
using Liens.Infrastructure.Audit;
using Liens.Infrastructure.Documents;
using Liens.Infrastructure.Notifications;
using Liens.Infrastructure.Persistence;
using Liens.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<ILienTaskTemplateService, LienTaskTemplateService>();
        services.AddScoped<ILienTaskGenerationRuleService, LienTaskGenerationRuleService>();
        services.AddScoped<ILienTaskGenerationEngine, LienTaskGenerationEngine>();
        services.AddScoped<ILienTaskNoteService, LienTaskNoteService>();
        services.AddScoped<ILienCaseNoteService, LienCaseNoteService>();

        var docsBaseUrl = configuration["Services:DocumentsUrl"] ?? "http://localhost:5006";
        services.AddHttpClient("DocumentsService", client =>
        {
            client.BaseAddress = new Uri(docsBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddAuditEventClient(configuration);
        services.AddScoped<IAuditPublisher, AuditPublisher>();

        var notifBaseUrl = configuration["Services:NotificationsUrl"] ?? "http://localhost:5008";
        services.AddHttpClient("NotificationsService", client =>
        {
            client.BaseAddress = new Uri(notifBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<INotificationPublisher, NotificationPublisher>();

        return services;
    }
}
