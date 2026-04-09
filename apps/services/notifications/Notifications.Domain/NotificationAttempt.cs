namespace Notifications.Domain;

public class NotificationAttempt
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int AttemptNumber { get; set; } = 1;
    public string? ProviderMessageId { get; set; }
    public string? ProviderOwnershipMode { get; set; }
    public Guid? ProviderConfigId { get; set; }
    public string? FailureCategory { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsFailover { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
