namespace Liens.Application.DTOs;

public sealed class CaseResponse
{
    public Guid Id { get; init; }
    public string CaseNumber { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public string? Title { get; init; }
    public string ClientFirstName { get; init; } = string.Empty;
    public string ClientLastName { get; init; } = string.Empty;
    public string ClientDisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateOnly? DateOfIncident { get; init; }
    public DateOnly? ClientDob { get; init; }
    public string? ClientPhone { get; init; }
    public string? ClientEmail { get; init; }
    public string? ClientAddress { get; init; }
    public string? ClientStreetAddress { get; init; }
    public string? ClientCity { get; init; }
    public string? ClientState { get; init; }
    public string? ClientZipcode { get; init; }
    public string? InsuranceCarrier { get; init; }
    public string? PolicyNumber { get; init; }
    public string? ClaimNumber { get; init; }
    public decimal? DemandAmount { get; init; }
    public decimal? SettlementAmount { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public string? Sex { get; init; }
    public string? CaseType { get; init; }
    public string? CurrentMedicalStatus { get; init; }
    public string? StateOfIncident { get; init; }
    public DateOnly? TrackingFollowUpDate { get; init; }
    public string? LeadId { get; init; }
    public string? LawFirmId { get; init; }
    public string? LawFirm { get; init; }
    public string? CaseManagerId { get; init; }
    public string? CaseManager { get; init; }
    public string? AccidentTypeId { get; init; }
    public string? AccidentType { get; init; }
    public DateTime? OpenedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
