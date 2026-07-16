using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xenia.Application.Assistant;

namespace Xenia.Infrastructure.Assistant;

internal sealed class FakeAssistantProvider : IAssistantProvider
{
    private const string CareConnectGroundingPrefix = "Authorized CareConnect referral context:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex GuidRegex = new(
        "[0-9a-fA-F]{8}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);
    private static readonly Regex RelativeDaysRegex = new(
        "(?:last|past)\\s+(\\d{1,3})\\s+days?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] ReferralSearchKeywords =
    [
        "referral", "referrals", "case", "cases", "client", "patient",
        "provider", "law firm", "lawfirm", "referrer", "organization", "firm"
    ];
    private static readonly string[] SynqLienKeywords =
    [
        "synqlien", "lien", "liens", "case", "cases", "client", "subject",
        "medical", "attorney", "settlement", "advance", "portfolio", "servicing"
    ];
    private static readonly string[] SynqLienCaseKeywords =
    [
        "case", "cases", "client", "claim", "law firm", "lawfirm", "settlement"
    ];
    private static readonly string[] SearchIntentKeywords =
    [
        "find", "search", "show", "lookup", "look up", "match", "which", "where"
    ];

    public Task<string> GetProviderKeyAsync(CancellationToken ct = default)
        => Task.FromResult("fake");

    public async IAsyncEnumerable<AssistantProviderEvent> StreamAsync(
        AssistantProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (request.Purpose == AssistantProviderPurpose.ToolSelection)
        {
            var decision = BuildToolDecisionPayload(request);
            foreach (var chunk in Chunk(decision, 40))
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(10, ct);
                yield return new AssistantProviderEvent("delta", Delta: chunk);
            }

            yield return new AssistantProviderEvent(
                "completed",
                ProviderResponseId: $"fake-{Guid.CreateVersion7()}",
                InputTokens: EstimateTokens(request.Messages.Sum(m => m.Content.Length) + request.SystemPrompt.Length),
                OutputTokens: EstimateTokens(decision.Length),
                FinishReason: "stop");
            yield break;
        }

        var lastUserMessage = request.Messages.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content
            ?? "How can I help?";
        var lastToolMessage = request.Messages.LastOrDefault(m => m.Role.Equals("tool", StringComparison.OrdinalIgnoreCase))?.Content;
        var grounding = request.Messages
            .FirstOrDefault(m => m.Content.StartsWith(CareConnectGroundingPrefix, StringComparison.OrdinalIgnoreCase))
            ?.Content;

        var text = lastToolMessage is not null
            ? $"Grounded response based on current product data:\n\n{CompactToolResult(lastToolMessage)}"
            : grounding is null
                ? $"Xenia {request.AgentKey} is running in fake-provider mode. I received: {lastUserMessage.Trim()}"
                : $"Xenia {request.AgentKey} is running in fake-provider mode. I received: {lastUserMessage.Trim()}\n\nGrounded context:\n{CompactGrounding(grounding)}";

        foreach (var chunk in Chunk(text, 24))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(15, ct);
            yield return new AssistantProviderEvent("delta", Delta: chunk);
        }

        yield return new AssistantProviderEvent(
            "completed",
            ProviderResponseId: $"fake-{Guid.CreateVersion7()}",
            InputTokens: EstimateTokens(request.Messages.Sum(m => m.Content.Length) + request.SystemPrompt.Length),
            OutputTokens: EstimateTokens(text.Length),
            FinishReason: "stop");
    }

    private static IEnumerable<string> Chunk(string value, int size)
    {
        for (var i = 0; i < value.Length; i += size)
            yield return value.Substring(i, Math.Min(size, value.Length - i));
    }

    private static int EstimateTokens(int characters)
        => Math.Max(1, (int)Math.Ceiling(characters / 4.0));

    private static string CompactGrounding(string grounding)
    {
        var compact = grounding.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n\n", "\n", StringComparison.Ordinal)
            .Trim();
        return compact.Length <= 320 ? compact : compact[..320];
    }

    private static string BuildToolDecisionPayload(AssistantProviderRequest request)
    {
        var lastTool = request.Messages.LastOrDefault(m => m.Role.Equals("tool", StringComparison.OrdinalIgnoreCase));
        if (lastTool is not null)
        {
            return JsonSerializer.Serialize(new
            {
                action = "final",
                message = $"Xenia {request.AgentKey} is running in fake-provider mode. I completed a grounded lookup and have tool results available:\n\n{CompactToolResult(lastTool.Content)}",
            }, JsonOptions);
        }

        var lastUserMessage = request.Messages.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content
            ?? "How can I help?";
        var lowered = lastUserMessage.ToLowerInvariant();
        if (IsSynqLienAgentOrIntent(request.AgentKey, lowered))
            return BuildSynqLienToolDecisionPayload(request, lastUserMessage, lowered);

        var contextualReferralId = TryExtractContextualReferralId(request.SystemPrompt, request.ContextJson);
        var explicitReferralId = TryExtractGuid(lastUserMessage);
        var referralId = explicitReferralId ?? contextualReferralId;

        if (lowered.Contains("history") && referralId.HasValue)
        {
            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "careconnect.referral.history.lookup",
                input = new
                {
                    referralId = referralId.Value,
                    top = 10,
                },
            }, JsonOptions);
        }

        if (LooksLikeReferralSummary(lowered))
        {
            var days = TryExtractRelativeDays(lowered);
            var statusGroup = DetectStatusGroup(lowered);
            var status = statusGroup is null ? DetectExactStatus(lowered) : null;

            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "careconnect.referral.queue.summary",
                input = new
                {
                    searchText = lastUserMessage.Trim(),
                    status,
                    statusGroup,
                    days,
                    recentTop = 5,
                },
            }, JsonOptions);
        }

        if (LooksLikeReferralSearch(lowered))
        {
            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "careconnect.referral.search",
                input = new
                {
                    searchText = lastUserMessage.Trim(),
                    top = 6,
                },
            }, JsonOptions);
        }

        if (LooksLikeProviderDirectorySearch(lowered))
        {
            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "careconnect.provider.search",
                input = new
                {
                    name = lastUserMessage.Trim(),
                    top = 6,
                },
            }, JsonOptions);
        }

        if (LooksLikeReferrerDirectorySearch(lowered))
        {
            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "careconnect.referrer.search",
                input = new
                {
                    searchText = lastUserMessage.Trim(),
                    top = 6,
                },
            }, JsonOptions);
        }

        if (referralId.HasValue)
        {
            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "careconnect.referral.lookup",
                input = new
                {
                    referralId = referralId.Value,
                },
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            action = "final",
            message = $"Xenia {request.AgentKey} is running in fake-provider mode. I received: {lastUserMessage.Trim()}",
        }, JsonOptions);
    }

    private static string BuildSynqLienToolDecisionPayload(
        AssistantProviderRequest request,
        string lastUserMessage,
        string lowered)
    {
        var contextualId = TryExtractGuid(request.SystemPrompt) ?? TryExtractGuid(request.ContextJson);
        var explicitId = TryExtractGuid(lastUserMessage);
        var recordId = explicitId ?? contextualId;

        if (LooksLikeSynqLienSummary(lowered))
        {
            var statusGroup = DetectSynqLienStatusGroup(lowered);
            var status = statusGroup is null ? DetectSynqLienStatus(lowered) : null;

            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "synqlien.lien.queue.summary",
                input = new
                {
                    searchText = lastUserMessage.Trim(),
                    status,
                    statusGroup,
                    lienType = DetectSynqLienType(lowered),
                    days = TryExtractRelativeDays(lowered),
                    recentTop = 5,
                },
            }, JsonOptions);
        }

        if (recordId.HasValue && LooksLikeSynqLienCaseLookup(lowered, request.SystemPrompt))
        {
            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "synqlien.case.lookup",
                input = new
                {
                    caseId = recordId.Value,
                    liensTop = 8,
                },
            }, JsonOptions);
        }

        if (recordId.HasValue)
        {
            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "synqlien.lien.lookup",
                input = new
                {
                    lienId = recordId.Value,
                },
            }, JsonOptions);
        }

        if (LooksLikeSynqLienCaseSearch(lowered))
        {
            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "synqlien.case.search",
                input = new
                {
                    searchText = lastUserMessage.Trim(),
                    status = DetectSynqLienCaseStatus(lowered),
                    top = 6,
                },
            }, JsonOptions);
        }

        if (LooksLikeSynqLienLienSearch(lowered))
        {
            var statusGroup = DetectSynqLienStatusGroup(lowered);
            var status = statusGroup is null ? DetectSynqLienStatus(lowered) : null;

            return JsonSerializer.Serialize(new
            {
                action = "tool",
                toolKey = "synqlien.lien.search",
                input = new
                {
                    searchText = lastUserMessage.Trim(),
                    status,
                    statusGroup,
                    lienType = DetectSynqLienType(lowered),
                    top = 6,
                },
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            action = "final",
            message = $"Xenia {request.AgentKey} is running in fake-provider mode. I received: {lastUserMessage.Trim()}",
        }, JsonOptions);
    }

    private static Guid? TryExtractGuid(string value)
    {
        var match = GuidRegex.Match(value);
        return match.Success && Guid.TryParse(match.Value, out var parsed)
            ? parsed
            : null;
    }

    private static Guid? TryExtractContextualReferralId(string systemPrompt, string contextJson)
    {
        var fromPrompt = TryExtractGuid(systemPrompt);
        return fromPrompt ?? TryExtractGuid(contextJson);
    }

    private static string CompactToolResult(string result)
    {
        var compact = result.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n\n", "\n", StringComparison.Ordinal)
            .Trim();
        return compact.Length <= 360 ? compact : compact[..360];
    }

    private static bool LooksLikeReferralSearch(string lowered)
        => SearchIntentKeywords.Any(lowered.Contains) &&
           ReferralSearchKeywords.Any(lowered.Contains);

    private static bool LooksLikeReferralSummary(string lowered)
        => lowered.Contains("queue") ||
           lowered.Contains("summary") ||
           lowered.Contains("how many") ||
           lowered.Contains("count") ||
           lowered.Contains("kpi") ||
           lowered.Contains("metric") ||
           lowered.Contains("total");

    private static bool LooksLikeProviderDirectorySearch(string lowered)
        => lowered.Contains("provider") &&
           !LooksLikeReferralSearch(lowered) &&
           (lowered.Contains("directory") || lowered.Contains("directories") || lowered.Contains("list providers"));

    private static bool LooksLikeReferrerDirectorySearch(string lowered)
        => (lowered.Contains("referrer") || lowered.Contains("law firm") || lowered.Contains("lawfirm")) &&
           !LooksLikeReferralSearch(lowered) &&
           (lowered.Contains("directory") || lowered.Contains("directories") || lowered.Contains("list referrers") || lowered.Contains("list law firms"));

    private static bool IsSynqLienAgentOrIntent(string agentKey, string lowered)
        => agentKey.Equals(AssistantModuleKeys.LiensAgentKey, StringComparison.OrdinalIgnoreCase) ||
           lowered.Contains("synqlien") ||
           lowered.Contains("lien") ||
           lowered.Contains("liens");

    private static bool LooksLikeSynqLienLienSearch(string lowered)
        => SearchIntentKeywords.Any(lowered.Contains) &&
           SynqLienKeywords.Any(lowered.Contains);

    private static bool LooksLikeSynqLienCaseSearch(string lowered)
        => SearchIntentKeywords.Any(lowered.Contains) &&
           SynqLienCaseKeywords.Any(lowered.Contains) &&
           !lowered.Contains("lien");

    private static bool LooksLikeSynqLienCaseLookup(string lowered, string systemPrompt)
        => lowered.Contains("case") ||
           systemPrompt.Contains("synqlien.case.lookup", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSynqLienSummary(string lowered)
        => (lowered.Contains("lien") || lowered.Contains("synqlien")) &&
           (lowered.Contains("queue") ||
            lowered.Contains("summary") ||
            lowered.Contains("how many") ||
            lowered.Contains("count") ||
            lowered.Contains("kpi") ||
            lowered.Contains("metric") ||
            lowered.Contains("total") ||
            lowered.Contains("status mix"));

    private static int? TryExtractRelativeDays(string lowered)
    {
        var match = RelativeDaysRegex.Match(lowered);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
            return parsed;

        return lowered switch
        {
            var value when value.Contains("today") => 1,
            var value when value.Contains("last week") || value.Contains("past week") || value.Contains("this week") => 7,
            var value when value.Contains("last month") || value.Contains("past month") || value.Contains("this month") => 30,
            _ => null,
        };
    }

    private static string? DetectStatusGroup(string lowered)
    {
        if (lowered.Contains("new referral") || lowered.Contains("new referrals") || lowered.Contains("pending referral") || lowered.Contains("pending referrals"))
            return "new";

        if (lowered.Contains("open referral") || lowered.Contains("open referrals") || lowered.Contains("active referral") || lowered.Contains("active referrals"))
            return "open";

        if (lowered.Contains("closed referral") || lowered.Contains("closed referrals"))
            return "closed";

        return null;
    }

    private static string? DetectExactStatus(string lowered)
        => lowered switch
        {
            var value when value.Contains("accepted") => "Accepted",
            var value when value.Contains("in progress") || value.Contains("inprogress") => "InProgress",
            var value when value.Contains("completed") => "Completed",
            var value when value.Contains("declined") => "Declined",
            var value when value.Contains("cancelled") || value.Contains("canceled") => "Cancelled",
            _ => null,
        };

    private static string? DetectSynqLienStatusGroup(string lowered)
    {
        if (lowered.Contains("draft") || lowered.Contains("new lien") || lowered.Contains("new liens"))
            return "draft";

        if (lowered.Contains("open lien") || lowered.Contains("open liens") || lowered.Contains("active lien") || lowered.Contains("active liens"))
            return "open";

        if (lowered.Contains("closed lien") || lowered.Contains("closed liens") || lowered.Contains("terminal lien") || lowered.Contains("terminal liens"))
            return "closed";

        if (lowered.Contains("marketplace") || lowered.Contains("for sale") || lowered.Contains("selling"))
            return "marketplace";

        if (lowered.Contains("servicing") || lowered.Contains("service"))
            return "servicing";

        return null;
    }

    private static string? DetectSynqLienStatus(string lowered)
        => lowered switch
        {
            var value when value.Contains("offered") => "Offered",
            var value when value.Contains("under review") || value.Contains("underreview") => "UnderReview",
            var value when value.Contains("sold") => "Sold",
            var value when value.Contains("active") => "Active",
            var value when value.Contains("settled") => "Settled",
            var value when value.Contains("withdrawn") => "Withdrawn",
            var value when value.Contains("cancelled") || value.Contains("canceled") => "Cancelled",
            var value when value.Contains("disputed") => "Disputed",
            var value when value.Contains("draft") => "Draft",
            _ => null,
        };

    private static string? DetectSynqLienCaseStatus(string lowered)
        => lowered switch
        {
            var value when value.Contains("pre demand") || value.Contains("predemand") => "PreDemand",
            var value when value.Contains("demand sent") || value.Contains("demandsent") => "DemandSent",
            var value when value.Contains("negotiation") => "InNegotiation",
            var value when value.Contains("settled") => "CaseSettled",
            var value when value.Contains("closed") => "Closed",
            _ => null,
        };

    private static string? DetectSynqLienType(string lowered)
        => lowered switch
        {
            var value when value.Contains("medical") => "MedicalLien",
            var value when value.Contains("attorney") => "AttorneyLien",
            var value when value.Contains("settlement advance") || value.Contains("advance") => "SettlementAdvance",
            var value when value.Contains("workers comp") || value.Contains("workerscomp") => "WorkersCompLien",
            var value when value.Contains("property") => "PropertyLien",
            _ => null,
        };
}
