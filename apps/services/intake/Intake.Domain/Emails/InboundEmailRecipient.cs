namespace Intake.Domain.Emails;

public sealed class InboundEmailRecipient
{
    public Guid Id { get; set; }
    public Guid InboundEmailId { get; set; }
    public InboundEmail? InboundEmail { get; set; }
    public string RecipientType { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string NormalizedEmailAddress { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int Ordinal { get; set; }
}