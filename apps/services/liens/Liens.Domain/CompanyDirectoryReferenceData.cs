namespace Liens.Domain;

public static class CompanyDirectoryReferenceData
{
    public static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly DateTime SeededAtUtc = new(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Guid LawFirmId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid FundingCompanyId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid MedicalProviderId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid MedicalFacilityId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    public static IReadOnlyList<CompanyTypeSeed> CompanyTypes { get; } =
    [
        new(LawFirmId, "LawFirm", "Law Firm", 1),
        new(FundingCompanyId, "FundingCompany", "Funding Company", 2),
        new(MedicalProviderId, "MedicalProvider", "Medical Provider", 3),
        new(MedicalFacilityId, "MedicalFacility", "Medical Facility", 4),
    ];

    public static IReadOnlyList<ContactPersonTypeSeed> ContactPersonTypes { get; } =
    [
        Role(1, LawFirmId, "Attorney", "Attorney", 1),
        Role(2, LawFirmId, "Paralegal", "Paralegal", 2),
        Role(3, LawFirmId, "CaseManager", "Case Manager", 3),
        Role(4, LawFirmId, "IntakeSpecialist", "Intake Specialist", 4),
        Role(5, LawFirmId, "LegalAssistant", "Legal Assistant", 5),
        Role(6, LawFirmId, "BillingSpecialist", "Billing Specialist", 6),
        Role(7, LawFirmId, "FirmAdministrator", "Firm Administrator", 7),
        Role(8, FundingCompanyId, "Underwriter", "Underwriter", 1),
        Role(9, FundingCompanyId, "FundingSpecialist", "Funding Specialist", 2),
        Role(10, FundingCompanyId, "AccountManager", "Account Manager", 3),
        Role(11, FundingCompanyId, "CollectionsSpecialist", "Collections Specialist", 4),
        Role(12, FundingCompanyId, "ComplianceOfficer", "Compliance Officer", 5),
        Role(13, FundingCompanyId, "FinanceManager", "Finance Manager", 6),
        Role(14, FundingCompanyId, "CompanyAdministrator", "Company Administrator", 7),
        Role(15, MedicalProviderId, "Physician", "Physician", 1),
        Role(16, MedicalProviderId, "Chiropractor", "Chiropractor", 2),
        Role(17, MedicalProviderId, "Therapist", "Therapist", 3),
        Role(18, MedicalProviderId, "NursePractitioner", "Nurse Practitioner", 4),
        Role(19, MedicalProviderId, "ProviderRepresentative", "Provider Representative", 5),
        Role(20, MedicalProviderId, "BillingSpecialist", "Billing Specialist", 6),
        Role(21, MedicalProviderId, "MedicalRecordsCoordinator", "Medical Records Coordinator", 7),
        Role(22, MedicalFacilityId, "FacilityAdministrator", "Facility Administrator", 1),
        Role(23, MedicalFacilityId, "PracticeManager", "Practice Manager", 2),
        Role(24, MedicalFacilityId, "FrontDeskIntakeStaff", "Front Desk/Intake Staff", 3),
        Role(25, MedicalFacilityId, "Scheduler", "Scheduler", 4),
        Role(26, MedicalFacilityId, "CareCoordinator", "Care Coordinator", 5),
        Role(27, MedicalFacilityId, "BillingSpecialist", "Billing Specialist", 6),
        Role(28, MedicalFacilityId, "MedicalRecordsSpecialist", "Medical Records Specialist", 7),
    ];

    private static ContactPersonTypeSeed Role(int sequence, Guid companyTypeId, string code, string name, int sortOrder)
        => new(Guid.Parse($"20000000-0000-0000-0000-{sequence:000000000000}"), companyTypeId, code, name, sortOrder);

    public sealed record CompanyTypeSeed(Guid Id, string Code, string Name, int SortOrder);
    public sealed record ContactPersonTypeSeed(Guid Id, Guid CompanyTypeId, string Code, string Name, int SortOrder);
}
