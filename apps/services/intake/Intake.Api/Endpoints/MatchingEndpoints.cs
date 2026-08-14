using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Configuration;
using Intake.Application.Matching;
using Intake.Contracts.Matching;

namespace Intake.Api.Endpoints;

public static class MatchingEndpoints
{
    public static IEndpointRouteBuilder MapMatchingEndpoints(
        this IEndpointRouteBuilder app)
    {
        var readGroup = app.MapGroup(string.Empty)
            .WithTags("Artifact Matching")
            .RequireAuthorization(IntakeAuthorizationPolicies.ClassificationRead);

        readGroup.MapGet("/matching/profiles", async (
                IArtifactMatchingService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListProfilesAsync(cancellationToken)))
            .WithSummary("List active lien-intake matching profiles");

        readGroup.MapGet("/artifacts/{artifactId:guid}/matching", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IArtifactMatchingService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCurrentAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get the current matching run for an Intake artifact");

        readGroup.MapGet("/artifacts/{artifactId:guid}/matching/history", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IArtifactMatchingService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetHistoryAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get matching history for an Intake artifact");

        var manageGroup = app.MapGroup(string.Empty)
            .WithTags("Artifact Matching")
            .RequireAuthorization(IntakeAuthorizationPolicies.ClassificationManage);

        manageGroup.MapPost("/artifacts/{artifactId:guid}/matching", async (
                Guid artifactId,
                MatchArtifactRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IArtifactMatchingService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.MatchAsync(
                RequireTenant(context),
                artifactId,
                request.ProcessingProfileCode,
                context.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Match the current B09 normalized facts for an Intake artifact");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext context) =>
        context.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required for this operation.");
}