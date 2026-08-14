using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Configuration;
using Intake.Application.Normalization;
using Intake.Contracts.Normalization;

namespace Intake.Api.Endpoints;

public static class NormalizationEndpoints
{
    public static IEndpointRouteBuilder MapNormalizationEndpoints(
        this IEndpointRouteBuilder app)
    {
        var readGroup = app.MapGroup(string.Empty)
            .WithTags("Artifact Normalization")
            .RequireAuthorization(IntakeAuthorizationPolicies.ClassificationRead);

        readGroup.MapGet("/normalization/profiles", async (
                IArtifactNormalizationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListProfilesAsync(cancellationToken)))
            .WithSummary("List active lien-intake normalization profiles");

        readGroup.MapGet("/artifacts/{artifactId:guid}/normalization", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IArtifactNormalizationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCurrentAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get the current normalization for an Intake artifact");

        readGroup.MapGet("/artifacts/{artifactId:guid}/normalization/history", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IArtifactNormalizationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetHistoryAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get normalization history for an Intake artifact");

        var manageGroup = app.MapGroup(string.Empty)
            .WithTags("Artifact Normalization")
            .RequireAuthorization(IntakeAuthorizationPolicies.ClassificationManage);

        manageGroup.MapPost("/artifacts/{artifactId:guid}/normalization", async (
                Guid artifactId,
                NormalizeArtifactRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IArtifactNormalizationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.NormalizeAsync(
                RequireTenant(context),
                artifactId,
                request.ProcessingProfileCode,
                context.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Normalize the current B08 facts for an Intake artifact");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext context) =>
        context.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required for this operation.");
}