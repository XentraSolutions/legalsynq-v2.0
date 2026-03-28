using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class ReferralNote : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ReferralId { get; private set; }
    public string NoteType { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public bool IsInternal { get; private set; }

    public Referral? Referral { get; private set; }

    private ReferralNote() { }

    public static ReferralNote Create(
        Guid tenantId,
        Guid referralId,
        string noteType,
        string content,
        bool isInternal,
        Guid? createdByUserId)
    {
        return new ReferralNote
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            ReferralId      = referralId,
            NoteType        = noteType,
            Content         = content.Trim(),
            IsInternal      = isInternal,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = DateTime.UtcNow,
            UpdatedAtUtc    = DateTime.UtcNow
        };
    }

    public void Update(
        string noteType,
        string content,
        bool isInternal,
        Guid? updatedByUserId)
    {
        NoteType        = noteType;
        Content         = content.Trim();
        IsInternal      = isInternal;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
