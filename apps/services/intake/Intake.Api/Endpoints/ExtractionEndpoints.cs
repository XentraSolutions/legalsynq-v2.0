using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Extraction;
using Intake.Application.Configuration;
using Intake.Contracts.Extraction;

namespace Intake.Api.Endpoints;

public static class ExtractionEndpoints
{
    public static IEndpointRouteBuilder MapExtractionEndpoints(
        this IEndpointRouteBuilder app)
    {
        var readGroup = app.MapGroup(string.Empty)
            .WithTags("Artifact Extraction")
            .RequireAuthorization(IntakeAuthorizationPolicies.ClassificationRead);

        readGroup.MapGet("/extraction/profiles", async (
                IArtifactExtractionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListProfilesAsync(cancellationToken)))
            .WithSummary("List active lien-intake extraction profiles");

        readGroup.MapGet("/artifacts/{artifactId:guid}/extraction", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IArtifactExtractionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCurrentAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get the current extraction for an Intake artifact");

        readGroup.MapGet("/artifacts/{artifactId:guid}/extraction/history", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IArtifactExtractionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetHistoryAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get extraction history for an Intake artifact");

        var manageGroup = app.MapGroup(string.Empty)
            .WithTags("Artifact Extraction")
            .RequireAuthorization(IntakeAuthorizationPolicies.ClassificationManage);

        manageGroup.MapPost("/artifacts/{artifactId:guid}/extraction", async (
                Guid artifactId,
                ExtractArtifactRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IArtifactExtractionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ExtractAsync(
                RequireTenant(context),
                artifactId,
                request.ProcessingProfileCode,
                context.UserId,
                httpContext.GetCorrelationId(),
                request.Retry,
                cancellationToken)))
            .WithSummary("Extract source facts from one classified Intake artifact");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext context) =>
        context.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required for this operation.");
}