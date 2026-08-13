using System.Security.Claims;
using BuildingBlocks.Authorization;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class TenantRegistrationEndpoints
{
    public static void MapTenantRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/public/tenant-registrations", async (SubmitTenantRegistrationRequest body,
            ITenantRegistrationService service, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!configuration.GetValue("TenantRegistration:Enabled", false)) return Results.NotFound();
            var result = await service.SubmitAsync(body, ct);
            return Results.Accepted($"/api/v1/public/tenant-registrations/{result.RegistrationId}", result);
        }).AllowAnonymous().RequireRateLimiting("tenant-registration");

        var admin = app.MapGroup("/api/v1/admin/tenant-registrations").RequireAuthorization(Policies.AdminOnly);
        admin.MapGet("/", async (ITenantRegistrationService service, CancellationToken ct,
            string? registrationStatus = null, string? provisioningStatus = null, string? search = null,
            DateTime? submittedFrom = null, DateTime? submittedTo = null, int page = 1, int pageSize = 20) =>
            Results.Ok(await service.ListAsync(registrationStatus, provisioningStatus, search, submittedFrom, submittedTo, page, pageSize, ct)));
        admin.MapGet("/{id:guid}", async (Guid id, ITenantRegistrationService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound());
        admin.MapPost("/{id:guid}/approve", async (Guid id, ClaimsPrincipal user,
            ITenantRegistrationService service, CancellationToken ct) =>
            Results.Ok(await service.ApproveAsync(id, ReviewerId(user), ct)));
        admin.MapPost("/{id:guid}/decline", async (Guid id, DeclineTenantRegistrationRequest body, ClaimsPrincipal user,
            ITenantRegistrationService service, CancellationToken ct) =>
            Results.Ok(await service.DeclineAsync(id, ReviewerId(user), body.Reason, ct)));
        admin.MapPost("/{id:guid}/provisioning/retry", async (Guid id, ITenantRegistrationService service, CancellationToken ct) =>
            Results.Ok(await service.RetryProvisioningAsync(id, ct)));
    }

    private static Guid ReviewerId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
