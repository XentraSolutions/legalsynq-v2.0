using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xenia.Application.Assistant;
using Xenia.Application.Configuration;
using Xenia.Domain.Configuration;
using Xenia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Xenia.Api.Endpoints;

public static class XeniaAssistantEndpoints
{
    public static IEndpointRouteBuilder MapXeniaAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/assistant")
            .WithTags("Xenia Assistant")
            .RequireAuthorization(XeniaPolicies.AssistantUse);

        group.MapGet("/bootstrap", async (IAssistantService assistant, CancellationToken ct) =>
            Results.Ok(await assistant.GetBootstrapAsync(ct)));

        group.MapGet("/agents", async (IAssistantService assistant, CancellationToken ct) =>
            Results.Ok(new { agents = await assistant.ListAgentsAsync(ct) }));

        group.MapGet("/conversations", async (IAssistantService assistant, CancellationToken ct) =>
            Results.Ok(new { conversations = await assistant.ListConversationsAsync(ct) }));

        group.MapPost("/conversations", async (
            [FromBody] CreateAssistantConversationRequest request,
            IAssistantService assistant,
            CancellationToken ct) =>
            Results.Created("/assistant/conversations", await assistant.CreateConversationAsync(request, ct)));

        group.MapGet("/conversations/{conversationId:guid}", async (
            Guid conversationId,
            IAssistantService assistant,
            CancellationToken ct) =>
        {
            var conversation = await assistant.GetConversationAsync(conversationId, ct);
            return conversation is null ? Results.NotFound() : Results.Ok(conversation);
        });

        group.MapPatch("/conversations/{conversationId:guid}", async (
            Guid conversationId,
            [FromBody] UpdateAssistantConversationRequest request,
            IAssistantService assistant,
            CancellationToken ct) =>
        {
            var conversation = await assistant.UpdateConversationAsync(conversationId, request, ct);
            return conversation is null ? Results.NotFound() : Results.Ok(conversation);
        });

        group.MapDelete("/conversations/{conversationId:guid}", async (
            Guid conversationId,
            IAssistantService assistant,
            CancellationToken ct) =>
            await assistant.ArchiveConversationAsync(conversationId, ct)
                ? Results.NoContent()
                : Results.NotFound());

        group.MapPost("/conversations/{conversationId:guid}/messages", async (
            Guid conversationId,
            [FromBody] CreateAssistantMessageRequest request,
            IAssistantService assistant,
            CancellationToken ct) =>
            Results.Ok(await assistant.CreateMessageAsync(conversationId, request, ct)));

        group.MapPost("/conversations/{conversationId:guid}/messages:stream", async (
            Guid conversationId,
            [FromBody] CreateAssistantMessageRequest request,
            IAssistantService assistant,
            HttpContext http,
            IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions,
            CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers.Connection = "keep-alive";
            http.Response.ContentType = "text/event-stream";

            await foreach (var evt in assistant.StreamMessageAsync(conversationId, request, ct))
            {
                await http.Response.WriteAsync($"event: {evt.Type}\n", ct);
                await http.Response.WriteAsync(
                    $"data: {JsonSerializer.Serialize(evt, jsonOptions.Value.SerializerOptions)}\n\n",
                    ct);
                await http.Response.Body.FlushAsync(ct);
            }
        });

        group.MapGet("/preferences", async (IAssistantService assistant, CancellationToken ct) =>
            Results.Ok(await assistant.GetPreferencesAsync(ct)));

        group.MapPatch("/preferences", async (
            [FromBody] UpdateAssistantPreferencesRequest request,
            IAssistantService assistant,
            CancellationToken ct) =>
            Results.Ok(await assistant.UpdatePreferencesAsync(request, ct)));

        return app;
    }

    public static IEndpointRouteBuilder MapXeniaAssistantAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin")
            .WithTags("Xenia Assistant Admin")
            .RequireAuthorization(XeniaPolicies.AssistantManage);

        group.MapGet("/settings", async (
            IAssistantRuntimeSettingsService settings,
            CancellationToken ct) =>
        {
            var effective = await settings.GetEffectiveSettingsAsync(null, ct);
            return Results.Ok(new AssistantAdminSettingsDto(
                effective.Provider,
                effective.ModelKey,
                effective.OpenAiBaseUrl,
                effective.HasOpenAiApiKey,
                effective.OpenAiTimeoutSeconds,
                effective.OpenAiReasoningEffort,
                effective.OpenAiTextVerbosity,
                effective.OpenAiMaxOutputTokens,
                effective.LastUpdatedAtUtc));
        });

        group.MapPut("/settings", async (
            [FromBody] UpdateAssistantAdminSettingsRequest request,
            IXeniaConfigurationService configuration,
            CancellationToken ct) =>
        {
            var validationErrors = ValidateAssistantAdminSettings(request);
            if (validationErrors.Count > 0)
                return Results.ValidationProblem(validationErrors);

            var provider = NormalizeProvider(request.Provider)!;

            await configuration.SetValueAsync(
                ScopeType.Global,
                null,
                AssistantModuleKeys.ConfigurationNamespace,
                AssistantConfigurationKeys.Provider,
                provider,
                isSecret: false,
                ct);

            await configuration.SetValueAsync(
                ScopeType.Global,
                null,
                AssistantModuleKeys.ConfigurationNamespace,
                AssistantConfigurationKeys.ModelKey,
                request.ModelKey.Trim(),
                isSecret: false,
                ct);

            await configuration.SetValueAsync(
                ScopeType.Global,
                null,
                AssistantModuleKeys.ConfigurationNamespace,
                AssistantConfigurationKeys.OpenAiBaseUrl,
                request.OpenAiBaseUrl.Trim(),
                isSecret: false,
                ct);

            await configuration.SetValueAsync(
                ScopeType.Global,
                null,
                AssistantModuleKeys.ConfigurationNamespace,
                AssistantConfigurationKeys.OpenAiTimeoutSeconds,
                request.OpenAiTimeoutSeconds.ToString(),
                isSecret: false,
                ct);

            await configuration.SetValueAsync(
                ScopeType.Global,
                null,
                AssistantModuleKeys.ConfigurationNamespace,
                AssistantConfigurationKeys.OpenAiReasoningEffort,
                NormalizeReasoningEffort(request.OpenAiReasoningEffort),
                isSecret: false,
                ct);

            await configuration.SetValueAsync(
                ScopeType.Global,
                null,
                AssistantModuleKeys.ConfigurationNamespace,
                AssistantConfigurationKeys.OpenAiTextVerbosity,
                NormalizeTextVerbosity(request.OpenAiTextVerbosity),
                isSecret: false,
                ct);

            await configuration.SetValueAsync(
                ScopeType.Global,
                null,
                AssistantModuleKeys.ConfigurationNamespace,
                AssistantConfigurationKeys.OpenAiMaxOutputTokens,
                request.OpenAiMaxOutputTokens?.ToString(),
                isSecret: false,
                ct);

            return Results.NoContent();
        });

        group.MapGet("/config/global", async (
            IXeniaConfigurationService configuration,
            CancellationToken ct) =>
            Results.Ok(new
            {
                entries = await configuration.GetVisibleConfigurationAsync(null, AssistantModuleKeys.ConfigurationNamespace, ct),
                precedence = "Global < Tenant < Agent < TenantAgent < UserPreferences",
                secrets = "The OpenAI API key is appsettings-only and is not persisted in this configuration store.",
            }));

        group.MapPut("/config/global", async (
            [FromBody] AssistantAdminConfigRequest request,
            IXeniaConfigurationService configuration,
            CancellationToken ct) =>
        {
            foreach (var (key, value) in request.Settings ?? new Dictionary<string, string?>())
            {
                await configuration.SetValueAsync(ScopeType.Global, null, AssistantModuleKeys.ConfigurationNamespace, key, value, isSecret: false, ct);
            }

            foreach (var (key, secretReference) in request.SecretReferences ?? new Dictionary<string, string?>())
            {
                await configuration.SetValueAsync(ScopeType.Global, null, AssistantModuleKeys.ConfigurationNamespace, key, secretReference, isSecret: true, ct);
            }

            return Results.NoContent();
        });

        group.MapGet("/tenants/{tenantId:guid}/config", async (
            Guid tenantId,
            IXeniaConfigurationService configuration,
            CancellationToken ct) =>
            Results.Ok(new
            {
                tenantId,
                entries = await configuration.GetVisibleConfigurationAsync(tenantId, AssistantModuleKeys.ConfigurationNamespace, ct),
            }));

        group.MapPut("/tenants/{tenantId:guid}/config", async (
            Guid tenantId,
            [FromBody] AssistantAdminConfigRequest request,
            IXeniaConfigurationService configuration,
            CancellationToken ct) =>
        {
            foreach (var (key, value) in request.Settings ?? new Dictionary<string, string?>())
            {
                await configuration.SetValueAsync(ScopeType.Tenant, tenantId.ToString(), AssistantModuleKeys.ConfigurationNamespace, key, value, isSecret: false, ct);
            }

            foreach (var (key, secretReference) in request.SecretReferences ?? new Dictionary<string, string?>())
            {
                await configuration.SetValueAsync(ScopeType.Tenant, tenantId.ToString(), AssistantModuleKeys.ConfigurationNamespace, key, secretReference, isSecret: true, ct);
            }

            return Results.NoContent();
        });

        var usageGroup = app.MapGroup("/admin")
            .WithTags("Xenia Assistant Admin")
            .RequireAuthorization(XeniaPolicies.AssistantUsageRead);

        usageGroup.MapGet("/usage", async (
            [FromQuery] Guid? tenantId,
            XeniaDbContext db,
            CancellationToken ct) =>
        {
            var since = DateTime.UtcNow.AddDays(-30);
            var query = db.AssistantUsageEvents.AsNoTracking().Where(e => e.OccurredAtUtc >= since);
            if (tenantId.HasValue) query = query.Where(e => e.TenantId == tenantId.Value);

            var rows = await query
                .GroupBy(e => new { e.TenantId, e.AgentKey, e.Provider, e.ModelKey })
                .Select(g => new
                {
                    g.Key.TenantId,
                    g.Key.AgentKey,
                    g.Key.Provider,
                    g.Key.ModelKey,
                    requests = g.Count(),
                    inputTokens = g.Sum(e => e.InputTokens),
                    outputTokens = g.Sum(e => e.OutputTokens),
                    estimatedCostUsd = g.Sum(e => e.EstimatedCostUsd),
                    averageLatencyMs = g.Average(e => e.LatencyMs),
                })
                .OrderByDescending(r => r.requests)
                .Take(100)
                .ToListAsync(ct);

            return Results.Ok(new { since, usage = rows });
        });

        usageGroup.MapGet("/audit", () => Results.Ok(new
        {
            events = Array.Empty<object>(),
            note = "Assistant audit event querying will be backed by the platform audit adapter when the production adapter is wired.",
        }));

        return app;
    }

    private static string? NormalizeProvider(string? provider)
        => provider?.Trim().ToUpperInvariant() switch
        {
            "OPENAI" => "OpenAI",
            "FAKE" => "Fake",
            _ => null,
        };

    private static string? NormalizeReasoningEffort(string? effort)
        => effort?.Trim().ToLowerInvariant() switch
        {
            "minimal" => "minimal",
            "low" => "low",
            "medium" => "medium",
            "high" => "high",
            "" => null,
            null => null,
            _ => null,
        };

    private static string? NormalizeTextVerbosity(string? verbosity)
        => verbosity?.Trim().ToLowerInvariant() switch
        {
            "low" => "low",
            "medium" => "medium",
            "high" => "high",
            "" => null,
            null => null,
            _ => null,
        };

    private static Dictionary<string, string[]> ValidateAssistantAdminSettings(UpdateAssistantAdminSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (NormalizeProvider(request.Provider) is null)
            errors[nameof(request.Provider)] = ["Provider must be either 'Fake' or 'OpenAI'."];

        if (string.IsNullOrWhiteSpace(request.ModelKey))
            errors[nameof(request.ModelKey)] = ["Model key is required."];

        if (string.IsNullOrWhiteSpace(request.OpenAiBaseUrl) ||
            !Uri.TryCreate(request.OpenAiBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors[nameof(request.OpenAiBaseUrl)] = ["OpenAI base URL must be a valid absolute http/https URL."];
        }

        if (request.OpenAiTimeoutSeconds <= 0 || request.OpenAiTimeoutSeconds > 600)
            errors[nameof(request.OpenAiTimeoutSeconds)] = ["OpenAI timeout must be between 1 and 600 seconds."];

        if (!string.IsNullOrWhiteSpace(request.OpenAiReasoningEffort) && NormalizeReasoningEffort(request.OpenAiReasoningEffort) is null)
            errors[nameof(request.OpenAiReasoningEffort)] = ["Reasoning effort must be blank or one of: minimal, low, medium, high."];

        if (!string.IsNullOrWhiteSpace(request.OpenAiTextVerbosity) && NormalizeTextVerbosity(request.OpenAiTextVerbosity) is null)
            errors[nameof(request.OpenAiTextVerbosity)] = ["Text verbosity must be blank or one of: low, medium, high."];

        if (request.OpenAiMaxOutputTokens.HasValue && request.OpenAiMaxOutputTokens.Value <= 0)
            errors[nameof(request.OpenAiMaxOutputTokens)] = ["Max output tokens must be greater than zero when set."];

        return errors;
    }
}

public sealed record AssistantAdminConfigRequest(
    Dictionary<string, string?>? Settings,
    Dictionary<string, string?>? SecretReferences);

public sealed record AssistantAdminSettingsDto(
    string Provider,
    string ModelKey,
    string OpenAiBaseUrl,
    bool OpenAiApiKeyConfigured,
    int OpenAiTimeoutSeconds,
    string? OpenAiReasoningEffort,
    string? OpenAiTextVerbosity,
    int? OpenAiMaxOutputTokens,
    DateTime? LastUpdatedAtUtc);

public sealed record UpdateAssistantAdminSettingsRequest(
    string Provider,
    string ModelKey,
    string OpenAiBaseUrl,
    int OpenAiTimeoutSeconds,
    string? OpenAiReasoningEffort,
    string? OpenAiTextVerbosity,
    int? OpenAiMaxOutputTokens);
