using Intake.Contracts.Review;
using Intake.Domain.Review;

namespace Intake.Application.Review;

public interface IIntakeReviewRepository
{
    Task<(IReadOnlyList<IntakeReview> Items, long TotalCount)> ListAsync(
        Guid tenantId,
        IntakeReviewListQuery query,
        CancellationToken cancellationToken);

    Task<IntakeReviewQueueSummaryResponse> GetSummaryAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IntakeReview?> FindAsync(
        Guid tenantId,
        Guid reviewId,
        bool track,
        CancellationToken cancellationToken);

    Task<IntakeReview?> FindActiveByContextAsync(
        Guid tenantId,
        Guid artifactId,
        Guid policyEvaluationId,
        CancellationToken cancellationToken);

    Task AddAsync(IntakeReview review, CancellationToken cancellationToken);

    Task MarkOpenReviewsSupersededAsync(
        Guid tenantId,
        Guid artifactId,
        Guid currentPolicyEvaluationId,
        Guid? actorUserId,
        CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}