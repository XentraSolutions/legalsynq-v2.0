namespace Liens.Domain.Enums;

public static class ContactType
{
    public const string LawFirm      = "LawFirm";
    public const string Provider     = "Provider";
    public const string MedicalFacility = "MedicalFacility";
    public const string LienHolder   = "LienHolder";
    public const string FundingCompany = "FundingCompany";
    public const string CaseManager  = "CaseManager";
    public const string InternalUser = "InternalUser";
    public const string Lead         = "Lead";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        LawFirm, Provider, MedicalFacility, LienHolder, FundingCompany, CaseManager, InternalUser, Lead
    };
}
