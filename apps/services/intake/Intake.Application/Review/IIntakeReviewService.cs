using Intake.Contracts.Review;

namespace Intake.Application.Review;

public interface IIntakeReviewService
{
    Task<IntakeReviewListResponse> ListAsync(
        Guid tenantId,
        IntakeReviewListQuery query,
        CancellationToken cancellationToken);

    Task<IntakeReviewQueueSummaryResponse> GetSummaryAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IntakeReviewWorkspaceResponse?> GetAsync(
        Guid tenantId,
        Guid reviewId,
        CancellationToken cancellationToken);

    Task<IntakeReviewWorkspaceResponse> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        string? correlationId,
        CreateIntakeReviewRequest request,
        CancellationToken cancellationToken);

    Task<IntakeReviewResponse> AssignAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        AssignIntakeReviewRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IntakeReviewResponse> ClaimAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        ReviewVersionRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IntakeReviewResponse> UnassignAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        ReviewVersionRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IntakeReviewResponse> AddCorrectionAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        AddReviewCorrectionRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IntakeReviewResponse> DecideMatchAsync(
        Guid tenantId,
        Guid reviewId,
        string entityType,
        Guid actorUserId,
        ReviewMatchDecisionRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IntakeReviewResponse> DecideDuplicateAsync(
        Guid tenantId,
        Guid reviewId,
        Guid signalId,
        Guid actorUserId,
        ReviewDuplicateDecisionRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IntakeReviewResponse> DecideFindingAsync(
        Guid tenantId,
        Guid reviewId,
        Guid findingId,
        Guid actorUserId,
        ReviewFindingDecisionRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IntakeReviewResponse> CompleteAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        CompleteIntakeReviewRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<ReviewedIntakeProjectionResponse> GetEffectiveAsync(
        Guid tenantId,
        Guid reviewId,
        CancellationToken cancellationToken);
}