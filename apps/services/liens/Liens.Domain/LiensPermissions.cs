namespace Liens.Domain;

public static class LiensPermissions
{
    public const string ProductCode = "SYNQ_LIENS";

    public const string LienCreate   = "SYNQ_LIENS.lien:create";
    public const string LienOffer    = "SYNQ_LIENS.lien:offer";
    public const string LienReadOwn  = "SYNQ_LIENS.lien:read:own";
    public const string LienBrowse   = "SYNQ_LIENS.lien:browse";
    public const string LienPurchase = "SYNQ_LIENS.lien:purchase";
    public const string LienReadHeld = "SYNQ_LIENS.lien:read:held";
    public const string LienService  = "SYNQ_LIENS.lien:service";
    public const string LienSettle   = "SYNQ_LIENS.lien:settle";
}
