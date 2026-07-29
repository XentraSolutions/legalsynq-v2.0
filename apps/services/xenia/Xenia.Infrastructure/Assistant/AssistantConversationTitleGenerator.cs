using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xenia.Application.Assistant;
using Xenia.Application.TenantContext;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Assistant;

internal interface IAssistantConversationTitleGenerator
{
    void QueueTitleGeneration(AssistantConversationTitleGenerationRequest request);
}

internal sealed record AssistantConversationTitleGenerationRequest(
    Guid ConversationId,
    Guid TenantId,
    Guid ActorId,
    string AgentKey,
    string AgentVersion,
    string AgentName,
    string FirstUserPrompt,
    string ContextJson,
    string ProviderModelKey,
    string? CorrelationId);

internal sealed class BackgroundAssistantConversationTitleGenerator : IAssistantConversationTitleGenerator
{
    private static readonly TimeSpan TitleGenerationTimeout = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundAssistantConversationTitleGenerator> _logger;

    public BackgroundAssistantConversationTitleGenerator(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundAssistantConversationTitleGenerator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void QueueTitleGeneration(AssistantConversationTitleGenerationRequest request)
        => _ = Task.Run(() => GenerateAndSaveTitleAsync(request));

    private async Task GenerateAndSaveTitleAsync(AssistantConversationTitleGenerationRequest request)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TitleGenerationTimeout);
            using var scope = _scopeFactory.CreateScope();

            var tenantAccessor = scope.ServiceProvider.GetRequiredService<XeniaTenantContextAccessor>();
            tenantAccessor.Set(new BackgroundTenantContext(
                request.TenantId,
                request.ActorId,
                request.CorrelationId));

            var provider = scope.ServiceProvider.GetRequiredService<IAssistantProvider>();
            var titleRequest = new AssistantProviderRequest(
                request.AgentKey,
                request.AgentVersion,
                AssistantConversationTitlePolicy.BuildTitlePrompt(),
                request.ProviderModelKey,
                [new AssistantProviderMessage("user", request.FirstUserPrompt)],
                request.ContextJson,
                request.CorrelationId ?? string.Empty,
                AssistantProviderPurpose.TitleGeneration);

            var result = await CollectProviderTextAsync(provider, titleRequest, timeout.Token);
            if (!string.IsNullOrWhiteSpace(result.SafeError))
            {
                _logger.LogDebug(
                    "Assistant conversation title provider failed; falling back to prompt title. conversationId={ConversationId} error={SafeError}",
                    request.ConversationId,
                    result.SafeError);
            }

            var title = AssistantConversationTitlePolicy.TryCleanProviderTitle(result.Text)
                ?? AssistantConversationTitlePolicy.BuildFallbackTitle(request.FirstUserPrompt);

            if (string.IsNullOrWhiteSpace(title)) return;

            var db = scope.ServiceProvider.GetRequiredService<XeniaDbContext>();
            var conversation = await db.AssistantConversations
                .FirstOrDefaultAsync(
                    c => c.Id == request.ConversationId &&
                         c.TenantId == request.TenantId &&
                         c.ActorId == request.ActorId,
                    timeout.Token);

            if (conversation is null ||
                !AssistantConversationTitlePolicy.IsGeneratedTitle(conversation.Title, request.AgentName))
            {
                return;
            }

            conversation.Rename(title);
            await db.SaveChangesAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug(
                "Assistant conversation title generation timed out. conversationId={ConversationId}",
                request.ConversationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Assistant conversation title generation failed. conversationId={ConversationId}",
                request.ConversationId);
        }
    }

    private static async Task<ProviderTextResult> CollectProviderTextAsync(
        IAssistantProvider provider,
        AssistantProviderRequest request,
        CancellationToken ct)
    {
        var chunks = new List<string>();
        string? safeError = null;

        await foreach (var evt in provider.StreamAsync(request, ct).WithCancellation(ct))
        {
            if (evt.Type == "delta" && !string.IsNullOrEmpty(evt.Delta))
            {
                chunks.Add(evt.Delta);
                continue;
            }

            if (evt.Type == "error")
            {
                safeError = evt.SafeError ?? "Assistant provider failed.";
                break;
            }
        }

        return new ProviderTextResult(string.Concat(chunks).Trim(), safeError);
    }

    private sealed record ProviderTextResult(string Text, string? SafeError);

    private sealed record BackgroundTenantContext(
        Guid TenantId,
        Guid ActorId,
        string? CorrelationId) : IXeniaTenantContext
    {
        public bool IsResolved => true;
        public string? TenantCode => null;
        Guid? IXeniaTenantContext.ActorId => ActorId;
    }
}

internal static class AssistantConversationTitlePolicy
{
    private const int GeneratedTitleMaxLength = 60;
    private const string GenericAssistantConversationTitle = "New Assistant Conversation";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingCourtesyRegex = new(
        @"^(?:please\s+)?(?:(?:can|could|would)\s+you\s+|i\s+(?:need|want)\s+(?:you\s+to\s+)?|help\s+me\s+(?:with|to)\s+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LeadingCommandRegex = new(
        @"^(?:show\s+me|give\s+me|pull\s+up|look\s+up|search\s+for|generate|create|make|build|prepare|list)\s+(?:a|an|the|all)?\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SummaryIntentRegex = new(
        @"^(?:summari[sz]e|summary\s+of)\s+(?:(?:this|the|a|an)\s+)?(?<topic>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> LowercaseTitleWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "as", "at", "by", "for", "from", "in", "of", "on", "or", "the", "to", "with"
    };

    public static bool IsGeneratedTitle(string title, string agentName)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;

        var trimmed = title.Trim();
        return trimmed.Equals(GenericAssistantConversationTitle, StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("New conversation", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals($"New {agentName} conversation", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildTitlePrompt()
        => """
           Generate a concise conversation title for chat history from the user's first message.
           Return only the title.

           Rules:
           - Use 2 to 6 words.
           - Use title case.
           - Describe the main topic or requested task.
           - Do not include quotation marks.
           - Do not end with punctuation.
           - Do not use generic words like chat, conversation, assistant, or request.
           """;

    public static string? TryCleanProviderTitle(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return null;

        var title = TryExtractTitleFromJson(rawTitle) ?? rawTitle;
        title = NormalizeTitleText(title.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(title)) return null;

        return ToDisplayTitle(LimitGeneratedTitle(title));
    }

    public static string BuildFallbackTitle(string prompt)
    {
        var normalized = NormalizeTitleText(prompt);
        if (string.IsNullOrWhiteSpace(normalized)) return "Assistant Conversation";

        var knownTitle = TryBuildKnownFallbackTitle(normalized);
        if (!string.IsNullOrWhiteSpace(knownTitle))
            return ToDisplayTitle(LimitGeneratedTitle(knownTitle));

        var candidate = LeadingCourtesyRegex.Replace(normalized, string.Empty);
        candidate = LeadingCommandRegex.Replace(candidate, string.Empty);

        var summaryMatch = SummaryIntentRegex.Match(candidate);
        if (summaryMatch.Success)
        {
            var topic = NormalizeTitleText(summaryMatch.Groups["topic"].Value);
            if (!string.IsNullOrWhiteSpace(topic))
                candidate = $"{topic} summary";
        }

        candidate = NormalizeTitleText(candidate);
        if (string.IsNullOrWhiteSpace(candidate)) candidate = normalized;

        return ToDisplayTitle(LimitGeneratedTitle(candidate));
    }

    private static string? TryExtractTitleFromJson(string rawTitle)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawTitle);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                return doc.RootElement.GetString();

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("title", out var titleElement) &&
                titleElement.ValueKind == JsonValueKind.String)
            {
                return titleElement.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? TryBuildKnownFallbackTitle(string normalizedPrompt)
    {
        if (normalizedPrompt.Contains("total liens", StringComparison.OrdinalIgnoreCase))
            return "total liens";

        if (normalizedPrompt.Contains("funded amount", StringComparison.OrdinalIgnoreCase) &&
            normalizedPrompt.Contains("report", StringComparison.OrdinalIgnoreCase))
            return "funded amount report";

        if (normalizedPrompt.Contains("settlement", StringComparison.OrdinalIgnoreCase) &&
            (normalizedPrompt.Contains("summarize", StringComparison.OrdinalIgnoreCase) ||
             normalizedPrompt.Contains("summarise", StringComparison.OrdinalIgnoreCase) ||
             normalizedPrompt.Contains("summary", StringComparison.OrdinalIgnoreCase)))
        {
            return "settlement summary";
        }

        if (normalizedPrompt.Contains("processing", StringComparison.OrdinalIgnoreCase) &&
            normalizedPrompt.Contains("case", StringComparison.OrdinalIgnoreCase))
        {
            return "processing cases";
        }

        return null;
    }

    private static string NormalizeTitleText(string value)
        => WhitespaceRegex.Replace(value, " ")
            .Trim()
            .Trim(' ', '\'', '"', '`', '?', '.', '!', ':', ';', ',', '-');

    private static string LimitGeneratedTitle(string value)
    {
        if (value.Length <= GeneratedTitleMaxLength) return value;

        var clipped = value[..GeneratedTitleMaxLength].TrimEnd();
        var lastSpace = clipped.LastIndexOf(' ');
        if (lastSpace >= 24)
            clipped = clipped[..lastSpace];

        return clipped.TrimEnd(' ', ',', ';', ':', '-');
    }

    private static string ToDisplayTitle(string value)
    {
        var words = NormalizeTitleText(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0) return "Assistant Conversation";

        for (var i = 0; i < words.Length; i++)
        {
            words[i] = FormatTitleWord(words[i], i == 0);
        }

        return string.Join(' ', words);
    }

    private static string FormatTitleWord(string word, bool isFirstWord)
    {
        if (word.All(c => !char.IsLetter(c))) return word;

        var lowered = word.ToLowerInvariant();
        if (!isFirstWord && LowercaseTitleWords.Contains(lowered))
            return lowered;

        var letterCount = 0;
        var allLettersUpper = true;
        foreach (var c in word)
        {
            if (!char.IsLetter(c)) continue;
            letterCount++;
            if (!char.IsUpper(c)) allLettersUpper = false;
        }

        if (letterCount is > 1 and <= 5 && allLettersUpper)
            return word;

        return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
    }
}
