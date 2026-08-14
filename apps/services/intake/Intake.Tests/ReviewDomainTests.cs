using Intake.Domain.Review;
using Xunit;

namespace Intake.Tests;

public sealed class ReviewDomainTests
{
    [Fact]
    public void New_review_starts_pending_with_an_optimistic_version()
    {
        var review = new IntakeReview();

        Assert.Equal(IntakeReviewStatuses.Pending, review.Status);
        Assert.Equal(IntakeReviewPriorities.Normal, review.Priority);
        Assert.Equal(1, review.Version);
        Assert.Equal(1, review.RevisionNumber);
        Assert.Empty(review.Corrections);
        Assert.Empty(review.MatchDecisions);
        Assert.Empty(review.DuplicateDecisions);
        Assert.Empty(review.FindingDecisions);
    }

    [Fact]
    public void Review_history_collections_are_append_only_domain_edges()
    {
        var review = new IntakeReview();
        var correction = new IntakeReviewCorrection
        {
            Id = Guid.NewGuid(),
            IntakeReviewId = review.Id,
            FactCode = "LIEN_NUMBER",
            CorrectionType = IntakeReviewCorrectionTypes.ValueCorrection,
        };

        review.Corrections.Add(correction);

        Assert.Single(review.Corrections);
        Assert.Same(correction, review.Corrections.Single());
        Assert.Equal(IntakeReviewStatuses.Pending, review.Status);
    }

    [Fact]
    public void Terminal_status_codes_are_stable()
    {
        Assert.Equal("COMPLETED", IntakeReviewStatuses.Completed);
        Assert.Equal("CANCELLED", IntakeReviewStatuses.Cancelled);
        Assert.Equal("SUPERSEDED", IntakeReviewStatuses.Superseded);
        Assert.Equal("APPROVED_WITH_CORRECTIONS", IntakeReviewOutcomes.ApprovedWithCorrections);
    }
}