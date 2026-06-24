namespace Liens.Domain.Enums;

public static class SellingPortfolioStatus
{
    public const string Draft = "DRAFT";
    public const string ReadyForReview = "READY_FOR_REVIEW";
    public const string Published = "PUBLISHED";
    public const string UnderReview = "UNDER_REVIEW";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
    public const string Withdrawn = "WITHDRAWN";
    public const string Closed = "CLOSED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Draft,
        ReadyForReview,
        Published,
        UnderReview,
        Accepted,
        Rejected,
        Withdrawn,
        Closed,
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Withdrawn,
        Closed,
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Draft] = new HashSet<string> { ReadyForReview, Withdrawn },
            [ReadyForReview] = new HashSet<string> { Published, Rejected, Withdrawn },
            [Published] = new HashSet<string> { UnderReview, Withdrawn, Closed },
            [UnderReview] = new HashSet<string> { Accepted, Rejected, Withdrawn },
            [Accepted] = new HashSet<string> { Closed },
            [Rejected] = new HashSet<string> { Draft, Closed },
            [Withdrawn] = new HashSet<string>(),
            [Closed] = new HashSet<string>(),
        };
}
