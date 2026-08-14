namespace Liens.Domain.Entities;

public sealed class SynqLienDocumentAssociation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentReference { get; set; } = string.Empty;
    public string DocumentRole { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid? RelatedCaseId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}