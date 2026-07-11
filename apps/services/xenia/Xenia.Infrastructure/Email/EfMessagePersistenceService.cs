using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// EF Core implementation of message persistence.
///
/// Process:
/// 1. If duplicate — record observation on existing message and return early.
/// 2. Create EmailMessage + populate from NormalizedMessage.
/// 3. Create EmailMessageRecipient records.
/// 4. SaveChanges atomically.
/// 5. Create EmailAttachmentReference stubs (Pending) — separate save.
/// 6. Return attachment stubs for dispatcher.
///
/// No binary content is stored here.
/// </summary>
internal sealed class EfMessagePersistenceService : IMessagePersistenceService
{
    private readonly XeniaDbContext _db;
    private readonly ILogger<EfMessagePersistenceService> _logger;

    public EfMessagePersistenceService(XeniaDbContext db, ILogger<EfMessagePersistenceService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<MessagePersistenceResult> PersistMessageAsync(
        Guid tenantId,
        Guid emailSourceId,
        EmailProviderType providerType,
        NormalizedMessage message,
        Guid runId,
        DuplicateCheckResult duplicateCheck,
        CancellationToken ct = default)
    {
        try
        {
            if (duplicateCheck.IsDuplicate && duplicateCheck.ExistingMessageId.HasValue)
            {
                // Update last-observed timestamp on duplicate
                var existing = await _db.EmailMessages
                    .FirstOrDefaultAsync(m => m.Id == duplicateCheck.ExistingMessageId.Value, ct);
                if (existing is not null)
                {
                    existing.MarkDuplicate(runId);
                    await _db.SaveChangesAsync(ct);
                }

                return new MessagePersistenceResult
                {
                    Success              = true,
                    MessageId            = duplicateCheck.ExistingMessageId,
                    ImportStatus         = MessageImportStatus.Duplicate,
                    AttachmentReferenceIds = [],
                };
            }

            // Create new message
            var msg = EmailMessage.Create(tenantId, emailSourceId, providerType, message.ProviderMessageId);
            msg.SetAddressing(
                message.Subject, message.FromAddress, message.FromName,
                message.SenderAddress, message.SenderName,
                message.ReplyToAddressesCsv,
                message.InternetMessageId, message.ThreadId, message.ConversationId);
            msg.SetTimestamps(message.SentAt, message.ReceivedAt);
            msg.SetMetadata(message.Importance, message.IsRead, message.HasAttachments, message.AttachmentCount);
            msg.SetBody(message.BodyType, message.BodyText, message.BodyHtml, message.BodyPreview);
            msg.SetHeadersAndMetadata(message.HeadersJson, message.ProviderMetadataJson, message.ContentHash);
            msg.MarkImported(runId);

            _db.EmailMessages.Add(msg);

            // Create recipients
            foreach (var r in message.Recipients)
            {
                _db.EmailMessageRecipients.Add(
                    EmailMessageRecipient.Create(tenantId, msg.Id, r.RecipientType, r.EmailAddress, r.DisplayName));
            }

            await _db.SaveChangesAsync(ct);

            // Create attachment stubs
            var attachmentIds = new List<Guid>();
            foreach (var att in message.Attachments)
            {
                var stub = EmailAttachmentReference.Create(
                    tenantId, msg.Id, att.ProviderAttachmentId,
                    att.FileName, att.MimeType, att.SizeBytes,
                    att.IsInline, att.ContentId);
                _db.EmailAttachmentReferences.Add(stub);
                attachmentIds.Add(stub.Id);
            }

            if (attachmentIds.Count > 0)
                await _db.SaveChangesAsync(ct);

            return new MessagePersistenceResult
            {
                Success              = true,
                MessageId            = msg.Id,
                ImportStatus         = MessageImportStatus.Imported,
                AttachmentReferenceIds = attachmentIds,
            };
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Race condition — another worker persisted this message concurrently
            _logger.LogDebug(ex, "Concurrent duplicate detected for tenantId={TenantId}", tenantId);
            return new MessagePersistenceResult
            {
                Success      = true,
                ImportStatus = MessageImportStatus.Duplicate,
                AttachmentReferenceIds = [],
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Persistence error for tenantId={TenantId} sourceId={SourceId}", tenantId, emailSourceId);
            return new MessagePersistenceResult
            {
                Success          = false,
                ImportStatus     = MessageImportStatus.Failed,
                ErrorCode        = "PERSISTENCE_ERROR",
                SafeErrorSummary = "Message could not be saved.",
                AttachmentReferenceIds = [],
            };
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message ?? string.Empty;
        return inner.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
}
