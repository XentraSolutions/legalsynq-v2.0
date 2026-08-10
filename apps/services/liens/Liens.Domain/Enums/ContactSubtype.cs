namespace Liens.Domain.Enums;

public static class ContactSubtype
{
    public const string FacilityContactPerson = "FacilityContactPerson";
    public const string LawFirmCaseManager = "CaseManager";
    public const string LawFirmAttorney = "Attorney";
    public const string LawFirmOther = "Other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        FacilityContactPerson,
        LawFirmCaseManager,
        LawFirmAttorney,
        LawFirmOther,
    };

    public static readonly IReadOnlySet<string> LawFirmRoles = new HashSet<string>
    {
        LawFirmCaseManager,
        LawFirmAttorney,
        LawFirmOther,
    };
}
