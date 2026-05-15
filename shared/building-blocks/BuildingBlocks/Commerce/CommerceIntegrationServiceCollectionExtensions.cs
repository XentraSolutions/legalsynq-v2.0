using Contracts.Commerce;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Commerce;

/// <summary>
/// DI registration helpers for the Commerce ecosystem integration layer.
///
/// <para>
/// Typical usage in a consuming service's <c>Program.cs</c>:
/// <code>
/// builder.Services.AddCommerceIntegration(builder.Configuration);
/// </code>
/// </para>
///
/// <para>
/// When <c>CommerceIntegration:Enabled = false</c> (default), noop
/// implementations are registered — no HTTP calls are made and no
/// external dependency on Commerce is introduced. Services can start and
/// operate without Commerce running.
/// </para>
///
/// <para>
/// When <c>CommerceIntegration:Enabled = true</c>, a named
/// <see cref="System.Net.Http.HttpClient"/> backed by
/// <c>HttpCommerceEntitlementClient</c> is wired, targeting
/// <c>CommerceIntegration:BaseUrl</c>.
/// </para>
/// </summary>
public static class CommerceIntegrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICommerceEntitlementClient"/> and
    /// <see cref="ICommerceLifecycleNotifier"/> using configuration from the
    /// <c>CommerceIntegration</c> section.
    /// </summary>
    public static IServiceCollection AddCommerceIntegration(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        services.Configure<CommerceIntegrationOptions>(
            configuration.GetSection(CommerceIntegrationOptions.SectionName));

        var opts = new CommerceIntegrationOptions();
        configuration.GetSection(CommerceIntegrationOptions.SectionName).Bind(opts);

        if (opts.Enabled)
        {
            services.AddHttpClient<ICommerceEntitlementClient, HttpCommerceEntitlementClient>(
                client =>
                {
                    var baseUrl = opts.BaseUrl.TrimEnd('/');
                    client.BaseAddress = new Uri(baseUrl + "/");
                    client.Timeout     = TimeSpan.FromSeconds(opts.TimeoutSeconds);

                    if (!string.IsNullOrWhiteSpace(opts.InternalServiceToken))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue(
                                "Bearer", opts.InternalServiceToken);
                    }
                });

            // LS-COMMERCE-FINAL-01 — wire real HTTP lifecycle notifier when
            // Commerce integration is enabled, so host lifecycle events
            // (tenant created/activated/suspended, product enabled/disabled)
            // are delivered to POST /api/commerce/integration/lifecycle-events.
            services.AddHttpClient<ICommerceLifecycleNotifier, HttpCommerceLifecycleNotifier>(
                client =>
                {
                    var baseUrl = opts.BaseUrl.TrimEnd('/');
                    client.BaseAddress = new Uri(baseUrl + "/");
                    client.Timeout     = TimeSpan.FromSeconds(opts.TimeoutSeconds);

                    if (!string.IsNullOrWhiteSpace(opts.InternalServiceToken))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue(
                                "Bearer", opts.InternalServiceToken);
                    }
                });
        }
        else
        {
            services.AddSingleton<ICommerceEntitlementClient, NoopCommerceEntitlementClient>();
            // Noop lifecycle notifier — no HTTP calls, returns Task.CompletedTask.
            services.AddSingleton<ICommerceLifecycleNotifier, NoopCommerceLifecycleNotifier>();
        }

        return services;
    }

    /// <summary>
    /// Registers a <see cref="CommerceServiceMetadata"/> singleton so that
    /// health and monitoring endpoints can surface Commerce integration posture.
    /// </summary>
    public static IServiceCollection AddCommerceServiceMetadata(
        this IServiceCollection services,
        CommerceServiceMetadata metadata)
    {
        services.AddSingleton(metadata);
        return services;
    }

    /// <summary>
    /// Convenience overload that builds <see cref="CommerceIntegrationOptions"/>
    /// from configuration to determine <c>CommerceIntegrationActive</c> automatically.
    /// </summary>
    public static IServiceCollection AddCommerceServiceMetadata(
        this IServiceCollection services,
        IConfiguration          configuration,
        string                  serviceName,
        string?                 productKey,
        string?                 primaryFeatureKey,
        bool                    subscriptionRequired,
        bool                    monetizationEnabled)
    {
        var opts = new CommerceIntegrationOptions();
        configuration.GetSection(CommerceIntegrationOptions.SectionName).Bind(opts);

        return services.AddCommerceServiceMetadata(new CommerceServiceMetadata(
            ServiceName:              serviceName,
            ProductKey:               productKey,
            PrimaryFeatureKey:        primaryFeatureKey,
            SubscriptionRequired:     subscriptionRequired,
            MonetizationEnabled:      monetizationEnabled,
            CommerceIntegrationActive: opts.Enabled));
    }
}
