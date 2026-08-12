namespace Liens.Domain.Enums;

public static class SellingBuyerResponseStatus
{
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Accepted,
        Declined,
    };
}
