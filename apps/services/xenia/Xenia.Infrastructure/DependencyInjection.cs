using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xenia.Application.Adapters;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Configuration;
using Xenia.Application.Email;
using Xenia.Application.Events;
using Xenia.Application.Modules;
using Xenia.Application.TenantContext;
using Xenia.Infrastructure.Configuration;
using Xenia.Infrastructure.Email;
using Xenia.Infrastructure.Email.Connectors;
using Xenia.Infrastructure.Events;
using Xenia.Infrastructure.Modules;
using Xenia.Infrastructure.Persistence;
using Xenia.Infrastructure.Platform;
using Xenia.Infrastructure.Registry;
using Xenia.Infrastructure.TenantContext;

namespace Xenia.Infrastructure;

public static class DependencyInjection
{
    public const string XeniaDbConnectionStringName = "XeniaDb";

    public static IServiceCollection AddXeniaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ─────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString(XeniaDbConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{XeniaDbConnectionStringName}' is missing. " +
                "Set it via the environment variable 'ConnectionStrings__XeniaDb'.");
        }

        services.AddDbContext<XeniaDbContext>(options =>
        {
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
            options.UseMySql(
                connectionString,
                serverVersion,
                mySqlOptions =>
                {
                    mySqlOptions.MigrationsAssembly(typeof(XeniaDbContext).Assembly.GetName().Name);
                    mySqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
        });

        // ── Migrations (run before any seeding) ──────────────────────────────
        services.AddHostedService<XeniaMigrationsHostedService>();

        // ── Module registry ───────────────────────────────────────────────────
        services.AddScoped<EfModuleRegistry>();
        services.AddScoped<IModuleRegistry>(sp => sp.GetRequiredService<EfModuleRegistry>());
        services.AddScoped<ITenantModuleRegistry>(sp => sp.GetRequiredService<EfModuleRegistry>());

        // ── Adapter registry ──────────────────────────────────────────────────
        services.AddScoped<IAdapterRegistry, EfAdapterRegistry>();

        // ── Configuration service ─────────────────────────────────────────────
        services.AddScoped<IXeniaConfigurationService, EfXeniaConfigurationService>();

        // ── Tenant context resolver ───────────────────────────────────────────
        services.AddScoped<ITenantContextResolver, JwtTenantContextResolver>();

        // ── Event publisher ───────────────────────────────────────────────────
        services.AddScoped<IEventPublisher, InMemoryEventPublisher>();

        // ── Platform adapters (noop / unavailable implementations) ────────────
        // Replace these registrations with real adapters in production when
        // the corresponding platform services are ready to be wired.
        services.AddScoped<ITenantAdapter, UnavailableTenantAdapter>();
        services.AddScoped<IIdentityAdapter, UnavailableIdentityAdapter>();
        services.AddScoped<IDocumentAdapter, UnavailableDocumentAdapter>();
        services.AddScoped<IAuditAdapter, UnavailableAuditAdapter>();
        services.AddScoped<INotificationAdapter, UnavailableNotificationAdapter>();
        services.AddScoped<IStorageAdapter, UnavailableStorageAdapter>();
        services.AddScoped<IWorkflowAdapter, UnavailableWorkflowAdapter>();
        services.AddScoped<IAiAdapter, UnavailableAiAdapter>();

        // ── Email module ──────────────────────────────────────────────────────
        AddEmailModule(services);

        return services;
    }

    private static void AddEmailModule(IServiceCollection services)
    {
        // Secret reference service (development stub — replace in production)
        services.AddScoped<ISecretReferenceService, UnavailableSecretReferenceService>();

        // Email source service
        services.AddScoped<IEmailSourceService, EfEmailSourceService>();

        // Connector registry (singleton — connectors are stateless)
        services.AddSingleton<EmailSourceConnectorRegistry>(sp =>
        {
            var registry = new EmailSourceConnectorRegistry();
            // Register all 5 connectors
            registry.RegisterConnector(
                sp.GetRequiredService<Microsoft365EmailConnector>());
            registry.RegisterConnector(
                sp.GetRequiredService<GoogleEmailConnector>());
            registry.RegisterConnector(
                sp.GetRequiredService<ImapEmailConnector>());
            registry.RegisterConnector(
                sp.GetRequiredService<Pop3EmailConnector>());
            registry.RegisterConnector(
                sp.GetRequiredService<ExchangeImapEmailConnector>());
            return registry;
        });
        services.AddSingleton<IEmailConnectorRegistry>(
            sp => sp.GetRequiredService<EmailSourceConnectorRegistry>());

        // Connector implementations (transient — no state)
        services.AddTransient<Microsoft365EmailConnector>();
        services.AddTransient<GoogleEmailConnector>();
        services.AddTransient<ImapEmailConnector>();
        services.AddTransient<Pop3EmailConnector>();
        services.AddTransient<ExchangeImapEmailConnector>();

        // Email module seeder (runs after migrations)
        services.AddHostedService<EmailModuleSeeder>();
    }
}
