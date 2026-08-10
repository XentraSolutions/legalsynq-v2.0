using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xenia.Application.Assistant;
using Xenia.Domain.Configuration;
using Xenia.Infrastructure.Assistant;
using Xenia.Infrastructure.Persistence;
using Xunit;

namespace Xenia.Tests.Assistant;

public sealed class AssistantRuntimeSettingsServiceTests : IDisposable
{
    private readonly XeniaDbContext _db;

    public AssistantRuntimeSettingsServiceTests()
    {
        var options = new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new XeniaDbContext(options);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_UsesAppSettingsFallbacks_WhenNoOverridesExist()
    {
        var sut = CreateSut();

        var settings = await sut.GetEffectiveSettingsAsync(null);

        Assert.Equal("Fake", settings.Provider);
        Assert.Equal("xenia-fake", settings.ModelKey);
        Assert.Equal("https://api.openai.com", settings.OpenAiBaseUrl);
        Assert.Equal("appsettings-openai-key", settings.OpenAiApiKey);
        Assert.Equal(60, settings.OpenAiTimeoutSeconds);
        Assert.Null(settings.OpenAiReasoningEffort);
        Assert.Null(settings.OpenAiTextVerbosity);
        Assert.Null(settings.OpenAiMaxOutputTokens);
        Assert.Null(settings.LastUpdatedAtUtc);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_AppliesGlobalOverrides_WhileKeepingAppSettingsApiKey()
    {
        Seed(ScopeType.Global, null, AssistantConfigurationKeys.Provider, "OpenAI");
        Seed(ScopeType.Global, null, AssistantConfigurationKeys.ModelKey, "gpt-4.1-mini");
        Seed(ScopeType.Global, null, AssistantConfigurationKeys.OpenAiBaseUrl, "https://example.openai.test");
        Seed(ScopeType.Global, null, AssistantConfigurationKeys.OpenAiTimeoutSeconds, "120");
        Seed(ScopeType.Global, null, AssistantConfigurationKeys.OpenAiReasoningEffort, "high");
        Seed(ScopeType.Global, null, AssistantConfigurationKeys.OpenAiTextVerbosity, "medium");
        Seed(ScopeType.Global, null, AssistantConfigurationKeys.OpenAiMaxOutputTokens, "4096");
        await _db.SaveChangesAsync();

        var sut = CreateSut();

        var settings = await sut.GetEffectiveSettingsAsync(null);

        Assert.Equal("OpenAI", settings.Provider);
        Assert.Equal("gpt-4.1-mini", settings.ModelKey);
        Assert.Equal("https://example.openai.test", settings.OpenAiBaseUrl);
        Assert.Equal("appsettings-openai-key", settings.OpenAiApiKey);
        Assert.Equal(120, settings.OpenAiTimeoutSeconds);
        Assert.Equal("high", settings.OpenAiReasoningEffort);
        Assert.Equal("medium", settings.OpenAiTextVerbosity);
        Assert.Equal(4096, settings.OpenAiMaxOutputTokens);
        Assert.NotNull(settings.LastUpdatedAtUtc);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_TenantOverridesWinOverGlobalOverrides()
    {
        var tenantId = Guid.CreateVersion7();

        Seed(ScopeType.Global, null, AssistantConfigurationKeys.Provider, "Fake");
        Seed(ScopeType.Global, null, AssistantConfigurationKeys.ModelKey, "xenia-fake");
        Seed(ScopeType.Global, null, AssistantConfigurationKeys.OpenAiReasoningEffort, "low");
        Seed(ScopeType.Tenant, tenantId.ToString(), AssistantConfigurationKeys.Provider, "OpenAI");
        Seed(ScopeType.Tenant, tenantId.ToString(), AssistantConfigurationKeys.ModelKey, "gpt-4.1");
        Seed(ScopeType.Tenant, tenantId.ToString(), AssistantConfigurationKeys.OpenAiReasoningEffort, "high");
        await _db.SaveChangesAsync();

        var sut = CreateSut();

        var settings = await sut.GetEffectiveSettingsAsync(tenantId);

        Assert.Equal("OpenAI", settings.Provider);
        Assert.Equal("gpt-4.1", settings.ModelKey);
        Assert.Equal("appsettings-openai-key", settings.OpenAiApiKey);
        Assert.Equal("high", settings.OpenAiReasoningEffort);
    }

    private AssistantRuntimeSettingsService CreateSut()
        => new(
            _db,
            new TestOptionsSnapshot<XeniaAssistantOptions>(
                new XeniaAssistantOptions
                {
                    Provider = "Fake",
                    ModelKey = "xenia-fake",
                    OpenAI = new XeniaAssistantOptions.OpenAiOptions
                    {
                        BaseUrl = "https://api.openai.com",
                        ApiKey = "appsettings-openai-key",
                        TimeoutSeconds = 60,
                        ReasoningEffort = null,
                        TextVerbosity = null,
                        MaxOutputTokens = null,
                    },
                }));

    private void Seed(
        ScopeType scopeType,
        string? scopeId,
        string key,
        string? value,
        bool isSecret = false)
    {
        _db.ConfigurationEntries.Add(new XeniaConfigurationEntry(
            Guid.CreateVersion7(),
            scopeType,
            scopeId,
            AssistantModuleKeys.ConfigurationNamespace,
            key,
            value,
            isSecret: isSecret));
    }

    public void Dispose() => _db.Dispose();

    private sealed class TestOptionsSnapshot<T>(T value) : IOptionsSnapshot<T>
        where T : class
    {
        public T Value { get; } = value;

        public T Get(string? name) => Value;
    }
}
