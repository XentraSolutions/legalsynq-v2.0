using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Configuration;
using Intake.Application.Review;
using Intake.Contracts.Review;

namespace Intake.Api.Endpoints;

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(
        this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup(string.Empty)
            .WithTags("Intake Human Review")
            .RequireAuthorization(IntakeAuthorizationPolicies.ReviewRead);

        read.MapGet("/reviews", async (
                [AsParameters] IntakeReviewListQuery query,
                ICurrentRequestContext context,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                RequireTenant(context),
                query,
                cancellationToken)))
            .WithSummary("List the tenant-scoped Intake human review queue");

        read.MapGet("/reviews/summary", async (
                ICurrentRequestContext context,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetSummaryAsync(
                RequireTenant(context),
                cancellationToken)))
            .WithSummary("Get summary counts for the Intake human review queue");

        read.MapGet("/reviews/{reviewId:guid}", async (
                Guid reviewId,
                ICurrentRequestContext context,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(
                RequireTenant(context),
                reviewId,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithSummary("Get an Intake human review workspace");

        read.MapGet("/reviews/{reviewId:guid}/effective", async (
                Guid reviewId,
                ICurrentRequestContext context,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetEffectiveAsync(
                RequireTenant(context),
                reviewId,
                cancellationToken)))
            .WithSummary("Get the deterministic effective reviewed projection");

        var manage = app.MapGroup(string.Empty)
            .WithTags("Intake Human Review")
            .RequireAuthorization(IntakeAuthorizationPolicies.ReviewManage);

        manage.MapPost("/reviews", async (
                CreateIntakeReviewRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.CreateAsync(
                RequireTenant(context),
                RequireUser(context),
                httpContext.GetCorrelationId(),
                request,
                cancellationToken)))
            .WithSummary("Create or return the active review for the current policy lineage");

        manage.MapPost("/reviews/{reviewId:guid}/claim", async (
                Guid reviewId,
                ReviewVersionRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ClaimAsync(
                RequireTenant(context),
                reviewId,
                RequireUser(context),
                request,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Claim a review for the authenticated reviewer");

        manage.MapPost("/reviews/{reviewId:guid}/unassign", async (
                Guid reviewId,
                ReviewVersionRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.UnassignAsync(
                RequireTenant(context),
                reviewId,
                RequireUser(context),
                request,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Return a review to the unassigned queue");

        manage.MapPost("/reviews/{reviewId:guid}/corrections", async (
                Guid reviewId,
                AddReviewCorrectionRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.AddCorrectionAsync(
                RequireTenant(context),
                reviewId,
                RequireUser(context),
                request,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Append an immutable human correction or fact decision");

        manage.MapPost("/reviews/{reviewId:guid}/matches/{entityType}/decision", async (
                Guid reviewId,
                string entityType,
                ReviewMatchDecisionRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.DecideMatchAsync(
                RequireTenant(context),
                reviewId,
                entityType,
                RequireUser(context),
                request,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Record a human decision over tenant-scoped candidate matches");

        manage.MapPost("/reviews/{reviewId:guid}/duplicates/{signalId:guid}/decision", async (
                Guid reviewId,
                Guid signalId,
                ReviewDuplicateDecisionRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.DecideDuplicateAsync(
                RequireTenant(context),
                reviewId,
                signalId,
                RequireUser(context),
                request,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Record a human decision over a duplicate signal");

        manage.MapPost("/reviews/{reviewId:guid}/findings/{findingId:guid}/decision", async (
                Guid reviewId,
                Guid findingId,
                ReviewFindingDecisionRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.DecideFindingAsync(
                RequireTenant(context),
                reviewId,
                findingId,
                RequireUser(context),
                request,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Record a human decision over a policy finding");

        var assign = app.MapGroup(string.Empty)
            .WithTags("Intake Human Review")
            .RequireAuthorization(IntakeAuthorizationPolicies.ReviewAssign);

        assign.MapPut("/reviews/{reviewId:guid}/assignment", async (
                Guid reviewId,
                AssignIntakeReviewRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.AssignAsync(
                RequireTenant(context),
                reviewId,
                RequireUser(context),
                request,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Assign a review to an authenticated tenant reviewer");

        var complete = app.MapGroup(string.Empty)
            .WithTags("Intake Human Review")
            .RequireAuthorization(IntakeAuthorizationPolicies.ReviewComplete);

        complete.MapPost("/reviews/{reviewId:guid}/complete", async (
                Guid reviewId,
                CompleteIntakeReviewRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IIntakeReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.CompleteAsync(
                RequireTenant(context),
                reviewId,
                RequireUser(context),
                request,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Complete an Intake human review immutably");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext context) =>
        context.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required for this operation.");

    private static Guid RequireUser(ICurrentRequestContext context) =>
        context.UserId ?? throw IntakeConfigurationException.Forbidden(
            Intake.Domain.Review.IntakeReviewErrorCodes.UnauthorizedUser,
            "An authenticated LegalSynq identity is required for this operation.");
}