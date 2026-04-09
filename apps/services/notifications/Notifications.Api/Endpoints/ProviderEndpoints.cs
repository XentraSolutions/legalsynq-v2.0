using Notifications.Api.Middleware;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

public static class ProviderEndpoints
{
    public static void MapProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/providers").WithTags("Providers");

        group.MapGet("/configs", async (HttpContext context, ITenantProviderConfigService service, string? channel) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.ListAsync(tenantId, channel);
            return Results.Ok(result);
        });

        group.MapGet("/configs/{id:guid}", async (HttpContext context, ITenantProviderConfigService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.GetByIdAsync(tenantId, id);
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

        group.MapGet("/channel-settings", async (HttpContext context, ITenantChannelProviderSettingRepository repo) =>
        {
            var tenantId = context.GetTenantId();
            var settings = await repo.GetByTenantAsync(tenantId);
            return Results.Ok(settings.Select(s => new TenantChannelSettingDto
            {
                Id = s.Id, TenantId = s.TenantId, Channel = s.Channel, ProviderMode = s.ProviderMode,
                PrimaryTenantProviderConfigId = s.PrimaryTenantProviderConfigId,
                FallbackTenantProviderConfigId = s.FallbackTenantProviderConfigId,
                AllowPlatformFallback = s.AllowPlatformFallback, AllowAutomaticFailover = s.AllowAutomaticFailover,
                CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt
            }));
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
