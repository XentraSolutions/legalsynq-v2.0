namespace Liens.Domain.Enums;

public static class SellingPortalMessageSenderType
{
    public const string Buyer = "buyer";
    public const string Seller = "seller";

    public static readonly HashSet<string> All =
    [
        Buyer,
        Seller,
    ];
}
