using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xenia.Application.Assistant;
using Xenia.Domain.Configuration;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Assistant;

internal sealed class AssistantRuntimeSettingsService : IAssistantRuntimeSettingsService
{
    private readonly XeniaDbContext _db;
    private readonly IOptionsSnapshot<XeniaAssistantOptions> _defaults;
    private readonly Dictionary<string, AssistantRuntimeSettings> _cache = [];

    public AssistantRuntimeSettingsService(
        XeniaDbContext db,
        IOptionsSnapshot<XeniaAssistantOptions> defaults)
    {
        _db = db;
        _defaults = defaults;
    }

    public async Task<AssistantRuntimeSettings> GetEffectiveSettingsAsync(
        Guid? tenantId,
        CancellationToken ct = default)
    {
        var cacheKey = tenantId?.ToString() ?? "global";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var defaults = _defaults.Value;
        var tenantScopeId = tenantId?.ToString();

        var entries = await _db.ConfigurationEntries
            .AsNoTracking()
            .Where(e =>
                e.Namespace == AssistantModuleKeys.ConfigurationNamespace &&
                AssistantConfigurationKeys.All.Contains(e.ConfigurationKey) &&
                (e.ScopeType == ScopeType.Global ||
                 (tenantScopeId != null && e.ScopeType == ScopeType.Tenant && e.ScopeId == tenantScopeId)))
            .ToListAsync(ct);

        var providerEntry = ResolveEntry(entries, tenantScopeId, AssistantConfigurationKeys.Provider);
        var modelKeyEntry = ResolveEntry(entries, tenantScopeId, AssistantConfigurationKeys.ModelKey);
        var baseUrlEntry = ResolveEntry(entries, tenantScopeId, AssistantConfigurationKeys.OpenAiBaseUrl);
        var timeoutEntry = ResolveEntry(entries, tenantScopeId, AssistantConfigurationKeys.OpenAiTimeoutSeconds);
        var reasoningEffortEntry = ResolveEntry(entries, tenantScopeId, AssistantConfigurationKeys.OpenAiReasoningEffort);
        var textVerbosityEntry = ResolveEntry(entries, tenantScopeId, AssistantConfigurationKeys.OpenAiTextVerbosity);
        var maxOutputTokensEntry = ResolveEntry(entries, tenantScopeId, AssistantConfigurationKeys.OpenAiMaxOutputTokens);

        var settings = new AssistantRuntimeSettings(
            Provider: FirstNonBlank(providerEntry?.ConfigurationValue, defaults.Provider, "Fake"),
            ModelKey: FirstNonBlank(modelKeyEntry?.ConfigurationValue, defaults.ModelKey, "xenia-fake"),
            OpenAiBaseUrl: FirstNonBlank(baseUrlEntry?.ConfigurationValue, defaults.OpenAI.BaseUrl, "https://api.openai.com"),
            OpenAiApiKey: FirstNonBlank(defaults.OpenAI.ApiKey),
            OpenAiTimeoutSeconds: ParsePositiveInt(timeoutEntry?.ConfigurationValue, defaults.OpenAI.TimeoutSeconds, 60),
            OpenAiReasoningEffort: NormalizeReasoningEffort(reasoningEffortEntry?.ConfigurationValue, defaults.OpenAI.ReasoningEffort),
            OpenAiTextVerbosity: NormalizeTextVerbosity(textVerbosityEntry?.ConfigurationValue, defaults.OpenAI.TextVerbosity),
            OpenAiMaxOutputTokens: ParseNullablePositiveInt(maxOutputTokensEntry?.ConfigurationValue, defaults.OpenAI.MaxOutputTokens),
            LastUpdatedAtUtc: MaxUpdatedAtUtc(
                providerEntry,
                modelKeyEntry,
                baseUrlEntry,
                timeoutEntry,
                reasoningEffortEntry,
                textVerbosityEntry,
                maxOutputTokensEntry));

        _cache[cacheKey] = settings;
        return settings;
    }

    private static XeniaConfigurationEntry? ResolveEntry(
        IReadOnlyList<XeniaConfigurationEntry> entries,
        string? tenantScopeId,
        string key)
    {
        if (tenantScopeId is not null)
        {
            var tenantEntry = entries.FirstOrDefault(e =>
                e.ScopeType == ScopeType.Tenant &&
                e.ScopeId == tenantScopeId &&
                e.ConfigurationKey.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (tenantEntry is not null)
                return tenantEntry;
        }

        return entries.FirstOrDefault(e =>
            e.ScopeType == ScopeType.Global &&
            e.ConfigurationKey.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static int ParsePositiveInt(string? value, params int[] fallbacks)
    {
        if (int.TryParse(value, out var parsed) && parsed > 0)
            return parsed;

        return fallbacks.FirstOrDefault(candidate => candidate > 0, 60);
    }

    private static int? ParseNullablePositiveInt(string? value, params int?[] fallbacks)
    {
        if (int.TryParse(value, out var parsed) && parsed > 0)
            return parsed;

        return fallbacks.FirstOrDefault(candidate => candidate.HasValue && candidate.Value > 0);
    }

    private static string? NormalizeReasoningEffort(params string?[] values)
    {
        foreach (var value in values)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            if (normalized is "minimal" or "low" or "medium" or "high")
                return normalized;
        }

        return null;
    }

    private static string? NormalizeTextVerbosity(params string?[] values)
    {
        foreach (var value in values)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            if (normalized is "low" or "medium" or "high")
                return normalized;
        }

        return null;
    }

    private static DateTime? MaxUpdatedAtUtc(params XeniaConfigurationEntry?[] entries)
    {
        var updated = entries
            .Where(entry => entry is not null)
            .Select(entry => entry!.UpdatedAtUtc)
            .ToList();

        return updated.Count == 0 ? null : updated.Max();
    }
}
