namespace Liens.Application.DTOs;

public sealed class UpdateCaseRequest
{
    public string ClientFirstName { get; init; } = string.Empty;
    public string ClientLastName { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public string? Title { get; init; }
    public DateOnly? ClientDob { get; init; }
    public string? ClientPhone { get; init; }
    public string? ClientEmail { get; init; }
    public string? ClientAddress { get; init; }
    public DateOnly? DateOfIncident { get; init; }
    public string? InsuranceCarrier { get; init; }
    public string? PolicyNumber { get; init; }
    public string? ClaimNumber { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public string? Status { get; init; }
    public decimal? DemandAmount { get; init; }
    public decimal? SettlementAmount { get; init; }
    public string? Sex { get; init; }
    public string? CaseType { get; init; }
    public string? CurrentMedicalStatus { get; init; }
    public string? StateOfIncident { get; init; }
    public DateOnly? TrackingFollowUpDate { get; init; }
    public string? LeadId { get; init; }
    public string? ShareCase { get; init; }
    public string? MinorComp { get; init; }
    public string? CaseDropped { get; init; }
    public string? ChildSupportLiens { get; init; }
    public string? IsUccFiled { get; init; }
    public string? LawFirmId { get; init; }
    public string? AccidentTypeId { get; init; }
    public string? CaseManagerId { get; init; }
    public string? AttorneyId { get; init; }
    public string? SwitchedDate { get; init; }
    public string? StatusLabel { get; init; }
}
