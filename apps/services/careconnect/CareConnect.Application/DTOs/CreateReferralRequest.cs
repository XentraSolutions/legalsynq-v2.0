namespace CareConnect.Application.DTOs;

public class CreateReferralRequest
{
    public Guid ProviderId { get; set; }
    public string ClientFirstName { get; set; } = string.Empty;
    public string ClientLastName { get; set; } = string.Empty;
    public DateTime? ClientDob { get; set; }
    public string ClientPhone { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string? CaseNumber { get; set; }
    public string RequestedService { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
