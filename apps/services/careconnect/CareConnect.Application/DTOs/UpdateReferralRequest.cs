namespace CareConnect.Application.DTOs;

public class UpdateReferralRequest
{
    public string? RequestedService { get; set; }
    public string Urgency { get; set; } = string.Empty;
    // Null = no status change (e.g. treatment-type-only updates). Omit or leave null to preserve current status.
    public string? Status { get; set; }
    public string? Notes { get; set; }
    // Set to true to explicitly clear Notes (sending Notes=null alone is treated as "no change").
    public bool ClearNotes { get; set; }
    // Receiver-only: set Type of Treatment. Null = no change; Guid.Empty = clear.
    public Guid? TreatmentTypeId { get; set; }
}
