using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Assistant;
using Xenia.Application.TenantContext;
using Xenia.Domain.Assistant;
using Xenia.Infrastructure.Observability;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Assistant;

internal sealed class EfAssistantService : IAssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly XeniaDbContext _db;
    private readonly XeniaTenantContextAccessor _tenantAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAssistantToolRegistry _toolRegistry;
    private readonly IAssistantToolExecutor _toolExecutor;
    private readonly IAssistantProvider _provider;
    private readonly IAssistantRuntimeSettingsService _runtimeSettings;
    private readonly IOptions<XeniaAssistantOptions> _options;
    private readonly XeniaMetrics _metrics;
    private readonly ILogger<EfAssistantService> _logger;

    public EfAssistantService(
        XeniaDbContext db,
        XeniaTenantContextAccessor tenantAccessor,
        IHttpContextAccessor httpContextAccessor,
        IAssistantToolRegistry toolRegistry,
        IAssistantToolExecutor toolExecutor,
        IAssistantProvider provider,
        IAssistantRuntimeSettingsService runtimeSettings,
        IOptions<XeniaAssistantOptions> options,
        XeniaMetrics metrics,
        ILogger<EfAssistantService> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _httpContextAccessor = httpContextAccessor;
        _toolRegistry = toolRegistry;
        _toolExecutor = toolExecutor;
        _provider = provider;
        _runtimeSettings = runtimeSettings;
        _options = options;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<AssistantBootstrapDto> GetBootstrapAsync(CancellationToken ct = default)
    {
        var (tenantId, actorId, _) = RequireContext();
        var providerKey = await _provider.GetProviderKeyAsync(ct);
        return new AssistantBootstrapDto(
            Enabled: true,
            Agents: await ListAgentsAsync(ct),
            Preferences: await GetPreferencesAsync(ct),
            Usage: await GetUsageSummaryAsync(tenantId, actorId, ct),
            FeatureFlags: new Dictionary<string, string>
            {
                ["streaming"] = "enabled",
                ["provider"] = providerKey,
                ["tool_runtime"] = "orchestrated",
            });
    }

    public async Task<IReadOnlyList<AssistantAgentDto>> ListAgentsAsync(CancellationToken ct = default)
    {
        var (tenantId, _, _) = RequireContext();
        var agents = await QueryAllowedAgentsAsync(tenantId, ct);
        return agents.Select(ToAgentDto).ToList();
    }

    public async Task<IReadOnlyList<AssistantConversationSummaryDto>> ListConversationsAsync(CancellationToken ct = default)
    {
        var (tenantId, actorId, _) = RequireContext();
        var conversations = await _db.AssistantConversations
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ActorId == actorId && c.Status == AssistantConversationStatus.Active)
            .OrderByDescending(c => c.LastMessageAtUtc ?? c.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        return conversations.Select(ToConversationSummaryDto).ToList();
    }

    public async Task<AssistantConversationDto> CreateConversationAsync(
        CreateAssistantConversationRequest request,
        CancellationToken ct = default)
    {
        var (tenantId, actorId, _) = RequireContext();
        var agent = await ResolveAgentAsync(tenantId, request.AgentKey, ct);
        var title = SafeTitle(request.Title) ?? $"New {agent.Name} conversation";
        var source = string.IsNullOrWhiteSpace(request.Source) ? "page" : request.Source.Trim();

        var conversation = new AssistantConversation(
            Guid.CreateVersion7(),
            tenantId,
            actorId,
            agent.AgentKey,
            agent.Version,
            title,
            source,
            SafeJsonObject(request.ContextJson));

        _db.AssistantConversations.Add(conversation);
        await _db.SaveChangesAsync(ct);

        _metrics.AssistantConversationsCreated.Add(1, KeyValuePair.Create<string, object?>("agent_key", agent.AgentKey));
        return await GetConversationAsync(conversation.Id, ct)
            ?? throw new InvalidOperationException("Conversation was created but could not be loaded.");
    }

    public async Task<AssistantConversationDto?> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var (tenantId, actorId, _) = RequireContext();
        var conversation = await _db.AssistantConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.TenantId == tenantId && c.ActorId == actorId, ct);

        if (conversation is null) return null;

        var messages = await _db.AssistantMessages
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAtMessageUtc)
            .ToListAsync(ct);

        var messageIds = messages.Select(m => m.Id).ToList();
        var citations = await _db.AssistantMessageCitations
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && messageIds.Contains(c.MessageId))
            .ToListAsync(ct);

        return ToConversationDto(conversation, messages, citations);
    }

    public async Task<AssistantConversationDto?> UpdateConversationAsync(
        Guid conversationId,
        UpdateAssistantConversationRequest request,
        CancellationToken ct = default)
    {
        var (tenantId, actorId, _) = RequireContext();
        var conversation = await _db.AssistantConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.TenantId == tenantId && c.ActorId == actorId, ct);

        if (conversation is null) return null;
        if (!string.IsNullOrWhiteSpace(request.Title)) conversation.Rename(request.Title);
        if (request.Archived == true) conversation.Archive(DateTime.UtcNow);

        await _db.SaveChangesAsync(ct);
        return await GetConversationAsync(conversationId, ct);
    }

    public async Task<bool> ArchiveConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var updated = await UpdateConversationAsync(conversationId, new UpdateAssistantConversationRequest(null, true), ct);
        return updated is not null;
    }

    public async Task<AssistantMessageDto> CreateMessageAsync(
        Guid conversationId,
        CreateAssistantMessageRequest request,
        CancellationToken ct = default)
    {
        AssistantMessageDto? completed = null;
        await foreach (var evt in StreamMessageAsync(conversationId, request, ct))
        {
            if (evt.Message?.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) == true)
                completed = evt.Message;
            if (evt.Type == "error")
                throw new InvalidOperationException(evt.Error ?? "Assistant response failed.");
        }

        return completed ?? throw new InvalidOperationException("Assistant did not produce a response.");
    }

    public async IAsyncEnumerable<AssistantStreamEventDto> StreamMessageAsync(
        Guid conversationId,
        CreateAssistantMessageRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var (tenantId, actorId, correlationId) = RequireContext();
        var options = _options.Value;
        var runtimeSettings = await _runtimeSettings.GetEffectiveSettingsAsync(tenantId, ct);
        var providerKey = await _provider.GetProviderKeyAsync(ct);

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Message content is required.", nameof(request));

        var content = request.Content.Trim();
        if (content.Length > Math.Max(1000, options.MaxPromptCharacters))
            throw new ArgumentException($"Message content must be {options.MaxPromptCharacters} characters or fewer.", nameof(request));

        var providerModelKey = string.IsNullOrWhiteSpace(runtimeSettings.ModelKey)
            ? "xenia-fake"
            : runtimeSettings.ModelKey;

        var conversation = await _db.AssistantConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.TenantId == tenantId && c.ActorId == actorId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' was not found.");

        if (conversation.Status != AssistantConversationStatus.Active)
            throw new InvalidOperationException("Archived conversations cannot receive new messages.");

        var agent = await ResolveAgentAsync(tenantId, conversation.AgentKey, ct);
        await EnforceQuotaAsync(tenantId, actorId, ct);

        var priorMessages = await _db.AssistantMessages
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAtMessageUtc)
            .Take(Math.Max(1, options.MaxConversationMessages))
            .ToListAsync(ct);

        priorMessages.Reverse();

        var userMessage = new AssistantMessage(
            Guid.CreateVersion7(),
            conversationId,
            tenantId,
            actorId,
            AssistantMessageRole.User,
            content,
            "user",
            null,
            SafeJsonObject(request.ContextJson));

        _db.AssistantMessages.Add(userMessage);
        conversation.Touch(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);

        yield return new AssistantStreamEventDto("user_message", null, ToMessageDto(userMessage, []), null);

        var mergedContextJson = MergeContextJson(conversation.ContextJson, request.ContextJson);
        var toolDefinitions = _toolRegistry.ListToolsForAgent(agent.AgentKey);
        var contextualToolHint = TryBuildContextualToolHint(mergedContextJson);
        var workingMessages = new List<AssistantMessage>(priorMessages) { userMessage };
        var toolRuns = new List<AssistantToolRun>();
        var assistantContent = string.Empty;
        string? providerResponseId = null;
        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        string? finishReason = null;
        string? providerError = null;
        string? plannedFinalMessage = null;
        var shouldStreamFinalAnswer = false;

        for (var iteration = 0; iteration < Math.Max(1, options.MaxToolIterations); iteration++)
        {
            var planRequest = new AssistantProviderRequest(
                agent.AgentKey,
                agent.Version,
                BuildToolSelectionPrompt(agent.SystemPrompt, toolDefinitions, contextualToolHint),
                providerModelKey,
                BuildProviderMessages(workingMessages),
                mergedContextJson,
                correlationId ?? string.Empty,
                AssistantProviderPurpose.ToolSelection);

            var providerResult = await CollectProviderTextAsync(planRequest, ct);
            providerResponseId = providerResult.ProviderResponseId ?? providerResponseId;
            totalInputTokens += providerResult.InputTokens ?? EstimateTokens(planRequest.Messages.Sum(m => m.Content.Length) + planRequest.SystemPrompt.Length);
            totalOutputTokens += providerResult.OutputTokens ?? EstimateTokens(providerResult.Text.Length);
            finishReason = providerResult.FinishReason ?? finishReason;

            if (!string.IsNullOrWhiteSpace(providerResult.SafeError))
            {
                providerError = providerResult.SafeError;
                break;
            }

            var plan = TryParseToolPlan(providerResult.Text);
            if (plan is null)
            {
                assistantContent = FallbackPlanMessage(providerResult.Text);
                break;
            }

            if (plan.Action.Equals("final", StringComparison.OrdinalIgnoreCase))
            {
                plannedFinalMessage = string.IsNullOrWhiteSpace(plan.Message)
                    ? null
                    : plan.Message.Trim();
                shouldStreamFinalAnswer = true;
                break;
            }

            if (!plan.Action.Equals("tool", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(plan.ToolKey))
            {
                assistantContent = FallbackPlanMessage(providerResult.Text);
                break;
            }

            var toolInputJson = ResolvePlannedToolInput(plan, contextualToolHint);
            var toolRun = await ExecuteToolAsync(
                conversationId,
                userMessage.Id,
                tenantId,
                actorId,
                agent.AgentKey,
                plan.ToolKey,
                toolInputJson,
                mergedContextJson,
                ct);

            toolRuns.Add(toolRun);
            workingMessages.Add(toolRun.ToolMessage);
        }

        if (!string.IsNullOrWhiteSpace(providerError))
        {
            _metrics.AssistantRequestsFailed.Add(1, KeyValuePair.Create<string, object?>("provider", providerKey));
            yield return new AssistantStreamEventDto("error", null, null, providerError);
            yield break;
        }

        if (shouldStreamFinalAnswer)
        {
            var answerRequest = new AssistantProviderRequest(
                agent.AgentKey,
                agent.Version,
                BuildAnswerPrompt(agent.SystemPrompt),
                providerModelKey,
                BuildProviderMessages(workingMessages),
                mergedContextJson,
                correlationId ?? string.Empty,
                AssistantProviderPurpose.Chat);

            var answerChunks = new List<string>();

            await foreach (var evt in _provider.StreamAsync(answerRequest, ct).WithCancellation(ct))
            {
                if (evt.Type == "delta" && !string.IsNullOrEmpty(evt.Delta))
                {
                    answerChunks.Add(evt.Delta);
                    yield return new AssistantStreamEventDto("delta", evt.Delta, null, null);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(evt.ProviderResponseId))
                    providerResponseId = evt.ProviderResponseId;

                totalInputTokens += evt.InputTokens ?? 0;
                totalOutputTokens += evt.OutputTokens ?? 0;
                finishReason = evt.FinishReason ?? finishReason;

                if (evt.Type == "error")
                {
                    providerError = evt.SafeError ?? "Assistant provider failed.";
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(providerError))
            {
                if (!string.IsNullOrWhiteSpace(plannedFinalMessage))
                {
                    assistantContent = plannedFinalMessage;
                    providerError = null;
                }
                else
                {
                    _metrics.AssistantRequestsFailed.Add(1, KeyValuePair.Create<string, object?>("provider", providerKey));
                    yield return new AssistantStreamEventDto("error", null, null, providerError);
                    yield break;
                }
            }
            else
            {
                assistantContent = string.Concat(answerChunks).Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(assistantContent))
            assistantContent = plannedFinalMessage
                ?? "I could not produce a response for this request.";

        if (!shouldStreamFinalAnswer)
        {
            foreach (var chunk in ChunkText(assistantContent, 72))
                yield return new AssistantStreamEventDto("delta", chunk, null, null);
        }

        if (totalInputTokens == 0)
            totalInputTokens = EstimateTokens(content.Length + agent.SystemPrompt.Length);

        if (totalOutputTokens == 0)
            totalOutputTokens = EstimateTokens(assistantContent.Length);

        finishReason ??= "stop";
        var assistantMetadataJson = BuildAssistantMessageMetadataJson(
            agent.AgentKey,
            mergedContextJson,
            toolRuns);

        var assistantMessage = new AssistantMessage(
            Guid.CreateVersion7(),
            conversationId,
            tenantId,
            actorId,
            AssistantMessageRole.Assistant,
            assistantContent,
            providerKey,
            providerResponseId,
            assistantMetadataJson);
        assistantMessage.SetUsage(totalInputTokens, totalOutputTokens, finishReason);

        _db.AssistantMessages.Add(assistantMessage);
        var assistantCitations = DeduplicateCitations(toolRuns.SelectMany(run => run.Result.Citations))
            .Select(citation => new AssistantMessageCitation(
                Guid.CreateVersion7(),
                assistantMessage.Id,
                tenantId,
                citation.SourceType,
                citation.SourceId,
                citation.Label,
                citation.Url))
            .ToList();

        foreach (var citation in assistantCitations)
        {
            _db.AssistantMessageCitations.Add(citation);
        }
        conversation.Touch(DateTime.UtcNow);

        await RecordUsageAsync(
            tenantId,
            actorId,
            conversationId,
            assistantMessage.Id,
            agent.AgentKey,
            providerKey,
            providerModelKey,
            totalInputTokens,
            totalOutputTokens,
            sw.ElapsedMilliseconds,
            ct);

        await _db.SaveChangesAsync(ct);

        _metrics.AssistantRequestsCompleted.Add(1, KeyValuePair.Create<string, object?>("provider", providerKey));
        _metrics.AssistantResponseDurationMs.Record(sw.Elapsed.TotalMilliseconds, KeyValuePair.Create<string, object?>("provider", providerKey));
        _metrics.AssistantTokens.Add(totalInputTokens + totalOutputTokens, KeyValuePair.Create<string, object?>("provider", providerKey));

        yield return new AssistantStreamEventDto(
            "message",
            null,
            ToMessageDto(assistantMessage, assistantCitations),
            null);
        yield return new AssistantStreamEventDto("done", null, null, null);
    }

    public async Task<AssistantUserPreferenceDto> GetPreferencesAsync(CancellationToken ct = default)
    {
        var (tenantId, actorId, _) = RequireContext();
        var preferences = await GetOrCreatePreferenceAsync(tenantId, actorId, ct);
        await _db.SaveChangesAsync(ct);
        return ToPreferenceDto(preferences);
    }

    public async Task<AssistantUserPreferenceDto> UpdatePreferencesAsync(
        UpdateAssistantPreferencesRequest request,
        CancellationToken ct = default)
    {
        var (tenantId, actorId, _) = RequireContext();
        var preferences = await GetOrCreatePreferenceAsync(tenantId, actorId, ct);
        var defaultAgentKey = request.DefaultAgentKey ?? preferences.DefaultAgentKey;
        await ResolveAgentAsync(tenantId, defaultAgentKey, ct);

        preferences.Update(
            defaultAgentKey,
            request.ContextHintsEnabled ?? preferences.ContextHintsEnabled,
            SafeJsonObject(request.PreferencesJson ?? preferences.PreferencesJson));

        await _db.SaveChangesAsync(ct);
        return ToPreferenceDto(preferences);
    }

    private async Task<IReadOnlyList<AssistantAgent>> QueryAllowedAgentsAsync(Guid tenantId, CancellationToken ct)
    {
        var agents = await _db.AssistantAgents
            .AsNoTracking()
            .Where(a => a.IsEnabled)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        var overrides = await _db.TenantAssistantAgents
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .ToDictionaryAsync(a => a.AgentKey, StringComparer.OrdinalIgnoreCase, ct);

        return agents
            .Where(a => (!overrides.TryGetValue(a.AgentKey, out var tenantAgent) || tenantAgent.Enabled) && PrincipalCanUseAgent(a))
            .ToList();
    }

    private async Task<AssistantAgent> ResolveAgentAsync(Guid tenantId, string? agentKey, CancellationToken ct)
    {
        var requestedKey = string.IsNullOrWhiteSpace(agentKey)
            ? AssistantModuleKeys.GenericAgentKey
            : agentKey.Trim();

        var agents = await QueryAllowedAgentsAsync(tenantId, ct);
        return agents.FirstOrDefault(a => a.AgentKey.Equals(requestedKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new UnauthorizedAccessException("The requested assistant agent is not enabled for this user.");
    }

    private bool PrincipalCanUseAgent(AssistantAgent agent)
    {
        var requiredProducts = ParseStringList(agent.RequiredProductCodesJson);
        if (requiredProducts.Count == 0) return true;

        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal is null) return false;

        if (HasRole(principal, "PlatformAdmin")) return true;

        var productClaims = principal.FindAll("product_codes")
            .Concat(principal.FindAll("enabled_products"))
            .Select(c => NormalizeProductCode(c.Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var role in principal.FindAll("product_roles"))
        {
            var productCode = role.Value.Split(':', 2)[0];
            if (!string.IsNullOrWhiteSpace(productCode))
                productClaims.Add(NormalizeProductCode(productCode));
        }

        return requiredProducts.All(code => productClaims.Contains(NormalizeProductCode(code)));
    }

    private async Task<AssistantUserPreference> GetOrCreatePreferenceAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var preferences = await _db.AssistantUserPreferences
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ActorId == actorId, ct);

        if (preferences is not null) return preferences;

        preferences = new AssistantUserPreference(Guid.CreateVersion7(), tenantId, actorId);
        _db.AssistantUserPreferences.Add(preferences);
        return preferences;
    }

    private async Task<AssistantUsageSummaryDto> GetUsageSummaryAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var usage = await _db.AssistantUsageEvents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.ActorId == actorId && e.OccurredAtUtc >= start)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Requests = g.Count(),
                InputTokens = g.Sum(e => e.InputTokens),
                OutputTokens = g.Sum(e => e.OutputTokens),
                Cost = g.Sum(e => e.EstimatedCostUsd),
            })
            .FirstOrDefaultAsync(ct);

        return new AssistantUsageSummaryDto(
            usage?.Requests ?? 0,
            usage?.InputTokens ?? 0,
            usage?.OutputTokens ?? 0,
            usage?.Cost ?? 0,
            _options.Value.MonthlyRequestLimit,
            _options.Value.MonthlyTokenLimit);
    }

    private async Task EnforceQuotaAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var summary = await GetUsageSummaryAsync(tenantId, actorId, ct);
        if (summary.MonthlyRequestLimit.HasValue && summary.RequestsThisMonth >= summary.MonthlyRequestLimit.Value)
            throw new InvalidOperationException("Monthly assistant request quota has been reached.");

        if (summary.MonthlyTokenLimit.HasValue &&
            summary.InputTokensThisMonth + summary.OutputTokensThisMonth >= summary.MonthlyTokenLimit.Value)
            throw new InvalidOperationException("Monthly assistant token quota has been reached.");
    }

    private async Task RecordUsageAsync(
        Guid tenantId,
        Guid actorId,
        Guid conversationId,
        Guid messageId,
        string agentKey,
        string provider,
        string modelKey,
        int inputTokens,
        int outputTokens,
        long latencyMs,
        CancellationToken ct)
    {
        _db.AssistantUsageEvents.Add(new AssistantUsageEvent(
            Guid.CreateVersion7(),
            tenantId,
            actorId,
            conversationId,
            messageId,
            agentKey,
            provider,
            modelKey,
            "message",
            inputTokens,
            outputTokens,
            0,
            (int)Math.Min(int.MaxValue, latencyMs)));

        var now = DateTime.UtcNow;
        var startsAt = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endsAt = startsAt.AddMonths(1);
        var windowKey = "monthly";

        var window = await _db.AssistantQuotaWindows
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.ActorId == actorId && w.WindowKey == windowKey && w.StartsAtUtc == startsAt, ct);

        if (window is null)
        {
            window = new AssistantQuotaWindow(Guid.CreateVersion7(), tenantId, actorId, windowKey, startsAt, endsAt);
            _db.AssistantQuotaWindows.Add(window);
        }

        window.AddUsage(inputTokens, outputTokens, 0);
    }

    private (Guid TenantId, Guid ActorId, string? CorrelationId) RequireContext()
    {
        var context = _tenantAccessor.Current;
        if (context?.IsResolved != true || context.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Tenant context is required.");

        if (context.ActorId is not { } actorId || actorId == Guid.Empty)
            throw new UnauthorizedAccessException("Actor context is required.");

        return (context.TenantId, actorId, context.CorrelationId);
    }

    private static AssistantAgentDto ToAgentDto(AssistantAgent agent)
        => new(
            agent.AgentKey,
            agent.Name,
            agent.Description,
            agent.Version,
            agent.IsEnabled,
            ParseStringList(agent.AllowedToolsJson),
            ParseStringList(agent.RequiredProductCodesJson));

    private static AssistantConversationSummaryDto ToConversationSummaryDto(AssistantConversation conversation)
        => new(
            conversation.Id,
            conversation.AgentKey,
            conversation.AgentVersion,
            conversation.Title,
            conversation.Source,
            conversation.Status.ToString(),
            conversation.LastMessageAtUtc,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc);

    private static AssistantConversationDto ToConversationDto(
        AssistantConversation conversation,
        IReadOnlyList<AssistantMessage> messages,
        IReadOnlyList<AssistantMessageCitation> citations)
    {
        var byMessage = citations.GroupBy(c => c.MessageId).ToDictionary(g => g.Key, g => g.ToList());
        var visibleMessages = messages
            .Where(m => m.Role is AssistantMessageRole.User or AssistantMessageRole.Assistant)
            .ToList();
        return new AssistantConversationDto(
            conversation.Id,
            conversation.AgentKey,
            conversation.AgentVersion,
            conversation.Title,
            conversation.Source,
            conversation.Status.ToString(),
            conversation.ContextJson,
            conversation.LastMessageAtUtc,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc,
            visibleMessages.Select(m => ToMessageDto(m, byMessage.TryGetValue(m.Id, out var messageCitations) ? messageCitations : [])).ToList());
    }

    private static AssistantMessageDto ToMessageDto(
        AssistantMessage message,
        IReadOnlyList<AssistantMessageCitation> citations)
        => new(
            message.Id,
            message.ConversationId,
            message.Role.ToString().ToLowerInvariant(),
            message.Content,
            message.Provider,
            message.ProviderResponseId,
            message.InputTokens,
            message.OutputTokens,
            message.FinishReason,
            message.CreatedAtMessageUtc,
            message.MetadataJson,
            citations.Select(c => new AssistantCitationDto(c.Id, c.SourceType, c.SourceId, c.Label, c.Url)).ToList());

    private static AssistantUserPreferenceDto ToPreferenceDto(AssistantUserPreference preferences)
        => new(preferences.DefaultAgentKey, preferences.ContextHintsEnabled, preferences.PreferencesJson);

    private static string? SafeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var trimmed = title.Trim();
        return trimmed.Length <= AssistantConversation.TitleMaxLength
            ? trimmed
            : trimmed[..AssistantConversation.TitleMaxLength];
    }

    private static string SafeJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "{}";
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? json : "{}";
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    private static string MergeContextJson(string conversationContextJson, string? messageContextJson)
    {
        var conversationContext = SafeJsonObject(conversationContextJson);
        var messageContext = SafeJsonObject(messageContextJson);
        if (messageContext == "{}") return conversationContext;

        var merged = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        using var conversationDoc = JsonDocument.Parse(conversationContext);
        foreach (var property in conversationDoc.RootElement.EnumerateObject())
            merged[property.Name] = property.Value.Clone();

        using var messageDoc = JsonDocument.Parse(messageContext);
        foreach (var property in messageDoc.RootElement.EnumerateObject())
            merged[property.Name] = property.Value.Clone();

        return JsonSerializer.Serialize(merged, JsonOptions);
    }

    private static IReadOnlyList<string> ParseStringList(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeProductCode(string code)
    {
        var normalized = code.Trim().Replace("_", "", StringComparison.OrdinalIgnoreCase).Replace("-", "", StringComparison.OrdinalIgnoreCase);
        return normalized.ToUpperInvariant() switch
        {
            "SYNQAI" or "XENIA" => "XENIA",
            "SYNQLIEN" or "SYNQLIENS" => "SYNQLIEN",
            "SYNQCARECONNECT" or "CARECONNECT" => "CARECONNECT",
            _ => normalized.ToUpperInvariant(),
        };
    }

    private static bool HasRole(ClaimsPrincipal principal, string role)
        => principal.IsInRole(role) || principal.HasClaim("role", role) || principal.HasClaim(ClaimTypes.Role, role);

    private static int EstimateTokens(int characters)
        => Math.Max(1, (int)Math.Ceiling(characters / 4.0));

    private static IReadOnlyList<AssistantProviderMessage> BuildProviderMessages(IReadOnlyList<AssistantMessage> messages)
        => messages
            .Where(m => m.Role is AssistantMessageRole.User or AssistantMessageRole.Assistant or AssistantMessageRole.Tool)
            .Select(m => new AssistantProviderMessage(
                m.Role switch
                {
                    AssistantMessageRole.Assistant => "assistant",
                    AssistantMessageRole.Tool => "tool",
                    _ => "user",
                },
                m.Content))
            .ToList();

    private static string BuildToolSelectionPrompt(
        string agentSystemPrompt,
        IReadOnlyList<AssistantToolDefinitionDto> tools,
        ContextualToolHint? contextualHint)
    {
        var toolCatalog = string.Join(
            "\n",
            tools.Select(tool => $"- {tool.ToolKey}: {tool.Description} InputSchema={tool.InputSchemaJson}"));

        var contextualHintLine = contextualHint is null
            ? "No page-scoped tool hint is available."
            : $"Contextual tool hint: {contextualHint.ToolKey} with input {contextualHint.InputJson}.";

        return $@"{agentSystemPrompt}

You are selecting the next step for Xenia's read-only assistant runtime.
Return ONLY valid JSON with one of these shapes:
{{""action"":""tool"",""toolKey"":""<tool-key>"",""input"":{{""example"":""value""}}}}
{{""action"":""final"",""message"":""<user-facing answer>""}}

Rules:
- Use a tool whenever live product data or search results are needed.
- Do not invent record identifiers or facts.
- If the user refers to the current page record, prefer the contextual tool hint when it matches the request.
- If the user is trying to find a referral by patient/client name, provider name, provider organization, law firm, or referrer contact, use careconnect.referral.search before considering provider-only or referrer-only directory tools.
- Use careconnect.referral.queue.summary when the user asks for counts, KPI-style summaries, status mix, or date-window totals.
- For questions about new referrals, prefer statusGroup=""new"" so both New and NewOpened are included.
- Use days for relative windows like ""last 7 days"" and createdFromUtc/createdToUtc for explicit date ranges.
- Use careconnect.provider.search only when the user wants providers themselves, not when they want referrals involving a provider.
- Use careconnect.referrer.search only when the user wants referrers or law firms themselves, not when they want referrals involving them.
- After tool results are available, either request another tool or return a final grounded answer.
- Keep the final answer concise and explicit about uncertainty.

Available tools:
{toolCatalog}

{contextualHintLine}
";
    }

    private static string BuildAnswerPrompt(string agentSystemPrompt)
        => $@"{agentSystemPrompt}

You are replying directly to the user in Xenia's read-only assistant runtime.
- Use grounded product data from prior tool messages when available.
- Do not invent facts, identifiers, or statuses.
- If the grounded data is incomplete, say exactly what is missing.
- Do not expose internal tool-selection steps unless the user asks.
- Keep the answer concise and helpful.";

    private async Task<ProviderTextResult> CollectProviderTextAsync(
        AssistantProviderRequest request,
        CancellationToken ct)
    {
        var chunks = new List<string>();
        string? providerResponseId = null;
        int? inputTokens = null;
        int? outputTokens = null;
        string? finishReason = null;
        string? safeError = null;

        await foreach (var evt in _provider.StreamAsync(request, ct).WithCancellation(ct))
        {
            if (evt.Type == "delta" && !string.IsNullOrEmpty(evt.Delta))
            {
                chunks.Add(evt.Delta);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(evt.ProviderResponseId))
                providerResponseId = evt.ProviderResponseId;

            inputTokens ??= evt.InputTokens;
            outputTokens ??= evt.OutputTokens;
            finishReason ??= evt.FinishReason;

            if (evt.Type == "error")
            {
                safeError = evt.SafeError ?? "Assistant provider failed.";
                break;
            }
        }

        return new ProviderTextResult(
            string.Concat(chunks).Trim(),
            providerResponseId,
            inputTokens,
            outputTokens,
            finishReason,
            safeError);
    }

    private async Task<AssistantToolRun> ExecuteToolAsync(
        Guid conversationId,
        Guid userMessageId,
        Guid tenantId,
        Guid actorId,
        string agentKey,
        string toolKey,
        string inputJson,
        string mergedContextJson,
        CancellationToken ct)
    {
        var invocation = new AssistantToolInvocation(
            Guid.CreateVersion7(),
            conversationId,
            userMessageId,
            tenantId,
            actorId,
            agentKey,
            toolKey,
            inputJson);

        _db.AssistantToolInvocations.Add(invocation);

        AssistantToolExecutionResultDto result;
        try
        {
            result = await _toolExecutor.ExecuteAsync(
                new AssistantToolExecutionRequestDto(toolKey, agentKey, inputJson, mergedContextJson),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Assistant tool execution failed. tool={ToolKey}", toolKey);
            result = new AssistantToolExecutionResultDto(
                false,
                "tool_execution_failed",
                "{}",
                "The requested product lookup failed.",
                2,
                []);
        }

        if (result.Succeeded)
        {
            invocation.Complete(result.OutputJson);
        }
        else
        {
            if (string.Equals(result.Status, "confirmation_required", StringComparison.OrdinalIgnoreCase))
                invocation.MarkConfirmationRequired();

            invocation.Fail(result.SafeError ?? "The requested product lookup failed.");
        }

        var toolMessage = new AssistantMessage(
            Guid.CreateVersion7(),
            conversationId,
            tenantId,
            actorId,
            AssistantMessageRole.Tool,
            BuildToolMessageContent(toolKey, result),
            "tool",
            null,
            JsonSerializer.Serialize(new
            {
                toolKey,
                result.Status,
                result.Succeeded,
            }, JsonOptions));

        _db.AssistantMessages.Add(toolMessage);
        await _db.SaveChangesAsync(ct);

        return new AssistantToolRun(toolKey, result, toolMessage);
    }

    private static string BuildToolMessageContent(string toolKey, AssistantToolExecutionResultDto result)
        => result.Succeeded
            ? $"Tool {toolKey} succeeded.\n{result.OutputJson}"
            : $"Tool {toolKey} failed with status '{result.Status}'. SafeError: {result.SafeError ?? "The requested product lookup failed."}";

    private static AssistantToolPlan? TryParseToolPlan(string rawText)
    {
        var candidate = ExtractJsonObject(rawText);
        if (string.IsNullOrWhiteSpace(candidate)) return null;

        try
        {
            using var doc = JsonDocument.Parse(candidate);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("action", out var actionElement) ||
                actionElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var action = actionElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(action)) return null;

            if (action.Equals("final", StringComparison.OrdinalIgnoreCase))
            {
                var message = doc.RootElement.TryGetProperty("message", out var messageElement) &&
                              messageElement.ValueKind == JsonValueKind.String
                    ? messageElement.GetString()
                    : null;
                return new AssistantToolPlan("final", null, "{}", message);
            }

            var toolKey = doc.RootElement.TryGetProperty("toolKey", out var toolElement) &&
                          toolElement.ValueKind == JsonValueKind.String
                ? toolElement.GetString()
                : null;

            var inputJson = "{}";
            if (doc.RootElement.TryGetProperty("input", out var inputElement) &&
                inputElement.ValueKind == JsonValueKind.Object)
            {
                inputJson = inputElement.GetRawText();
            }
            else if (doc.RootElement.TryGetProperty("arguments", out var argumentElement) &&
                     argumentElement.ValueKind == JsonValueKind.Object)
            {
                inputJson = argumentElement.GetRawText();
            }

            return new AssistantToolPlan(action, toolKey, inputJson, null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ExtractJsonObject(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        var trimmed = rawText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineBreak = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineBreak >= 0 && lastFence > firstLineBreak)
                trimmed = trimmed[(firstLineBreak + 1)..lastFence].Trim();
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace
            ? trimmed[firstBrace..(lastBrace + 1)]
            : trimmed;
    }

    private static string FallbackPlanMessage(string rawText)
    {
        var cleaned = ExtractJsonObject(rawText);
        return string.IsNullOrWhiteSpace(cleaned)
            ? "I could not produce a response for this request."
            : cleaned.Trim();
    }

    private static string ResolvePlannedToolInput(AssistantToolPlan plan, ContextualToolHint? contextualHint)
    {
        if (!string.IsNullOrWhiteSpace(plan.InputJson) && plan.InputJson != "{}")
            return SafeJsonObject(plan.InputJson);

        if (contextualHint is not null &&
            plan.ToolKey is not null &&
            plan.ToolKey.Equals(contextualHint.ToolKey, StringComparison.OrdinalIgnoreCase))
        {
            return contextualHint.InputJson;
        }

        return "{}";
    }

    private static IEnumerable<string> ChunkText(string value, int chunkSize)
    {
        if (string.IsNullOrEmpty(value))
            yield break;

        for (var index = 0; index < value.Length; index += chunkSize)
            yield return value.Substring(index, Math.Min(chunkSize, value.Length - index));
    }

    private static IReadOnlyList<AssistantToolCitationDto> DeduplicateCitations(IEnumerable<AssistantToolCitationDto> citations)
        => citations
            .GroupBy(c => $"{c.SourceType}|{c.SourceId}|{c.Url}|{c.Label}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

    private static string BuildAssistantMessageMetadataJson(
        string agentKey,
        string mergedContextJson,
        IReadOnlyList<AssistantToolRun> toolRuns)
    {
        var lookupResults = toolRuns
            .SelectMany(run => BuildLookupResults(run.ToolKey, run.Result.OutputJson))
            .Take(8)
            .ToList();

        var followUpPrompts = BuildFollowUpPrompts(agentKey, mergedContextJson, toolRuns);
        if (lookupResults.Count == 0 && followUpPrompts.Count == 0)
            return "{}";

        return JsonSerializer.Serialize(new
        {
            lookupResults,
            followUpPrompts,
        }, JsonOptions);
    }

    private static IReadOnlyList<AssistantLookupResultCard> BuildLookupResults(string toolKey, string outputJson)
    {
        if (string.IsNullOrWhiteSpace(outputJson) || outputJson == "{}")
            return [];

        try
        {
            using var doc = JsonDocument.Parse(outputJson);
            var root = doc.RootElement;

            return toolKey switch
            {
                "careconnect.referral.lookup" => root.TryGetProperty("referral", out var referral)
                    ? BuildReferralCards([referral])
                    : [],
                "careconnect.referral.history.lookup" => root.TryGetProperty("referral", out var historyReferral)
                    ? BuildReferralCards([historyReferral])
                    : [],
                "careconnect.referral.search" => root.TryGetProperty("results", out var referralResults)
                    ? BuildReferralCards(referralResults.EnumerateArray())
                    : [],
                "careconnect.referral.queue.summary" => root.TryGetProperty("recentResults", out var recentResults)
                    ? BuildReferralCards(recentResults.EnumerateArray())
                    : [],
                "careconnect.provider.search" => root.TryGetProperty("results", out var providerResults)
                    ? BuildProviderCards(providerResults.EnumerateArray())
                    : [],
                "careconnect.referrer.search" => root.TryGetProperty("results", out var referrerResults)
                    ? BuildReferrerCards(referrerResults.EnumerateArray())
                    : [],
                _ => [],
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<AssistantLookupResultCard> BuildReferralCards(IEnumerable<JsonElement> items)
        => items
            .Select(item => new AssistantLookupResultCard(
                "referral",
                GetString(item, "id") ?? string.Empty,
                GetString(item, "clientDisplayName") ?? "Referral",
                GetString(item, "providerName"),
                BuildReferralDescription(item),
                GetString(item, "status"),
                GetString(item, "url"),
                BuildBadges(GetString(item, "urgency"), GetString(item, "treatmentTypeName"))))
            .Where(card => !string.IsNullOrWhiteSpace(card.Id))
            .ToList();

    private static IReadOnlyList<AssistantLookupResultCard> BuildProviderCards(IEnumerable<JsonElement> items)
        => items
            .Select(item => new AssistantLookupResultCard(
                "provider",
                GetString(item, "id") ?? string.Empty,
                GetString(item, "displayLabel") ?? GetString(item, "name") ?? "Provider",
                CombineText(GetString(item, "city"), GetString(item, "state")),
                GetString(item, "organizationName"),
                GetBool(item, "acceptingReferrals") == true ? "Accepting referrals" : "Directory result",
                GetString(item, "url"),
                BuildBadges(GetString(item, "primaryCategory"), GetBool(item, "isActive") == true ? "Active" : null)))
            .Where(card => !string.IsNullOrWhiteSpace(card.Id))
            .ToList();

    private static IReadOnlyList<AssistantLookupResultCard> BuildReferrerCards(IEnumerable<JsonElement> items)
        => items
            .Select(item => new AssistantLookupResultCard(
                "referrer",
                GetString(item, "referrerEmail") ?? GetString(item, "referrerName") ?? string.Empty,
                GetString(item, "referrerName") ?? "Referrer",
                GetString(item, "referrerEmail"),
                BuildReferrerDescription(item),
                GetInt(item, "openReferralCount") is { } count ? $"{count} open referrals" : null,
                GetString(item, "url"),
                []))
            .Where(card => !string.IsNullOrWhiteSpace(card.Id))
            .ToList();

    private static List<string> BuildFollowUpPrompts(
        string agentKey,
        string mergedContextJson,
        IReadOnlyList<AssistantToolRun> toolRuns)
    {
        var prompts = new List<string>();
        if (toolRuns.Any(run => run.ToolKey.Equals("careconnect.referral.search", StringComparison.OrdinalIgnoreCase)))
        {
            prompts.Add("Show only New referrals");
            prompts.Add("Find the status history for the best match");
            prompts.Add("Narrow these results by provider or referrer");
        }

        if (toolRuns.Any(run => run.ToolKey.Equals("careconnect.provider.search", StringComparison.OrdinalIgnoreCase)))
        {
            prompts.Add("Find referrals for one of these providers");
        }

        if (toolRuns.Any(run => run.ToolKey.Equals("careconnect.referrer.search", StringComparison.OrdinalIgnoreCase)))
        {
            prompts.Add("Show the most recent referrals from this law firm");
        }

        if (toolRuns.Any(run => run.ToolKey.Equals("careconnect.referral.queue.summary", StringComparison.OrdinalIgnoreCase)))
        {
            prompts.Add("Which referrals need attention first?");
            prompts.Add("Show the recent referrals behind this queue summary");
        }

        if (toolRuns.Count == 0 && agentKey.Equals(AssistantModuleKeys.CareConnectAgentKey, StringComparison.OrdinalIgnoreCase))
        {
            prompts.Add("Search referrals by client, provider, or referrer");
            prompts.Add("Summarize my referral queue");
        }

        if (toolRuns.Count == 0 && TryBuildContextualToolHint(mergedContextJson) is { ToolKey: "careconnect.referral.lookup" })
        {
            prompts.Add("Summarize this referral");
            prompts.Add("Show this referral's history");
        }

        return prompts
            .Where(prompt => !string.IsNullOrWhiteSpace(prompt))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
    }

    private static string? BuildReferralDescription(JsonElement item)
    {
        var requestedService = GetString(item, "requestedService");
        return requestedService;
    }

    private static string? BuildReferrerDescription(JsonElement item)
    {
        var recentCases = item.TryGetProperty("recentCaseNumbers", out var casesElement) && casesElement.ValueKind == JsonValueKind.Array
            ? casesElement.EnumerateArray()
                .Select(value => value.ValueKind == JsonValueKind.String ? value.GetString() : null)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(3)
                .ToList()
            : [];

        if (recentCases.Count == 0) return null;
        return $"Recent cases: {string.Join(", ", recentCases)}";
    }

    private static IReadOnlyList<string> BuildBadges(params string?[] values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string? CombineText(string? first, string? second)
    {
        var parts = new[] { first, second }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();
        return parts.Count == 0 ? null : string.Join(" • ", parts);
    }

    private static string? GetString(JsonElement item, string propertyName)
        => item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? GetBool(JsonElement item, string propertyName)
        => item.TryGetProperty(propertyName, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    private static int? GetInt(JsonElement item, string propertyName)
        => item.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static ContextualToolHint? TryBuildContextualToolHint(string mergedContextJson)
    {
        var entityId = TryGetContextEntityId(mergedContextJson);
        if (entityId.HasValue)
        {
            return new ContextualToolHint(
                "careconnect.referral.lookup",
                JsonSerializer.Serialize(new { referralId = entityId.Value }, JsonOptions));
        }

        var path = TryGetContextPath(mergedContextJson);
        if (!string.IsNullOrWhiteSpace(path) &&
            TryParseCareConnectReferralId(path, out var referralId))
        {
            return new ContextualToolHint(
                "careconnect.referral.lookup",
                JsonSerializer.Serialize(new { referralId }, JsonOptions));
        }

        return null;
    }

    private static string? TryGetContextPath(string mergedContextJson)
    {
        if (string.IsNullOrWhiteSpace(mergedContextJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(mergedContextJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("path", out var pathElement) ||
                pathElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return pathElement.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Guid? TryGetContextEntityId(string mergedContextJson)
    {
        if (string.IsNullOrWhiteSpace(mergedContextJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(mergedContextJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("entity", out var entityElement) ||
                entityElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var kind = entityElement.TryGetProperty("kind", out var kindElement) && kindElement.ValueKind == JsonValueKind.String
                ? kindElement.GetString()
                : null;
            var id = entityElement.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString()
                : null;

            return kind?.Equals("referral", StringComparison.OrdinalIgnoreCase) == true &&
                   Guid.TryParse(id, out var parsed)
                ? parsed
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryParseCareConnectReferralId(string path, out Guid referralId)
    {
        referralId = Guid.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length >= 3 &&
               segments[0].Equals("careconnect", StringComparison.OrdinalIgnoreCase) &&
               segments[1].Equals("referrals", StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(segments[2], out referralId) &&
               referralId != Guid.Empty;
    }

    private sealed record ProviderTextResult(
        string Text,
        string? ProviderResponseId,
        int? InputTokens,
        int? OutputTokens,
        string? FinishReason,
        string? SafeError);

    private sealed record AssistantToolPlan(
        string Action,
        string? ToolKey,
        string InputJson,
        string? Message);

    private sealed record ContextualToolHint(
        string ToolKey,
        string InputJson);

    private sealed record AssistantToolRun(
        string ToolKey,
        AssistantToolExecutionResultDto Result,
        AssistantMessage ToolMessage);

    private sealed record AssistantLookupResultCard(
        string Kind,
        string Id,
        string Title,
        string? Subtitle,
        string? Description,
        string? Status,
        string? Url,
        IReadOnlyList<string> Badges);
}
