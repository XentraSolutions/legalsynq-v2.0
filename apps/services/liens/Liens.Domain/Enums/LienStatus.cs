namespace Liens.Domain.Enums;

public static class LienStatus
{
    public const string Draft      = "Draft";
    public const string Offered    = "Offered";
    public const string UnderReview = "UnderReview";
    public const string Sold       = "Sold";
    public const string Active     = "Active";
    public const string Settled    = "Settled";
    public const string Withdrawn  = "Withdrawn";
    public const string Cancelled  = "Cancelled";
    public const string Disputed   = "Disputed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Draft, Offered, UnderReview, Sold, Active, Settled, Withdrawn, Cancelled, Disputed
    };

    public static readonly IReadOnlySet<string> Open = new HashSet<string>
    {
        Draft, Offered, UnderReview, Active, Disputed
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Sold, Settled, Withdrawn, Cancelled
    };
}
