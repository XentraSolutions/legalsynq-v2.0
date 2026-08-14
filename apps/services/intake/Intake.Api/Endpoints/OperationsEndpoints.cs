using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Configuration;
using Intake.Application.Operations;

namespace Intake.Api.Endpoints;

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(
        this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api/intake/operations")
            .WithTags("Intake Operations")
            .RequireAuthorization(IntakeAuthorizationPolicies.OperationsRead);

        read.MapGet("/summary", async (
                string? range,
                DateTimeOffset? from,
                DateTimeOffset? to,
                ICurrentRequestContext context,
                IIntakeRecoveryService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetSummaryAsync(
                RequireTenant(context),
                ResolveFrom(range, from, to),
                cancellationToken)));

        read.MapGet("/stages", async (
                string? range,
                DateTimeOffset? from,
                DateTimeOffset? to,
                ICurrentRequestContext context,
                IIntakeRecoveryService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetStageFunnelAsync(
                RequireTenant(context),
                ResolveFrom(range, from, to),
                cancellationToken)));

        read.MapGet("/failures", async (
                string? range,
                DateTimeOffset? from,
                DateTimeOffset? to,
                ICurrentRequestContext context,
                IIntakeRecoveryService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetFailuresAsync(
                RequireTenant(context),
                ResolveFrom(range, from, to),
                cancellationToken)));

        read.MapGet("/recovery", async (
                string? range,
                DateTimeOffset? from,
                DateTimeOffset? to,
                ICurrentRequestContext context,
                IIntakeRecoveryService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetRecoveryAnalyticsAsync(
                RequireTenant(context),
                ResolveFrom(range, from, to),
                cancellationToken)));

        read.MapGet("/recovery/worker", async (
                IIntakeRecoveryService service) =>
            Results.Ok(await service.GetWorkerHealthAsync()));

        read.MapGet("/recovery/items", async (
                string? stage,
                string? status,
                string? failureCategory,
                bool? retryable,
                DateTimeOffset? from,
                DateTimeOffset? to,
                int page,
                int pageSize,
                ICurrentRequestContext context,
                IIntakeRecoveryService service,
                CancellationToken cancellationToken) =>
        {
            var result = await service.ListAsync(
                RequireTenant(context),
                new RecoveryQuery(
                    stage,
                    status,
                    failureCategory,
                    retryable,
                    from,
                    to,
                    page <= 0 ? 1 : page,
                    pageSize <= 0 ? 50 : pageSize),
                cancellationToken);
            return Results.Ok(new
            {
                result.Items,
                Page = page <= 0 ? 1 : page,
                PageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 100),
                result.TotalCount,
            });
        });

        read.MapGet("/recovery/items/{workItemId:guid}", async (
                Guid workItemId,
                ICurrentRequestContext context,
                IIntakeRecoveryService service,
                CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(
                RequireTenant(context), workItemId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        var manage = app.MapGroup("/api/intake/operations")
            .WithTags("Intake Operations")
            .RequireAuthorization(IntakeAuthorizationPolicies.OperationsRecover);

        manage.MapPost("/recovery/{stage}/{workItemId:guid}", async (
                string stage,
                Guid workItemId,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeRecoveryService service,
                CancellationToken cancellationToken) =>
        {
            var tenantId = RequireTenant(context);
            var current = await service.GetAsync(tenantId, workItemId, cancellationToken);
            if (current is null || !string.Equals(current.Stage, stage, StringComparison.OrdinalIgnoreCase))
                return Results.NotFound();
            var result = await service.RecoverAsync(
                tenantId,
                workItemId,
                RequireUser(context),
                httpContext.GetCorrelationId(),
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        manage.MapPost("/recovery/{stage}/{workItemId:guid}/cancel", async (
                string stage,
                Guid workItemId,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeRecoveryService service,
                CancellationToken cancellationToken) =>
        {
            var tenantId = RequireTenant(context);
            var current = await service.GetAsync(tenantId, workItemId, cancellationToken);
            if (current is null || !string.Equals(current.Stage, stage, StringComparison.OrdinalIgnoreCase))
                return Results.NotFound();
            var cancelled = await service.CancelAsync(
                tenantId,
                workItemId,
                RequireUser(context),
                httpContext.GetCorrelationId(),
                cancellationToken);
            return cancelled ? Results.NoContent() : Results.Conflict();
        });

        return app;
    }

    private static DateTimeOffset ResolveFrom(
        string? range,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var now = DateTimeOffset.UtcNow;
        var upper = to ?? now;
        var lower = from ?? (range?.ToLowerInvariant() switch
        {
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            _ => now.AddHours(-24),
        });
        if (lower > upper || upper - lower > TimeSpan.FromDays(30))
            throw IntakeConfigurationException.BadRequest(
                "OPERATIONS_TIME_RANGE_INVALID",
                "Operations analytics supports a maximum bounded 30-day time range.");
        return lower;
    }

    private static Guid RequireTenant(ICurrentRequestContext context) =>
        context.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required.");

    private static Guid RequireUser(ICurrentRequestContext context) =>
        context.UserId ?? throw IntakeConfigurationException.Forbidden(
            "USER_CONTEXT_REQUIRED",
            "An authenticated LegalSynq identity is required.");
}