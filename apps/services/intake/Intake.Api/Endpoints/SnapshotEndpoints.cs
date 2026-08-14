using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Snapshot;
using Intake.Contracts.Snapshot;

namespace Intake.Api.Endpoints;

public static class SnapshotEndpoints
{
    public static IEndpointRouteBuilder MapSnapshotEndpoints(
        this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup(string.Empty)
            .WithTags("Intake Approved Snapshots")
            .RequireAuthorization(IntakeAuthorizationPolicies.SnapshotRead);

        read.MapGet("/snapshots/{snapshotId:guid}", async (
                Guid snapshotId,
                ICurrentRequestContext context,
                IApprovedIntakeSnapshotService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(
                RequireTenant(context),
                snapshotId,
                cancellationToken)))
            .WithSummary("Get an immutable approved Intake snapshot");

        read.MapGet("/artifacts/{artifactId:guid}/snapshots/current", async (
                Guid artifactId,
                ICurrentRequestContext context,
                IApprovedIntakeSnapshotService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCurrentAsync(
                RequireTenant(context),
                artifactId,
                cancellationToken)))
            .WithSummary("Get the current approved snapshot for an artifact");

        read.MapGet("/artifacts/{artifactId:guid}/snapshots", async (
                Guid artifactId,
                int page,
                int pageSize,
                ICurrentRequestContext context,
                IApprovedIntakeSnapshotService service,
                CancellationToken cancellationToken) =>
        {
            var result = await service.ListByArtifactAsync(
                RequireTenant(context),
                artifactId,
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 25 : pageSize,
                cancellationToken);
            return Results.Ok(new
            {
                result.Items,
                Page = page <= 0 ? 1 : page,
                PageSize = pageSize <= 0 ? 25 : pageSize,
                result.TotalCount,
            });
        }).WithSummary("List approved snapshot history for an artifact");

        read.MapGet("/adapters", (
                IIntakeAdapterExecutionService service) =>
            Results.Ok(service.ListAdapters()))
            .WithSummary("List registered product-neutral Intake adapters");

        read.MapGet("/snapshots/{snapshotId:guid}/adapter-executions", async (
                Guid snapshotId,
                ICurrentRequestContext context,
                IIntakeAdapterExecutionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                RequireTenant(context),
                snapshotId,
                cancellationToken)))
            .WithSummary("List adapter executions for an approved snapshot");

        read.MapGet("/snapshots/{snapshotId:guid}/adapter-executions/{executionId:guid}", async (
                Guid snapshotId,
                Guid executionId,
                ICurrentRequestContext context,
                IIntakeAdapterExecutionService service,
                CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(
                RequireTenant(context),
                snapshotId,
                executionId,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithSummary("Get one adapter execution");

        read.MapGet("/snapshots/{snapshotId:guid}/document-associations", async (
                Guid snapshotId,
                ICurrentRequestContext context,
                IDocumentAssociationExecutionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                RequireTenant(context), snapshotId, cancellationToken)))
            .WithSummary("List document-association executions for an approved snapshot");

        read.MapGet("/snapshots/{snapshotId:guid}/document-associations/{executionId:guid}", async (
                Guid snapshotId,
                Guid executionId,
                ICurrentRequestContext context,
                IDocumentAssociationExecutionService service,
                CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(
                RequireTenant(context), snapshotId, executionId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithSummary("Get one document-association execution");

        var manage = app.MapGroup(string.Empty)
            .WithTags("Intake Approved Snapshots")
            .RequireAuthorization(IntakeAuthorizationPolicies.SnapshotManage);

        manage.MapPost("/reviews/{reviewId:guid}/snapshot", async (
                Guid reviewId,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IApprovedIntakeSnapshotService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.CreateAsync(
                RequireTenant(context),
                reviewId,
                RequireUser(context),
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Create or return the immutable approved snapshot for a completed review");

        var execute = app.MapGroup(string.Empty)
            .WithTags("Intake Approved Snapshots")
            .RequireAuthorization(IntakeAuthorizationPolicies.AdapterExecute);

        execute.MapPost("/snapshots/{snapshotId:guid}/adapters/{adapterCode}/execute", async (
                Guid snapshotId,
                string adapterCode,
                ExecuteAdapterRequest? request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeAdapterExecutionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ExecuteAsync(
                RequireTenant(context),
                snapshotId,
                adapterCode,
                RequireUser(context),
                request?.DryRun ?? false,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Execute a product-neutral adapter against an approved snapshot");

        execute.MapPost("/snapshots/{snapshotId:guid}/adapter-executions/{executionId:guid}/retry", async (
                Guid snapshotId,
                Guid executionId,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeAdapterExecutionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.RetryAsync(
                RequireTenant(context),
                snapshotId,
                executionId,
                RequireUser(context),
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Retry a retryable adapter execution");

        execute.MapPost("/snapshots/{snapshotId:guid}/document-associations/execute", async (
                Guid snapshotId,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IDocumentAssociationExecutionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ExecuteAsync(
                RequireTenant(context), snapshotId, RequireUser(context),
                httpContext.GetCorrelationId(), cancellationToken)))
            .WithSummary("Associate approved snapshot documents with authoritative Case/Lien targets");

        execute.MapPost("/snapshots/{snapshotId:guid}/document-associations/{executionId:guid}/retry", async (
                Guid snapshotId,
                Guid executionId,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IDocumentAssociationExecutionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.RetryAsync(
                RequireTenant(context), snapshotId, executionId, RequireUser(context),
                httpContext.GetCorrelationId(), cancellationToken)))
            .WithSummary("Retry only failed document-association items");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext context) =>
        context.TenantId ?? throw Intake.Application.Configuration.IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required.");

    private static Guid RequireUser(ICurrentRequestContext context) =>
        context.UserId ?? throw Intake.Application.Configuration.IntakeConfigurationException.Forbidden(
            "USER_CONTEXT_REQUIRED",
            "An authenticated LegalSynq identity is required.");
}