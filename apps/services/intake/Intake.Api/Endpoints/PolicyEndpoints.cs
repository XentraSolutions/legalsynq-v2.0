using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Configuration;
using Intake.Application.Policy;
using Intake.Contracts.Policy;

namespace Intake.Api.Endpoints;

public static class PolicyEndpoints
{
    public static IEndpointRouteBuilder MapPolicyEndpoints(
        this IEndpointRouteBuilder app)
    {
        var readGroup = app.MapGroup(string.Empty)
            .WithTags("Artifact Policy")
            .RequireAuthorization(IntakeAuthorizationPolicies.ClassificationRead);

        readGroup.MapGet("/policy/profiles", async (
                IArtifactPolicyService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListProfilesAsync(cancellationToken)))
            .WithSummary("List active confidence and policy profiles");

        readGroup.MapGet("/artifacts/{artifactId:guid}/policy", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IArtifactPolicyService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCurrentAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get the current policy evaluation for an Intake artifact");

        readGroup.MapGet("/artifacts/{artifactId:guid}/policy/history", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IArtifactPolicyService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetHistoryAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get policy evaluation history for an Intake artifact");

        var manageGroup = app.MapGroup(string.Empty)
            .WithTags("Artifact Policy")
            .RequireAuthorization(IntakeAuthorizationPolicies.ClassificationManage);

        manageGroup.MapPost("/artifacts/{artifactId:guid}/policy/evaluate", async (
                Guid artifactId,
                EvaluateArtifactPolicyRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IArtifactPolicyService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.EvaluateAsync(
                RequireTenant(context),
                artifactId,
                request.ProcessingProfileCode,
                context.UserId,
                httpContext.GetCorrelationId(),
                request.Retry,
                cancellationToken)))
            .WithSummary("Evaluate the persisted B07-B10 results for an Intake artifact");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext context) =>
        context.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required for this operation.");
}