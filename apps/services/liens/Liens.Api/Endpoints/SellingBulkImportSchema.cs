namespace Liens.Api.Endpoints;

internal static class SellingBulkImportSchema
{
    public const string CaseCode = "Case Code*";
    public const string LienStatus = "Lien Status";
    public const string ListingVisibility = "Listing Visibility";
    public const string PurchaseDate = "Purchase Date";
    public const string InitialServiceDate = "Initial Service Date*";
    public const string EndServiceDate = "End Service Date";
    public const string LienNotes = "Lien Notes";
    public const string FundingCompany = "Funding Company";
    public const string FacilityName = "Facility Name*";
    public const string MedicalProvider = "Medical Provider";
    public const string MedicalCodeAndDescription = "Medical Code & Description*";
    public const string MedicareCost = "Medicare Cost";
    public const string BillingAmount = "Billing Amount*";
    public const string TargetAskAmount = "Target Ask Amount";

    public static readonly string[] Columns =
    [
        CaseCode,
        LienStatus,
        ListingVisibility,
        PurchaseDate,
        InitialServiceDate,
        EndServiceDate,
        LienNotes,
        FundingCompany,
        FacilityName,
        MedicalProvider,
        MedicalCodeAndDescription,
        MedicareCost,
        BillingAmount,
        TargetAskAmount,
    ];

    public static readonly string[] Example =
    [
        "CASE-10001",
        "Pending",
        "Private",
        "01/15/2026",
        "01/10/2026",
        "01/12/2026",
        "Example selling lien import",
        "Example Funding Co.",
        "Example Medical Center",
        "Example Medical Provider",
        "99213 - Office visit",
        "82.00",
        "250.00",
        "175.00",
    ];

    public static string? GetValue(
        IReadOnlyDictionary<string, string> values,
        string canonicalName,
        params string[] legacyAliases)
    {
        foreach (var name in new[] { canonicalName }.Concat(legacyAliases))
        {
            var value = values.FirstOrDefault(pair =>
                string.Equals(pair.Key.Trim(), name, StringComparison.OrdinalIgnoreCase)).Value?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
