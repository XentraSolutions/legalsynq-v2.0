using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.FlowClient;

public static class FlowClientServiceCollectionExtensions
{
    /// <summary>
    /// LS-FLOW-MERGE-P4 — register <see cref="IFlowClient"/> using the
    /// <c>Flow</c> configuration section. Adds <see cref="IHttpContextAccessor"/>
    /// so the client can forward the caller's bearer token.
    /// </summary>
    public static IServiceCollection AddFlowClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<FlowClientOptions>()
            .Bind(configuration.GetSection(FlowClientOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Flow:BaseUrl must be configured.")
            .Validate(o => o.TimeoutSeconds > 0, "Flow:TimeoutSeconds must be positive.");

        services.AddHttpContextAccessor();

        services.AddTransient<FlowRetryHandler>(sp =>
            new FlowRetryHandler(sp.GetRequiredService<ILogger<FlowRetryHandler>>()));

        services.AddHttpClient<IFlowClient, FlowClient>((sp, http) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FlowClientOptions>>().Value;
            http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/'));
            http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        })
        .AddHttpMessageHandler<FlowRetryHandler>();

        return services;
    }
}
