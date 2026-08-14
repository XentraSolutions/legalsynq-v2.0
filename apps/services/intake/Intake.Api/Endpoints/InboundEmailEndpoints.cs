using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Configuration;
using Intake.Application.Artifacts;
using Intake.Application.Emails;
using Intake.Contracts.Emails;

namespace Intake.Api.Endpoints;

public static class InboundEmailEndpoints
{
    public static IEndpointRouteBuilder MapInboundEmailEndpoints(
        this IEndpointRouteBuilder app)
    {
        var readGroup = app.MapGroup(string.Empty)
            .WithTags("Inbound Emails")
            .RequireAuthorization(IntakeAuthorizationPolicies.EmailRead);

        readGroup.MapGet("/emails", async (
                [AsParameters] InboundEmailListQuery query,
                ICurrentRequestContext requestContext,
                IInboundEmailQueryService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                RequireTenant(requestContext),
                query,
                cancellationToken)))
            .WithSummary("List captured inbound emails for the current tenant");

        readGroup.MapGet("/emails/{emailId:guid}", async (
                Guid emailId,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IInboundEmailQueryService service,
                CancellationToken cancellationToken) =>
        {
            var detail = await service.GetAsync(
                RequireTenant(requestContext),
                emailId,
                cancellationToken);

            return detail is null
                ? Results.NotFound(new
                {
                    error = "INBOUND_EMAIL_NOT_FOUND",
                    detail = "The inbound email was not found for the current tenant.",
                    correlationId = httpContext.GetCorrelationId(),
                })
                : Results.Ok(detail);
        }).WithSummary("Get a captured inbound email for the current tenant");

        var analyticsGroup = app.MapGroup(string.Empty)
            .WithTags("Inbound Email Analytics")
            .RequireAuthorization(IntakeAuthorizationPolicies.EmailAnalytics);

        analyticsGroup.MapGet("/emails/analytics", async (
                DateTimeOffset? from,
                DateTimeOffset? to,
                ICurrentRequestContext requestContext,
                IInboundEmailQueryService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAnalyticsAsync(
                RequireTenant(requestContext),
                from,
                to,
                cancellationToken)))
            .WithSummary("Get tenant-scoped inbound email analytics");

        var artifactReadGroup = app.MapGroup(string.Empty)
            .WithTags("Inbound Email Artifacts")
            .RequireAuthorization(IntakeAuthorizationPolicies.ArtifactRead);

        artifactReadGroup.MapGet("/emails/{emailId:guid}/artifacts", async (
                Guid emailId,
                ICurrentRequestContext requestContext,
                IEmailArtifactProcessingService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                RequireTenant(requestContext),
                emailId,
                cancellationToken)))
            .WithSummary("List tenant-scoped artifacts for a captured inbound email");

        artifactReadGroup.MapGet("/emails/{emailId:guid}/artifacts/reconcile", async (
                Guid emailId,
                ICurrentRequestContext requestContext,
                IEmailArtifactProcessingService service,
                CancellationToken cancellationToken) =>
        {
            var result = await service.ReconcileAsync(
                RequireTenant(requestContext),
                emailId,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithSummary("Reconcile B04 attachment metadata with extracted artifacts");

        artifactReadGroup.MapGet("/emails/artifacts/analytics", async (
                Guid? emailId,
                ICurrentRequestContext requestContext,
                IEmailArtifactProcessingService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAnalyticsAsync(
                RequireTenant(requestContext),
                emailId,
                cancellationToken)))
            .WithSummary("Get tenant-scoped email artifact processing metrics");

        var artifactManageGroup = app.MapGroup(string.Empty)
            .WithTags("Inbound Email Artifacts")
            .RequireAuthorization(IntakeAuthorizationPolicies.ArtifactManage);

        artifactManageGroup.MapPost("/emails/{emailId:guid}/artifacts/process", async (
                Guid emailId,
                ICurrentRequestContext requestContext,
                IEmailArtifactProcessingService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ProcessAsync(
                RequireTenant(requestContext),
                emailId,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Process captured email artifacts through the Documents Service");

        artifactManageGroup.MapPost("/emails/{emailId:guid}/artifacts/{artifactId:guid}/retry", async (
                Guid emailId,
                Guid artifactId,
                ICurrentRequestContext requestContext,
                IEmailArtifactProcessingService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.RetryAsync(
                RequireTenant(requestContext),
                emailId,
                artifactId,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Retry one failed inbound email artifact");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext requestContext) =>
        requestContext.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required for this operation.");
}