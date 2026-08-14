using Intake.Application.Classification;
using Intake.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Intake.Infrastructure.Classification;

public sealed class ConfiguredSynqAiProviderRegistry(
    IEnumerable<ISynqAiProvider> providers) : ISynqAiProviderRegistry
{
    private readonly IReadOnlyDictionary<string, ISynqAiProvider> providers =
        providers.ToDictionary(provider => provider.ProviderCode, StringComparer.Ordinal);

    public IReadOnlyList<string> AvailableProviderCodes =>
        providers.Values
            .Where(provider => provider.IsConfigured)
            .Select(provider => provider.ProviderCode)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

    public ISynqAiProvider GetRequired(string providerCode)
    {
        if (!providers.TryGetValue(providerCode.Trim().ToUpperInvariant(), out var provider) ||
            !provider.IsConfigured)
        {
            throw IntakeConfigurationException.BadRequest(
                Domain.Classification.ClassificationFailureCodes.ProviderUnavailable,
                $"AI provider '{providerCode}' is not configured or supported.");
        }

        return provider;
    }
}