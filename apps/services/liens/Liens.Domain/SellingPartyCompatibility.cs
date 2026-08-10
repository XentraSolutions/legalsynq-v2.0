namespace Liens.Domain;

public static class SellingPartyAliasNamespaces
{
    public const string LegacyContact = "LegacyContact";
    public const string LegacyFacility = "LegacyFacility";
    public const string IdentityOrganization = "IdentityOrganization";
}

public static class SellingPartyAliasScopes
{
    public const string Tenant = "Tenant";
    public const string Organization = "Organization";
}

public static class SellingPartyWorkflows
{
    public const string CompanyDirectory = "CompanyDirectory";
    public const string SellingCaseInformation = "SellingCaseInformation";
    public const string SellingPreparation = "SellingPreparation";
    public const string SellingBuyerAccess = "SellingBuyerAccess";
    public const string LegacyFacility = "LegacyFacility";
    public const string LegacyContact = "LegacyContact";
}

public static class SellingPartyBackfillStatuses
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
