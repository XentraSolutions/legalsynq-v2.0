using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Data;
using Notifications.Infrastructure.Providers.Adapters;
using Notifications.Infrastructure.Repositories;
using Notifications.Infrastructure.Services;
using Notifications.Infrastructure.Webhooks.Verifiers;
using Notifications.Infrastructure.Workers;
using LegalSynq.AuditClient;

namespace Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var host = configuration["NOTIF_DB_HOST"] ?? "localhost";
        var port = configuration["NOTIF_DB_PORT"] ?? "3306";
        var database = configuration["NOTIF_DB_NAME"] ?? "notifications_db";
        var user = configuration["NOTIF_DB_USER"] ?? "root";
        var password = configuration["NOTIF_DB_PASSWORD"] ?? "";

        var connectionString = configuration.GetConnectionString("NotificationsDb")
            ?? $"Server={host};Port={port};Database={database};User={user};Password={password};";

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)),
                mysql => mysql.EnableRetryOnFailure(3)));

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationAttemptRepository, NotificationAttemptRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<ITemplateVersionRepository, TemplateVersionRepository>();
        services.AddScoped<ITenantProviderConfigRepository, TenantProviderConfigRepository>();
        services.AddScoped<ITenantChannelProviderSettingRepository, TenantChannelProviderSettingRepository>();
        services.AddScoped<IProviderHealthRepository, ProviderHealthRepository>();
        services.AddScoped<IWebhookLogRepository, WebhookLogRepository>();
        services.AddScoped<INotificationEventRepository, NotificationEventRepository>();
        services.AddScoped<IContactSuppressionRepository, ContactSuppressionRepository>();
        services.AddScoped<IRecipientContactHealthRepository, RecipientContactHealthRepository>();
        services.AddScoped<IDeliveryIssueRepository, DeliveryIssueRepository>();
        services.AddScoped<ITenantBillingPlanRepository, TenantBillingPlanRepository>();
        services.AddScoped<ITenantBillingRateRepository, TenantBillingRateRepository>();
        services.AddScoped<ITenantRateLimitPolicyRepository, TenantRateLimitPolicyRepository>();
        services.AddScoped<ITenantContactPolicyRepository, TenantContactPolicyRepository>();
        services.AddScoped<ITenantBrandingRepository, TenantBrandingRepository>();
        services.AddScoped<IUsageMeterEventRepository, UsageMeterEventRepository>();

        services.AddScoped<INotificationService, NotificationServiceImpl>();
        services.AddScoped<ITemplateService, TemplateServiceImpl>();
        services.AddScoped<ITenantProviderConfigService, TenantProviderConfigServiceImpl>();
        services.AddScoped<ITemplateRenderingService, TemplateRenderingService>();
        services.AddScoped<ITemplateResolutionService, TemplateResolutionService>();
        services.AddScoped<IBrandingResolutionService, BrandingResolutionService>();
        services.AddScoped<IDeliveryStatusService, DeliveryStatusService>();
        services.AddScoped<IDeliveryIssueService, DeliveryIssueServiceImpl>();
        services.AddScoped<IContactEnforcementService, ContactEnforcementService>();
        services.AddScoped<IUsageEvaluationService, UsageEvaluationService>();
        services.AddScoped<IUsageMeteringService, UsageMeteringService>();
        services.AddScoped<IRecipientContactHealthService, RecipientContactHealthService>();
        services.AddScoped<IProviderRoutingService, ProviderRoutingService>();
        // In-memory provider is the default registration so role/org fan-out
        // resolves the seeded membership set at runtime; production deployments
        // replace it with an identity-backed implementation. Registered as both
        // the interface and the concrete type so a startup seeder can hydrate it.
        services.AddSingleton<InMemoryRoleMembershipProvider>();
        services.AddSingleton<IRoleMembershipProvider>(sp => sp.GetRequiredService<InMemoryRoleMembershipProvider>());
        services.AddScoped<IRecipientResolver, RecipientResolver>();
        services.AddScoped<IWebhookIngestionService, WebhookIngestionServiceImpl>();
        services.AddScoped<InternalEmailService>();

        services.AddHttpClient("SendGrid");
        services.AddHttpClient("Twilio");

        var sgApiKey = configuration["SENDGRID_API_KEY"] ?? "";
        var sgFromEmail = configuration["SENDGRID_FROM_EMAIL"] ?? "noreply@legalsynq.com";
        var sgFromName = configuration["SENDGRID_FROM_NAME"] ?? "LegalSynq";

        services.AddScoped<IEmailProviderAdapter>(sp =>
        {
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<SendGridAdapter>>();
            return new SendGridAdapter(sgApiKey, sgFromEmail, sgFromName, httpFactory.CreateClient("SendGrid"), logger);
        });

        var twilioSid = configuration["TWILIO_ACCOUNT_SID"] ?? "";
        var twilioToken = configuration["TWILIO_AUTH_TOKEN"] ?? "";
        var twilioFrom = configuration["TWILIO_FROM_NUMBER"] ?? "";

        services.AddScoped<ISmsProviderAdapter>(sp =>
        {
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<TwilioAdapter>>();
            return new TwilioAdapter(twilioSid, twilioToken, twilioFrom, httpFactory.CreateClient("Twilio"), logger);
        });

        var sgWebhookEnabled = configuration.GetValue<bool>("SENDGRID_WEBHOOK_VERIFICATION_ENABLED", false);
        var sgPublicKey = configuration["SENDGRID_WEBHOOK_PUBLIC_KEY"] ?? "";
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";

        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SendGridVerifier>>();
            return new SendGridVerifier(sgWebhookEnabled, sgPublicKey, environment, logger);
        });

        var twilioWebhookEnabled = configuration.GetValue<bool>("TWILIO_WEBHOOK_VERIFICATION_ENABLED", false);

        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TwilioVerifier>>();
            return new TwilioVerifier(twilioWebhookEnabled, twilioToken, environment, logger);
        });

        services.AddAuditEventClient(configuration);

        services.AddHostedService<NotificationWorker>();
        services.AddHostedService<ProviderHealthWorker>();
        services.AddHostedService<StatusSyncWorker>();

        return services;
    }
}
