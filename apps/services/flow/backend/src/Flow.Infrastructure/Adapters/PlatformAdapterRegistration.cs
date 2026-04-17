using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Adapters.NotificationAdapter;
using Flow.Application.Events;
using Flow.Application.Outbox;
using Flow.Infrastructure.Adapters;
using Flow.Infrastructure.Events;
using Flow.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.Infrastructure;

/// <summary>
/// Wires Flow's platform adapter seams. Logging-backed safe defaults are
/// always registered. HTTP-backed clients are layered in only when
/// configuration supplies a base URL, keeping local/dev runs viable
/// without external dependencies.
/// </summary>
public static class PlatformAdapterRegistration
{
    public static IServiceCollection AddFlowPlatformAdapters(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ----- Audit ----------------------------------------------------
        services.AddSingleton<LoggingAuditAdapter>();
        var auditBaseUrl = configuration["Audit:BaseUrl"];

        if (!string.IsNullOrWhiteSpace(auditBaseUrl))
        {
            services.AddHttpClient<HttpAuditAdapter>(client =>
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(auditBaseUrl));
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            services.AddScoped<IAuditAdapter>(sp => new HttpAuditAdapter(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpAuditAdapter)),
                sp.GetRequiredService<LoggingAuditAdapter>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpAuditAdapter>>()));
        }
        else
        {
            services.AddScoped<IAuditAdapter>(sp => sp.GetRequiredService<LoggingAuditAdapter>());
        }

        // ----- Notifications --------------------------------------------
        services.AddSingleton<LoggingNotificationAdapter>();
        var notifBaseUrl = configuration["Notifications:BaseUrl"];

        if (!string.IsNullOrWhiteSpace(notifBaseUrl))
        {
            services.AddHttpClient<HttpNotificationAdapter>(client =>
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(notifBaseUrl));
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            services.AddScoped<INotificationAdapter>(sp => new HttpNotificationAdapter(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpNotificationAdapter)),
                sp.GetRequiredService<LoggingNotificationAdapter>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpNotificationAdapter>>()));
        }
        else
        {
            services.AddScoped<INotificationAdapter>(sp => sp.GetRequiredService<LoggingNotificationAdapter>());
        }

        // ----- Internal in-process event dispatcher ---------------------
        services.AddScoped<IFlowEventDispatcher, FlowEventDispatcher>();

        // ----- LS-FLOW-E10.2 — transactional outbox + async processor --
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<OutboxDispatcher>();
        services.AddHostedService<OutboxProcessor>();

        return services;
    }

    private static string EnsureTrailingSlash(string url) =>
        url.EndsWith('/') ? url : url + "/";
}
