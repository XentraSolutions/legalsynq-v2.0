namespace CareConnect.Application.DTOs;

public class CreateAppointmentRequest
{
    public Guid ReferralId { get; set; }
    public Guid AppointmentSlotId { get; set; }
    public string? Notes { get; set; }
}

public class AppointmentResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ReferralId { get; init; }
    public Guid ProviderId { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public Guid FacilityId { get; init; }
    public string FacilityName { get; init; } = string.Empty;
    public Guid? ServiceOfferingId { get; init; }
    public string? ServiceOfferingName { get; init; }
    public Guid? AppointmentSlotId { get; init; }
    public DateTime ScheduledStartAtUtc { get; init; }
    public DateTime ScheduledEndAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public class AppointmentSearchParams
{
    public Guid? ReferralId { get; set; }
    public Guid? ProviderId { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
