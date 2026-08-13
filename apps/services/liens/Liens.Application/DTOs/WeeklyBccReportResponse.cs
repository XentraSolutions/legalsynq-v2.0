namespace Liens.Application.DTOs;

public sealed class WeeklyBccReportResult
{
    public DateOnly AsOfDate { get; init; }
    public List<WeeklyBccReportRow> Items { get; init; } = [];
}

public sealed class WeeklyBccReportRow
{
    public string? PlaintiffFirstName { get; init; }
    public string? PlaintiffLastName { get; init; }
    public string? PlaintiffDob { get; init; }
    public string? PlaintiffPhone { get; init; }
    public string? PlaintiffAddress { get; init; }
    public string? PlaintiffCity { get; init; }
    public string? PlaintiffState { get; init; }
    public string? PlaintiffZip { get; init; }
    public string? LienId { get; init; }
    public string? CaseId { get; init; }
    public string? PurchaseDate { get; init; }
    public int? DaysSincePurchase { get; init; }
    public string? PurchaseAmt { get; init; }
    public string? BillingAmt { get; init; }
    public string? ExpectedSettlementAmt { get; init; }
    public string? ReductionPercentage { get; init; }
    public string? CapitalProviders { get; init; }
    public string? DateClosed { get; init; }
    public string? ReturnedAmt { get; init; }
    public string? GrossProfit { get; init; }
    public string? Roi { get; init; }
    public string? AnnualizedRoi { get; init; }
    public int? MedicalCodeCount { get; init; }
    public string? MedicalCodes { get; init; }
    public string? InitialServiceDate { get; init; }
    public string? EndServiceDate { get; init; }
    public string? MedicalProviders { get; init; }
    public string? MedicalFacilityContact { get; init; }
    public string? MedicalFacility { get; init; }
    public string? MedicalFacilityAddress { get; init; }
    public string? MedicalFacilityCity { get; init; }
    public string? MedicalFacilityState { get; init; }
    public string? MedicalFacilityZip { get; init; }
    public string? Noted { get; init; }
    public string? Lawfirm { get; init; }
    public string? LawfirmAddress { get; init; }
    public string? LawfirmCity { get; init; }
    public string? LawfirmState { get; init; }
    public string? LawfirmZip { get; init; }
    public string? LawfirmPhone { get; init; }
    public string? CaseType { get; init; }
    public string? StateOfIncident { get; init; }
    public string? CaseTrackingContact { get; init; }
    public string? CaseTrackingContactEmail { get; init; }
    public string? CaseManager { get; init; }
    public string? AmtToSettlement { get; init; }
    public string? CaseStatus { get; init; }
    public string? MedicalStatus { get; init; }
    public string? CaseTrackingFollowUpDate { get; init; }
    public string? LastActivityDate { get; init; }
    public string? LastActivity { get; init; }
    public string? CaseEnteredBy { get; init; }
    public string? LeadSource { get; init; }
    public string? DateOfLoss { get; init; }
    public string? LastCaseNote { get; init; }
    public string? LastCaseNoteDate { get; init; }
    public string? Reduction { get; init; }
}
