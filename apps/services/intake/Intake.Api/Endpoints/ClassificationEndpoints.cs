using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Classification;
using Intake.Application.Configuration;
using Intake.Contracts.Classification;

namespace Intake.Api.Endpoints;

public static class ClassificationEndpoints
{
    public static IEndpointRouteBuilder MapClassificationEndpoints(
        this IEndpointRouteBuilder app)
    {
        var readGroup = app.MapGroup(string.Empty)
            .WithTags("Artifact Classification")
            .RequireAuthorization(IntakeAuthorizationPolicies.ClassificationRead);

        readGroup.MapGet("/classification/profiles", async (
                IClassificationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListProfilesAsync(cancellationToken)))
            .WithSummary("List active generic classification profiles");

        readGroup.MapGet("/classification/policy", async (
                ICurrentRequestContext context,
                IClassificationService service,
                CancellationToken cancellationToken) =>
        {
            var policy = await service.GetPolicyAsync(
                RequireTenant(context),
                cancellationToken);
            return policy is null ? Results.NotFound() : Results.Ok(policy);
        }).WithSummary("Get the current tenant AI policy");

        readGroup.MapGet("/artifacts/{artifactId:guid}/classification", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IClassificationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCurrentAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get the current classification for an Intake artifact");

        readGroup.MapGet("/artifacts/{artifactId:guid}/classification/history", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IClassificationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetHistoryAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get classification history for an Intake artifact");

        var manageGroup = app.MapGroup(string.Empty)
            .WithTags("Artifact Classification")
            .RequireAuthorization(IntakeAuthorizationPolicies.ClassificationManage);

        manageGroup.MapPut("/classification/policy", async (
                UpsertTenantAiPolicyRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IClassificationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.UpsertPolicyAsync(
                RequireTenant(context),
                request,
                context.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Create or update the tenant AI policy");

        manageGroup.MapPost("/artifacts/{artifactId:guid}/classification", async (
                Guid artifactId,
                ClassifyArtifactRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IClassificationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ClassifyAsync(
                RequireTenant(context),
                artifactId,
                request.ProcessingProfileCode,
                context.UserId,
                httpContext.GetCorrelationId(),
                request.Retry,
                cancellationToken)))
            .WithSummary("Classify one completed Intake artifact");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext context) =>
        context.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required for this operation.");

    private sealed class ClassifyArtifactRequest
    {
        public string? ProcessingProfileCode { get; set; }
        public bool Retry { get; set; }
    }
}