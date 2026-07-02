using Microsoft.Extensions.Configuration;

namespace CareConnect.Application.DTOs;

public sealed class ReferralRuntimeOptions
{
    public const string DevFallbackSecret = "LEGALSYNQ-DEV-REFERRAL-TOKEN-SECRET-2026";

    public string TokenSecret { get; init; } = string.Empty;
    public string AppBaseUrl { get; init; } = "http://localhost:3000";
    public string AppBaseDomain { get; init; } = string.Empty;
    public bool UsingDevelopmentFallbackSecret { get; init; }

    public static ReferralRuntimeOptions FromConfiguration(IConfiguration configuration)
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var isDevelopment = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
        var configuredSecret = configuration["ReferralToken:Secret"];

        if (string.IsNullOrWhiteSpace(configuredSecret) && !isDevelopment)
        {
            throw new InvalidOperationException(
                "ReferralToken:Secret must be configured in non-Development environments. " +
                "Set the 'ReferralToken:Secret' configuration key to a strong random value. " +
                $"Current environment: '{environment}'.");
        }

        return new ReferralRuntimeOptions
        {
            TokenSecret = string.IsNullOrWhiteSpace(configuredSecret) ? DevFallbackSecret : configuredSecret,
            AppBaseUrl = (configuration["AppBaseUrl"] ?? "http://localhost:3000").TrimEnd('/'),
            AppBaseDomain = (configuration["AppBaseDomain"] ?? string.Empty).Trim().TrimStart('.'),
            UsingDevelopmentFallbackSecret = string.IsNullOrWhiteSpace(configuredSecret),
        };
    }
}
