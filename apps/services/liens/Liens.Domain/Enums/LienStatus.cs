namespace Liens.Domain.Enums;

public static class LienStatus
{
    public const string Draft     = "Draft";
    public const string Offered   = "Offered";
    public const string Sold      = "Sold";
    public const string Withdrawn = "Withdrawn";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Draft, Offered, Sold, Withdrawn
    };
}
