using Notifications.Api.Middleware;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

public static class ProviderEndpoints
{
    // Platform-default routing priority (mirrors ProviderRoutingService.PlatformProviderPriority)
    private static readonly Dictionary<string, string[]> PlatformPriority = new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = new[] { "sendgrid", "smtp" },
        ["sms"]   = new[] { "twilio" },
        ["push"]  = Array.Empty<string>(),
    };

    private static readonly Dictionary<string, string> ProviderDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sendgrid"] = "SendGrid",
        ["smtp"]     = "SMTP",
        ["twilio"]   = "Twilio",
    };

    public static void MapProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/providers").WithTags("Providers");

        // ── Catalog ────────────────────────────────────────────────────────────────
        // Static list of all provider types supported by the platform.
        // No auth / tenant scope needed — this is purely informational.
        group.MapGet("/catalog", () =>
        {
            var catalog = PlatformPriority
                .SelectMany(kv => kv.Value.Select(providerType => new
                {
                    providerType,
                    channel     = kv.Key,
                    displayName = ProviderDisplayNames.TryGetValue(providerType, out var name) ? name : providerType,
                }))
                .ToArray();

            return Results.Ok(catalog);
        });

        // ── Configs ────────────────────────────────────────────────────────────────

        group.MapGet("/configs", async (HttpContext context, ITenantProviderConfigService service, string? channel) =>
        {
            var tenantId = context.TryGetTenantId();
            if (tenantId == null) return Results.Ok(Array.Empty<TenantProviderConfigDto>());
            var result = await service.ListAsync(tenantId.Value, channel);
            return Results.Ok(result);
        });

        group.MapGet("/configs/{id:guid}", async (HttpContext context, ITenantProviderConfigService service, Guid id) =>
        {
            var tenantId = context.TryGetTenantId();
            if (tenantId == null) return Results.NotFound();
            var result = await service.GetByIdAsync(tenantId.Value, id);
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapPost("/configs", async (HttpContext context, ITenantProviderConfigService service, CreateTenantProviderConfigDto request) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.CreateAsync(tenantId, request);
            return Results.Created($"/v1/providers/configs/{result.Id}", result);
        });

        group.MapPut("/configs/{id:guid}", async (HttpContext context, ITenantProviderConfigService service, Guid id, UpdateTenantProviderConfigDto request) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.UpdateAsync(tenantId, id, request);
            return Results.Ok(result);
        });

        group.MapDelete("/configs/{id:guid}", async (HttpContext context, ITenantProviderConfigService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            await service.DeleteAsync(tenantId, id);
            return Results.NoContent();
        });

        group.MapPost("/configs/{id:guid}/validate", async (HttpContext context, ITenantProviderConfigService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.ValidateAsync(tenantId, id);
            return Results.Ok(result);
        });

        group.MapPost("/configs/{id:guid}/health-check", async (HttpContext context, ITenantProviderConfigService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.HealthCheckAsync(tenantId, id);
            return Results.Ok(result);
        });

        // ── Channel settings ───────────────────────────────────────────────────────

        group.MapGet("/channel-settings", async (HttpContext context, ITenantChannelProviderSettingRepository repo) =>
        {
            var tenantId = context.TryGetTenantId();
            var settings = tenantId.HasValue
                ? await repo.GetByTenantAsync(tenantId.Value)
                : Enumerable.Empty<Domain.TenantChannelProviderSetting>();

            var response = settings.Select(s =>
            {
                // Resolve human-readable provider names from the platform priority list
                // when the channel operates in platform-managed mode.
                PlatformPriority.TryGetValue(s.Channel, out var priorityList);
                var primaryProvider  = priorityList is { Length: > 0 } ? priorityList[0] : null;
                var fallbackProvider = priorityList is { Length: > 1 } ? priorityList[1] : null;

                return new
                {
                    id                             = s.Id,
                    tenantId                       = s.TenantId,
                    channel                        = s.Channel,
                    mode                           = s.ProviderMode,   // alias expected by UI
                    providerMode                   = s.ProviderMode,
                    primaryProvider,
                    fallbackProvider,
                    primaryTenantProviderConfigId  = s.PrimaryTenantProviderConfigId,
                    fallbackTenantProviderConfigId = s.FallbackTenantProviderConfigId,
                    allowPlatformFallback          = s.AllowPlatformFallback,
                    allowAutomaticFailover         = s.AllowAutomaticFailover,
                    createdAt                      = s.CreatedAt,
                    updatedAt                      = s.UpdatedAt,
                };
            });

            return Results.Ok(response);
        });

        group.MapPut("/channel-settings/{channel}", async (HttpContext context, ITenantChannelProviderSettingRepository repo, string channel, UpdateChannelSettingDto request) =>
        {
            var tenantId = context.GetTenantId();
            var setting = new Domain.TenantChannelProviderSetting
            {
                TenantId = tenantId, Channel = channel,
                ProviderMode = request.ProviderMode ?? "platform_managed",
                PrimaryTenantProviderConfigId = request.PrimaryTenantProviderConfigId,
                FallbackTenantProviderConfigId = request.FallbackTenantProviderConfigId,
                AllowPlatformFallback = request.AllowPlatformFallback ?? true,
                AllowAutomaticFailover = request.AllowAutomaticFailover ?? true
            };
            var result = await repo.UpsertAsync(setting);
            return Results.Ok(result);
        });
    }
}
