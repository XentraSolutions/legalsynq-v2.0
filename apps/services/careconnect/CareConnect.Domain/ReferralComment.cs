namespace CareConnect.Domain;

public class ReferralComment
{
    public Guid   Id          { get; set; } = Guid.CreateVersion7();
    public Guid   TenantId    { get; set; }
    public Guid   ReferralId  { get; set; }

    /// <summary>"referrer" | "provider" | "system"</summary>
    public string SenderType  { get; set; } = string.Empty;

    public string SenderName  { get; set; } = string.Empty;
    public string Message     { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ReferralAttachment> Attachments { get; set; } = [];
}
