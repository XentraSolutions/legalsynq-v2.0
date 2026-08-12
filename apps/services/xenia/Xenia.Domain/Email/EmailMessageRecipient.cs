namespace Xenia.Domain.Email;

/// <summary>
/// Normalized recipient record for an ingested email message.
///
/// Bcc recipients are stored only when supplied by the provider.
/// Do not expose Bcc in public read APIs unless explicitly authorized.
/// </summary>
public sealed class EmailMessageRecipient
{
    public const int AddressMaxLength     = 320;
    public const int DisplayNameMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmailMessageId { get; private set; }
    public EmailRecipientType RecipientType { get; private set; }

    /// <summary>Normalized, lower-cased email address.</summary>
    public string EmailAddress { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private EmailMessageRecipient() { }

    public static EmailMessageRecipient Create(
        Guid tenantId,
        Guid emailMessageId,
        EmailRecipientType recipientType,
        string emailAddress,
        string? displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        return new EmailMessageRecipient
        {
            Id             = Guid.CreateVersion7(),
            TenantId       = tenantId,
            EmailMessageId = emailMessageId,
            RecipientType  = recipientType,
            EmailAddress   = emailAddress.Trim().ToLowerInvariant(),
            DisplayName    = displayName?.Trim(),
            CreatedAtUtc   = DateTime.UtcNow,
        };
    }
}
