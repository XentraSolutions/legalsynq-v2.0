using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xenia.Application;
using Xenia.Infrastructure.Data;
using Xenia.Infrastructure.Providers;
using Xenia.Infrastructure.Security;

namespace Xenia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddXeniaInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString =
            configuration.GetConnectionString("XeniaDb")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__XeniaDb")
            ?? "Server=localhost;Database=xenia_db;User=root;Password=;";

        services.AddDbContext<XeniaDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 36)),
                mySql => mySql.EnableRetryOnFailure(3)));

        services.AddDataProtection();
        services.AddHttpClient();

        services.AddScoped<EfCoreXeniaStateStore>();
        services.AddScoped<EncryptedDbAiCredentialStore>();
        services.AddSingleton<InMemoryXeniaStateStore>();
        services.AddSingleton<InMemoryAiCredentialStore>();
        if (environment.IsDevelopment())
        {
            services.AddScoped<IXeniaStateStore, ResilientXeniaStateStore>();
            services.AddScoped<IAiCredentialStore, ResilientAiCredentialStore>();
        }
        else
        {
            services.AddScoped<IXeniaStateStore>(serviceProvider => serviceProvider.GetRequiredService<EfCoreXeniaStateStore>());
            services.AddScoped<IAiCredentialStore>(serviceProvider => serviceProvider.GetRequiredService<EncryptedDbAiCredentialStore>());
        }

        services.AddScoped<IAiUsageNormalizer, XeniaUsageNormalizer>();
        services.AddScoped<IAiProviderHealthCheck, XeniaProviderHealthCheck>();
        services.AddScoped<IProviderRoutingPolicy, DefaultProviderRoutingPolicy>();
        services.AddScoped<IProviderFailoverPolicy, DefaultProviderFailoverPolicy>();
        services.AddScoped<IAiProviderGateway, AiProviderGateway>();
        services.AddScoped<IAiProviderAdapter, OpenAiProviderAdapter>();
        services.AddScoped<IAiProviderAdapter, AzureOpenAiProviderAdapter>();
        services.AddScoped<IAiProviderAdapter, AnthropicProviderAdapter>();
        services.AddScoped<IAiProviderAdapter, GeminiProviderAdapter>();
        services.AddScoped<IAiProviderAdapter, AwsBedrockProviderAdapter>();

        return services;
    }
}
