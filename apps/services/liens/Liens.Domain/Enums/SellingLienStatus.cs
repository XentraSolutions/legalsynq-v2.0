namespace Liens.Domain.Enums;

public static class SellingLienStatus
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Internal = "Internal";
    public const string PreparedForSale = "PreparedForSale";
    public const string SubmittedForSale = "SubmittedForSale";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";
    public const string Sold = "Sold";
    public const string Withdrawn = "Withdrawn";
    public const string Archived = "Archived";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Draft,
        Pending,
        Internal,
        PreparedForSale,
        SubmittedForSale,
        Accepted,
        Declined,
        Sold,
        Withdrawn,
        Archived,
    };
}
