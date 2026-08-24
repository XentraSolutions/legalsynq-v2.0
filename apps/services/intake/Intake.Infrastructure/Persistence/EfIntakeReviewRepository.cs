using Intake.Application.Review;
using Intake.Contracts.Review;
using Intake.Domain.Review;
using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

public sealed class EfIntakeReviewRepository(IntakeDbContext db)
    : IIntakeReviewRepository
{
    public async Task<(IReadOnlyList<IntakeReview> Items, long TotalCount)> ListAsync(
        Guid tenantId,
        IntakeReviewListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Clamp(query.Page, 1, 10_000);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var reviews = db.IntakeReviews.AsNoTracking()
            .Where(review => review.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Status))
            reviews = reviews.Where(review => review.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Priority))
            reviews = reviews.Where(review => review.Priority == query.Priority);
        if (!string.IsNullOrWhiteSpace(query.Disposition))
            reviews = reviews.Where(review => review.B11Disposition == query.Disposition);
        if (!string.IsNullOrWhiteSpace(query.ClassificationCode))
            reviews = reviews.Where(review => review.ClassificationCode == query.ClassificationCode);
        if (!string.IsNullOrWhiteSpace(query.SourceType))
            reviews = reviews.Where(review => review.SourceType == query.SourceType);
        if (query.AssignedToUserId.HasValue)
            reviews = reviews.Where(review => review.AssignedToUserId == query.AssignedToUserId);
        if (query.UnassignedOnly)
            reviews = reviews.Where(review => review.AssignedToUserId == null);
        if (query.CreatedDateFrom.HasValue)
            reviews = reviews.Where(review => review.CreatedAt >= query.CreatedDateFrom);
        if (query.CreatedDateTo.HasValue)
            reviews = reviews.Where(review => review.CreatedAt < query.CreatedDateTo);
        if (query.OlderThanDays is > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-query.OlderThanDays.Value);
            reviews = reviews.Where(review => review.CreatedAt <= cutoff);
        }

        var totalCount = await reviews.LongCountAsync(cancellationToken);
        var items = await reviews
            .OrderByDescending(review =>
                review.Priority == IntakeReviewPriorities.Urgent ? 4 :
                review.Priority == IntakeReviewPriorities.High ? 3 :
                review.Priority == IntakeReviewPriorities.Normal ? 2 : 1)
            .ThenBy(review => review.CreatedAt)
            .ThenBy(review => review.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<IntakeReviewQueueSummaryResponse> GetSummaryAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var query = db.IntakeReviews.AsNoTracking()
            .Where(review => review.TenantId == tenantId);
        var today = DateTimeOffset.UtcNow.Date;
        var highPriorities = new[]
        {
            IntakeReviewPriorities.High,
            IntakeReviewPriorities.Urgent,
        };
        return new IntakeReviewQueueSummaryResponse(
            await query.CountAsync(item => item.Status == IntakeReviewStatuses.Pending, cancellationToken),
            await query.CountAsync(item => item.Status == IntakeReviewStatuses.Assigned, cancellationToken),
            await query.CountAsync(item => item.Status == IntakeReviewStatuses.InReview, cancellationToken),
            await query.CountAsync(
                item => item.Status == IntakeReviewStatuses.Completed &&
                        item.CompletedAt >= today,
                cancellationToken),
            await query.CountAsync(item => highPriorities.Contains(item.Priority), cancellationToken),
            await query.CountAsync(
                item => item.B11Disposition == "DUPLICATE" &&
                        item.Status != IntakeReviewStatuses.Completed,
                cancellationToken),
            await query.CountAsync(
                item => item.B11Disposition == "NO_MATCH" &&
                        item.Status != IntakeReviewStatuses.Completed,
                cancellationToken),
            await query.CountAsync(
                item => item.B11Disposition == "CONFLICTED" &&
                        item.Status != IntakeReviewStatuses.Completed,
                cancellationToken),
            await query.Where(item => item.Status == IntakeReviewStatuses.Pending)
                .OrderBy(item => item.CreatedAt)
                .Select(item => (DateTimeOffset?)item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken));
    }

    public Task<IntakeReview?> FindAsync(
        Guid tenantId,
        Guid reviewId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = db.IntakeReviews
            .Include(review => review.Corrections)
            .Include(review => review.MatchDecisions)
            .Include(review => review.DuplicateDecisions)
            .Include(review => review.FindingDecisions)
            .Include(review => review.Activities)
            .Where(review => review.TenantId == tenantId && review.Id == reviewId);
        if (!track)
            query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(cancellationToken);
    }

    public Task<IntakeReview?> FindActiveByContextAsync(
        Guid tenantId,
        Guid artifactId,
        Guid policyEvaluationId,
        CancellationToken cancellationToken) =>
        db.IntakeReviews
            .AsNoTracking()
            .Where(review =>
                review.TenantId == tenantId &&
                review.ArtifactId == artifactId &&
                review.ArtifactPolicyEvaluationId == policyEvaluationId &&
                review.ActiveContextKey != null)
            .OrderByDescending(review => review.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(
        IntakeReview review,
        CancellationToken cancellationToken)
    {
        db.IntakeReviews.Add(review);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkOpenReviewsSupersededAsync(
        Guid tenantId,
        Guid artifactId,
        Guid currentPolicyEvaluationId,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        var reviews = await db.IntakeReviews
            .Include(review => review.Activities)
            .Where(review =>
                review.TenantId == tenantId &&
                review.ArtifactId == artifactId &&
                review.ArtifactPolicyEvaluationId != currentPolicyEvaluationId &&
                review.Status != IntakeReviewStatuses.Completed &&
                review.Status != IntakeReviewStatuses.Cancelled &&
                review.Status != IntakeReviewStatuses.Superseded)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var review in reviews)
        {
            review.Status = IntakeReviewStatuses.Superseded;
            review.ActiveContextKey = null;
            review.Version++;
            review.UpdatedAt = now;
            review.Activities.Add(new IntakeReviewActivity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                IntakeReviewId = review.Id,
                ActivityType = IntakeReviewActivityTypes.Superseded,
                ActorUserId = actorUserId,
                SafeMetadataJson = $"{{\"policyEvaluationId\":\"{currentPolicyEvaluationId}\"}}",
                CreatedAt = now,
            });
        }
        if (reviews.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}