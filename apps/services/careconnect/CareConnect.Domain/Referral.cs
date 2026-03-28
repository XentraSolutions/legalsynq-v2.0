using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class Referral : AuditableEntity
{
    public static class ValidStatuses
    {
        public const string New = "New";
        public const string Received = "Received";
        public const string Contacted = "Contacted";
        public const string Scheduled = "Scheduled";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyList<string> All =
            new[] { New, Received, Contacted, Scheduled, Completed, Cancelled };
    }

    public static class ValidUrgencies
    {
        public const string Low = "Low";
        public const string Normal = "Normal";
        public const string Urgent = "Urgent";
        public const string Emergency = "Emergency";

        public static readonly IReadOnlyList<string> All =
            new[] { Low, Normal, Urgent, Emergency };
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProviderId { get; private set; }
    public string ClientFirstName { get; private set; } = string.Empty;
    public string ClientLastName { get; private set; } = string.Empty;
    public DateTime? ClientDob { get; private set; }
    public string ClientPhone { get; private set; } = string.Empty;
    public string ClientEmail { get; private set; } = string.Empty;
    public string? CaseNumber { get; private set; }
    public string RequestedService { get; private set; } = string.Empty;
    public string Urgency { get; private set; } = string.Empty;
    public string Status { get; private set; } = ValidStatuses.New;
    public string? Notes { get; private set; }

    public Provider? Provider { get; private set; }

    private Referral() { }

    public static Referral Create(
        Guid tenantId,
        Guid providerId,
        string clientFirstName,
        string clientLastName,
        DateTime? clientDob,
        string clientPhone,
        string clientEmail,
        string? caseNumber,
        string requestedService,
        string urgency,
        string? notes,
        Guid? createdByUserId)
    {
        return new Referral
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderId = providerId,
            ClientFirstName = clientFirstName.Trim(),
            ClientLastName = clientLastName.Trim(),
            ClientDob = clientDob,
            ClientPhone = clientPhone.Trim(),
            ClientEmail = clientEmail.Trim(),
            CaseNumber = caseNumber?.Trim(),
            RequestedService = requestedService.Trim(),
            Urgency = urgency,
            Status = ValidStatuses.New,
            Notes = notes?.Trim(),
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void Update(string requestedService, string urgency, string status, string? notes, Guid? updatedByUserId)
    {
        RequestedService = requestedService.Trim();
        Urgency = urgency;
        Status = status;
        Notes = notes?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
