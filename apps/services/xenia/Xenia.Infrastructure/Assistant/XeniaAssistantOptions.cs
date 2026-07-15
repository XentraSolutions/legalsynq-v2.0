namespace Xenia.Infrastructure.Assistant;

public sealed class XeniaAssistantOptions
{
    public const string SectionName = "XeniaAssistant";

    public string Provider { get; set; } = "Fake";
    public string ModelKey { get; set; } = "xenia-fake";
    public int MaxPromptCharacters { get; set; } = 8000;
    public int MaxConversationMessages { get; set; } = 40;
    public int MaxToolIterations { get; set; } = 4;
    public int? MonthlyRequestLimit { get; set; }
    public int? MonthlyTokenLimit { get; set; }
    public OpenAiOptions OpenAI { get; set; } = new();
    public CareConnectOptions CareConnect { get; set; } = new();

    public sealed class OpenAiOptions
    {
        public string BaseUrl { get; set; } = "https://api.openai.com";
        public string? ApiKey { get; set; }
        public int TimeoutSeconds { get; set; } = 60;
        public string? ReasoningEffort { get; set; }
        public string? TextVerbosity { get; set; }
        public int? MaxOutputTokens { get; set; }
    }

    public sealed class CareConnectOptions
    {
        public string BaseUrl { get; set; } = "http://127.0.0.1:5003";
        public int TimeoutSeconds { get; set; } = 20;
        public int MaxHistoryItems { get; set; } = 5;
    }
}
