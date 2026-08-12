using System.Runtime.CompilerServices;
using Xenia.Application.Assistant;
using Xenia.Application.TenantContext;

namespace Xenia.Infrastructure.Assistant;

internal sealed class ConfiguredAssistantProvider : IAssistantProvider
{
    private readonly FakeAssistantProvider _fake;
    private readonly OpenAiAssistantProvider _openAi;
    private readonly IAssistantRuntimeSettingsService _settings;
    private readonly XeniaTenantContextAccessor _tenantAccessor;

    public ConfiguredAssistantProvider(
        FakeAssistantProvider fake,
        OpenAiAssistantProvider openAi,
        IAssistantRuntimeSettingsService settings,
        XeniaTenantContextAccessor tenantAccessor)
    {
        _fake = fake;
        _openAi = openAi;
        _settings = settings;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<string> GetProviderKeyAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        return settings.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
            ? "openai"
            : "fake";
    }

    public async IAsyncEnumerable<AssistantProviderEvent> StreamAsync(
        AssistantProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        var provider = settings.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
            ? _openAi.StreamAsync(request, ct)
            : _fake.StreamAsync(request, ct);

        await foreach (var evt in provider.WithCancellation(ct))
            yield return evt;
    }

    private Task<AssistantRuntimeSettings> GetSettingsAsync(CancellationToken ct)
        => _settings.GetEffectiveSettingsAsync(
            _tenantAccessor.Current?.IsResolved == true
                ? _tenantAccessor.Current.TenantId
                : null,
            ct);
}
