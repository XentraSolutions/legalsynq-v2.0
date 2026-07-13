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
                ["tool_runtime"] = "registry-only",
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
        var grounding = await TryBuildGroundingAsync(
            conversationId,
            userMessage.Id,
            tenantId,
            actorId,
            agent.AgentKey,
            mergedContextJson,
            ct);

        var providerMessages = priorMessages
            .Where(m => m.Role is AssistantMessageRole.User or AssistantMessageRole.Assistant)
            .Select(m => new AssistantProviderMessage(ToProviderRole(m.Role), m.Content))
            .ToList();

        if (!string.IsNullOrWhiteSpace(grounding?.PromptMessage))
        {
            providerMessages.Add(new AssistantProviderMessage("user", grounding.PromptMessage));
        }

        providerMessages.Add(new AssistantProviderMessage("user", content));

        var providerRequest = new AssistantProviderRequest(
            agent.AgentKey,
            agent.Version,
            agent.SystemPrompt,
            string.IsNullOrWhiteSpace(runtimeSettings.ModelKey) ? "xenia-fake" : runtimeSettings.ModelKey,
            providerMessages,
            mergedContextJson,
            correlationId ?? string.Empty);

        var chunks = new List<string>();
        string? providerResponseId = null;
        int? inputTokens = null;
        int? outputTokens = null;
        string? finishReason = null;

        await foreach (var evt in _provider.StreamAsync(providerRequest, ct).WithCancellation(ct))
        {
            if (evt.Type == "delta" && !string.IsNullOrEmpty(evt.Delta))
            {
                chunks.Add(evt.Delta);
                yield return new AssistantStreamEventDto("delta", evt.Delta, null, null);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(evt.ProviderResponseId))
                providerResponseId = evt.ProviderResponseId;

            inputTokens ??= evt.InputTokens;
            outputTokens ??= evt.OutputTokens;
            finishReason ??= evt.FinishReason;

            if (evt.Type == "error")
            {
                _metrics.AssistantRequestsFailed.Add(1, KeyValuePair.Create<string, object?>("provider", providerKey));
                yield return new AssistantStreamEventDto("error", null, null, evt.SafeError ?? "Assistant provider failed.");
                yield break;
            }
        }

        var assistantContent = string.Concat(chunks).Trim();
        if (string.IsNullOrWhiteSpace(assistantContent))
            assistantContent = "I could not produce a response for this request.";

        inputTokens ??= EstimateTokens(providerMessages.Sum(m => m.Content.Length) + agent.SystemPrompt.Length);
        outputTokens ??= EstimateTokens(assistantContent.Length);
        finishReason ??= "stop";

        var assistantMessage = new AssistantMessage(
            Guid.CreateVersion7(),
            conversationId,
            tenantId,
            actorId,
            AssistantMessageRole.Assistant,
            assistantContent,
            providerKey,
            providerResponseId,
            "{}");
        assistantMessage.SetUsage(inputTokens, outputTokens, finishReason);

        _db.AssistantMessages.Add(assistantMessage);
        var assistantCitations = (grounding?.Citations ?? [])
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
            providerRequest.ModelKey,
            inputTokens.Value,
            outputTokens.Value,
            sw.ElapsedMilliseconds,
            ct);

        await _db.SaveChangesAsync(ct);

        _metrics.AssistantRequestsCompleted.Add(1, KeyValuePair.Create<string, object?>("provider", providerKey));
        _metrics.AssistantResponseDurationMs.Record(sw.Elapsed.TotalMilliseconds, KeyValuePair.Create<string, object?>("provider", providerKey));
        _metrics.AssistantTokens.Add(inputTokens.Value + outputTokens.Value, KeyValuePair.Create<string, object?>("provider", providerKey));

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
            messages.Select(m => ToMessageDto(m, byMessage.TryGetValue(m.Id, out var messageCitations) ? messageCitations : [])).ToList());
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
            citations.Select(c => new AssistantCitationDto(c.Id, c.SourceType, c.SourceId, c.Label, c.Url)).ToList());

    private static AssistantUserPreferenceDto ToPreferenceDto(AssistantUserPreference preferences)
        => new(preferences.DefaultAgentKey, preferences.ContextHintsEnabled, preferences.PreferencesJson);

    private static string ToProviderRole(AssistantMessageRole role)
        => role == AssistantMessageRole.Assistant ? "assistant" : "user";

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

    private async Task<AssistantGroundingResult?> TryBuildGroundingAsync(
        Guid conversationId,
        Guid userMessageId,
        Guid tenantId,
        Guid actorId,
        string agentKey,
        string mergedContextJson,
        CancellationToken ct)
    {
        var toolKey = TryResolveGroundingToolKey(mergedContextJson);
        if (toolKey is null) return null;

        var inputJson = BuildGroundingInputJson(toolKey, mergedContextJson);
        if (inputJson is null) return null;

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
            _logger.LogWarning(ex, "Assistant grounding tool execution failed. tool={ToolKey}", toolKey);
            invocation.Fail("The grounded product lookup failed.");
            await _db.SaveChangesAsync(ct);
            return new AssistantGroundingResult(
                BuildGroundingFailurePrompt("The grounded product lookup failed."),
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

            invocation.Fail(result.SafeError ?? "The grounded product lookup failed.");
        }

        await _db.SaveChangesAsync(ct);

        return result.Succeeded
            ? new AssistantGroundingResult(
                BuildGroundingPrompt(toolKey, result.OutputJson),
                result.Citations)
            : new AssistantGroundingResult(
                BuildGroundingFailurePrompt(result.SafeError ?? "The grounded product lookup failed."),
                []);
    }

    private static string? TryResolveGroundingToolKey(string mergedContextJson)
    {
        var path = TryGetContextPath(mergedContextJson);
        if (string.IsNullOrWhiteSpace(path)) return null;

        return TryParseCareConnectReferralId(path, out _)
            ? "careconnect.referral.lookup"
            : null;
    }

    private static string? BuildGroundingInputJson(string toolKey, string mergedContextJson)
    {
        var path = TryGetContextPath(mergedContextJson);
        if (string.IsNullOrWhiteSpace(path)) return null;

        if (toolKey.Equals("careconnect.referral.lookup", StringComparison.OrdinalIgnoreCase) &&
            TryParseCareConnectReferralId(path, out var referralId))
        {
            return JsonSerializer.Serialize(new { referralId }, JsonOptions);
        }

        return null;
    }

    private static string BuildGroundingPrompt(string toolKey, string outputJson)
        => toolKey.Equals("careconnect.referral.lookup", StringComparison.OrdinalIgnoreCase)
            ? $"Authorized CareConnect referral context:\n{outputJson}\nUse only this grounded referral data when answering the next question. If the user asks beyond it, say that the current lookup does not provide that detail."
            : outputJson;

    private static string BuildGroundingFailurePrompt(string safeError)
        => $"Authorized product lookup was unavailable for the current page: {safeError} Do not make record-specific claims without grounded data.";

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

    private sealed record AssistantGroundingResult(
        string PromptMessage,
        IReadOnlyList<AssistantToolCitationDto> Citations);
}
