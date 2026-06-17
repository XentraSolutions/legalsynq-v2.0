namespace CareConnect.Application.DTOs;

public class ReferralCommentResponse
{
    public Guid Id { get; init; }
    public string SenderType { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public class CreateReferralCommentRequest
{
    public string Message { get; set; } = string.Empty;
}

public class ReferralThreadAttachmentResponse
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
}

public class PublicReferralThreadResponse
{
    public Guid ReferralId { get; init; }
    public Guid TenantId { get; init; }
    public Guid ProviderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public string? ClientPhone { get; init; }
    public string? ClientEmail { get; init; }
    public string? ClientDob { get; init; }
    public string? CaseNumber { get; init; }
    public string Service { get; init; } = string.Empty;
    public string? Urgency { get; init; }
    public string? Notes { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string? ProviderFirstName { get; init; }
    public string? ProviderLastName { get; init; }
    public string ProviderEmail { get; init; } = string.Empty;
    public string ProviderPhone { get; init; } = string.Empty;
    public string ProviderAddressLine1 { get; init; } = string.Empty;
    public string ProviderCity { get; init; } = string.Empty;
    public string ProviderState { get; init; } = string.Empty;
    public string ProviderPostalCode { get; init; } = string.Empty;
    public string? ReferrerFirmName { get; init; }
    public string? ReferrerName { get; init; }
    public string? ReferrerFirstName { get; init; }
    public string? ReferrerLastName { get; init; }
    public string? ReferrerEmail { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool ProviderHasAccount { get; init; }
    public string? DateOfAccident { get; init; }
    public Guid?   TreatmentTypeId   { get; init; }
    public string? TreatmentTypeName { get; init; }
    public IReadOnlyList<ReferralCommentResponse> Comments { get; init; } = [];
    public IReadOnlyList<ReferralThreadAttachmentResponse> Attachments { get; init; } = [];
}
