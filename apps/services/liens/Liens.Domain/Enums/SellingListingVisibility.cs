namespace Liens.Domain.Enums;

public static class SellingListingVisibility
{
    public const string Public = "Public";
    public const string Private = "Private";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Public,
        Private,
    };
}
